// Compiles the actual Storage_yg.cs and real profile schema against small Unity/SDK I/O stubs.
// This checks production C# control flow; it does not simulate a successful live cloud service.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using YG;
using YG.Insides;
using Vodovorot.Stage2;

internal static class StorageGuardSmoke
{
    private static int checks;
    private static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    private static SavesYG Guest() => new SavesYG { isFirst = false, exp = 123, goldCoins = 45, equipedMaterial = 2,
        massiveOfObtaining = new[] {1, 1, 1, 0, 0}, idSave = 50 };
    private static void Reset(bool pending = true)
    {
        YG2.saves = Guest();
        YG2.infoYG.Storage.saveCloud = true;
        YG2.infoYG.Storage.saveLocal = false;
        LeaderboardRoundService.Pending = pending;
        LeaderboardRoundService.Failed = false;
        LeaderboardRoundService.GuestProfile = YG2.saves;
        LeaderboardRoundService.ExpectedProfile = null;
        Probe.Log.Clear();
        YG2.onDefaultSaves = () => Probe.Log.Add("default");
        YG.Utils.LocalStorage.Values.Clear();
    }
    private static string Cloud(SavesYG save) => JsonSerializer.Serialize(new[] { JsonSerializer.Serialize(save, Json.Options) }, Json.Options);

    public static void Main()
    {
        Reset(); var guest = YG2.saves;
        YGInsides.SetLoadSaves("STAGE2_AUTH_CLOUD_ERROR");
        Check(ReferenceEquals(YG2.saves, guest) && Probe.Log.SequenceEqual(new[] { "failed" }), "Failed sentinel must return before parse/default/profile replacement.");
        YG2.SaveProgress();
        Check(guest.idSave == 50 && !Probe.Log.Contains("saveCloud"), "A failed auth load must not write cloud or advance the profile save ID.");

        Reset(); guest = YG2.saves;
        YGInsides.SetLoadSaves("[\"broken-json\"]");
        Check(ReferenceEquals(YG2.saves, guest) && LeaderboardRoundService.Failed && !Probe.Log.Contains("default") && !Probe.Log.Contains("resolve"),
            "Malformed parsed cloud must stop at the new Broken guard, before default/recovery or resolving a guest migration.");

        Reset(); guest = YG2.saves;
        YGInsides.SetLoadSaves(InfoYG.NO_DATA);
        Check(ReferenceEquals(YG2.saves, guest) && guest.exp == 123 && guest.goldCoins == 45 && guest.equipedMaterial == 2 && guest.massiveOfObtaining[2] == 1,
            "Explicit empty cloud must carry the whole guest profile without resetting XP, wallet, or skins.");
        Check(Probe.Log.SequenceEqual(new[] { "resolve", "ready", "saveCloud", "data" }) && guest.idSave == 51,
            "The resolved profile must be assigned and marked ready before its sole cloud write, then emit generic profile data.");

        Reset(); guest = YG2.saves;
        YG2.infoYG.Storage.saveLocal = true;
        YG.Utils.LocalStorage.Values["YG2_SavesData"] = JsonSerializer.Serialize(guest, Json.Options);
        var account = new SavesYG { isFirst = false, exp = 9, goldCoins = 3, equipedMaterial = 1,
            massiveOfObtaining = new[] {1,1,0,0,0}, idSave = 2 };
        YGInsides.SetLoadSaves(Cloud(account));
        Check(!ReferenceEquals(YG2.saves, guest) && YG2.saves.exp == 9 && YG2.saves.goldCoins == 3 && YG2.saves.equipedMaterial == 1 && YG2.saves.massiveOfObtaining[2] == 0,
            "Existing cloud must win over a higher guest/local save ID without merging XP, wallet or skins.");
        Check(Probe.Log.SequenceEqual(new[] { "resolve", "ready", "data" }), "Existing account load must not save a guest profile over it.");

        Reset(); guest = YG2.saves;
        YG2.SaveProgress();
        Check(guest.idSave == 50 && Probe.Log.Count == 0, "An unresolved auth profile blocks every C# save route before save ID mutation.");

        Reset(); guest = YG2.saves;
        YGInsides.SetLoadSaves("STAGE2_AUTH_CLOUD_ERROR");
        guest.exp += 7; guest.goldCoins += 5; guest.massiveOfObtaining[3] = 1;
        YGInsides.SetLoadSaves(InfoYG.NO_DATA);
        Check(ReferenceEquals(YG2.saves, guest) && guest.exp == 130 && guest.goldCoins == 50 && guest.massiveOfObtaining[3] == 1,
            "Successful empty-cloud retry keeps guest changes made while the connection was blocked.");
        Check(!LeaderboardRoundService.IsAuthProfileWriteBlocked && Probe.Log.SequenceEqual(new[] { "failed", "resolve", "ready", "saveCloud", "data" }),
            "Only an actual successful profile load unblocks the subsequent guest migration write.");

        Reset(false); guest = YG2.saves;
        YGInsides.SetLoadSaves(InfoYG.NO_DATA);
        Check(!ReferenceEquals(YG2.saves, guest) && Probe.Log.Contains("default"), "Ordinary startup retains the plugin's existing no-cloud default behavior.");
        Console.WriteLine($"PASS: {checks} actual C# storage sentinel/broken/order/profile-preservation smoke checks");
    }
}

