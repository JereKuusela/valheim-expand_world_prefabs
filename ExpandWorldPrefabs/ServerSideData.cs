using System;
using System.Collections.Generic;
using Data;
using HarmonyLib;
using Service;
using UnityEngine;

namespace ExpandWorld.Prefab;

public static class ServerSideData
{
  private static bool IsPatched = false;

  private static readonly Dictionary<ZDOID, Dictionary<int, string>> Strings = [];
  private static readonly Dictionary<ZDOID, Dictionary<int, float>> Floats = [];
  private static readonly Dictionary<ZDOID, Dictionary<int, int>> Ints = [];
  private static readonly Dictionary<ZDOID, Dictionary<int, long>> Longs = [];
  private static readonly Dictionary<ZDOID, Dictionary<int, Vector3>> Vecs = [];
  private static readonly Dictionary<ZDOID, Dictionary<int, Quaternion>> Quats = [];
  private static readonly Dictionary<ZDOID, Dictionary<int, byte[]>> Bytes = [];

  private static readonly int StorageHash = ZdoHelper.Hash("_ewp_serverdata");
  // Built on the main thread before the save thread starts; the save thread only reads it.
  private static Dictionary<ZDOID, byte[]>? SavePayloads;

  private const int CurrentVersion = 1;
  private const ushort VersionShift = 12;
  private const ushort VersionMask = unchecked((ushort)0xF000);
  private const ushort FlagsMask = 0x00FF;

  private const ushort FlagConnections = 1 << 0;
  private const ushort FlagFloats = 1 << 1;
  private const ushort FlagVec3 = 1 << 2;
  private const ushort FlagQuaternions = 1 << 3;
  private const ushort FlagInts = 1 << 4;
  private const ushort FlagLongs = 1 << 5;
  private const ushort FlagStrings = 1 << 6;
  private const ushort FlagByteArrays = 1 << 7;

  public static event Action<ZDO>? Loaded;
  public static event Action<ZDOID>? Destroyed;

  public static bool ShouldUse(int hash) => ZdoHelper.IsServerSideHash(hash);

  public static void Patch(Harmony harmony, bool shouldPatch)
  {
    if (shouldPatch && !IsPatched)
      DoPatch(harmony);
    if (!shouldPatch && IsPatched)
      DoUnpatch(harmony);
  }

  private static void DoPatch(Harmony harmony)
  {
    IsPatched = true;

    var loadMethod = AccessTools.Method(typeof(ZDO), nameof(ZDO.Load));
    var loadPostfix = AccessTools.Method(typeof(ServerSideData), nameof(AfterLoad));
    harmony.Patch(loadMethod, postfix: new HarmonyMethod(loadPostfix));

    var prepareMethod = AccessTools.Method(typeof(ZDOMan), nameof(ZDOMan.PrepareSave));
    var preparePrefix = AccessTools.Method(typeof(ServerSideData), nameof(BeforePrepareSave));
    harmony.Patch(prepareMethod, prefix: new HarmonyMethod(preparePrefix));

    // Modify only the save result; the live ZDO and its ExtraData stay untouched.
    var getSaveDataMethod = AccessTools.Method(typeof(ZDOExtraData), nameof(ZDOExtraData.GetSaveData));
    var saveDataPostfix = AccessTools.Method(typeof(ServerSideData), nameof(AfterGetSaveData));
    harmony.Patch(getSaveDataMethod, postfix: new HarmonyMethod(saveDataPostfix));

    var cleanupMethod = AccessTools.Method(typeof(ZDOMan), nameof(ZDOMan.SaveCleanup));
    var cleanupPostfix = AccessTools.Method(typeof(ServerSideData), nameof(AfterSaveCleanup));
    harmony.Patch(cleanupMethod, postfix: new HarmonyMethod(cleanupPostfix));

    var destroyMethod = AccessTools.Method(typeof(ZDOMan), nameof(ZDOMan.HandleDestroyedZDO), [typeof(ZDOID)]);
    var destroyPostfix = AccessTools.Method(typeof(ServerSideData), nameof(AfterDestroyed));
    harmony.Patch(destroyMethod, postfix: new HarmonyMethod(destroyPostfix));
  }

