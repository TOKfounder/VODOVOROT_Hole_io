using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vodovorot.RoundExperience;
using Vodovorot.Stage3;
using YG;

public static class CheckNarrowCamera
{
    public static async Task<string> Run()
    {
        string error = null;
        string rendererName = null;
        string dimensions = null;
        bool included = false, hidden = false, restored = false, silhouetteHidden = false, originalPointRayMissed = false;
        byte[] originalSave = File.ReadAllBytes("Assets/PluginYourGames/Editor/SavesEditorYG2.json");
        try
        {
            if (!Application.isPlaying || !RankSimulationContext.Active || YG2.saves.musicValue != 0 || YG2.saves.soundValue != 0)
                throw new InvalidOperationException("Use the isolated silent tuning lab.");
            SceneManager.LoadScene(3);
            await Task.Delay(800);
            CameraCutawayView cutaway = UnityEngine.Object.FindAnyObjectByType<CameraCutawayView>();
            var data = new SerializedObject(cutaway);
            Camera camera = (Camera)data.FindProperty("viewCamera").objectReferenceValue;
            Vector3 position = camera.transform.position;
            Renderer tower = SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Renderer>(true))
                .First(renderer => renderer.name == "Tower_visual" && renderer.enabled && renderer.gameObject.activeInHierarchy &&
                    !renderer.forceRenderingOff && Vector3.Distance(renderer.bounds.center, position) > 30);
            rendererName = tower.name;
            dimensions = tower.bounds.size.ToString();
            SerializedProperty occluders = data.FindProperty("occluders");
            included = Enumerable.Range(0, occluders.arraySize)
                .Any(index => occluders.GetArrayElementAtIndex(index).objectReferenceValue == tower);
            if (!included) throw new Exception("The previously omitted narrow tower is missing from the saved scene bindings.");
            data.FindProperty("keepViewInsideMap").boolValue = false;
            data.ApplyModifiedPropertiesWithoutUndo();
            camera.transform.position = tower.bounds.center;
            await Task.Delay(180);
            hidden = tower.forceRenderingOff;
            if (!hidden) throw new Exception("A camera inside the narrow tower is still occluded.");
            camera.transform.position = position;
            await Task.Delay(400);
            restored = !tower.forceRenderingOff;
            if (!restored) throw new Exception("Narrow tower visibility did not recover.");
            BlackHoleController player = UnityEngine.Object.FindAnyObjectByType<BlackHoleController>();
            Vector3 playerPosition = player.transform.position;
            Quaternion cameraRotation = camera.transform.rotation;
            float radius = player.GetVisualSizeOfHole().x * .5f;
            player.transform.position = new Vector3(tower.bounds.min.x - radius * .5f, playerPosition.y, tower.bounds.center.z);
            camera.transform.position = player.transform.position + new Vector3(-4, 4, 0);
            camera.transform.LookAt(player.transform.position);
            Vector3 sight = player.transform.position - camera.transform.position;
            originalPointRayMissed = !tower.bounds.IntersectRay(new Ray(camera.transform.position, sight.normalized), out float hit) || hit > sight.magnitude;
            await Task.Delay(180);
            silhouetteHidden = tower.forceRenderingOff;
            if (!originalPointRayMissed || !silhouetteHidden)
                throw new Exception("The visible rim is still covered when a feet-only sight ray misses the wall.");
            player.transform.position = playerPosition;
            camera.transform.position = position;
            camera.transform.rotation = cameraRotation;
            if (!originalSave.SequenceEqual(File.ReadAllBytes("Assets/PluginYourGames/Editor/SavesEditorYG2.json")))
                throw new Exception("Personal save changed.");
        }
        catch (Exception failure) { error = failure.ToString(); throw; }
        finally
        {
            File.WriteAllText("Docs/ImprovementPass-20261107/Checks/narrow-camera.json",
                Newtonsoft.Json.JsonConvert.SerializeObject(new { environment = "Editor controlled fixture, not gameplay",
                    rendererName, dimensions, included, hidden, restored, originalPointRayMissed, silhouetteHidden, error }, Newtonsoft.Json.Formatting.Indented));
            RankSimulationContext.Stop();
        }
        return "PASS: previously omitted narrow Medieval tower is bound, hidden and restored; personal save unchanged.";
    }
}