internal static class Probe { internal static readonly List<string> Log = new List<string>(); }
internal static class Json { internal static readonly JsonSerializerOptions Options = new JsonSerializerOptions { IncludeFields = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }; }
namespace UnityEngine
{
    public static class Time { public static float unscaledTime = 10; }
    public static class Debug { public static void LogError(string message) { } }
    public static class JsonUtility
    {
        public static T FromJson<T>(string value) => JsonSerializer.Deserialize<T>(value, Json.Options);
        public static string ToJson(object value, bool pretty = false) => JsonSerializer.Serialize(value, Json.Options);
    }
}
namespace YG
{
    public sealed class InitYGAttribute : Attribute { }
    public sealed class StartYGAttribute : Attribute { }
    public sealed class InfoYG
    {
        public const string NO_DATA = "no data";
        public readonly StorageSettings Storage = new StorageSettings();
        public sealed class StorageSettings { public bool saveCloud = true; public bool saveLocal; }
    }
    public sealed class PlatformStub
    {
        public void SaveCloud() { if (LeaderboardRoundService.IsAuthProfileWriteBlocked) throw new Exception("Save before readiness"); Probe.Log.Add("saveCloud"); }
        public void LoadCloud() { }
    }
    public static partial class YG2
    {
        private static bool _SDKEnabled = true;
        public static InfoYG infoYG = new InfoYG();
        public static PlatformStub iPlatform = new PlatformStub();
        public static void Message(string message) { }
        public static void GetDataInvoke() { Probe.Log.Add("data"); }
    }
}
namespace YG.Utils
{
    public static class LocalStorage
    {
        public static readonly Dictionary<string,string> Values = new Dictionary<string,string>();
        public static bool HasKey(string key) => Values.ContainsKey(key);
        public static string GetKey(string key) => Values[key];
        public static void SetKey(string key, string value) { Values[key] = value; }
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
namespace Vodovorot.Stage2
{
    public static class LeaderboardRoundService
    {
        public static bool Pending, Failed;
        public static SavesYG GuestProfile, ExpectedProfile;
        public static bool IsAuthProfileWriteBlocked => Pending || Failed;
        public static bool NotifyAuthCloudLoadFailed()
        {
            if (!Pending) return false;
            Failed = true; Probe.Log.Add("failed"); return true;
        }
        public static bool TryResolveAuthCloudProfile(SavesYG cloud, bool missing, bool broken, out SavesYG resolved, out bool carry)
        {
            resolved = null; carry = false;
            var choice = LeaderboardProfileLoadRules.Choose(Pending, missing, broken);
            if (choice == LeaderboardAuthProfileChoice.Ignore) return false;
            if (choice == LeaderboardAuthProfileChoice.RejectBrokenCloud) throw new Exception("Broken guard ran too late");
            Probe.Log.Add("resolve");
            carry = choice == LeaderboardAuthProfileChoice.GuestForEmptyAccount;
            resolved = carry ? GuestProfile : cloud;
            ExpectedProfile = resolved;
            return true;
        }
        public static void NotifyAuthCloudProfileReady()
        {
            if (!ReferenceEquals(YG2.saves, ExpectedProfile)) throw new Exception("Readiness signaled before account assignment");
            Pending = false; Failed = false; Probe.Log.Add("ready");
        }
    }
}