  private static void DoUnpatch(Harmony harmony)
  {
    // Unpatching mid-save would drop data.
    // This should never happen but better safe than sorry.
    if (ZNet.instance != null)
      ZNet.instance.m_saveThread?.Join();
    IsPatched = false;
    var loadMethod = AccessTools.Method(typeof(ZDO), nameof(ZDO.Load));
    var loadPostfix = AccessTools.Method(typeof(ServerSideData), nameof(AfterLoad));
    var prepareMethod = AccessTools.Method(typeof(ZDOMan), nameof(ZDOMan.PrepareSave));
    var preparePrefix = AccessTools.Method(typeof(ServerSideData), nameof(BeforePrepareSave));
    var getSaveDataMethod = AccessTools.Method(typeof(ZDOExtraData), nameof(ZDOExtraData.GetSaveData));
    var saveDataPostfix = AccessTools.Method(typeof(ServerSideData), nameof(AfterGetSaveData));
    var cleanupMethod = AccessTools.Method(typeof(ZDOMan), nameof(ZDOMan.SaveCleanup));
    var cleanupPostfix = AccessTools.Method(typeof(ServerSideData), nameof(AfterSaveCleanup));
    var destroyMethod = AccessTools.Method(typeof(ZDOMan), nameof(ZDOMan.HandleDestroyedZDO), [typeof(ZDOID)]);
    var destroyPostfix = AccessTools.Method(typeof(ServerSideData), nameof(AfterDestroyed));

    harmony.Unpatch(loadMethod, loadPostfix);
    harmony.Unpatch(prepareMethod, preparePrefix);
    harmony.Unpatch(getSaveDataMethod, saveDataPostfix);
    harmony.Unpatch(cleanupMethod, cleanupPostfix);
    harmony.Unpatch(destroyMethod, destroyPostfix);

    SavePayloads = null;
    ClearAll();
  }

  private static void AfterLoad(ZDO __instance)
  {
    Deserialize(__instance);
  }

  // Valheim uses snapshot too, so match that logic.
  private static void BeforePrepareSave(ZDOMan __instance)
  {
    var dirtyChunks = __instance.m_dirtyChunks[0];
    var dirtyPortals = __instance.DirtyPortalObjects;
    var portalHashes = Game.instance.PortalPrefabHash;
    SavePayloads = BuildSaveSnapshot(id =>
    {
      var zdo = __instance.GetZDO(id);
      return zdo != null && zdo.Persistent && WillBeSaved(zdo, dirtyChunks, dirtyPortals, portalHashes);
    });
  }

  internal static Dictionary<ZDOID, byte[]> BuildSaveSnapshot(Func<ZDOID, bool> include)
  {
    HashSet<ZDOID> ids = [
      .. Strings.Keys, .. Floats.Keys, .. Ints.Keys, .. Longs.Keys,
      .. Vecs.Keys, .. Quats.Keys, .. Bytes.Keys
    ];

    Dictionary<ZDOID, byte[]> snapshot = [];
    foreach (var id in ids)
    {
      if (!include(id))
        continue;
      var payload = Serialize(id);
      if (payload != null && payload.Length > 0)
        snapshot.Add(id, payload);
    }
    return snapshot;
  }

  // Mirrors ZDOMan.GetSaveClonePerChunk which only writes dirty chunks (at any merged chunk size).
  private static bool WillBeSaved(ZDO zdo, HashSet<ZoneSystem.ChunkIndex> dirtyChunks, bool dirtyPortals, List<int> portalHashes)
  {
    if (portalHashes.Contains(zdo.GetPrefab()))
      return dirtyPortals;
    var chunk = ZoneSystem.GetZonesChunk(zdo.GetSectorIndex());
    if (dirtyChunks.Contains(chunk))
      return true;
    for (byte size = 1; size <= 3; size++)
    {
      chunk = ZoneSystem.ChunkIndexFromIndexAndSize(chunk, size);
      if (dirtyChunks.Contains(chunk))
        return true;
    }
    return false;
  }

  private static void AfterSaveCleanup()
  {
    SavePayloads = null;
  }

  private static void AfterGetSaveData(ZDOID zid, ref List<KeyValuePair<int, byte[]>> byteArray)
  {
    var snapshot = SavePayloads;
    if (snapshot != null)
      AddSavePayload(snapshot, zid, ref byteArray);
  }

