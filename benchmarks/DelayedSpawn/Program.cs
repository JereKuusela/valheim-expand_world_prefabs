using System.Diagnostics;

const int spawnCount = 100_000;
const int ticksPerSecond = 20;
const int repetitions = 5;
var tiers = new (string Name, int FarInterval, int MediumInterval)[]
{
  ("Tiers 50/250", 250_000, 0),
  ("Tiers 50/500", 500_000, 0),
  ("Tiers 50/1000", 1_000_000, 0),
  ("Tiers 50/2000", 2_000_000, 0),
  ("Tiers 50/4000", 4_000_000, 0),
  ("Tiers 50/100/2000", 2_000_000, 100_000),
  ("Tiers 50/250/1000", 1_000_000, 250_000),
  ("Tiers 50/250/2000", 2_000_000, 250_000),
  ("Tiers 50/500/2000", 2_000_000, 500_000),
  ("Tiers 50/500/4000", 4_000_000, 500_000),
};
var strategies = new (string Name, Func<ISpawnQueue> Create)[]
{
  ("List + RemoveAt", () => new ScanQueue()),
  ("List compaction", () => new CompactQueue()),
  ("Stable min-heap", () => new HeapQueue()),
  ("50 ms buckets", () => new BucketQueue()),
};
var comparisons = strategies.Concat(tiers.Select(tier =>
  (tier.Name, Create: (Func<ISpawnQueue>)(() => new TieredQueue(tier.FarInterval, tier.MediumInterval))))).ToArray();

foreach (var (name, farInterval, mediumInterval) in tiers)
{
  var boundarySpawns = new[] { mediumInterval, farInterval, farInterval + mediumInterval, 2 * farInterval }
    .Where(boundary => boundary > 0).Distinct()
    .SelectMany(boundary => new[] { 1, -1, 0 }.Select(offset => boundary + offset))
    .Select((due, index) => new Spawn(index, due)).ToArray();
  if (Run(() => new TieredQueue(farInterval, mediumInterval), boundarySpawns, boundarySpawns.Max(spawn => spawn.DueTick)).ReorderedTicks != 0)
    throw new InvalidOperationException($"{name} reordered the boundary workload.");
}

const int seconds = 10;
var workloads = new (bool Stepped, bool Ordered, bool SingleDeadline)[]
{
  (false, false, false), (false, true, false), (true, false, false), (true, true, false),
  (false, false, true),
};
foreach (var (stepped, ordered, singleDeadline) in workloads)
{
  var ticks = seconds * ticksPerSecond;
  var lastDeadline = ticks * Spawn.TickMicroseconds;
  var random = new Random(42);
  var delays = singleDeadline ? Enumerable.Repeat(lastDeadline, spawnCount) : Enumerable.Range(0, spawnCount).Select(_ => stepped
    ? random.Next(1, seconds + 1) * 1_000_000
    : random.Next(1, lastDeadline + 1));
  if (ordered)
    delays = delays.Order();
  var spawns = delays.Select((due, index) => new Spawn(index, due)).ToArray();
  var scenario = singleDeadline ? $"all due at {seconds} s" : $"{(stepped ? "1 s steps" : "uniform delays")}, {(ordered ? "ascending deadlines" : "random order")}";
  Console.WriteLine($"\n{spawnCount:N0} spawns, {seconds} simulated seconds at {ticksPerSecond} ticks/s, {scenario}");
  var waitColumns = singleDeadline ? $" {"Wait ms",9} {"Due tick",9}" : "";
  Console.WriteLine($"{"Queue",-19} {"Add ms",8} {"Drain ms",9} {"P95 tick",9} {"Max tick",9} {"Alloc KiB",10} {"Reordered ticks",15}{waitColumns}");
  foreach (var (name, create) in comparisons)
  {
    Run(create, spawns, ticks);
    var samples = Enumerable.Range(0, repetitions).Select(_ => Run(create, spawns, ticks)).ToArray();
    var tickTimes = samples.SelectMany(sample => sample.TickTimes).Order().ToArray();
    var p95 = tickTimes[(int)Math.Ceiling(tickTimes.Length * 0.95) - 1];
    var addMs = Median(samples.Select(sample => sample.AddTime));
    var drainMs = Median(samples.Select(sample => sample.TickTimes.Sum()));
    var allocKiB = Median(samples.Select(sample => (double)sample.AllocatedBytes)) / 1024;
    var waitStats = singleDeadline
      ? $" {Median(samples.Select(sample => sample.TickTimes.Take(ticks - 1).Sum())),9:F3} {Median(samples.Select(sample => sample.TickTimes[^1])),9:F3}"
      : "";
    Console.WriteLine($"{name,-19} {addMs,8:F2} {drainMs,9:F2} {p95,9:F3} {tickTimes[^1],9:F3} {allocKiB,10:F0} {samples[0].ReorderedTicks,15}{waitStats}");
  }
}

static double Median(IEnumerable<double> values)
{
  var sorted = values.Order().ToArray();
  return sorted[sorted.Length / 2];
}

