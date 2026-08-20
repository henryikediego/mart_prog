using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

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

        [Tooltip("Godot pixel_size 0.010 converted to Unity world-text scale. " +
                 "TMP renders about 0.1 world units per fontSize unit at " +
                 "scale 1, so 0.010 / 0.1 = 0.1 — see porting note 9.")]
        [SerializeField] private float labelPixelScale = 0.1f;

        [Tooltip("Markers use Godot pixel_size 0.0105 rather than 0.010.")]
        [SerializeField] private float markerScaleMultiplier = 1.05f;

        [Tooltip("Godot: the 4.25 x-offset of the focus marker and action " +
                 "badge. It sits OUTSIDE the table — see porting note 10. " +
                 "Reduce if it falls off screen on a phone.")]
        [SerializeField] private float markerXOffset = 4.25f;

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

        private const float RowHeight = 0.52f;
        private const float RowGap = 0.11f;
        private const float RowWidth = 6.80f;
        private const float RowDepth = 0.16f;

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

            currentFocusName = "";
            currentFocusAction = "";
            currentPreviousValue = "";
            currentNewValue = "";
            removedMemoryName = "";
            removedScopeName = "";

            currentCodeLine = "Program not started.";
            lastActionMessage = "Memory has been cleared.";

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

        // Godot: func draw_memory(force_redraw := false)
        public void DrawMemory(bool forceRedraw = false)
        {
            if (isAnimating && !forceRedraw) return;

            ClearVisuals();

            float rowStep = (RowHeight + RowGap) * rowSpacingMultiplier;

            AddTitle("MEMORY TABLE", 0f, 0.85f, MarkerZ, titleColor);
            AddHeaderRow(0.35f);

            if (memoryRecords.Count == 0)
            {
                AddEmptyMessage(-0.25f);
                return;
            }

            for (int i = 0; i < memoryRecords.Count; i++)
            {
                float rowY = -0.25f - i * rowStep;
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
                x * s,
                arHeight + y * s,
                arDistance - z * s);
        }

        // Godot: func add_header_row(row_position: Vector3)
        private void AddHeaderRow(float rowY)
        {
            if (flowMarkers == null) return;

            CreateBox(
                new Vector3(RowWidth, 0.36f, RowDepth),
                Place(0f, rowY, 0f),
                headerRowColor,
                flowMarkers,
                "MemoryHeaderRow");

            AddTextLabel("ADDRESS", AddressX, rowY, LabelZ, HeaderFontSize, headerLabelColor, flowMarkers);
            AddTextLabel("NAME", NameX, rowY, LabelZ, HeaderFontSize, headerLabelColor, flowMarkers);
            AddTextLabel("TYPE", TypeX, rowY, LabelZ, HeaderFontSize, headerLabelColor, flowMarkers);

            // Source colours the VALUE header green like the value cells,
            // unlike the other four headers. Preserved.
            AddTextLabel("VALUE", ValueX, rowY, LabelZ, HeaderFontSize, valueColor, flowMarkers);

            AddTextLabel("SIZE", SizeX, rowY, LabelZ, HeaderFontSize, headerLabelColor, flowMarkers);
        }

        // Godot: func add_memory_row(record, row_position, is_focus)
        private void AddMemoryRow(MemoryRecord record, float rowY, bool isFocus)
        {
            if (memoryBlocks == null) return;

            Color color = isFocus ? focusRowColor : rowColor;

            Transform rowBg = CreateBox(
                new Vector3(RowWidth, RowHeight, RowDepth),
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

            // Source nudges ADDRESS_LAYOUT's marker up by 0.22 and leaves
            // every other action at row height. Preserved.
            float yOffset = currentFocusAction == "ADDRESS_LAYOUT" ? 0.22f : 0f;

            AddMarkerLabel(text, markerXOffset, rowY + yOffset, MarkerZ);
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
            AddMarkerLabel(text, markerXOffset, 0.35f, MarkerZ);
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
        private TextMeshPro AddTextLabel(
            string text, float x, float y, float z, int fontSize, Color color, Transform parent)
        {
            return BuildLabel(text, x, y, z, fontSize, color, parent, labelPixelScale, labelOutlineWidth);
        }

        /// Markers differ from labels only in pixel_size (0.0105 vs 0.010)
        /// and outline_size (10 vs 8) — porting note 9.
        private void AddMarkerLabel(string text, float x, float y, float z)
        {
            BuildLabel(
                text, x, y, z, MarkerFontSize, markerColor, highlightLayer,
                labelPixelScale * markerScaleMultiplier, markerOutlineWidth);
        }

        private TextMeshPro BuildLabel(
            string text, float x, float y, float z, int fontSize, Color color,
            Transform parent, float pixelScale, float outlineWidth)
        {
            if (parent == null) return null;

            GameObject labelObj = new GameObject("MemoryLabel");
            labelObj.transform.SetParent(parent, false);
            labelObj.transform.localPosition = Place(x, y, z);
            labelObj.transform.localRotation = Quaternion.identity;
            labelObj.transform.localScale = Vector3.one * pixelScale * TableScale;

            TextMeshPro tmp = labelObj.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = fontSize;
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
    }
}
