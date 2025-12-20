using System;
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

  public static void PatchRealTime(Harmony harmony, bool trackSeconds, bool trackMinutes, bool trackHours, bool trackDays)
  {
    TrackRealSeconds = trackSeconds;
    TrackRealMinutes = trackMinutes;
    TrackRealHours = trackHours;
    TrackRealDays = trackDays;
    var method = AccessTools.Method(typeof(ZNet), nameof(ZNet.UpdateNetTime));
    var patch = AccessTools.Method(typeof(HandleTime), nameof(UpdateRealTime));
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

  private static bool TrackRealSeconds = false;
  private static bool TrackRealMinutes = false;
  private static bool TrackRealHours = false;
  private static bool TrackRealDays = false;
  private static long PreviousRealSecond = 0;
  private static int PreviousRealMinute = 0;
  private static int PreviousRealHour = 0;
  private static int PreviousRealDay = 0;
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

  private static void UpdateRealTime()
  {
    var now = DateTimeOffset.UtcNow;
    var localTime = now.ToLocalTime();
    var second = now.ToUnixTimeSeconds();
    var minute = localTime.Minute;
    var hour = localTime.Hour;
    var day = localTime.DayOfYear;

    if (PreviousRealSecond != 0)
    {
      if (TrackRealSeconds && PreviousRealSecond != second)
        Manager.HandleGlobal(ActionType.RealTime, ["second", second.ToString()], Vector3.zero, false);
      if (TrackRealDays && PreviousRealDay != day)
        Manager.HandleGlobal(ActionType.RealTime, ["day", day.ToString()], Vector3.zero, false);
      if (TrackRealHours && PreviousRealHour != hour)
        Manager.HandleGlobal(ActionType.RealTime, ["hour", hour.ToString(), day.ToString()], Vector3.zero, false);
      if (TrackRealMinutes && PreviousRealMinute != minute)
        Manager.HandleGlobal(ActionType.RealTime, ["minute", minute.ToString(), hour.ToString(), day.ToString()], Vector3.zero, false);
    }
    PreviousRealSecond = second;
    PreviousRealDay = day;
    PreviousRealHour = hour;
    PreviousRealMinute = minute;
  }
}
