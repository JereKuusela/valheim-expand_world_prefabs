using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Data;
using ExpandWorld.Prefab;
using NUnit.Framework;
using UnityEngine;

namespace ExpandWorldPrefabs.Tests;

// Managed storage/serializer tests; the real PrepareSave snapshot and dirty-chunk
// selection still require a Valheim dedicated-server test.
[NonParallelizable]
public class ServerSideDataSaveTests
{
  private const BindingFlags StaticFields = BindingFlags.Static | BindingFlags.NonPublic;
  private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
  private const ushort AllFlags = 0xFE;

  private static readonly int StorageHash = (int)typeof(ServerSideData).GetField("StorageHash", StaticFields)!.GetValue(null)!;
  private static readonly MethodInfo SerializeMethod = typeof(ServerSideData).GetMethod("Serialize", StaticFields)!;
  private static readonly MethodInfo DeserializeMethod = typeof(ServerSideData).GetMethod("Deserialize", StaticFields)!;
  private static readonly FieldInfo IdField = typeof(ZDO).GetField("m_uid", InstanceFields)!;

  private static readonly Dictionary<ZDOID, BinarySearchDictionary<int, byte[]>> Extra = ZDOExtraData.s_byteArrays;
  private static readonly Dictionary<ZDOID, Dictionary<int, string>> Strings = Store<string>("Strings");
  private static readonly Dictionary<ZDOID, Dictionary<int, float>> Floats = Store<float>("Floats");
  private static readonly Dictionary<ZDOID, Dictionary<int, int>> Ints = Store<int>("Ints");
  private static readonly Dictionary<ZDOID, Dictionary<int, long>> Longs = Store<long>("Longs");
  private static readonly Dictionary<ZDOID, Dictionary<int, Vector3>> Vecs = Store<Vector3>("Vecs");
  private static readonly Dictionary<ZDOID, Dictionary<int, Quaternion>> Quats = Store<Quaternion>("Quats");
  private static readonly Dictionary<ZDOID, Dictionary<int, byte[]>> Bytes = Store<byte[]>("Bytes");

  private readonly List<ZDOID> ids = [];
  private uint next = 1000000;

  private static Dictionary<ZDOID, Dictionary<int, T>> Store<T>(string name) =>
    (Dictionary<ZDOID, Dictionary<int, T>>)typeof(ServerSideData).GetField(name, StaticFields)!.GetValue(null)!;

  private ZDOID NewId()
  {
    var id = new ZDOID(-20358721L, ++next);
    ids.Add(id);
    return id;
  }

  private static ZDO Zdo(ZDOID id)
  {
#pragma warning disable SYSLIB0050
    var zdo = (ZDO)FormatterServices.GetUninitializedObject(typeof(ZDO));
#pragma warning restore SYSLIB0050
    IdField.SetValue(zdo, id);
    return zdo;
  }

  private static byte[]? Serialize(ZDOID id) => (byte[]?)SerializeMethod.Invoke(null, [id]);

  private static BinarySearchDictionary<int, byte[]> Bucket(ZDOID id)
  {
    ZDOHelper.Init(Extra, id);
    return Extra[id];
  }

  [TearDown]
  public void TearDown()
  {
    foreach (var id in ids)
    {
      Strings.Remove(id);
      Floats.Remove(id);
      Ints.Remove(id);
      Longs.Remove(id);
      Vecs.Remove(id);
      Quats.Remove(id);
      Bytes.Remove(id);
      Extra.Remove(id);
    }
    ids.Clear();
  }

