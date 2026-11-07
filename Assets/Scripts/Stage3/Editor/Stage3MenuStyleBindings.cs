#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using YG;

namespace Vodovorot.Stage3.Editor
{
    /// <summary>Explicit saved-scene migration of missing UI references and eight font references only.</summary>
    public static class Stage3MenuStyleBindings
    {
        public const string SourceScenePath = "Assets/Scenes/MainMenu.unity";
        public const string ReportPath = "Builds/Stage3MenuQA/bindings-report.json";
        private const string ProfilePath = "Assets/PluginYourGames/Editor/SavesEditorYG2.json";

        [Serializable] private sealed class Check { public string name, detail; public bool passed; }
        [Serializable] private sealed class Report
        {
            public string finishedUtc, error, sourceBeforeSha256, sourceAfterSha256;
            public string profileBeforeSha256, profileAfterSha256, otherFieldsBeforeSha256, otherFieldsAfterSha256;
            public bool passed, savedSource, restoredSourceAfterFailure;
            public int allowedFieldCount, changedFieldCount;
            public string scope = "MainMenu only: four existing text references per carousel, PurchaseYG price text references, eight currency-shop font references. No purchases, economy values, placement or geometry changes.";
            public List<string> changes = new List<string>();
            public List<Check> checks = new List<Check>();
        }
        private sealed class LoadedSceneState
        {
            public Scene scene;
            public bool dirty;
        }
        private sealed class ReferenceUpdate
        {
            public string yaml, description;
        }

