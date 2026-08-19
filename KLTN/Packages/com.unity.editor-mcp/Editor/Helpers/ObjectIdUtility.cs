using UnityEngine;

namespace UnityEditorMCP.Helpers
{
    internal static class ObjectIdUtility
    {
        public static object GetSerializableId(Object obj)
        {
            if (obj == null)
            {
                return null;
            }

#if UNITY_6000_5_OR_NEWER
            return obj.GetEntityId().ToString();
#else
            return obj.GetInstanceID();
#endif
        }

        public static string GetKey(Object obj)
        {
            return GetSerializableId(obj)?.ToString();
        }
    }
}
