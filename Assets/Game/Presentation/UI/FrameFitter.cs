using UnityEngine;
using UnityEngine.UI;

namespace Tabletop.Presentation
{
    /// <summary>
    /// Keeps the whole 1920x1080 interface frame on screen whatever the screen shape (D-029). Widescreen and
    /// wider (PC, iPhone) scale by height and get side margins; narrower screens (iPad 4:3, a non-16:9 window)
    /// scale by width and get margins above and below, so corner controls are never cut off.
    /// </summary>
    [RequireComponent(typeof(CanvasScaler))]
    public sealed class FrameFitter : MonoBehaviour
    {
        public const float FrameAspect = 16f / 9f;
        private CanvasScaler _scaler;

        /// <summary>0 = match width, 1 = match height (CanvasScaler.matchWidthOrHeight).</summary>
        public static float MatchFor(float screenAspect) => screenAspect >= FrameAspect - 0.001f ? 1f : 0f;

        private void Awake()
        {
            _scaler = GetComponent<CanvasScaler>();
            Apply();
        }

        private void Update() => Apply();

        private void Apply()
        {
            if (Screen.height <= 0) return;
            float match = MatchFor((float)Screen.width / Screen.height);
            if (!Mathf.Approximately(_scaler.matchWidthOrHeight, match)) _scaler.matchWidthOrHeight = match;
        }
    }
}
