// Form1_SettingsPreview.cs
// Adds a compact read-only settings readout panel to the Analysis tab,
// positioned between cbShowCircle and lbEmail.  The panel paints its own
// rows so label and value columns are perfectly aligned with no border.
//
// Usage:
//   1. Add this file to the project.
//   2. In Form1_Designer.cs, inside InitializeComponent():
//        a. After all other field declarations at the top, add:
//               settingsPreview = new SettingsPreviewPanel();
//        b. After the tabAnalysis.Controls.Add(...) block, add:
//               tabAnalysis.Controls.Add(settingsPreview);
//        c. After the cbShowCircle property block, add the settingsPreview block
//           shown in the comment at the bottom of this file.
//   3. Call RefreshSettingsPreview() after LoadSettings() and after SaveSettings().

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Spectrum
{
    // ── Lightweight owner-drawn panel ────────────────────────────────────────
    internal sealed class SettingsPreviewPanel : Panel
    {
        private const int RowHeight   = 16;   // px per row
        private const int LabelCol    = 0;    // left edge of label
        private const int ValueCol    = 158;  // left edge of value (fixed split)
        private const int RowPadY     = 1;    // extra vertical padding above text

        private static readonly Font  RowFont  = new Font("Segoe UI", 8.25f, FontStyle.Regular);

        //private static readonly Brush LabelBrush = new SolidBrush(Color.FromArgb(180, 180, 180));
        //private static readonly Brush ValueBrush = new SolidBrush(Color.FromArgb(230, 230, 230));
        private static readonly Brush LabelBrush = new SolidBrush(Color.FromArgb(150, 150, 150));
        private static readonly Brush ValueBrush = new SolidBrush(Color.FromArgb(180, 180, 180));

        // The rows to display — set by the owning form.
        public IReadOnlyList<(string Label, string Value)> Rows { get; private set; }
            = Array.Empty<(string, string)>();

        public SettingsPreviewPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint            |
                     ControlStyles.OptimizedDoubleBuffer, true);
            BackColor  = Color.Transparent;
            BorderStyle = BorderStyle.None;
            TabStop    = false;
        }

        public void SetRows(IReadOnlyList<(string Label, string Value)> rows)
        {
            Rows = rows;
            Invalidate();
        }

        // How many rows fit in the current height?
        public int VisibleRowCount => Math.Max(0, Height / RowHeight);

        protected override void OnPaint(PaintEventArgs e)
        {
            var g      = e.Graphics;
            var rows   = Rows;
            int maxRow = Math.Min(rows.Count, VisibleRowCount);
            int w      = Width;

            for (int i = 0; i < maxRow; i++)
            {
                int y = i * RowHeight + RowPadY;
                // Clip value text to available width
                var labelRect = new Rectangle(LabelCol, y, ValueCol - LabelCol - 4, RowHeight);
                var valueRect = new Rectangle(ValueCol, y, w - ValueCol, RowHeight);

                TextRenderer.DrawText(g, rows[i].Label, RowFont, labelRect, Color.FromArgb(190, 170, 170),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
                TextRenderer.DrawText(g, rows[i].Value, RowFont, valueRect, Color.FromArgb(170, 170, 170),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            }
        }
    }

    // ── Form1 partial — wires up the panel ───────────────────────────────────
    public partial class Form1
    {
        //private SettingsPreviewPanel settingsPreview = null!;

        // Call this after InitializeComponent() in the Form1 constructor, and
        // again after LoadSettings() / SaveSettings().
        private void InitSettingsPreview()
        {
            // Anchor: top = just below cbShowCircle, bottom = just above lbEmail.
            // cbShowCircle.Bottom ≈ 544, lbEmail.Top ≈ 874 (both in tabAnalysis coords).
            const int topY    = 549;   // a few px below cbShowCircle
            const int bottomY = 868;   // a few px above lbEmail

            settingsPreview.Location = new Point(7, topY);
            settingsPreview.Size     = new Size(tabAnalysis.ClientSize.Width - 14, bottomY - topY);
            settingsPreview.Anchor   = AnchorStyles.Top | AnchorStyles.Bottom
                                     | AnchorStyles.Left | AnchorStyles.Right;

            // Initial populate
            RefreshSettingsPreview();
        }

        // Build the ordered label/value list from current runtime fields.
        // Only settings that are numeric/bool and interesting at a glance are shown.
        // Order matches the Settings tab top-to-bottom.
        internal void RefreshSettingsPreview()
        {
            if (settingsPreview == null) return;

            var rows = new List<(string, string)>
            {
                ("FFT Size (2ⁿ)",         ((int)Math.Log2(FFT_SIZE)).ToString()),
                ("Low FFT",               _lowFftEnabled ? "on" : "off"),
                ("Crossover Note",        LOW_FFT_CROSSOVER_NOTE),
                ("Crossover Semitones",   LOW_FFT_CROSSOVER_SEMITONES.ToString("G4")),
                ("Tuning (Hz)",           tuning.ToString("G6")),
                ("Target FPS",            TARGET_FPS.ToString("G4")),
                ("Peaks Captured",        MAX_PEAKS_PER_FRAME.ToString()),
                ("Peak Min Rel",          PEAK_MIN_REL.ToString("G4")),
                ("Peak Spacing (¢)",      PEAK_MIN_SPACING_CENTS.ToString("G4")),
                ("Peak Gamma",            PEAK_GAMMA.ToString("G4")),
                ("Peak Mode",             PEAK_MODE.ToString()),
                ("Ridge Max Jump (¢)",    RIDGE_MAX_CENTS_JUMP.ToString("G4")),
                ("Ridge Miss Max",        RIDGE_MISS_MAX.ToString()),
                ("Ridge Freq EMA",        RIDGE_FREQ_EMA.ToString("G4")),
                ("Ridge Vel EMA",         RIDGE_VEL_EMA.ToString("G4")),
                ("Ridge Intensity EMA",   RIDGE_INTENSITY_EMA.ToString("G4")),
                ("Miss Fade Pow",         RidgeMissFadePow.ToString("G4")),
                ("Age Decay",             RidgeAgeDecay.ToString("G4")),
                ("Min Draw Alpha",        MinDrawAlpha.ToString("G4")),
                ("Merge Cents",           RIDGE_MERGE_CENTS.ToString("G4")),
                ("Merge Brightness",      RIDGE_MERGE_BRIGHTNESS_BOOST.ToString("G4")),
                ("Merge Width Add",       RIDGE_MERGE_WIDTH_ADD.ToString("G4")),
                ("Merge Width Decay",     RIDGE_MERGE_WIDTH_DECAY.ToString("G4")),
                ("Harm Cents Tol",        HARMONIC_FAMILY_CENTS_TOL.ToString("G4")),
                ("Harm Max Ratio",        HARMONIC_FAMILY_MAX_RATIO.ToString("G4")),
                ("Harm Suppression",      HARMONIC_SUPPRESSION.ToString("G4")),
                ("Chord Avg Frames",      CHORD_AVG_FRAMES.ToString()),
                ("Chord Out Penalty",     CHORD_OUT_PENALTY.ToString("G4")),
                ("Chord Ridges",          CHORD_RIDGES.ToString()),
                ("Key Mode Bias",         KEY_MODE_BIAS.ToString("G4")),
                ("Ridge Match LogHz",     RIDGE_MATCH_LOGHZ.ToString("G4")),
                ("Match Pred Boost",      RIDGE_MATCH_LOGHZ_PRED_BOOST.ToString("G4")),
                ("Max Col Shift",         MAX_COL_SHIFT.ToString("G4")),
                ("Level Smooth EMA",      LEVEL_SMOOTH_EMA.ToString("G4")),
            };

            settingsPreview.SetRows(rows);
        }
    }
}
