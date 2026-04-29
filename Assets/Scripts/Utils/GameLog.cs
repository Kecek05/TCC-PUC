using System.Diagnostics;
using UnityObject = UnityEngine.Object;
using UnityDebug = UnityEngine.Debug;

/// <summary>
/// Build-time-conditional logging facade. <see cref="Info"/> and <see cref="Warn"/>
/// calls (including their argument evaluation, e.g. string interpolation) are stripped
/// by the compiler in release Player builds — no runtime cost.
/// <see cref="Error"/> is intentionally NOT conditional: real errors should still reach
/// logs/crash analytics in shipped builds.
/// </summary>
public static class GameLog
{
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Info(object message) => UnityDebug.Log(message);

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Info(object message, UnityObject context) => UnityDebug.Log(message, context);

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Warn(object message) => UnityDebug.LogWarning(message);

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Warn(object message, UnityObject context) => UnityDebug.LogWarning(message, context);

    public static void Error(object message) => UnityDebug.LogError(message);

    public static void Error(object message, UnityObject context) => UnityDebug.LogError(message, context);
}
