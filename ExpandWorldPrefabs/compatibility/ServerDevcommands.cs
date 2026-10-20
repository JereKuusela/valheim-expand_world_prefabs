using BepInEx.Bootstrap;
using BepInEx.Configuration;
using Service;
namespace ExpandWorld.Prefab;

public static class ServerDevcommands
{
  public static void Init()
  {
    if (!Chainloader.PluginInfos.TryGetValue("server_devcommands", out var plugin)) return;
    var settings = plugin.Instance.GetType().Assembly.GetType("ServerDevcommands.Settings");
    if (settings?.GetField("configServerChatName")?.GetValue(null) is not ConfigEntry<string> entry) return;
    ServerClient.SetName(entry.Value);
    entry.SettingChanged += (s, e) => ServerClient.SetName(entry.Value);
  }
}
