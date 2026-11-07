using System;
using UnityEngine;
using UnityEngine.UI;

namespace Vodovorot.Stage3
{
    /// <summary>Instantiates the same complete catalog model in a live authored shop slot.</summary>
    public static class SkinVisualFactory
    {
        public sealed class ShopVisual
        {
            public readonly SkinVisualCatalog.Entry entry;
            public readonly Transform root;
            public readonly Text caption;

            internal ShopVisual(SkinVisualCatalog.Entry entry, Transform root, Text caption)
            { this.entry = entry; this.root = root; this.caption = caption; }
        }

        /// <summary>
        /// Portrait-only outer coordinate frames stay in the catalog prefab. The live shop
        /// already has those frames, so extract only the complete model and keep this slot's
        /// original local pose, scale and order. Remap caption references before retiring it.
        /// </summary>
        public static ShopVisual ReplaceShopVisual(SkinVisualCatalog.Entry entry, Transform existingSlot,
            bool russian, Action<Text, Text> remapCaption = null)
        {
            if (entry == null || entry.visualPrefab == null || existingSlot == null || existingSlot.parent == null)
                throw new ArgumentException("A complete catalog entry and an existing authored shop slot are required.");
            if (entry.id != SkinVisualCatalog.IdForLegacyIndex(entry.legacyIndex))
                throw new InvalidOperationException("The skin's stable identity does not match its historical index.");
            string relativeCaption = RelativeModelPath(entry.captionPath, entry.modelRootPath);
            Text originalCaption = existingSlot.Find(relativeCaption)?.GetComponent<Text>();
            if (originalCaption == null) throw new InvalidOperationException("Existing shop slot has no original caption: " + existingSlot.name);
            Transform parent = existingSlot.parent;
            Vector3 localPosition = existingSlot.localPosition, localScale = existingSlot.localScale;
            Quaternion localRotation = existingSlot.localRotation;
            int sibling = existingSlot.GetSiblingIndex();
            bool active = existingSlot.gameObject.activeSelf;
            var holder = new GameObject("Temporary full skin extraction");
            holder.SetActive(false);
            Transform replacement = null;
            bool committed = false;
            try
            {
                GameObject complete = UnityEngine.Object.Instantiate(entry.visualPrefab, holder.transform, false);
                replacement = complete.transform.Find(entry.modelRootPath);
                Text caption = replacement != null ? replacement.Find(relativeCaption)?.GetComponent<Text>() : null;
                if (replacement == null || caption == null)
                    throw new InvalidOperationException("Catalog prefab is missing its complete shop root or caption: " + entry.id);
                replacement.gameObject.SetActive(false);
                replacement.SetParent(parent, false);
                replacement.name = existingSlot.name;
                replacement.localPosition = localPosition;
                replacement.localRotation = localRotation;
                replacement.localScale = localScale;
                replacement.SetSiblingIndex(sibling);
                var visual = new ShopVisual(entry, replacement, caption);
                ApplyCaptionLanguage(visual, russian);
                remapCaption?.Invoke(originalCaption, caption);
                existingSlot.gameObject.SetActive(false);
                existingSlot.SetParent(null, false); // Destroy is deferred in play; keep carousel child count exactly five now.
                replacement.gameObject.SetActive(active);
                Destroy(existingSlot.gameObject);
                committed = true;
                return visual;
            }
            finally
            {
                if (!committed && replacement != null)
                {
                    replacement.gameObject.SetActive(false);
                    replacement.SetParent(null, false);
                    Destroy(replacement.gameObject);
                }
                Destroy(holder);
            }
        }

        public static void ApplyCaptionLanguage(ShopVisual visual, bool russian)
        {
            if (visual == null || visual.caption == null) return;
            string name = LocalizedName(visual.entry, russian);
            if (visual.caption.text != name) visual.caption.text = name;
        }

        public static string LocalizedName(SkinVisualCatalog.Entry entry, bool russian)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            return russian ? entry.nameRu : entry.nameEn;
        }

        private static string RelativeModelPath(string path, string modelPath)
        {
            string prefix = modelPath + "/";
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(modelPath) || !path.StartsWith(prefix, StringComparison.Ordinal))
                throw new InvalidOperationException("Catalog component path is outside its complete model root.");
            return path.Substring(prefix.Length);
        }

        private static void Destroy(UnityEngine.Object item)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(item);
            else UnityEngine.Object.DestroyImmediate(item);
        }
    }
}
