using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MartProg.Core
{
    /// <summary>
    /// Phase 51. Makes a TMP_Dropdown's OPEN LIST match the size of its own
    /// CLOSED BOX, so the options are as readable and as tappable as the
    /// control that reveals them.
    ///
    /// ---------------------------------------------------------------
    /// WHY THIS EXISTS
    /// ---------------------------------------------------------------
    ///
    /// A TMP_Dropdown draws its option rows by cloning a hidden child
    /// object, `Template/Viewport/Content/Item`. Nothing about that
    /// template inherits from the dropdown around it: its row height and
    /// its label's font size are whatever the TextMeshPro "Create >
    /// Dropdown" menu item wrote when the control was first added —
    /// 20 units tall, 14 pt label. Sizing the dropdown itself up, as
    /// every control strip in this project does, changes the closed box
    /// and the caption and leaves the template exactly where it was.
    ///
    /// Measured from a device screenshot of Memory's activity dropdown
    /// (Phase 51): the caption glyphs are ~13 px tall and each option row
    /// is ~17 px tall on a 1080-wide screen. At that phone's ~2.6 px/dp
    /// that is a **6.5 dp row** against Android's 48 dp minimum touch
    /// target — the participant is aiming at a target seven times smaller
    /// than the guideline, which is what "difficult to select an option"
    /// means in practice. Selection's mode dropdown is the same control
    /// with the same untouched template.
    ///
    /// ---------------------------------------------------------------
    /// WHY IT TAKES NO SIZES AS ARGUMENTS
    /// ---------------------------------------------------------------
    ///
    /// Both numbers this needs already exist correctly elsewhere on the
    /// same control, so it reads them rather than inventing them:
    ///
    ///   row height  <- the dropdown's OWN rect height. The closed box is
    ///                  already a deliberately-sized touch target that the
    ///                  participant hits without difficulty; a row exactly
    ///                  as tall is exactly as easy to hit. This also means
    ///                  the fix follows any future resize of the control
    ///                  without being re-tuned.
    ///
    ///   font size   <- the caption's OWN font size. The caption is the
    ///                  selected option rendered in the closed box, so an
    ///                  option in the list should be the same size. Making
    ///                  them equal is what removes the jarring shrink the
    ///                  moment the list opens.
    ///
    /// There is therefore no magic constant here to drift out of date,
    /// and no second place to keep in step when a control strip is
    /// restyled. The one free choice — how many rows are visible before
    /// the list scrolls — is a plain argument with a stated default.
    ///
    /// ---------------------------------------------------------------
    /// WHAT IT DOES NOT DO
    /// ---------------------------------------------------------------
    ///
    /// It does not touch the accessibility Text Size setting. Text Size
    /// scales the six dashboard information panels only (see
    /// InformationPanel.SetTextScale); the control strips, including
    /// these dropdowns, are outside it. Because the item font is taken
    /// from the caption, a dropdown styled here will follow the control
    /// strip if the strip is ever brought into that system — no change
    /// needed in this file.
    /// </summary>
    public static class DropdownStyler
    {
        /// How many option rows are visible before the list starts to
        /// scroll. Five is the most rows that fit above the dashboard on
        /// the 1080x2400 test device once rows are full-size; Memory has
        /// seven options and Selection three, so Memory scrolls by two
        /// and Selection does not scroll at all.
        public const int DefaultVisibleRows = 5;

        /// The smallest an over-long option label may shrink to, as a
        /// fraction of the caption's font size. 0.55 comfortably fits
        /// "Final Assessment", the longest option in the app.
        public const float MinimumLabelFontFraction = 0.55f;

        /// <summary>
        /// Resizes <paramref name="dropdown"/>'s option template so rows
        /// are as tall as the dropdown's own box and labelled at the same
        /// font size as its caption. Safe to call more than once and safe
        /// to call on a dropdown with no options yet — it edits the
        /// template, which is cloned each time the list opens.
        /// </summary>
        public static void MatchListToControl(TMP_Dropdown dropdown, int visibleRows = DefaultVisibleRows)
        {
            if (dropdown == null) return;

            // ----- the two values we copy from the control itself -----

            RectTransform dropdownRect = dropdown.transform as RectTransform;
            if (dropdownRect == null) return;

            float rowHeight = dropdownRect.rect.height;

            // A dropdown whose height comes from a layout group reports
            // zero until that group has run, which on this project's
            // control strips means it is still zero inside Start(). Force
            // the strip to lay out and re-read rather than depending on
            // every caller to defer by a frame. (Selection and Memory do
            // call from their existing one-frame InitializeDeferred()
            // coroutines, where this rebuild is a no-op; the module
            // selector has no such coroutine and relies on it.)
            if (rowHeight <= 1f)
            {
                RectTransform strip = dropdownRect.parent as RectTransform;
                LayoutRebuilder.ForceRebuildLayoutImmediate(strip != null ? strip : dropdownRect);
                rowHeight = dropdownRect.rect.height;
            }

            // Still zero: there is nothing meaningful to copy, and leaving
            // the template alone is better than writing a zero-height row.
            if (rowHeight <= 1f)
            {
                Debug.LogWarning(
                    "DropdownStyler: '" + dropdown.name + "' reported a rect height of " +
                    rowHeight.ToString("0.##") + ", so its option rows were left as they were. " +
                    "Call this after layout has run (Start, not Awake).");
                return;
            }

            float captionSize = ReadFontSize(dropdown.captionText);

            // ----- apply to the template -----

            TMP_Text itemLabel = dropdown.itemText;
            if (itemLabel == null || dropdown.template == null)
            {
                Debug.LogWarning(
                    "DropdownStyler: '" + dropdown.name + "' has no Item Label or no Template " +
                    "assigned, so its option rows could not be resized.");
                return;
            }

            if (captionSize > 0f)
            {
                // Phase 51 round 2. Setting a flat font size is not enough.
                // "Final Assessment" is one character longer than "Remove
                // Variable", which was the longest label that fitted, so at
                // the caption's size it WRAPPED onto a second line and the
                // row overflowed — reported from device, and the same
                // reason "Memory Management" reads badly in the module
                // selector. Two changes fix it for every label, present and
                // future, with no per-string tuning:
                //
                //   1. Wrapping off. An option is one line by definition;
                //      a two-line row among one-line rows reads as broken
                //      whether or not it is clipped.
                //   2. Auto-size DOWN only. fontSizeMax is the caption's
                //      size, so every label that fits renders at exactly
                //      the caption size as before. Only a label too long
                //      for the box shrinks, and only as far as it must.
                //
                // The floor stops a very long future option from shrinking
                // to something unreadable; if one ever hits the floor it
                // will visibly overflow instead, which is the honest
                // failure and easy to spot.
                DisableWordWrap(itemLabel);
                itemLabel.enableAutoSizing = true;
                itemLabel.fontSizeMax = captionSize;
                itemLabel.fontSizeMin = captionSize * MinimumLabelFontFraction;
            }

            // Item Label's parent is the Item row itself. Its own parent
            // is Content, the ScrollRect's content object. TMP_Dropdown
            // recomputes Content's height from the row height when the
            // list opens, so only the row and the outer Template need
            // setting here.
            RectTransform itemRow = itemLabel.rectTransform.parent as RectTransform;
            if (itemRow != null)
            {
                itemRow.sizeDelta = new Vector2(itemRow.sizeDelta.x, rowHeight);
            }

            int rows = Mathf.Max(1, visibleRows);

            // A caller may legitimately ask for every option to be visible
            // (the module selector does). Clamp to what the canvas can
            // actually show so that request can never produce a list taller
            // than the screen.
            Canvas canvas = dropdown.GetComponentInParent<Canvas>();
            RectTransform canvasRect = canvas != null ? canvas.transform as RectTransform : null;
            if (canvasRect != null && canvasRect.rect.height > 1f)
            {
                int maxRows = Mathf.Max(1, Mathf.FloorToInt(canvasRect.rect.height * 0.8f / rowHeight));
                rows = Mathf.Min(rows, maxRows);
            }

            dropdown.template.sizeDelta =
                new Vector2(dropdown.template.sizeDelta.x, rowHeight * rows);

            Debug.Log(
                "DropdownStyler: '" + dropdown.name + "' option rows set to " +
                rowHeight.ToString("0.#") + " units tall at " +
                (captionSize > 0f
                    ? "up to " + captionSize.ToString("0.#") + " pt, one line, shrinking no further than "
                      + (captionSize * MinimumLabelFontFraction).ToString("0.#") + " pt"
                    : "their authored size") +
                ", " + rows + " visible before scrolling.");
        }

        /// Turns word wrapping off. TextMeshPro renamed this control from
        /// `enableWordWrapping` to `textWrappingMode` and marked the old
        /// name obsolete, and which one a project has depends on its TMP
        /// version rather than on its Unity version. Choosing at runtime
        /// costs one reflection call at startup and cannot fail to
        /// compile on either — worth it here, because a wrong guess would
        /// be found only at build time.
        private static void DisableWordWrap(TMP_Text label)
        {
            System.Type type = label.GetType();

            System.Reflection.PropertyInfo modern = type.GetProperty("textWrappingMode");
            if (modern != null)
            {
                // TextWrappingModes.NoWrap is the zero member.
                modern.SetValue(label, System.Enum.ToObject(modern.PropertyType, 0), null);
                return;
            }

            System.Reflection.PropertyInfo legacy = type.GetProperty("enableWordWrapping");
            if (legacy != null)
            {
                legacy.SetValue(label, false, null);
            }
        }

        /// Reads a label's effective font size. With auto-sizing on,
        /// `fontSize` holds whatever the last layout pass computed, which
        /// before the first layout is not yet meaningful; `fontSizeMax` is
        /// the authored intent in that case.
        private static float ReadFontSize(TMP_Text label)
        {
            if (label == null) return 0f;
            return label.enableAutoSizing ? label.fontSizeMax : label.fontSize;
        }
    }
}
