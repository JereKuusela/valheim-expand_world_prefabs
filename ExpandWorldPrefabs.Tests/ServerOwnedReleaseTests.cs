using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Data;
using ExpandWorld.Prefab;
using NUnit.Framework;

namespace ExpandWorldPrefabs.Tests;

// Managed marker tests. ZDO.Deserialize, the chunk writer and real owner transfer
// still require dedicated-server/client acceptance tests.
[TestFixture, NonParallelizable]
public class ServerOwnedReleaseTests
{
  private const BindingFlags StaticFields = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
  private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
  private static readonly int Marker = ZdoHelper.Hash("ewp_server_owned");
  private static readonly int OtherKey = ZdoHelper.Hash("ewp_unrelated_int");
  private static readonly FieldInfo IdField = typeof(ZDO).GetField("m_uid", InstanceFields)!;
  private static readonly MethodInfo Prefix = typeof(ServerOwned).GetMethod("SetOwner", StaticFields)!;
  private static readonly MethodInfo Serialize = typeof(ServerSideData).GetMethod("Serialize", StaticFields)!;
  private static readonly MethodInfo Deserialize = typeof(ServerSideData).GetMethod("Deserialize", StaticFields)!;
  private static readonly int PayloadKey = (int)typeof(ServerSideData).GetField("StorageHash", StaticFields)!.GetValue(null)!;
  private static readonly Dictionary<ZDOID, Dictionary<int, int>> Ints =
    (Dictionary<ZDOID, Dictionary<int, int>>)typeof(ServerSideData).GetField("Ints", StaticFields)!.GetValue(null)!;

  private readonly List<ZDOID> ids = [];
  private uint serial;

  [TearDown]
  public void TearDown()
  {
    foreach (var id in ids)
    {
      Ints.Remove(id);
      ZDOExtraData.s_ints.Remove(id);
      ZDOExtraData.s_byteArrays.Remove(id);
    }
    ids.Clear();
  }

  // Set up synchronized values as pre-existing/incoming extra data. Calling
  // ZDO.Set on an uninitialized test ZDO requires native owner/revision state.
  private static void WriteSyncedInt(ZDO zdo, int key, int value)
  {
    ZDOHelper.Init(ZDOExtraData.s_ints, zdo.m_uid);
    ZDOExtraData.s_ints[zdo.m_uid].SetValue(key, value);
  }

  private static void WriteSyncedPayload(ZDO zdo, byte[] payload)
  {
    ZDOHelper.Init(ZDOExtraData.s_byteArrays, zdo.m_uid);
    ZDOExtraData.s_byteArrays[zdo.m_uid].SetValue(PayloadKey, payload);
  }

  private ZDO NewZdo()
  {
    var id = new ZDOID(-7301001L, ++serial);
    ids.Add(id);
#pragma warning disable SYSLIB0050
    var zdo = (ZDO)FormatterServices.GetUninitializedObject(typeof(ZDO));
#pragma warning restore SYSLIB0050
    IdField.SetValue(zdo, id);
    return zdo;
  }

  [Test]
  public void ServerSideOneAndZero_AreDifferentFromMissing()
  {
    var zdo = NewZdo();
    Assert.That(ServerSideData.TryGetInt(zdo.m_uid, Marker, out _), Is.False);
    Ints[zdo.m_uid] = new() { [Marker] = 1 };
    Assert.That(ServerOwned.IsMarked(zdo), Is.True);
    Ints[zdo.m_uid][Marker] = 0;
    Assert.That(ServerSideData.TryGetInt(zdo.m_uid, Marker, out var value), Is.True);
    Assert.That(value, Is.Zero);
    Assert.That(ServerOwned.IsMarked(zdo), Is.False);
    Ints[zdo.m_uid][Marker] = 2;
    Assert.That(ServerOwned.IsMarked(zdo), Is.False);
  }

