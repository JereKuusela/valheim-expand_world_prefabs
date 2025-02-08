using System.Collections.Generic;
using System.Linq;
using Data;
using Service;

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
    Parameters = [.. lines.OrderBy(p => int.TryParse(p.Key, out var k) ? k : 1000).Where(p => Parse.TryInt(p.Key, out var _)).Select(p => Parse.Kvp(p.Value))];
  }
  public void Invoke(ZDO zdo, Parameters pars)
  {
    var source = ZRoutedRpc.instance.m_id;
    var sourceParameter = SourceParameter?.Get(pars);
    if (sourceParameter != null && sourceParameter != "")
    {
      var id = Parse.ZdoId(sourceParameter);
      source = ZDOMan.instance.GetZDO(id)?.GetOwner() ?? 0;
    }
    var delay = Delay?.Get(pars) ?? 0f;
    var parameters = Packaged ? GetPackagedParameters(zdo, pars) : GetParameters(zdo, pars);
    if (Target == RpcTarget.Owner)
      DelayedRpc.Add(delay, source, zdo.GetOwner(), GetId(zdo), Hash, parameters);
    else if (Target == RpcTarget.All)
      DelayedRpc.Add(delay, source, ZRoutedRpc.Everybody, GetId(zdo), Hash, parameters);
    else if (Target == RpcTarget.ZDO)
    {
      var targetParameter = TargetParameter?.Get(pars);
      if (targetParameter != null && targetParameter != "")
      {
        var id = Parse.ZdoId(targetParameter);
        var peerId = ZDOMan.instance.GetZDO(id)?.GetOwner();
        if (peerId.HasValue)
          DelayedRpc.Add(delay, source, peerId.Value, GetId(zdo), Hash, parameters);
      }
    }
  }
  public void InvokeGlobal(Parameters pars)
  {
    var source = ZRoutedRpc.instance.m_id;
    var parameters = Packaged ? PackagedGetParameters(pars) : GetParameters(pars);
    var delay = Delay?.Get(pars) ?? 0f;
    DelayedRpc.Add(delay, source, ZRoutedRpc.Everybody, ZDOID.None, Hash, parameters);
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
      if (type == "userinfo") parameters[i] = arg == "" ? UserInfo.GetLocalUser() : new() { Gamertag = "", Name = arg, NetworkUserId = PrivilegeManager.GetNetworkUserId() };
    }
    return parameters;
  }
  private object[] GetPackagedParameters(ZDO? zdo, Parameters pars)
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
  private object[] PackagedGetParameters(Parameters pars) => GetPackagedParameters(null, pars);
}


public class ObjectRpcInfo(Dictionary<string, string> lines) : RpcInfo(lines)
{
  protected override ZDOID GetId(ZDO zdo) => zdo.m_uid;
}
public class ClientRpcInfo(Dictionary<string, string> lines) : RpcInfo(lines)
{
  protected override ZDOID GetId(ZDO zdo) => ZDOID.None;
}