        [MenuItem("Tools/Vodovorot/Stage 3/Bind existing shop texts and fonts")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                RejectBeforeSourceOpen("Apply menu bindings from Edit Mode. No scene was changed.");
            LoadedSceneState[] loaded = Enumerable.Range(0, SceneManager.sceneCount).Select(index =>
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (scene.isDirty) RejectBeforeSourceOpen("An open scene has unsaved changes: " + scene.path + ". No scene was changed or saved.");
                return new LoadedSceneState { scene = scene, dirty = scene.isDirty };
            }).ToArray();
            Scene active = SceneManager.GetActiveScene();
            byte[] sourceBefore = File.ReadAllBytes(SourceScenePath);
            bool hadProfile = File.Exists(ProfilePath);
            byte[] profileBefore = hadProfile ? File.ReadAllBytes(ProfilePath) : null;
            var report = new Report { sourceBeforeSha256 = Sha256(sourceBefore), profileBeforeSha256 = hadProfile ? Sha256(profileBefore) : "absent" };
            var allowed = new HashSet<string>(StringComparer.Ordinal);
            var updates = new Dictionary<string, ReferenceUpdate>(StringComparer.Ordinal);
            Scene preview = default;
            Exception failure = null;
            bool attemptedSourceWrite = false;
            try
            {
                Font font = AssetDatabase.LoadAssetAtPath<Font>(RankingVisualStyleBuilder.FontPath);
                if (font == null) throw new InvalidOperationException("The canonical Comic Sans font is missing.");
                preview = SkinVisualCatalogBuilder.OpenSourcePreviewScene(SourceScenePath);
                foreach (string canvasName in new[] { "CanvasForDesktop", "CanvasForMobile" })
                {
                    Transform canvas = preview.GetRootGameObjects().Single(item => item.name == canvasName).transform;
                    Transform panel = RequirePath(canvas, "PanelOfSkins");
                    HorizontalLayout3D carousel = RequirePath(panel, "LayoutGroup3D").GetComponent<HorizontalLayout3D>();
                    if (carousel == null || carousel.buttonOfBuying == null || carousel.donateButton == null)
                        throw new InvalidOperationException("Missing canonical shop controls under " + canvasName);
                    Transform buying = RequirePath(panel, "Panel/Buying");
                    if (buying.gameObject != carousel.buttonOfBuying)
                        throw new InvalidOperationException("The authored Buying group differs from the serialized shop reference.");
                    Text feature = RequireText(panel, "Panel/Feature");
                    Text level = RequireText(buying, "Achivements/Text (Legacy)");
                    Text coins = RequireText(buying, "InnerValute/Text (Legacy)");
                    Text donate = RequireText(buying, "Donate/Buy Button/Text (Legacy)");
                    SetReference(carousel, "featureText", feature, allowed, updates, canvasName + " description");
                    SetReference(carousel, "necessaryLevelText", level, allowed, updates, canvasName + " level requirement");
                    SetReference(carousel, "costForCoinsText", coins, allowed, updates, canvasName + " coin price");
                    SetReference(carousel, "costForDonateText", donate, allowed, updates, canvasName + " SDK price");
                    PurchaseYG widget = PersistentPurchase(carousel.donateButton);
                    SetReference(widget, "textLegasy.priceValue", donate, allowed, updates, canvasName + " PurchaseYG priceValue");
                    foreach (string product in new[] { "Bag", "Hand", "Chest", "Box" })
                    {
                        Text price = RequireText(canvas, "PanelOfValute/Layout/" + product + "/Donate/Buy Button/Text");
                        SetReference(price, "m_FontData.m_Font", font, allowed, updates, canvasName + " " + product + " canonical font");
                    }
                    Add(report, canvasName + ".all-existing-bindings", carousel.HasCompleteShopTextBindings && widget.textLegasy.priceValue == donate,
                        "Bound actual authored Text components; prices, names, button listeners and unlock values were not changed.");
                }
                report.allowedFieldCount = allowed.Count;
                if (allowed.Count != 18 || updates.Count != 18) throw new InvalidOperationException("Expected exactly 18 distinct binding/font fields.");
                EditorSceneManager.ClosePreviewScene(preview);
                preview = default;
                byte[] patched = TransformReferenceValues(sourceBefore, updates, report);
                string before = FingerprintProjection(sourceBefore, allowed), planned = FingerprintProjection(patched, allowed);
                report.otherFieldsBeforeSha256 = Sha256(Encoding.UTF8.GetBytes(before));
                if (!string.Equals(before, planned, StringComparison.Ordinal))
                    throw new InvalidOperationException("Planned MainMenu patch changed outside the 18 exact reference values. " + FirstDifference(before, planned));
                if (!sourceBefore.SequenceEqual(File.ReadAllBytes(SourceScenePath)))
                    throw new InvalidOperationException("The saved MainMenu changed during validation; no patch was applied.");
                if (report.changedFieldCount > 0)
                {
                    attemptedSourceWrite = true;
                    File.WriteAllBytes(SourceScenePath, patched);
                    report.savedSource = true;
                    AssetDatabase.ImportAsset(SourceScenePath, ImportAssetOptions.ForceUpdate);
                }
                byte[] sourceAfter = File.ReadAllBytes(SourceScenePath);
                if (!patched.SequenceEqual(sourceAfter))
                    throw new InvalidOperationException("Imported MainMenu bytes differ from the exact validated reference patch.");
                Add(report, "source.exact-reference-values", true, "Imported bytes equal the complete planned patch of the 18 validated scene/font references.");
                string after = FingerprintProjection(sourceAfter, allowed);
                report.otherFieldsAfterSha256 = Sha256(Encoding.UTF8.GetBytes(after));
                if (!string.Equals(before, after, StringComparison.Ordinal))
                    throw new InvalidOperationException("Patched MainMenu changed outside the 18 explicit fields. " + FirstDifference(before, after));
                Add(report, "source.only-approved-fields", true, "All bytes outside the 18 exact reference values are unchanged, including original BOM, mixed line endings, economy values and transforms.");
            }
            catch (Exception error)
            {
                failure = error; report.error = error.ToString();
                if (attemptedSourceWrite && !sourceBefore.SequenceEqual(File.ReadAllBytes(SourceScenePath)))
                {
                    File.WriteAllBytes(SourceScenePath, sourceBefore);
                    AssetDatabase.ImportAsset(SourceScenePath, ImportAssetOptions.ForceUpdate);
                    report.restoredSourceAfterFailure = true;
                }
            }
            finally
            {
                if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
                bool profileUnchanged = File.Exists(ProfilePath) == hadProfile && (!hadProfile || profileBefore.SequenceEqual(File.ReadAllBytes(ProfilePath)));
                report.profileAfterSha256 = File.Exists(ProfilePath) ? Sha256(File.ReadAllBytes(ProfilePath)) : "absent";
                bool scenesUnchanged = SceneManager.GetActiveScene() == active && SceneManager.sceneCount == loaded.Length &&
                    loaded.All(state => state.scene.IsValid() && state.scene.isLoaded && state.scene.isDirty == state.dirty);
                Add(report, "profile.byte-exact", profileUnchanged, "Profile bytes were read only for comparison; no profile write occurred.");
                Add(report, "editor.loaded-scenes-preserved", scenesUnchanged, "Existing loaded scenes were not opened, saved, closed or made dirty. Reload the clean saved MainMenu explicitly before live QA.");
                report.sourceAfterSha256 = Sha256(File.ReadAllBytes(SourceScenePath));
                report.finishedUtc = DateTime.UtcNow.ToString("o");
                report.passed = failure == null && report.checks.All(check => check.passed);
                Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
                File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
            }
            if (!report.passed) throw new InvalidOperationException("Menu binding update failed; see " + ReportPath, failure);
            Debug.Log("[Stage3] Existing shop text/font bindings updated safely. " + report.changedFieldCount + " fields changed; see " + ReportPath);
        }

