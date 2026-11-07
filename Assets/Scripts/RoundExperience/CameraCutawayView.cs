using UnityEngine;

namespace Vodovorot.RoundExperience
{
    [DefaultExecutionOrder(100)]
    public sealed class CameraCutawayView : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Transform target;
        [SerializeField] private RoundExperienceSettings settings;
        [SerializeField] private GamingManager game;
        [SerializeField] private bool keepViewInsideMap = true;
        [SerializeField] private Renderer[] occluders = new Renderer[0];
        [SerializeField] private Renderer[] playerVisuals = new Renderer[0];
        private bool[] originalVisibility;
        private float[] restoreAfter;
        private float releaseSeconds;
        private bool originalOcclusionCulling;
        private Vector3 authoredLocalPosition;
        private bool haveCameraPosition;

        private void Start()
        {
            // HoleParent applies the authored mobile offset in Start first.
            if (viewCamera == null) return;
            authoredLocalPosition = viewCamera.transform.localPosition;
            haveCameraPosition = true;
        }

        public float ReleaseSeconds
        {
            get => releaseSeconds;
            set => releaseSeconds = Mathf.Clamp01(value);
        }

        private void Awake()
        {
            releaseSeconds = settings.CreateRuntimeValues().CutawayReleaseSeconds;
        }

        private void OnEnable()
        {
            if (viewCamera != null) originalOcclusionCulling = viewCamera.useOcclusionCulling;
            originalVisibility = new bool[occluders.Length];
            restoreAfter = new float[occluders.Length];
            for (int i = 0; i < occluders.Length; i++)
                if (occluders[i] != null) originalVisibility[i] = occluders[i].forceRenderingOff;
        }

        private void LateUpdate()
        {
            if (viewCamera == null || target == null) return;
            KeepGroundInView();
            Vector3 origin = viewCamera.transform.position;
            Bounds playerBounds = new Bounds(target.position, Vector3.zero);
            foreach (Renderer visual in playerVisuals)
                if (visual != null && visual.enabled && visual.gameObject.activeInHierarchy)
                    playerBounds.Encapsulate(visual.bounds);
            Vector3 direction = playerBounds.center - origin;
            float distance = direction.magnitude;
            if (distance <= Mathf.Epsilon) return;
            Ray sight = new Ray(origin, direction / distance);
            bool anyHidden = false;

            for (int i = 0; i < occluders.Length; i++)
            {
                Renderer obstacle = occluders[i];
                if (obstacle == null || originalVisibility[i]) continue;
                Bounds bounds = obstacle.bounds;
                // Sweep the visible character volume, not just a point at its
                // feet: a wall can cover the rim while missing that single ray.
                bounds.Expand(playerBounds.size);
                bool obstructed = bounds.Contains(origin) ||
                    (bounds.IntersectRay(sight, out float hitDistance) && hitDistance < distance);
                if (obstructed) restoreAfter[i] = Time.unscaledTime + releaseSeconds;
                obstacle.forceRenderingOff = obstructed || Time.unscaledTime < restoreAfter[i];
                anyHidden |= obstacle.forceRenderingOff;
            }
            // A hidden static occluder still participates in Unity's baked occlusion.
            // Otherwise the camera reveals its clear color instead of the world behind it.
            viewCamera.useOcclusionCulling = originalOcclusionCulling && !anyHidden;
        }

        private void KeepGroundInView()
        {
            if (!keepViewInsideMap || !haveCameraPosition || game == null) return;
            Transform cameraTransform = viewCamera.transform;
            cameraTransform.localPosition = authoredLocalPosition;
            Plane ground = new Plane(Vector3.up, target.position);
            float left = float.PositiveInfinity, right = float.NegativeInfinity;
            float bottom = float.PositiveInfinity, top = float.NegativeInfinity;
            for (int corner = 0; corner < 4; corner++)
            {
                Ray ray = viewCamera.ViewportPointToRay(new Vector3(corner % 2, corner / 2, 0));
                if (!ground.Raycast(ray, out float distance)) return;
                Vector3 point = ray.GetPoint(distance);
                left = Mathf.Min(left, point.x);
                right = Mathf.Max(right, point.x);
                bottom = Mathf.Min(bottom, point.z);
                top = Mathf.Max(top, point.z);
            }
            Vector3 shift = new Vector3(
                // Keep a small border visible so a player touching a wall is
                // still inside the frame instead of being clipped by its edge.
                InwardShift(left, right, game.minX - 2f, game.maxX + 2f), 0,
                InwardShift(bottom, top, game.minZ - 2f, game.maxZ + 2f));
            Vector3 originalPosition = cameraTransform.position;
            cameraTransform.position = originalPosition + shift;
            if (TargetFitsFrame()) return;
            // The ground footprint is a trapezoid. At a corner, clamping its
            // widest edge can push the player outside its narrower near edge.
            // Apply as much correction as possible while retaining the player.
            float low = 0f, high = 1f;
            for (int step = 0; step < 10; step++)
            {
                float amount = (low + high) * .5f;
                cameraTransform.position = originalPosition + shift * amount;
                if (TargetFitsFrame()) low = amount;
                else high = amount;
            }
            cameraTransform.position = originalPosition + shift * low;
        }

        private bool TargetFitsFrame()
        {
            Vector3 point = viewCamera.WorldToViewportPoint(target.position);
            return point.z > 0f && point.x >= .3f && point.x <= .7f && point.y >= .3f && point.y <= .7f;
        }

        private static float InwardShift(float low, float high, float minimum, float maximum)
        {
            if (maximum <= minimum) return 0;
            if (high - low >= maximum - minimum) return (minimum + maximum - low - high) * .5f;
            return Mathf.Clamp(0, minimum - low, maximum - high);
        }

        private void OnDisable()
        {
            if (viewCamera != null) viewCamera.useOcclusionCulling = originalOcclusionCulling;
            if (viewCamera != null && haveCameraPosition) viewCamera.transform.localPosition = authoredLocalPosition;
            if (originalVisibility == null) return;
            for (int i = 0; i < occluders.Length; i++)
                if (occluders[i] != null) occluders[i].forceRenderingOff = originalVisibility[i];
        }
    }
}
