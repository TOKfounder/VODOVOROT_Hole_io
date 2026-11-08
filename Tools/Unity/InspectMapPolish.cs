using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class InspectMapPolish
{
    public static string Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Use clean Edit Mode.");
        string original = SceneManager.GetActiveScene().path;
        var records = new List<object>();
        foreach (string path in new[] { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/Low Poly City.unity", "Assets/Scenes/Garden.unity", "Assets/Scenes/MedievalCastle.unity" })
        {
            Scene scene = EditorSceneManager.OpenScene(path);
            var roots = scene.GetRootGameObjects();
            var renderers = roots.SelectMany(x => x.GetComponentsInChildren<Renderer>(true)).ToArray();
            var game = roots.SelectMany(x => x.GetComponentsInChildren<GamingManager>(true)).FirstOrDefault();
            var player = roots.SelectMany(x => x.GetComponentsInChildren<BlackHoleController>(true)).FirstOrDefault();
            records.Add(new
            {
                path,
                roots = roots.Select(x => x.name).ToArray(),
                player = player == null ? null : new
                {
                    position = player.transform.position.ToString(),
                    scale = player.transform.localScale.ToString(),
                    fit = player.GetFitSize().ToString()
                },
                walls = game == null ? null : game.walls.Select(x => new { name = PathOf(x.transform), position = x.transform.position.ToString(), size = x.GetComponent<Collider>().bounds.size.ToString(), min = x.GetComponent<Collider>().bounds.min.ToString(), max = x.GetComponent<Collider>().bounds.max.ToString(), renderer = x.GetComponent<Renderer>()?.enabled }).ToArray(),
                cameras = roots.SelectMany(x => x.GetComponentsInChildren<Camera>(true)).Select(x => new { name = PathOf(x.transform), position = x.transform.position.ToString(), rotation = x.transform.eulerAngles.ToString(), fov = x.fieldOfView, far = x.farClipPlane, clear = x.clearFlags.ToString(), background = x.backgroundColor.ToString() }).ToArray(),
                broadSurfaces = renderers.OrderByDescending(x => x.bounds.size.x * x.bounds.size.z).Take(18).Select(x => new { name = PathOf(x.transform), center = x.bounds.center.ToString(), size = x.bounds.size.ToString(), materials = x.sharedMaterials.Select(m => m == null ? "null" : AssetDatabase.GetAssetPath(m) + " | " + m.shader.name).ToArray() }).ToArray(),
                settings = roots.SelectMany(x => x.GetComponentsInChildren<RectTransform>(true)).Where(x => x.name.Contains("PanelOfSettings")).Select(x => new { name = PathOf(x), rect = x.rect.ToString(), scale = x.localScale.ToString(), anchors = x.anchorMin + ".." + x.anchorMax, elements = x.GetComponentsInChildren<RectTransform>(true).Select(t => new { name = PathOf(t), rect = t.rect.ToString(), position = t.anchoredPosition.ToString(), anchors = t.anchorMin + ".." + t.anchorMax, text = t.GetComponent<Text>()?.text }).ToArray() }).ToArray()
            });
        }
        EditorSceneManager.OpenScene(original);
        File.WriteAllText("Docs/MapPolish-20261108/Checks/authored-baseline.json", Newtonsoft.Json.JsonConvert.SerializeObject(records, Newtonsoft.Json.Formatting.Indented));
        return "Saved authored baseline for four scenes; returned to original clean scene.";
    }

    private static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
}
