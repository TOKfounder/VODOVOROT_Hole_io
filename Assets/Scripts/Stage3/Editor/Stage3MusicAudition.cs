#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Vodovorot.Stage2;

namespace Vodovorot.Stage3.Editor
{
    /// <summary>Shared controls can also be drawn in the Stage 3 review window.</summary>
    [InitializeOnLoad]
    public static class Stage3MusicAudition
    {
        private const string AudioPath = "Assets/Editor/Stage3/MusicAuditions/Resources/Stage3Music/";
        private static readonly string[] Ids = { "menu", "city", "garden", "medieval" };
        private static readonly string[] Labels = { "Меню — акустический грув", "Город — акустический фанк", "Сад — мягкие струны и дерево", "Замок — шуточный струнный танец" };
        private static string auditioning;
        private static string pendingAccentId, pendingRoundId, pendingTheme;
        private static double accentDue;
        private static bool exitingPlayMode;

        public static bool CanAuditionRankAccent => EditorApplication.isPlaying && Application.isPlaying && RankSimulationContext.Active;
        public static bool RankAccentPending => pendingAccentId != null;

        static Stage3MusicAudition()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting += Stop;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update += TickPendingAccent;
        }

        /// <summary>Edit mode uses Unity's one-clip editor preview; Play mode uses the runtime Music bus.</summary>
        public static void Play(string themeId)
        {
            string resource = BackgroundMusicPlayer.ResourceForTheme(themeId);
            if (resource == null) return;
            EndAudition(false);
            if (EditorApplication.isPlaying)
            {
                BackgroundMusicPlayer.PlayPreviewTheme(themeId);
                auditioning = themeId;
                return;
            }
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPath + resource.Substring(resource.LastIndexOf('/') + 1) + ".wav");
            if (clip == null) { Debug.LogWarning("Stage 3 demo has not been imported: " + resource); return; }
            Type audioUtil = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
            MethodInfo play = audioUtil != null ? audioUtil.GetMethod("PlayPreviewClip", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null) : null;
            if (play == null) { Debug.LogWarning("Editor audio preview is unavailable. Open the WAV directly or audition in Play mode."); return; }
            try { play.Invoke(null, new object[] { clip, 0, false }); auditioning = themeId; }
            catch (Exception error) { Debug.LogWarning("Unable to preview Stage 3 music: " + error.Message); }
        }

        public static void Stop() => EndAudition(true);

        private static void EndAudition(bool restoreAutomatic)
        {
            CancelPendingRankAccent();
            BackgroundMusicPlayer.StopPreview();
            if (restoreAutomatic && !exitingPlayMode && EditorApplication.isPlaying && Application.isPlaying)
                BackgroundMusicPlayer.SetAutomaticPlaybackForReview(true);
            Type audioUtil = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
            MethodInfo stop = audioUtil != null ? audioUtil.GetMethod("StopAllPreviewClips", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) : null;
            try { if (stop != null) stop.Invoke(null, null); }
            catch (Exception error) { Debug.LogWarning("Unable to stop the editor audio preview: " + error.Message); }
            auditioning = null;
        }

        /// <summary>Audition only: no profile, rank service or award mutation; uses the real SFX path.</summary>
        public static bool PlayRankAccentAlone()
        {
            if (!CanAuditionRankAccent) return false;
            EndAudition(false);
            BackgroundMusicPlayer.SetAutomaticPlaybackForReview(false);
            ScheduleRankAccent(null, .9);
            TickPendingAccent();
            return true;
        }

        /// <summary>Simple entry point for the review window and automated QA.</summary>
        public static bool PlayRankAccent(bool withBackground) =>
            withBackground ? PlayRankAccentWithBackground() : PlayRankAccentAlone();

        /// <summary>Restarts the selected demo, waits for its 0.7s fade to complete, then accents/ducks.</summary>
        public static bool PlayRankAccentWithBackground(string themeId = null)
        {
            if (!CanAuditionRankAccent) return false;
            string selected = themeId ?? BackgroundMusicPlayer.PreviewThemeId ?? auditioning ?? "menu";
            if (BackgroundMusicPlayer.ResourceForTheme(selected) == null) return false;
            Play(selected);
            ScheduleRankAccent(selected, .9);
            return true;
        }

        private static void ScheduleRankAccent(string backgroundTheme, double entryDelay)
        {
            pendingAccentId = NewAccentId();
            pendingRoundId = RankSimulationContext.CurrentRoundId;
            pendingTheme = backgroundTheme;
            accentDue = EditorApplication.timeSinceStartup + entryDelay;
        }

        public static void CancelPendingRankAccent()
        {
            pendingAccentId = pendingRoundId = pendingTheme = null;
            accentDue = 0;
        }

        private static string NewAccentId() => "stage3-audio-audition-" + Guid.NewGuid().ToString("N");

        private static void PlayActualRankAccent(string id)
        {
            if (CanAuditionRankAccent) Stage2Audio.PlayConfirmedRankImprovement(30, 29, id);
        }

        private static void TickPendingAccent()
        {
            if (!RankAccentPending) return;
            // Covers Stop(), play-mode exit, scene changes, simulation Stop/restart,
            // and direct BackgroundMusicPlayer.StopPreview() calls from QA/tools.
            if (!CanAuditionRankAccent || RankSimulationContext.CurrentRoundId != pendingRoundId ||
                (pendingTheme != null && BackgroundMusicPlayer.PreviewThemeId != pendingTheme))
            {
                CancelPendingRankAccent();
                return;
            }
            // ShowImmediate can already have played the real celebration. Wait for its
            // actual DSP cooldown, rather than making a new button silently lose a cue.
            if (EditorApplication.timeSinceStartup < accentDue || AudioSettings.dspTime < Stage2Audio.NextRankAccentAt) return;
            if (pendingTheme == null && BackgroundMusicPlayer.HasActiveMusicVoice) return;
            if (pendingTheme != null && !BackgroundMusicPlayer.IsPreviewPlaying) return;
            string id = pendingAccentId;
            CancelPendingRankAccent();
            PlayActualRankAccent(id);
        }

        public static void DrawControls()
        {
            EditorGUILayout.HelpBox("Полные темы меню, города, сада и замка готовы и звучат автоматически в игре. Здесь — короткие отрывки для сравнения. «Остановить» возвращает музыку текущей сцены.", MessageType.Info);
            for (int i = 0; i < Ids.Length; i++)
                if (GUILayout.Button(Labels[i], GUILayout.Height(28))) Play(Ids[i]);
            using (new EditorGUI.DisabledScope(!CanAuditionRankAccent))
            {
                if (GUILayout.Button("Победный акцент — отдельно", GUILayout.Height(28))) PlayRankAccentAlone();
                if (GUILayout.Button("Победный акцент — с музыкой", GUILayout.Height(28))) PlayRankAccentWithBackground();
            }
            if (!CanAuditionRankAccent)
                EditorGUILayout.LabelField("Для проверки акцента сначала нажмите «Показать рейтинг сейчас» в окне этапа 3.", EditorStyles.wordWrappedMiniLabel);
            else if (RankAccentPending)
                EditorGUILayout.LabelField(pendingTheme != null ? "Готовим акцент: ждём входа музыки и окончания предыдущего сигнала." :
                    "Предыдущий сигнал завершается; затем прозвучит акцент отдельно.", EditorStyles.wordWrappedMiniLabel);
            if (GUILayout.Button("Остановить предпрослушивание", GUILayout.Height(26))) Stop();
            EditorGUILayout.LabelField("Выбрано: " + (auditioning ?? "—"), EditorStyles.miniLabel);
            EditorGUILayout.LabelField(EditorApplication.isPlaying ? "Предпрослушивание: канал Music и настройки игры." :
                "Предпрослушивание редактора: громкость Unity / системы.", EditorStyles.wordWrappedMiniLabel);
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            exitingPlayMode = state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode;
            Stop();
            if (state == PlayModeStateChange.EnteredEditMode) exitingPlayMode = false;
        }
    }

    public sealed class Stage3MusicAuditionWindow : EditorWindow
    {
        [MenuItem("Tools/Vodovorot/Stage 3/Listen to music demos")]
        public static void Open()
        {
            Stage3MusicAuditionWindow window = GetWindow<Stage3MusicAuditionWindow>("Музыка · этап 3");
            window.minSize = new Vector2(370, 420);
        }

        private void OnGUI() => Stage3MusicAudition.DrawControls();
        private void OnDisable() => Stage3MusicAudition.Stop();
    }
}
#endif
