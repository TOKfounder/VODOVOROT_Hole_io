using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vodovorot.Stage3
{
    /// <summary>
    /// Complete shop visuals, separate from ownership, prices and gameplay skins.
    /// Legacy save/payment identities stay fixed even when a future view changes its order.
    /// The builder copies body, water, decoration and the real world-space caption together.
    /// </summary>
    public sealed class SkinVisualCatalog : ScriptableObject
    {
        public const string ResourcePath = "Stage3/SkinVisualCatalog";
        private static SkinVisualCatalog cached;

        [Serializable]
        public sealed class Entry
        {
            public string id;
            public int legacyIndex;
            public GameObject visualPrefab;
            [TextArea] public string nameRu;
            [TextArea] public string nameEn;

            [Header("Exact shop source and paths inside the visual prefab")]
            public string sourceScenePath;
            public string sourceRootPath;
            public string sourceGlobalObjectId;
            public string modelRootPath;
            public string bodyPath;
            public string captionPath;
            public string waterPath;

            [Header("Authored shop camera; no gameplay camera dependency")]
            public Vector3 authoredCameraPosition;
            public Quaternion authoredCameraRotation = Quaternion.identity;
            public float authoredFieldOfView;
            public bool authoredOrthographic;
            public float authoredOrthographicSize;

            [Header("Extraction evidence")]
            public int sourceTransformCount;
            public int sourceMeshRendererCount;
            public int sourceGraphicCount;
            public string sourceBodyMaterialPath;
            public string sourceWaterMaterialPath;
            public string poseDescription;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();
        public IReadOnlyList<Entry> Entries => entries;

        public static SkinVisualCatalog Load()
        {
            if (cached == null) cached = Resources.Load<SkinVisualCatalog>(ResourcePath);
            if (cached == null)
                throw new InvalidOperationException("Missing full shop visual catalog. Run Tools/Vodovorot/Stage 3/Build full skin visual catalog.");
            cached.ValidateIdentity();
            return cached;
        }

        public bool TryGetById(string id, out Entry entry)
        {
            if (!string.IsNullOrEmpty(id) && entries != null) foreach (Entry candidate in entries)
            {
                if (candidate != null && string.Equals(candidate.id, id, StringComparison.Ordinal))
                { entry = candidate; return true; }
            }
            entry = null; return false;
        }

        public Entry GetByLegacyIndex(int index)
        {
            string expectedId = IdForLegacyIndex(index);
            if (TryGetById(expectedId, out Entry entry) && entry.legacyIndex == index) return entry;
            throw new InvalidOperationException("Catalog has no full visual for legacy skin " + index + " (" + expectedId + ").");
        }

        public static string IdForLegacyIndex(int index)
        {
            switch (index)
            {
                case 0: return "obodok";
                case 1: return "white";
                case 2: return "gold";
                case 3: return "scrag";
                case 4: return "lord";
                default: throw new ArgumentOutOfRangeException(nameof(index), index, "The existing five legacy skin identities are fixed.");
            }
        }

        public void ValidateIdentity()
        {
            if (entries == null || entries.Length != 5)
                throw new InvalidOperationException("Full shop visual catalog must contain exactly the five existing skins.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (Entry entry in entries)
            {
                if (entry == null || entry.legacyIndex < 0 || entry.legacyIndex > 4 || entry.visualPrefab == null ||
                    entry.id != IdForLegacyIndex(entry.legacyIndex) || !ids.Add(entry.id))
                    throw new InvalidOperationException("Full shop visual catalog contains an incomplete or remapped legacy identity.");
            }
        }

#if UNITY_EDITOR
        public void EditorSetEntries(Entry[] builtEntries)
        {
            entries = builtEntries ?? throw new ArgumentNullException(nameof(builtEntries));
            ValidateIdentity();
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() { cached = null; }
    }
}
