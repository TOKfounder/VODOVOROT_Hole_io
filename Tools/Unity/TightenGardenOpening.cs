using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TightenGardenOpening
{
    public static string Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Use clean Edit Mode.");
        string original = SceneManager.GetActiveScene().path;
        var report = new JArray();
        try
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/Garden.unity");
            var player = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<BlackHoleController>(true)).Single();
            Vector3 start = player.transform.position;
            var edits = (JArray)JArray.Parse(File.ReadAllText("Docs/QualityPass-20261108/Checks/opening-authoring-map1.json"))[0]["edits"];
            var chosen = edits.Where(row => (int)row["wave"] == 1 && ((string)row["name"]).Contains("Natures_Bush_02")).TakeLast(3).ToArray();
            for (int index = 0; index < chosen.Length; index++)
            {
                GlobalObjectId id;
                if (!GlobalObjectId.TryParse((string)chosen[index]["globalId"], out id)) throw new InvalidOperationException("Invalid authored ID");
                var item = (FallingObject)GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id);
                Vector3 before = item.transform.position;
                item.transform.position = new Vector3(start.x + (index == 1 ? .03f : -.025f), before.y, start.z + .38f + index * .35f);
                PrefabUtility.RecordPrefabInstancePropertyModifications(item.transform);
                report.Add(JObject.FromObject(new { globalId = id.ToString(), name = item.name, before = before.ToString("F5"), after = item.transform.position.ToString("F5"), reason = "Three existing starter shrubs ease the garden turn; no added props, value, scale or collider changes" }));
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally { EditorSceneManager.OpenScene(original); }
        File.WriteAllText("Docs/QualityPass-20261108/Checks/garden-start-adjustment.json", report.ToString());
        return report.ToString();
    }
}
