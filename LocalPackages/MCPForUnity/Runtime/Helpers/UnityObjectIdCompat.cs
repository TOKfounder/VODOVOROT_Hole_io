using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Object = UnityEngine.Object;

namespace MCPForUnity.Runtime.Helpers
{
    // Part of MCP for Unity's compat-shim family. See UnityCompatShims.cs in this
    // folder for the full list of shims, the audit policy, and the reflection pattern.
    /// <summary>
    /// Version-gated wrappers for the InstanceID ↔ EntityId migration introduced in Unity 6.5
    /// and tightened in 6.6.
    ///   Forward (Object → int): <see cref="GetInstanceIDCompat"/>
    ///   Reverse (int → Object, Editor-only): <see cref="InstanceIDToObjectCompat"/>
    /// </summary>
    public static class UnityObjectIdCompat
    {
        /// <summary>
        /// Returns an opaque session-scoped handle for the legacy int wire format.
        /// The registry retains the full EntityId; a handle is never a truncated ID.
        /// Serialized references also carry the full entityID field on Unity 6.5+.
        /// </summary>
        public static int GetInstanceIDCompat(this Object obj)
        {
            if (obj == null)
            {
                return 0;
            }

#if UNITY_6000_5_OR_NEWER
            return GetOrCreateHandle(obj.GetEntityId());
#else
            return obj.GetInstanceID();
#endif
        }

#if UNITY_6000_5_OR_NEWER
        private static readonly Dictionary<EntityId, int> Handles = new Dictionary<EntityId, int>();
        private static readonly Dictionary<int, EntityId> Entities = new Dictionary<int, EntityId>();
        private static int nextHandle = -1;

        private static int GetOrCreateHandle(EntityId entityId)
        {
            if (Handles.TryGetValue(entityId, out int handle))
                return handle;
            if (nextHandle == int.MinValue)
                throw new InvalidOperationException("MCP session object handles exhausted.");

            handle = nextHandle--;
            Handles.Add(entityId, handle);
            Entities.Add(handle, entityId);
            return handle;
        }

#if UNITY_EDITOR
        private const string RegistryKey = "MCPForUnity.EntityHandles.v1";

        [Serializable]
        private sealed class HandleSnapshot
        {
            public int nextHandle = -1;
            public int[] handles;
            public string[] entityIds;
        }

        static UnityObjectIdCompat()
        {
            string json = SessionState.GetString(RegistryKey, "");
            if (!string.IsNullOrEmpty(json))
            {
                var snapshot = JsonUtility.FromJson<HandleSnapshot>(json);
                if (snapshot != null && snapshot.handles != null && snapshot.entityIds != null
                    && snapshot.handles.Length == snapshot.entityIds.Length)
                {
                    nextHandle = snapshot.nextHandle;
                    for (int i = 0; i < snapshot.handles.Length; i++)
                    {
                        if (!ulong.TryParse(snapshot.entityIds[i], NumberStyles.None,
                            CultureInfo.InvariantCulture, out ulong raw))
                            continue;
                        var entity = EntityId.FromULong(raw);
                        Handles[entity] = snapshot.handles[i];
                        Entities[snapshot.handles[i]] = entity;
                    }
                }
            }
            // Keep tool references valid through recompilation/Play domain reloads.
            // SessionState is cleared when the Editor quits, just like object identity.
            AssemblyReloadEvents.beforeAssemblyReload += SaveHandles;
        }

        private static void SaveHandles()
        {
            var snapshot = new HandleSnapshot
            {
                nextHandle = nextHandle,
                handles = new int[Entities.Count],
                entityIds = new string[Entities.Count]
            };
            int index = 0;
            foreach (var entry in Entities)
            {
                snapshot.handles[index] = entry.Key;
                snapshot.entityIds[index] = EntityId.ToULong(entry.Value)
                    .ToString(CultureInfo.InvariantCulture);
                index++;
            }
            SessionState.SetString(RegistryKey, JsonUtility.ToJson(snapshot));
        }
#endif
#endif

#if UNITY_EDITOR
        /// <summary>
        /// Resolves an int instance ID handle back to a UnityEngine.Object.
        ///   Pre-6.0  : EditorUtility.InstanceIDToObject(int)
        ///   6.0–6.4  : EditorUtility.EntityIdToObject(int)
        ///   6.5+     : resolve the opaque handle to its complete EntityId.
        /// </summary>
        public static Object InstanceIDToObjectCompat(int instanceId)
        {
#if UNITY_6000_5_OR_NEWER
            return Entities.TryGetValue(instanceId, out EntityId entityId)
                ? EditorUtility.EntityIdToObject(entityId) : null;
#elif UNITY_6000_3_OR_NEWER
            return EditorUtility.EntityIdToObject(instanceId);
#else
            return EditorUtility.InstanceIDToObject(instanceId);
#endif
        }
#endif
    }
}
