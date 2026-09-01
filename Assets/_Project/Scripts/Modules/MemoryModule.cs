using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using MartProg.Menu;

namespace MartProg.Modules
{
    /// <summary>
    /// Direct port of memory_module.gd — the 3D memory-table visual for the
    /// Memory Management module. Display surface only: it owns the record
    /// list and the drawing, and MemoryController drives which activity is
    /// running. Phase 26 covers the table itself plus the two one-button
    /// demo activities (Data Types, Address Layout).
    ///
    /// ---- Porting notes ----
    ///
    /// 1. **memory_module.gd IS the live implementation — the opposite of
    ///    Selection.** This was checked first, because selection_module.gd
    ///    turned out to be dead code whose state fields nothing ever wrote,
    ///    and reading it as if it were live cost two on-device bugs in
    ///    Phase 25. Memory is not like that: ar_learning_screen.gd calls
    ///    these methods directly and constantly (set_variable_from_text,
    ///    check_trace_answer, inspect_memory_record, step_loop_memory,
    ///    show_data_type_memory_demo, ...) and reads memory_module.memory_records
    ///    for its progress sync. Every conditional in this file can be
    ///    trusted to actually run.
    ///
    /// 2. **The table is a GRID, so it uses ONE scale, not the project's
    ///    usual two.** Every module so far separates cellSizeMultiplier
    ///    (sizes) from spacingMultiplier (positions), which works when the
    ///    content is independent boxes in a row. Here the five column
    ///    positions (ADDRESS_X ... SIZE_X) must stay inside a single row
    ///    background of width ROW_WIDTH, and the labels must stay inside
    ///    their columns. Scaling positions and sizes by different factors
    ///    would slide the columns off the row at any setting other than 1:1.
    ///    So tableSizeMultiplier scales everything uniformly, and
    ///    rowSpacingMultiplier is offered separately for the ONE axis where
    ///    independent adjustment is safe: the vertical gap between rows.
    ///
    /// 3. **MEMORY_VISUAL_Y = 3.05 is not ported**, matching every module
    ///    before it. Godot's absolute Y constants place content in a fixed
    ///    scene; in AR the content is placed relative to the student, so
    ///    arHeight replaces it. Every other Y in the file IS ported, because
    ///    those are relative offsets from that origin and they are what keeps
    ///    the title, header and rows in their correct stack.
    ///
    /// 4. **create_box() builds a StandardMaterial3D — magenta under URP.**
    ///    The same trap Functions, Loops and Selection each hit.
    ///    BuildRowMaterial() resolves a URP Unlit shader with Lit and
    ///    Sprites/Default fallbacks, and an Inspector override. Unlit is
    ///    correct here for the same reason as elsewhere: source sets
    ///    SHADING_MODE_UNSHADED explicitly, and a lit table would read as a
    ///    different shade in every student's room.
    ///
    /// 5. **DELIBERATE CORRECTION: the focused row is tracked by reference,
    ///    not found by colour.** highlight_current_record() locates the row
    ///    to animate by walking the mesh children and testing
    ///    `mat.albedo_color == Color(0.30, 0.22, 0.05, 1.0)` — it identifies
    ///    the row by its highlight tint. That works only because exactly one
    ///    row is ever tinted, and it silently animates the wrong row (the
    ///    first match) the moment that stops being true. The comment above
    ///    it in source even concedes the approach: "Since each row is
    ///    recreated, we use position matching through the highlighted color."
    ///    AddMemoryRow() here simply remembers the focused row's Transform.
    ///    Observably identical, and it cannot pick the wrong row. Logged as
    ///    a correction rather than a quirk because nothing about it is
    ///    visible to a student.
    ///
    /// 6. **apply_memory_table_readability() in ar_learning_screen.gd is
    ///    almost certainly DEAD, and is not ported.** It looks up a child
    ///    node by trying seven candidate paths — "MemoryTableRoot",
    ///    "MemoryTable", "MemoryTable3D", "TableRoot", "Table",
    ///    "MemoryRoot/MemoryTable", "MemoryVisual/MemoryTable" — and returns
    ///    early if none resolve. memory_module.gd's children are
    ///    $MemoryBlocks, $FlowMarkers and $HighlightLayer. None of the seven
    ///    names appears anywhere in the module, and the list reads like
    ///    guesswork. So its responsive font scaling never runs in Godot.
    ///
    ///    Stated as "almost certainly" rather than "definitely" because the
    ///    .tscn scene files were never part of this project's source set, so
    ///    a node with one of those names could in principle exist without the
    ///    script referencing it. Either way the INTENT — a bigger table on a
    ///    phone — is real and worth having in AR, where this is by far the
    ///    largest visual in the app. It is served here by the standard
    ///    arScale / tableSizeMultiplier fields rather than by porting a
    ///    function that does nothing.
    ///
    /// 7. **apply_memory_table_overview_style() is live, but only half of it
    ///    is ported by default.** In Overview it scales the whole module by
    ///    1.45 AND moves it down by 2.90 scene units. The 1.45 is ported
    ///    as-is. The 2.90 drop is exposed as overviewDropUnits but
    ///    **defaults to 0**, which is a deliberate AR adaptation confirmed
    ///    on-device in Phase 26.
    ///
    ///    Source states its own reason in a comment: "Move the enlarged table
    ///    slightly downward so the title is not cut off." That is a framing
    ///    fix for a FIXED camera in a fixed scene — Godot's viewport clips
    ///    the top of the enlarged table, so the table moves down to stay in
    ///    frame. AR has the opposite problem. There is no fixed frame to fall
    ///    out of the top of, and the bottom half of the screen is covered by
    ///    the dashboard, so 2.90 x tableScale (0.35 m at default arScale)
    ///    pushes the entire table down behind the dashboard panels. That is
    ///    exactly what the first on-device run showed.
    ///
    ///    Same class of adaptation as FunctionModule's layoutXOffset and the
    ///    unported MEMORY_VISUAL_Y: a Godot constant that exists to satisfy a
    ///    fixed camera has no meaning once the student holds the camera.
    ///
    /// 8. **Records are Dictionaries with optional keys in Godot.** A row may
    ///    or may not carry "array"/"index" (array elements), "frame"/"role"
    ///    (function frames), "scope" (block locals) or "byte_count" (address
    ///    layout). Source tests them with `str(record.get("array", "")) != ""`.
    ///    MemoryRecord is a class with empty-string defaults so
    ///    string.IsNullOrEmpty reproduces that test exactly, rather than a
    ///    Dictionary that would carry Godot's untyped access into C# for no
    ///    benefit.
    ///
    /// 9. **Font sizes are ported literally, but the label SCALE is
    ///    derived** — this is Phase 25's lesson applied properly rather than
    ///    ignored. Godot Label3D reaches a world size from font_size paired
    ///    with pixel_size: a font_size 22 label at pixel_size 0.010 stands
    ///    0.22 scene units tall, against a ROW_HEIGHT of 0.52. TMP instead
    ///    renders roughly 0.1 world units per fontSize unit at scale 1. So
    ///    keeping source's font numbers and setting the label object's scale
    ///    to (pixel_size / 0.1) x tableScale reproduces Godot's proportions
    ///    exactly. labelPixelScale = 0.1 is that conversion for the 0.010
    ///    labels; markers use pixel_size 0.0105, which is why
    ///    markerScaleMultiplier defaults to 1.05 rather than being folded
    ///    away as a rounding difference.
    ///
    /// 10. **The action marker sits OUTSIDE the table, and that is a real AR
    ///     concern.** Source places it at x = 4.25 while the row is only
    ///     ROW_WIDTH = 6.80 wide, so it hangs 0.85 units past the right edge
    ///     and takes the content's full width past 8.5 units — wider than
    ///     anything else in the app by a large margin. Ported faithfully, but
    ///     markerXOffset is exposed so it can be pulled inward if it falls
    ///     outside the student's view on a phone. Watch for this on device.
    ///
    /// 14. **The action marker cannot fit beside the table on a phone, so by
    ///     default it goes below it.** Source places the focus marker and the
    ///     action badge at x = 4.25 against a row spanning +/-3.40 — that is
    ///     25% further out than the row's own edge, and the ratio is fixed.
    ///     So whenever the table is scaled to fill the screen width, the
    ///     marker is by definition off the screen. Squeezing with
    ///     columnSpreadMultiplier does not help: it moves both by the same
    ///     factor and the 25% overhang is preserved exactly. Reported from
    ///     device as "ALL..." and "new" clipped at the right edge.
    ///
    ///     placeMarkerBelowTable puts it under the last row instead, centred,
    ///     one row-step clear of the table however many records are showing.
    ///     Nothing is lost by this: the FOCUSED ROW IS ALREADY TINTED, so the
    ///     row identity was never the marker's job — the marker carries the
    ///     message ("UPDATED 20 -> 25", "REMOVED out of scope"), and a message
    ///     reads just as well below the thing it describes as beside it.
    ///
    ///     Turn it off for source's exact placement on a display wide enough
    ///     to take it.
    ///
    /// 13. **The table is too wide for a phone in portrait, and the fix is
    ///     to change its ASPECT, not its size.** Source's ROW_WIDTH is 6.80
    ///     scene units with columns at x = -2.70 to +2.55 and the action
    ///     marker at +4.25. At the default arScale that is roughly 0.8 m of
    ///     content, and a phone's HORIZONTAL field of view in portrait is
    ///     narrow — far narrower than its vertical one. The ADDRESS column
    ///     falls off the left edge, which is exactly what the first on-device
    ///     run showed.
    ///
    ///     Shrinking the whole table (tableSizeMultiplier) or pushing it
    ///     further away (arDistance) both fit it, and both do so by making
    ///     the TEXT smaller in the same proportion — they are the same
    ///     trade in different clothes, because angular size is what the eye
    ///     sees. Neither buys anything the other does not.
    ///
    ///     columnSpreadMultiplier does something the other two cannot: it
    ///     narrows the row and moves the columns inward TOGETHER while
    ///     leaving row height, font size and label scale alone. The table
    ///     stops being a wide thin strip and becomes a proportionally
    ///     narrower one, with the text as legible as it was. Because the row
    ///     width and the column positions move by the same factor, the
    ///     columns stay inside the row at any setting — the constraint
    ///     porting note 2 exists to protect.
    ///
    ///     Worth knowing there is free room before anything is squeezed: the
    ///     outermost labels sit at -2.70 and +2.55 inside a row spanning
    ///     -3.40 to +3.40, so about 20% of source's width is empty margin.
    ///     The first fifth of the squeeze costs nothing at all.
    ///
    ///     Defaults to 0.75 rather than source's 1.0 because 1.0 is known
    ///     not to fit a phone. Set it to 1 for source-exact proportions on a
    ///     wide display.
    ///
    /// 12. **Destroy() is deferred; Godot's free() is not — and this
    ///     module redraws often enough for that to matter.** clear_visuals()
    ///     calls child.free(), which unparents and frees the node on the
    ///     spot. Unity's Destroy() only queues the object for removal at the
    ///     end of the frame, so it is still a child and still rendering until
    ///     then. Every module before this one used plain Destroy() safely
    ///     because each redraws at most once per operation. Memory does not:
    ///     add_or_update_variable() draws, and highlight_current_record()
    ///     then draws twice more, all within the same frame. Two complete
    ///     overlapping copies of the table would z-fight for that frame.
    ///     ClearChildren() therefore deactivates and detaches each child
    ///     before destroying it, reproducing free()'s observable behaviour.
    ///
    /// 11. **no_depth_test = true is not reproduced.** Every Label3D in
    ///     source sets it, so labels draw over the geometry regardless of
    ///     depth. In Unity that needs a custom material per label, and it is
    ///     not needed: labels sit at z = 0.18 in front of boxes only 0.16
    ///     deep, so they clear the geometry on their own. Revisit only if
    ///     labels are ever seen z-fighting with their row.
    /// </summary>
    public class MemoryModule : MonoBehaviour
    {
        // -----------------------------------------------------------------
        // Record model — see porting note 8
        // -----------------------------------------------------------------

        /// Godot: the untyped Dictionary appended to memory_records.
        public sealed class MemoryRecord
        {
            public string Address = "";
            public string Name = "";
            public string Type = "";
            public string Value = "";
            public string Size = "";

            // Optional keys. Empty string / -1 means "key absent" in source.
            public string ArrayName = "";
            public int Index = -1;
            public string Frame = "";
            public string Role = "";
            public string Scope = "";
            public int ByteCount = -1;

            public MemoryRecord Clone()
            {
                return new MemoryRecord
                {
                    Address = Address,
                    Name = Name,
                    Type = Type,
                    Value = Value,
                    Size = Size,
                    ArrayName = ArrayName,
                    Index = Index,
                    Frame = Frame,
                    Role = Role,
                    Scope = Scope,
                    ByteCount = ByteCount
                };
            }
        }

        // -----------------------------------------------------------------
        // Inspector
        // -----------------------------------------------------------------

        [Header("Scene references")]
        [Tooltip("Godot: $MemoryBlocks — row backgrounds and their five column labels.")]
        [SerializeField] private Transform memoryBlocks;

        [Tooltip("Godot: $FlowMarkers — title, header row and the empty-table message.")]
        [SerializeField] private Transform flowMarkers;

        [Tooltip("Godot: $HighlightLayer — the focus marker and the global action badge.")]
        [SerializeField] private Transform highlightLayer;

        [Header("AR placement")]
        [SerializeField] private float arScale = 0.12f;
        [SerializeField] private float arDistance = 0.5f;
        [SerializeField] private float arHeight = 0f;

        [Tooltip("Uniform scale for the whole table — see porting note 2. " +
                 "Positions AND sizes use this together, so the columns stay " +
                 "inside their rows at any value.")]
        [SerializeField] private float tableSizeMultiplier = 1.0f;

        [Tooltip("The ONE axis it is safe to adjust independently: the " +
                 "vertical gap between rows. 1 reproduces source exactly.")]
        [SerializeField] private float rowSpacingMultiplier = 1.0f;

        [Tooltip("Squeezes the table HORIZONTALLY without changing text size or " +
                 "row height — see porting note 13. Scales the row width, the five " +
                 "column positions and the marker offset together, so columns stay " +
                 "inside their rows. 1 reproduces source's proportions exactly; " +
                 "0.75 is the default because source's table is too wide for a " +
                 "phone held in portrait. Lower it until the ADDRESS column is " +
                 "fully visible; raise it if columns start to crowd.")]
        [SerializeField] private float columnSpreadMultiplier = 0.75f;

        [Tooltip("Godot pixel_size 0.010 converted to Unity world-text scale. " +
                 "TMP renders about 0.1 world units per fontSize unit at " +
                 "scale 1, so 0.010 / 0.1 = 0.1 — see porting note 9.")]
        [SerializeField] private float labelPixelScale = 0.1f;

