using System.Collections.Generic;
using UnityEngine;

namespace Vodovorot.RoundExperience
{
    /// <summary>Owns only temporary renderer overrides; source assets remain untouched.</summary>
    internal sealed class CutawaySurfaceFade
    {
        private static readonly int AlphaId = Shader.PropertyToID("_CutawayAlpha");
        private readonly Renderer renderer;
        private readonly Material[] originals;
        private readonly Material[] faded;
        private readonly MaterialPropertyBlock[] originalBlocks;
        private readonly MaterialPropertyBlock working = new MaterialPropertyBlock();
        private bool installed;

        public CutawaySurfaceFade(Renderer renderer, Shader shader, Dictionary<Material, Material> cache)
        {
            this.renderer = renderer;
            originals = renderer.sharedMaterials;
            faded = new Material[originals.Length];
            originalBlocks = new MaterialPropertyBlock[originals.Length];
            for (int i = 0; i < originals.Length; i++)
            {
                Material source = originals[i];
                if (source == null)
                    continue;
                if (!cache.TryGetValue(source, out Material material))
                {
                    material = new Material(shader) { name = source.name + " (temporary cutaway)", hideFlags = HideFlags.HideAndDontSave };
                    if (source.HasProperty("_MainTex"))
                    {
                        material.mainTexture = source.mainTexture;
                        material.mainTextureScale = source.mainTextureScale;
                        material.mainTextureOffset = source.mainTextureOffset;
                    }
                    material.color = source.HasProperty("_Color") ? source.color : Color.white;
                    material.SetFloat("_Unlit", source.shader.name.Contains("Unlit") ? 1f : 0f);
                    cache.Add(source, material);
                }
                faded[i] = material;
                originalBlocks[i] = new MaterialPropertyBlock();
            }
        }

        public void Apply(float opacity)
        {
            if (renderer == null)
                return;
            if (!installed)
            {
                for (int i = 0; i < originalBlocks.Length; i++)
                    if (originalBlocks[i] != null)
                        renderer.GetPropertyBlock(originalBlocks[i], i);
                renderer.sharedMaterials = faded;
                installed = true;
            }
            for (int i = 0; i < faded.Length; i++)
            {
                if (faded[i] == null)
                    continue;
                // Per-material overrides take precedence over the whole-renderer block.
                // Preserve either existing source rather than erasing tint/texture overrides.
                if (originalBlocks[i].isEmpty)
                    renderer.GetPropertyBlock(working);
                else
                    renderer.GetPropertyBlock(working, i);
                working.SetFloat(AlphaId, opacity);
                renderer.SetPropertyBlock(working, i);
            }
        }

        public void Restore()
        {
            if (!installed || renderer == null)
                return;
            renderer.sharedMaterials = originals;
            for (int i = 0; i < originalBlocks.Length; i++)
                renderer.SetPropertyBlock(originalBlocks[i] == null || originalBlocks[i].isEmpty ? null : originalBlocks[i], i);
            installed = false;
        }
    }
}
