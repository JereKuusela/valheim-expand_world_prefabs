using HarmonyLib;
using UnityEngine;

namespace ExpandWorld.Prefab;

public class HandleTime
{
  public static void Patch(Harmony harmony, bool trackTicks, bool trackMinutes, bool trackHours, bool trackDays)
  {
    TrackTicks = trackTicks;
    TrackMinutes = trackMinutes;
    TrackHours = trackHours;
    TrackDays = trackDays;
    var method = AccessTools.Method(typeof(ZNet), nameof(ZNet.UpdateNetTime));
    var patch = AccessTools.Method(typeof(HandleTime), nameof(UpdateNetTime));
    harmony.Patch(method, postfix: new HarmonyMethod(patch));
  }
  private static bool TrackTicks = false;
  private static bool TrackMinutes = false;
  private static bool TrackHours = false;
  private static bool TrackDays = false;
  private static double PreviousTime = 0;
  private static int PreviousMinute = 0;
  private static int PreviousHour = 0;
  private static int PreviousDay = 0;
  private static void UpdateNetTime(ZNet __instance)
  {
    if (__instance.m_netTime == PreviousTime) return;
    var ticks = (long)(__instance.m_netTime * 10000000);
    var dayLength = EnvMan.instance.m_dayLengthSec;
    var hourLength = dayLength / 24.0;
    var minuteLength = hourLength / 60.0;
    var day = (int)(__instance.m_netTime / dayLength);
    var hours = __instance.m_netTime - (day * dayLength);
    var hour = (int)(hours / hourLength);
    var minute = (int)((hours - (hour * hourLength)) / minuteLength);
    if (PreviousTime != 0)
    {
      if (TrackTicks)
        Manager.HandleGlobal(ActionType.Time, ["tick", ticks.ToString()], Vector3.zero, false);
      if (TrackDays && PreviousDay != day)
        Manager.HandleGlobal(ActionType.Time, ["day", day.ToString()], Vector3.zero, false);
      if (TrackHours && PreviousHour != hour)
        Manager.HandleGlobal(ActionType.Time, ["hour", hour.ToString(), day.ToString()], Vector3.zero, false);
      if (TrackMinutes && PreviousMinute != minute)
        Manager.HandleGlobal(ActionType.Time, ["minute", minute.ToString(), hour.ToString(), day.ToString()], Vector3.zero, false);
    }
    PreviousTime = __instance.m_netTime;
    PreviousDay = day;
    PreviousHour = hour;
    PreviousMinute = minute;
  }
}
