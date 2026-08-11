using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MartProg.Modules;

namespace MartProg.Core
{
    /// <summary>
    /// Phase 20: the Change Module selector, ported from ar_learning_screen.gd's
    /// _on_change_module_button_pressed() / _on_start_module_button_pressed() /
    /// setup_student_panel_layout() / setup_module_selector() /
    /// show_selected_module(), and finally replacing the manual "keep exactly
    /// one module's three objects enabled in the Hierarchy" convention (Migration
    /// Spec §5) that stood in for this since Phase 16, for the three modules
    /// that exist so far.
    ///
    /// Scope decision, stated up front: Godot reuses ONE panel (`student_panel`)
    /// for two different moments — the app's very first launch screen (which
    /// also asks for a Student ID via `student_id_input` before anything else
    /// is usable) and every later "Change Module" reopening of that same panel.
    /// Unity never ported the first-launch/Student-ID moment; every module
    /// controller already defaults ResearchLogger.StudentId / AppState.StudentId
    /// to "TEST_STUDENT" and the app has always started directly inside
    /// whichever module's GameObjects were left enabled in the Hierarchy (see
    /// Migration Spec §5's single-active-module convention, in place since
    /// Phase 16). This phase ports only the module-*switching* half of
    /// student_panel — the ID-entry half stays out of scope, unrequested and
    /// separable. Confirming a selection here does not touch ResearchLogger's
    /// StudentId at all.
    ///
    /// Porting decisions and preserved/adapted source quirks:
    ///
    /// 1. **All 8 modules are listed**, matching setup_module_selector()'s
    ///    exact 8 add_item() calls and display strings ("Array/List", "Stack",
    ///    "Queue", "Linked List", "Functions", "Loops", "Selection",
    ///    "Memory Management") in the same order — not just the 3 built so
    ///    far. Each dropdown entry maps to a ModuleGroupEntry in
    ///    `moduleGroups` (Inspector-configured); the 5 not-yet-built entries
    ///    are left with their controlStrip/moduleRoot/controllerObject fields
    ///    unassigned rather than removed from the list, so nothing needs to
    ///    be rewired here again as Linked List, Functions, Loops, Selection,
    ///    and Memory get built in later phases — only that entry's three
    ///    fields need filling in.
    ///
    /// 2. **"Not available yet" gating — corrected, not literally ported.**
    ///    The Godot source's own availability check is genuinely odd: inside
    ///    _on_start_module_button_pressed(), after the full if/elif chain that
    ///    sets current_module from the dropdown selection, there's an
    ///    unconditional `if memory_module != null: ... else: set_label_text(...,
    ///    selected_module + " is not available yet."); return` sitting at the
    ///    SAME indentation as the if/elif chain itself — i.e. it runs after
    ///    every selection, not just "Memory Management", and only ever checks
    ///    the one memory_module field regardless of what was actually chosen.
    ///    In the finished Godot build this is dormant (memory_module is always
    ///    assigned, so the else branch never fires for any selection). A
    ///    literal line-for-line port would NOT be dormant in Unity today —
    ///    Unity's equivalent reference for whichever module was selected is
    ///    null for 5 of the 8 entries, so copying the check exactly (checking
    ///    only one hardcoded field) would either wrongly block every
    ///    selection or wrongly allow all of them, depending on which field got
    ///    copied. What the source is clearly *trying* to express — "if the
    ///    selected module's scene reference isn't wired, say so and don't
    ///    switch" — is preserved and generalized correctly to check
    ///    `selected.IsBuilt` for whichever entry the student actually picked,
    ///    with the exact same message text and phrasing
    ///    ("{DisplayName} is not available yet."). This is a deliberate
    ///    deviation from the literal source structure, not an oversight —
    ///    see Migration Spec §8f for the fuller writeup.
    ///
    /// 3. **No Cancel/Back button.** Matches the source exactly: student_panel
    ///    has no dismiss path other than confirming a selection (Start Module
    ///    is the only button on it). Reopening Change Module and then
    ///    deciding to stay on the current module still requires confirming
    ///    that same module's entry in the dropdown.
    ///
    /// 4. **Dropdown selection is not reset when reopened.** Godot's
    ///    setup_module_selector() (which does `module_select_option.select(0)`)
    ///    only ever runs once, from _ready() — _on_change_module_button_pressed()
    ///    does not call it again. Reopening the selector leaves whichever item
    ///    was last chosen still showing, not defaulting back to Array/List.
    ///    Ported as-is; PopulateDropdown() below only ever runs from Start().
    ///
    /// 5. **EnterModule() replaces show_selected_module() +
    ///    reset_dashboard_state_for_module_start() for the target module** —
    ///    see IModuleController.cs and each controller's own EnterModule().
    ///    Every confirmed switch fully resets the destination module back to
    ///    its starting values and default dashboard text, even when switching
    ///    back to a module the student already used earlier in the session —
    ///    matching Godot's behavior exactly (it resets on every display, not
    ///    just the first).
    ///
    /// 6. **ResearchLogger.StartNewSession() runs on every confirmed switch**,
    ///    matching _on_start_module_button_pressed()'s
    ///    `ResearchLogger.start_new_session()` call. This means the CSV log's
    ///    SessionId changes every time a module is (re-)entered via this
    ///    selector, even when switching back to the module already active —
    ///    a real source behavior, not a bug introduced here.
    ///
    /// 7. **Two-frame defer before calling EnterModule() on the target
    ///    controller** (see EnterSelectedModuleDeferred() below). If the
    ///    target module's GameObjects were never active before this moment,
    ///    this is the first frame their own Start()/InitializeDeferred() ever
    ///    run — Unity does not retroactively call Start() for objects that
    ///    were inactive at scene load, only once when first enabled. Waiting
    ///    two frames keeps this comfortably behind that controller's own
    ///    one-frame-deferred init before this script also calls EnterModule()
    ///    itself. Calling it twice in that case (once from the controller's
    ///    own Start(), once from here) is redundant but harmless — both write
    ///    the same starting values/text. For a *returning* module (Start()
    ///    already ran earlier in the session), this call is the only thing
    ///    that resets it, since re-enabling a GameObject does not re-run
    ///    Start().
    ///
    /// 8. **On-device fix:** the enable/disable loops in
    ///    `OnChangeModuleClicked()` and `OnConfirmClicked()` originally only
    ///    toggled `controlStrip` and `moduleRoot`, never `controllerObject`.
    ///    Since each module's controller (e.g. `ArrayListController`) lives
    ///    on its own separate root-level GameObject — not a child of
    ///    `moduleRoot` — and starts the session disabled for every module
    ///    except whichever one is active by default, that left every other
    ///    module's controller permanently disabled: its `Start()` (and thus
    ///    `WireButtons()`'s `onClick.AddListener()` calls) never ran, so
    ///    Insert/Push/etc. silently did nothing after switching to it, even
    ///    though its 3D content still drew correctly (a separate object,
    ///    correctly toggled) and `EnterModule()` still appeared to work
    ///    (direct method calls execute fine on a disabled GameObject; only
    ///    Unity's own lifecycle messages and coroutines are affected by
    ///    active state) — which is what made the dashboard/reset side look
    ///    fine while the buttons were dead. Both loops now toggle all three
    ///    references together.
    /// </summary>
    public class ChangeModuleController : MonoBehaviour
    {
        [System.Serializable]
        public class ModuleGroupEntry
        {
            [Tooltip("Internal key — matches ResearchLogger.ModuleName / AppState.CurrentModule " +
                     "convention exactly: ArrayList, Stack, Queue, LinkedList, Functions, Loops, " +
                     "Selection, or Memory.")]
            public string moduleKey;

