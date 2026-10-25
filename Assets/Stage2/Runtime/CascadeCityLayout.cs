using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Vodovorot.Stage2
{
    /// <summary>One small City experiment. Repositions existing objects, never instantiates props.</summary>
    public sealed class CascadeCityLayout : MonoBehaviour
    {
        public const string CitySceneName = "Low Poly City";
        public const int ExpectedPropCount = 16;
        private readonly Collider[] overlaps = new Collider[128];
        private readonly List<Placement> placements = new List<Placement>(ExpectedPropCount);
        private readonly HashSet<FallingObject> selected = new HashSet<FallingObject>();
        private readonly List<Snapshot> snapshots = new List<Snapshot>(ExpectedPropCount);
        private int originalMapValue;
        public bool Applied { get; private set; }
        public int ArrangedCount => snapshots.Count;
        public Vector3 SiteCenter { get; private set; }

        private sealed class Placement
        {
            public FallingObject item;
            public Vector3 position, floorPosition;
            public FallingObject[] supports;
        }

        private sealed class Snapshot
        {
            public FallingObject item;
            public int value;
            public Vector3 size, localScale;
            public Quaternion rotation;
            public Collider collider;
            public Vector3 colliderSize;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Subscribe()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            SceneManager.sceneLoaded += SceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitialScene() => Install(SceneManager.GetActiveScene());
        private static void SceneLoaded(Scene scene, LoadSceneMode mode) => Install(scene);

        private static void Install(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || scene.name != CitySceneName) return;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.GetComponentInChildren<CascadeCityLayout>(true) != null) return;
            var controller = new GameObject("City cascade compositions");
            SceneManager.MoveGameObjectToScene(controller, scene);
            controller.AddComponent<CascadeCityLayout>();
        }

        private IEnumerator Start()
        {
            // Every FallingObject.Start first computes its ORIGINAL value/dimensions. A new
            // position is then recorded for the existing reset, without recomputing either.
            yield return null;
            if (!TryArrange())
                Debug.LogWarning("[CascadeCity] No clear site/eligible original props; City was left unchanged.", this);
        }

        private bool TryArrange()
        {
            if (gameObject.scene.name != CitySceneName) return false;
            if (!CascadePhysics.ValidateConfiguration(out string physicsError))
            { Debug.LogError("[CascadeCity] " + physicsError, this); return false; }
            FallingObject[] objects = FindObjectsByType<FallingObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var cones = Select(objects, "Props_Traffic cone", 4, Vector3.zero);
            if (cones == null) return false;
            Vector3 anchor = Vector3.zero;
            float floor = 0f;
            foreach (FallingObject cone in cones) { anchor += cone.CascadeBounds.center; floor += cone.CascadeBounds.min.y; }
            anchor /= cones.Length;
            floor /= cones.Length;
            if (floor < -.1f || floor > .35f) return false;
            anchor.y = floor;

            var pots = Select(objects, "Natures_Pot Bush_small", 6, anchor);
            var rocks = Select(objects, "Natures_Rock_Big", 6, anchor);
            if (pots == null || rocks == null) return false;
            foreach (FallingObject item in cones) selected.Add(item);
            foreach (FallingObject item in pots) selected.Add(item);
            foreach (FallingObject item in rocks) selected.Add(item);
            if (selected.Count != ExpectedPropCount) return false;

            // All three compositions stay in the same small street patch, close to the
            // original cone lane. If occupied, try at most eight neighbouring patches.
            Vector2[] offsets = { Vector2.zero, new Vector2(4, 0), new Vector2(-4, 0),
                new Vector2(0, 4), new Vector2(0, -4), new Vector2(4, 4), new Vector2(-4, 4),
                new Vector2(4, -4), new Vector2(-4, -4) };
            bool clear = false;
            foreach (Vector2 offset in offsets)
            {
                SiteCenter = anchor + new Vector3(offset.x, 0, offset.y);
                BuildPlan(cones, pots, rocks, SiteCenter, floor);
                if (IsClear()) { clear = true; break; }
            }
            if (!clear) return false;

            originalMapValue = VodovorotGameManager.Instance != null && VodovorotGameManager.Instance.GamingManager != null
                ? VodovorotGameManager.Instance.GamingManager.AllValues : 0;
            foreach (Placement placement in placements)
            {
                FallingObject item = placement.item;
                snapshots.Add(new Snapshot { item = item, value = item.value, size = item.size,
                    localScale = item.transform.localScale, rotation = item.transform.rotation,
                    collider = item.GetComponent<Collider>(), colliderSize = item.CascadeBounds.size });
                CascadeProp member = item.gameObject.AddComponent<CascadeProp>();
                item.ArrangeCascade(placement.position, member);
            }
            Physics.SyncTransforms();
            foreach (Placement placement in placements)
                placement.item.GetComponent<CascadeProp>().Configure(placement.item, placement.supports, placement.floorPosition);
            Applied = true;
            if (!ValidateInitialState(out string error)) Debug.LogError("[CascadeCity] " + error, this);
            else Debug.Log("[CascadeCity] 3 compositions / 16 existing props. Map value, dimensions and colliders preserved. Site " + SiteCenter, this);
            return true;
        }

        private FallingObject[] Select(FallingObject[] objects, string prefix, int count, Vector3 anchor)
        {
            var candidates = new List<FallingObject>();
            foreach (FallingObject item in objects)
            {
                if (item.gameObject.scene != gameObject.scene || !item.CanArrangeCascade ||
                    !item.name.StartsWith(prefix, StringComparison.Ordinal)) continue;
                Bounds bounds = item.CascadeBounds;
                // Selection of already-existing small props is an authoring constraint only;
                // the game's score and CanFit thresholds are not changed.
                float widest = Mathf.Max(bounds.size.x, bounds.size.z);
                if (widest < .08f || widest > 1.4f || bounds.size.y < .05f || bounds.size.y > 1.4f ||
                    bounds.min.y < -.1f || bounds.min.y > .35f) continue;
                candidates.Add(item);
            }
            candidates.Sort((a, b) =>
            {
                int distance = (a.transform.position - anchor).sqrMagnitude.CompareTo((b.transform.position - anchor).sqrMagnitude);
                return distance != 0 ? distance : string.CompareOrdinal(a.name, b.name);
            });
            return candidates.Count < count ? null : candidates.GetRange(0, count).ToArray();
        }

        private void BuildPlan(FallingObject[] cones, FallingObject[] pots, FallingObject[] rocks, Vector3 center, float floor)
        {
            placements.Clear();
            float coneStep = MaxDepth(cones) + .15f;
            for (int i = 0; i < cones.Length; i++)
                Place(cones[i], center + new Vector3(-2.6f + (i % 2 == 0 ? -.2f : .2f), 0, (i - 1.5f) * coneStep), floor);

            float potStep = MaxWidth(pots) + .04f;
            float potHeight = MaxHeight(pots);
            Vector3 potCenter = center + new Vector3(1.2f, 0, -1.2f);
            for (int i = 0; i < 3; i++) Place(pots[i], potCenter + Vector3.right * ((i - 1) * potStep), floor);
            Place(pots[3], potCenter - Vector3.right * (potStep * .5f), floor + potHeight + .005f, pots[0], pots[1]);
            Place(pots[4], potCenter + Vector3.right * (potStep * .5f), floor + potHeight + .005f, pots[1], pots[2]);
            Place(pots[5], potCenter, floor + potHeight * 2f + .01f, pots[3], pots[4]);

            float rockStep = MaxWidth(rocks) + .15f;
            Vector3 rockCenter = center + new Vector3(1.2f, 0, 1.6f);
            for (int i = 0; i < 3; i++)
            {
                Vector3 spot = rockCenter + Vector3.right * ((i - 1) * rockStep);
                Place(rocks[i * 2], spot, floor);
                Place(rocks[i * 2 + 1], spot, floor + rocks[i * 2].CascadeBounds.size.y + .005f, rocks[i * 2]);
            }
        }

        private void Place(FallingObject item, Vector3 spot, float bottom, params FallingObject[] supports)
        {
            Bounds bounds = item.CascadeBounds;
            Vector3 position = item.transform.position + new Vector3(spot.x - bounds.center.x, bottom - bounds.min.y, spot.z - bounds.center.z);
            Vector3 floorPosition = position + Vector3.down * (bottom - SiteCenter.y);
            placements.Add(new Placement { item = item, position = position, floorPosition = floorPosition, supports = supports });
        }

        private bool IsClear()
        {
            foreach (Placement placement in placements)
            {
                Bounds bounds = placement.item.CascadeBounds;
                bounds.center += placement.position - placement.item.transform.position;
                // Touching the actual street is fine. Everything above it must be clear.
                Vector3 extents = bounds.extents;
                extents.y = Mathf.Max(.005f, extents.y - .008f);
                int count = Physics.OverlapBoxNonAlloc(bounds.center + Vector3.up * .008f, extents, overlaps,
                    Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                if (count == overlaps.Length) return false;
                for (int i = 0; i < count; i++)
                {
                    Collider other = overlaps[i];
                    if (other == null || other.GetComponentInParent<HoleParent>() != null) continue;
                    FallingObject owner = other.GetComponentInParent<FallingObject>();
                    if (owner != null && selected.Contains(owner)) continue;
                    if (other.bounds.max.y <= SiteCenter.y + .025f) continue;
                    return false;
                }
            }
            return true;
        }

        // Safe to call immediately after setup; no simulated playtest is implied by this check.
        public bool ValidateInitialState(out string error)
        {
            error = null;
            if (!Applied || snapshots.Count != ExpectedPropCount) { error = "Expected exactly 16 existing City props."; return false; }
            foreach (Snapshot snapshot in snapshots)
            {
                FallingObject item = snapshot.item;
                if (item == null || item.value != snapshot.value || item.size != snapshot.size ||
                    item.transform.localScale != snapshot.localScale || Quaternion.Angle(item.transform.rotation, snapshot.rotation) > .001f ||
                    item.GetComponent<Collider>() != snapshot.collider || (item.CascadeBounds.size - snapshot.colliderSize).sqrMagnitude > .000001f)
                { error = "A prop's value, cached dimensions, rotation, scale or collider changed."; return false; }
                if (item.gameObject.layer != CascadePhysics.Layer)
                { error = "An arranged prop is missing its isolated collision layer."; return false; }
                foreach (Collider owned in item.GetComponentsInChildren<Collider>(true))
                    if (owned.GetComponentInParent<FallingObject>() == item && owned.gameObject.layer != CascadePhysics.Layer)
                    { error = "An owned collider is missing its isolated collision layer."; return false; }
            }
            GamingManager game = VodovorotGameManager.Instance != null ? VodovorotGameManager.Instance.GamingManager : null;
            if (game != null && game.AllValues != originalMapValue) { error = "The map total value changed."; return false; }
            return true;
        }

        private static float MaxWidth(FallingObject[] items)
        { float result = 0; foreach (FallingObject item in items) result = Mathf.Max(result, item.CascadeBounds.size.x); return result; }
        private static float MaxDepth(FallingObject[] items)
        { float result = 0; foreach (FallingObject item in items) result = Mathf.Max(result, item.CascadeBounds.size.z); return result; }
        private static float MaxHeight(FallingObject[] items)
        { float result = 0; foreach (FallingObject item in items) result = Mathf.Max(result, item.CascadeBounds.size.y); return result; }
    }
}
