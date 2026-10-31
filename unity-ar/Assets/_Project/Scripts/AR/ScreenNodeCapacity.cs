using UnityEngine;

namespace Loci.AR
{
    /// <summary>Computes a shared readable node limit from the display's usable short side.</summary>
    public static class ScreenNodeCapacity
    {
        public const float ReferenceShortSideMillimeters = 66.7f;
        public const int ReferenceCapacity = 5;
        public const int FallbackCapacity = 5;
        public const int MinimumCapacity = 3;

        public static int ForCurrentScreen()
        {
            Rect safeArea = Screen.safeArea;
            float dpi = Screen.dpi;
            if (dpi <= 1f || safeArea.width <= 0f || safeArea.height <= 0f)
                return FallbackCapacity;

            float usableShortSideMillimeters = Mathf.Min(safeArea.width, safeArea.height) / dpi * 25.4f;
            int capacity = Mathf.RoundToInt(
                usableShortSideMillimeters / ReferenceShortSideMillimeters * ReferenceCapacity);
            return Mathf.Max(MinimumCapacity, capacity);
        }
    }
}