            [Tooltip("Display name shown in the dropdown — matches setup_module_selector()'s " +
                     "add_item() text exactly, e.g. \"Linked List\", \"Memory Management\".")]
            public string displayName;

            [Tooltip("This module's control-strip UI object (e.g. StackControlStrip). Leave " +
                     "unassigned if this module isn't built in Unity yet.")]
            public GameObject controlStrip;

            [Tooltip("This module's root 3D object (e.g. StackModule). Leave unassigned if this " +
                     "module isn't built in Unity yet.")]
            public GameObject moduleRoot;

            [Tooltip("The GameObject holding this module's controller component (e.g. the object " +
                     "with StackController on it) — must implement IModuleController. Leave " +
                     "unassigned if this module isn't built yet.")]
            public GameObject controllerObject;

            /// <summary>True once all three references above are wired.</summary>
            public bool IsBuilt => controlStrip != null && moduleRoot != null && controllerObject != null;
        }

        [Header("Trigger")]
        [Tooltip("The shared \"Change Module\" button — sits alongside Export Log / Clear Log in " +
                 "the same admin row per the Godot source's admin_button_row grouping, but is " +
                 "wired here rather than through AdminController, which stays scoped to logging.")]
        [SerializeField] private Button changeModuleButton;

        [Header("Selector Panel")]
        [Tooltip("Root object of the module-selector overlay (dropdown + Confirm button). Hidden " +
                 "at Start(), shown while switching, hidden again once a built module is confirmed.")]
        [SerializeField] private GameObject moduleSelectorPanel;
        [SerializeField] private TMP_Dropdown moduleDropdown;
        [SerializeField] private Button confirmButton;

        [Tooltip("Shows \"<Module> is not available yet.\" when an unbuilt module is confirmed — " +
                 "the Unity equivalent of student_error_label. Cleared every time the panel opens.")]
        [SerializeField] private TMP_Text messageLabel;

        [Header("Shared Dashboard (hidden while selecting)")]
        [Tooltip("The dashboard's root Canvas object (Canvas > DashboardRoot). Optional — if " +
                 "assigned, it's hidden while the selector is open and restored after a confirmed " +
                 "switch, mirroring Godot's main_ui.visible = false while student_panel is shown.")]
        [SerializeField] private GameObject dashboardRoot;

