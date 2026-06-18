using UnityEngine;

namespace LunaWolfStudiosEditor.ScriptableSheets.Shared
{
	public static class ObjectUtility
	{
		/// <summary>
		/// Returns a session-stable integer identifier for an Object.
		/// Unity 6.4 deprecated <see cref="Object.GetInstanceID"/> in favor of GetEntityId, which returns an
		/// EntityId rather than an int. We hash the EntityId to retain an int identity for serialization and
		/// lookups because the implicit EntityId to int conversion is itself obsolete.
		/// </summary>
		public static int GetInstanceIdCompat(this Object obj)
		{
#if UNITY_6000_4_OR_NEWER
			return obj.GetEntityId().GetHashCode();
#else
			return obj.GetInstanceID();
#endif
		}
	}
}
