#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Vodovorot.Stage3.Editor
{
    /// <summary>Idempotent asset binding; does not touch scenes, prefabs, importers or project settings.</summary>
    public static class RankingVisualStyleBuilder
    {
        public const string AssetPath = "Assets/Resources/Stage3/RankingVisualStyle.asset";
        public const string ShapePath = "Assets/Images/Sprite/Primitives/UI_SquareOutline.png";
        public const string FillPath = "Assets/Images/Sprite/Primitives/UI_SquareRounded.png";
        public const string FontPath = "Assets/Images/Comic Sans MS.ttf";
        public const string AtlasPath = "Assets/Images/Sprite/GUI.png";

        [MenuItem("Tools/Vodovorot/Stage 3/Build ranking visual style")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Stage3")) AssetDatabase.CreateFolder("Assets/Resources", "Stage3");
            var style = AssetDatabase.LoadAssetAtPath<RankingVisualStyle>(AssetPath);
            if (style == null)
            {
                style = ScriptableObject.CreateInstance<RankingVisualStyle>();
                AssetDatabase.CreateAsset(style, AssetPath);
            }
            // MainMenu/PlayButton uses this exact sliced primitive with a black Outline.
            style.roundedShape = Require<Sprite>(ShapePath);
            style.filledShape = Require<Sprite>(FillPath);
            style.comicFont = Require<Font>(FontPath);
            // Verified atlas names: GUI_4 is the gold medal; GUI_24 is the lit gold star.
            var sprites = AssetDatabase.LoadAllAssetsAtPath(AtlasPath).OfType<Sprite>().ToArray();
            style.goldMedal = sprites.Single(sprite => sprite.name == "GUI_4");
            style.goldStar = sprites.Single(sprite => sprite.name == "GUI_24");
            EditorUtility.SetDirty(style); AssetDatabase.SaveAssets();
            Debug.Log("[Stage3] Ranking style bound to canonical PlayButton outline, opaque UI_SquareRounded fill, Comic Sans, GUI_4 and GUI_24. Only ranking consumes this catalog.");
        }

        private static T Require<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new InvalidOperationException("Missing canonical " + typeof(T).Name + ": " + path);
            return asset;
        }
    }
}
#endif
