namespace Data;

internal static class InventoryStorage
{
  public const int FormatVersion = 109;

  public static Inventory Create(ZDO zdo, int width, int height)
  {
    var inventory = new Inventory("", null, width, height);
    TryLoad(zdo, inventory);
    return inventory;
  }

  public static bool TryLoad(ZDO zdo, Inventory inventory)
  {
    var bytes = zdo.GetByteArray(ZDOVars.s_items, null);
    if (bytes != null && bytes.Length > 0)
    {
      inventory.Load(new ZPackage(bytes));
      return true;
    }

    // Compatibility for containers saved before Deep North moved s_items
    // from a Base64 string to a raw byte array.
    var legacy = zdo.GetString(ZDOVars.s_items);
    if (legacy == "") return false;
    inventory.Load(new ZPackage(legacy));
    return true;
  }

  public static void Save(ZDO zdo, Inventory inventory)
  {
    ZPackage package = new();
    inventory.Save(package);
    RemoveLegacy(zdo);
    zdo.Set(ZDOVars.s_items, package.GetArray());
  }

  public static void RemoveLegacy(ZDO zdo) =>
    ZDOExtraData.s_strings.Remove(zdo.m_uid, ZDOVars.s_items);
}
