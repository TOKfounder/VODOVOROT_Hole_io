#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Vodovorot.Stage3.Editor
{
    /// <summary>Checks the unconnected catalogue foundation without changing a live shop or profile.</summary>
    public static class SkinVisualCatalogReview
    {
        [Serializable] private sealed class Check { public string name, detail; public bool passed; }
        [Serializable] private sealed class Report
        {
            public string finishedUtc;
            public bool passed;
            public string scope = "Complete shop catalogue and isolated portraits. Not yet connected to shop/card UI; not a WebGL/device claim.";
            public List<Check> checks = new List<Check>();
        }
        private static Report report;
        private static string Output => Path.GetFullPath("Builds/Stage3SkinQA");

        [MenuItem("Tools/Vodovorot/Stage 3/Check complete skin catalogue")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Check catalogue from Edit Mode.");
            report = new Report(); Directory.CreateDirectory(Output);
            byte[] sourceScene = File.ReadAllBytes(SkinVisualCatalogBuilder.SourceScenePath);
            byte[] profile = File.ReadAllBytes("Assets/PluginYourGames/Editor/SavesEditorYG2.json");
            Scene active = SceneManager.GetActiveScene(); bool wasDirty = active.isDirty;
            Scene source = SkinVisualCatalogBuilder.OpenSourcePreviewScene(SkinVisualCatalogBuilder.SourceScenePath);
            Scene temporary = EditorSceneManager.NewPreviewScene();
            try
            {
                SkinVisualCatalog catalog = SkinVisualCatalog.Load(); catalog.ValidateIdentity();
                Transform carousel = Find(source, SkinVisualCatalogBuilder.SourceCarouselPath);
                carousel.GetComponent<HorizontalLayout3D>().ArrangeChildren();
                Add("identity.five-stable-ids", catalog.Entries.Count == 5 && Enumerable.Range(0, 5).All(index =>
                    catalog.GetByLegacyIndex(index).id == SkinVisualCatalog.IdForLegacyIndex(index)), "Fixed historical mapping; sibling order is not an identity.");
                foreach (SkinVisualCatalog.Entry entry in catalog.Entries)
                {
                    Transform original = Find(source, entry.sourceRootPath);
                    Transform savedModel = entry.visualPrefab.transform.Find(entry.modelRootPath);
                    Add(entry.id + ".complete-source-counts", savedModel != null &&
                        original.GetComponentsInChildren<Transform>(true).Length == savedModel.GetComponentsInChildren<Transform>(true).Length &&
                        original.GetComponentsInChildren<MeshRenderer>(true).Length == savedModel.GetComponentsInChildren<MeshRenderer>(true).Length &&
                        original.GetComponentsInChildren<Graphic>(true).Length == savedModel.GetComponentsInChildren<Graphic>(true).Length,
                        "All descendants, renderers and WorldSpace UI retained, including inactive content.");
                    Add(entry.id + ".visual-only", entry.visualPrefab.GetComponentsInChildren<Collider>(true).Length == 0 &&
                        entry.visualPrefab.GetComponentsInChildren<Rigidbody>(true).Length == 0 &&
                        entry.visualPrefab.GetComponentsInChildren<Camera>(true).Length == 0 &&
                        entry.visualPrefab.GetComponentsInChildren<MonoBehaviour>(true).All(component => component is Graphic ||
                            component is Shadow || component is CanvasScaler || component is SpiralRotation),
                        "No physics, camera, gameplay or shop controller in the visual prefab.");
                    var copy = UnityEngine.Object.Instantiate(entry.visualPrefab);
                    copy.hideFlags = HideFlags.HideAndDontSave;
                    SceneManager.MoveGameObjectToScene(copy, temporary);
                    try
                    {
                        Texture2D full = SkinPortraitRenderer.RenderVisual(copy, entry, true);
                        try
                        {
                            Color32[] pixels = full.GetPixels32();
                            int opaque = pixels.Count(pixel => pixel.a > 16);
                            Add(entry.id + ".portrait-coverage", opaque > 1500 && opaque < 512 * 512 * .9f && pixels[0].a == 0,
                                "Nonempty transparent PNG; covered pixels=" + opaque);
                            File.WriteAllBytes(Path.Combine(Output, entry.id + "-ru.png"), full.EncodeToPNG());
                            Texture2D withoutWater = SkinPortraitRenderer.RenderVisual(copy, entry, true, 512,
                                visual => visual.transform.Find(entry.waterPath).gameObject.SetActive(false));
                            try { Add(entry.id + ".visible-water", Different(pixels, withoutWater.GetPixels32()) > 100,
                                "Removing the actual water changes the rendered portrait."); }
                            finally { UnityEngine.Object.DestroyImmediate(withoutWater); }
                            Texture2D withoutCaption = SkinPortraitRenderer.RenderVisual(copy, entry, true, 512,
                                visual => visual.transform.Find(entry.captionPath).gameObject.SetActive(false));
                            try { Add(entry.id + ".visible-caption", Different(pixels, withoutCaption.GetPixels32()) > 30,
                                "Removing the real WorldSpace caption changes the rendered portrait."); }
                            finally { UnityEngine.Object.DestroyImmediate(withoutCaption); }
                        }
                        finally { UnityEngine.Object.DestroyImmediate(full); }
                        Texture2D english = SkinPortraitRenderer.RenderVisual(copy, entry, false);
                        try { File.WriteAllBytes(Path.Combine(Output, entry.id + "-en.png"), english.EncodeToPNG()); }
                        finally { UnityEngine.Object.DestroyImmediate(english); }
                    }
                    finally { UnityEngine.Object.DestroyImmediate(copy); }
                }
            }
            catch (Exception error) { Add("runner.exception", false, error.ToString()); Debug.LogException(error); }
            finally
            {
                EditorSceneManager.ClosePreviewScene(temporary); EditorSceneManager.ClosePreviewScene(source);
                Add("source.scene-byte-exact", sourceScene.SequenceEqual(File.ReadAllBytes(SkinVisualCatalogBuilder.SourceScenePath)), "Saved MainMenu unchanged.");
                Add("profile.byte-exact", profile.SequenceEqual(File.ReadAllBytes("Assets/PluginYourGames/Editor/SavesEditorYG2.json")), "Profile unchanged.");
                Add("editor.active-scene-preserved", SceneManager.GetActiveScene() == active && active.isDirty == wasDirty, "Active scene and its dirty state preserved.");
                report.finishedUtc = DateTime.UtcNow.ToString("o"); report.passed = report.checks.All(check => check.passed);
                File.WriteAllText(Path.Combine(Output, "catalog-report.json"), JsonUtility.ToJson(report, true));
            }
        }

        private static int Different(Color32[] a, Color32[] b)
        {
            int count = 0;
            for (int i = 0; i < a.Length; i++)
                if (Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].g - b[i].g) + Mathf.Abs(a[i].b - b[i].b) + Mathf.Abs(a[i].a - b[i].a) > 24) count++;
            return count;
        }

        private static Transform Find(Scene scene, string path)
        {
            string[] parts = path.Split('/');
            Transform root = scene.GetRootGameObjects().Single(item => item.name == parts[0]).transform;
            return parts.Length == 1 ? root : root.Find(string.Join("/", parts.Skip(1)));
        }
        private static void Add(string name, bool passed, string detail)
        {
            report.checks.Add(new Check { name = name, passed = passed, detail = detail });
            Debug.Log("[Skin catalog QA] " + (passed ? "PASS " : "FAIL ") + name + ": " + detail);
        }
    }
}
#endif
