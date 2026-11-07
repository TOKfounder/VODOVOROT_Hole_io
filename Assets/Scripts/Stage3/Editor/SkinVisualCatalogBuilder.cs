#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Vodovorot.Stage3.Editor
{
    /// <summary>
    /// Extracts complete shop visuals from a temporary preview of the saved MainMenu.
    /// Never saves that scene or changes the shop, gameplay material array or profile.
    /// </summary>
    public static class SkinVisualCatalogBuilder
    {
        public const string SourceScenePath = "Assets/Scenes/MainMenu.unity";
        public const string SourceCarouselPath = "CanvasForDesktop/PanelOfSkins/LayoutGroup3D";
        public const string AssetPath = "Assets/Resources/Stage3/SkinVisualCatalog.asset";
        public const string PrefabDirectory = "Assets/Stage3/SkinVisuals";
        private static readonly string[] NamesRu = { "Красный\nТазик", "Белый\nДруг", "Золотой\nунитаз", "Трон\nКощея", "Туалет\nБога" };
        private static readonly string[] NamesEn = { "Red\nbasin", "White\nFriend", "Golden\nbowl", "Scrag's\nThrone", "God's\nToilet" };

        // Fail closed if a source gains an unfamiliar controller or physical component.
        // SpiralRotation is the existing pure visual water rotation, not a hole controller.
        private static readonly HashSet<Type> VisualComponents = new HashSet<Type>
        {
            typeof(Transform), typeof(RectTransform), typeof(MeshFilter), typeof(MeshRenderer),
            typeof(CanvasRenderer), typeof(Text), typeof(Image), typeof(Shadow), typeof(Outline), typeof(SpiralRotation)
        };

        [MenuItem("Tools/Vodovorot/Stage 3/Build full skin visual catalog")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Build the visual catalog from Edit Mode.");
            byte[] savedScene = File.ReadAllBytes(SourceScenePath);
            Scene sourceScene = default;
            Scene stagingScene = default;
            try
            {
                sourceScene = OpenSourcePreviewScene(SourceScenePath);
                Transform sourceCarousel = FindScenePath(sourceScene, SourceCarouselPath);
                HorizontalLayout3D carousel = sourceCarousel.GetComponent<HorizontalLayout3D>();
                if (carousel == null || carousel.targetCamera == null)
                    throw new InvalidOperationException("The canonical shop carousel or its authored camera is missing.");
                ValidateLegacyIds(carousel);
                Canvas sourceCanvas = sourceCarousel.GetComponentInParent<Canvas>(true);
                if (sourceCanvas == null || sourceCanvas.renderMode != RenderMode.WorldSpace ||
                    sourceCanvas.transform.name != "CanvasForDesktop")
                    throw new InvalidOperationException("Expected the author's WorldSpace CanvasForDesktop.");
                Transform sourcePanel = sourceCarousel.parent;
                if (sourcePanel == null || sourcePanel.parent != sourceCanvas.transform)
                    throw new InvalidOperationException("The explicit Canvas/Panel/Carousel source frames changed; review extraction before rebuilding.");

                var sources = new Transform[5];
                for (int index = 0; index < sources.Length; index++)
                {
                    // Serialized child order is 2,3,4,0,1. It is not a skin identity.
                    string exactName = "SkinManicen (" + index + ")";
                    sources[index] = Children(sourceCarousel).Single(child => child.name == exactName);
                    ValidateSource(sources[index], index);
                }
                if (sourceCarousel.childCount != 5)
                    throw new InvalidOperationException("The canonical shop must have exactly five full mannequin roots.");
                if (carousel.captions == null || carousel.captions.Length != 5 ||
                    !new HashSet<GameObject>(carousel.captions).SetEquals(sources.Select(source => source.Find("Cap").gameObject)))
                    throw new InvalidOperationException("Carousel caption references do not cover the five real model captions.");

                // This method only arranges transforms and faces the real captions to the
                // authored camera. It does not invoke Start, purchases, equipment or saves.
                // Only the temporary preview scene is changed, never the saved source.
                carousel.ArrangeChildren();
                stagingScene = EditorSceneManager.NewPreviewScene();
                var built = new SkinVisualCatalog.Entry[5];
                EnsureFolder("Assets/Resources/Stage3");
                EnsureFolder(PrefabDirectory);
                for (int index = 0; index < sources.Length; index++)
                    built[index] = Extract(sources[index], index, sourceCanvas, sourcePanel, sourceCarousel, carousel.targetCamera, stagingScene);

                SkinVisualCatalog catalog = AssetDatabase.LoadAssetAtPath<SkinVisualCatalog>(AssetPath);
                if (catalog == null)
                {
                    catalog = ScriptableObject.CreateInstance<SkinVisualCatalog>();
                    AssetDatabase.CreateAsset(catalog, AssetPath);
                }
                catalog.EditorSetEntries(built);
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssetIfDirty(catalog);
                Debug.Log("[Stage3] Full shop visual catalog built: obodok, white, gold, scrag, lord. Body, water, caption, decoration and inactive children preserved. Existing scenes and consumers unchanged.");
            }
            finally
            {
                if (stagingScene.IsValid()) EditorSceneManager.ClosePreviewScene(stagingScene);
                if (sourceScene.IsValid()) EditorSceneManager.ClosePreviewScene(sourceScene);
                if (!savedScene.SequenceEqual(File.ReadAllBytes(SourceScenePath)))
                    throw new InvalidOperationException("The saved source scene changed while extracting the catalog. Review concurrent scene edits.");
            }
        }

        /// <summary>
        /// Unity 2022 exposes preview-scene opening internally. Resolve only its known
        /// isolated-scene signatures; never fall back to opening the current scene or
        /// creating a scene copy under Assets. Shared with the independent visual QA.
        /// </summary>
        public static Scene OpenSourcePreviewScene(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                throw new ArgumentException("A saved source scene is required.", nameof(path));
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            MethodInfo[] candidates = typeof(EditorSceneManager).GetMethods(flags)
                .Where(method => method.Name == "OpenPreviewScene" && method.ReturnType == typeof(Scene)).ToArray();
            MethodInfo opener = candidates.FirstOrDefault(method =>
            {
                ParameterInfo[] parameters = method.GetParameters();
                return parameters.Length == 1 && parameters[0].ParameterType == typeof(string);
            }) ?? candidates.FirstOrDefault(method =>
            {
                ParameterInfo[] parameters = method.GetParameters();
                return parameters.Length == 2 && parameters[0].ParameterType == typeof(string) && parameters[1].ParameterType == typeof(bool);
            });
            MethodInfo isPreview = typeof(EditorSceneManager).GetMethod("IsPreviewScene", flags, null, new[] { typeof(Scene) }, null);
            if (opener == null || isPreview == null || isPreview.ReturnType != typeof(bool))
                throw new NotSupportedException("This Unity Editor does not expose the checked isolated preview-scene API. No source scene was opened.");
            ParameterInfo[] signature = opener.GetParameters();
            object[] arguments = signature.Length == 1 ? new object[] { path } : new object[]
            {
                path, signature[1].HasDefaultValue && signature[1].DefaultValue is bool defaultFlag ? defaultFlag : false
            };
            try
            {
                Scene scene = (Scene)opener.Invoke(null, arguments);
                if (!scene.IsValid() || !scene.isLoaded || !(bool)isPreview.Invoke(null, new object[] { scene }))
                    throw new InvalidOperationException("Unity did not return a loaded isolated preview scene for " + path + ".");
                return scene;
            }
            catch (TargetInvocationException error)
            {
                throw new InvalidOperationException("Could not open isolated source preview scene: " + path, error.InnerException ?? error);
            }
        }

        private static SkinVisualCatalog.Entry Extract(Transform source, int index, Canvas sourceCanvas,
            Transform sourcePanel, Transform sourceCarousel, Camera camera, Scene stagingScene)
        {
            string id = SkinVisualCatalog.IdForLegacyIndex(index);
            var root = new GameObject(id + " · complete shop visual");
            SceneManager.MoveGameObjectToScene(root, stagingScene);
            try
            {
                // Preserve the complete coordinate chain instead of decomposing a sheared
                // world matrix or flattening the slot's nonuniform 50/225.4/50 scale.
                Transform canvasFrame = CopyFrame(sourceCanvas.transform, root.transform);
                Canvas canvas = canvasFrame.gameObject.AddComponent<Canvas>();
                EditorUtility.CopySerialized(sourceCanvas, canvas);
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = null; // The isolated renderer supplies its own camera.
                CanvasScaler originalScaler = sourceCanvas.GetComponent<CanvasScaler>();
                if (originalScaler != null)
                {
                    CanvasScaler scaler = canvasFrame.gameObject.AddComponent<CanvasScaler>();
                    EditorUtility.CopySerialized(originalScaler, scaler);
                }
                Transform panelFrame = CopyFrame(sourcePanel, canvasFrame);
                Transform carouselFrame = CopyFrame(sourceCarousel, panelFrame);
                GameObject model = UnityEngine.Object.Instantiate(source.gameObject, carouselFrame, false);
                model.name = source.name;
                model.transform.localPosition = source.localPosition;
                model.transform.localRotation = source.localRotation;
                model.transform.localScale = source.localScale;
                ValidateExactClone(source, model.transform);

                string prefabPath = PrefabDirectory + "/" + id + ".prefab";
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out bool saved);
                if (!saved || prefab == null) throw new InvalidOperationException("Could not save full visual prefab: " + prefabPath);
                string modelPath = SourceCarouselPath + "/" + source.name;
                Transform savedModel = prefab.transform.Find(modelPath);
                if (savedModel == null) throw new InvalidOperationException("Saved prefab lost its complete model root: " + id);
                ValidateExactClone(source, savedModel);
                if (prefab.GetComponentsInChildren<Collider>(true).Length != 0 ||
                    prefab.GetComponentsInChildren<Rigidbody>(true).Length != 0 ||
                    prefab.GetComponentsInChildren<HorizontalLayout3D>(true).Length != 0)
                    throw new InvalidOperationException("A visual prefab contains physics or a shop controller: " + id);

                MeshRenderer body = source.Find(index == 0 ? "obodok/Toilet" : "Toilet").GetComponent<MeshRenderer>();
                MeshRenderer water = source.Find("Cylinder").GetComponent<MeshRenderer>();
                return new SkinVisualCatalog.Entry
                {
                    id = id, legacyIndex = index, visualPrefab = prefab, nameRu = NamesRu[index], nameEn = NamesEn[index],
                    sourceScenePath = SourceScenePath, sourceRootPath = modelPath,
                    sourceGlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(source.gameObject).ToString(),
                    modelRootPath = modelPath, bodyPath = modelPath + (index == 0 ? "/obodok/Toilet" : "/Toilet"),
                    captionPath = modelPath + "/Cap", waterPath = modelPath + "/Cylinder",
                    authoredCameraPosition = camera.transform.position, authoredCameraRotation = camera.transform.rotation,
                    authoredFieldOfView = camera.fieldOfView, authoredOrthographic = camera.orthographic,
                    authoredOrthographicSize = camera.orthographicSize,
                    sourceTransformCount = source.GetComponentsInChildren<Transform>(true).Length,
                    sourceMeshRendererCount = source.GetComponentsInChildren<MeshRenderer>(true).Length,
                    sourceGraphicCount = source.GetComponentsInChildren<Graphic>(true).Length,
                    sourceBodyMaterialPath = AssetDatabase.GetAssetPath(body.sharedMaterial),
                    sourceWaterMaterialPath = AssetDatabase.GetAssetPath(water.sharedMaterial),
                    poseDescription = "Saved MainMenu desktop shop; ArrangeChildren applied only in temporary preview; Canvas/Panel/Carousel frames and all model local transforms preserved."
                };
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void ValidateLegacyIds(HorizontalLayout3D carousel)
        {
            var serialized = new SerializedObject(carousel);
            SerializedProperty ids = serialized.FindProperty("toiletIDs");
            if (ids == null || ids.arraySize != 5) throw new InvalidOperationException("The existing shop ID contract changed.");
            for (int index = 0; index < 5; index++)
                if (ids.GetArrayElementAtIndex(index).stringValue != SkinVisualCatalog.IdForLegacyIndex(index))
                    throw new InvalidOperationException("The existing shop ID/legacy-index contract changed at " + index + ".");
        }

        private static void ValidateSource(Transform source, int index)
        {
            foreach (Component component in source.GetComponentsInChildren<Component>(true))
                if (component == null || !VisualComponents.Contains(component.GetType()))
                    throw new InvalidOperationException("Review unfamiliar or nonvisual component in " + source.name + ": " + (component == null ? "missing script" : component.GetType().FullName));
            Transform body = source.Find(index == 0 ? "obodok/Toilet" : "Toilet");
            Transform water = source.Find("Cylinder"), caption = source.Find("Cap");
            if (body == null || body.GetComponent<MeshFilter>() == null || body.GetComponent<MeshRenderer>() == null ||
                water == null || water.GetComponent<MeshFilter>() == null || water.GetComponent<MeshRenderer>() == null ||
                water.GetComponent<SpiralRotation>() == null || caption == null || caption.GetComponent<Text>() == null ||
                caption.GetComponent<Shadow>() == null || !body.gameObject.activeSelf || !water.gameObject.activeSelf || !caption.gameObject.activeSelf)
                throw new InvalidOperationException("Incomplete body, water or real caption in " + source.name + ".");
            Text label = caption.GetComponent<Text>();
            if (label.font == null || NormalizeName(label.text) != NormalizeName(NamesRu[index]))
                throw new InvalidOperationException("Caption does not match the immutable skin identity: " + source.name + ".");
            if (index == 0 && (source.Find("board") == null || !source.Find("board").gameObject.activeSelf ||
                source.Find("board").GetComponent<Image>() == null || body.Find("Plane (1)") == null))
                throw new InvalidOperationException("The red basin's board or Plane decoration is missing.");
            foreach (MeshFilter mesh in source.GetComponentsInChildren<MeshFilter>(true))
                if (mesh.sharedMesh == null) throw new InvalidOperationException("Missing source mesh in " + source.name + ".");
            foreach (MeshRenderer renderer in source.GetComponentsInChildren<MeshRenderer>(true))
                if (renderer.sharedMaterials.Length == 0 || renderer.sharedMaterials.Any(material => material == null))
                    throw new InvalidOperationException("Missing original source material in " + source.name + ".");
        }

        private static string NormalizeName(string value) => value.Replace("\r", " ").Replace("\n", " ").Trim();

        private static Transform CopyFrame(Transform source, Transform parent)
        {
            var frame = new GameObject(source.name, source is RectTransform ? typeof(RectTransform) : typeof(Transform));
            frame.layer = source.gameObject.layer;
            frame.transform.SetParent(parent, false);
            if (source is RectTransform original)
            {
                var rect = (RectTransform)frame.transform;
                rect.anchorMin = original.anchorMin; rect.anchorMax = original.anchorMax;
                rect.pivot = original.pivot; rect.sizeDelta = original.sizeDelta;
                rect.anchoredPosition3D = original.anchoredPosition3D;
            }
            frame.transform.localPosition = source.localPosition;
            frame.transform.localRotation = source.localRotation;
            frame.transform.localScale = source.localScale;
            return frame.transform;
        }

        private static void ValidateExactClone(Transform source, Transform clone)
        {
            string context = source.name;
            Require(source.name == clone.name && source.childCount == clone.childCount &&
                source.gameObject.activeSelf == clone.gameObject.activeSelf && source.gameObject.layer == clone.gameObject.layer,
                context + ": hierarchy/active state/layer");
            Require(Vector3.Distance(source.localPosition, clone.localPosition) < .0001f &&
                Vector3.Distance(source.localScale, clone.localScale) < .0001f && Quaternion.Angle(source.localRotation, clone.localRotation) < .005f,
                context + ": local transform");
            for (int element = 0; element < 16; element++)
                Require(Mathf.Abs(source.localToWorldMatrix[element] - clone.localToWorldMatrix[element]) < .002f, context + ": inherited world matrix");
            Component[] originals = source.GetComponents<Component>(), copies = clone.GetComponents<Component>();
            Require(originals.Length == copies.Length, context + ": component count");
            for (int index = 0; index < originals.Length; index++)
            {
                Component original = originals[index], copy = copies[index];
                Require(original != null && copy != null && original.GetType() == copy.GetType(), context + ": component identity");
                if (original is Behaviour behaviour) Require(behaviour.enabled == ((Behaviour)copy).enabled, context + ": component enabled state");
                if (original is MeshFilter mesh) Require(mesh.sharedMesh == ((MeshFilter)copy).sharedMesh, context + ": original mesh");
                if (original is MeshRenderer renderer)
                    Require(renderer.sharedMaterials.SequenceEqual(((MeshRenderer)copy).sharedMaterials) && renderer.enabled == ((MeshRenderer)copy).enabled,
                        context + ": original materials (including cutout and water shader settings)");
                if (original is RectTransform rect)
                {
                    var other = (RectTransform)copy;
                    Require(rect.anchorMin == other.anchorMin && rect.anchorMax == other.anchorMax && rect.pivot == other.pivot &&
                        Vector2.Distance(rect.sizeDelta, other.sizeDelta) < .0001f && Vector3.Distance(rect.anchoredPosition3D, other.anchoredPosition3D) < .0001f,
                        context + ": complete caption/decoration rectangle");
                }
                if (original is Graphic graphic)
                {
                    var other = (Graphic)copy;
                    Require(graphic.color == other.color && graphic.material == other.material && graphic.raycastTarget == other.raycastTarget,
                        context + ": graphic style");
                }
                if (original is Text text)
                {
                    var other = (Text)copy;
                    Require(text.text == other.text && text.font == other.font && text.fontSize == other.fontSize && text.fontStyle == other.fontStyle &&
                        text.alignment == other.alignment && text.lineSpacing == other.lineSpacing && text.resizeTextForBestFit == other.resizeTextForBestFit &&
                        text.resizeTextMinSize == other.resizeTextMinSize && text.resizeTextMaxSize == other.resizeTextMaxSize &&
                        text.horizontalOverflow == other.horizontalOverflow && text.verticalOverflow == other.verticalOverflow,
                        context + ": original complete text and font layout");
                }
                if (original is Image image)
                {
                    var other = (Image)copy;
                    Require(image.sprite == other.sprite && image.type == other.type && image.preserveAspect == other.preserveAspect &&
                        image.pixelsPerUnitMultiplier == other.pixelsPerUnitMultiplier && image.fillCenter == other.fillCenter,
                        context + ": original decoration sprite");
                }
                if (original is Shadow shadow)
                {
                    var other = (Shadow)copy;
                    Require(shadow.effectColor == other.effectColor && shadow.effectDistance == other.effectDistance && shadow.useGraphicAlpha == other.useGraphicAlpha,
                        context + ": caption/board outline or shadow");
                }
                if (original is SpiralRotation rotation)
                    Require(rotation.rotationSpeed == ((SpiralRotation)copy).rotationSpeed, context + ": visual water rotation");
            }
            for (int index = 0; index < source.childCount; index++) ValidateExactClone(source.GetChild(index), clone.GetChild(index));
        }

        private static void Require(bool passed, string description)
        {
            if (!passed) throw new InvalidOperationException("Full visual clone differs from its source: " + description);
        }

        private static Transform FindScenePath(Scene scene, string path)
        {
            int slash = path.IndexOf('/');
            string rootName = slash < 0 ? path : path.Substring(0, slash);
            GameObject root = scene.GetRootGameObjects().Single(item => item.name == rootName);
            Transform result = slash < 0 ? root.transform : root.transform.Find(path.Substring(slash + 1));
            return result != null ? result : throw new InvalidOperationException("Missing explicit visual source: " + path);
        }

        private static IEnumerable<Transform> Children(Transform parent)
        {
            for (int index = 0; index < parent.childCount; index++) yield return parent.GetChild(index);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash); EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
#endif
