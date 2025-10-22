using System.Collections.Generic;
using System.Linq;
using Data;
using Service;
using Splatform;

namespace ExpandWorld.Prefab;

public enum RpcTarget
{
  All,
  Owner,
  Search,
  ZDO
}

public abstract class RpcInfo
{
  protected abstract ZDOID GetId(ZDO zdo);
  private static readonly HashSet<string> Types = ["int", "long", "float", "bool", "string", "vec", "quat", "hash", "hit", "enum_reason", "enum_message", "enum_trap", "zdo"];
  public static bool IsType(string line) => Types.Contains(Parse.Kvp(line).Key);
  private readonly int Hash;
  private readonly RpcTarget Target;
  private readonly IStringValue? TargetParameter;
  private readonly IStringValue? SourceParameter;
  private readonly KeyValuePair<string, string>[] Parameters;
  private readonly IFloatValue? Delay;
  private readonly IIntValue? Repeat;
  private readonly IFloatValue? RepeatInterval;
  private readonly IFloatValue? RepeatChance;
  private readonly IFloatValue? Chance;
  public readonly IFloatValue? Weight;
  public readonly IBoolValue? Overwrite;
  public bool IsTarget => Target == RpcTarget.Search;
  private readonly bool Packaged;

  public RpcInfo(Dictionary<string, string> lines)
  {
    Target = RpcTarget.Owner;
    if (lines.TryGetValue("name", out var name))
      Hash = name.GetStableHashCode();

    if (lines.TryGetValue("source", out var source))
      SourceParameter = DataValue.String(source);

    if (lines.TryGetValue("packaged", out var packaged))
      Packaged = Parse.Boolean(packaged);

    if (lines.TryGetValue("target", out var target))
    {
      if (target == "all")
        Target = RpcTarget.All;
      else if (target == "search")
        Target = RpcTarget.Search;
      else if (target == "owner")
        Target = RpcTarget.Owner;
      else
      {
        Target = RpcTarget.ZDO;
        TargetParameter = DataValue.String(target);
      }
    }
    if (lines.TryGetValue("delay", out var d))
      Delay = DataValue.Float(d);
    if (lines.TryGetValue("repeat", out var r))
      Repeat = DataValue.Int(r);
    if (lines.TryGetValue("repeatInterval", out var ri))
      RepeatInterval = DataValue.Float(ri);
    if (lines.TryGetValue("repeatChance", out var rc))
      RepeatChance = DataValue.Float(rc);
    if (lines.TryGetValue("chance", out var c))
      Chance = DataValue.Float(c);
    if (lines.TryGetValue("weight", out var w))
      Weight = DataValue.Float(w);
    if (lines.TryGetValue("overwrite", out var o))
      Overwrite = DataValue.Bool(o);
    Parameters = [.. lines.OrderBy(p => int.TryParse(p.Key, out var k) ? k : 1000).Where(p => Parse.TryInt(p.Key, out var _)).Select(p => Parse.Kvp(p.Value))];
  }
  public void Invoke(ZDO zdo, Parameters pars)
  {
    var chance = Chance?.Get(pars) ?? 1f;
    if (chance < 1f && UnityEngine.Random.value > chance)
      return;

    var delay = Delay?.Get(pars) ?? 0f;
    var delays = GenerateDelays(delay, pars);
    var overwrite = Overwrite?.GetBool(pars) ?? false;
    if (delays != null)
    {
      foreach (var d in delays)
      {
        // Only first call should overwrite so next calls don't remove previous ones.
        Invoke(zdo, pars, d, overwrite);
        overwrite = false;
      }

    }
    else
      Invoke(zdo, pars, delay, overwrite);
  }
  private void Invoke(ZDO zdo, Parameters pars, float delay, bool overwrite)
  {
    var source = ZRoutedRpc.instance.m_id;
    var sourceParameter = SourceParameter?.Get(pars);
    if (sourceParameter != null && sourceParameter != "")
    {
      var id = Parse.ZdoId(sourceParameter);
      source = ZDOMan.instance.GetZDO(id)?.GetOwner() ?? 0;
    }
    var parameters = Packaged ? GetPackagedParameters(pars) : GetParameters(zdo, pars);
    if (Target == RpcTarget.Owner)
      DelayedRpc.Add(delay, source, zdo.GetOwner(), GetId(zdo), Hash, parameters, overwrite);
    else if (Target == RpcTarget.All)
      DelayedRpc.Add(delay, source, ZRoutedRpc.Everybody, GetId(zdo), Hash, parameters, overwrite);
    else if (Target == RpcTarget.ZDO)
    {
      var targetParameter = TargetParameter?.Get(pars);
      if (targetParameter != null && targetParameter != "")
      {
        var id = Parse.ZdoId(targetParameter);
        var peerId = ZDOMan.instance.GetZDO(id)?.GetOwner();
        if (peerId.HasValue)
          DelayedRpc.Add(delay, source, peerId.Value, GetId(zdo), Hash, parameters, overwrite);
      }
    }
  }
  public void InvokeGlobal(Parameters pars)
  {
    var chance = Chance?.Get(pars) ?? 1f;
    if (chance < 1f && UnityEngine.Random.value > chance)
      return;

    var delay = Delay?.Get(pars) ?? 0f;
    var delays = GenerateDelays(delay, pars);
    if (delays != null)
    {
      foreach (var d in delays)
        InvokeGlobal(pars, d);
    }
    else
      InvokeGlobal(pars, delay);
  }
  private void InvokeGlobal(Parameters pars, float delay)
  {
    var source = ZRoutedRpc.instance.m_id;
    var parameters = Packaged ? PackagedGetParameters(pars) : GetParameters(pars);
    var overwrite = Overwrite?.GetBool(pars) ?? false;
    DelayedRpc.Add(delay, source, ZRoutedRpc.Everybody, ZDOID.None, Hash, parameters, overwrite);
  }
  private List<float>? GenerateDelays(float delay, Parameters pars)
  {
    var repeat = Repeat?.Get(pars) ?? 0;
    var repeatInterval = RepeatInterval?.Get(pars) ?? delay;
    var repeatChance = RepeatChance?.Get(pars) ?? 1f;
    return Helper.GenerateDelays(delay, repeat, repeatInterval, repeatChance);
  }
  private object[] GetParameters(ZDO? zdo, Parameters pars)
  {
    var parameters = Parameters.Select(p => pars.Replace(p.Value)).ToArray<object>();
    for (var i = 0; i < parameters.Length; i++)
    {
      var type = Parameters[i].Key;
      var arg = (string)parameters[i];
      if (type == "int") parameters[i] = Calculator.EvaluateInt(arg) ?? 0;
      if (type == "long") parameters[i] = Calculator.EvaluateLong(arg) ?? 0;
      if (type == "float") parameters[i] = Calculator.EvaluateFloat(arg) ?? 0f;
      if (type == "bool") parameters[i] = Parse.Boolean(arg);
      if (type == "string") parameters[i] = arg;
      if (type == "vec") parameters[i] = Calculator.EvaluateVector3(arg);
      if (type == "quat") parameters[i] = Calculator.EvaluateQuaternion(arg);
      if (type == "hash") parameters[i] = arg.GetStableHashCode();
      if (type == "hit") parameters[i] = Parse.Hit(zdo, arg);
      if (type == "zdo") parameters[i] = Parse.ZdoId(arg);
      if (type == "enum_message") parameters[i] = Parse.EnumMessage(arg);
      if (type == "enum_reason") parameters[i] = Parse.EnumReason(arg);
      if (type == "enum_trap") parameters[i] = Parse.EnumTrap(arg);
      if (type == "enum_damagetext") parameters[i] = Parse.EnumDamageText(arg);
      if (type == "enum_terrainpaint") parameters[i] = Parse.EnumTerrainPaint(arg);
      if (type == "userinfo") parameters[i] = GetInfo(arg);
    }
    return parameters;
  }

