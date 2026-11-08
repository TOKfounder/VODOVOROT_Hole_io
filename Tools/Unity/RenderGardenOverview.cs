using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class RenderGardenOverview
{
    public static string Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Use clean Edit Mode.");
        string original = SceneManager.GetActiveScene().path;
        Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/Garden.unity");
        Camera camera = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Camera>(true)).Single();
        Vector3 position = camera.transform.position;
        Quaternion rotation = camera.transform.rotation;
        RenderTexture texture = RenderTexture.GetTemporary(1024, 1024, 24);
        var pixels = new Texture2D(1024, 1024, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            camera.transform.position = new Vector3(-31.7f, 100, -38.1f);
            camera.transform.rotation = Quaternion.Euler(90, 0, 0);
            camera.orthographic = true;
            camera.orthographicSize = 35;
            camera.aspect = 1;
            camera.targetTexture = texture;
            camera.Render();
            RenderTexture.active = texture;
            pixels.ReadPixels(new Rect(0, 0, 1024, 1024), 0, 0);
            pixels.Apply();
            File.WriteAllBytes("Docs/MapPolish-20261108/Evidence/garden-overview.png", pixels.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            camera.targetTexture = null;
            camera.transform.position = position;
            camera.transform.rotation = rotation;
            UnityEngine.Object.DestroyImmediate(pixels);
            RenderTexture.ReleaseTemporary(texture);
            EditorSceneManager.OpenScene(original);
        }
        return "Rendered authored Garden overview only; original scene restored without saving camera changes.";
    }
}
