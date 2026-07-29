using UnityEngine;

// Direct port of app_state.gd.
// Same singleton pattern as DashboardController — reach it anywhere
// via AppState.Instance.StudentId, etc.
public class AppState : MonoBehaviour
{
    public static AppState Instance { get; private set; }

    [Header("Matches the three vars in app_state.gd")]
    public string StudentId = "TEST_STUDENT";
    public string Language = "Python";
    public string CurrentModule = "Arrays";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }
}
