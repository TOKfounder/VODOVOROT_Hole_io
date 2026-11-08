using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;

namespace Vodovorot.RoundExperience
{
    /// <summary>Keeps authored layout units equal to CSS pixels on a high-density Web canvas.</summary>
    [RequireComponent(typeof(CanvasScaler))]
    public sealed class CssCanvasScale : MonoBehaviour
    {
        private CanvasScaler scaler;
        private int width, height;
        private float nextCheck;
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern float LeaderboardStage2_CssRatio();
#endif

        private void Awake()
        {
            scaler = GetComponent<CanvasScaler>();
            Refresh();
        }

        private void LateUpdate()
        {
            if (width != Screen.width || height != Screen.height || Time.unscaledTime >= nextCheck)
                Refresh();
        }

        private void Refresh()
        {
            width = Screen.width;
            height = Screen.height;
            nextCheck = Time.unscaledTime + .25f;
            float ratio = 1f;
#if UNITY_WEBGL && !UNITY_EDITOR
            ratio = Mathf.Clamp(LeaderboardStage2_CssRatio(), .25f, 8f);
#endif
            if (!Mathf.Approximately(scaler.scaleFactor, ratio))
                scaler.scaleFactor = ratio;
        }
    }
}