        [Tooltip("Markers use Godot pixel_size 0.0105 rather than 0.010.")]
        [SerializeField] private float markerScaleMultiplier = 1.05f;

        [Tooltip("Godot: the 4.25 x-offset of the focus marker and action " +
                 "badge, which places it OUTSIDE the table — see porting " +
                 "note 10. Used only when Place Marker Below Table is off.")]
        [SerializeField] private float markerXOffset = 4.25f;

        [Tooltip("Places the focus marker and action badge BELOW the table, " +
                 "centred, instead of beside it — see porting note 14. On by " +
                 "default because source's side placement cannot fit a phone: " +
                 "the marker sits 25% further out than the row's own edge, so " +
                 "whenever the row fills the screen the marker is off it. The " +
                 "highlighted row still shows WHICH record; the marker only " +
                 "says what happened to it. Turn off for source's placement.")]
        [SerializeField] private bool placeMarkerBelowTable = true;

        [Header("Overview framing (porting note 7)")]
        [SerializeField] private float overviewScaleMultiplier = 1.45f;

        [Tooltip("Godot moves the enlarged Overview table down 2.90 scene " +
                 "units so its title is not cut off by a FIXED camera. " +
                 "Defaults to 0 here — see porting note 7. Set it to 2.90 to " +
                 "reproduce source's own framing.")]
        [SerializeField] private float overviewDropUnits;

        [Header("Colours (all from memory_module.gd)")]
        [SerializeField] private Color titleColor = new Color(1.0f, 0.85f, 0.1f, 1f);
        [SerializeField] private Color headerRowColor = new Color(0.08f, 0.08f, 0.08f, 1f);
        [SerializeField] private Color headerLabelColor = new Color(0.4f, 0.8f, 1.0f, 1f);
        [SerializeField] private Color valueColor = new Color(0.4f, 1.0f, 0.55f, 1f);
        [SerializeField] private Color rowColor = new Color(0.14f, 0.14f, 0.14f, 1f);
        [SerializeField] private Color focusRowColor = new Color(0.30f, 0.22f, 0.05f, 1f);
        [SerializeField] private Color emptyMessageColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        [SerializeField] private Color markerColor = new Color(1.0f, 0.85f, 0.1f, 1f);

        [Header("Materials / text")]
        [Tooltip("Optional. Leave empty and a URP Unlit material is built at " +
                 "runtime — see porting note 4.")]
        [SerializeField] private Material rowMaterialOverride;

        [SerializeField] private float labelOutlineWidth = 0.2f;
        [SerializeField] private float markerOutlineWidth = 0.25f;

        // -----------------------------------------------------------------
        // Godot constants (MEMORY_VISUAL_Y deliberately absent — note 3)
        // -----------------------------------------------------------------

        // PHASE 50 — two-line records. Godot's originals, kept for reference
        // since every value below is derived from them:
        //     RowHeight 0.52, RowWidth 6.80
        //     AddressX -2.70, NameX -1.35, TypeX -0.15, ValueX 1.10, SizeX 2.55
        //
        // Those five columns on ONE line made this table 6.80 units wide, and
        // Phase 49's measurements showed that was simply more than a phone's
        // horizontal AR field of view can render legibly: 0.90 m of content
        // against 0.81 m of visible width (110.7%), the only module of eight
        // that genuinely overflowed. Phase 49 could only redistribute a fixed
        // budget by scaling, which bought fit at the cost of legibility.
        //
        // This splits each record across two lines instead, which trades the
        // axis that is scarce for the axis that is abundant — the same device
        // measures 34.8 deg horizontally against 69.7 deg vertically in
        // portrait, and this table was using almost none of the latter.
        //
        //     line 1:   ADDRESS      NAME      TYPE
        //     line 2:   VALUE                  SIZE
        //
        // Column spacing is deliberately preserved at 1.30 units against the
        // original's 1.35 — the point is to need less width, NOT to crowd
        // columns closer, which is what Phase 45 Round 2 tried and Phase 47
        // reverted. Width drops to 4.10 units (60% of 6.80) purely because
        // there are three columns per line instead of five.
        // SINGLE-LINE, as Godot had it. A two-line reflow was built and
        // tested here (Phase 50) and then REVERTED on device evidence —
        // keeping the account, because the reasoning was sound and the
        // conclusion still surprised:
        //
        // The reflow worked exactly as designed. Splitting each record over
        // two lines cut the table from 6.80 to 4.10 units wide, which let
        // the fit scale rise from ~0.75 to 1.20-1.40, making the text
        // roughly 60% larger on screen. Every number moved the right way.
        //
        // It was still worse to read. Five columns running across is the
        // natural form for a table: the eye scans a row as one record, and
        // columns align down the page. Breaking each record into two lines
        // destroys both of those, and no amount of extra point size buys
        // that back. Compared side by side on device, the single-line
        // version was plainly clearer despite its smaller text.
        //
        // **The lesson worth keeping: text size and legibility are not the
        // same quantity.** This phase optimised the one that was easy to
        // measure. Memory's text really is small — that is a genuine
        // consequence of a 35-degree horizontal field of view, and the fit
        // already extracts everything available from it. The remaining
        // lever is less horizontal content, not more scale and not reflow.
        private const float RowHeight = 0.52f;
        private const float RowGap = 0.11f;
        private const float RowWidth = 6.80f;
        private const float RowDepth = 0.16f;

        private const float TitleY = 0.85f;
        private const float HeaderY = 0.35f;
        private const float FirstRowY = -0.25f;
        private const float HeaderHeight = 0.36f;

        private const float AddressX = -2.70f;
        private const float NameX = -1.35f;
        private const float TypeX = -0.15f;
        private const float ValueX = 1.10f;
        private const float SizeX = 2.55f;

        // Z offsets, ported as-is and scaled with everything else (note 11).
        private const float LabelZ = 0.18f;
        private const float MarkerZ = 0.55f;

        // Font sizes, straight from source (note 9).
        private const int TitleFontSize = 30;
        private const int HeaderFontSize = 20;
        private const int AddressFontSize = 22;
        private const int NameFontSize = 23;
        private const int TypeFontSize = 22;
        private const int ValueFontSize = 24;
        private const int SizeFontSize = 20;
        private const int EmptyMessageFontSize = 26;
        private const int MarkerFontSize = 28;

        // -----------------------------------------------------------------
        // State — mirrors memory_module.gd's top-of-file vars
        // -----------------------------------------------------------------

        private readonly List<MemoryRecord> memoryRecords = new List<MemoryRecord>();

        private bool isAnimating;

        private string currentFocusName = "";
        private string currentFocusAction = "";
        private string currentPreviousValue = "";
        private string currentNewValue = "";
        private string currentCodeLine = "Program not started.";
        private string lastActionMessage = "";
        private string removedMemoryName = "";
        private string removedScopeName = "";

        // Note 5: the focused row, remembered rather than colour-matched.
        private Transform focusedRow;

        // Overview framing state (note 7).
        private bool defaultTransformSaved;
        private Vector3 defaultLocalScale;
        private Vector3 defaultLocalPosition;

        // -----------------------------------------------------------------
        // Public surface
        // -----------------------------------------------------------------

        public IReadOnlyList<MemoryRecord> Records => memoryRecords;
        public bool IsAnimating => isAnimating;
        public string CurrentFocusAction => currentFocusAction;

        private float TableScale => arScale * tableSizeMultiplier;

        /// Guarded so a zero or negative value in the Inspector collapses the
        /// table to a line rather than inverting it.
        ///
        /// Phase 45 Round 2 grew this with CurrentTextScale to relieve
        /// ADDRESS/NAME header crowding at Extra Large. Phase 47 reverts
        /// that: real on-device testing (a Samsung Galaxy A32, 1080x2400,
        /// 20:9) showed the wider spread pushed the ADDRESS column half off
        /// the LEFT edge of the screen, which is worse than the crowding it
        /// fixed — a column missing entirely is worse than two headers
        /// sitting close together. See the class comment's porting note 13:
        /// this table was already tuned right up against a phone's narrow
        /// horizontal AR field of view even at Normal size ("about 20%
        /// margin", by design, no room to spare). ColumnSpread is a true
        /// no-op at every text scale again — the table's rendered width can
        /// never exceed what already fit at Normal. AddHeaderRow()'s own
        /// headerTextScale (below) relieves the original crowding a
        /// different way instead, without widening the table.
        private float ColumnSpread => columnSpreadMultiplier;

        /// Source's ROW_WIDTH, narrowed by the horizontal squeeze.
        private float ScaledRowWidth => RowWidth * ColumnSpread;

        private void Awake()
        {
            // Godot: func _ready(): reset_memory()
            ResetMemory();
        }

        // Godot: func clear_visuals()
        private void ClearVisuals()
        {
            ClearChildren(memoryBlocks);
            ClearChildren(flowMarkers);
            ClearChildren(highlightLayer);

            focusedRow = null;
        }

        /// Porting note 12: Godot's free() removes a node immediately;
        /// Unity's Destroy() defers to the end of the frame. So the child is
        /// deactivated and detached first, and only then destroyed.
        private void ClearChildren(Transform parent)
        {
            if (parent == null) return;

            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);

                child.gameObject.SetActive(false);
                child.SetParent(null, false);

