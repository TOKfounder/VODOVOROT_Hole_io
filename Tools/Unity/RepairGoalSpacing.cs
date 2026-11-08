using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RepairGoalSpacing
{
    public static string Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Use clean Edit Mode.");
        const string path = "Assets/Prefabs/GamingPrefabs/ImmersivePack.prefab";
        GameObject prefab = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var panel = (RectTransform)prefab.transform.Find("RoundGuidanceCanvas/SafeArea/Optional route goal");
            panel.anchorMin = new Vector2(.14f, .23f);
            panel.anchorMax = new Vector2(.86f, .33f);
            PrefabUtility.SaveAsPrefabAsset(prefab, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        return "Saved goal panel above desktop boost button.";
    }
}
