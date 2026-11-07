#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Vodovorot.Stage2;
using Vodovorot.Stage3;

public static class AchievementVisualReview
{
    [Serializable] private sealed class Check { public string name, detail; public bool passed; }
    [Serializable] private sealed class Report { public bool passed; public Check[] checks; public string scope = "Actual Unity card renderer and full shop catalog; browser/runtime integration checked separately."; }
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Check artwork in Edit Mode.");
        string output = Path.GetFullPath("Builds/Stage3CardQA"); Directory.CreateDirectory(output);
        var checks = new System.Collections.Generic.List<Check>();
        foreach (bool ru in new[] { true, false })
        foreach (SkinVisualCatalog.Entry entry in SkinVisualCatalog.Load().Entries)
        {
            var data = new AchievementCard.CardData { ru = ru, skin = ru ? entry.nameRu : entry.nameEn, skinId = entry.id,
                percent = "87.6%", map = ru ? "Средневековье" : "Medieval Castle", mode = ru ? "Тотальная зачистка" : "Total Cleaning",
                rank = 321, xp = 24800, url = "https://yandex.ru/games/app/473763" };
            Texture2D portrait = SkinPortraitRenderer.Render(entry, ru, 1024);
            Texture2D image = null;
            try
            {
                image = AchievementCardVisual.RenderCard(data, portrait);
                Color32[] pixels = image.GetPixels32();
                string name = entry.id + (ru ? "-ru" : "-en");
                File.WriteAllBytes(Path.Combine(output, name + ".png"), image.EncodeToPNG());
                Texture2D translated = SkinPortraitRenderer.RenderAtRuntimeOriginForReview(entry, ru, 1024);
                try
                {
                    Color32[] a = portrait.GetPixels32(), b = translated.GetPixels32();
                    double error = 0;
                    for (int i = 0; i < a.Length; i++)
                        error += Math.Abs(a[i].r - b[i].r) + Math.Abs(a[i].g - b[i].g) + Math.Abs(a[i].b - b[i].b) + Math.Abs(a[i].a - b[i].a);
                    double mean = error / (a.Length * 4.0 * 255);
                    checks.Add(new Check { name = name + ".runtime-origin-camera", passed = mean < .005,
                        detail = "Same renderer translated to runtime origin y=-1000; normalized mean pixel error=" + mean });
                }
                finally { UnityEngine.Object.DestroyImmediate(translated); }
                checks.Add(new Check { name = name + ".full-card", passed = image.width == 1080 && image.height == 1350 &&
                    pixels.Count(p => p.a > 250) > pixels.Length * .9f && pixels.Count(p => p.r < 60 && p.g < 80 && p.b < 100) > 5000,
                    detail = "Full 1080×1350 game-font card with opaque paper and actual lettering." });
                AchievementCardVisual.ApplyWebArtwork(data);
                string[] buttons = { data.webShareButton, data.webSaveButton, data.webCopyButton, data.webCloseButton };
                checks.Add(new Check { name = name + ".shared-browser-buttons", passed = buttons.All(value => !string.IsNullOrEmpty(value) && Convert.FromBase64String(value).Length > 1000),
                    detail = "Four authored sprite/font button images, not a browser font substitution." });
                if (entry.legacyIndex == 0) File.WriteAllText(Path.Combine(output, "browser-data" + (ru ? "-ru" : "-en") + ".json"), JsonUtility.ToJson(data));
            }
            finally { if (image != null) UnityEngine.Object.DestroyImmediate(image); if (portrait != null) UnityEngine.Object.DestroyImmediate(portrait); }
        }
        File.WriteAllText(Path.Combine(output, "card-report.json"), JsonUtility.ToJson(new Report { passed = checks.All(c => c.passed), checks = checks.ToArray() }, true));
        if (checks.Any(check => !check.passed)) throw new InvalidOperationException("Card artwork checks failed; inspect exported PNGs/report.");
    }
}
#endif
