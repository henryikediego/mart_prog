using UnityEngine;

namespace Loci.Core
{
    /// <summary>
    /// Shared visual tokens from the Loci redesign handoff.
    /// Design-pixel values are kept as source tokens and are not converted to Canvas units here.
    /// </summary>
    [CreateAssetMenu(fileName = "LociTheme", menuName = "Loci/Theme")]
    public sealed class LociTheme : ScriptableObject
    {
        [Header("Colors")]
        public Color32 primary = new Color32(91, 75, 255, 255); // #5B4BFF
        public Color32 pageBackground = new Color32(244, 245, 249, 255); // #F4F5F9
        public Color32 primaryText = new Color32(18, 20, 43, 255); // #12142B
        public Color32 secondaryText = new Color32(91, 96, 122, 255); // #5B607A
        public Color32 buttonBorder = new Color32(211, 214, 228, 255); // #D3D6E4
        public Color32 cardBorder = new Color32(227, 229, 238, 255); // #E3E5EE
        public Color32 disabledFill = new Color32(211, 214, 228, 255); // #D3D6E4
        public Color32 disabledText = new Color32(138, 143, 168, 255); // #8A8FA8
        public Color32 errorTextAndBorder = new Color32(179, 38, 30, 255); // #B3261E
        public Color32 errorIconBackground = new Color32(252, 232, 230, 255); // #FCE8E6
        public Color32 success = new Color32(35, 194, 122, 255); // #23C27A
        public Color32 studyModeAndNoteSurface = new Color32(255, 244, 214, 255); // #FFF4D6

        [Header("Typography")]
        public string bodyFontFamily = "Inter";
        [Range(400, 700)] public int bodyFontMinimumWeight = 400;
        [Range(400, 700)] public int bodyFontMaximumWeight = 700;
        public string codeFontFamily = "JetBrains Mono";
        [Range(100, 900)] public int codeFontMinimumWeight = 500;
        [Range(100, 900)] public int codeFontMaximumWeight = 600;

        [Header("Shape and Layout")]
        [Min(0)] public float buttonHeightDesignPixels = 56;
        [Min(0)] public float buttonCornerRadiusDesignPixels = 16;
        public Vector2 canvasReferenceResolution = new Vector2(810, 1800);
    }
}
