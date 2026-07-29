using UnityEngine;

// Direct port of responsive_manager.gd.
public class ResponsiveManager : MonoBehaviour
{
    public static ResponsiveManager Instance { get; private set; }

    public enum MartLayoutProfile
    {
        Desktop,
        Tablet,
        MobilePortrait,
        MobileLandscape
    }

    [Header("Testing Overrides — same two @export bools from Godot")]
    [SerializeField] private bool forceMobileLayoutForTesting = true;
    [SerializeField] private bool forceDesktopLayoutForTesting = false;

    public MartLayoutProfile CurrentProfile { get; private set; } = MartLayoutProfile.Desktop;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        UpdateLayoutProfile();

        // CurrentProfile is a C# property, not a field, so it won't
        // show in the Inspector. Logging it here to the Console is
        // the simplest way to confirm what was actually detected.
        PrintLayoutDebug();
    }

    // Godot: func determine_layout_profile() -> int
    public MartLayoutProfile DetermineLayoutProfile()
    {
        if (forceDesktopLayoutForTesting)
        {
            return MartLayoutProfile.Desktop;
        }

        if (forceMobileLayoutForTesting)
        {
            return MartLayoutProfile.MobilePortrait;
        }

        // Godot: OS.has_feature("mobile"/"android"/"ios")
        // Unity: Application.isMobilePlatform covers Android + iOS together.
        bool runningOnMobile = Application.isMobilePlatform;

        if (runningOnMobile)
        {
            if (Screen.width > Screen.height)
            {
                return MartLayoutProfile.MobileLandscape;
            }
            return MartLayoutProfile.MobilePortrait;
        }

        return MartLayoutProfile.Desktop;
    }

    // Godot: func update_layout_profile() -> void
    public void UpdateLayoutProfile()
    {
        CurrentProfile = DetermineLayoutProfile();
    }

    // Godot: func get_device_type() -> String
    public string GetDeviceType()
    {
        if (Application.platform == RuntimePlatform.Android)
        {
            return "ANDROID";
        }

        if (Application.platform == RuntimePlatform.IPhonePlayer)
        {
            return "IOS";
        }

        if (Application.isMobilePlatform)
        {
            return "MOBILE";
        }

        return "DESKTOP";
    }

    // Godot: func get_layout_profile_name() -> String
    public string GetLayoutProfileName()
    {
        switch (CurrentProfile)
        {
            case MartLayoutProfile.Desktop:
                return "DESKTOP";
            case MartLayoutProfile.Tablet:
                return "TABLET";
            case MartLayoutProfile.MobilePortrait:
                return "MOBILE PORTRAIT";
            case MartLayoutProfile.MobileLandscape:
                return "MOBILE LANDSCAPE";
            default:
                return "UNKNOWN";
        }
    }

    // ----- Helper functions (direct ports) -----

    public bool IsDesktop() => CurrentProfile == MartLayoutProfile.Desktop;

    public bool IsTablet() => CurrentProfile == MartLayoutProfile.Tablet;

    public bool IsMobilePortrait() => CurrentProfile == MartLayoutProfile.MobilePortrait;

    public bool IsMobileLandscape() => CurrentProfile == MartLayoutProfile.MobileLandscape;

    public bool IsMobile() =>
        CurrentProfile == MartLayoutProfile.MobilePortrait
        || CurrentProfile == MartLayoutProfile.MobileLandscape;

    // ----- UI helpers (direct ports) -----

    // Godot: func get_button_columns() -> int
    public int GetButtonColumns()
    {
        switch (CurrentProfile)
        {
            case MartLayoutProfile.Desktop:
                return 6;
            case MartLayoutProfile.Tablet:
                return 4;
            case MartLayoutProfile.MobileLandscape:
                return 3;
            case MartLayoutProfile.MobilePortrait:
                return 2;
            default:
                return 6;
        }
    }

    // Godot: func get_visual_scale() -> float
    public float GetVisualScale()
    {
        switch (CurrentProfile)
        {
            case MartLayoutProfile.Desktop:
                return 1.20f;
            case MartLayoutProfile.Tablet:
                return 1.00f;
            case MartLayoutProfile.MobileLandscape:
                return 0.90f;
            case MartLayoutProfile.MobilePortrait:
                return 0.80f;
            default:
                return 1.00f;
        }
    }

    // ----- Dashboard behaviour (direct ports) -----

    public bool UseScrollableDashboard() => IsMobile();

    public bool UseCompactDashboard() => IsMobile();

    public bool UseSideBySideLayout() => IsDesktop();

    public bool UseLargeVisuals() => IsDesktop();

    public bool UseCompactButtons() => IsMobile();

    // ----- Debug helpers (direct ports, print -> Debug.Log) -----

    public void PrintDashboardProfile()
    {
        Debug.Log("----- DASHBOARD PROFILE -----");
        Debug.Log("Layout: " + GetLayoutProfileName());
        Debug.Log("Scrollable Dashboard: " + UseScrollableDashboard());
        Debug.Log("Compact Dashboard: " + UseCompactDashboard());
        Debug.Log("Side By Side Layout: " + UseSideBySideLayout());
        Debug.Log("Large Visuals: " + UseLargeVisuals());
        Debug.Log("Compact Buttons: " + UseCompactButtons());
        Debug.Log("-----------------------------");
    }

    public void PrintLayoutDebug()
    {
        Debug.Log("----------------------------------");
        Debug.Log("Device Type: " + GetDeviceType());
        Debug.Log("Layout Profile: " + GetLayoutProfileName());
        Debug.Log("Viewport: " + Screen.width + " x " + Screen.height);
        Debug.Log("----------------------------------");
    }
}