  [Test]
  public void EmptyAndMixedPayloads_KeepVersionOneAndAllSevenTypes()
  {
    Assert.That(Serialize(NewId()), Is.Null);
    var id = NewId();
    Floats[id] = new() { [11] = 0.5f };
    Vecs[id] = new() { [12] = new Vector3(1, 2, 3) };
    Quats[id] = new() { [13] = new Quaternion(1, 2, 3, 4) };
    Ints[id] = new() { [14] = 0 }; // Zero is a stored value, not an absent key.
    Longs[id] = new() { [15] = 1234567890123L };
    Strings[id] = new() { [16] = "test" };
    Bytes[id] = new() { [17] = [] }; // Empty byte[] is still present.

    var payload = Serialize(id);
    Assert.That(payload, Is.Not.Null.And.Not.Empty);
    var pkg = new ZPackage(payload!);
    Assert.That(pkg.ReadInt(), Is.EqualTo((1 << 12) | AllFlags));
    Assert.That(pkg.ReadInt(), Is.EqualTo(1)); // float
    Assert.That(pkg.ReadInt(), Is.EqualTo(11));
    Assert.That(pkg.ReadSingle(), Is.EqualTo(0.5f));
    Assert.That(pkg.ReadInt(), Is.EqualTo(1)); // vec3
    Assert.That(pkg.ReadInt(), Is.EqualTo(12));
    Assert.That(pkg.ReadVector3(), Is.EqualTo(new Vector3(1, 2, 3)));
    Assert.That(pkg.ReadInt(), Is.EqualTo(1)); // quat
    Assert.That(pkg.ReadInt(), Is.EqualTo(13));
    Assert.That(pkg.ReadQuaternion(), Is.EqualTo(new Quaternion(1, 2, 3, 4)));
    Assert.That(pkg.ReadInt(), Is.EqualTo(1)); // int
    Assert.That(pkg.ReadInt(), Is.EqualTo(14));
    Assert.That(pkg.ReadInt(), Is.Zero);
    Assert.That(pkg.ReadInt(), Is.EqualTo(1)); // long
    Assert.That(pkg.ReadInt(), Is.EqualTo(15));
    Assert.That(pkg.ReadLong(), Is.EqualTo(1234567890123L));
    Assert.That(pkg.ReadInt(), Is.EqualTo(1)); // string
    Assert.That(pkg.ReadInt(), Is.EqualTo(16));
    Assert.That(pkg.ReadString(), Is.EqualTo("test"));
    Assert.That(pkg.ReadInt(), Is.EqualTo(1)); // byte[]
    Assert.That(pkg.ReadInt(), Is.EqualTo(17));
    Assert.That(pkg.ReadByteArray(), Is.Empty);
  }

  [Test]
  public void VersionOnePayload_DeserializesAndRemovesLiveStorageHash()
  {
    var source = NewId();
    var restored = NewId();
    Strings[source] = new() { [51] = "persist" };
    Ints[source] = new() { [52] = 0 };
    Bytes[source] = new() { [53] = new byte[] { 1, 4 } };
    var oldPayload = Serialize(source)!;
    Bucket(restored).SetValue(StorageHash, oldPayload);

    DeserializeMethod.Invoke(null, [Zdo(restored)]);
    Assert.That(ServerSideData.TryGetString(restored, 51, out var str), Is.True);
    Assert.That(str, Is.EqualTo("persist"));
    Assert.That(ServerSideData.TryGetInt(restored, 52, out var integer), Is.True);
    Assert.That(integer, Is.Zero);
    Assert.That(ServerSideData.TryGetBytes(restored, 53, out var bytes), Is.True);
    Assert.That(bytes, Is.EqualTo(new byte[] { 1, 4 }));
    Assert.That(Extra.ContainsKey(restored) && Extra[restored].ContainsKey(StorageHash), Is.False);
  }