  private UserInfo GetInfo(string arg) => new()
  {
    Name = arg == "" ? Game.instance.GetPlayerProfile().GetName() : arg,
    UserId = PlatformManager.DistributionPlatform.LocalUser.PlatformUserID
  };

  private object[] GetPackagedParameters(Parameters pars)
  {
    ZPackage pkg = new();
    var parameters = Parameters.Select(p => pars.Replace(p.Value)).ToArray<object>();
    for (var i = 0; i < parameters.Length; i++)
    {
      var type = Parameters[i].Key;
      var arg = (string)parameters[i];
      if (type == "int") pkg.Write(Calculator.EvaluateInt(arg) ?? 0);
      if (type == "long") pkg.Write(Calculator.EvaluateLong(arg) ?? 0);
      if (type == "float") pkg.Write(Calculator.EvaluateFloat(arg) ?? 0f);
      if (type == "bool") pkg.Write(Parse.Boolean(arg));
      if (type == "string") pkg.Write(arg);
      if (type == "vec") pkg.Write(Calculator.EvaluateVector3(arg));
      if (type == "quat") pkg.Write(Calculator.EvaluateQuaternion(arg));
      if (type == "hash") pkg.Write(arg.GetStableHashCode());
      if (type == "zdo") pkg.Write(Parse.ZdoId(arg));
      if (type == "enum_message") pkg.Write(Parse.EnumMessage(arg));
      if (type == "enum_reason") pkg.Write(Parse.EnumReason(arg));
      if (type == "enum_trap") pkg.Write(Parse.EnumTrap(arg));
      if (type == "enum_damagetext") pkg.Write(Parse.EnumDamageText(arg));
      if (type == "enum_terrainpaint") pkg.Write(Parse.EnumTerrainPaint(arg));
    }
    return [pkg];
  }

  private object[] GetParameters(Parameters pars) => GetParameters(null, pars);
  private object[] PackagedGetParameters(Parameters pars) => GetPackagedParameters(pars);
}


public class ObjectRpcInfo(Dictionary<string, string> lines) : RpcInfo(lines)
{
  protected override ZDOID GetId(ZDO zdo) => zdo.m_uid;
}
public class ClientRpcInfo(Dictionary<string, string> lines) : RpcInfo(lines)
{
  protected override ZDOID GetId(ZDO zdo) => ZDOID.None;
}