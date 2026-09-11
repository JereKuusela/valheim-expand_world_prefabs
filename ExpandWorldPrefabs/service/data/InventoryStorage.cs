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
    return false;
  }

  public static void Save(ZDO zdo, Inventory inventory)
  {
    ZPackage package = new();
    inventory.Save(package);
    zdo.Set(ZDOVars.s_items, package.GetArray());
  }
}