        [Header("Module Groups (order MUST match the dropdown — 8 entries; 5 may be unbuilt)")]
        [SerializeField] private List<ModuleGroupEntry> moduleGroups = new List<ModuleGroupEntry>();

        private void Start()
        {
            if (changeModuleButton != null)
                changeModuleButton.onClick.AddListener(OnChangeModuleClicked);

            if (confirmButton != null)
                confirmButton.onClick.AddListener(OnConfirmClicked);

            PopulateDropdown();

            // Unlike Godot, Unity's app doesn't start ON this selector — it
            // starts directly inside whichever module's objects were already
            // enabled in the Hierarchy (Migration Spec §5). So the panel
            // starts hidden here, not shown, matching Unity's existing entry
            // point rather than Godot's _ready().
            if (moduleSelectorPanel != null) moduleSelectorPanel.SetActive(false);
            if (messageLabel != null) messageLabel.text = "";
        }

        /// <summary>
        /// Direct port of setup_module_selector(). Runs once, from Start()
        /// only — see porting note 4 above for why it's never called again.
        /// </summary>
        private void PopulateDropdown()
        {
            if (moduleDropdown == null) return;

            moduleDropdown.ClearOptions();

            List<string> options = new List<string>();
            foreach (ModuleGroupEntry entry in moduleGroups)
            {
                options.Add(entry.displayName);
            }

            moduleDropdown.AddOptions(options);
            moduleDropdown.value = 0;
            moduleDropdown.RefreshShownValue();
        }

        /// <summary>
        /// Direct port of _on_change_module_button_pressed(). Hides every
        /// module's own control-strip + 3D content (the per-module
        /// equivalent of ar_world.visible = false cascading to every module
        /// Node3D) and the shared dashboard (main_ui.visible = false), then
        /// shows the selector on top.
        /// </summary>
        public void OnChangeModuleClicked()
        {
            foreach (ModuleGroupEntry entry in moduleGroups)
            {
                if (entry.controlStrip != null) entry.controlStrip.SetActive(false);
                if (entry.moduleRoot != null) entry.moduleRoot.SetActive(false);
                if (entry.controllerObject != null) entry.controllerObject.SetActive(false);
            }

            if (dashboardRoot != null) dashboardRoot.SetActive(false);
            if (messageLabel != null) messageLabel.text = "";
            if (moduleSelectorPanel != null) moduleSelectorPanel.SetActive(true);

            ResearchLogger.Instance?.LogEvent(
                "Change Module",
                "",
                "Opened",
                "User returned to module selection screen");
        }

        /// <summary>
        /// Direct port of _on_start_module_button_pressed(), minus the
        /// Student ID validation (out of scope — see class comment). Reads
        /// the dropdown selection, blocks with a message if that module
        /// isn't built yet (porting note 2), otherwise switches to it.
        /// </summary>
        public void OnConfirmClicked()
        {
            if (moduleDropdown == null || moduleGroups.Count == 0) return;

            int index = moduleDropdown.value;
            if (index < 0 || index >= moduleGroups.Count) return;

            ModuleGroupEntry selected = moduleGroups[index];

            if (!selected.IsBuilt)
            {
                if (messageLabel != null)
                {
                    messageLabel.text = selected.displayName + " is not available yet.";
                }

                return;
            }

            foreach (ModuleGroupEntry entry in moduleGroups)
            {
                bool isSelected = entry == selected;
                if (entry.controlStrip != null) entry.controlStrip.SetActive(isSelected);
                if (entry.moduleRoot != null) entry.moduleRoot.SetActive(isSelected);
                if (entry.controllerObject != null) entry.controllerObject.SetActive(isSelected);
            }

            if (AppState.Instance != null)
            {
                AppState.Instance.CurrentModule = selected.moduleKey;
            }

            if (ResearchLogger.Instance != null)
            {
                ResearchLogger.Instance.ModuleName = selected.moduleKey;
                ResearchLogger.Instance.StartNewSession();
            }

            StartCoroutine(EnterSelectedModuleDeferred(selected));
        }

        /// <summary>
        /// See porting note 7 above for why this waits two frames before
        /// calling EnterModule() on the target controller.
        /// </summary>
        private IEnumerator EnterSelectedModuleDeferred(ModuleGroupEntry selected)
        {
            yield return null;
            yield return null;

            if (selected.controllerObject != null)
            {
                IModuleController controller = selected.controllerObject.GetComponent<IModuleController>();

                if (controller != null)
                {
                    controller.EnterModule();
                }
                else
                {
                    Debug.LogWarning(
                        "ChangeModuleController: " + selected.displayName +
                        "'s controllerObject does not implement IModuleController.");
                }
            }

            if (dashboardRoot != null) dashboardRoot.SetActive(true);
            if (moduleSelectorPanel != null) moduleSelectorPanel.SetActive(false);

            ResearchLogger.Instance?.LogEvent(
                "Module Opened",
                selected.displayName,
                "Started",
                selected.displayName + " module opened");
        }
    }
}