  internal static void AddSavePayload(Dictionary<ZDOID, byte[]> snapshot, ZDOID id,
    ref List<KeyValuePair<int, byte[]>> byteArrays)
  {
    if (!snapshot.TryGetValue(id, out var payload))
      return;

    byteArrays ??= [];
    for (var i = 0; i < byteArrays.Count; i++)
    {
      if (byteArrays[i].Key == StorageHash)
      {
        byteArrays[i] = new KeyValuePair<int, byte[]>(StorageHash, payload);
        return;
      }
    }
    byteArrays.Add(new KeyValuePair<int, byte[]>(StorageHash, payload));
  }

  private static void AfterDestroyed(ZDOID uid)
  {
    Remove(uid);
    Destroyed?.Invoke(uid);
  }

  private static byte[]? Serialize(ZDOID uid)
  {
    Strings.TryGetValue(uid, out var strings);
    Floats.TryGetValue(uid, out var floats);
    Ints.TryGetValue(uid, out var ints);
    Longs.TryGetValue(uid, out var longs);
    Vecs.TryGetValue(uid, out var vecs);
    Quats.TryGetValue(uid, out var quats);
    Bytes.TryGetValue(uid, out var bytes);

    ushort flags = 0;
    if (floats?.Count > 0) flags |= FlagFloats;
    if (vecs?.Count > 0) flags |= FlagVec3;
    if (quats?.Count > 0) flags |= FlagQuaternions;
    if (ints?.Count > 0) flags |= FlagInts;
    if (longs?.Count > 0) flags |= FlagLongs;
    if (strings?.Count > 0) flags |= FlagStrings;
    if (bytes?.Count > 0) flags |= FlagByteArrays;

    if (flags == 0)
      return null;

    var header = (ushort)(flags | (CurrentVersion << VersionShift));
    ZPackage pkg = new();
    pkg.Write((int)header);

    if ((flags & FlagFloats) != 0)
      WriteFloats(pkg, floats!);
    if ((flags & FlagVec3) != 0)
      WriteVecs(pkg, vecs!);
    if ((flags & FlagQuaternions) != 0)
      WriteQuats(pkg, quats!);
    if ((flags & FlagInts) != 0)
      WriteInts(pkg, ints!);
    if ((flags & FlagLongs) != 0)
      WriteLongs(pkg, longs!);
    if ((flags & FlagStrings) != 0)
      WriteStrings(pkg, strings!);
    if ((flags & FlagByteArrays) != 0)
      WriteBytes(pkg, bytes!);

    return pkg.GetArray();
  }

  private static void Deserialize(ZDO zdo)
  {
    try
    {
      var payload = zdo.GetByteArray(StorageHash, null);
      if (payload == null || payload.Length == 0)
      {
        Remove(zdo.m_uid);
        ExtractServerSideInts(zdo);
        return;
      }

      ZPackage pkg = new(payload);
      var header = (ushort)pkg.ReadInt();
      var version = (header & VersionMask) >> VersionShift;
      if (version != CurrentVersion)
      {
        Log.Warning($"ServerSideData: Unsupported serializer version {version} for {zdo.m_uid}.");
        Remove(zdo.m_uid);
        return;
      }

      var flags = (ushort)(header & FlagsMask);
      Remove(zdo.m_uid);

      // Reserved in current schema.
      if ((flags & FlagConnections) != 0)
      {
      }
      if ((flags & FlagFloats) != 0)
        Floats[zdo.m_uid] = ReadFloats(pkg);
      if ((flags & FlagVec3) != 0)
        Vecs[zdo.m_uid] = ReadVecs(pkg);
      if ((flags & FlagQuaternions) != 0)
        Quats[zdo.m_uid] = ReadQuats(pkg);
      if ((flags & FlagInts) != 0)
        Ints[zdo.m_uid] = ReadInts(pkg);
      if ((flags & FlagLongs) != 0)
        Longs[zdo.m_uid] = ReadLongs(pkg);
      if ((flags & FlagStrings) != 0)
        Strings[zdo.m_uid] = ReadStrings(pkg);
      if ((flags & FlagByteArrays) != 0)
        Bytes[zdo.m_uid] = ReadBytes(pkg);
      ExtractServerSideInts(zdo);
      CleanSyncedData(zdo);
      Loaded?.Invoke(zdo);
    }
    catch (Exception e)
    {
      Log.Warning($"ServerSideData: Failed to deserialize payload for {zdo.m_uid}: {e.Message}");
      Remove(zdo.m_uid);
    }
    finally
    {
      RemovePayloadFromZdo(zdo);
    }
  }

