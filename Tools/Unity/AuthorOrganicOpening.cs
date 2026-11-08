using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using Newtonsoft.Json.Linq;
using Vodovorot.RoundExperience;

public static class AuthorOrganicOpening
{
    public static string Run() => RunMap(0);
    public static string Garden() => RunMap(1);
    public static string Castle() => RunMap(2);
    public static string Lab() => RunMap(3);

    private static string RunMap(int targetMap)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Use clean Edit Mode.");
        string original = SceneManager.GetActiveScene().path;
        JArray source = JArray.Parse(File.ReadAllText("Docs/MapPolish-20261108/Checks/opening-authoring.json"));
        var report = new JArray();
        try
        {
            for (int map = targetMap; map <= targetMap; map++)
            {
                JObject baseline = (JObject)source[map == 3 ? 0 : map];
                Scene scene = EditorSceneManager.OpenScene(map == 3 ? RoundExperienceLab.ScenePath : (string)baseline["scene"]);
                if (scene.GetRootGameObjects().Any(root => root.name == "Organic opening authored"))
                    throw new InvalidOperationException("Opening already authored; inspect before reapplying.");
                var all = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<FallingObject>(true)).ToArray();
                var byPath = all.GroupBy(item => Path(item.transform)).ToDictionary(group => group.Key, group => group.ToArray());
                var chosen = new List<FallingObject>();
                var waves = new List<int>();
                foreach (JObject edit in baseline["edits"])
                {
                    Vector3 previous = Vector((string)edit["after"]);
                    FallingObject match = byPath[(string)edit["name"]].Where(item => !chosen.Contains(item))
                        .OrderBy(item => (item.transform.position - previous).sqrMagnitude).FirstOrDefault();
                    if (match != null && chosen.Any(item => item.transform == match.transform))
                        continue;
                    if (match != null && Vector3.Distance(match.transform.position, previous) > .025f && match.GetComponents<FallingObject>().Length > 1 &&
                        baseline["edits"].Any(other => (string)other["name"] == (string)edit["name"] &&
                            Vector3.Distance(match.transform.position, Vector((string)other["after"])) < .025f))
                        continue; // The old manifest moved one transform twice via two existing components.
                    if (match == null || Vector3.Distance(match.transform.position, previous) > .025f)
                        throw new InvalidOperationException("Opening baseline differs: " + edit["name"]);
                    chosen.Add(match);
                    waves.Add((int)edit["wave"]);
                }
                var bounds = chosen.ToDictionary(item => item, item => item.GetComponent<Collider>().bounds);
                var selected = new HashSet<FallingObject>(chosen);
                var edits = new JArray();
                Physics.SyncTransforms();
                for (int index = 0; index < chosen.Count; index++)
                {
                    FallingObject item = chosen[index];
                    int wave = waves[index];
                    int slot = wave == 1 ? index : index - (int)baseline["initialCandidates"];
                    int columns = wave == 1 ? 2 : 4;
                    int row = slot / columns;
                    Vector3 forward = Vector3.forward;
                    if (map == 1)
                    {
                        // Use the saved route's local direction, including garden corners.
                        int nextIndex = Mathf.Min(chosen.Count - 1, index + columns);
                        int previousIndex = Mathf.Max(wave == 1 ? 0 : (int)baseline["initialCandidates"], index - columns);
                        Vector3 next = bounds[chosen[nextIndex]].center - bounds[chosen[previousIndex]].center;
                        next.y = 0;
                        forward = Mathf.Abs(next.x) > Mathf.Abs(next.z) ? Vector3.right * Mathf.Sign(next.x) : Vector3.forward * Mathf.Sign(next.z);
                    }
                    Vector3 right = Vector3.Cross(Vector3.up, forward);
                    float sway = Mathf.Sin(row * .69f) * (wave == 1 ? .11f : .24f);
                    float cluster = Mathf.Sin(row * 1.05f) * (wave == 1 ? .055f : .10f);
                    float stagger = ((slot % columns) % 2 == 0 ? -.02f : .02f);
                    Vector3 delta = right * sway + forward * (cluster + stagger);
                    if (wave == 1 && row < 2)
                        delta *= .25f; // Keep the first visible pair within the same easy opening reach.
                    Bounds proposed = bounds[item];
                    proposed.center += delta;
                    Bounds check = proposed;
                    check.Expand(-.012f);
                    if (chosen.Any(other => other != item && bounds[other].Intersects(check)))
                        continue;
                    Collider[] touching = Physics.OverlapBox(proposed.center, Vector3.Max(proposed.extents - Vector3.one * .012f, Vector3.one * .01f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                    bool blocked = touching.Any(c => !selected.Contains(c.GetComponentInParent<FallingObject>()) &&
                        !(c.bounds.size.y < .25f && c.bounds.size.x * c.bounds.size.z > 5));
                    if (blocked)
                        continue;
                    Vector3 before = item.transform.position;
                    item.transform.position += delta;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(item.transform);
                    bounds[item] = proposed;
                    edits.Add(JObject.FromObject(new { name = Path(item.transform),
                        globalId = GlobalObjectId.GetGlobalObjectIdSlow(item).ToString(), wave,
                        before = before.ToString("F5"), after = item.transform.position.ToString("F5"),
                        deltaMetres = delta.magnitude, localScale = item.transform.localScale.ToString("F5"),
                        localRotation = item.transform.localRotation.eulerAngles.ToString("F5") }));
                }
                new GameObject("Organic opening authored");
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                report.Add(JObject.FromObject(new { scene = scene.path, originalCount = chosen.Count, moved = edits.Count, edits }));
            }
            File.WriteAllText("Docs/QualityPass-20261108/Checks/opening-authoring-map" + targetMap + ".json", report.ToString());
        }
        finally { EditorSceneManager.OpenScene(original); }
        return "Saved staggered, gently curved starter clusters; original props, rotations, scales and values retained.";
    }
    public static string CaptureExistingCity()
    {
        string original = SceneManager.GetActiveScene().path;
        try
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/Low Poly City.unity");
            if (!scene.GetRootGameObjects().Any(root => root.name == "Organic opening authored"))
                throw new InvalidOperationException("City authoring stamp missing");
            JObject source = (JObject)JArray.Parse(File.ReadAllText("Docs/MapPolish-20261108/Checks/opening-authoring.json"))[0];
            var byPath = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<FallingObject>(true))
                .GroupBy(item => Path(item.transform)).ToDictionary(group => group.Key, group => group.First());
            var edits = new JArray();
            foreach (var entry in source["edits"])
            {
                FallingObject item = byPath[(string)entry["name"]];
                Vector3 before = Vector((string)entry["after"]);
                if (Vector3.Distance(before, item.transform.position) > .025f)
                    edits.Add(JObject.FromObject(new { name = Path(item.transform),
                        globalId = GlobalObjectId.GetGlobalObjectIdSlow(item).ToString(),
                        beforeRoundedTo2Decimals = before.ToString("F2"), after = item.transform.position.ToString("F5") }));
            }
            File.WriteAllText("Docs/QualityPass-20261108/Checks/opening-authoring-map0.json", new JArray(JObject.FromObject(new
            { scene = scene.path, originalCount = source["edits"].Count(), movedBeyondRoundingTolerance = edits.Count,
                recoveredAfterObserverTimeout = true, exactBeforeState = "git checkpoint78bd67e", edits })).ToString());
            return "Captured already-saved City without reapplying the layout.";
        }
        finally { EditorSceneManager.OpenScene(original); }
    }

    private static Vector3 Vector(string text)
    {
        float[] values = text.Trim('(', ')').Split(',').Select(value => float.Parse(value, CultureInfo.InvariantCulture)).ToArray();
        return new Vector3(values[0], values[1], values[2]);
    }
    private static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
}
