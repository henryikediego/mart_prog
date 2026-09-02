using System.Collections;
using System.Collections.Generic;
using TMPro;
using System.Text.RegularExpressions;
using UnityEngine;
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
    /// 9. **Phase 38: this panel is no longer the app's first screen.** The
    ///    Landing Page is. Godot has nothing equivalent — `main_menu.gd` is a
    ///    student ID field, a dead language dropdown and a Start button, and
    ///    `student_panel` was always the first thing a participant saw. So
    ///    this is new behaviour rather than a closer port, and it is the point
    ///    where this screen stops being a direct translation of source.
    ///
    ///    What did NOT change: everything this panel does once open, and the
    ///    hiding of the module world at boot (now `HideModuleWorld()`, still
    ///    the honest port of `_ready()`). Only what appears on top of it.
    ///
    ///    Phase 37's temporary About button on this panel is gone with it —
    ///    that was always a placeholder for exactly this.
    ///
    ///    **The fallback is deliberate:** leave `landingPage` unassigned and
    ///    the app boots straight here again, exactly as it did from Phase 27
    ///    to Phase 37. The Godot-matching boot path is one Inspector field
    ///    away, not deleted.
    ///
    /// 10. **DEVIATION — an optional Back button on this panel.** Porting
    ///    note 3 records that source has no Cancel/Back: the only way out of
    ///    `student_panel` is confirming a selection. That was reasonable while
    ///    this panel WAS the first screen — there was nowhere behind it to go.
    ///    With a Landing Page in front, the same absence becomes a dead end:
    ///    a student who opens module choice and wants to read About instead
    ///    would have no way back.
    ///
    ///    So `selectorBackButton` exists, and is optional. Leave it unassigned
    ///    to keep source behaviour exactly. The deviation is in reachability,
    ///    not in what the panel does — the same category as Phase 28 wiring up
    ///    Peek.
    ///
    /// 11. **Phase 40 — ID release relocated; study mode now runtime-adjustable.**
    ///    Two changes, both driven by the new Researcher Area gated screen
    ///    rather than by this file's own scope growing:
    ///
    ///    Releasing a registered ID used to be a hidden long press directly on
    ///    `studentIdInput` (Phase 35) — a workaround for there being no gated
    ///    screen yet. Now that one exists, that gesture is gone from this file
    ///    entirely; `ReleaseRegisteredId()` is a plain public method a plain
    ///    button in the Researcher Area calls. `WireStudentIdLongPress()` /
    ///    `OnStudentIdLongPress()` and their supporting fields are deleted, not
    ///    just unwired — the EventTrigger they built no longer exists either.
    ///
    ///    `studentIdMode` was build-time only (Inspector default, one value for
    ///    the life of the build). `EffectiveStudentIdMode` reads a PlayerPrefs
    ///    override first, so a researcher can switch a device between study
    ///    designs from the Researcher Area's toggle without a rebuild — useful
    ///    for testing all three shapes on one device, or correcting a
    ///    misconfigured device without reflashing it. The Inspector field is
    ///    unchanged and still the fallback for a device nothing has set yet.
    ///
    /// 12. **Phase 43 — module order enforcement. NOT A PORT.** Godot's
    ///    module selector always offered free choice; there is no source
    ///    behaviour being translated here, and nothing in ar_learning_screen.gd
    ///    resembles it. Backlog_Landing_Page_And_Navigation.md §4.5/§6.4
    ///    (this project's own forward-looking research/design notes, not
    ///    Godot source) raise it as something the study might want: "If the
    ///    study wants modules attempted in a set order, the landing page is
    ///    where that could be enforced." Built as configuration, not code,
    ///    exactly as §6.4 asks — moduleGroups' own Inspector order IS the
    ///    required order, so reordering that one list changes both what the
    ///    dropdown offers and what order it's gated in, with nothing else to
    ///    keep in sync. Off by default; a researcher turns it on per protocol
    ///    from the Researcher Area, mirroring Student ID Mode's runtime-
    ///    toggle shape exactly. See EffectiveEnforceModuleOrder,
    ///    SetEnforceModuleOrder(), MarkModuleQuizCompleted(), and
    ///    IsUnlockedByModuleOrder() below.
    ///
    /// 13. **Phase 44 — Continue Learning. NOT A PORT.** Godot has no
    ///    progress concept at all — main_menu.gd always opens on a blank
    ///    student panel. Backlog §6.5 confirmed "progress" means modules,
    ///    quizzes, and Memory activities together; this reuses porting note
    ///    12's completed-quiz set as the single "module done" signal for all
    ///    of that rather than adding a second tracker, and adds Memory-
    ///    specific step-level resume on top only where the codebase already
    ///    has a step concept to resume within (MemoryController's own
    ///    14-activity GetNextActivity(), previously per-session only — see
    ///    that class's comment note 12 for the cross-session persistence
    ///    this phase gives it). OnConfirmClicked()'s activation tail is
    ///    extracted into ActivateModule() so ContinueLearning() below reaches
    ///    the exact same path a manual confirm already uses, not a second,
    ///    parallel one. See GetNextIncompleteModule(), CanContinueLearning(),
    ///    and ContinueLearning() below, and Migration Spec §8w.
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
        [Tooltip("The \"Change Module\" button, wired here rather than through a logging-scoped " +
                 "controller — matches the Godot source's admin_button_row grouping, which put " +
                 "it alongside Export Log / Clear Log. Those two lived on AdminController until " +
                 "Phase 40 retired it in favor of the gated Researcher Area; Change Module was " +
                 "never part of that move and has always been wired here.")]
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

        // Phase 40: the study design a device expects is now adjustable at
        // runtime from the Researcher Area, not just at build time from the
        // Inspector. PlayerPrefs is checked first; the Inspector field above
        // remains the fallback for a device nothing has ever set on — so a
        // fresh install still behaves exactly as before this phase existed.
        private const string StudentIdModePrefKey = "MARTPROG_StudentIdMode";

        /// <summary>
        /// The study design actually in effect on this device — PlayerPrefs
        /// first, falling back to the Inspector-configured <see cref="studentIdMode"/>
        /// when nothing has been set on-device yet. Every internal check
        /// (<see cref="TryNormaliseStudentId"/>, <see cref="ExampleId"/>) reads
        /// this, never the raw field, so a mode change from the Researcher
        /// Area takes effect immediately without a rebuild.
        /// </summary>
        public StudentIdMode EffectiveStudentIdMode
        {
            get
            {
                int stored = PlayerPrefs.GetInt(StudentIdModePrefKey, -1);
                return stored >= 0 && System.Enum.IsDefined(typeof(StudentIdMode), stored)
                    ? (StudentIdMode)stored
                    : studentIdMode;
            }
        }

        /// <summary>
        /// Phase 40: called from the Researcher Area's Student ID Mode
        /// toggle. Persists immediately so the choice survives an app
        /// restart, matching how the registered ID itself persists.
        /// </summary>
        public void SetStudentIdMode(StudentIdMode mode)
        {
            PlayerPrefs.SetInt(StudentIdModePrefKey, (int)mode);
            PlayerPrefs.Save();
        }

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
                        (EffectiveStudentIdMode == StudentIdMode.SingleGroup
                            ? "and nothing after it."
                            : "then c for control or e for experiment.");
                return false;
            }

            string number = m.Groups[1].Value;
            string suffix = m.Groups[2].Success ? m.Groups[2].Value.ToLowerInvariant() : "";

            if (EffectiveStudentIdMode == StudentIdMode.TwoGroups && suffix == "")
            {
                error = "This study has two groups, so the ID needs a group letter: " +
                        "s" + number + "c for control or s" + number + "e for experiment.";
                return false;
            }

            if (EffectiveStudentIdMode == StudentIdMode.SingleGroup && suffix != "")
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
        // across app restarts via PlayerPrefs.
        //
        // Phase 40: release moved off this field entirely. It used to be a
        // long press directly on the input field (3 seconds, twice) — a
        // hidden gesture that stood in for the gated screen this project
        // didn't have yet. Now that the Researcher Area exists (reached by
        // its own long press on About's version block), nesting a second
        // hidden gesture inside an already-gated screen is redundant
        // complexity, not extra safety. Release is a plain, clearly-labeled
        // button there, calling the public ReleaseRegisteredId() below
        // directly. See ResearcherAreaController's class comment.
        //
        // Deliberately independent of Clear Log: clearing the log is about
        // data, releasing the ID is about identity, and wanting one rarely
        // means wanting the other.
        // =================================================================

        private const string StudentIdPrefKey = "MARTPROG_RegisteredStudentId";

        [Tooltip("Phase 35: once a research ID is entered it is remembered and the " +
                 "field is locked, so a participant keeps one ID for the whole study. " +
                 "Release it from the Researcher Area (Phase 40) — see " +
                 "ReleaseRegisteredId() below. Untick to allow the ID to be retyped " +
                 "freely — useful while testing, not during a study.")]
        [SerializeField] private bool lockStudentIdOnceRegistered = true;

        /// <summary>
        /// The ID registered on this device, or "" if none. Public as of
        /// Phase 38 so the Landing Page can greet a returning participant
        /// without duplicating the PlayerPrefs key — one place owns that
        /// string, and a second copy of it is exactly how two screens end up
        /// disagreeing about who is signed in.
        /// </summary>
        public static string RegisteredStudentId =>
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
        /// Phase 40: extracted from the old long-press handler's confirmed
        /// branch (see the Phase 35 block comment above for why release moved
        /// off this field). Called directly by ResearcherAreaController's
        /// Release Registered ID button — no arm/confirm state of its own,
        /// since that two-tap-arm pattern now lives in the button itself,
        /// matching Export Log / Clear Log / Clear Feedback there.
        ///
        /// Returns the ID that was released ("" if none was registered) so
        /// the caller can show it in its own confirmation message without a
        /// second PlayerPrefs read after the key is already gone.
        /// </summary>
        public string ReleaseRegisteredId()
        {
            if (!IsStudentIdLocked) return "";

            string released = RegisteredStudentId;

            ResearchLogger.Instance?.LogEvent(
                "Student ID Released", released, "Success",
                "Registered research ID released on this device");

            PlayerPrefs.DeleteKey(StudentIdPrefKey);
            PlayerPrefs.Save();

            if (studentIdInput != null) studentIdInput.SetTextWithoutNotify("");
            ApplyStudentIdLockState();

            if (messageLabel != null)
            {
                messageLabel.text = "Research ID " + released +
                                    " released. Enter the next participant's ID.";
            }

            return released;
        }

        private string ExampleId()
        {
            switch (EffectiveStudentIdMode)
            {
                case StudentIdMode.TwoGroups:   return "s1c or s2e";
                case StudentIdMode.SingleGroup: return "s1";
                default:                        return "s1, s1c or s2e";
            }
        }

        /// <summary>
        /// Phase 40 fix. `studentIdInput`'s placeholder text was a plain
        /// string typed once into the Editor scene — harmless while
        /// `studentIdMode` was a build-time-only Inspector field, since
        /// whoever set that up would naturally type a matching placeholder
        /// at the same time. It became actively misleading the moment
        /// <see cref="EffectiveStudentIdMode"/> could change on-device: a
        /// researcher switching a device to Two Groups from the Researcher
        /// Area would still see "e.g. s1 or s1c" on the field a Two-Groups
        /// study actually rejects a bare "s1" from.
        ///
        /// Reuses <see cref="ExampleId"/> — the exact same text already
        /// shown in the validation error messages — rather than a second,
        /// separately-worded example living only here, which is exactly how
        /// the two could drift apart again. Called from
        /// <see cref="ShowSelector"/> every time the panel is about to be
        /// shown, not just once from <see cref="Start"/>, since the mode can
        /// change on a completely different screen (the Researcher Area)
        /// while this one isn't even open.
        /// </summary>
        private void RefreshStudentIdPlaceholder()
        {
            if (studentIdInput == null) return;

            TMP_Text placeholder = studentIdInput.placeholder as TMP_Text;
            if (placeholder == null) return;

            placeholder.text = "Student ID, e.g. " + ExampleId();
        }

        [Tooltip("Optional. The Change Module row. Hidden on the entry screen and shown " +
                 "once a module starts, matching Godot hiding main_ui in _ready(). Leave " +
                 "empty and the row simply stays visible throughout.\n\n" +
                 "Phase 40: Export Log / Clear Log moved off this row entirely, into the " +
                 "gated Researcher Area (AdminController is retired) — Change Module stays " +
                 "here since it is a legitimate participant action, not a researcher-only " +
                 "one.")]
        [SerializeField] private GameObject adminRow;

        [Header("Shared Dashboard (hidden while selecting)")]
        [Tooltip("The dashboard's root Canvas object (Canvas > DashboardRoot). Optional — if " +
                 "assigned, it's hidden while the selector is open and restored after a confirmed " +
                 "switch, mirroring Godot's main_ui.visible = false while student_panel is shown.")]
        [SerializeField] private GameObject dashboardRoot;

        [Header("Module Groups (order MUST match the dropdown — 8 entries; 5 may be unbuilt)")]
        [SerializeField] private List<ModuleGroupEntry> moduleGroups = new List<ModuleGroupEntry>();

        [Header("Phase 43 — Module Order Enforcement")]
        [Tooltip("When enabled, a module can only be started once every BUILT module " +
                 "before it in the list above has had its quiz completed at least once " +
                 "by the currently registered student. moduleGroups' own order is the " +
                 "protocol order -- there is no second list to keep in sync. Off by " +
                 "default, so the study can run with free module choice exactly as it " +
                 "always has, until a researcher turns this on for a specific protocol.\n\n" +
                 "Runtime-adjustable from the Researcher Area, mirroring Student ID Mode " +
                 "-- this Inspector value is only the fallback for a device nothing has " +
                 "set yet. See EffectiveEnforceModuleOrder / SetEnforceModuleOrder().")]
        [SerializeField] private bool enforceModuleOrder = false;

        // Phase 43: same PlayerPrefs-override-with-Inspector-fallback shape as
        // studentIdMode / EffectiveStudentIdMode / SetStudentIdMode() above --
        // see that block's comment. -1 (unset) falls back to the Inspector
        // field; 0/1 (set, from a researcher's toggle) wins.
        private const string EnforceModuleOrderPrefKey = "MARTPROG_EnforceModuleOrder";

        /// <summary>
        /// Whether module order is actually enforced on this device --
        /// PlayerPrefs first, falling back to the Inspector-configured
        /// <see cref="enforceModuleOrder"/> when nothing has been set
        /// on-device yet. <see cref="OnConfirmClicked"/> reads this, never
        /// the raw field, so a Researcher Area toggle takes effect
        /// immediately without a rebuild.
        /// </summary>
        public bool EffectiveEnforceModuleOrder
        {
            get
            {
                int stored = PlayerPrefs.GetInt(EnforceModuleOrderPrefKey, -1);
                return stored >= 0 ? stored == 1 : enforceModuleOrder;
            }
        }

        /// <summary>
        /// Phase 43: called from the Researcher Area's order-enforcement
        /// toggle. Persists immediately, matching SetStudentIdMode().
        /// </summary>
        public void SetEnforceModuleOrder(bool enforce)
        {
            PlayerPrefs.SetInt(EnforceModuleOrderPrefKey, enforce ? 1 : 0);
            PlayerPrefs.Save();
        }

        // =================================================================
        // Phase 43 (continued) — which modules the CURRENT participant has
        // already completed a quiz for, persisted per REGISTERED student ID
        // (not the transient ResearchLogger.StudentId) so that releasing an
        // ID from the Researcher Area and registering a new one on the same
        // device starts that new participant with a clean slate, rather than
        // inheriting whatever the previous participant had already unlocked.
        //
        // "Completed" is decided entirely by QuizController.ShowQuizResult()
        // — MarkModuleQuizCompleted() below only records the fact and
        // IsUnlockedByModuleOrder() only answers the gating question, the
        // same split OnConfirmClicked() already has with EffectiveStudentIdMode
        // (validated here; the researcher-facing toggle lives on
        // ResearcherAreaController).
        // =================================================================

        private const string CompletedQuizzesPrefKeyPrefix = "MARTPROG_CompletedQuizzes_";

        /// <summary>
        /// PlayerPrefs key for the current participant's completed-quiz set,
        /// or null if no student ID is registered yet — nothing to key it
        /// to. See the block comment above for why this uses
        /// <see cref="RegisteredStudentId"/> specifically.
        /// </summary>
        private static string CompletedQuizzesPrefKey
        {
            get
            {
                string id = RegisteredStudentId;
                return string.IsNullOrEmpty(id) ? null : CompletedQuizzesPrefKeyPrefix + id;
            }
        }

        /// <summary>
        /// Comma-separated moduleKeys — PlayerPrefs' only native string
        /// shape. Empty/unset PlayerPrefs value or no registered ID both
        /// resolve to an empty set, never a set containing "".
        /// </summary>
        private static HashSet<string> LoadCompletedQuizModules()
        {
            var result = new HashSet<string>();

            string key = CompletedQuizzesPrefKey;
            if (key == null) return result;

            string stored = PlayerPrefs.GetString(key, "");
            if (string.IsNullOrEmpty(stored)) return result;

            foreach (string moduleKey in stored.Split(','))
            {
                if (!string.IsNullOrEmpty(moduleKey)) result.Add(moduleKey);
            }

            return result;
        }

        private static void SaveCompletedQuizModules(HashSet<string> completed)
        {
            string key = CompletedQuizzesPrefKey;
            if (key == null) return;

            PlayerPrefs.SetString(key, string.Join(",", completed));
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Called from QuizController.ShowQuizResult() the moment a quiz
        /// finishes — any score, including a timeout — for every module, not
        /// just Memory (compare MemoryController.MarkQuizCompleted(), a
        /// different, Memory-specific progress tracker this is not replacing).
        /// Safe to call for a module already marked, and safe to call with no
        /// student ID registered (a no-op — see CompletedQuizzesPrefKey).
        /// </summary>
        public void MarkModuleQuizCompleted(string moduleKey)
        {
            if (string.IsNullOrEmpty(moduleKey)) return;
            if (CompletedQuizzesPrefKey == null) return;

            HashSet<string> completed = LoadCompletedQuizModules();
            if (completed.Contains(moduleKey)) return;

            completed.Add(moduleKey);
            SaveCompletedQuizModules(completed);
        }

        /// <summary>
        /// True once every BUILT module ahead of <paramref name="entry"/> in
        /// moduleGroups has a completed quiz for the current registered
        /// student. Unbuilt modules are skipped when walking the list — they
        /// can never be selected anyway (the IsBuilt gate in
        /// OnConfirmClicked() already blocks that), so treating them as a
        /// completed step keeps the study runnable while modules are still
        /// being built out, the same partial-build tolerance every other
        /// pass over moduleGroups in this file already has. A module at
        /// index 0, or with nothing built before it, is always unlocked.
        /// </summary>
        private bool IsUnlockedByModuleOrder(ModuleGroupEntry entry)
        {
            HashSet<string> completed = LoadCompletedQuizModules();

            foreach (ModuleGroupEntry candidate in moduleGroups)
            {
                if (candidate == entry) break;
                if (!candidate.IsBuilt) continue;
                if (!completed.Contains(candidate.moduleKey)) return false;
            }

            return true;
        }

        // =================================================================
        // Phase 44 — Continue Learning. NOT A PORT — Backlog §3.6/§4.1 (in
        // the original proposal) and §6.5. Reuses Phase 43's machinery
        // directly: "module completed" for Continue Learning purposes is the
        // exact same completed-quiz signal IsUnlockedByModuleOrder() above
        // gates on, not a second, separate tracker — see Migration Spec §8w.
        // =================================================================

        /// <summary>
        /// The first BUILT module in moduleGroups' order whose quiz the
        /// current registered student hasn't completed yet — "where do you
        /// pick back up," independent of whether EffectiveEnforceModuleOrder
        /// is even on; Continue Learning's own notion of "next" doesn't
        /// depend on whether skipping ahead is currently blocked. Returns
        /// null once every built module is done (or if none are built).
        /// </summary>
        public ModuleGroupEntry GetNextIncompleteModule()
        {
            HashSet<string> completed = LoadCompletedQuizModules();

            foreach (ModuleGroupEntry entry in moduleGroups)
            {
                if (!entry.IsBuilt) continue;
                if (!completed.Contains(entry.moduleKey)) return entry;
            }

            return null;
        }

        /// <summary>
        /// Whether the Landing Page's Continue Learning button should be
        /// shown: a registered participant with at least one built module
        /// still incomplete. Read fresh every time the Landing Page is about
        /// to show (LandingPageController.Show()), never cached — progress
        /// can change from anywhere in a session. Once every built module is
        /// done, this goes false and stays false for that student (matching
        /// the chosen behaviour: hide the button rather than reopen the last
        /// module visited).
        /// </summary>
        public bool CanContinueLearning()
        {
            return !string.IsNullOrEmpty(RegisteredStudentId) && GetNextIncompleteModule() != null;
        }

        /// <summary>
        /// Called from the Landing Page's Continue Learning button. Goes
        /// straight into GetNextIncompleteModule() via the same
        /// ActivateModule() path OnConfirmClicked() uses — no selector, no
        /// dropdown, no Student ID re-entry, since a registered ID and a
        /// target module are both already known. Guards defensively against
        /// both failing (no registered ID, or nothing left to continue) even
        /// though CanContinueLearning() should already have hidden the button
        /// in either case — this method does not assume its caller checked.
        /// </summary>
        public void ContinueLearning()
        {
            string studentId = RegisteredStudentId;
            if (string.IsNullOrEmpty(studentId)) return;

            ModuleGroupEntry target = GetNextIncompleteModule();
            if (target == null) return;

            if (landingPage != null) landingPage.Hide();

            ActivateModule(target, studentId, isResume: true);
        }

        // Phase 37's TEMPORARY About button on this panel is GONE as of Phase
        // 38 — the Landing Page owns that entry point now, which was always
        // the plan (Backlog §5/§7 item 3). The `aboutButton` / `aboutController`
        // fields that carried it have been removed rather than left unassigned,
        // so nothing suggests there is a second way in. The AboutButton
        // GameObject on ModuleSelectorPanel should be deleted from the scene
        // too — see the Phase 38 stage doc.

        [Header("Phase 38 — Landing Page")]
        [Tooltip("The Landing Page, which owns the app's first screen as of Phase 38.\n\n" +
                 "ASSIGNED: this controller hides everything at boot and lets the " +
                 "Landing Page show itself; Start Learning then calls back into " +
                 "OpenSelectorFromLanding().\n\n" +
                 "EMPTY: the app boots straight to this selector exactly as it did " +
                 "from Phase 27 to Phase 37. That fallback is deliberate — it keeps " +
                 "the Godot-matching boot path one Inspector field away, rather than " +
                 "deleted.")]
        [SerializeField] private LandingPageController landingPage;

        [Tooltip("Optional. A Back button on ModuleSelectorPanel, returning to the " +
                 "Landing Page. DEVIATION from source — see porting note 10. Leave " +
                 "empty if you want the Godot behaviour of no way out of this panel " +
                 "except confirming a module.")]
        [SerializeField] private Button selectorBackButton;

        private void Start()
        {
            if (changeModuleButton != null)
                changeModuleButton.onClick.AddListener(OnChangeModuleClicked);

            if (selectorBackButton != null)
                selectorBackButton.onClick.AddListener(OnSelectorBackClicked);

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
            // selector is first shown. Phase 40: no long-press wiring here any
            // more — release now happens from the Researcher Area's own
            // button (ReleaseRegisteredId()), not a gesture on this field.
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

            // Phase 27: the app BOOTS to this selector, matching Godot's
            // _ready() —
            //
            //     student_panel.visible = true
            //     main_ui.visible = false
            //     quiz_panel.visible = false
            //     ar_world.visible = false
            //
            // Before that, Unity started inside whichever module was left
            // enabled in the Hierarchy (Migration Spec §5's single-active-
            // module convention), which is why no Student ID was ever asked
            // for. Everything is hidden here instead, in code, so the scene's
            // enabled/disabled state at edit time no longer decides where the
            // app opens.
            //
            // Phase 38 puts the Landing Page in front of it. The module world
            // is still hidden here — that part is unchanged and still matches
            // _ready() — but the SELECTOR is no longer what appears. Godot has
            // no landing page at all, so this is new behaviour, not a port
            // (see the class comment's porting note 9).
            //
            // Driven from here rather than from LandingPageController.Start()
            // deliberately: Unity does not guarantee Start() ordering between
            // two MonoBehaviours in the same frame, and "which screen does the
            // app open on" must not depend on that. One script decides, and it
            // is the one that already owned the decision.
            if (landingPage != null)
            {
                HideModuleWorld();
                if (moduleSelectorPanel != null) moduleSelectorPanel.SetActive(false);
                landingPage.Show();
            }
            else
            {
                ShowSelector(logEvent: false);
            }
        }

        /// <summary>
        /// Phase 38: the hiding half of ShowSelector(), split out so the
        /// Landing Page can put the app into "nothing running" state at boot
        /// without also opening the selector.
        ///
        /// This is the part that genuinely ports Godot's _ready() — main_ui
        /// and ar_world hidden. What varies is only what gets shown on top.
        /// </summary>
        private void HideModuleWorld()
        {
            foreach (ModuleGroupEntry entry in moduleGroups)
            {
                if (entry.controlStrip != null) entry.controlStrip.SetActive(false);
                if (entry.moduleRoot != null) entry.moduleRoot.SetActive(false);
                if (entry.controllerObject != null) entry.controllerObject.SetActive(false);
            }

            if (dashboardRoot != null) dashboardRoot.SetActive(false);
            if (adminRow != null) adminRow.SetActive(false);
        }

        /// <summary>
        /// Phase 38: entry from the Landing Page's Start Learning button.
        ///
        /// Does NOT log. Godot's boot into student_panel logs nothing either,
        /// and this is the same moment — the student arriving at module choice
        /// for the first time, not returning to it. The "Change Module" row is
        /// reserved for an actual mid-session return, so that a count of those
        /// rows still means what it has always meant.
        /// </summary>
        public void OpenSelectorFromLanding()
        {
            ShowSelector(logEvent: false);
        }

        /// <summary>
        /// Phase 38: Back from the selector to the Landing Page.
        ///
        /// See porting note 10 — this button does not exist in source, and is
        /// optional here.
        /// </summary>
        public void OnSelectorBackClicked()
        {
            if (landingPage == null) return;

            if (moduleSelectorPanel != null) moduleSelectorPanel.SetActive(false);
            landingPage.Show();
        }

        /// <summary>
        /// Godot: the shared body of _ready() and
        /// _on_change_module_button_pressed() — hide the module world, show
        /// the panel, clear the error label. Only the log event differs, so
        /// that is the parameter.
        /// </summary>
        private void ShowSelector(bool logEvent)
        {
            HideModuleWorld();

            // Phase 38: the Landing Page must go away when the selector opens,
            // including on the Change Module path mid-session — otherwise it
            // would still be sitting on top of the selector.
            if (landingPage != null) landingPage.Hide();

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

            // Phase 40: the placeholder has to be refreshed every time this
            // panel is about to be shown, not just once at Start() — see
            // RefreshStudentIdPlaceholder()'s own comment for why a
            // one-time-only version would go stale the moment a researcher
            // uses the new Student ID Mode toggle.
            RefreshStudentIdPlaceholder();

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

            // Phase 51. The module list is the first control a participant
            // ever touches, and it carried the same factory-default option
            // rows as Selection's and Memory's dropdowns. There is no
            // deferred coroutine on this screen, so MatchListToControl()
            // forces the layout pass itself before reading the height.
            //
            // Round 2: ask for every module to be visible at once rather
            // than the default five. This screen is a dedicated panel with
            // room to spare, and with a scrolling list the selected module
            // sat half-cut at the bottom edge — reported from device on
            // "Memory Management". Passing the count rather than a literal
            // means adding a ninth module needs no change here, and
            // MatchListToControl clamps to what the canvas can show.
            DropdownStyler.MatchListToControl(moduleDropdown, options.Count);
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

            // Phase 43: off by default — see EffectiveEnforceModuleOrder's
            // comment. Checked after IsBuilt so an unbuilt module still gets
            // its own "not available yet" message rather than a confusing
            // order message about a module that can't be reached either way.
            if (EffectiveEnforceModuleOrder && !IsUnlockedByModuleOrder(selected))
            {
                if (messageLabel != null)
                {
                    messageLabel.text = "Complete the earlier modules' quizzes before starting " +
                                         selected.displayName + ".";
                }

                return;
            }

            ActivateModule(selected, enteredId, isResume: false);
        }

        /// <summary>
        /// Phase 44: the activation tail of OnConfirmClicked() — everything
        /// from "which GameObjects turn on" through "log the session start
        /// and defer into the controller's own EnterModule()" — extracted
        /// unchanged so ContinueLearning() below can reach the exact same
        /// path a manual dropdown confirm already uses, rather than a second,
        /// parallel copy that could drift from it. <paramref name="isResume"/>
        /// only affects EnterSelectedModuleDeferred()'s Memory-specific
        /// branch (see its own comment) — everything else here behaves
        /// identically for both callers.
        /// </summary>
        private void ActivateModule(ModuleGroupEntry selected, string studentId, bool isResume)
        {
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
                ResearchLogger.Instance.StudentId = studentId;
                ResearchLogger.Instance.ModuleName = selected.moduleKey;
                ResearchLogger.Instance.StartNewSession();

                // Phase 35: register on first successful start, after the
                // session exists so the "Student ID Registered" row lands in
                // the new session rather than the previous one. A no-op on
                // the Continue Learning path — the ID is already registered
                // (ContinueLearning() requires one), same as RegisterStudentId()
                // already no-ops for any already-registered ID.
                RegisterStudentId(studentId);
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
                AppState.Instance.StudentId = studentId;
            }

            StartCoroutine(EnterSelectedModuleDeferred(selected, isResume));
        }

        /// <summary>
        /// See porting note 7 above for why this waits two frames before
        /// calling EnterModule() on the target controller.
        ///
        /// Phase 44: <paramref name="isResume"/> is true only on a Continue
        /// Learning entry (see ContinueLearning() below), false on every
        /// ordinary confirm. When true AND the target is Memory specifically,
        /// MemoryController.ResumeToNextActivity() runs immediately after
        /// EnterModule() — so EnterModule()'s own reset to Overview happens
        /// first, exactly as it always has, and the resume is a second step
        /// on top of it, not a replacement for it. Every other module has no
        /// equivalent step-level concept to resume within (see Migration Spec
        /// §8v/§8w), so isResume changes nothing for them — EnterModule() is
        /// the whole story, same as a manual confirm.
        /// </summary>
        private IEnumerator EnterSelectedModuleDeferred(ModuleGroupEntry selected, bool isResume)
        {
            yield return null;
            yield return null;

            if (selected.controllerObject != null)
            {
                IModuleController controller = selected.controllerObject.GetComponent<IModuleController>();

                if (controller != null)
                {
                    controller.EnterModule();

                    if (isResume && selected.moduleKey == "Memory")
                    {
                        MemoryController memoryController = selected.controllerObject.GetComponent<MemoryController>();
                        memoryController?.ResumeToNextActivity();
                    }
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
