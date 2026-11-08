using System;
using System.IO;
using UnityEngine;
using YG;
using Vodovorot.QualityPass;

public static class CheckPurchaseDelivery
{
    public static string Run()
    {
        int checks = 0;
        Action<bool, string> check = (ok, name) => {
            if (!ok) throw new InvalidOperationException(name);
            checks++;
        };
        var legacy = JsonUtility.FromJson<SavesYG>("{\"diamonds\":3,\"massiveOfObtaining\":[1,0,0,0,0]}");
        check(PurchaseDeliveryRules.TryApply(legacy, "hand", "test-order-1"), "legacy save accepts receipt");
        check(legacy.diamonds == 23, "original grant20");
        check(PurchaseDeliveryRules.TryApply(legacy, "hand", "test-order-1") && legacy.diamonds == 23, "duplicate callback once");
        var restored = JsonUtility.FromJson<SavesYG>(JsonUtility.ToJson(legacy));
        check(PurchaseDeliveryRules.TryApply(restored, "hand", "test-order-1") && restored.diamonds == 23, "reload/consume failure once");
        check(PurchaseDeliveryRules.TryApply(restored, "hand", "test-order-2") && restored.diamonds == 43, "separate orders sameSKU");
        check(!PurchaseDeliveryRules.TryApply(restored, "vortex_no_ads", "other-order") && restored.diamonds == 43, "other draft SKU untouched");
        check(!PurchaseDeliveryRules.TryApply(restored, "gold", "") && restored.massiveOfObtaining[2] == 0, "missing receipt rejected");
        check(PurchaseDeliveryRules.TryApply(restored, "gold", "test-skin-1") && restored.massiveOfObtaining[2] == 1, "skin grant");
        check(PurchaseDeliveryRules.TryApply(restored, "gold", "test-skin-1") && restored.fulfilledPurchaseReceipts.Count == 3, "skin duplicate once");
        check(!JsonUtility.ToJson(restored).Contains("test-order"), "raw platform receipt not persisted");
        string[] products = { "hand", "bag", "box", "chest", "gold", "scrag", "lord" };
        int[] diamonds = { 20, 100, 300, 600, 0, 0, 0 };
        for (int index = 0; index < products.Length; index++)
        {
            var profile = new SavesYG { diamonds = 0 };
            check(PurchaseDeliveryRules.TryApply(profile, products[index], "test-" + index), "supported SKU grant");
            check(profile.diamonds == diamonds[index], "unchanged diamond grant");
            if (index >= 4) check(profile.massiveOfObtaining[index - 2] == 1, "correct skin index");
            check(PurchaseDeliveryRules.TryApply(profile, products[index], "test-" + index) &&
                profile.fulfilledPurchaseReceipts.Count == 1, "all SKU duplicates once");
        }
        string report = JsonUtility.ToJson(new Result { checks = checks, passed = true }, true);
        File.WriteAllText("Docs/QualityPass-20261108/Checks/purchase-delivery-csharp.json", report);
        return report;
    }
    [Serializable] private class Result { public int checks; public bool passed; }
}