                Destroy(child.gameObject);
            }
        }

        // Godot: func reset_memory()
        public void ResetMemory()
        {
            if (isAnimating) return;

            memoryRecords.Clear();
            resetExerciseBanksFully = true;
            ResetStepCounters();

            currentFocusName = "";
            currentFocusAction = "";
            currentPreviousValue = "";
            currentNewValue = "";
            removedMemoryName = "";
            removedScopeName = "";

            currentCodeLine = "Program not started.";
            lastActionMessage = "Memory has been cleared.";

            // reset_memory() clears the inspection message;
            // clear_activity_memory() deliberately does not. Preserved.
            inspectionMessage = "";

            DrawMemory(true);
        }

        // Godot: func clear_activity_memory()
        // Distinct from reset_memory(): source uses this when the ACTIVITY
        // changes, and its message differs ("Activity memory has been
        // cleared." vs "Memory has been cleared."). Both clear the same
        // state; the message is what the dashboard shows, so the difference
        // is visible and is preserved.
        public void ClearActivityMemory()
        {
            if (isAnimating) return;

            memoryRecords.Clear();
            resetExerciseBanksFully = false;
            ResetStepCounters();

            currentFocusName = "";
            currentFocusAction = "";
            currentPreviousValue = "";
            currentNewValue = "";
            removedMemoryName = "";
            removedScopeName = "";

            currentCodeLine = "Activity memory cleared.";
            lastActionMessage = "Activity memory has been cleared.";

            DrawMemory(true);
        }

        /// Godot: reset_memory() and clear_activity_memory() both zero every
        /// per-activity step counter. Shared here so a counter added in a
        /// later phase cannot be forgotten in one of the two.
        private bool resetExerciseBanksFully;

        private void ResetStepCounters()
        {
            functionStep = 0;
            selectionStep = 0;
            scopeStep = 0;
            accessStep = 0;
            loopMemoryStep = 0;
            removeMemoryStep = 0;
            executionStep = 0;
            activeFunctionName = "";

            // Godot resets the whole prediction block in both reset_memory()
            // and clear_activity_memory().
            awaitingPrediction = false;
            pendingMemoryStep = 0;
            predictionPrompt = "";
            expectedPrediction = "";
            pendingCodeLine = "";

            // clear_activity_memory() clears only the awaiting flags;
            // reset_memory() also resets indices, current items and the
            // Final score. ResetStepCounters() runs from both, so the
            // caller decides which.
            ResetExerciseState(resetExerciseBanksFully);
        }

        // Godot: func draw_memory(force_redraw := false)
        public void DrawMemory(bool forceRedraw = false)
        {
            if (isAnimating && !forceRedraw) return;

            ClearVisuals();

            float rowStep = RowStep;

            AddTitle("MEMORY TABLE", 0f, TitleY, MarkerZ, titleColor);
            AddHeaderRow(HeaderY);

            if (memoryRecords.Count == 0)
            {
                AddEmptyMessage(FirstRowY);
                return;
            }

            for (int i = 0; i < memoryRecords.Count; i++)
            {
                float rowY = FirstRowY - i * rowStep;
                MemoryRecord record = memoryRecords[i];

                bool isFocus = currentFocusName != "" && record.Name == currentFocusName;

                AddMemoryRow(record, rowY, isFocus);
            }

            // Godot: the global badge branches, for actions with no row to
            // attach to. Note these overlap the focus-marker texts below;
            // source repeats them deliberately because a REMOVED row no
            // longer exists to carry its own marker.
            if (currentFocusAction == "REMOVED_VARIABLE")
            {
                AddGlobalActionBadge("REMOVED\n" + removedMemoryName);
            }
            else if (currentFocusAction == "LOOP_REMOVED")
            {
                AddGlobalActionBadge("REMOVED\ni");
            }
            else if (currentFocusAction == "SCOPE_REMOVED")
            {
                AddGlobalActionBadge("REMOVED\n" + removedScopeName);
            }
            else if (currentFocusAction == "CONDITION_TRUE")
            {
                AddGlobalActionBadge("CONDITION\nTRUE");
            }
            else if (currentFocusAction == "CONDITION_FALSE")
            {
                AddGlobalActionBadge("CONDITION\nFALSE");
            }
            else if (currentFocusAction == "BRANCH_SKIPPED")
            {
                AddGlobalActionBadge("BRANCH\nSKIPPED");
            }
        }

        // -----------------------------------------------------------------
        // Drawing helpers
        // -----------------------------------------------------------------

        /// Converts a Godot table-space coordinate to Unity local space.
        /// Note the Z inversion: Godot's +Z on this table brings content
        /// toward the viewer, Unity's +Z pushes it away, so it is subtracted
        /// from arDistance.
        private Vector3 Place(float x, float y, float z)
        {
            float s = TableScale;
            return new Vector3(
                x * ColumnSpread * s,
                arHeight + y * s,
                arDistance - z * s);
        }

        /// Vertical distance between one record block and the next. Phase 50
        /// centralises this: it was computed identically in DrawMemory() and
        /// BelowTableY(), and two-line records change it, so a single
        /// definition removes the chance of the marker and the table
        /// disagreeing about where the bottom of the table is.
        private float RowStep => (RowHeight + RowGap) * rowSpacingMultiplier;

        // Godot: func add_header_row(row_position: Vector3)
        private void AddHeaderRow(float rowY)
        {
            if (flowMarkers == null) return;

            CreateBox(
                new Vector3(ScaledRowWidth, HeaderHeight, RowDepth),
                Place(0f, rowY, 0f),
                headerRowColor,
                flowMarkers,
                "MemoryHeaderRow");

            // Phase 47: headers grow at HALF the accessibility scale's
            // rate, not the full rate the data rows below get (see
            // AddMemoryRow()). Headers are five short, fixed, already-bold
            // category labels a participant reads once and remembers —
            // the DATA is what the accessibility setting exists to help
            // with. Slowing header growth relieves the ADDRESS/NAME
            // crowding Phase 45 Round 2 found, without needing to widen
            // ColumnSpread again (see that property's comment for why
            // widening it was reverted instead of tuned further).
            float headerTextScale = 1f + (LandingPageController.CurrentTextScale - 1f) * 0.5f;

            AddTextLabel("ADDRESS", AddressX, rowY, LabelZ, HeaderFontSize, headerLabelColor, flowMarkers, headerTextScale);
            AddTextLabel("NAME", NameX, rowY, LabelZ, HeaderFontSize, headerLabelColor, flowMarkers, headerTextScale);
            AddTextLabel("TYPE", TypeX, rowY, LabelZ, HeaderFontSize, headerLabelColor, flowMarkers, headerTextScale);

            // Source colours the VALUE header green like the value cells,
            // unlike the other four headers. Preserved.
            AddTextLabel("VALUE", ValueX, rowY, LabelZ, HeaderFontSize, valueColor, flowMarkers, headerTextScale);

            AddTextLabel("SIZE", SizeX, rowY, LabelZ, HeaderFontSize, headerLabelColor, flowMarkers, headerTextScale);
        }

        // Godot: func add_memory_row(record, row_position, is_focus)
        private void AddMemoryRow(MemoryRecord record, float rowY, bool isFocus)
        {
            if (memoryBlocks == null) return;

            Color color = isFocus ? focusRowColor : rowColor;

            Transform rowBg = CreateBox(
                new Vector3(ScaledRowWidth, RowHeight, RowDepth),
                Place(0f, rowY, 0f),
                color,
                memoryBlocks,
                "MemoryRow_" + record.Name);

            AddTextLabel(record.Address, AddressX, rowY, LabelZ, AddressFontSize, Color.white, memoryBlocks);
            AddTextLabel(record.Name, NameX, rowY, LabelZ, NameFontSize, Color.white, memoryBlocks);
            AddTextLabel(record.Type, TypeX, rowY, LabelZ, TypeFontSize, Color.white, memoryBlocks);
            AddTextLabel(record.Value, ValueX, rowY, LabelZ, ValueFontSize, valueColor, memoryBlocks);
            AddTextLabel(record.Size, SizeX, rowY, LabelZ, SizeFontSize, Color.white, memoryBlocks);

            if (isFocus)
            {
                // Note 5: remember it instead of finding it by colour later.
                focusedRow = rowBg;
                AddFocusMarker(rowY);
            }
        }

        // Godot: func add_focus_marker(row_position: Vector3)
        private void AddFocusMarker(float rowY)
        {
            string text = FocusMarkerText();

            if (placeMarkerBelowTable)
            {
                AddMarkerLabel(text, 0f, BelowTableY(), MarkerZ);
                return;
            }

            // Source nudges ADDRESS_LAYOUT's marker up by 0.22 and leaves
            // every other action at row height. Preserved.
            float yOffset = currentFocusAction == "ADDRESS_LAYOUT" ? 0.22f : 0f;

            AddMarkerLabel(text, markerXOffset, rowY + yOffset, MarkerZ);
        }

        /// One row-step below the last row, so the marker clears the table
        /// however many records are on screen. Porting note 14.
        private float BelowTableY()
        {
            return FirstRowY - memoryRecords.Count * RowStep;
        }

        /// Godot: the long if/elif chain inside add_focus_marker(). Split out
        /// so the geometry above stays readable. Every branch ported verbatim,
        /// including the "\n" line breaks, which are what give the marker its
        /// two-line shape.
        private string FocusMarkerText()
        {
            if (currentFocusAction == "UPDATED" && currentPreviousValue != "")
            {
                // Source uses a real arrow glyph here, not "->".
                return "UPDATED\n" + currentPreviousValue + " → " + currentNewValue;
            }

            switch (currentFocusAction)
            {
                case "ALLOCATED_ARRAY": return "ARRAY\nALLOCATED";
                case "FRAME_ALLOCATED": return "FUNCTION\nFRAME";
                case "RETURN_VALUE": return "RETURN\n" + currentNewValue;
                case "RETURN_STORED": return "RETURN\nSTORED";
                case "CONDITION_TRUE": return "CONDITION\nTRUE";
                case "CONDITION_FALSE": return "CONDITION\nFALSE";
                case "BRANCH_SKIPPED": return "BRANCH\nSKIPPED";
                case "ALLOCATED": return "ALLOCATED\nnew variable";
                case "INSPECTED": return "INSPECTED";
                case "TRACE_RESULT": return "TRACE\nRESULT";
                case "DEBUG_RESULT": return "DEBUG\nRESULT";
                case "FINAL_ASSESSMENT_RESULT": return "FINAL\nRESULT";
                case "SCOPE_LOCAL": return "LOCAL\nSCOPE";
                case "SCOPE_REMAINS": return "REMAINS\nIN MEMORY";
                case "SCOPE_REMOVED": return "REMOVED\nout of scope";
                case "ADDRESS_LAYOUT": return "ADDRESS\nLAYOUT";
                case "READ": return "READ\n" + currentFocusName + " = " + currentNewValue;
                case "LOOP_COMPLETE": return "LOOP\nCOMPLETE";
                case "LOOP_REMOVED": return "REMOVED\nloop counter";
                case "REMOVED_VARIABLE": return "REMOVED\n" + removedMemoryName;
                case "REMAINS_AFTER_REMOVE": return "REMAINS\nIN MEMORY";

                // Godot's final else. Note this is what an UPDATED action
                // with an empty previous value falls through to, since the
                // UPDATED branch above requires current_previous_value != "".
                default: return "ACTIVE";
            }
        }

        // Godot: func add_global_action_badge(message_text: String)
        // Positioned against the table origin rather than a row, because the
        // row it describes has just been removed.
        private void AddGlobalActionBadge(string text)
        {
            if (placeMarkerBelowTable)
            {
                AddMarkerLabel(text, 0f, BelowTableY(), MarkerZ);
                return;
            }

            // Sits at header height, offset sideways by markerXOffset — the
            // same 0.35 the header uses, now via the shared constant so the
            // two cannot drift apart.
            AddMarkerLabel(text, markerXOffset, HeaderY, MarkerZ);
        }

        // Godot: func add_empty_message(message_position: Vector3)
        private void AddEmptyMessage(float rowY)
        {
            AddTextLabel(
                "No variables allocated yet.",
                0f, rowY, MarkerZ,
                EmptyMessageFontSize,
                emptyMessageColor,
                flowMarkers);
        }

        // Godot: func add_title(text_value, title_position, title_color)
        private void AddTitle(string text, float x, float y, float z, Color color)
        {
            AddTextLabel(text, x, y, z, TitleFontSize, color, flowMarkers);
        }

        // Godot: func create_box(box_size, box_position, box_color)
        private Transform CreateBox(Vector3 size, Vector3 position, Color color, Transform parent, string name)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;

            // Decorative only, like every generated visual in this project.
            Collider collider = box.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localRotation = Quaternion.identity;
            box.transform.localScale = size * TableScale;

            Renderer renderer = box.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material material = BuildRowMaterial();
                material.color = color;
                renderer.material = material;
            }

            return box.transform;
        }

        /// Porting note 4 — URP-safe replacement for source's
        /// StandardMaterial3D with SHADING_MODE_UNSHADED.
        private Material BuildRowMaterial()
        {
            if (rowMaterialOverride != null)
            {
                // Instanced so tinting one row never edits the shared asset.
                return new Material(rowMaterialOverride);
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            return new Material(shader);
        }

        // Godot: func add_text_label(text_value, label_position, font_size, label_color, parent_node)
        // Phase 47: textScaleOverride lets a caller (the header row) opt
        // out of the full CurrentTextScale multiplier — see AddHeaderRow()'s
        // headerTextScale for why. null (the default) preserves every
        // existing caller's Phase 45 behaviour exactly, including every
        // data row through AddMemoryRow().
        private TextMeshPro AddTextLabel(
            string text, float x, float y, float z, int fontSize, Color color, Transform parent,
            float? textScaleOverride = null)
        {
            return BuildLabel(text, x, y, z, fontSize, color, parent, labelPixelScale, labelOutlineWidth, textScaleOverride);
        }

        /// Markers differ from labels only in pixel_size (0.0105 vs 0.010)
        /// and outline_size (10 vs 8) — porting note 9.
        private void AddMarkerLabel(string text, float x, float y, float z)
        {
            BuildLabel(
                text, x, y, z, MarkerFontSize, markerColor, highlightLayer,
                labelPixelScale * markerScaleMultiplier, markerOutlineWidth, null);
        }

        private TextMeshPro BuildLabel(
            string text, float x, float y, float z, int fontSize, Color color,
            Transform parent, float pixelScale, float outlineWidth, float? textScaleOverride)
        {
            if (parent == null) return null;

            GameObject labelObj = new GameObject("MemoryLabel");
            labelObj.transform.SetParent(parent, false);
            labelObj.transform.localPosition = Place(x, y, z);
            labelObj.transform.localRotation = Quaternion.identity;
            labelObj.transform.localScale = Vector3.one * pixelScale * TableScale;

            TextMeshPro tmp = labelObj.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = fontSize * (textScaleOverride ?? LandingPageController.CurrentTextScale); // Phase 45/47 — the
            // one choke point both AddTextLabel() and AddMarkerLabel() route
            // through, so every row (address, value, header, marker) in this
            // table scales, headers at their own reduced rate.
            tmp.color = color;
            tmp.outlineWidth = outlineWidth;
            tmp.outlineColor = Color.black;
            tmp.alignment = TextAlignmentOptions.Center;

            // Phase 25b's finding: Label3D has no rect and never wraps, while
            // TMP defaults to a 200-unit rect. Column text like
            // "REMAINS\nIN MEMORY" and "0x1000" is short, but "No variables
            // allocated yet." is not, and it wrapped without this.
            tmp.rectTransform.sizeDelta = new Vector2(600f, 100f);
            tmp.overflowMode = TextOverflowModes.Overflow;

            // Godot: billboard = BILLBOARD_ENABLED on every label.
            labelObj.AddComponent<Billboard>();

            return tmp;
        }

        // -----------------------------------------------------------------
        // Highlight animation — Godot: func highlight_current_record()
        // -----------------------------------------------------------------

        public IEnumerator HighlightCurrentRecord()
        {
            if (currentFocusName == "") yield break;

            isAnimating = true;

            DrawMemory(true);

            yield return new WaitForSeconds(0.12f);

            // Note 5: source scans meshes for the highlight colour here.
            // AddMemoryRow() already handed us the row.
            if (focusedRow != null)
            {
                Vector3 originalScale = focusedRow.localScale;

                yield return ScaleTo(focusedRow, originalScale * 1.04f, 0.16f, true);
                yield return ScaleTo(focusedRow, originalScale, 0.16f, false);
            }

            isAnimating = false;

            DrawMemory(true);
        }

        private IEnumerator ScaleTo(Transform target, Vector3 endScale, float duration, bool easeOut)
        {
            if (target == null) yield break;

            Vector3 startScale = target.localScale;
            float t = 0f;

            while (t < duration)
            {
                if (target == null) yield break;

                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / duration);
                float eased = easeOut ? EaseOutBack(p) : EaseInBack(p);
                target.localScale = Vector3.LerpUnclamped(startScale, endScale, eased);
                yield return null;
            }

            if (target != null) target.localScale = endScale;
        }

        private float EaseOutBack(float x)
        {
            float c1 = 1.70158f;
            float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3) + c1 * Mathf.Pow(x - 1f, 2);
        }

        private float EaseInBack(float x)
        {
            float c1 = 1.70158f;
            float c3 = c1 + 1f;
            return c3 * x * x * x - c1 * x * x;
        }

        // -----------------------------------------------------------------
        // Type / address helpers — ported verbatim
        // -----------------------------------------------------------------

        // Godot: func get_type_size(type_name) -> String
        public static string GetTypeSize(string typeName)
        {
            switch (typeName)
            {
                case "int": return "4 bytes";
                case "float": return "4 bytes";
                case "double": return "8 bytes";
                case "char": return "1 byte";
                case "bool": return "1 byte";
                case "string": return "varies";
                default: return "unknown";
            }
        }

        /// Godot: func get_type_byte_count(type_name) -> int
        /// Note the inconsistency with GetTypeSize above, preserved: "string"
        /// displays as "varies" but counts as 8 bytes for address arithmetic,
        /// and an unknown type displays "unknown" but counts as 4.
        public static int GetTypeByteCount(string typeName)
        {
            switch (typeName)
            {
                case "int": return 4;
                case "float": return 4;
                case "double": return 8;
                case "char": return 1;
                case "bool": return 1;
                case "string": return 8;
                default: return 4;
            }
        }

        /// Godot: func get_address_for_index(index) -> String
        /// Uniform 4-byte stride regardless of type — used by
        /// add_or_update_variable(). Contrast get_address_for_byte_offset()
        /// below, which is size-aware. Source uses both, and the Address
        /// Layout activity exists precisely to show the size-aware one.
        public static string GetAddressForIndex(int index)
        {
            return "0x" + (4096 + index * 4).ToString("X");
        }

        // Godot: func get_address_for_byte_offset(byte_offset) -> String
        public static string GetAddressForByteOffset(int byteOffset)
        {
            return "0x" + (4096 + byteOffset).ToString("X");
        }

        // Godot: func get_total_allocated_bytes() -> int
        public int GetTotalAllocatedBytes()
        {
            int total = 0;

            foreach (MemoryRecord record in memoryRecords)
            {
                string type = string.IsNullOrEmpty(record.Type) ? "int" : record.Type;
                total += GetTypeByteCount(type);
            }

            return total;
        }

        // -----------------------------------------------------------------
        // Record mutation
        // -----------------------------------------------------------------

        // Godot: func add_or_update_variable(type_name, variable_name, variable_value)
        public void AddOrUpdateVariable(string typeName, string variableName, string variableValue)
        {
            int foundIndex = -1;
            string oldValue = "";

            for (int i = 0; i < memoryRecords.Count; i++)
            {
                if (memoryRecords[i].Name == variableName)
                {
                    foundIndex = i;
                    oldValue = memoryRecords[i].Value;
                    break;
                }
            }

            if (foundIndex == -1)
            {
                memoryRecords.Add(new MemoryRecord
                {
                    Address = GetAddressForIndex(memoryRecords.Count),
                    Name = variableName,
                    Type = typeName,
                    Value = variableValue,
                    Size = GetTypeSize(typeName)
                });

                currentFocusAction = "ALLOCATED";
                currentPreviousValue = "";
                currentNewValue = variableValue;

                lastActionMessage =
                    "Allocated new variable " + variableName +
                    " with value " + variableValue + ".";
            }
            else
            {
                memoryRecords[foundIndex].Type = typeName;
                memoryRecords[foundIndex].Value = variableValue;
                memoryRecords[foundIndex].Size = GetTypeSize(typeName);

                currentFocusAction = "UPDATED";
                currentPreviousValue = oldValue;
                currentNewValue = variableValue;

                lastActionMessage =
                    "Updated " + variableName +
                    " from " + oldValue +
                    " to " + variableValue + ".";
            }

            currentFocusName = variableName;

            DrawMemory(true);
        }

        // -----------------------------------------------------------------
        // Phase 26 activities
        // -----------------------------------------------------------------

        /// Godot: func show_data_type_memory_demo()
        /// Builds its records directly rather than through
        /// add_or_update_variable(), so addresses come from the SIZE-AWARE
        /// helper. Note the subtlety this creates: get_total_allocated_bytes()
        /// is re-read on every iteration, so each row's address depends on
        /// the accumulated size of the rows before it.
        public void ShowDataTypeMemoryDemo()
        {
            if (isAnimating) return;

            memoryRecords.Clear();

            currentFocusName = "";
            currentFocusAction = "";
            currentPreviousValue = "";
            currentNewValue = "";

            (string name, string type, string value)[] demo =
            {
                ("age", "int", "20"),
                ("price", "float", "15.5"),
                ("grade", "char", "'A'"),
                ("passed", "bool", "true"),
                ("name", "string", "\"Mike\"")
            };

            foreach ((string name, string type, string value) item in demo)
            {
                memoryRecords.Add(new MemoryRecord
                {
                    Address = GetAddressForByteOffset(GetTotalAllocatedBytes()),
                    Name = item.name,
                    Type = item.type,
                    Value = item.value,
                    Size = GetTypeSize(item.type)
                });
            }

            currentCodeLine = "Data type memory example";
            lastActionMessage =
                "Displayed common data types with their values, addresses, and memory sizes.";

            DrawMemory(true);
        }

        /// Godot: func show_address_layout_demo()
        ///
        /// Nearly the same as the Data Types demo, and closer than source
        /// appears to intend. Three differences, only one of which a student
        /// can see:
        ///
        /// - The last row is "level" (int) rather than "name" (string), and
        ///   the "level" row is FOCUSED with an ADDRESS_LAYOUT marker. This
        ///   is the only visible difference.
        /// - byte_count is stored on every record — and never read. It is
        ///   not displayed by add_memory_row(), and the string does not
        ///   appear anywhere in ar_learning_screen.gd. Write-only data.
        ///   Ported so a later phase that does want it finds it populated,
        ///   but nothing reads it today.
        /// - The byte offset is accumulated in a LOCAL running total instead
        ///   of being re-derived from get_total_allocated_bytes() each pass.
        ///
        /// **That last difference changes nothing.** Both were worked through
        /// by hand: Data Types yields 0x1000, 0x1004, 0x1008, 0x1009, 0x100A
        /// and Address Layout yields exactly the same five addresses, because
        /// summing the record list is arithmetically identical to carrying a
        /// running total. The 4, 4, 1, 1 stride the activity teaches is
        /// already visible in Data Types; Address Layout re-presents it with
        /// a marker and an int in the final row instead of a variable-size
        /// string. Worth stating plainly so nobody "fixes" one demo to match
        /// the other later on, having assumed they diverge.
        public void ShowAddressLayoutDemo()
        {
            if (isAnimating) return;

            memoryRecords.Clear();

            currentFocusName = "";
            currentFocusAction = "";
            currentPreviousValue = "";
            currentNewValue = "";

            (string name, string type, string value)[] demo =
            {
                ("age", "int", "20"),
                ("price", "float", "15.5"),
                ("grade", "char", "'A'"),
                ("passed", "bool", "true"),
                ("level", "int", "3")
            };

            int byteOffset = 0;

            foreach ((string name, string type, string value) item in demo)
            {
                int byteCount = GetTypeByteCount(item.type);

                memoryRecords.Add(new MemoryRecord
                {
                    Address = GetAddressForByteOffset(byteOffset),
                    Name = item.name,
                    Type = item.type,
                    Value = item.value,
                    Size = GetTypeSize(item.type),
                    ByteCount = byteCount
                });

                byteOffset += byteCount;
            }

            currentFocusName = "level";
            currentFocusAction = "ADDRESS_LAYOUT";
            currentPreviousValue = "";
            currentNewValue = "";

            currentCodeLine = "Address layout memory example";
            lastActionMessage = "Addresses increase according to data type size.";

            DrawMemory(true);
        }


        // -----------------------------------------------------------------
        // Phase 26c — the six one-button step traces
        //
        // Godot: step_function_call_memory(), step_selection_memory(),
        // step_scope_memory(), step_memory_access(), step_loop_memory(),
        // step_remove_memory(). All six share one shape: increment a
        // per-activity step counter, act on it, and `await
        // highlight_current_record()` before returning current_code_line —
        // or return null once the trace runs out, which is how the caller
        // knows the activity is finished.
        //
        // Godot returns a value from an awaited function. C# coroutines
        // cannot, so each takes an Action<string> callback, matching
        // LoopModule.StepLoop(Action<int?>)'s established convention. The
        // callback receives null exactly where source returns null.
        // -----------------------------------------------------------------

        private int functionStep;
        private int selectionStep;
        private int scopeStep;
        private int accessStep;
        private int loopMemoryStep;
        private int removeMemoryStep;

        private string activeFunctionName = "";

        /// Godot: var active_function_name. Set by the Functions trace and
        /// never read anywhere in source. Exposed here rather than dropped so
        /// the port stays complete and the compiler stops warning about an
        /// assigned-but-unused field.
        public string ActiveFunctionName => activeFunctionName;

        // ----- Functions -----

        /// Godot: func add_function_memory_record(frame_name, type_name,
        /// variable_name, variable_value, role)
        /// Note the display name is "name:role" — that colon is what
        /// find_memory_record() later strips when a student inspects "sum"
        /// rather than "sum:local".
        private void AddFunctionMemoryRecord(
            string frameName, string typeName, string variableName, string variableValue, string role)
        {
            string displayName = role != "" ? variableName + ":" + role : variableName;

            memoryRecords.Add(new MemoryRecord
            {
                Address = GetAddressForByteOffset(GetTotalAllocatedBytes()),
                Name = displayName,
                Type = typeName,
                Value = variableValue,
                Size = GetTypeSize(typeName),
                Frame = frameName,
                Role = role
            });

            currentFocusName = displayName;
            currentFocusAction = "FRAME_ALLOCATED";
            currentPreviousValue = "";
            currentNewValue = variableValue;

            lastActionMessage =
                "Created " + role + " variable " + variableName +
                " inside " + frameName + " with value " + variableValue + ".";

            DrawMemory(true);
        }

        /// Godot: func remove_function_frame(frame_name)
        /// Sets currentFocusAction to "FRAME_REMOVED", which appears in
        /// NEITHER add_focus_marker()'s chain nor the global badge list — so
        /// nothing is drawn for it. Preserved: the removal is visible as the
        /// rows disappearing, and source clearly accepted that.
        private void RemoveFunctionFrame(string frameName)
        {
            for (int i = memoryRecords.Count - 1; i >= 0; i--)
            {
                if (memoryRecords[i].Frame == frameName) memoryRecords.RemoveAt(i);
            }

            currentFocusName = "";
            currentFocusAction = "FRAME_REMOVED";
            currentPreviousValue = "";
            currentNewValue = "";

            lastActionMessage =
                "The function frame " + frameName + " was removed after the function returned.";

            DrawMemory(true);
        }

        // Godot: func step_function_call_memory()
        public IEnumerator StepFunctionCallMemory(Action<string> onComplete = null)
        {
            if (isAnimating) { onComplete?.Invoke(null); yield break; }

            functionStep++;

            switch (functionStep)
            {
                case 1:
                    currentCodeLine = "int x = 10;";
                    AddOrUpdateVariable("int", "x", "10");
                    break;

                case 2:
                    currentCodeLine = "int y = 20;";
                    AddOrUpdateVariable("int", "y", "20");
                    break;

                case 3:
                    activeFunctionName = "add()";
                    currentCodeLine = "result = add(x, y);";

                    // "CALL_FRAME" is another action with no marker text and
                    // no badge — and with an empty focus name, so no marker is
                    // drawn at all. The frame has not been allocated yet;
                    // this step exists to narrate the call itself.
                    currentFocusName = "";
                    currentFocusAction = "CALL_FRAME";
                    currentPreviousValue = "";
                    currentNewValue = "";

                    lastActionMessage =
                        "Function call add(x, y) started. A new function frame is created.";

                    DrawMemory(true);
                    break;

                case 4:
                    currentCodeLine = "int add(int a, int b)";
                    AddFunctionMemoryRecord("add()", "int", "a", "10", "param");
                    break;

                case 5:
                    // Source repeats the same code line for steps 4 and 5,
                    // because both parameters belong to one signature.
                    currentCodeLine = "int add(int a, int b)";
                    AddFunctionMemoryRecord("add()", "int", "b", "20", "param");
                    break;

                case 6:
                    currentCodeLine = "int sum = a + b;";
                    AddFunctionMemoryRecord("add()", "int", "sum", "30", "local");
                    break;

                case 7:
                    currentCodeLine = "return sum;";
                    currentFocusName = "sum:local";
                    currentFocusAction = "RETURN_VALUE";
                    currentPreviousValue = "";
                    currentNewValue = "30";

                    lastActionMessage = "The function returns the value 30.";

                    DrawMemory(true);
                    break;

                case 8:
                    currentCodeLine = "int result = 30;";
                    RemoveFunctionFrame("add()");
                    AddOrUpdateVariable("int", "result", "30");

                    // Overwrites what AddOrUpdateVariable just set. The redraw
                    // it triggered has already happened with "ALLOCATED", so
                    // the RETURN_STORED marker only appears on the redraw
                    // inside HighlightCurrentRecord() below. Faithful.
                    currentFocusAction = "RETURN_STORED";
                    lastActionMessage =
                        "Return value 30 was stored in result. The add() frame was removed.";
                    break;

                default:
                    currentCodeLine = "Function call completed.";
                    currentFocusName = "";
                    currentFocusAction = "";
                    currentPreviousValue = "";
                    currentNewValue = "";

                    lastActionMessage = "Function call memory trace completed.";

                    DrawMemory(true);
                    onComplete?.Invoke(null);
                    yield break;
            }

            yield return HighlightCurrentRecord();

            onComplete?.Invoke(currentCodeLine);
        }

        // ----- Selection -----

        // Godot: func set_condition_focus(message, condition_result)
        private void SetConditionFocus(string message, bool conditionResult)
        {
            currentFocusName = "";
            currentFocusAction = conditionResult ? "CONDITION_TRUE" : "CONDITION_FALSE";
            currentPreviousValue = "";
            currentNewValue = conditionResult ? "TRUE" : "FALSE";

            lastActionMessage = message;

            DrawMemory(true);
        }

        // Godot: func step_selection_memory()
        public IEnumerator StepSelectionMemory(Action<string> onComplete = null)
        {
            if (isAnimating) { onComplete?.Invoke(null); yield break; }

            selectionStep++;

            switch (selectionStep)
            {
                case 1:
                    currentCodeLine = "int score = 75;";
                    AddOrUpdateVariable("int", "score", "75");
                    break;

                case 2:
                    currentCodeLine = "if (score >= 50)";
                    SetConditionFocus(
                        "The condition score >= 50 evaluated TRUE because score stores 75.",
                        true);
                    break;

                case 3:
                    currentCodeLine = "result = 1;";
                    AddOrUpdateVariable("int", "result", "1");

                    // Overrides AddOrUpdateVariable's own "Allocated new
                    // variable result with value 1." message.
                    lastActionMessage =
                        "The TRUE branch executed. result was stored as 1 in memory.";
                    break;

                case 4:
                    currentCodeLine = "else branch skipped";

                    currentFocusName = "";
                    currentFocusAction = "BRANCH_SKIPPED";
                    currentPreviousValue = "";
                    currentNewValue = "";

                    lastActionMessage =
                        "The ELSE branch was skipped because the condition was TRUE.";

                    DrawMemory(true);
                    break;

                default:
                    currentCodeLine = "Selection trace completed.";
                    currentFocusName = "";
                    currentFocusAction = "";
                    currentPreviousValue = "";
                    currentNewValue = "";

                    lastActionMessage = "Selection memory trace completed.";

                    DrawMemory(true);
                    onComplete?.Invoke(null);
                    yield break;
            }

            yield return HighlightCurrentRecord();

            onComplete?.Invoke(currentCodeLine);
        }

        // ----- Scope -----

        // Godot: func add_scope_memory_record(scope_name, type_name, variable_name, variable_value)
        private void AddScopeMemoryRecord(
            string scopeName, string typeName, string variableName, string variableValue)
        {
            memoryRecords.Add(new MemoryRecord
            {
                Address = GetAddressForByteOffset(GetTotalAllocatedBytes()),
                Name = variableName,
                Type = typeName,
                Value = variableValue,
                Size = GetTypeSize(typeName),
                Scope = scopeName
            });

            currentFocusName = variableName;
            currentFocusAction = "SCOPE_LOCAL";
            currentPreviousValue = "";
            currentNewValue = variableValue;

            lastActionMessage =
                "Created local variable " + variableName +
                " inside " + scopeName + " with value " + variableValue + ".";

            DrawMemory(true);
        }

        // Godot: func remove_scope_variable(variable_name)
        private void RemoveScopeVariable(string variableName)
        {
            string removedValue = "";

            for (int i = memoryRecords.Count - 1; i >= 0; i--)
            {
                if (memoryRecords[i].Name == variableName)
                {
                    removedValue = memoryRecords[i].Value;
                    memoryRecords.RemoveAt(i);
                    break;
                }
            }

            currentFocusName = "";
            currentFocusAction = "SCOPE_REMOVED";
            currentPreviousValue = removedValue;
            currentNewValue = "";
            removedScopeName = variableName;

            lastActionMessage =
                "Removed local variable " + variableName + " because its block scope ended.";

            DrawMemory(true);
        }

        // Godot: func step_scope_memory()
        public IEnumerator StepScopeMemory(Action<string> onComplete = null)
        {
            if (isAnimating) { onComplete?.Invoke(null); yield break; }

            scopeStep++;

            switch (scopeStep)
            {
                case 1:
                    currentCodeLine = "int x = 10;";
                    AddOrUpdateVariable("int", "x", "10");
                    break;

                case 2:
                    currentCodeLine = "if (x > 5)";
                    currentFocusName = "";
                    currentFocusAction = "CONDITION_TRUE";
                    currentPreviousValue = "";
                    currentNewValue = "TRUE";

                    // Source sets these inline here rather than calling
                    // set_condition_focus(), which would have produced the
                    // identical result. Ported as written.
                    lastActionMessage =
                        "The condition x > 5 evaluated TRUE because x stores 10.";

                    DrawMemory(true);
                    break;

                case 3:
                    currentCodeLine = "int y = 20;";
                    AddScopeMemoryRecord("if block", "int", "y", "20");
                    break;

                case 4:
                    currentCodeLine = "x = x + y;";
                    AddOrUpdateVariable("int", "x", "30");

                    lastActionMessage =
                        "x was updated from 10 to 30 using the local variable y.";
                    break;

                case 5:
                    currentCodeLine = "end of if block";
                    RemoveScopeVariable("y");
                    break;

                case 6:
                    currentCodeLine = "x remains in memory";
                    currentFocusName = "x";
                    currentFocusAction = "SCOPE_REMAINS";
                    currentPreviousValue = "";
                    currentNewValue = "30";

                    lastActionMessage =
                        "x remains in memory because it was declared outside the if block.";

                    DrawMemory(true);
                    break;

                default:
                    currentCodeLine = "Scope trace completed.";
                    currentFocusName = "";
                    currentFocusAction = "";
                    currentPreviousValue = "";
                    currentNewValue = "";

                    lastActionMessage = "Scope memory trace completed.";

                    DrawMemory(true);
                    onComplete?.Invoke(null);
                    yield break;
            }

            yield return HighlightCurrentRecord();

            onComplete?.Invoke(currentCodeLine);
        }

        // ----- Memory Access -----

        /// Godot: func read_memory_variable(variable_name) -> bool
        /// Returns false and writes a failure message when the name is not
        /// found. Nothing in the Memory Access trace can reach that path —
        /// every name it reads was allocated by an earlier step — but it is
        /// ported because Loop Memory reads array elements by name too.
        private bool ReadMemoryVariable(string variableName)
        {
            foreach (MemoryRecord record in memoryRecords)
            {
                if (record.Name == variableName)
                {
                    currentFocusName = variableName;
                    currentFocusAction = "READ";
                    currentPreviousValue = "";
                    currentNewValue = record.Value;

                    lastActionMessage =
                        "Read " + variableName +
                        " from memory. Its stored value is " + record.Value + ".";

                    DrawMemory(true);
                    return true;
                }
            }

            lastActionMessage =
                "Could not read " + variableName + " because it was not found in memory.";
            return false;
        }

        // Godot: func step_memory_access()
        public IEnumerator StepMemoryAccess(Action<string> onComplete = null)
        {
            if (isAnimating) { onComplete?.Invoke(null); yield break; }

            accessStep++;

            switch (accessStep)
            {
                case 1:
                    currentCodeLine = "int x = 4;";
                    AddOrUpdateVariable("int", "x", "4");
                    break;

                case 2:
                    currentCodeLine = "int y = 6;";
                    AddOrUpdateVariable("int", "y", "6");
                    break;

                case 3:
                    currentCodeLine = "read x";
                    ReadMemoryVariable("x");
                    break;

                case 4:
                    currentCodeLine = "read y";
                    ReadMemoryVariable("y");
                    break;

                case 5:
                    currentCodeLine = "int sum = x + y;";
                    AddOrUpdateVariable("int", "sum", "10");

                    lastActionMessage =
                        "x and y were read from memory. Their values 4 and 6 were added, so sum stores 10.";
                    break;

                default:
                    // Source spells step 6 and the else branch out separately
                    // with identical bodies. Collapsed here — the behaviour is
                    // the same, and the duplication is not a quirk worth
                    // reproducing structurally.
                    currentCodeLine = "Memory access trace completed.";
                    currentFocusName = "";
                    currentFocusAction = "";
                    currentPreviousValue = "";
                    currentNewValue = "";

                    lastActionMessage = "Memory access trace completed.";

                    DrawMemory(true);
                    onComplete?.Invoke(null);
                    yield break;
            }

            yield return HighlightCurrentRecord();

            onComplete?.Invoke(currentCodeLine);
        }

        // ----- Loop Memory -----

        // Godot: func remove_loop_variable(variable_name)
        private void RemoveLoopVariable(string variableName)
        {
            string removedValue = "";

            for (int i = memoryRecords.Count - 1; i >= 0; i--)
            {
                if (memoryRecords[i].Name == variableName)
                {
                    removedValue = memoryRecords[i].Value;
                    memoryRecords.RemoveAt(i);
                    break;
                }
            }

            currentFocusName = "";
            currentFocusAction = "LOOP_REMOVED";
            currentPreviousValue = removedValue;
            currentNewValue = "";

            lastActionMessage =
                "Removed loop counter " + variableName + " because the loop has finished.";

            DrawMemory(true);
        }

        // Godot: func step_loop_memory()
        public IEnumerator StepLoopMemory(Action<string> onComplete = null)
        {
            if (isAnimating) { onComplete?.Invoke(null); yield break; }

            loopMemoryStep++;

            switch (loopMemoryStep)
            {
                case 1:
                    currentCodeLine = "int numbers[3] = {2, 4, 6};";
                    AllocateArray("int", "numbers", new List<string> { "2", "4", "6" });

                    // Overrides AllocateArray's own "Allocated array numbers
                    // with 3 element(s)." message. The controller rewrites it
                    // AGAIN — see MemoryController's Loop Memory note.
                    lastActionMessage =
                        "The array numbers was stored in consecutive memory locations.";
                    break;

                case 2:
                    currentCodeLine = "int total = 0;";
                    AddOrUpdateVariable("int", "total", "0");
                    lastActionMessage = "total was allocated with the starting value 0.";
                    break;

                case 3:
                    currentCodeLine = "int i = 0;";
                    AddOrUpdateVariable("int", "i", "0");
                    lastActionMessage = "Loop counter i was created with value 0.";
                    break;

                case 4:
                    currentCodeLine = "read numbers[0]";
                    ReadMemoryVariable("numbers[0]");
                    lastActionMessage = "The loop reads numbers[0], which stores 2.";
                    break;

                case 5:
                    currentCodeLine = "total = total + numbers[0];";
                    AddOrUpdateVariable("int", "total", "2");
                    lastActionMessage = "total was updated from 0 to 2.";
                    break;

                case 6:
                    currentCodeLine = "i = i + 1;";
                    AddOrUpdateVariable("int", "i", "1");
                    lastActionMessage = "Loop counter i was updated from 0 to 1.";
                    break;

                case 7:
                    currentCodeLine = "read numbers[1]";
                    ReadMemoryVariable("numbers[1]");
                    lastActionMessage = "The loop reads numbers[1], which stores 4.";
                    break;

                case 8:
                    currentCodeLine = "total = total + numbers[1];";
                    AddOrUpdateVariable("int", "total", "6");
                    lastActionMessage = "total was updated from 2 to 6.";
                    break;

                case 9:
                    currentCodeLine = "i = i + 1;";
                    AddOrUpdateVariable("int", "i", "2");
                    lastActionMessage = "Loop counter i was updated from 1 to 2.";
                    break;

                case 10:
                    currentCodeLine = "read numbers[2]";
                    ReadMemoryVariable("numbers[2]");
                    lastActionMessage = "The loop reads numbers[2], which stores 6.";
                    break;

                case 11:
                    currentCodeLine = "total = total + numbers[2];";
                    AddOrUpdateVariable("int", "total", "12");
                    lastActionMessage = "total was updated from 6 to 12.";
                    break;

                case 12:
                    currentCodeLine = "loop ends";
                    RemoveLoopVariable("i");
                    lastActionMessage =
                        "The loop ended. The temporary loop counter i was removed.";
                    break;

                case 13:
                    // Note this is the ONLY trace whose final step sets a
                    // focus and a marker before finishing — LOOP_COMPLETE on
                    // total. It returns null like every other ending, so the
                    // controller marks the activity complete here.
                    currentCodeLine = "Loop memory trace completed.";

                    currentFocusName = "total";
                    currentFocusAction = "LOOP_COMPLETE";
                    currentPreviousValue = "";
                    currentNewValue = "12";

                    lastActionMessage = "Loop memory trace completed. Final total is 12.";

                    DrawMemory(true);
                    onComplete?.Invoke(null);
                    yield break;

                default:
                    currentCodeLine = "Loop memory trace completed.";
                    lastActionMessage = "Loop memory trace completed.";
                    DrawMemory(true);
                    onComplete?.Invoke(null);
                    yield break;
            }

            yield return HighlightCurrentRecord();

            onComplete?.Invoke(currentCodeLine);
        }

        // ----- Remove Variable -----

        /// Godot: func remove_memory_record(variable_name) -> bool
        /// **A latent source bug, ported as-is.** It reports failure when the
        /// REMOVED VALUE is empty, not when the record was absent:
        ///
        ///     if removed_value == "":
        ///         last_action_message = "Could not remove ... not found"
        ///         return false
        ///
        /// A record that genuinely exists but stores an empty string would be
        /// removed from the list and then reported as "not found". Nothing in
        /// the Remove Variable trace can trigger it — both its variables hold
        /// real values — so it stays a latent bug rather than a live one, and
        /// correcting it would change no observable behaviour today. Flagged
        /// because Phase 26d lets students name the variable to remove.
        private bool RemoveMemoryRecord(string variableName)
        {
            string removedValue = "";
            string removedType = "";

            for (int i = memoryRecords.Count - 1; i >= 0; i--)
            {
                if (memoryRecords[i].Name == variableName)
                {
                    removedValue = memoryRecords[i].Value;
                    removedType = memoryRecords[i].Type;
                    memoryRecords.RemoveAt(i);
                    break;
                }
            }

            if (removedValue == "")
            {
                lastActionMessage =
                    "Could not remove " + variableName + " because it was not found in memory.";
                return false;
            }

            currentFocusName = "";
            currentFocusAction = "REMOVED_VARIABLE";
            currentPreviousValue = removedValue;
            currentNewValue = "";
            removedMemoryName = variableName;

            lastActionMessage =
                "Removed " + variableName + " from memory. It was a " + removedType +
                " storing " + removedValue + ".";

            DrawMemory(true);
            return true;
        }

        // Godot: func step_remove_memory()
        public IEnumerator StepRemoveMemory(Action<string> onComplete = null)
        {
            if (isAnimating) { onComplete?.Invoke(null); yield break; }

            removeMemoryStep++;

            switch (removeMemoryStep)
            {
                case 1:
                    currentCodeLine = "int age = 20;";
                    AddOrUpdateVariable("int", "age", "20");
                    lastActionMessage = "age was allocated with value 20.";
                    break;

                case 2:
                    currentCodeLine = "float price = 15.5;";
                    AddOrUpdateVariable("float", "price", "15.5");
                    lastActionMessage = "price was allocated with value 15.5.";
                    break;

                case 3:
                    currentCodeLine = "remove age;";
                    RemoveMemoryRecord("age");
                    lastActionMessage = "age was removed from memory.";
                    break;

                case 4:
                    currentCodeLine = "price remains in memory";

                    currentFocusName = "price";
                    currentFocusAction = "REMAINS_AFTER_REMOVE";
                    currentPreviousValue = "";
                    currentNewValue = "15.5";

                    lastActionMessage = "price remains in memory because only age was removed.";

                    DrawMemory(true);
                    break;

                default:
                    currentCodeLine = "Remove trace completed.";

                    currentFocusName = "";
                    currentFocusAction = "";
                    currentPreviousValue = "";
                    currentNewValue = "";

                    lastActionMessage = "Remove Variable trace completed.";

                    DrawMemory(true);
                    onComplete?.Invoke(null);
                    yield break;
            }

            yield return HighlightCurrentRecord();

            onComplete?.Invoke(currentCodeLine);
        }

        // -----------------------------------------------------------------
        // Array allocation — Godot: allocate_array() and friends.
        //
        // Belongs to the Arrays activity (Phase 26d), but Loop Memory's first
        // step calls it, so it is ported here rather than stubbed. Its
        // remaining callers — set_array_from_text() and update_array_element()
        // — arrive with Arrays.
        // -----------------------------------------------------------------

        // Godot: func remove_existing_array_records(array_name)
        private void RemoveExistingArrayRecords(string arrayName)
        {
            for (int i = memoryRecords.Count - 1; i >= 0; i--)
            {
                if (memoryRecords[i].ArrayName == arrayName) memoryRecords.RemoveAt(i);
            }
        }

        /// Godot: func is_valid_variable_name(variable_name) -> bool
        /// Source compiles the regex "^[A-Za-z_][A-Za-z0-9_]*$" on every call.
        /// Cached here as a static Regex — same result, and this runs once per
        /// character in Phase 26d's input handling.
        private static readonly Regex VariableNamePattern =
            new Regex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

        public static bool IsValidVariableName(string variableName)
        {
            if (string.IsNullOrEmpty(variableName)) return false;
            return VariableNamePattern.IsMatch(variableName);
        }

        /// Godot: func allocate_array(type_name, array_name, array_values) -> bool
        /// The six-element cap is source's own, and it is what keeps the table
        /// within the row budget the layout assumes.
        public bool AllocateArray(string typeName, string arrayName, List<string> arrayValues)
        {
            lastArrayTypeError = "";

            if (arrayValues == null || arrayValues.Count == 0) return false;
            if (arrayValues.Count > 6) return false;
            if (!IsValidVariableName(arrayName)) return false;

            // Second half of the same correction as UpdateArrayElement's.
            // Source checks nothing here either, so `int marks[3]=70,abc,65`
            // allocates an int array holding "abc" — and when no type is
            // written the type is inferred from the FIRST value alone, so
            // `marks[3]=70,abc,65` infers int and stores "abc" just the same.
            // Gating only the element-update path would have closed half the
            // hole. Every value is checked before ANY record is written, so a
            // rejected declaration leaves the previous array untouched rather
            // than half-replaced — RemoveExistingArrayRecords() below is
            // destructive.
            for (int i = 0; i < arrayValues.Count; i++)
            {
                string candidate = arrayValues[i].Trim();

                if (!IsValueCompatibleWithType(typeName, candidate))
                {
                    lastArrayTypeError = GetValueTypeErrorMessage(
                        typeName, arrayName + "[" + i + "]", candidate);
                    lastActionMessage = lastArrayTypeError;

                    currentCodeLine = "Invalid assignment blocked.";
                    DrawMemory(true);
                    return false;
                }
            }

            RemoveExistingArrayRecords(arrayName);

            int baseOffset = GetTotalAllocatedBytes();
            int typeBytes = GetTypeByteCount(typeName);

            for (int i = 0; i < arrayValues.Count; i++)
            {
                memoryRecords.Add(new MemoryRecord
                {
                    Address = GetAddressForByteOffset(baseOffset + i * typeBytes),
                    Name = arrayName + "[" + i + "]",
                    Type = typeName,
                    Value = arrayValues[i].Trim(),
                    Size = GetTypeSize(typeName),
                    ArrayName = arrayName,
                    Index = i
                });
            }

            currentFocusName = arrayName + "[0]";
            currentFocusAction = "ALLOCATED_ARRAY";
            currentPreviousValue = "";
            currentNewValue = arrayValues.Count + " elements";

            lastActionMessage =
                "Allocated array " + arrayName + " with " + arrayValues.Count + " element(s).";

            currentCodeLine =
                typeName + " " + arrayName + "[" + arrayValues.Count + "] = {" +
                string.Join(", ", arrayValues) + "};";

            DrawMemory(true);

            return true;
        }


        // -----------------------------------------------------------------
        // Phase 26d — Variables and Arrays: typed input, type inference,
        // validation, inspection, and the prediction flow.
        // -----------------------------------------------------------------

        private string inspectionMessage = "";

        /// Set when an array assignment is rejected because a value does not
        /// match the element type — see the type-gate note on
        /// UpdateArrayElement(). Empty means "no type error", which lets the
        /// controller tell a TYPE rejection apart from an UNPARSEABLE input
        /// and report the specific reason instead of the generic one.
        ///
        /// Cleared at the top of every array entry point, so it always
        /// describes the most recent attempt and never a stale one.
        private string lastArrayTypeError = "";

        public string LastArrayTypeError => lastArrayTypeError;

        // ----- Type checking -----

        /// Godot: func is_value_compatible_with_type(type_name, variable_value)
        /// The safety gate that stops memory being updated with a value the
        /// declared type cannot hold — the behaviour the Debug Exercise
        /// activity later asks students about.
        public static bool IsValueCompatibleWithType(string typeName, string variableValue)
        {
            string cleaned = (variableValue ?? "").Trim();
            string lower = cleaned.ToLowerInvariant();

            switch (typeName)
            {
                case "int":
                    return IsValidInt(cleaned);

                case "float":
                case "double":
                    return IsValidFloat(cleaned);

                case "bool":
                    return lower == "true" || lower == "false";

                case "char":
                    if (!cleaned.StartsWith("'") || !cleaned.EndsWith("'")) return false;
                    // Godot: cleaned.substr(1, cleaned.length() - 2)
                    if (cleaned.Length < 2) return false;
                    return cleaned.Substring(1, cleaned.Length - 2).Length == 1;

                case "string":
                    return (cleaned.StartsWith("\"") && cleaned.EndsWith("\"")) ||
                           (cleaned.StartsWith("'") && cleaned.EndsWith("'"));

                default:
                    return false;
            }
        }

        /// Godot's String.is_valid_int() / is_valid_float(). Close enough for
        /// this module's inputs, with one documented difference: Godot's
        /// is_valid_float() accepts scientific notation such as "1e3", and so
        /// does float.TryParse with NumberStyles.Float. Invariant culture is
        /// forced so a device with a comma decimal separator still reads
        /// "15.5" as a float — a real hazard the Godot build never faced,
        /// since it ran on one machine.
        private static bool IsValidInt(string text)
        {
            return int.TryParse(
                text, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out _);
        }

        private static bool IsValidFloat(string text)
        {
            return float.TryParse(
                text, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out _);
        }

        // Godot: func get_value_type_error_message(type_name, variable_name, variable_value)
        public static string GetValueTypeErrorMessage(
            string typeName, string variableName, string variableValue)
        {
            switch (typeName)
            {
                case "int":
                    return "Invalid input. " + variableName +
                           " is an int, so use a whole number like 25.";
                case "float":
                    return "Invalid input. " + variableName +
                           " is a float, so use a number like 25.5.";
                case "double":
                    return "Invalid input. " + variableName +
                           " is a double, so use a number like 25.5.";
                case "bool":
                    return "Invalid input. " + variableName +
                           " is a bool, so use true or false.";
                case "char":
                    return "Invalid input. " + variableName +
                           " is a char, so use one character in single quotes, like 'A'.";
                case "string":
                    return "Invalid input. " + variableName +
                           " is a string, so use text in quotes, like \"Mike\".";
                default:
                    return "Invalid input. The value " + variableValue +
                           " does not match the expected type.";
            }
        }

        /// Godot: func infer_type_from_value(variable_value)
        /// Note the single-quote branch: one character is a char, anything
        /// else in single quotes is a string. That asymmetry is source's, and
        /// it is why 'AB' infers as string rather than failing — the Debug
        /// Exercise's third question turns on exactly this.
        public static string InferTypeFromValue(string variableValue)
        {
            string cleaned = (variableValue ?? "").Trim();
            string lower = cleaned.ToLowerInvariant();

            if (lower == "true" || lower == "false") return "bool";

            if (cleaned.StartsWith("\"") && cleaned.EndsWith("\"")) return "string";

            if (cleaned.StartsWith("'") && cleaned.EndsWith("'"))
            {
                if (cleaned.Length < 2) return "string";
                return cleaned.Substring(1, cleaned.Length - 2).Length == 1 ? "char" : "string";
            }

            if (IsValidInt(cleaned)) return "int";
            if (IsValidFloat(cleaned)) return "float";

            return "unknown";
        }

        // Godot: func get_existing_variable_type(variable_name)
        private string GetExistingVariableType(string variableName)
        {
            foreach (MemoryRecord record in memoryRecords)
            {
                if (record.Name == variableName) return record.Type;
            }

            return "";
        }

        // ----- Variables: typed input -----

        /// Godot: func set_variable_from_text(text_value) -> bool
        ///
        /// Accepts `int age=20`, `age=25`, `char grade='A'`, `string name="Mike"`.
        ///
        /// Two source behaviours worth knowing, both preserved:
        ///
        /// - The type prefix is stripped with `replace("int ", "")`, which
        ///   removes EVERY occurrence, not just the leading one. C#'s
        ///   string.Replace behaves identically, so this ports exactly —
        ///   including the edge case where a value contains the type word.
        /// - An existing variable KEEPS its original type when no type is
        ///   written. So `age=25` after `int age=20` stays an int, and
        ///   `age=abc` is then rejected rather than re-inferred as a string.
        ///   That is the whole point of the activity.
        public bool SetVariableFromText(string textValue)
        {
            if (isAnimating) return false;

            string cleaned = (textValue ?? "").Trim();

            if (cleaned == "")
            {
                lastActionMessage = "Invalid input. No variable was entered.";
                return false;
            }

            string typeName = "";
            string declarationText = cleaned;
            bool hasExplicitType = false;

            string[] typePrefixes = { "int ", "float ", "double ", "char ", "bool ", "string " };

            foreach (string prefix in typePrefixes)
            {
                if (cleaned.StartsWith(prefix))
                {
                    typeName = prefix.Trim();
                    declarationText = cleaned.Replace(prefix, "");
                    hasExplicitType = true;
                    break;
                }
            }

            if (!declarationText.Contains("="))
            {
                lastActionMessage =
                    "Invalid input. Use the format name=value or type name=value.";
                return false;
            }

            string[] parts = declarationText.Split('=');

            if (parts.Length != 2)
            {
                lastActionMessage = "Invalid input. Use only one equals sign.";
                return false;
            }

            string variableName = parts[0].Trim();
            string variableValue = parts[1].Trim();

            if (variableName == "" || variableValue == "")
            {
                lastActionMessage = "Invalid input. Variable name and value are required.";
                return false;
            }

            if (!IsValidVariableName(variableName))
            {
                lastActionMessage =
                    "Invalid input. " + variableName + " is not a valid variable name.";
                return false;
            }

            string existingType = GetExistingVariableType(variableName);

            if (existingType != "" && !hasExplicitType)
            {
                typeName = existingType;
            }
            else if (!hasExplicitType)
            {
                typeName = InferTypeFromValue(variableValue);
            }

            if (typeName == "" || typeName == "unknown")
            {
                lastActionMessage = "Invalid input. Could not identify the variable type.";
                return false;
            }

            // Source calls this "Important safety gate" in a comment: memory
            // is not touched at all unless the value matches the type.
            if (!IsValueCompatibleWithType(typeName, variableValue))
            {
                lastActionMessage =
                    GetValueTypeErrorMessage(typeName, variableName, variableValue);
                currentCodeLine = "Invalid assignment blocked.";
                DrawMemory(true);
                return false;
            }

            AddOrUpdateVariable(typeName, variableName, variableValue);

            currentCodeLine = hasExplicitType
                ? typeName + " " + variableName + " = " + variableValue + ";"
                : variableName + " = " + variableValue + ";";

            return true;
        }

        // ----- Arrays: typed input -----

        // Godot: func get_existing_array_type(array_name)
        private string GetExistingArrayType(string arrayName)
        {
            foreach (MemoryRecord record in memoryRecords)
            {
                if (record.ArrayName == arrayName) return record.Type;
            }

            return "";
        }

        /// Godot: func update_array_element(array_name, array_index, new_value) -> bool
        ///
        /// *** DELIBERATE CORRECTION: a type gate was added. ***
        ///
        /// Source performs NO type check here, unlike set_variable_from_text()
        /// which calls its own gate "Important safety gate" in a comment. So
        /// in the Godot build `marks[1]=abc` is accepted into an int array and
        /// the table shows a non-numeric value in an int row.
        ///
        /// That is not a quirk worth preserving in a module whose entire
        /// subject is that a type constrains what a location can hold — and
        /// whose Debug Exercise activity asks students to spot exactly this
        /// mistake in `age = "Mike"`. Leaving it would have the app teach one
        /// rule and then break it one activity later.
        ///
        /// The gate reuses the SAME two functions the variable path uses, so
        /// the rules and the wording match: IsValueCompatibleWithType() and
        /// GetValueTypeErrorMessage(). Corrected at the user's decision;
        /// recorded alongside the migration's other deliberate deviations.
        ///
        /// The element's EXISTING type is what is enforced, mirroring
        /// set_variable_from_text()'s rule that an existing variable keeps its
        /// declared type rather than re-inferring one.
        public bool UpdateArrayElement(string arrayName, int arrayIndex, string newValue)
        {
            lastArrayTypeError = "";

            string elementName = arrayName + "[" + arrayIndex + "]";

            for (int i = 0; i < memoryRecords.Count; i++)
            {
                if (memoryRecords[i].Name != elementName) continue;

                if (!IsValueCompatibleWithType(memoryRecords[i].Type, newValue))
                {
                    lastArrayTypeError =
                        GetValueTypeErrorMessage(memoryRecords[i].Type, elementName, newValue);
                    lastActionMessage = lastArrayTypeError;

                    // Matches what the variable path does on the same failure,
                    // so the code line reads the same way for both.
                    currentCodeLine = "Invalid assignment blocked.";
                    DrawMemory(true);
                    return false;
                }

                string oldValue = memoryRecords[i].Value;
                memoryRecords[i].Value = newValue;

                currentFocusName = elementName;
                currentFocusAction = "UPDATED";
                currentPreviousValue = oldValue;
                currentNewValue = newValue;

                lastActionMessage =
                    "Updated " + elementName + " from " + oldValue + " to " + newValue + ".";

                currentCodeLine = elementName + " = " + newValue + ";";

                DrawMemory(true);
                return true;
            }

            return false;
        }

        /// Godot: func set_array_from_text(text_value) -> bool
        ///
        /// Two cases, distinguished by whether a type was written and whether
        /// the right side holds a comma:
        ///
        ///   marks[2]=85                  -> update one element
        ///   int marks[3]=70,80,65        -> allocate a new array
        ///
        /// Every failure path returns false with NO message — unlike
        /// set_variable_from_text(), which explains each one. The controller
        /// therefore supplies a single generic message for all of them, which
        /// is what source does too.
        public bool SetArrayFromText(string textValue)
        {
            lastArrayTypeError = "";

            if (isAnimating) return false;

            string cleaned = (textValue ?? "").Trim();

            if (cleaned == "") return false;
            if (!cleaned.Contains("=")) return false;

            string[] parts = cleaned.Split('=');
            if (parts.Length != 2) return false;

            string leftSide = parts[0].Trim();
            string rightSide = parts[1].Trim();

            rightSide = rightSide.Replace("{", "").Replace("}", "");

            if (!leftSide.Contains("[") || !leftSide.Contains("]")) return false;

            string typeName = "";
            string arrayPart = leftSide;
            bool hasExplicitType = false;

            string[] typePrefixes = { "int ", "float ", "double ", "char ", "bool ", "string " };

            foreach (string prefix in typePrefixes)
            {
                if (leftSide.StartsWith(prefix))
                {
                    typeName = prefix.Trim();
                    arrayPart = leftSide.Replace(prefix, "");
                    hasExplicitType = true;
                    break;
                }
            }

            int openIndex = arrayPart.IndexOf('[');
            int closeIndex = arrayPart.IndexOf(']');

            if (openIndex == -1 || closeIndex == -1) return false;

            string arrayName = arrayPart.Substring(0, openIndex).Trim();
            string bracketText =
                arrayPart.Substring(openIndex + 1, closeIndex - openIndex - 1).Trim();

            if (arrayName == "") return false;
            if (!IsValidVariableName(arrayName)) return false;

            // Case 1: update an existing element, e.g. marks[2]=85
            if (!hasExplicitType && IsValidInt(bracketText) && !rightSide.Contains(","))
            {
                if (GetExistingArrayType(arrayName) == "") return false;

                return UpdateArrayElement(arrayName, int.Parse(bracketText), rightSide);
            }

            // Case 2: allocate a new array, e.g. int marks[5]=70,80,65,90,75
            //
            // Note the bracket contents are NEVER checked against the number
            // of values supplied. `int marks[9]=1,2` allocates two elements,
            // and `int marks[2]=1,2,3` allocates three. Source's declared size
            // is decoration; the value list is what counts. Preserved.
            if (!hasExplicitType)
            {
                string[] firstSplit = rightSide.Split(',');
                typeName = InferTypeFromValue(firstSplit.Length > 0 ? firstSplit[0].Trim() : "");
            }

            if (typeName == "" || typeName == "unknown") return false;

            List<string> values = new List<string>();

            foreach (string item in rightSide.Split(','))
            {
                string cleanedItem = item.Trim();
                if (cleanedItem != "") values.Add(cleanedItem);
            }

            if (values.Count == 0) return false;

            return AllocateArray(typeName, arrayName, values);
        }

        // ----- Inspection -----

        /// Godot: func find_memory_record(record_name) -> Dictionary
        /// Exact match first, then a fallback that lets a student type "sum"
        /// and find the function-frame record stored as "sum:local".
        private MemoryRecord FindMemoryRecord(string recordName)
        {
            string cleanedName = (recordName ?? "").Trim();

            if (cleanedName == "") return null;

            foreach (MemoryRecord record in memoryRecords)
            {
                if (record.Name == cleanedName) return record;
            }

            foreach (MemoryRecord record in memoryRecords)
            {
                if (!record.Name.Contains(":")) continue;

                if (record.Name.Split(':')[0] == cleanedName) return record;
            }

            return null;
        }

        /// Godot: func inspect_memory_record(record_name) -> bool
        /// Three different messages depending on what the record is: an array
        /// element names its array, a function-frame variable names its frame
        /// and says it is temporary, everything else reports type and size.
        public bool InspectMemoryRecord(string recordName)
        {
            if (isAnimating) return false;

            string cleanedName = (recordName ?? "").Trim();

            if (cleanedName == "")
            {
                inspectionMessage = "Inspect: Please enter a variable or memory record name.";
                lastActionMessage = inspectionMessage;
                return false;
            }

            MemoryRecord record = FindMemoryRecord(cleanedName);

            if (record == null)
            {
                currentFocusName = "";
                currentFocusAction = "";
                inspectionMessage =
                    "Inspect: No memory record named " + cleanedName + " was found.";
                lastActionMessage = inspectionMessage;
                currentCodeLine = "Inspect " + cleanedName;
                DrawMemory(true);
                return false;
            }

            currentFocusName = record.Name;
            currentFocusAction = "INSPECTED";
            currentPreviousValue = "";
            currentNewValue = record.Value;

            currentCodeLine = "Inspect " + record.Name;

            if (!string.IsNullOrEmpty(record.ArrayName))
            {
                inspectionMessage =
                    "Inspect: " + record.Name +
                    " is an array element of " + record.ArrayName +
                    ". It stores " + record.Value +
                    " at address " + record.Address +
                    " and uses " + record.Size + ".";
            }
            else if (!string.IsNullOrEmpty(record.Frame))
            {
                inspectionMessage =
                    "Inspect: " + record.Name +
                    " belongs to the function frame " + record.Frame +
                    ". It stores " + record.Value +
                    " at address " + record.Address +
                    " and is temporary while the function runs.";
            }
            else
            {
                inspectionMessage =
                    "Inspect: " + record.Name +
                    " is a " + record.Type +
                    " variable. It stores " + record.Value +
                    " at address " + record.Address +
                    " and uses " + record.Size + ".";
            }

            lastActionMessage = inspectionMessage;

            DrawMemory(true);
            return true;
        }

        // Godot: func get_inspection_message() -> String
        public string GetInspectionMessage()
        {
            if (inspectionMessage == "") return "No memory record has been inspected yet.";
            return inspectionMessage;
        }

        // -----------------------------------------------------------------
        // The prediction flow — Godot: prepare_next_memory_prediction(),
        // execute_pending_memory_step(), step_memory(), and their accessors.
        //
        // *** THIS IS COMPLETE IN SOURCE AND UNREACHABLE THERE. ***
        // handle_memory_step() is the only function that calls
        // prepare_next_memory_prediction(), and nothing in
        // ar_learning_screen.gd calls handle_memory_step(). So
        // awaiting_prediction is permanently false in the Godot build, and
        // every branch that depends on it — the 450px dashboard height, the
        // "Prediction task:" and "Next statement:" filters in
        // get_memory_mode_details_block(), all six text markers in
        // is_memory_variable_prediction_active() — is dead.
        //
        // The Predict Step button instead calls handle_memory_set_variable(),
        // the same function Set/Inspect calls, so the two buttons are
        // identical in the Godot build.
        //
        // MemoryController wires it up, at the user's decision — see its
        // "Predict Step" porting note. The label says "Predict Step" and the
        // Variables instruction says "Use Predict Step for guided
        // prediction", so source's intent is not in doubt; only one call is
        // missing.
        // -----------------------------------------------------------------

        private bool awaitingPrediction;
        private int pendingMemoryStep;
        private string predictionPrompt = "";
        private string expectedPrediction = "";
        private string pendingCodeLine = "";
        private int executionStep;

        public bool IsWaitingForPrediction() => awaitingPrediction;
        public string GetPredictionPrompt() => predictionPrompt;
        public string GetExpectedPrediction() => expectedPrediction;
        public string GetPendingCodeLine() => pendingCodeLine;

        /// Godot: func normalize_prediction_value(text_value)
        /// Lowercases, takes the right-hand side of any "=", drops a trailing
        /// semicolon, and strips one layer of matching quotes — so a student
        /// answering `age = 21;`, `21`, or `'A'` is judged on the value.
        public static string NormalizePredictionValue(string textValue)
        {
            string cleaned = (textValue ?? "").Trim().ToLowerInvariant();

            if (cleaned.Contains("="))
            {
                string[] parts = cleaned.Split('=');
                if (parts.Length == 2) cleaned = parts[1].Trim();
            }

            if (cleaned.EndsWith(";")) cleaned = cleaned.Substring(0, cleaned.Length - 1);

            cleaned = cleaned.Trim();

            if (cleaned.Length >= 2 && cleaned.StartsWith("'") && cleaned.EndsWith("'"))
            {
                cleaned = cleaned.Substring(1, cleaned.Length - 2);
            }

            if (cleaned.Length >= 2 && cleaned.StartsWith("\"") && cleaned.EndsWith("\""))
            {
                cleaned = cleaned.Substring(1, cleaned.Length - 2);
            }

            return cleaned;
        }

        /// Godot: func prepare_next_memory_prediction() -> bool
        /// The five prompts line up one-for-one with step_memory()'s five
        /// steps, which is what lets execute_pending_memory_step() rewind
        /// execution_step and let the normal trace run the prepared step.
        public bool PrepareNextMemoryPrediction()
        {
            if (isAnimating) return false;
            if (awaitingPrediction) return true;

            int nextStep = executionStep + 1;

            switch (nextStep)
            {
                case 1:
                    pendingCodeLine = "int age = 20;";
                    predictionPrompt = "Predict the value that will be stored in age.";
                    expectedPrediction = "20";
                    break;

                case 2:
                    pendingCodeLine = "float price = 15.5;";
                    predictionPrompt = "Predict the value that will be stored in price.";
                    expectedPrediction = "15.5";
                    break;

                case 3:
                    pendingCodeLine = "char grade = 'A';";
                    predictionPrompt = "Predict the value that will be stored in grade.";
                    expectedPrediction = "'A'";
                    break;

                case 4:
                    pendingCodeLine = "age = 21;";
                    predictionPrompt =
                        "Predict the new value that will replace the old value of age.";
                    expectedPrediction = "21";
                    break;

                case 5:
                    pendingCodeLine = "bool passed = true;";
                    predictionPrompt = "Predict the value that will be stored in passed.";
                    expectedPrediction = "true";
                    break;

                default:
                    awaitingPrediction = false;
                    pendingMemoryStep = 0;
                    predictionPrompt = "";
                    expectedPrediction = "";
                    pendingCodeLine = "Program finished.";
                    currentCodeLine = "Program finished.";
                    lastActionMessage = "The step-by-step memory trace has finished.";
                    DrawMemory(true);
                    return false;
            }

            pendingMemoryStep = nextStep;
            awaitingPrediction = true;
            currentCodeLine = pendingCodeLine;
            lastActionMessage = predictionPrompt;

            DrawMemory(true);

            return true;
        }

        /// Godot: func step_memory()
        /// The five-statement Variables trace. Reachable only through the
        /// prediction flow — nothing else calls it, in source or here.
        private IEnumerator StepMemory(Action<string> onComplete = null)
        {
            if (isAnimating) { onComplete?.Invoke(null); yield break; }

            executionStep++;

            switch (executionStep)
            {
                case 1:
                    currentCodeLine = "int age = 20;";
                    AddOrUpdateVariable("int", "age", "20");
                    break;

                case 2:
                    currentCodeLine = "float price = 15.5;";
                    AddOrUpdateVariable("float", "price", "15.5");
                    break;

                case 3:
                    currentCodeLine = "char grade = 'A';";
                    AddOrUpdateVariable("char", "grade", "'A'");
                    break;

                case 4:
                    currentCodeLine = "age = 21;";
                    AddOrUpdateVariable("int", "age", "21");
                    break;

                case 5:
                    currentCodeLine = "bool passed = true;";
                    AddOrUpdateVariable("bool", "passed", "true");
                    break;

                default:
                    currentCodeLine = "Program finished.";
                    currentFocusName = "";
                    DrawMemory(true);
                    onComplete?.Invoke(null);
                    yield break;
            }

            yield return HighlightCurrentRecord();

            onComplete?.Invoke(currentCodeLine);
        }

        /// Godot: func execute_pending_memory_step(user_prediction) -> Dictionary
        public sealed class PredictionResult
        {
            public string ExecutedLine;
            public bool Correct;
            public string Expected = "";
            public string UserAnswer = "";
            public string Message = "";
        }

        public IEnumerator ExecutePendingMemoryStep(
            string userPrediction, Action<PredictionResult> onComplete)
        {
            PredictionResult result = new PredictionResult
            {
                ExecutedLine = null,
                Correct = false,
                Expected = expectedPrediction,
                UserAnswer = userPrediction,
                Message = ""
            };

            if (!awaitingPrediction)
            {
                result.Message = "No prediction is currently waiting.";
                onComplete?.Invoke(result);
                yield break;
            }

            bool wasCorrect =
                NormalizePredictionValue(userPrediction) ==
                NormalizePredictionValue(expectedPrediction);

            awaitingPrediction = false;

            // Godot's comment: "Make sure the normal step_memory() executes
            // the prepared step." Rewinding by one is what makes the two
            // counters agree.
            executionStep = pendingMemoryStep - 1;

            string executedLine = null;
            yield return StepMemory(line => executedLine = line);

            result.ExecutedLine = executedLine;
            result.Correct = wasCorrect;
            result.Expected = expectedPrediction;

            result.Message = wasCorrect
                ? "Correct. The predicted value was " + expectedPrediction + "."
                : "Not correct. The expected value was " + expectedPrediction + ".";

            pendingMemoryStep = 0;
            predictionPrompt = "";
            expectedPrediction = "";
            pendingCodeLine = "";

            onComplete?.Invoke(result);
        }


        // -----------------------------------------------------------------
        // Phase 26e — Trace Exercise, Debug Exercise, Final Assessment.
        //
        // Three question/answer banks with identical machinery: start an
        // item, hold it until an answer arrives, judge it with the same
        // normalize_prediction_value() the prediction flow uses, then REPLACE
        // the whole table with the exercise's stated final memory state so
        // the student sees what the code would actually have produced.
        //
        // Godot writes all three out separately, three times over, with the
        // identifiers renamed. They are one shape here.
        // -----------------------------------------------------------------

        /// One entry of trace_exercises / debug_exercises /
        /// final_assessment_items. FinalRecords are PARTIAL records — source
        /// gives only name/type/value (+ array/index for array elements) and
        /// lets add_trace_final_record() compute the address and size.
        public sealed class MemoryExercise
        {
            public readonly string Code;
            public readonly string Question;
            public readonly string Answer;
            public readonly string Focus;
            public readonly string Explanation;
            public readonly MemoryRecord[] FinalRecords;

            public MemoryExercise(
                string code, string question, string answer, string focus,
                string explanation, MemoryRecord[] finalRecords)
            {
                Code = code;
                Question = question;
                Answer = answer;
                Focus = focus;
                Explanation = explanation;
                FinalRecords = finalRecords;
            }
        }

        private static MemoryRecord Spec(string name, string type, string value)
        {
            return new MemoryRecord { Name = name, Type = type, Value = value };
        }

        private static MemoryRecord ArraySpec(string name, string type, string value, string arrayName, int index)
        {
            return new MemoryRecord
            {
                Name = name, Type = type, Value = value, ArrayName = arrayName, Index = index
            };
        }

        // Godot: var trace_exercises
        private static readonly MemoryExercise[] TraceExercises =
        {
            new MemoryExercise(
                "int x = 4; int y = 6; x = y + 2;",
                "What value is stored in x after the last line?", "8", "x",
                "y stores 6, so y + 2 gives 8. Therefore, x is updated from 4 to 8.",
                new[] { Spec("x", "int", "8"), Spec("y", "int", "6") }),

            new MemoryExercise(
                "int a = 10; int b = 3; b = a - b;",
                "What value is stored in b after the last line?", "7", "b",
                "a stores 10 and b stores 3. The expression a - b gives 7, so b becomes 7.",
                new[] { Spec("a", "int", "10"), Spec("b", "int", "7") }),

            new MemoryExercise(
                "int score = 45; if (score >= 50) result = 1; else result = 0;",
                "What value is stored in result?", "0", "result",
                "score is 45, so score >= 50 is false. The else branch runs and result becomes 0.",
                new[] { Spec("score", "int", "45"), Spec("result", "int", "0") }),

            new MemoryExercise(
                "int marks[3] = {70, 80, 90}; marks[1] = 85;",
                "What value is stored in marks[1]?", "85", "marks[1]",
                "Only index 1 is updated. marks[1] changes from 80 to 85.",
                new[]
                {
                    ArraySpec("marks[0]", "int", "70", "marks", 0),
                    ArraySpec("marks[1]", "int", "85", "marks", 1),
                    ArraySpec("marks[2]", "int", "90", "marks", 2)
                }),

            new MemoryExercise(
                "int p = 5; int q = p; p = 9;",
                "What value is stored in q after p changes?", "5", "q",
                "q received a copy of p's value when p was 5. Changing p later does not change q.",
                new[] { Spec("p", "int", "9"), Spec("q", "int", "5") })
        };

        /// Godot: var debug_exercises
        /// **FOUR entries, against a total-questions constant of FIVE.**
        /// See MemoryController's note on the repeated-question guard, which
        /// is what actually ends this exercise.
        private static readonly MemoryExercise[] DebugExercises =
        {
            new MemoryExercise(
                "1. int age = 20;\n2. age = \"Mike\";",
                "Which line causes a type error?", "2", "age",
                "Line 2 is wrong because age is an int, but \"Mike\" is a string. " +
                "The stored value remains 20.",
                new[] { Spec("age", "int", "20") }),

            new MemoryExercise(
                "1. int marks[3] = {70, 80, 90};\n2. marks[3] = 100;",
                "Which line tries to access an invalid array index?", "2", "marks[2]",
                "Line 2 is wrong because marks[3] is outside the valid indexes. " +
                "For size 3, valid indexes are 0, 1, and 2.",
                new[]
                {
                    ArraySpec("marks[0]", "int", "70", "marks", 0),
                    ArraySpec("marks[1]", "int", "80", "marks", 1),
                    ArraySpec("marks[2]", "int", "90", "marks", 2)
                }),

            new MemoryExercise(
                "1. char grade = 'A';\n2. grade = 'AB';",
                "Which line assigns an invalid char value?", "2", "grade",
                "Line 2 is wrong because a char should store one character only. " +
                "'AB' has two characters.",
                new[] { Spec("grade", "char", "'A'") }),

            new MemoryExercise(
                "1. bool passed = true;\n2. passed = yes;",
                "Which line assigns an invalid bool value?", "2", "passed",
                "Line 2 is wrong because a bool should store true or false, not yes.",
                new[] { Spec("passed", "bool", "true") })
        };

        // Godot: var final_assessment_items
        private static readonly MemoryExercise[] FinalAssessmentItems =
        {
            new MemoryExercise(
                "int x = 4;\nint y = 6;\nx = y + 2;",
                "What value is stored in x after the last line?", "8", "x",
                "y stores 6, so y + 2 gives 8. Therefore, x becomes 8.",
                new[] { Spec("x", "int", "8"), Spec("y", "int", "6") }),

            new MemoryExercise(
                "float price = 10.5;\nprice = 12.5;",
                "What value is stored in price after the update?", "12.5", "price",
                "The original value 10.5 is replaced by 12.5 in the same memory location.",
                new[] { Spec("price", "float", "12.5") }),

            new MemoryExercise(
                "int marks[3] = {70, 80, 90};\nmarks[0] = 75;",
                "What value is stored in marks[0]?", "75", "marks[0]",
                "Only index 0 is updated. marks[0] changes from 70 to 75.",
                new[]
                {
                    ArraySpec("marks[0]", "int", "75", "marks", 0),
                    ArraySpec("marks[1]", "int", "80", "marks", 1),
                    ArraySpec("marks[2]", "int", "90", "marks", 2)
                }),

            new MemoryExercise(
                "int score = 55;\nif (score >= 50) result = 1;\nelse result = 0;",
                "What value is stored in result?", "1", "result",
                "score is 55, so score >= 50 is true. The true branch runs and result becomes 1.",
                new[] { Spec("score", "int", "55"), Spec("result", "int", "1") }),

            new MemoryExercise(
                "int a = 10;\nint b = 20;\nint sum = a + b;",
                "What value is stored in sum?", "30", "sum",
                "a stores 10 and b stores 20. Therefore, a + b gives 30.",
                new[]
                {
                    Spec("a", "int", "10"), Spec("b", "int", "20"), Spec("sum", "int", "30")
                })
        };

        /// The three banks are addressed by activity name so the controller
        /// can drive all of them through one code path.
        public enum AssessmentBank { Trace, Debug, Final }

        private static MemoryExercise[] BankFor(AssessmentBank bank)
        {
            switch (bank)
            {
                case AssessmentBank.Trace: return TraceExercises;
                case AssessmentBank.Debug: return DebugExercises;
                default: return FinalAssessmentItems;
            }
        }

        // ----- Per-bank state (Godot keeps three copies of each) -----

        private readonly Dictionary<AssessmentBank, int> bankIndex =
            new Dictionary<AssessmentBank, int>
            {
                { AssessmentBank.Trace, -1 }, { AssessmentBank.Debug, -1 }, { AssessmentBank.Final, -1 }
            };

        private readonly Dictionary<AssessmentBank, MemoryExercise> bankCurrent =
            new Dictionary<AssessmentBank, MemoryExercise>
            {
                { AssessmentBank.Trace, null }, { AssessmentBank.Debug, null }, { AssessmentBank.Final, null }
            };

        private readonly Dictionary<AssessmentBank, bool> bankAwaiting =
            new Dictionary<AssessmentBank, bool>
            {
                { AssessmentBank.Trace, false }, { AssessmentBank.Debug, false }, { AssessmentBank.Final, false }
            };

        /// Godot: final_assessment_score, kept only for the Final bank —
        /// the other two count in ar_learning_screen.gd instead. Ported
        /// where source puts it even though the controller also counts, so
        /// start_final_assessment_item()'s own completion message stays
        /// truthful.
        private int finalAssessmentScore;

        public bool IsWaitingForTraceAnswer() => bankAwaiting[AssessmentBank.Trace];
        public bool IsWaitingForDebugAnswer() => bankAwaiting[AssessmentBank.Debug];
        public bool IsWaitingForFinalAssessmentAnswer() => bankAwaiting[AssessmentBank.Final];

        public string GetExerciseCode(AssessmentBank bank)
        {
            MemoryExercise current = bankCurrent[bank];
            return current == null ? "" : current.Code;
        }

        public string GetExerciseQuestion(AssessmentBank bank)
        {
            MemoryExercise current = bankCurrent[bank];
            return current == null ? "" : current.Question;
        }

        public bool IsWaitingForAnswer(AssessmentBank bank) => bankAwaiting[bank];

        /// Godot: func add_trace_final_record(record_data)
        /// Shared by all three banks in source too — debug and final both
        /// call the trace-named helper.
        private void AddExerciseFinalRecord(MemoryRecord spec)
        {
            if (spec == null || spec.Name == "") return;

            string recordType = spec.Type == "" ? "int" : spec.Type;

            MemoryRecord record = new MemoryRecord
            {
                Address = GetAddressForByteOffset(GetTotalAllocatedBytes()),
                Name = spec.Name,
                Type = recordType,
                Value = spec.Value,
                Size = GetTypeSize(recordType)
            };

            if (!string.IsNullOrEmpty(spec.ArrayName)) record.ArrayName = spec.ArrayName;
            if (spec.Index >= 0) record.Index = spec.Index;

            memoryRecords.Add(record);
        }

        /// Godot: func start_trace_exercise() / start_debug_exercise() /
        /// start_final_assessment_item()
        ///
        /// The index ADVANCES then WRAPS: once past the last item it returns
        /// to 0 and re-serves question 1. That wrap is why the controller
        /// keeps a set of question keys — it is the only way to tell "next
        /// question" from "we have looped".
        ///
        /// Final Assessment is the exception: it does NOT wrap. It returns
        /// false once the index passes the end, and writes its own completion
        /// message. Preserved.
        public bool StartExercise(AssessmentBank bank)
        {
            if (isAnimating) return false;

            MemoryExercise[] items = BankFor(bank);

            if (items.Length == 0)
            {
                lastActionMessage = bank == AssessmentBank.Trace
                    ? "No trace exercises are available."
                    : bank == AssessmentBank.Debug
                        ? "No debug exercises are available."
                        : "No final assessment items are available.";
                return false;
            }

            if (bank == AssessmentBank.Final)
            {
                if (bankIndex[bank] == -1) finalAssessmentScore = 0;

                bankIndex[bank] = bankIndex[bank] + 1;

                if (bankIndex[bank] >= items.Length)
                {
                    bankAwaiting[bank] = false;

                    lastActionMessage =
                        "Final Assessment completed. Score: " + finalAssessmentScore +
                        " out of " + items.Length + ".";

                    currentCodeLine = "Final Assessment completed.";

                    DrawMemory(true);
                    return false;
                }
            }
            else
            {
                // Source clears the table BEFORE advancing for trace/debug,
                // and AFTER the bounds check for final. Preserved.
                memoryRecords.Clear();

                bankIndex[bank] = bankIndex[bank] + 1;

                if (bankIndex[bank] >= items.Length) bankIndex[bank] = 0;
            }

            if (bank == AssessmentBank.Final) memoryRecords.Clear();

            bankCurrent[bank] = items[bankIndex[bank]];
            bankAwaiting[bank] = true;

            currentFocusName = "";
            currentFocusAction = "";
            currentPreviousValue = "";
            currentNewValue = "";

            currentCodeLine = bankCurrent[bank].Code;

            string label = bank == AssessmentBank.Trace ? "Trace Exercise: "
                : bank == AssessmentBank.Debug ? "Debug Exercise: "
                : "Final Assessment: ";

            lastActionMessage = label + bankCurrent[bank].Question;

            DrawMemory(true);

            return true;
        }

        /// Godot: func apply_trace_final_memory() / apply_debug_final_memory()
        /// / apply_final_assessment_memory()
        ///
        /// Replaces the ENTIRE table with the exercise's stated end state.
        /// This is what makes the exercises teach: the student answers, then
        /// sees the memory the code would really have produced, with the
        /// focus row marked TRACE RESULT / DEBUG RESULT / FINAL RESULT.
        private void ApplyExerciseFinalMemory(AssessmentBank bank)
        {
            memoryRecords.Clear();

            MemoryExercise current = bankCurrent[bank];
            if (current == null) return;

            foreach (MemoryRecord spec in current.FinalRecords)
            {
                AddExerciseFinalRecord(spec);
            }

            currentFocusName = current.Focus;
            currentFocusAction = bank == AssessmentBank.Trace ? "TRACE_RESULT"
                : bank == AssessmentBank.Debug ? "DEBUG_RESULT"
                : "FINAL_ASSESSMENT_RESULT";
            currentPreviousValue = "";
            currentNewValue = current.Answer;

            DrawMemory(true);
        }

        public sealed class AnswerResult
        {
            public bool Correct;
            public string Expected = "";
            public string UserAnswer = "";
            public string Message = "";
            public string Explanation = "";
            public int Score;
            public int Total;
        }

        /// Godot: func check_trace_answer() / check_debug_answer() /
        /// check_final_assessment_answer()
        ///
        /// Judged with normalize_prediction_value() — the same lenient
        /// comparison the prediction flow uses, so "2", "2;" and " 2 " all
        /// pass, and a char answer works with or without quotes.
        public AnswerResult CheckExerciseAnswer(AssessmentBank bank, string userAnswer)
        {
            MemoryExercise[] items = BankFor(bank);

            AnswerResult result = new AnswerResult
            {
                Correct = false,
                Expected = "",
                UserAnswer = userAnswer,
                Message = "",
                Explanation = "",
                Score = finalAssessmentScore,
                Total = items.Length
            };

            if (!bankAwaiting[bank])
            {
                result.Message = bank == AssessmentBank.Trace
                    ? "No trace exercise is waiting for an answer."
                    : bank == AssessmentBank.Debug
                        ? "No debug exercise is waiting for an answer."
                        : "No final assessment item is waiting for an answer.";
                return result;
            }

            MemoryExercise current = bankCurrent[bank];
            string expectedAnswer = current != null ? current.Answer : "";

            bool wasCorrect =
                NormalizePredictionValue(userAnswer) == NormalizePredictionValue(expectedAnswer);

            bankAwaiting[bank] = false;

            // Only the Final bank keeps a module-side score, and it is
            // incremented BEFORE the table is rebuilt. Source's order.
            if (bank == AssessmentBank.Final && wasCorrect) finalAssessmentScore++;

            ApplyExerciseFinalMemory(bank);

            string explanation = current != null ? current.Explanation : "";

            lastActionMessage = wasCorrect
                ? "Correct. " + explanation
                : "Not correct. Expected answer: " + expectedAnswer + ". " + explanation;

            currentCodeLine = current != null ? current.Code : "";

            result.Correct = wasCorrect;
            result.Expected = expectedAnswer;
            result.Message = lastActionMessage;
            result.Explanation = explanation;
            result.Score = finalAssessmentScore;

            return result;
        }

        /// Godot: reset_memory() and clear_activity_memory() both clear the
        /// three awaiting flags; reset_memory() additionally resets the
        /// indices, the current items and the final score.
        private void ResetExerciseState(bool full)
        {
            bankAwaiting[AssessmentBank.Trace] = false;
            bankAwaiting[AssessmentBank.Debug] = false;
            bankAwaiting[AssessmentBank.Final] = false;

            if (!full) return;

            bankIndex[AssessmentBank.Trace] = -1;
            bankIndex[AssessmentBank.Debug] = -1;
            bankIndex[AssessmentBank.Final] = -1;

            bankCurrent[AssessmentBank.Trace] = null;
            bankCurrent[AssessmentBank.Debug] = null;
            bankCurrent[AssessmentBank.Final] = null;

            finalAssessmentScore = 0;
        }

        // -----------------------------------------------------------------
        // Dashboard text sources
        // -----------------------------------------------------------------

        /// Godot: func get_status_text() -> String
        /// The array-element branch cannot fire until Phase 26c adds arrays,
        /// but it is ported now so the string is never rewritten later.
        public string GetStatusText()
        {
            if (memoryRecords.Count == 0)
            {
                return "Memory status: no variables allocated.";
            }

            int arrayElements = 0;
            int normalVariables = 0;

            foreach (MemoryRecord record in memoryRecords)
            {
                if (!string.IsNullOrEmpty(record.ArrayName)) arrayElements++;
                else normalVariables++;
            }

            if (arrayElements > 0)
            {
                return
                    "Memory status: " + normalVariables + " variable(s), " +
                    arrayElements + " array element(s) stored.";
            }

            return "Memory status: " + normalVariables + " variable(s) currently stored.";
        }

        // Godot: func get_last_action_message() -> String
        public string GetLastActionMessage()
        {
            if (lastActionMessage == "") return "No memory action has been performed yet.";
            return lastActionMessage;
        }

        // Godot: func get_current_code_line() -> String
        public string GetCurrentCodeLine()
        {
            return currentCodeLine;
        }

        // -----------------------------------------------------------------
        // Overview framing — Godot: apply_memory_table_overview_style()
        // See porting note 7.
        // -----------------------------------------------------------------

        public void SetOverviewStyle(bool isOverview)
        {
            if (!defaultTransformSaved)
            {
                defaultLocalScale = transform.localScale;
                defaultLocalPosition = transform.localPosition;
                defaultTransformSaved = true;
            }

            if (isOverview)
            {
                transform.localScale = defaultLocalScale * overviewScaleMultiplier;
                transform.localPosition =
                    defaultLocalPosition + new Vector3(0f, -overviewDropUnits * TableScale, 0f);
            }
            else
            {
                transform.localScale = defaultLocalScale;
                transform.localPosition = defaultLocalPosition;
            }
        }
    
        // -----------------------------------------------------------------
        // Phase 29 fix (F1): a module can be disabled MID-ANIMATION.
        //
        // ChangeModuleController.ShowSelector() disables every module
        // unconditionally, including the one currently on screen, and the
        // Change Module button is never made non-interactable — so a student
        // can tap it during an animation. Disabling a GameObject halts its
        // coroutines instantly, killing the animation between
        // "isAnimating = true" and the "isAnimating = false" at its tail.
        //
        // Nothing else in the project ever cleared that flag. It stayed true
        // for the rest of the app run, and because Reset*() self-guards on
        // it, EnterModule() -> Reset*() silently did nothing on re-entry.
        // The module was then permanently unusable — every button answered
        // "Please wait. An animation is still running." until the app was
        // restarted. Reachable in ~3 seconds of ordinary use during a
        // Search or Traverse.
        //
        // OnDisable() is the primary hook: it fires on every deactivation
        // whatever the cause. ForceStopAnimation() is public as a backstop
        // for the case where the CONTROLLER's GameObject is disabled but
        // this module's is not — OnDisable() here would not run then, so
        // the controller also calls it from EnterModule().
        // -----------------------------------------------------------------

        private void OnDisable()
        {
            ForceStopAnimation();
        }

        /// <summary>
        /// Clears any in-flight animation state. Safe to call at any time.
        /// See the Phase 29 note above.
        /// </summary>
        public void ForceStopAnimation()
        {
            StopAllCoroutines();
            isAnimating = false;
        }
    }
}
