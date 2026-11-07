// Compiles the actual Editor storage entry points against small Unity/SDK I/O stubs.
// All filesystem I/O is inside a fresh OS temporary directory; user saves are never addressed.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using YG;
using YG.Insides;
using Vodovorot.Stage3;

internal static class EditorStorageIsolationSmoke
{
    private static int checks;
    private static void Check(bool value, string detail) { checks++; if (!value) throw new Exception(detail); }
    public static void Main()
    {
        string directory = Path.Combine(Path.GetTempPath(), "vodovorot-stage3-storage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        InfoYG.PATCH_PC_EDITOR = directory;
        string file = Path.Combine(directory, "SavesEditorYG2.json");
        try
        {
            YG2.saves = new SavesYG { idSave = 7, exp = 123, goldCoins = 41 };
            YGInsides.SaveEditor();
            byte[] original = File.ReadAllBytes(file);
            Check(original.Length > 0, "Control save must exercise the real normal Editor write route.");
            Check(UnityEditor.AssetDatabase.RefreshCount == 1, "Normal first save must retain the plugin refresh behavior.");
            var test = new SavesYG { idSave = 11, exp = 9000, goldCoins = 999 };
            YG2.saves = test;
            RankSimulationContext.Active = test;
            RankSimulationContext.Guarded = true;
            YGInsides.SaveEditor();
            Check(File.ReadAllBytes(file).SequenceEqual(original), "A direct SaveEditor call must not write the original file during a simulation.");
            Check(RankSimulationContext.Suppressed == 1, "The direct suppressed save must be observable.");
            YG2.SaveProgress();
            Check(test.idSave == 12 && File.ReadAllBytes(file).SequenceEqual(original), "The real SaveProgress route may advance only the in-memory profile ID.");
            Check(RankSimulationContext.Suppressed == 2, "The ordinary SaveProgress route must reach the guard.");
            YGInsides.LoadEditor();
            Check(ReferenceEquals(YG2.saves, test) && YG2.saves.exp == 9000, "A normal Editor reload must retain the exact isolated object reference.");
            InfoYG.PATCH_PC_EDITOR = Path.Combine(directory, "missing-directory");
            YGInsides.SaveEditor();
            Check(!Directory.Exists(InfoYG.PATCH_PC_EDITOR), "The guard must return before any filesystem write or missing-directory failure.");
            YG2.saves = new SavesYG();
            YGInsides.LoadEditor();
            Check(ReferenceEquals(YG2.saves, test), "An active profile load must return before even the absent-file/default-profile branch.");
            Check(UnityEditor.AssetDatabase.RefreshCount == 1, "Suppressed saves must not refresh the AssetDatabase.");
            InfoYG.PATCH_PC_EDITOR = directory;
            RankSimulationContext.Active = null;
            RankSimulationContext.IsolateOnNormalLoad = true;
            YGInsides.LoadEditor();
            Check(RankSimulationContext.Captured != null && RankSimulationContext.Captured.exp == 123 && RankSimulationContext.Captured.goldCoins == 41,
                "The early Play guard must capture the original profile only after the plugin's normal load.");
            Check(ReferenceEquals(YG2.saves, RankSimulationContext.Active) && !ReferenceEquals(YG2.saves, RankSimulationContext.Captured),
                "The first loaded profile must be isolated before gameplay can save.");
            YG2.SaveProgress();
            Check(File.ReadAllBytes(file).SequenceEqual(original), "The original Editor save must remain byte-exact across a guarded first load and subsequent save.");
            Check(YG2.iPlatform.CloudWrites == 0, "Editor isolation must not route a save to the platform/cloud.");
            Console.WriteLine($"PASS: {checks} actual Editor storage guard/load/object-identity/file-byte contracts");
        }
        finally { Directory.Delete(directory, true); }
    }
}

internal static class Json { internal static readonly JsonSerializerOptions Options = new JsonSerializerOptions { IncludeFields = true }; }
namespace UnityEngine
{
    public static class Time { public static float unscaledTime = 10; }
    public static class Debug { public static void LogError(string text) { } }
    public static class JsonUtility
    {
        public static string ToJson(object value, bool pretty = false) => JsonSerializer.Serialize(value, Json.Options);
        public static T FromJson<T>(string value) => JsonSerializer.Deserialize<T>(value, Json.Options);
    }
}
namespace UnityEditor
{
    public static class AssetDatabase { public static int RefreshCount; public static void Refresh() { RefreshCount++; } }
}
namespace YG
{
    public sealed class InitYGAttribute : Attribute { }
    public sealed class StartYGAttribute : Attribute { }
    public sealed class InfoYG
    {
        public const string NO_DATA = "no data";
        public static string PATCH_PC_EDITOR;
        public StorageSettings Storage = new StorageSettings();
        public sealed class StorageSettings { public bool saveCloud = true; public bool saveLocal; }
    }
    public sealed class PlatformStub
    {
        public int CloudWrites;
        public void SaveCloud() { CloudWrites++; }
        public void LoadCloud() { }
    }
    public static partial class YG2
    {
        private static bool _SDKEnabled = true;
        public static InfoYG infoYG = new InfoYG();
        public static PlatformStub iPlatform = new PlatformStub();
        public static void Message(string value) { }
        public static void GetDataInvoke() { }
    }
}
namespace YG.Insides
{
    public static partial class YGInsides
    {
        public static InfoYG infoYG => YG2.infoYG;
        public static PlatformStub iPlatform => YG2.iPlatform;
        public static void Message(string value) { }
        public static void GetDataInvoke() => YG2.GetDataInvoke();
    }
}
namespace YG.Utils
{
    public static class LocalStorage
    {
        public static bool HasKey(string key) => false;
        public static string GetKey(string key) => null;
        public static void SetKey(string key, string value) { }
    }
}
namespace Vodovorot.Stage2
{
    public static class LeaderboardRoundService
    {
        public static bool IsAuthProfileWriteBlocked => false;
        public static bool NotifyAuthCloudLoadFailed() => false;
        public static bool TryResolveAuthCloudProfile(SavesYG cloud, bool missing, bool broken, out SavesYG resolved, out bool carry)
        { resolved = null; carry = false; return false; }
        public static void NotifyAuthCloudProfileReady() { }
    }
}
namespace Vodovorot.Stage3
{
    public static class RankSimulationContext
    {
        public static bool Guarded, IsolateOnNormalLoad;
        public static SavesYG Active, Captured;
        public static int Suppressed;
        public static bool BlockEditorSave => Guarded;
        public static void NotifySuppressedEditorSave() { Suppressed++; }
        public static bool TryLoadIsolatedEditorProfile()
        {
            if (Active == null) return false;
            YG2.saves = Active;
            return true;
        }
        public static void OnEditorProfileLoaded()
        {
            if (!IsolateOnNormalLoad) return;
            Captured = YG2.saves;
            Active = new SavesYG { exp = 1000, goldCoins = 100 };
            YG2.saves = Active;
        }
    }
}
