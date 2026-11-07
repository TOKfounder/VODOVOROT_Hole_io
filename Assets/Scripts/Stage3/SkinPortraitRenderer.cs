using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Vodovorot.Stage3
{
    /// <summary>Renders the complete catalogue visual, including WorldSpace UI, to an owned transparent texture.</summary>
    public static class SkinPortraitRenderer
    {
        private const int PortraitLayer = 31;

        public static Texture2D Render(SkinVisualCatalog.Entry entry, bool russian = true, int size = 512)
        {
            if (entry == null || entry.visualPrefab == null) return null;
            return RenderVisual(entry.visualPrefab, entry, russian, size);
        }

        /// <summary>The optional source is useful for checking a prepared shop visual against its saved prefab.</summary>
        public static Texture2D RenderVisual(GameObject source, SkinVisualCatalog.Entry entry, bool russian = true, int size = 512,
            Action<GameObject> beforeCapture = null)
        {
#if UNITY_EDITOR
            Vector3 origin = Vector3.zero;
#else
            Vector3 origin = Vector3.down * 1000;
#endif
            return RenderVisualAtOrigin(source, entry, russian, size, beforeCapture, origin);
        }

#if UNITY_EDITOR
        public static Texture2D RenderAtRuntimeOriginForReview(SkinVisualCatalog.Entry entry, bool russian, int size) =>
            RenderVisualAtOrigin(entry.visualPrefab, entry, russian, size, null, Vector3.down * 1000);
#endif

        private static Texture2D RenderVisualAtOrigin(GameObject source, SkinVisualCatalog.Entry entry, bool russian, int size,
            Action<GameObject> beforeCapture, Vector3 origin)
        {
            if (source == null || entry == null) return null;
            size = Mathf.Clamp(size, 128, 1024);
            GameObject stage = null;
            RenderTexture target = null, previous = RenderTexture.active;
            Texture2D image = null;
            Camera camera = null;
#if !UNITY_EDITOR
            var externalLightMasks = new System.Collections.Generic.Dictionary<Light, int>();
#endif
#if UNITY_EDITOR
            Scene preview = EditorSceneManager.NewPreviewScene();
#endif
            try
            {
                stage = new GameObject("Vodovorot complete skin portrait") { hideFlags = HideFlags.HideAndDontSave };
                stage.SetActive(false);
#if UNITY_EDITOR
                SceneManager.MoveGameObjectToScene(stage, preview);
#endif
                // Translation is applied consistently to both model and authored camera pose.
                stage.transform.position = origin;
                GameObject visual = UnityEngine.Object.Instantiate(source, stage.transform, false);
                visual.name = "Visual";
                foreach (Transform child in visual.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = PortraitLayer;
                foreach (SpiralRotation rotation in visual.GetComponentsInChildren<SpiralRotation>(true)) rotation.enabled = false;
                var caption = Find(visual.transform, entry.captionPath)?.GetComponent<Text>();
                if (caption != null)
                {
                    caption.text = (russian ? entry.nameRu : entry.nameEn).Replace(' ', '\n');
                    ImproveCaptionSampling(caption);
                }

                var cameraObject = new GameObject("Portrait camera", typeof(Camera));
                cameraObject.transform.SetParent(stage.transform, false);
                camera = cameraObject.GetComponent<Camera>();
                camera.enabled = false;
                camera.cullingMask = 1 << PortraitLayer;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear;
                camera.orthographic = true;
                camera.aspect = 1;
                camera.allowHDR = false;
                camera.allowMSAA = true;
                camera.useOcclusionCulling = false;
                camera.nearClipPlane = .01f;
                camera.farClipPlane = 30;
#if UNITY_EDITOR
                camera.scene = preview;
#endif
                foreach (Canvas canvas in visual.GetComponentsInChildren<Canvas>(true))
                {
                    canvas.renderMode = RenderMode.WorldSpace;
                    canvas.worldCamera = camera;
                }
                AddLight(stage.transform, "Portrait key", new Vector3(35, -35, 0), .95f);
                AddLight(stage.transform, "Portrait fill", new Vector3(30, 145, 0), .35f);
#if !UNITY_EDITOR
                // Directional lights reach any world position. The capture's private
                // layer must receive only its two neutral lights, regardless of map.
                foreach (Light light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
                    if (!light.transform.IsChildOf(stage.transform) && (light.cullingMask & (1 << PortraitLayer)) != 0)
                    { externalLightMasks.Add(light, light.cullingMask); light.cullingMask &= ~(1 << PortraitLayer); }
#endif
                stage.SetActive(true);
                Canvas.ForceUpdateCanvases();
                if (!TryBounds(visual, out Bounds initial)) return null;
                Vector3 viewing = initial.center - (stage.transform.position + entry.authoredCameraPosition);
                if (viewing.sqrMagnitude < .001f) viewing = entry.authoredCameraRotation * Vector3.forward;
                viewing.Normalize();
                // Only a uniform outer normalization is allowed: the shop's nested nonuniform scales stay intact.
                float dimension = Mathf.Max(initial.size.x, initial.size.y, initial.size.z);
                float normalization = 1.5f / Mathf.Max(.00001f, dimension);
                Vector3 offset = stage.transform.position;
                visual.transform.position = offset + (visual.transform.position - initial.center) * normalization;
                visual.transform.localScale *= normalization;
                Canvas.ForceUpdateCanvases();
                if (!TryBounds(visual, out Bounds bounds)) return null;
                camera.transform.rotation = Quaternion.LookRotation(viewing, Vector3.up);
                camera.transform.position = bounds.center - viewing * 5;
                // The store already turns captions toward its camera. Repeat that cosmetic pose for this camera.
                if (caption != null)
                    caption.transform.rotation = Quaternion.LookRotation(caption.transform.position - camera.transform.position, Vector3.up);
                Canvas.ForceUpdateCanvases();
                TryBounds(visual, out bounds);
                camera.transform.position = bounds.center - viewing * 5;
                camera.orthographicSize = ProjectedHalfSize(bounds, camera.transform.rotation) * 1.12f;
                target = RenderTexture.GetTemporary(size, size, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = target;
                // Validation can omit a component after framing, keeping camera and scale identical.
                beforeCapture?.Invoke(visual);
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                image = new Texture2D(size, size, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                image.Apply();
                Texture2D result = image; image = null;
                return result;
            }
            finally
            {
#if !UNITY_EDITOR
                foreach (var pair in externalLightMasks) if (pair.Key != null) pair.Key.cullingMask = pair.Value;
#endif
                RenderTexture.active = previous;
                if (camera != null) camera.targetTexture = null;
                if (target != null) RenderTexture.ReleaseTemporary(target);
                if (image != null) Destroy(image);
                if (stage != null) { stage.SetActive(false); Destroy(stage); }
#if UNITY_EDITOR
                EditorSceneManager.ClosePreviewScene(preview);
#endif
            }
        }

        private static Transform Find(Transform root, string path) => string.IsNullOrEmpty(path) ? root : root.Find(path);

        private static void ImproveCaptionSampling(Text caption)
        {
            // The authored world-space label is small in texture pixels. Increase only
            // its offscreen clone's glyph sampling; its world bounds and style stay identical.
            const int density = 4;
            RectTransform rect = caption.rectTransform;
            Vector3 position = rect.localPosition;
            Vector2 size = rect.rect.size;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x * density);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y * density);
            rect.localScale /= density; rect.localPosition = position;
            caption.fontSize *= density;
            caption.resizeTextMinSize *= density;
            caption.resizeTextMaxSize *= density;
            foreach (Shadow shadow in caption.GetComponents<Shadow>()) shadow.effectDistance *= density;
        }

        private static void AddLight(Transform parent, string name, Vector3 angles, float intensity)
        {
            var root = new GameObject(name, typeof(Light)); root.transform.SetParent(parent, false);
            root.transform.rotation = Quaternion.Euler(angles);
            Light light = root.GetComponent<Light>();
            light.type = LightType.Directional; light.color = Color.white; light.intensity = intensity;
            light.cullingMask = 1 << PortraitLayer; light.shadows = LightShadows.None;
        }

        public static bool TryBounds(GameObject visual, out Bounds bounds)
        {
            bounds = default; bool found = false;
            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(false))
            {
                if (!renderer.enabled) continue;
                Include(ref bounds, ref found, renderer.bounds);
            }
            foreach (Graphic graphic in visual.GetComponentsInChildren<Graphic>(false))
            {
                if (!graphic.isActiveAndEnabled) continue;
                var corners = new Vector3[4]; graphic.rectTransform.GetWorldCorners(corners);
                foreach (Vector3 corner in corners)
                    Include(ref bounds, ref found, new Bounds(corner, Vector3.zero));
            }
            return found;
        }

        private static void Include(ref Bounds bounds, ref bool found, Bounds next)
        {
            if (!found) { bounds = next; found = true; } else bounds.Encapsulate(next);
        }

        private static float ProjectedHalfSize(Bounds bounds, Quaternion rotation)
        {
            Quaternion inverse = Quaternion.Inverse(rotation); float result = .01f;
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = inverse * Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                        result = Mathf.Max(result, Mathf.Abs(corner.x), Mathf.Abs(corner.y));
                    }
            return result;
        }

        private static void Destroy(UnityEngine.Object item)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(item);
            else UnityEngine.Object.DestroyImmediate(item);
        }
    }
}