        private static PurchaseYG PersistentPurchase(Button button)
        {
            PurchaseYG result = null;
            for (int index = 0; index < button.onClick.GetPersistentEventCount(); index++)
            {
                if (button.onClick.GetPersistentMethodName(index) != nameof(PurchaseYG.BuyPurchase) ||
                    !(button.onClick.GetPersistentTarget(index) is PurchaseYG widget)) continue;
                if (result != null) throw new InvalidOperationException("Unexpected multiple persistent purchase handlers. Review before saving any scene changes.");
                result = widget;
            }
            return result != null ? result : throw new InvalidOperationException("Expected the existing persistent PurchaseYG handler.");
        }

        private static void RejectBeforeSourceOpen(string message)
        {
            var report = new Report { finishedUtc = DateTime.UtcNow.ToString("o"), error = message };
            Add(report, "editor.safe-entry-guard", false, message);
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
            throw new InvalidOperationException(message);
        }

        private static void SetReference(UnityEngine.Object target, string propertyPath, UnityEngine.Object value,
            HashSet<string> allowed, Dictionary<string, ReferenceUpdate> updates, string description)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyPath);
            if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
                throw new InvalidOperationException("Missing exact reference property: " + propertyPath);
            string fileId = GlobalObjectId.GetGlobalObjectIdSlow(target).targetObjectId.ToString(CultureInfo.InvariantCulture);
            if (fileId == "0") throw new InvalidOperationException("The source object has no stable serialized identity.");
            string key = fileId + ":" + propertyPath;
            if (!allowed.Add(key)) throw new InvalidOperationException("Duplicate planned reference field: " + key);
            string reference;
            if (value is Font)
            {
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out string guid, out long localId) || string.IsNullOrEmpty(guid))
                    throw new InvalidOperationException("The canonical font has no persistent asset identity.");
                reference = "{fileID: " + localId.ToString(CultureInfo.InvariantCulture) + ", guid: " + guid + ", type: 3}";
            }
            else if (value is Component component && target is Component owner && component.gameObject.scene == owner.gameObject.scene)
            {
                ulong localId = GlobalObjectId.GetGlobalObjectIdSlow(value).targetObjectId;
                if (localId == 0) throw new InvalidOperationException("The intended scene Text has no serialized object identity.");
                reference = "{fileID: " + localId.ToString(CultureInfo.InvariantCulture) + "}";
            }
            else throw new InvalidOperationException("Only same-scene Text and canonical external Font references may be patched.");
            updates.Add(key, new ReferenceUpdate { yaml = reference, description = description });
            if (property.objectReferenceValue == value) return;
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform RequirePath(Transform root, string path) => root.Find(path) ??
            throw new InvalidOperationException("Missing authored UI path: " + root.name + "/" + path);

        private static Text RequireText(Transform root, string path)
        {
            Text text = RequirePath(root, path).GetComponent<Text>();
            return text != null ? text : throw new InvalidOperationException("Expected the original Text at " + path);
        }

        private static string FingerprintProjection(byte[] bytes, HashSet<string> allowed)
        {
            var mask = allowed.ToDictionary(key => key, key => new ReferenceUpdate { yaml = "{explicit-reference-update}" }, StringComparer.Ordinal);
            return new UTF8Encoding(false, true).GetString(TransformReferenceValues(bytes, mask, null));
        }

        /// <summary>Replaces only inline reference tokens; every other UTF-8 byte and original line ending survives.</summary>
        private static byte[] TransformReferenceValues(byte[] bytes, Dictionary<string, ReferenceUpdate> updates, Report report)
        {
            var utf8 = new UTF8Encoding(false, true);
            string source = utf8.GetString(bytes);
            if (!bytes.SequenceEqual(utf8.GetBytes(source)))
                throw new InvalidOperationException("MainMenu does not round-trip as exact UTF-8; no patch is permitted.");
            var output = new StringBuilder(source.Length + 1200);
            var parents = new List<KeyValuePair<int, string>>();
            var found = new HashSet<string>(StringComparer.Ordinal);
            string objectId = "";
            foreach (Match sourceLine in Regex.Matches(source, @"[^\r\n]*(?:\r\n|\n|\r|$)"))
            {
                if (sourceLine.Length == 0) continue;
                string line = sourceLine.Value;
                int endingLength = line.EndsWith("\r\n", StringComparison.Ordinal) ? 2 :
                    line.EndsWith("\n", StringComparison.Ordinal) || line.EndsWith("\r", StringComparison.Ordinal) ? 1 : 0;
                string ending = endingLength == 0 ? "" : line.Substring(line.Length - endingLength);
                line = endingLength == 0 ? line : line.Substring(0, line.Length - endingLength);
                Match header = Regex.Match(line, "^--- !u![0-9]+ &([0-9]+)");
                if (header.Success) { objectId = header.Groups[1].Value; parents.Clear(); }
                Match field = Regex.Match(line, "^( *)([A-Za-z_][A-Za-z0-9_]*):(.*)$");
                if (field.Success && field.Groups[1].Length > 0)
                {
                    int indent = field.Groups[1].Length;
                    while (parents.Count > 0 && parents[parents.Count - 1].Key >= indent) parents.RemoveAt(parents.Count - 1);
                    string key = field.Groups[2].Value;
                    string path = string.Join(".", parents.Select(pair => pair.Value).Concat(new[] { key }));
                    string fieldId = objectId + ":" + path;
                    if (updates.TryGetValue(fieldId, out ReferenceUpdate update))
                    {
                        if (!found.Add(fieldId)) throw new InvalidOperationException("The exact serialized reference occurs twice: " + fieldId);
                        Match token = Regex.Match(line, @"^(?<prefix> *[A-Za-z_][A-Za-z0-9_]*:[ \t]*)(?<reference>\{[^{}\r\n]*\})(?<suffix>[ \t]*)$");
                        if (!token.Success) throw new InvalidOperationException("The intended field is not an inline Unity reference: " + fieldId);
                        string previous = token.Groups["reference"].Value;
                        line = token.Groups["prefix"].Value + update.yaml + token.Groups["suffix"].Value;
                        if (report != null && previous != update.yaml)
                        {
                            report.changedFieldCount++;
                            report.changes.Add(update.description + " (" + fieldId + "): " + previous + " -> " + update.yaml);
                        }
                    }
                    if (string.IsNullOrWhiteSpace(field.Groups[3].Value)) parents.Add(new KeyValuePair<int, string>(indent, key));
                }
                output.Append(line).Append(ending);
            }
            if (found.Count != updates.Count)
                throw new InvalidOperationException("Some intended serialized reference fields were not found: " + string.Join(", ", updates.Keys.Where(key => !found.Contains(key))));
            return utf8.GetBytes(output.ToString());
        }

        private static string FirstDifference(string before, string after)
        {
            string[] original = before.Split('\n'), saved = after.Split('\n');
            for (int index = 0; index < Mathf.Min(original.Length, saved.Length); index++)
                if (original[index] != saved[index]) return "First unexpected line " + (index + 1) + ": " + original[index].Trim() + " -> " + saved[index].Trim();
            return "Unexpected serialized line-count change.";
        }

        private static string Sha256(byte[] bytes)
        {
            using (SHA256 hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        private static void Add(Report report, string name, bool passed, string detail) =>
            report.checks.Add(new Check { name = name, passed = passed, detail = detail });
    }
}
#endif
