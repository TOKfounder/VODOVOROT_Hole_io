using UnityEngine;

namespace Vodovorot.RoundExperience
{
    /// <summary>Moves a small authored marker pool onto nearby targets that really fit.</summary>
    public sealed class EdibleTargetHints : MonoBehaviour
    {
        [SerializeField] private BlackHoleController player;
        [SerializeField] private GamingManager game;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private RoundExperienceSettings settings;
        [SerializeField] private Transform[] markers;
        [SerializeField] private LayerMask targetLayers = (1 << 7) | (1 << 30);
        private readonly Collider[] overlaps = new Collider[256];
        private FallingObject[] targets;
        private float[] distances;
        private float nextRefresh;
        private float radius = 5.5f;

        public float Radius
        {
            get => radius; set => radius = Mathf.Clamp(value, 2f, 10f);
        }
        public int VisibleCount
        {
            get; private set;
        }

        private void Awake()
        {
            targets = new FallingObject[markers.Length];
            distances = new float[markers.Length];
            if (settings != null)
                Radius = settings.CreateRuntimeValues().TargetHintRadius;
        }

        private void LateUpdate()
        {
            if (player == null || game == null || viewCamera == null || Time.timeScale == 0f || game.HasRoundEnded)
            {
                HideAll();
                return;
            }
            if (Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + .15f;
                SelectTargets();
            }
            VisibleCount = 0;
            for (int i = 0; i < markers.Length; i++)
            {
                FallingObject item = targets[i];
                bool visible = Eligible(item);
                markers[i].gameObject.SetActive(visible);
                if (!visible)
                    continue;
                Bounds bounds = item.rend.bounds;
                markers[i].position = new Vector3(bounds.center.x, bounds.min.y + .025f, bounds.center.z);
                markers[i].localScale = new Vector3(Mathf.Clamp(bounds.size.x + .24f, .4f, 6f), 1f,
                    Mathf.Clamp(bounds.size.z + .24f, .4f, 6f));
                VisibleCount++;
            }
        }

        private void SelectTargets()
        {
            for (int i = 0; i < targets.Length; i++)
            {
                targets[i] = null;
                distances[i] = float.PositiveInfinity;
            }
            int count = Physics.OverlapSphereNonAlloc(player.transform.position, radius, overlaps, targetLayers,
                QueryTriggerInteraction.Ignore);
            for (int index = 0; index < count; index++)
            {
                FallingObject item = overlaps[index].GetComponentInParent<FallingObject>();
                if (!Eligible(item))
                    continue;
                bool duplicate = false;
                for (int i = 0; i < targets.Length; i++)
                    duplicate |= targets[i] == item;
                if (duplicate)
                    continue;
                Vector3 delta = item.rend.bounds.center - player.transform.position;
                delta.y = 0;
                float distance = delta.sqrMagnitude;
                for (int slot = 0; slot < targets.Length; slot++)
                {
                    if (distance >= distances[slot])
                        continue;
                    for (int i = targets.Length - 1; i > slot; i--)
                    {
                        targets[i] = targets[i - 1];
                        distances[i] = distances[i - 1];
                    }
                    targets[slot] = item;
                    distances[slot] = distance;
                    break;
                }
            }
        }

        private bool Eligible(FallingObject item)
        {
            if (item == null || !item.IsAvailableTarget || !item.CanBeEatenBy(player) || item.rend.forceRenderingOff)
                return false;
            Vector3 point = viewCamera.WorldToViewportPoint(item.rend.bounds.center);
            return point.z > 0 && point.x > .03f && point.x < .97f && point.y > .08f && point.y < .88f;
        }

        private void HideAll()
        {
            VisibleCount = 0;
            if (markers == null)
                return;
            foreach (Transform marker in markers)
                if (marker != null)
                    marker.gameObject.SetActive(false);
        }

        private void OnDisable() => HideAll();
    }
}
