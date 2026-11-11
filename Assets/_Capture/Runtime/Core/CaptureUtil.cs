#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Reflection;
using UnityEngine;

namespace KeceK.Capture
{
    /// <summary>Finds UI by name the way a script would describe it ("DeckEntry2", "BattleButton").</summary>
    public static class UiFinder
    {
        public static RectTransform Find(string name, string underName = "")
        {
            RectTransform best = null;

            foreach (RectTransform rect in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (rect.name != name || !rect.gameObject.activeInHierarchy) continue;
                if (!string.IsNullOrEmpty(underName) && !HasAncestor(rect, underName)) continue;
                if (rect.rect.width <= 0f && rect.rect.height <= 0f && rect.childCount == 0) continue;

                best = rect;
                break;
            }

            return best;
        }

        public static GameObject FindObject(string name)
        {
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == name) return t.gameObject;
            return null;
        }

        private static bool HasAncestor(Transform t, string name)
        {
            for (Transform p = t.parent; p != null; p = p.parent)
                if (p.name == name) return true;
            return false;
        }
    }

    /// <summary>
    /// Reaches private members of game components. Only for the camera rig and the bot's fill timer, where
    /// the capture needs the game's own code path but the game exposes no seam — never to change gameplay.
    /// </summary>
    public static class Reflect
    {
        private const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        public static void Call(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, Any)?.Invoke(target, args);

        public static T Get<T>(object target, string member)
        {
            System.Type type = target.GetType();
            PropertyInfo property = type.GetProperty(member, Any);
            if (property != null) return (T)property.GetValue(target);

            FieldInfo field = type.GetField(member, Any);
            return field != null ? (T)field.GetValue(target) : default;
        }

        public static void Set(object target, string member, object value)
        {
            System.Type type = target.GetType();
            FieldInfo field = type.GetField(member, Any);
            if (field != null) field.SetValue(target, value);
        }
    }
}
#endif
