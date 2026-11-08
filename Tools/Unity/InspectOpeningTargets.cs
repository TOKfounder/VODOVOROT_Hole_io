using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vodovorot.Stage3;

public static class InspectOpeningTargets
{
    public static async Task<string> Run()
    {
        if (!Application.isPlaying || !RankSimulationContext.Active || YG.YG2.saves.soundValue != 0 || YG.YG2.saves.musicValue != 0)
            throw new InvalidOperationException("Use isolated silent Play.");
        var records = new List<object>();
        foreach (int index in new[] { 1, 2, 3 })
        {
            SceneManager.LoadScene(index);
            await Task.Delay(700);
            BlackHoleController player = UnityEngine.Object.FindAnyObjectByType<BlackHoleController>();
            FallingObject[] items = UnityEngine.Object.FindObjectsByType<FallingObject>(FindObjectsInactive.Exclude);
            Vector3 fit = player.GetFitSize();
            var nearest = items.Where(x => !x.isColon && x.value > 0 && Tool.CanFit2D(x.size, fit) && x.rend != null && x.rend.bounds.min.y >= -.1f)
                .OrderBy(x => (x.transform.position - player.transform.position).sqrMagnitude).Take(90).ToArray();
            records.Add(new
            {
                scene = SceneManager.GetActiveScene().path,
                count = items.Length,
                player = player.transform.position.ToString(),
                fit = fit.ToString(),
                within3 = nearest.Count(x => Vector3.Distance(x.transform.position, player.transform.position) < 3),
                within8 = nearest.Count(x => Vector3.Distance(x.transform.position, player.transform.position) < 8),
                items = nearest.Select(x => new
                {
                    path = PathOf(x.transform),
                    position = x.transform.position.ToString(),
                    size = x.size.ToString(),
                    value = x.value,
                    source = UnityEditor.PrefabUtility.GetCorrespondingObjectFromOriginalSource(x.gameObject) == null ? "scene" : UnityEditor.AssetDatabase.GetAssetPath(UnityEditor.PrefabUtility.GetCorrespondingObjectFromOriginalSource(x.gameObject)),
                    distance = Vector3.Distance(x.transform.position, player.transform.position)
                }).ToArray()
            });
        }
        File.WriteAllText("Docs/MapPolish-20261108/Checks/opening-targets-before.json", Newtonsoft.Json.JsonConvert.SerializeObject(records, Newtonsoft.Json.Formatting.Indented));
        RankSimulationContext.Stop();
        return "Recorded real runtime fit/value/density for three maps; original profile restored.";
    }
    private static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
}