  // Server side data is now always enabled, so have to extract legacy keys so that they work correctly with the new system.
  private static void ExtractServerSideInts(ZDO zdo)
  {
    ExtractServerSideInt(zdo, SupportAttach.EWPAttachedHash);
    ExtractServerSideInt(zdo, ServerOwned.EWPServerOwnedHash);
  }

  private static void ExtractServerSideInt(ZDO zdo, int key)
  {
    if (!ZDOExtraData.s_ints.TryGetValue(zdo.m_uid, out var ints) || !ints.TryGetValue(key, out var value))
      return;
    if (!TryGetInt(zdo.m_uid, key, out _))
      SetInt(zdo, key, value);
    zdo.RemoveInt(key);
  }

  private static void RemovePayloadFromZdo(ZDO zdo)
  {
    RemoveFromStore(ZDOExtraData.s_byteArrays, zdo.m_uid, StorageHash);
  }

  public static bool TryGetStrings(ZDOID id, out Dictionary<int, string> values) => Strings.TryGetValue(id, out values);
  public static bool TryGetFloats(ZDOID id, out Dictionary<int, float> values) => Floats.TryGetValue(id, out values);
  public static bool TryGetInts(ZDOID id, out Dictionary<int, int> values) => Ints.TryGetValue(id, out values);
  public static bool TryGetLongs(ZDOID id, out Dictionary<int, long> values) => Longs.TryGetValue(id, out values);
  public static bool TryGetVecs(ZDOID id, out Dictionary<int, Vector3> values) => Vecs.TryGetValue(id, out values);
  public static bool TryGetQuaternions(ZDOID id, out Dictionary<int, Quaternion> values) => Quats.TryGetValue(id, out values);
  public static bool TryGetBytes(ZDOID id, out Dictionary<int, byte[]> values) => Bytes.TryGetValue(id, out values);

  public static bool TryGetString(ZDOID id, int key, out string value) => TryGet(Strings, id, key, out value);
  public static bool TryGetFloat(ZDOID id, int key, out float value) => TryGet(Floats, id, key, out value);
  public static bool TryGetInt(ZDOID id, int key, out int value) => TryGet(Ints, id, key, out value);
  public static bool TryGetLong(ZDOID id, int key, out long value) => TryGet(Longs, id, key, out value);
  public static bool TryGetVec(ZDOID id, int key, out Vector3 value) => TryGet(Vecs, id, key, out value);
  public static bool TryGetQuaternion(ZDOID id, int key, out Quaternion value) => TryGet(Quats, id, key, out value);
  public static bool TryGetBytes(ZDOID id, int key, out byte[] value) => TryGet(Bytes, id, key, out value);

  public static void SetString(ZDO zdo, int key, string value)
  {
    Set(Strings, zdo, key, value);
  }
  public static void SetFloat(ZDO zdo, int key, float value)
  {
    Set(Floats, zdo, key, value);
  }
  public static void SetInt(ZDO zdo, int key, int value)
  {
    Set(Ints, zdo, key, value);
  }
  public static void SetLong(ZDO zdo, int key, long value)
  {
    Set(Longs, zdo, key, value);
  }
  public static void SetVec(ZDO zdo, int key, Vector3 value)
  {
    Set(Vecs, zdo, key, value);
  }
  public static void SetQuaternion(ZDO zdo, int key, Quaternion value)
  {
    Set(Quats, zdo, key, value);
  }
  public static void SetBytes(ZDO zdo, int key, byte[] value)
  {
    Set(Bytes, zdo, key, value);
  }
  public static void RemoveString(ZDO zdo, int key)
  {
    RemoveFromStore(Strings, zdo, key);
    ZDOExtraData.s_strings.Remove(zdo.m_uid, key);
  }
  public static void RemoveFloat(ZDO zdo, int key)
  {
    RemoveFromStore(Floats, zdo, key);
    zdo.RemoveFloat(key);
  }
  public static void RemoveInt(ZDO zdo, int key)
  {
    RemoveFromStore(Ints, zdo, key);
    zdo.RemoveInt(key);
  }
  public static void RemoveLong(ZDO zdo, int key)
  {
    RemoveFromStore(Longs, zdo, key);
    zdo.RemoveLong(key);
  }
  public static void RemoveVec(ZDO zdo, int key)
  {
    RemoveFromStore(Vecs, zdo, key);
    zdo.RemoveVec3(key);
  }
  public static void RemoveQuaternion(ZDO zdo, int key)
  {
    RemoveFromStore(Quats, zdo, key);
    zdo.RemoveQuaternion(key);
  }
  public static void RemoveBytes(ZDO zdo, int key)
  {
    RemoveFromStore(Bytes, zdo, key);
    ZDOExtraData.s_byteArrays.Remove(zdo.m_uid, key);
  }
  public static void CleanSyncedData(ZDO zdo)
  {
    var id = zdo.m_uid;
    if (Floats.TryGetValue(id, out var floats))
      RemoveKeys(ZDOExtraData.s_floats, id, floats.Keys);
    if (Ints.TryGetValue(id, out var ints))
      RemoveKeys(ZDOExtraData.s_ints, id, ints.Keys);
    if (Longs.TryGetValue(id, out var longs))
      RemoveKeys(ZDOExtraData.s_longs, id, longs.Keys);
    if (Strings.TryGetValue(id, out var strings))
      RemoveKeys(ZDOExtraData.s_strings, id, strings.Keys);
    if (Vecs.TryGetValue(id, out var vecs))
      RemoveKeys(ZDOExtraData.s_vec3, id, vecs.Keys);
    if (Quats.TryGetValue(id, out var quats))
      RemoveKeys(ZDOExtraData.s_quats, id, quats.Keys);
    if (Bytes.TryGetValue(id, out var bytes))
      RemoveKeys(ZDOExtraData.s_byteArrays, id, bytes.Keys);
  }

