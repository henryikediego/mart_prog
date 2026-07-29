using UnityEngine;
using UnityEngine.UI;

// Direct port of dashboard_container.gd.
// Attach this to DashboardRoot (or Canvas — anywhere that runs once at
// scene start). It wires the six InformationPanel instances into
// DashboardController and sets their initial default text, exactly
// like the _ready() function did in Godot.
public class DashboardContainer : MonoBehaviour
{
    [Header("Drag the six InformationPanel instances here, in this order")]
    [SerializeField] private InformationPanel summaryPanel;
    [SerializeField] private InformationPanel instructionPanel;
    [SerializeField] private InformationPanel operationPanel;
    [SerializeField] private InformationPanel explanationPanel;
    [SerializeField] private InformationPanel statusPanel;
    [SerializeField] private InformationPanel progressPanel;

    [Header("The container whose layout needs a forced refresh on start")]
    [SerializeField] private RectTransform dashboardRoot;

    [Header("Optional: assign this once scrolling is added, to guarantee the dashboard starts scrolled to the top")]
    [SerializeField] private ScrollRect dashboardScrollRect;

    [Header("Optional: the Viewport GameObject itself, toggled off/on after rebuild to force its Mask to fully re-register")]
    [SerializeField] private GameObject viewportObject;

    private void Start()
    {
        // ----- Fix for the Play-mode overlap issue -----
        // Vertical Layout Group + Content Size Fitter, working together,
        // don't always finish recalculating sizes/positions in the very
        // first frame(s) after the scene starts. Forcing one explicit
        // rebuild here guarantees every card is correctly sized and
        // stacked before anything else runs. This is a standard, common
        // fix for this exact class of Unity UI timing issue.
        Canvas.ForceUpdateCanvases();
        if (dashboardRoot != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(dashboardRoot);
        }

        // The Mask on Viewport locks in its clip rectangle during
        // OnEnable, which runs before this Start() and the rebuild
        // above. Toggling the whole GameObject off and back on forces
        // a full reset (Mask + Canvas Renderer together) so it
        // re-captures the now-correct rectangle instead of clipping
        // to a stale (sometimes zero-sized) one from before layout
        // settled.
        if (viewportObject != null)
        {
            viewportObject.SetActive(false);
            viewportObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
        }

        // Guards against the earlier bug where Content's saved scroll
        // position no longer made sense after its height changed.
        // 1 = fully scrolled to top. Done last, after the Viewport
        // reset above, so it applies to the final, settled state.
        if (dashboardScrollRect != null)
        {
            dashboardScrollRect.verticalNormalizedPosition = 1f;
        }

        // ----- Same wiring dashboard_container.gd did in _ready() -----
        DashboardController.Instance.Initialize(
            summaryPanel,
            instructionPanel,
            operationPanel,
            explanationPanel,
            statusPanel,
            progressPanel
        );

        DashboardController.Instance.SetSummary(
            "Provides an overview of the current concept."
        );
        DashboardController.Instance.SetInstruction(
            "Follow the instructions below."
        );
        DashboardController.Instance.SetOperation(
            "No operation performed yet."
        );
        DashboardController.Instance.SetExplanation(
            "Educational explanations appear here."
        );
        DashboardController.Instance.SetStatus(
            "Ready."
        );
        DashboardController.Instance.SetProgress(
            "0% Complete"
        );
    }
}
