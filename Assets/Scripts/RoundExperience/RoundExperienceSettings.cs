using UnityEngine;
using System;

namespace Vodovorot.RoundExperience
{
    [CreateAssetMenu(menuName = "Водоворот/Параметры раунда и прогресса")]
    public sealed class RoundExperienceSettings : ScriptableObject
    {
        [SerializeField, Min(1)] private int experiencePerLevel = 100;
        [SerializeField, Min(1)] private float roundSeconds = 180f;
        [SerializeField, Min(0)] private int minimumCompletedExperience = 5;
        [SerializeField, Min(0)] private int minimumCompletedCoins = 1;
        [SerializeField, Range(0f, 1f)] private float cutawayReleaseSeconds = 0.15f;
        [SerializeField, Range(.03f, 1f)] private float cutawayFadeOutSeconds = .14f;
        [SerializeField, Range(.03f, 1f)] private float cutawayFadeInSeconds = .35f;
        [SerializeField, Range(2f, 10f)] private float targetHintRadius = 5.5f;
        [SerializeField, Range(.5f, 3f)] private float blockedTargetHintSeconds = 1.2f;
        [SerializeField, Range(.35f, .8f)] private float secondaryGoalRevealFraction = .5f;
        [SerializeField] private RewardedOfferDefinition[] rewardedOffers =
        {
            new RewardedOfferDefinition("couple", 1, 5),
            new RewardedOfferDefinition("hand", 2, 20),
            new RewardedOfferDefinition("bag", 5, 100),
            new RewardedOfferDefinition("box", 10, 300)
        };

        public RoundExperienceValues CreateRuntimeValues()
        {
            return new RoundExperienceValues
            {
                ExperiencePerLevel = Mathf.Max(1, experiencePerLevel),
                RoundSeconds = Mathf.Max(1f, roundSeconds),
                MinimumCompletedExperience = Mathf.Max(0, minimumCompletedExperience),
                MinimumCompletedCoins = Mathf.Max(0, minimumCompletedCoins),
                CutawayReleaseSeconds = Mathf.Clamp01(cutawayReleaseSeconds),
                CutawayFadeOutSeconds = Mathf.Clamp(cutawayFadeOutSeconds, .03f, 1f),
                CutawayFadeInSeconds = Mathf.Clamp(cutawayFadeInSeconds, .03f, 1f),
                TargetHintRadius = Mathf.Clamp(targetHintRadius, 2f, 10f),
                BlockedTargetHintSeconds = Mathf.Clamp(blockedTargetHintSeconds, .5f, 3f),
                SecondaryGoalRevealFraction = Mathf.Clamp(secondaryGoalRevealFraction, .35f, .8f),
                RewardedOffers = Array.ConvertAll(rewardedOffers, offer => new RewardedOfferValues
                {
                    Id = offer.Id,
                    RequiredAds = Mathf.Max(1, offer.RequiredAds),
                    Diamonds = Mathf.Max(0, offer.Diamonds)
                })
            };
        }
    }

    [Serializable]
    public sealed class RewardedOfferDefinition
    {
        [SerializeField] private string id;
        [SerializeField, Min(1)] private int requiredAds;
        [SerializeField, Min(0)] private int diamonds;
        public string Id => id;
        public int RequiredAds => requiredAds;
        public int Diamonds => diamonds;

        public RewardedOfferDefinition(string id, int requiredAds, int diamonds)
        {
            this.id = id;
            this.requiredAds = requiredAds;
            this.diamonds = diamonds;
        }
    }

    public struct RewardedOfferValues
    {
        public string Id;
        public int RequiredAds;
        public int Diamonds;
    }

    public struct RoundExperienceValues
    {
        public int ExperiencePerLevel;
        public float RoundSeconds;
        public int MinimumCompletedExperience;
        public int MinimumCompletedCoins;
        public float CutawayReleaseSeconds;
        public float CutawayFadeOutSeconds;
        public float CutawayFadeInSeconds;
        public float TargetHintRadius;
        public float BlockedTargetHintSeconds;
        public float SecondaryGoalRevealFraction;
        public RewardedOfferValues[] RewardedOffers;

        public bool TryGetRewardedOffer(string id, out RewardedOfferValues offer)
        {
            if (RewardedOffers != null)
                foreach (RewardedOfferValues candidate in RewardedOffers)
                    if (candidate.Id == id)
                    {
                        offer = candidate;
                        return true;
                    }
            offer = default;
            return false;
        }
    }
}