  private static bool TryGet<T>(Dictionary<ZDOID, Dictionary<int, T>> store, ZDOID id, int key, out T value)
  {
    if (store.TryGetValue(id, out var data) && data.TryGetValue(key, out value))
      return true;
    value = default!;
    return false;
  }

  internal static bool Set<T>(Dictionary<ZDOID, Dictionary<int, T>> store, ZDO zdo, int key, T value)
  {
    if (!store.TryGetValue(zdo.m_uid, out var data))
    {
      data = [];
      store[zdo.m_uid] = data;
    }
    if (data.TryGetValue(key, out var previous))
    {
      if (previous is byte[] oldBytes && value is byte[] newBytes)
      {
        if (SameBytes(oldBytes, newBytes)) return false;
      }
      else if (EqualityComparer<T>.Default.Equals(previous, value))
        return false;
    }

    data[key] = value is byte[] bytes ? (T)(object)(byte[])bytes.Clone() : value;
    MarkSaveChunkDirty(zdo);
    return true;
  }

  private static bool SameBytes(byte[] left, byte[] right)
  {
    if (left.Length != right.Length) return false;
    for (var i = 0; i < left.Length; ++i)
      if (left[i] != right[i]) return false;
    return true;
  }

  private static void MarkSaveChunkDirty(ZDO zdo)
  {
    if (zdo.Persistent && ZDOMan.instance != null && ZNet.instance != null && ZNet.instance.IsServer())
      ZDOMan.instance.SetDirtySector(zdo);
  }

  private static void Remove(ZDOID id)
  {
    Strings.Remove(id);
    Floats.Remove(id);
    Ints.Remove(id);
    Longs.Remove(id);
    Vecs.Remove(id);
    Quats.Remove(id);
    Bytes.Remove(id);
  }

  private static void ClearAll()
  {
    Strings.Clear();
    Floats.Clear();
    Ints.Clear();
    Longs.Clear();
    Vecs.Clear();
    Quats.Clear();
    Bytes.Clear();
  }

  internal static bool RemoveFromStore<T>(Dictionary<ZDOID, Dictionary<int, T>> store, ZDO zdo, int key)
  {
    var id = zdo.m_uid;
    if (!store.TryGetValue(id, out var data) || !data.Remove(key))
      return false;
    if (data.Count == 0)
      store.Remove(id);
    MarkSaveChunkDirty(zdo);
    return true;
  }

  private static void RemoveFromStore<T>(Dictionary<ZDOID, BinarySearchDictionary<int, T>> store, ZDOID id, int key)
  {
    if (!store.TryGetValue(id, out var data))
      return;
    data.Remove(key);
    if (data.Count == 0)
      store.Remove(id);
  }