  [Test]
  public void GetSaveData_TargetHasExpectedSaveByteArrayParameter()
  {
    var method = typeof(ZDOExtraData).GetMethod("GetSaveData", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
    Assert.That(method, Is.Not.Null);
    var parameters = method!.GetParameters();
    Assert.That(parameters.Length, Is.GreaterThan(7));
    Assert.That(parameters[0].ParameterType, Is.EqualTo(typeof(ZDOID)));
    Assert.That(parameters[7].ParameterType, Is.EqualTo(typeof(List<KeyValuePair<int, byte[]>>).MakeByRefType()));
  }

  [Test]
  public void Snapshot_ContainsOnlyEligiblePersistentObjects()
  {
    var included = NewId();
    var ignored = NewId();
    Ints[included] = new() { [21] = 7 };
    Ints[ignored] = new() { [22] = 8 };

    HashSet<ZDOID> skipped = [];
    var snapshot = ServerSideData.BuildSaveSnapshot(id => id == included, skipped);
    Assert.That(snapshot.Keys, Is.EquivalentTo(new[] { included }));
    Assert.That(skipped, Does.Contain(ignored).And.Not.Contain(included));
    Assert.That(snapshot[included], Is.EqualTo(Serialize(included)));
    Assert.That(Extra.ContainsKey(included), Is.False);
    Assert.That(Extra.ContainsKey(ignored), Is.False);
  }

  [Test]
  public void GetSaveData_AppendsPayloadWithoutMutatingLiveExtraData()
  {
    var id = NewId();
    Ints[id] = new() { [21] = 7 };
    var snapshot = ServerSideData.BuildSaveSnapshot(_ => true, []);
    List<KeyValuePair<int, byte[]>>? saveBytes = [new(5, [12])];
    ServerSideData.AddSavePayload(snapshot, id, ref saveBytes);

    Assert.That(saveBytes!.Count, Is.EqualTo(2));
    Assert.That(saveBytes![0].Key, Is.EqualTo(5));
    Assert.That(saveBytes![1].Key, Is.EqualTo(StorageHash));
    Assert.That(saveBytes![1].Value, Is.SameAs(snapshot[id]));
    Assert.That(Extra.ContainsKey(id), Is.False);
  }

  [Test]
  public void GetSaveData_ReplacesOldHashInOutputButPreservesLiveValue()
  {
    var id = NewId();
    var original = new byte[] { 7, 8, 9 };
    Bucket(id).SetValue(StorageHash, original);
    Ints[id] = new() { [24] = 101 };
    var snapshot = ServerSideData.BuildSaveSnapshot(_ => true, []);
    List<KeyValuePair<int, byte[]>>? saveBytes = [new(StorageHash, original), new(9, [5])];

    ServerSideData.AddSavePayload(snapshot, id, ref saveBytes);
    Assert.That(saveBytes!.Count, Is.EqualTo(2));
    Assert.That(saveBytes![0].Value, Is.SameAs(snapshot[id]));
    Assert.That(saveBytes![1].Value, Is.EqualTo(new byte[] { 5 }));
    Assert.That(Extra[id].TryGetValue(StorageHash, out var live), Is.True);
    Assert.That(live, Is.SameAs(original));
  }

  [Test]
  public void PreparedPayload_IsUnaffectedByLaterServerOnlyChanges()
  {
    var id = NewId();
    Ints[id] = new() { [21] = 101 };
    var before = ServerSideData.BuildSaveSnapshot(_ => true, []);
    Ints[id][21] = 202;
    var after = ServerSideData.BuildSaveSnapshot(_ => true, []);
    Assert.That(before[id], Is.Not.EqualTo(after[id]));
    Assert.That(before[id], Is.Not.EqualTo(Serialize(id)));
    List<KeyValuePair<int, byte[]>>? saveBytes = [];
    ServerSideData.AddSavePayload(before, id, ref saveBytes);
    Assert.That(saveBytes![0].Value, Is.SameAs(before[id]));
  }

  [Test]
  public void GetSaveData_LeavesMissingObjectsUntouchedAndCreatesListWhenNeeded()
  {
    var id = NewId();
    var missing = NewId();
    Strings[id] = new() { [25] = "hello" };
    var snapshot = ServerSideData.BuildSaveSnapshot(_ => true, []);
    List<KeyValuePair<int, byte[]>> saveBytes = [];
    ServerSideData.AddSavePayload(snapshot, missing, ref saveBytes);
    Assert.That(saveBytes, Is.Empty);
    ServerSideData.AddSavePayload(snapshot, id, ref saveBytes);
    Assert.That(saveBytes, Has.Count.EqualTo(1));
    Assert.That(saveBytes![0].Key, Is.EqualTo(StorageHash));
    Assert.That(Extra.ContainsKey(id), Is.False);
  }

  [Test]
  public void SetAndRemove_TrackActualValuesAcrossAllSevenStores()
  {
    var id = NewId();
    var zdo = Zdo(id);
    Assert.That(ServerSideData.Set(Strings, zdo, 1, "a"), Is.True);
    Assert.That(ServerSideData.Set(Strings, zdo, 1, "a"), Is.False);
    Assert.That(ServerSideData.Set(Floats, zdo, 2, 1f), Is.True);
    Assert.That(ServerSideData.Set(Floats, zdo, 2, 1f), Is.False);
    Assert.That(ServerSideData.Set(Longs, zdo, 3, 1L), Is.True);
    Assert.That(ServerSideData.Set(Longs, zdo, 3, 1L), Is.False);
    Assert.That(ServerSideData.Set(Vecs, zdo, 4, new Vector3(1, 2, 3)), Is.True);
    Assert.That(ServerSideData.Set(Vecs, zdo, 4, new Vector3(1, 2, 3)), Is.False);
    Assert.That(ServerSideData.Set(Quats, zdo, 5, new Quaternion(1, 2, 3, 4)), Is.True);
    Assert.That(ServerSideData.Set(Quats, zdo, 5, new Quaternion(1, 2, 3, 4)), Is.False);
    Assert.That(ServerSideData.Set(Bytes, zdo, 6, Array.Empty<byte>()), Is.True);
    Assert.That(ServerSideData.Set(Bytes, zdo, 6, Array.Empty<byte>()), Is.False);

    Assert.That(ServerSideData.RemoveFromStore(Strings, zdo, 1), Is.True);
    Assert.That(ServerSideData.RemoveFromStore(Floats, zdo, 2), Is.True);
    Assert.That(ServerSideData.RemoveFromStore(Longs, zdo, 3), Is.True);
    Assert.That(ServerSideData.RemoveFromStore(Vecs, zdo, 4), Is.True);
    Assert.That(ServerSideData.RemoveFromStore(Quats, zdo, 5), Is.True);
    Assert.That(ServerSideData.RemoveFromStore(Bytes, zdo, 6), Is.True);
    Assert.That(ServerSideData.RemoveFromStore(Bytes, zdo, 6), Is.False);
    Assert.That(Bytes.ContainsKey(id), Is.False); // Last key removal drops the inner dictionary.
  }

  [Test]
  public void ServerOnlyScalarWrites_IgnoreUnchangedValuesAndRetainZero()
  {
    var id = NewId();
    var zdo = Zdo(id);
    Assert.That(ServerSideData.Set(Ints, zdo, 30, 0), Is.True);
    Assert.That(ServerSideData.Set(Ints, zdo, 30, 0), Is.False);
    Assert.That(ServerSideData.Set(Ints, zdo, 30, 5), Is.True);
    Assert.That(ServerSideData.Set(Ints, zdo, 30, 0), Is.True);
    Assert.That(ServerSideData.TryGetInt(id, 30, out var value), Is.True);
    Assert.That(value, Is.Zero);
  }

  [Test]
  public void ServerOnlyBytes_UseContentEqualityAndCopyCallerOwnedBuffers()
  {
    var id = NewId();
    var zdo = Zdo(id);
    var input = new byte[] { 1, 2 };
    Assert.That(ServerSideData.Set(Bytes, zdo, 41, input), Is.True);
    input[0] = 9;
    Assert.That(ServerSideData.TryGetBytes(id, 41, out var stored), Is.True);
    Assert.That(stored, Is.EqualTo(new byte[] { 1, 2 }));
    Assert.That(ServerSideData.Set(Bytes, zdo, 41, new byte[] { 1, 2 }), Is.False);
    Assert.That(ServerSideData.Set(Bytes, zdo, 41, input), Is.True);
    Assert.That(ServerSideData.Set(Bytes, zdo, 41, input), Is.False);
    // Direct mutation via the existing TryGetBytes references remains outside Set detection.
    stored = Bytes[id][41];
    stored[0] = 8;
    Assert.That(Bytes[id][41][0], Is.EqualTo(8));
  }
}
