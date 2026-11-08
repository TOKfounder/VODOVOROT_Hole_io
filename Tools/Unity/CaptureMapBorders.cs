using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vodovorot.RoundExperience;
using Vodovorot.Stage3;

/// <summary>Controlled Editor world renders; these deliberately do not claim browser/UI input.</summary>
public static class CaptureMapBorders
{
    public static async Task<string> Run()
    {
        if (!Application.isPlaying || !RankSimulationContext.Active || YG.YG2.saves.musicValue != 0 || YG.YG2.saves.soundValue != 0)
            throw new InvalidOperationException("Use isolated silent Play.");
        byte[] save = File.ReadAllBytes("Assets/PluginYourGames/Editor/SavesEditorYG2.json");
        var records = new List<object>();
        string error = null;
        try
        {
            for (int map = 1; map <= 3; map++)
            {
                SceneManager.LoadScene(map);
                await Task.Delay(600);
                GamingManager game = UnityEngine.Object.FindAnyObjectByType<GamingManager>();
                BlackHoleController player = UnityEngine.Object.FindAnyObjectByType<BlackHoleController>();
                CameraCutawayView cutaway = UnityEngine.Object.FindAnyObjectByType<CameraCutawayView>();
                var data = new UnityEditor.SerializedObject(cutaway);
                Camera camera = (Camera)data.FindProperty("viewCamera").objectReferenceValue;
                float height = player.transform.position.y;
                foreach (int score in new[] { 0, 163 })
                {
                    player.InitializeScore(score, true);
                    float inset = Mathf.Max(player.size.x, player.size.z) * .5f + .55f;
                    Vector3[] corners = {
                        new Vector3(game.minX+inset,height,game.minZ+inset),new Vector3(game.minX+inset,height,game.maxZ-inset),
                        new Vector3(game.maxX-inset,height,game.minZ+inset),new Vector3(game.maxX-inset,height,game.maxZ-inset)
                    };
                    for (int corner = 0; corner < corners.Length; corner++)
                    {
                        RenderTexture target = RenderTexture.GetTemporary(1280, 720, 24);
                        RenderTexture previous = RenderTexture.active;
                        Texture2D image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                        try
                        {
                            camera.targetTexture = target;
                            player.transform.position = corners[corner];
                            Physics.SyncTransforms();
                            await Task.Delay(350);
                            camera.Render();
                            RenderTexture.active = target;
                            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                            image.Apply();
                            string file = "editor-border-map" + map + "-size" + (score == 0 ? 1 : 3) + "-corner" + corner + ".png";
                            File.WriteAllBytes("Docs/MapPolish-20261108/Evidence/" + file, image.EncodeToPNG());
                            Color32[] pixels = image.GetPixels32();
                            int pureBlueEdge = 0;
                            for (int y = 0; y < 720; y++)
                                for (int x = 0; x < 1280; x++)
                                {
                                    if (x > 64 && x < 1216 && y > 36 && y < 684)
                                        continue;
                                    Color32 color = pixels[y * 1280 + x];
                                    if (color.r <= 5 && color.g <= 5 && color.b >= 250)
                                        pureBlueEdge++;
                                }
                            Vector3 point = camera.WorldToViewportPoint(player.transform.position);
                            bool framed = point.z > 0 && point.x >= .29f && point.x <= .71f && point.y >= .29f && point.y <= .71f;
                            records.Add(new
                            {
                                map,
                                score,
                                corner,
                                player = player.transform.position.ToString(),
                                viewport = point.ToString(),
                                framed,
                                pureBlueEdge,
                                evidence = "../Evidence/" + file
                            });
                            if (!framed || pureBlueEdge > 0)
                                throw new Exception("Border framing/clear-color failure: " + file);
                        }
                        finally
                        {
                            camera.targetTexture = null;
                            RenderTexture.active = previous;
                            RenderTexture.ReleaseTemporary(target);
                            UnityEngine.Object.Destroy(image);
                        }
                    }
                }
            }
            if (!save.SequenceEqual(File.ReadAllBytes("Assets/PluginYourGames/Editor/SavesEditorYG2.json")))
                throw new Exception("Personal save changed.");
        }
        catch (Exception failure)
        {
            error = failure.ToString();
            throw;
        }
        finally
        {
            File.WriteAllText("Docs/MapPolish-20261108/Checks/editor-border-views.json", Newtonsoft.Json.JsonConvert.SerializeObject(
                new
                {
                    environment = "Controlled Editor world renders,1280x720; no HUD/native input",
                    records,
                    error
                }, Newtonsoft.Json.Formatting.Indented));
            RankSimulationContext.Stop();
        }
        return "PASS:24 border/corner world views at sizes1/3; player framed, no pure-blue clear strip, personal save unchanged.";
    }
}
