using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Vodovorot.RoundExperience;
using Vodovorot.Stage3;
using YG;

public static class CheckRoundExperience
{
    [Serializable] private sealed class Check { public string name; public bool passed; }
    [Serializable] private sealed class Report
    {
        public string environment = "Unity Editor: controlled fixtures and real components; not a gameplay pass";
        public string finishedUtc;
        public string error;
        public List<Check> checks = new List<Check>();
        public List<string> runtimeErrors = new List<string>();
    }
    private static Report report;
    private static RoundExperienceSettings sourceSettings;
    private static RoundExperienceValues originalValues;

    public static async Task<string> Run()
    {
        report = new Report();
        Application.logMessageReceived += ObserveError;
        byte[] originalSave = File.ReadAllBytes("Assets/PluginYourGames/Editor/SavesEditorYG2.json");
        try
        {
            Assert(Application.isPlaying && RankSimulationContext.Active, "isolated profile required");
            Assert(SceneManager.GetActiveScene().path == RoundExperienceLab.ScenePath, "actual saved tuning scene loaded");
            Assert(YG2.saves.soundValue == 0f && YG2.saves.musicValue == 0f, "test starts with Music and SFX at zero");
            RoundExperienceLab lab = One<RoundExperienceLab>();
            var data = new SerializedObject(lab);
            RoundExperienceSettings settings = (RoundExperienceSettings)data.FindProperty("settings").objectReferenceValue;
            RoundExperienceValues defaults = settings.CreateRuntimeValues();
            sourceSettings = settings;
            originalValues = defaults;
            Slider[] sliders = Enumerable.Range(0, 4).Select(i => (Slider)data.FindProperty("sliders").GetArrayElementAtIndex(i).objectReferenceValue).ToArray();
            sliders[0].value = 120;
            sliders[1].value = 7;
            sliders[2].value = 2;
            sliders[3].value = .35f;
            Assert(One<GamingManager>().totalCleaningDuration == 120, "lab slider changes the real round timer");
            Assert(settings.CreateRuntimeValues().MinimumCompletedExperience == defaults.MinimumCompletedExperience,
                "moving a slider does not mutate the shared settings asset");
            ((Button)data.FindProperty("saveButton").objectReferenceValue).onClick.Invoke();
            string saved = File.ReadAllText(AssetDatabase.GetAssetPath(settings));
            Assert(saved.Contains("minimumCompletedExperience: 7") && saved.Contains("roundSeconds: 120"), "Save writes selected parameters to the asset");
            sliders[1].value = 9;
            ((Button)data.FindProperty("resetButton").objectReferenceValue).onClick.Invoke();
            Assert(sliders[1].value == 7, "Reset restores the saved choice");
            ((Button)data.FindProperty("restartButton").objectReferenceValue).onClick.Invoke();
            await Task.Delay(600);
            lab = One<RoundExperienceLab>();
            data = new SerializedObject(lab);
            sliders = Enumerable.Range(0, 4).Select(i => (Slider)data.FindProperty("sliders").GetArrayElementAtIndex(i).objectReferenceValue).ToArray();
            Assert(sliders[0].value == 120 && sliders[1].value == 7 && One<GamingManager>().timer < 3,
                "Restart creates a new real round and retains selected parameters");
            sliders[0].value = defaults.RoundSeconds;
            sliders[1].value = defaults.MinimumCompletedExperience;
            sliders[2].value = defaults.MinimumCompletedCoins;
            sliders[3].value = defaults.CutawayReleaseSeconds;
            ((Button)data.FindProperty("saveButton").objectReferenceValue).onClick.Invoke();
            Assert(settings.CreateRuntimeValues().RoundSeconds == 180 && settings.CreateRuntimeValues().MinimumCompletedExperience == 5,
                "production defaults restored after testing Save");

            GamingManager game = One<GamingManager>();
            BlackHoleController player = One<BlackHoleController>();
            SetAutoProperty(game, "Stage2NaturalCompletion", true);
            Assert(game.GetCurrentTotalCleaningReward().exp == 0, "empty completed round receives no minimum reward");
            game.RegisterPlayerAbsorption(player, 1);
            Assert(game.GetCurrentTotalCleaningReward().exp == 5 && game.GetCurrentTotalCleaningReward().coins == 1,
                "useful completed round receives configured minimum XP and coins");
            SetAutoProperty(game, "Stage2NaturalCompletion", false);
            Assert(game.GetCurrentTotalCleaningReward().exp == 0, "interrupted round cannot farm the completion minimum");
            SetAutoProperty(game, "Stage2NaturalCompletion", true);
            int xpBefore = YG2.saves.exp;
            int coinsBefore = YG2.saves.goldCoins;
            var reward = game.GetCurrentTotalCleaningReward();
            game.ApplyMatchReward(reward);
            game.ApplyMatchReward(reward);
            Assert(YG2.saves.exp == xpBefore + 5 && YG2.saves.goldCoins == coinsBefore + 1,
                "base reward is applied exactly once");
            Assert(game.TryBeginCoinBonus() && !game.TryBeginCoinBonus(), "only one rewarded request can be pending");
            game.CancelCoinBonus();
            Assert(!game.TryApplyCoinBonus() && YG2.saves.goldCoins == coinsBefore + 1,
                "cancelled or unsolicited rewarded callback grants nothing");
            Assert(game.TryBeginCoinBonus() && game.TryApplyCoinBonus() && !game.TryApplyCoinBonus(),
                "successful coin multiplier can be claimed only once");
            Assert(YG2.saves.goldCoins == coinsBefore + 3 && !game.TryBeginCoinBonus(),
                "x3 total is exact and another request is refused");

            player.InitializeScore(15000, true);
            Assert(player.currentLevel == 10 && player.PointsToNextSize == 0 && player.border.fillAmount == 1f,
                "final authored growth threshold is reachable and full");
            player.InitializeScore(25, true);
            Assert(player.currentLevel == 0 && player.PointsToNextSize == 1, "growth readout respects the first threshold boundary");
            player.InitializeScore(0, true);
            float smallWidth = player.GetVisualSizeOfHole().x;
            int growthFrame = Time.frameCount;
            player.AddScore(26);
            // Editor automation can render only a few frames during a wall-clock delay.
            // Wait for the actual interpolation rather than assuming a frame rate.
            DateTime growthDeadline = DateTime.UtcNow.AddSeconds(12);
            while (player.transform.localScale.x < .805f && DateTime.UtcNow < growthDeadline)
                await Task.Delay(100);
            Assert(player.currentLevel == 1 && player.size.x > smallWidth * 1.7f &&
                // FixedUpdate and the rotating polygon's rendered bounds use different samples.
                // A 5% allowance still rejects the old pre-growth bounds (about 50% stale).
                Mathf.Abs(player.size.x - player.GetVisualSizeOfHole().x) < player.GetVisualSizeOfHole().x * .05f,
                "fit size follows visible growth without another score; level=" + player.currentLevel +
                "; small=" + smallWidth + "; cached=" + player.size.x + "; visible=" + player.GetVisualSizeOfHole().x +
                "; timeScale=" + Time.timeScale + "; frames=" + (Time.frameCount - growthFrame));
            Time.timeScale = 0;
            await Task.Delay(1400);
            Assert(!UnityEngine.Object.FindObjectsByType<PointsScript>(FindObjectsInactive.Exclude).Any(),
                "point effects expire while the game is paused");
            Time.timeScale = 1;

            CameraCutawayView cutaway = One<CameraCutawayView>();
            var cutawayData = new SerializedObject(cutaway);
            cutawayData.FindProperty("keepViewInsideMap").boolValue = false;
            cutawayData.ApplyModifiedPropertiesWithoutUndo();
            Camera camera = (Camera)cutawayData.FindProperty("viewCamera").objectReferenceValue;
            bool originalCulling = camera.useOcclusionCulling;
            Vector3 cameraPosition = camera.transform.position;
            Renderer obstacle = Enumerable.Range(0, cutawayData.FindProperty("occluders").arraySize)
                .Select(i => (Renderer)cutawayData.FindProperty("occluders").GetArrayElementAtIndex(i).objectReferenceValue)
                .First(r => !r.forceRenderingOff && Vector3.Distance(r.bounds.center, cameraPosition) > 30);
            camera.transform.position = obstacle.bounds.center;
            await Task.Delay(120);
            Assert(obstacle.forceRenderingOff, "a camera inside an authored large occluder hides that renderer");
            Assert(!camera.useOcclusionCulling, "hidden baked occluders cannot cull the revealed world");
            camera.transform.position = cameraPosition;
            await Task.Delay(350);
            Assert(!obstacle.forceRenderingOff, "occluder is restored after the camera leaves");
            camera.transform.position = obstacle.bounds.center;
            await Task.Delay(100);
            cutaway.enabled = false;
            Assert(!obstacle.forceRenderingOff, "disabling the cutaway restores original renderer visibility");
            Assert(camera.useOcclusionCulling == originalCulling, "disabling cutaway restores the camera culling setting");
            camera.transform.position = cameraPosition;
            cutaway.enabled = true;

            cutawayData.Update();
            cutawayData.FindProperty("keepViewInsideMap").boolValue = true;
            cutawayData.ApplyModifiedPropertiesWithoutUndo();
            Vector3 originalPlayerPosition = player.transform.position;
            for (int corner = 0; corner < 4; corner++)
            {
                player.transform.position = new Vector3(corner % 2 == 0 ? game.minX + .5f : game.maxX - .5f,
                    originalPlayerPosition.y, corner / 2 == 0 ? game.minZ + .5f : game.maxZ - .5f);
                camera.transform.localPosition = (Vector3)typeof(CameraCutawayView)
                    .GetField("authoredLocalPosition", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(cutaway);
                float originalOvershoot = BorderOvershoot(camera, player.transform.position, game);
                await Task.Delay(180);
                Plane ground = new Plane(Vector3.up, player.transform.position);
                bool footprintInside = true;
                var footprint = new List<string>();
                for (int rayIndex = 0; rayIndex < 4; rayIndex++)
                {
                    Ray ray = camera.ViewportPointToRay(new Vector3(rayIndex % 2, rayIndex / 2, 0));
                    if (!ground.Raycast(ray, out float distance)) { footprintInside = false; break; }
                    Vector3 hit = ray.GetPoint(distance);
                    footprint.Add(hit.ToString());
                    footprintInside &= hit.x >= game.minX - 2.01f && hit.x <= game.maxX + 2.01f &&
                        hit.z >= game.minZ - 2.01f && hit.z <= game.maxZ + 2.01f;
                }
                Vector3 playerOnScreen = camera.WorldToViewportPoint(player.transform.position);
                float correctedOvershoot = BorderOvershoot(camera, player.transform.position, game);
                Assert(correctedOvershoot < originalOvershoot - .05f && playerOnScreen.x >= .299f && playerOnScreen.x <= .701f &&
                    playerOnScreen.y >= .299f && playerOnScreen.y <= .701f, "camera reduces out-of-map view without clipping the player at corner " + corner +
                    "; inside=" + footprintInside + "; player=" + playerOnScreen + "; camera=" + camera.transform.position +
                    "; overshoot=" + originalOvershoot + " -> " + correctedOvershoot +
                    "; bounds=" + game.minX + "," + game.maxX + "," + game.minZ + "," + game.maxZ + "; footprint=" + string.Join(";", footprint));
            }
            player.transform.position = originalPlayerPosition;
            var movement = player.GetComponentInChildren<PinePie.SimpleJoystick.Examples.DemoScript.MovementScript>(true);
            MethodInfo clampPosition = movement.GetType().GetMethod("ClampPositionToMap", BindingFlags.Instance | BindingFlags.NonPublic);
            for (int corner = 0; corner < 4; corner++)
            {
                Vector3 requested = new Vector3(corner % 2 == 0 ? -10000 : 10000, originalPlayerPosition.y,
                    corner / 2 == 0 ? -10000 : 10000);
                Vector3 bounded = (Vector3)clampPosition.Invoke(movement, new object[] { requested });
                Assert(bounded.x - player.size.x * .5f > game.minX && bounded.x + player.size.x * .5f < game.maxX &&
                    bounded.z - player.size.z * .5f > game.minZ && bounded.z + player.size.z * .5f < game.maxZ &&
                    bounded.y == requested.y, "movement keeps the entire visible rim within the map at corner " + corner);
            }

            game.EndOfGame();
            await Task.Delay(500);
            Assert(UnityEngine.Object.FindObjectsByType<RoundHudView>(FindObjectsInactive.Exclude)
                .All(view => !view.GetComponent<Text>().enabled), "growth HUD is hidden on results");

            SceneManager.LoadScene(0);
            await Task.Delay(600);
            MainMenuController menu = One<MainMenuController>();
            AccountProgressView[] goalViews = UnityEngine.Object.FindObjectsByType<AccountProgressView>(FindObjectsInactive.Include);
            Assert(goalViews.All(view => new SerializedObject(view).FindProperty("settings").objectReferenceValue != null),
                "all saved account goal views retain their settings asset reference");
            YG2.saves.langRu = false;
            menu.UpdateMainMenu();
            Assert(goalViews.Where(view => view.isActiveAndEnabled).All(view => view.GetComponent<Text>().text.Contains("XP")),
                "account goal follows English menu localization");
            YG2.saves.langRu = true;
            menu.UpdateMainMenu();
            Assert(goalViews.Where(view => view.isActiveAndEnabled).All(view => view.GetComponent<Text>().text.Contains("опыта")),
                "account goal follows Russian menu localization");
            Assert(menu.scoreText.text == Tool.ConvertText(YG2.saves.exp) && menu.DscoreText.text == menu.scoreText.text,
                "XP display uses the saved account experience rather than the authored placeholder");
            LanguageManager language = One<LanguageManager>();
            // Restore through a scene transition first so the real selector derives
            // its state from the saved RU value, then use its actual button handler.
            SceneManager.LoadScene(0);
            await Task.Delay(500);
            language = One<LanguageManager>();
            Assert(YG2.saves.langRu, "Russian choice survives a menu scene reload");
            language.Onclick();
            Assert(!YG2.saves.langRu, "language button toggles the restored choice to English");
            SceneManager.LoadScene(0);
            await Task.Delay(500);
            menu = One<MainMenuController>();
            Assert(!YG2.saves.langRu && UnityEngine.Object.FindObjectsByType<AccountProgressView>(FindObjectsInactive.Exclude)
                .All(view => view.GetComponent<Text>().text.Contains("XP")), "English choice and goal survive another scene reload");
            FieldInfo pending = typeof(MainMenuController).GetField("pendingRewardId", BindingFlags.Instance | BindingFlags.NonPublic);
            int diamondsBefore = YG2.saves.diamonds;
            pending.SetValue(menu, "hand");
            YG2.onRewardAdv?.Invoke("hand");
            YG2.onRewardAdv?.Invoke("hand");
            Assert(YG2.saves.rewardedHandProgress == 1 && YG2.saves.diamonds == diamondsBefore,
                "one ad advances one saved step; duplicate callback has no effect");
            SceneManager.LoadScene(0);
            await Task.Delay(500);
            menu = One<MainMenuController>();
            Assert(YG2.saves.rewardedHandProgress == 1 && menu.Thand.text.Contains("1"),
                "partial rewarded progress survives leaving and reopening the menu");
            pending.SetValue(menu, "hand");
            YG2.onRewardAdv?.Invoke("hand");
            YG2.onRewardAdv?.Invoke("hand");
            Assert(YG2.saves.rewardedHandProgress == 0 && YG2.saves.diamonds == diamondsBefore + 20,
                "second valid hand step pays the promised reward exactly once");
            pending.SetValue(menu, "bag");
            YG2.onErrorRewardedAdv?.Invoke();
            YG2.onRewardAdv?.Invoke("bag");
            Assert(YG2.saves.rewardedBagProgress == 0, "failed ad does not advance saved progress");
            var oldSave = JsonUtility.FromJson<SavesYG>(System.Text.Encoding.UTF8.GetString(originalSave));
            Assert(oldSave.rewardedHandProgress == 0 && oldSave.rewardedBagProgress == 0 && oldSave.rewardedBoxProgress == 0,
                "old JSON without new fields loads with zero ad progress");
            SavesYG roundTrip = JsonUtility.FromJson<SavesYG>(JsonUtility.ToJson(YG2.saves));
            Assert(roundTrip.diamonds == YG2.saves.diamonds && roundTrip.exp == YG2.saves.exp,
                "new profile round-trip preserves existing economy fields");

            AudioManager audio = One<AudioManager>();
            audio.DMusicSlider.SetValueWithoutNotify(.3f);
            int saves = RankSimulationContext.SuppressedSaveCount;
            audio.MusicSlider.onValueChanged.Invoke(0f);
            Assert(RankSimulationContext.SuppressedSaveCount == saves + 1 && audio.DMusicSlider.value == 0f && YG2.saves.musicValue == 0f,
                "silent slider synchronization causes only one save callback");
            Assert(ProgressReadout.Capture(.002f, true) == "0,2%" && ProgressReadout.AccountGoal(5, 100, true).Contains("95"),
                "coverage and next account goal show the actual earned progress");
            Assert(originalSave.SequenceEqual(File.ReadAllBytes("Assets/PluginYourGames/Editor/SavesEditorYG2.json")),
                "personal save remains byte-for-byte unchanged");
            Assert(report.runtimeErrors.Count == 0, "no Unity runtime errors during the integration sequence");
        }
        catch (Exception error)
        {
            report.error = error.ToString();
            throw;
        }
        finally
        {
            if (sourceSettings != null)
            {
                var original = new SerializedObject(sourceSettings);
                original.FindProperty("roundSeconds").floatValue = originalValues.RoundSeconds;
                original.FindProperty("minimumCompletedExperience").intValue = originalValues.MinimumCompletedExperience;
                original.FindProperty("minimumCompletedCoins").intValue = originalValues.MinimumCompletedCoins;
                original.FindProperty("cutawayReleaseSeconds").floatValue = originalValues.CutawayReleaseSeconds;
                original.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(sourceSettings);
            }
            report.finishedUtc = DateTime.UtcNow.ToString("o");
            Directory.CreateDirectory("Docs/ImprovementPass-20261107/Checks");
            File.WriteAllText("Docs/ImprovementPass-20261107/Checks/editor-integration.json",
                Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented));
            RankSimulationContext.Stop();
            Application.logMessageReceived -= ObserveError;
        }
        return "PASS: " + report.checks.Count + " Editor integration checks (controlled fixtures).";
    }

    private static T One<T>() where T : UnityEngine.Object => UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include).Single();
    private static float BorderOvershoot(Camera camera, Vector3 position, GamingManager game)
    {
        Plane plane = new Plane(Vector3.up, position);
        float maximum = 0;
        for (int corner = 0; corner < 4; corner++)
        {
            Ray ray = camera.ViewportPointToRay(new Vector3(corner % 2, corner / 2, 0));
            if (!plane.Raycast(ray, out float distance)) return float.PositiveInfinity;
            Vector3 hit = ray.GetPoint(distance);
            maximum = Mathf.Max(maximum, game.minX - hit.x, hit.x - game.maxX, game.minZ - hit.z, hit.z - game.maxZ);
        }
        return maximum;
    }
    private static void SetAutoProperty(object target, string property, object value) =>
        target.GetType().GetProperty(property).SetValue(target, value);
    private static void ObserveError(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            report.runtimeErrors.Add(message + "\n" + stack);
    }
    private static void Assert(bool value, string name)
    {
        report.checks.Add(new Check { name = name, passed = value });
        if (!value) throw new Exception(name);
    }
}
