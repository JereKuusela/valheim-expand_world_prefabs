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
  public void SnapshotInjection_RemovesOnlyItsOwnTemporaryKey()
  {
    var id = NewId();
    Ints[id] = new() { [21] = 7 };
    var payload = Serialize(id)!;
    var state = new ServerSideData.SaveState();
    ServerSideData.InjectPayload(state, id, payload);
    Assert.That(Extra[id].TryGetValue(StorageHash, out var live), Is.True);
    Assert.That(live, Is.SameAs(payload));
    var snapshot = (byte[])live.Clone();

    ServerSideData.CleanupSave(state);
    ServerSideData.CleanupSave(state);
    Assert.That(Extra.ContainsKey(id), Is.False);
    Assert.That(snapshot, Is.EqualTo(payload));
  }

  [Test]
  public void PreExistingHashValue_IsRestoredByIdentity()
  {
    var id = NewId();
    var original = new byte[] { 7, 8, 9 };
    Bucket(id).SetValue(StorageHash, original);
    var state = new ServerSideData.SaveState();
    ServerSideData.InjectPayload(state, id, [1, 2, 3]);
    ServerSideData.CleanupSave(state);
    Assert.That(Extra[id].TryGetValue(StorageHash, out var restored), Is.True);
    Assert.That(restored, Is.SameAs(original));
  }

  [Test]
  public void PartialInjectionAndOriginalExceptionSimulation_RestoresPreviousEntries()
  {
    var id1 = NewId();
    var id2 = NewId();
    var old = new byte[] { 4 };
    Bucket(id1).SetValue(StorageHash, old);
    var state = new ServerSideData.SaveState();
    try
    {
      ServerSideData.InjectPayload(state, id1, [10]);
      ServerSideData.InjectPayload(state, id2, [20]);
      throw new InvalidOperationException("simulated original exception");
    }
    catch (InvalidOperationException e)
    {
      ServerSideData.CleanupSave(state);
      Assert.That(e.Message, Is.EqualTo("simulated original exception"));
    }
    Assert.That(Extra[id1].TryGetValue(StorageHash, out var result), Is.True);
    Assert.That(result, Is.SameAs(old));
    Assert.That(Extra.ContainsKey(id2), Is.False);
  }

  [Test]
  public void AnotherWriterAndOverlappingStates_AreNotBlindlyCleaned()
  {
    var id = NewId();
    var first = new ServerSideData.SaveState();
    var second = new ServerSideData.SaveState();
    ServerSideData.InjectPayload(first, id, [1]);
    ServerSideData.InjectPayload(second, id, [2]);
    ServerSideData.CleanupSave(first); // A different payload now owns the slot.
    Assert.That(Extra[id].TryGetValue(StorageHash, out var live), Is.True);
    Assert.That(live, Is.SameAs(second.Entries[0].Payload));
    ServerSideData.CleanupSave(second);
    ServerSideData.CleanupSave(first);
    Assert.That(Extra.ContainsKey(id), Is.False);

    var third = new ServerSideData.SaveState();
    ServerSideData.InjectPayload(third, id, [3]);
    var foreign = new byte[] { 99 };
    Extra[id].SetValue(StorageHash, foreign);
    ServerSideData.CleanupSave(third);
    Assert.That(Extra[id].TryGetValue(StorageHash, out live), Is.True);
    Assert.That(live, Is.SameAs(foreign));
  }

  [Test]
  public void MutationRecordedBeforeWrite_CleansUpEvenWithoutSuccessFlag()
  {
    var id = NewId();
    var old = new byte[] { 11 };
    var payload = new byte[] { 22 };
    var bucket = Bucket(id);
    bucket.SetValue(StorageHash, old);
    var state = new ServerSideData.SaveState();
    var entry = new ServerSideData.SaveEntry(id, payload, true)
    {
      Bucket = bucket, HadValue = true, Previous = old
    };
    state.Entries.Add(entry); // SetValue has not yet returned to set Written = true.
    bucket.SetValue(StorageHash, payload);

    ServerSideData.CleanupSave(state);
    Assert.That(bucket.TryGetValue(StorageHash, out var result), Is.True);
    Assert.That(result, Is.SameAs(old));
  }

  [Test]
  public void EnterSave_RejectsOverlapWithoutReleasingOtherInvocation()
  {
    var first = new ServerSideData.SaveState();
    var overlap = new ServerSideData.SaveState();
    ServerSideData.EnterSave(first);
    try
    {
      Assert.Throws<InvalidOperationException>(() => ServerSideData.EnterSave(overlap));
      ServerSideData.CleanupSave(overlap);
      Assert.Throws<InvalidOperationException>(() => ServerSideData.EnterSave(overlap));
    }
    finally
    {
      ServerSideData.CleanupSave(first);
    }
    Assert.DoesNotThrow(() => ServerSideData.EnterSave(overlap));
    ServerSideData.CleanupSave(overlap);
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

    Assert.That(ServerSideData.RemoveFromStore(Strings, id, 1), Is.True);
    Assert.That(ServerSideData.RemoveFromStore(Floats, id, 2), Is.True);
    Assert.That(ServerSideData.RemoveFromStore(Longs, id, 3), Is.True);
    Assert.That(ServerSideData.RemoveFromStore(Vecs, id, 4), Is.True);
    Assert.That(ServerSideData.RemoveFromStore(Quats, id, 5), Is.True);
    Assert.That(ServerSideData.RemoveFromStore(Bytes, id, 6), Is.True);
    Assert.That(ServerSideData.RemoveFromStore(Bytes, id, 6), Is.False);
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