  [TestCase("ewp_attached", false)]
  [TestCase("ewp_attached", true)]
  [TestCase("ewp_server_owned", false)]
  [TestCase("ewp_server_owned", true)]
  public void Deserialize_ExtractsLegacyMarker_AndPreservesOtherInts(string markerName, bool withPayload)
  {
    var zdo = NewZdo();
    var marker = ZdoHelper.Hash(markerName);
    if (withPayload)
    {
      Ints[zdo.m_uid] = new() { [OtherKey] = 37 };
      WriteSyncedPayload(zdo, (byte[])Serialize.Invoke(null, [zdo.m_uid])!);
      Ints.Remove(zdo.m_uid);
    }
    WriteSyncedInt(zdo, marker, 1);
    WriteSyncedInt(zdo, OtherKey, 11);

    Deserialize.Invoke(null, [zdo]);

    Assert.That(ServerSideData.TryGetInt(zdo.m_uid, marker, out var value), Is.True);
    Assert.That(value, Is.EqualTo(1));
    Assert.That(zdo.GetInt(marker, 0), Is.Zero);
    Assert.That(zdo.GetInt(OtherKey, 0), Is.EqualTo(withPayload ? 0 : 11));
    if (withPayload)
    {
      Assert.That(ServerSideData.TryGetInt(zdo.m_uid, OtherKey, out var other), Is.True);
      Assert.That(other, Is.EqualTo(37));
    }
  }

  [TestCase("ewp_attached")]
  [TestCase("ewp_server_owned")]
  public void Deserialize_PreservesServerSideMarker_OverLegacyMarker(string markerName)
  {
    var zdo = NewZdo();
    var marker = ZdoHelper.Hash(markerName);
    Ints[zdo.m_uid] = new() { [marker] = 0 };
    WriteSyncedPayload(zdo, (byte[])Serialize.Invoke(null, [zdo.m_uid])!);
    Ints.Remove(zdo.m_uid);
    WriteSyncedInt(zdo, marker, 1);

    Deserialize.Invoke(null, [zdo]);

    Assert.That(ServerSideData.TryGetInt(zdo.m_uid, marker, out var value), Is.True);
    Assert.That(value, Is.Zero);
    Assert.That(zdo.GetInt(marker, 0), Is.Zero);
  }

  [Test]
  public void SynchronizedLegacyMarker_IsIgnoredUntilLoadMigratesIt()
  {
    var zdo = NewZdo();
    WriteSyncedInt(zdo, Marker, 1);

    // Synchronized client data is not authoritative during gameplay.
    Assert.That(ServerSideData.TryGetInt(zdo.m_uid, Marker, out _), Is.False);
    Assert.That(ServerOwned.IsMarked(zdo), Is.False);

    // Migration applies to old world saves, not network synchronization.
    Deserialize.Invoke(null, [zdo]);
    Assert.That(ServerSideData.TryGetInt(zdo.m_uid, Marker, out var value), Is.True);
    Assert.That(value, Is.EqualTo(1));
    Assert.That(ServerOwned.IsMarked(zdo), Is.True);
    Assert.That(ZDOExtraData.s_ints.TryGetValue(zdo.m_uid, out var synced) && synced.ContainsKey(Marker), Is.False);
  }

  [Test]
  public void Unmark_RemovesMarkerFromBothStores_AndPreservesOtherKeys()
  {
    var zdo = NewZdo();
    Ints[zdo.m_uid] = new() { [Marker] = 1, [OtherKey] = 37 };
    WriteSyncedInt(zdo, Marker, 1);
    WriteSyncedInt(zdo, OtherKey, 11);

    ServerOwned.Unmark(zdo);

    Assert.That(ServerSideData.TryGetInt(zdo.m_uid, Marker, out _), Is.False);
    Assert.That(ServerOwned.IsMarked(zdo), Is.False);
    Assert.That(ZDOExtraData.s_ints[zdo.m_uid].ContainsKey(Marker), Is.False);
    Assert.That(zdo.GetInt(OtherKey, 0), Is.EqualTo(11));
    Assert.That(ServerSideData.TryGetInt(zdo.m_uid, OtherKey, out var other), Is.True);
    Assert.That(other, Is.EqualTo(37));
  }