static Sample Run(Func<ISpawnQueue> create, Spawn[] spawns, int ticks)
{
  var output = new int[spawns.Length];
  var tickEnds = new int[ticks];
  var tickTimes = new double[ticks];
  var count = 0;
  var before = GC.GetAllocatedBytesForCurrentThread();
  var start = Stopwatch.GetTimestamp();
  var queue = create();
  foreach (var spawn in spawns)
    queue.Add(spawn);
  var addTime = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
  for (var tick = 0; tick < ticks; tick++)
  {
    start = Stopwatch.GetTimestamp();
    count += queue.Drain((tick + 1) * Spawn.TickMicroseconds, output.AsSpan(count));
    tickTimes[tick] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    tickEnds[tick] = count;
  }
  var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
  if (count != spawns.Length)
    throw new InvalidOperationException($"Expected {spawns.Length} spawns, got {count}.");

  var seen = new bool[spawns.Length];
  var reorderedTicks = 0;
  var offset = 0;
  for (var tick = 0; tick < ticks; tick++)
  {
    var previous = -1;
    var reordered = false;
    for (; offset < tickEnds[tick]; offset++)
    {
      var id = output[offset];
      if (seen[id] || spawns[id].DueTick != tick + 1)
        throw new InvalidOperationException($"Spawn {id} duplicated or executed on the wrong tick ({tick + 1}).");
      seen[id] = true;
      reordered |= id < previous;
      previous = id;
    }
    if (reordered)
      reorderedTicks++;
  }
  return new(addTime, tickTimes, allocatedBytes, reorderedTicks);
}

sealed record Sample(double AddTime, double[] TickTimes, long AllocatedBytes, int ReorderedTicks);

sealed record Spawn(int Id, int Due)
{
  public const int TickMicroseconds = 50_000;
  public int DueTick => (Due - 1) / TickMicroseconds + 1;
}

interface ISpawnQueue
{
  void Add(Spawn spawn);
  int Drain(int now, Span<int> output);
}

sealed class ScanQueue : ISpawnQueue
{
  private readonly List<Spawn> Spawns = [];

  public void Add(Spawn spawn) => Spawns.Add(spawn);

  public int Drain(int now, Span<int> output)
  {
    var count = 0;
    for (var index = 0; index < Spawns.Count; index++)
    {
      var spawn = Spawns[index];
      if (spawn.Due > now) continue;
      Spawns.RemoveAt(index);
      index--;
      output[count++] = spawn.Id;
    }
    return count;
  }
}

sealed class CompactQueue : ISpawnQueue
{
  private readonly List<Spawn> Spawns = [];

  public void Add(Spawn spawn) => Spawns.Add(spawn);

  public int Drain(int now, Span<int> output) => Drain(Spawns, now, output);

  public static int Drain(List<Spawn> spawns, int now, Span<int> output)
  {
    var count = 0;
    var pending = 0;
    for (var index = 0; index < spawns.Count; index++)
    {
      var spawn = spawns[index];
      if (spawn.Due <= now)
        output[count++] = spawn.Id;
      else
      {
        if (pending != index)
          spawns[pending] = spawn;
        pending++;
      }
    }
    spawns.RemoveRange(pending, count);
    return count;
  }
}

sealed class HeapQueue : ISpawnQueue
{
  private readonly PriorityQueue<Spawn, (int Due, int Id)> Spawns = new();

  public void Add(Spawn spawn) => Spawns.Enqueue(spawn, (spawn.Due, spawn.Id));

  public int Drain(int now, Span<int> output)
  {
    var count = 0;
    while (Spawns.TryPeek(out var spawn, out _) && spawn.Due <= now)
      output[count++] = Spawns.Dequeue().Id;
    return count;
  }
}

sealed class BucketQueue : ISpawnQueue
{
  private readonly SortedDictionary<int, List<Spawn>> Buckets = [];

  public void Add(Spawn spawn)
  {
    if (!Buckets.TryGetValue(spawn.DueTick, out var bucket))
      Buckets.Add(spawn.DueTick, bucket = []);
    bucket.Add(spawn);
  }

  public int Drain(int now, Span<int> output)
  {
    if (!Buckets.Remove(now / Spawn.TickMicroseconds, out var bucket)) return 0;
    for (var index = 0; index < bucket.Count; index++)
      output[index] = bucket[index].Id;
    return bucket.Count;
  }
}

sealed class TieredQueue : ISpawnQueue
{
  private readonly int FarInterval;
  private readonly int MediumInterval;
  private readonly List<Spawn> Near = [];
  private readonly List<Spawn>? Medium;
  private readonly List<Spawn> Far = [];
  private int Now;
  private int NextMedium;
  private int NextFar;

  public TieredQueue(int farInterval, int mediumInterval)
  {
    FarInterval = NextFar = farInterval;
    MediumInterval = NextMedium = mediumInterval;
    Medium = mediumInterval > 0 ? [] : null;
  }

  public void Add(Spawn spawn)
  {
    if (spawn.Due <= Now + (Medium == null ? FarInterval : MediumInterval))
      Near.Add(spawn);
    else if (Medium != null && spawn.Due <= Now + FarInterval)
      Medium.Add(spawn);
    else
      Far.Add(spawn);
  }

  public int Drain(int now, Span<int> output)
  {
    Now = now;
    if (now >= NextFar)
    {
      Promote(Far, Medium ?? Near, now + FarInterval);
      NextFar = now - now % FarInterval + FarInterval;
    }
    if (Medium != null && now >= NextMedium)
    {
      Promote(Medium, Near, now + MediumInterval);
      NextMedium = now - now % MediumInterval + MediumInterval;
    }
    return CompactQueue.Drain(Near, now, output);
  }

  private static void Promote(List<Spawn> source, List<Spawn> target, int cutoff)
  {
    var pending = 0;
    for (var index = 0; index < source.Count; index++)
    {
      var spawn = source[index];
      if (spawn.Due <= cutoff)
        target.Add(spawn);
      else
      {
        if (pending != index)
          source[pending] = spawn;
        pending++;
      }
    }
    source.RemoveRange(pending, source.Count - pending);
  }
}