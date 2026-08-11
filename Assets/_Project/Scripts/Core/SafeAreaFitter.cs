using UnityEngine;

namespace MartProg.Core
{
    /// <summary>
    /// Phase 20 on-device finding, not a Godot port. Keeps a RectTransform
    /// inside the device's actual safe area (Screen.safeArea) instead of the
    /// raw screen rect, so content anchored to a screen edge doesn't render
    /// underneath a phone's front-camera cutout, notch, or gesture-navigation
    /// bar.
    ///
    /// First observed need: AdminControlStrip's Export Log / Clear Log /
    /// Change Module row is anchored flush to the very top of the screen
    /// (Migration Spec §4, "top-anchored, Height 70"). On a real device with
    /// a front-camera cutout, that partially hides Clear Log behind it. A
    /// fixed pixel margin would only fix this for one specific phone's
    /// cutout size/position — Screen.safeArea reports the actual usable
    /// rectangle for whichever device is currently running, so this adapts
    /// automatically instead.
    ///
    /// Usage: do NOT attach this directly to a strip that already has its
    /// own fixed-height top/bottom anchor setup (e.g. AdminControlStrip
    /// itself) — this component sets its own RectTransform's anchors to
    /// match the safe area exactly, which would override that strip's
    /// existing Height-70 anchoring rather than cooperate with it. Instead,
    /// create an empty full-stretch wrapper GameObject (e.g. "SafeArea") as
    /// a direct child of Canvas, put THIS component on the wrapper, and
    /// re-parent whichever strip needs protecting (AdminControlStrip) to be
    /// a child of that wrapper. The strip's own anchor setup then applies
    /// relative to the safe area instead of the raw screen, automatically
    /// pushing it clear of any cutout without changing anything about how
    /// the strip itself is configured.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform rectTransform;
        private Rect lastSafeArea;
        private Vector2Int lastScreenSize;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            ApplySafeArea();
        }

        private void Update()
        {
            // Cheap guard, not a recompute every frame — only re-applies if
            // the safe area or screen size actually changed (device
            // rotation, split-screen resize, etc.).
            if (Screen.safeArea != lastSafeArea ||
                Screen.width != lastScreenSize.x ||
                Screen.height != lastScreenSize.y)
            {
                ApplySafeArea();
            }
        }

        private void ApplySafeArea()
        {
            Rect safeArea = Screen.safeArea;
            lastSafeArea = safeArea;
            lastScreenSize = new Vector2Int(Screen.width, Screen.height);

            if (Screen.width <= 0 || Screen.height <= 0) return;

            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;

            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
        }
    }
}