  private static void RemoveKeys<T>(Dictionary<ZDOID, BinarySearchDictionary<int, T>> store, ZDOID id, Dictionary<int, T>.KeyCollection keys)
  {
    if (!store.TryGetValue(id, out var data))
      return;
    foreach (var key in keys)
      data.Remove(key);
    if (data.Count == 0)
      store.Remove(id);
  }

  private static void WriteFloats(ZPackage pkg, Dictionary<int, float> values)
  {
    pkg.Write(values.Count);
    foreach (var pair in values)
    {
      pkg.Write(pair.Key);
      pkg.Write(pair.Value);
    }
  }

  private static void WriteVecs(ZPackage pkg, Dictionary<int, Vector3> values)
  {
    pkg.Write(values.Count);
    foreach (var pair in values)
    {
      pkg.Write(pair.Key);
      pkg.Write(pair.Value);
    }
  }

  private static void WriteQuats(ZPackage pkg, Dictionary<int, Quaternion> values)
  {
    pkg.Write(values.Count);
    foreach (var pair in values)
    {
      pkg.Write(pair.Key);
      pkg.Write(pair.Value);
    }
  }

  private static void WriteInts(ZPackage pkg, Dictionary<int, int> values)
  {
    pkg.Write(values.Count);
    foreach (var pair in values)
    {
      pkg.Write(pair.Key);
      pkg.Write(pair.Value);
    }
  }

  private static void WriteLongs(ZPackage pkg, Dictionary<int, long> values)
  {
    pkg.Write(values.Count);
    foreach (var pair in values)
    {
      pkg.Write(pair.Key);
      pkg.Write(pair.Value);
    }
  }

  private static void WriteStrings(ZPackage pkg, Dictionary<int, string> values)
  {
    pkg.Write(values.Count);
    foreach (var pair in values)
    {
      pkg.Write(pair.Key);
      pkg.Write(pair.Value);
    }
  }

  private static void WriteBytes(ZPackage pkg, Dictionary<int, byte[]> values)
  {
    pkg.Write(values.Count);
    foreach (var pair in values)
    {
      pkg.Write(pair.Key);
      pkg.Write(pair.Value ?? []);
    }
  }

  private static Dictionary<int, float> ReadFloats(ZPackage pkg)
  {
    var count = pkg.ReadInt();
    Dictionary<int, float> result = new(Math.Max(0, count));
    for (var i = 0; i < count; ++i)
      result[pkg.ReadInt()] = pkg.ReadSingle();
    return result;
  }

  private static Dictionary<int, Vector3> ReadVecs(ZPackage pkg)
  {
    var count = pkg.ReadInt();
    Dictionary<int, Vector3> result = new(Math.Max(0, count));
    for (var i = 0; i < count; ++i)
      result[pkg.ReadInt()] = pkg.ReadVector3();
    return result;
  }

  private static Dictionary<int, Quaternion> ReadQuats(ZPackage pkg)
  {
    var count = pkg.ReadInt();
    Dictionary<int, Quaternion> result = new(Math.Max(0, count));
    for (var i = 0; i < count; ++i)
      result[pkg.ReadInt()] = pkg.ReadQuaternion();
    return result;
  }

  private static Dictionary<int, int> ReadInts(ZPackage pkg)
  {
    var count = pkg.ReadInt();
    Dictionary<int, int> result = new(Math.Max(0, count));
    for (var i = 0; i < count; ++i)
      result[pkg.ReadInt()] = pkg.ReadInt();
    return result;
  }

  private static Dictionary<int, long> ReadLongs(ZPackage pkg)
  {
    var count = pkg.ReadInt();
    Dictionary<int, long> result = new(Math.Max(0, count));
    for (var i = 0; i < count; ++i)
      result[pkg.ReadInt()] = pkg.ReadLong();
    return result;
  }

  private static Dictionary<int, string> ReadStrings(ZPackage pkg)
  {
    var count = pkg.ReadInt();
    Dictionary<int, string> result = new(Math.Max(0, count));
    for (var i = 0; i < count; ++i)
      result[pkg.ReadInt()] = pkg.ReadString();
    return result;
  }

  private static Dictionary<int, byte[]> ReadBytes(ZPackage pkg)
  {
    var count = pkg.ReadInt();
    Dictionary<int, byte[]> result = new(Math.Max(0, count));
    for (var i = 0; i < count; ++i)
      result[pkg.ReadInt()] = pkg.ReadByteArray();
    return result;
  }
}
