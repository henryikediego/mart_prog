using System.Collections;
using System.Collections.Generic;
using TMPro;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MartProg.Modules;
using MartProg.Menu;

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
    /// **Phase 27: the deferred half is now built.** Godot reuses ONE panel
    /// (`student_panel`) for two moments — the app's first launch screen,
    /// which asks for a Student ID before anything else is usable, and every
    /// later "Change Module" reopening of that same panel. Phase 20 ported
    /// only the module-SWITCHING half and recorded the ID-entry half as out
    /// of scope; the app started directly inside whichever module's
    /// GameObjects were left enabled in the Hierarchy (Migration Spec §5),
    /// and ResearchLogger.StudentId kept its "TEST_STUDENT" default.
    ///
    /// That default was written into **every row of mart_prog_log.csv**,
    /// whose header is
    /// `Timestamp,StudentID,SessionID,Module,Action,Value,Result,Details`.
    /// For an application whose purpose is collecting per-student interaction
    /// data, an identity column reading "TEST_STUDENT" for every participant
    /// is not a cosmetic gap. That is why this was taken before the remaining
    /// quiz-bank and log-name audits.
    ///
    /// Now ported: the app boots to this panel, a Student ID is required
    /// before any module can start, and the entered value is written to
    /// ResearchLogger.StudentId ahead of StartNewSession(). Godot's exact
    /// message text is used for the empty case.
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
    ///
    /// 9. **Phase 37: a TEMPORARY "About" button was added to this same
    ///    ModuleSelectorPanel**, since it's still the app's real first screen
    ///    and there is no Landing Page yet. See AboutController.cs and the
    ///    Phase 37 stage doc — this is a placeholder entry point, not a
    ///    permanent feature of the selector itself, and should come back out
    ///    once the Landing Page shell exists.
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

        [Tooltip("Godot's student_error_label, which carries BOTH of this panel's messages: " +
                 "\"Please enter a student research ID, for example S001.\" and " +
                 "\"<Module> is not available yet.\" Source uses one label for both. " +
                 "Cleared every time the panel opens.")]
        [SerializeField] private TMP_Text messageLabel;

        /// <summary>
        /// Phase 34: which ID shape this study expects. Set once before a
        /// study runs — it decides which typo the app is able to catch.
        /// </summary>
        public enum StudentIdMode
        {
            /// Accepts s1 and s1c/s1e alike. Safe default; catches malformed
            /// IDs but cannot catch a dropped group letter.
            AcceptEither,

            /// Requires a c or e suffix. Use for a control/experiment study,
            /// where "s1" typed instead of "s1c" would leave a participant
            /// unassigned and you would not find out until analysis.
            TwoGroups,

            /// Requires NO suffix. Use for a single-group study, where a
            /// stray letter would invent a group that does not exist.
            SingleGroup
        }

        [Header("Phase 27 — Student ID")]
        [Tooltip("Godot's student_id_input. Required: no module can start until this " +
                 "is non-empty, and its value becomes ResearchLogger.StudentId.")]
        [SerializeField] private TMP_InputField studentIdInput;

        [Tooltip("Which ID shape this study expects.\n\n" +
                 "Two Groups — s1c / s2e required (c = control, e = experiment).\n" +
                 "Single Group — s1 / s2 required, no suffix.\n" +
                 "Accept Either — both allowed.\n\n" +
                 "Set this BEFORE a study runs: it is what lets the app catch a " +
                 "dropped or stray group letter, which is otherwise invisible until " +
                 "analysis.")]
        [SerializeField] private StudentIdMode studentIdMode = StudentIdMode.AcceptEither;

        // s, then at least one digit, then an optional single group letter.
        // Case-insensitive on entry; normalised to lower case before use so
        // "S1C" and "s1c" can never become two different participants.
        private static readonly Regex StudentIdPattern =
            new Regex(@"^s(\d+)([ce])?$", RegexOptions.IgnoreCase);

        /// <summary>
        /// Phase 34. Validates and normalises a typed research ID against the
        /// configured study design.
        ///
        /// Before this, the only check was non-empty — a bare number, a name,
        /// or anything else was accepted straight into the research log, and
        /// nothing downstream could tell a mistyped ID from a real one.
        /// </summary>
        private bool TryNormaliseStudentId(string raw, out string normalised, out string error)
        {
            normalised = "";
            error = "";

            string trimmed = (raw ?? "").Trim();

            if (trimmed == "")
            {
                error = "Please enter a student research ID, for example " + ExampleId() + ".";
                return false;
            }

            Match m = StudentIdPattern.Match(trimmed);

            if (!m.Success)
            {
                error = "That is not a valid research ID. Use " + ExampleId() +
                        " — the letter s, a number, " +
                        (studentIdMode == StudentIdMode.SingleGroup
                            ? "and nothing after it."
                            : "then c for control or e for experiment.");
                return false;
            }

            string number = m.Groups[1].Value;
            string suffix = m.Groups[2].Success ? m.Groups[2].Value.ToLowerInvariant() : "";

            if (studentIdMode == StudentIdMode.TwoGroups && suffix == "")
            {
                error = "This study has two groups, so the ID needs a group letter: " +
                        "s" + number + "c for control or s" + number + "e for experiment.";
                return false;
            }

            if (studentIdMode == StudentIdMode.SingleGroup && suffix != "")
            {
                error = "This study has one group, so the ID should not have a group " +
                        "letter. Use s" + number + ".";
                return false;
            }

            // Leading zeros are stripped so s01c and s1c cannot become two
            // participants. int.TryParse also rejects an absurdly long entry
            // rather than overflowing.
            if (!int.TryParse(number, out int parsedNumber) || parsedNumber < 1)
            {
                error = "Participant numbers start at 1. Use " + ExampleId() + ".";
                return false;
            }

            number = parsedNumber.ToString();

            normalised = "s" + number + suffix;
            return true;
        }

        // =================================================================
        // Phase 35 — the research ID is registered once, then locked.
        //
        // A participant keeps one ID for the whole study, like a university
        // student number. Until now the field was fully editable every time
        // the student returned to the selector, so a participant could change
        // their own ID — by accident or otherwise — and the research log
        // would show one person as two.
        //
        // The lock is against the STUDENT, not the researcher: it persists
        // across app restarts via PlayerPrefs, and is released by a long
        // press on the field (see BeginIdRelease). That works whether each
        // participant has their own device or one device is shared and the
        // researcher releases the ID between participants.
        //
        // Deliberately independent of Clear Log: clearing the log is about
        // data, releasing the ID is about identity, and wanting one rarely
        // means wanting the other.
        // =================================================================

        private const string StudentIdPrefKey = "MARTPROG_RegisteredStudentId";

        [Tooltip("Phase 35: once a research ID is entered it is remembered and the " +
                 "field is locked, so a participant keeps one ID for the whole study. " +
                 "Release it with a long press on the field (3 seconds, twice). " +
                 "Untick to allow the ID to be retyped freely — useful while testing, " +
                 "not during a study.")]
        [SerializeField] private bool lockStudentIdOnceRegistered = true;

        [Tooltip("Seconds to hold the locked ID field to begin releasing it.")]
        [SerializeField] private float idReleaseHoldSeconds = 3f;

        private float idPressStartedAt = -1f;
        private float idReleaseArmedUntil = -1f;

        private static string RegisteredStudentId =>
            PlayerPrefs.GetString(StudentIdPrefKey, "");

        private bool IsStudentIdLocked =>
            lockStudentIdOnceRegistered && RegisteredStudentId != "";

        /// <summary>
        /// Puts the field into the state matching whether an ID is registered.
        /// Safe to call repeatedly.
        /// </summary>
        private void ApplyStudentIdLockState()
        {
            if (studentIdInput == null) return;

            if (IsStudentIdLocked)
            {
                studentIdInput.SetTextWithoutNotify(RegisteredStudentId);
                studentIdInput.interactable = false;
            }
            else
            {
                studentIdInput.interactable = true;
            }
        }

        private void RegisterStudentId(string normalisedId)
        {
            if (!lockStudentIdOnceRegistered) return;
            if (RegisteredStudentId == normalisedId) return;

            PlayerPrefs.SetString(StudentIdPrefKey, normalisedId);
            PlayerPrefs.Save();

            ResearchLogger.Instance?.LogEvent(
                "Student ID Registered", normalisedId, "Success",
                "Research ID registered and locked for this device");
        }

        /// <summary>
        /// Long-press handling on the ID field. Two deliberate holds, the same
        /// arm-then-confirm shape Clear Log uses — a student leaning on the
        /// screen cannot reach it, and a researcher who knows can do it in
        /// six seconds without any extra UI.
        ///
        /// Wired at runtime through an EventTrigger so this needs no scene
        /// work. A non-interactable InputField still receives pointer events:
        /// Selectable checks `interactable` in its own handlers, but
        /// EventTrigger implements the handler interfaces directly and does
        /// not. If that ever changes, the fallback is to leave the field
        /// interactable and set `readOnly = true` instead.
        /// </summary>
        private void WireStudentIdLongPress()
        {
            if (studentIdInput == null) return;

            EventTrigger trigger = studentIdInput.gameObject.GetComponent<EventTrigger>();
            if (trigger == null) trigger = studentIdInput.gameObject.AddComponent<EventTrigger>();

            // Start() runs once per GameObject ACTIVATION, so without this a
            // re-activation would stack a second pair of listeners and fire
            // the release twice per hold. Same lifecycle trap as the Phase 27
            // button-grid regression, in a different costume.
            trigger.triggers.Clear();

            EventTrigger.Entry down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            down.callback.AddListener(_ => idPressStartedAt = Time.unscaledTime);
            trigger.triggers.Add(down);

            EventTrigger.Entry up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            up.callback.AddListener(_ =>
            {
                float held = idPressStartedAt < 0f ? 0f : Time.unscaledTime - idPressStartedAt;
                idPressStartedAt = -1f;

                if (held >= idReleaseHoldSeconds) OnStudentIdLongPress();
            });
            trigger.triggers.Add(up);
        }

        private void OnStudentIdLongPress()
        {
            if (!IsStudentIdLocked) return;

            if (Time.unscaledTime <= idReleaseArmedUntil)
            {
                string released = RegisteredStudentId;

                ResearchLogger.Instance?.LogEvent(
                    "Student ID Released", released, "Success",
                    "Registered research ID released on this device");

                PlayerPrefs.DeleteKey(StudentIdPrefKey);
                PlayerPrefs.Save();

                idReleaseArmedUntil = -1f;

                if (studentIdInput != null) studentIdInput.SetTextWithoutNotify("");
                ApplyStudentIdLockState();

                if (messageLabel != null)
                {
                    messageLabel.text = "Research ID " + released +
                                        " released. Enter the next participant's ID.";
                }

                return;
            }

            idReleaseArmedUntil = Time.unscaledTime + 10f;

            if (messageLabel != null)
            {
                messageLabel.text = "Release the registered ID " + RegisteredStudentId +
                                    "? Hold the field again to confirm.";
            }
        }

        private string ExampleId()
        {
            switch (studentIdMode)
            {
                case StudentIdMode.TwoGroups:   return "s1c or s2e";
                case StudentIdMode.SingleGroup: return "s1";
                default:                        return "s1, s1c or s2e";
            }
        }

        [Tooltip("Optional. The Export Log / Clear Log / Change Module row. Hidden on the " +
                 "entry screen and shown once a module starts, matching Godot hiding main_ui " +
                 "in _ready(). Leave empty and the row simply stays visible throughout.")]
        [SerializeField] private GameObject adminRow;

        [Header("Shared Dashboard (hidden while selecting)")]
        [Tooltip("The dashboard's root Canvas object (Canvas > DashboardRoot). Optional — if " +
                 "assigned, it's hidden while the selector is open and restored after a confirmed " +
                 "switch, mirroring Godot's main_ui.visible = false while student_panel is shown.")]
        [SerializeField] private GameObject dashboardRoot;

        [Header("Module Groups (order MUST match the dropdown — 8 entries; 5 may be unbuilt)")]
        [SerializeField] private List<ModuleGroupEntry> moduleGroups = new List<ModuleGroupEntry>();

        [Header("Phase 37 — About (TEMPORARY entry point)")]
        [Tooltip("TEMPORARY. A button added directly to ModuleSelectorPanel, not part " +
                 "of the Godot source, since this panel is currently the app's real " +
                 "first screen and there is no Landing Page yet. Moves to the Landing " +
                 "Page's own About button once that's built (Backlog §5/§7 item 3) — " +
                 "this field should be left unassigned at that point, not kept as a " +
                 "second way in. See AboutController's class comment.")]
        [SerializeField] private Button aboutButton;

        [SerializeField] private AboutController aboutController;

        private void Start()
        {
            if (changeModuleButton != null)
                changeModuleButton.onClick.AddListener(OnChangeModuleClicked);

            if (aboutButton != null && aboutController != null)
                aboutButton.onClick.AddListener(aboutController.Show);

            // Phase 29 (F11): nothing in the project was wired to onSubmit,
            // so a student who typed their ID and pressed Enter/Done saw the
            // keyboard dismiss and nothing else happen. This is the app's
            // FIRST screen, where a confused student has the least context
            // to work out that a separate button press is needed.
            // onSubmit rather than onEndEdit: onEndEdit also fires on focus
            // loss, which would start a session the moment they tapped away.
            if (studentIdInput != null)
                studentIdInput.onSubmit.AddListener(_ => OnConfirmClicked());

            // Phase 35: restore a registered ID and lock the field, before the
            // selector is first shown.
            WireStudentIdLongPress();
            ApplyStudentIdLockState();

            if (IsStudentIdLocked && ResearchLogger.Instance != null)
            {
                // The logger needs the ID even before the student presses
                // Start, so anything logged from the selector carries it.
                ResearchLogger.Instance.StudentId = RegisteredStudentId;
            }

            if (confirmButton != null)
                confirmButton.onClick.AddListener(OnConfirmClicked);

            PopulateDropdown();

            // Phase 27: the app now BOOTS to this selector, matching Godot's
            // _ready() —
            //
            //     student_panel.visible = true
            //     main_ui.visible = false
            //     quiz_panel.visible = false
            //     ar_world.visible = false
            //
            // Until now Unity started inside whichever module was left
            // enabled in the Hierarchy (Migration Spec §5's single-active-
            // module convention), which is why no Student ID was ever asked
            // for. Everything is hidden here instead, in code, so the scene's
            // enabled/disabled state at edit time no longer decides where the
            // app opens.
            ShowSelector(logEvent: false);
        }

        /// <summary>
        /// Godot: the shared body of _ready() and
        /// _on_change_module_button_pressed() — hide the module world, show
        /// the panel, clear the error label. Only the log event differs, so
        /// that is the parameter.
        /// </summary>
        private void ShowSelector(bool logEvent)
        {
            foreach (ModuleGroupEntry entry in moduleGroups)
            {
                if (entry.controlStrip != null) entry.controlStrip.SetActive(false);
                if (entry.moduleRoot != null) entry.moduleRoot.SetActive(false);
                if (entry.controllerObject != null) entry.controllerObject.SetActive(false);
            }

            if (dashboardRoot != null) dashboardRoot.SetActive(false);
            if (adminRow != null) adminRow.SetActive(false);
            if (messageLabel != null)
            {
                // Phase 35: a greyed-out field with someone's ID already in it
                // is confusing without a word of explanation. Say it plainly
                // rather than leaving the student to wonder whether they are
                // meant to type.
                messageLabel.text = IsStudentIdLocked
                    ? "Signed in as " + RegisteredStudentId + ". Choose a module to continue."
                    : "";
            }

            if (moduleSelectorPanel != null) moduleSelectorPanel.SetActive(true);

            if (logEvent)
            {
                ResearchLogger.Instance?.LogEvent(
                    "Change Module",
                    "",
                    "Opened",
                    "User returned to module selection screen");
            }
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
            // Godot does NOT clear student_id_input here — the field keeps
            // its text, so a student returning to the selector mid-session
            // simply presses Start again rather than retyping their ID.
            // Preserved: only the error label is cleared.
            ShowSelector(logEvent: true);
        }

        /// <summary>
        /// Direct port of _on_start_module_button_pressed(), Student ID
        /// validation included as of Phase 27. Order matters and matches
        /// source: the ID is checked FIRST, before the dropdown is read at
        /// all, so an empty ID blocks every module equally.
        /// </summary>
        public void OnConfirmClicked()
        {
            if (moduleDropdown == null || moduleGroups.Count == 0) return;

            // Godot:
            //     var entered_id = student_id_input.text.strip_edges()
            //     if entered_id == "":
            //         set_label_text(student_error_label,
            //             "Please enter a student research ID, for example S001.")
            //         return
            //
            // Message ported verbatim, S001 example included — it is the only
            // guidance a participant gets on what an ID should look like.
            string enteredId;

            if (IsStudentIdLocked)
            {
                // Phase 35: already registered on this device. The field is
                // non-interactable, but the stored value is used rather than
                // whatever the field happens to contain — so even if the field
                // is somehow edited, the registered ID is what gets logged.
                enteredId = RegisteredStudentId;
            }
            else
            {
                string typedId = studentIdInput != null ? studentIdInput.text : "";

                if (!TryNormaliseStudentId(typedId, out enteredId, out string idError))
                {
                    if (messageLabel != null) messageLabel.text = idError;
                    return;
                }

                // Write the normalised form back so the student sees exactly
                // what was recorded — "S01C" typed becomes "s1c" on screen and
                // in the log, rather than the two silently diverging.
                if (studentIdInput != null && studentIdInput.text != enteredId)
                {
                    studentIdInput.SetTextWithoutNotify(enteredId);
                }
            }

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
                // Godot: ResearchLogger.student_id = entered_id, set
                // immediately before start_new_session(). The order matters —
                // StartNewSession() writes a session-start row, and that row
                // must already carry the real ID rather than the default.
                ResearchLogger.Instance.StudentId = enteredId;
                ResearchLogger.Instance.ModuleName = selected.moduleKey;
                ResearchLogger.Instance.StartNewSession();

                // Phase 35: register on first successful start, after the
                // session exists so the "Student ID Registered" row lands in
                // the new session rather than the previous one.
                RegisterStudentId(enteredId);
                ApplyStudentIdLockState();
            }

            if (AppState.Instance != null)
            {
                // Unity-side mirror. Godot sets only ResearchLogger's copy,
                // because it has no AppState — but this project added one in
                // Phase 13 and it has carried the same "TEST_STUDENT" default
                // ever since. Leaving the two out of step would make whichever
                // one a future reader happens to consult a coin toss.
                //
                // If AppState has no StudentId field in your build, this is
                // the one line to delete.
                AppState.Instance.StudentId = enteredId;
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
            if (adminRow != null) adminRow.SetActive(true);
            if (moduleSelectorPanel != null) moduleSelectorPanel.SetActive(false);

            ResearchLogger.Instance?.LogEvent(
                "Module Opened",
                selected.displayName,
                "Started",
                selected.displayName + " module opened");
        }
    }
}
