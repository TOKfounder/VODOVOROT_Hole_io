using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using YG;

namespace Vodovorot.QualityPass
{
    public static class PurchaseDeliveryRules
    {
        public static bool IsSupported(string id)
        {
            return id == "hand" || id == "bag" || id == "box" || id == "chest" ||
                id == "gold" || id == "scrag" || id == "lord";
        }

        // The receipt and its grant share the same save. A failed cloud write leaves
        // the platform order pending; replaying it never awards the loaded profile twice.
        public static bool TryApply(SavesYG saves, string id, string token)
        {
            if (saves == null || !IsSupported(id) || string.IsNullOrEmpty(token)) return false;
            string receipt;
            using (SHA256 sha = SHA256.Create())
                receipt = Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(token)));
            if (saves.fulfilledPurchaseReceipts == null)
                saves.fulfilledPurchaseReceipts = new List<string>();
            if (saves.fulfilledPurchaseReceipts.Contains(receipt)) return true;
            if (saves.massiveOfObtaining == null || saves.massiveOfObtaining.Length < 5) return false;
            switch (id)
            {
                case "hand": saves.diamonds += 20; break;
                case "bag": saves.diamonds += 100; break;
                case "box": saves.diamonds += 300; break;
                case "chest": saves.diamonds += 600; break;
                case "gold": saves.massiveOfObtaining[2] = 1; break;
                case "scrag": saves.massiveOfObtaining[3] = 1; break;
                case "lord": saves.massiveOfObtaining[4] = 1; break;
            }
            saves.fulfilledPurchaseReceipts.Add(receipt);
            return true;
        }
    }
}

namespace YG
{
    public partial class SavesYG
    {
        public List<string> fulfilledPurchaseReceipts = new List<string>();
    }
}