  [Test]
  public void Unmark_MasksRepeatedStaleSynchronizedWrites()
  {
    var zdo = NewZdo();
    Ints[zdo.m_uid] = new() { [Marker] = 1 };
    ServerOwned.Unmark(zdo);
    for (var i = 0; i < 1000; ++i)
    {
      // Models the stale marker arriving via sync; not a native Deserialize call.
      WriteSyncedInt(zdo, Marker, 1);
      Assert.That(ServerOwned.IsMarked(zdo), Is.False);
    }
  }

  [Test]
  public void Unmark_NonPersistentObject_RemovesExistingMarker()
  {
    var zdo = NewZdo();
    zdo.Persistent = false;
    Ints[zdo.m_uid] = new() { [Marker] = 1 };
    Assert.That(ServerOwned.IsMarked(zdo), Is.True);

    ServerOwned.Unmark(zdo);

    Assert.That(ServerSideData.TryGetInt(zdo.m_uid, Marker, out _), Is.False);
    Assert.That(ServerOwned.IsMarked(zdo), Is.False);
  }

  [Test]
  public void ReleasedObject_DoesNotRewriteRequestedOwnerInHarmonyPrefix()
  {
    var zdo = NewZdo();
    ServerOwned.Unmark(zdo);
    object[] arguments = [zdo, 987654321L];
    Prefix.Invoke(null, arguments);
    Assert.That(arguments[1], Is.EqualTo(987654321L));
  }

  [Test]
  public void Unmark_ExcludesRemovedMarkerFromSerializer()
  {
    var zdo = NewZdo();
    Ints[zdo.m_uid] = new() { [Marker] = 1, [OtherKey] = 37 };
    ServerOwned.Unmark(zdo);

    var payload = (byte[]?)Serialize.Invoke(null, [zdo.m_uid]);
    Assert.That(payload, Is.Not.Null.And.Not.Empty);
    var pkg = new ZPackage(payload!);
    var header = (ushort)pkg.ReadInt();
    Assert.That(header >> 12, Is.EqualTo(1));
    Assert.That(header & (1 << 4), Is.Not.Zero);
    Assert.That(pkg.ReadInt(), Is.EqualTo(1));
    Assert.That(pkg.ReadInt(), Is.EqualTo(OtherKey));
    Assert.That(pkg.ReadInt(), Is.EqualTo(37));

    // With no other server-side keys, removing ownership leaves no payload.
    Ints[zdo.m_uid].Remove(OtherKey);
    Assert.That(Serialize.Invoke(null, [zdo.m_uid]), Is.Null);
  }

  [Test]
  public void Unmark_RoundTrip_KeepsOwnershipReleasedAndOtherData()
  {
    var zdo = NewZdo();
    Ints[zdo.m_uid] = new() { [Marker] = 1, [OtherKey] = 37 };
    WriteSyncedInt(zdo, Marker, 1);
    ServerOwned.Unmark(zdo);
    var payload = (byte[]?)Serialize.Invoke(null, [zdo.m_uid]);
    Assert.That(payload, Is.Not.Null.And.Not.Empty);

    Ints.Remove(zdo.m_uid); // Model a freshly loaded server-side dictionary.
    WriteSyncedPayload(zdo, payload!);
    Deserialize.Invoke(null, [zdo]);

    Assert.That(ServerSideData.TryGetInt(zdo.m_uid, Marker, out _), Is.False);
    Assert.That(ServerOwned.IsMarked(zdo), Is.False);
    Assert.That(ServerSideData.TryGetInt(zdo.m_uid, OtherKey, out var value), Is.True);
    Assert.That(value, Is.EqualTo(37));
    Assert.That(ZDOExtraData.s_ints.TryGetValue(zdo.m_uid, out var synced) && synced.ContainsKey(Marker), Is.False);
  }

}
