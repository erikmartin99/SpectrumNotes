// Copyright (c) 2026, Erik Martin
// Form1_CircleOfFifths.cs
//
// Circle of Fifths overlay drawn directly into the existing SpectrogramPanel (pic).
// When cbShowCircle is checked, Pic_Paint skips the spectrogram bitmap blit and
// calls DrawCircleOfFifths(g) instead.  Ridge tracking continues in the background.
//
// WIRING — two additions to existing files:
//
// 1. Form1_Load, after InitScaleDropdowns():
//        InitCircleOfFifths();
//
// 2. Pic_Paint, at the very top of the method body:
//        if (cbShowCircle.Checked)
//        {
//            DrawCircleOfFifths(e.Graphics, pic.Width, pic.Height);
//            return;
//        }

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace Spectrum
{
    public partial class Form1
    {
        // ── State (written from any thread, read on paint thread) ─────────────
        private volatile int _circleChordRoot = -1;   // pitch-class 0–11, or -1
        private volatile bool _circleChordIsMajor = true;
        private volatile int _circleKeyRoot = -1;   // pitch-class 0–11, or -1
        private volatile bool _circleKeyIsMajor = true;

        // ── Circle-of-fifths layout ───────────────────────────────────────────
        // Clockwise from 12 o'clock: C G D A E B F# Db Ab Eb Bb F
        private static readonly int[] CofMajorPc =
            { 0, 7, 2, 9, 4, 11, 6, 1, 8, 3, 10, 5 };

        // Relative minor pitch-class at the same CoF wedge index.
        private static readonly int[] CofMinorPc =
            { 9, 4, 11, 6, 1, 8, 3, 10, 5, 0, 7, 2 };

        private static readonly string[] CofMajorLabelSharp =
            { "C", "G", "D", "A", "E", "B", "F#", "C#", "Ab", "Eb", "Bb", "F" };
        private static readonly string[] CofMajorLabelFlat =
            { "C", "G", "D", "A", "E", "B", "Gb", "Db", "Ab", "Eb", "Bb", "F" };

        private static readonly string[] CofMinorLabelSharp =
            { "Am", "Em", "Bm", "F#m", "C#m", "G#m", "D#m", "Bbm", "Fm", "Cm", "Gm", "Dm" };
        private static readonly string[] CofMinorLabelFlat =
            { "Am", "Em", "Bm", "Gbm", "Dbm", "Abm", "Ebm", "Bbm", "Fm", "Cm", "Gm", "Dm" };

        // 6 consecutive CoF wedges span the diatonic set (IV I V ii vi iii).
        // Bracket starts 1 wedge counter-clockwise from the key wedge.
        private const int DiatonicSpan = 6;

        // ── Colour palette ────────────────────────────────────────────────────
        private static readonly Color ColCircleChordMaj = Color.FromArgb(220, 155, 50);  // amber
        private static readonly Color ColCircleChordMin = Color.FromArgb(95, 80, 210);  // violet
        private static readonly Color ColCircleKeyLine = Color.FromArgb(255, 255, 255);  // white
        private static readonly Color ColCircleDiatEdge = Color.FromArgb(70, 210, 140);  // mint
        private static readonly Color ColCircleWedgeMaj = Color.FromArgb(26, 26, 34);
        private static readonly Color ColCircleWedgeMin = Color.FromArgb(20, 20, 28);
        private static readonly Color ColCircleGrid = Color.FromArgb(52, 52, 62);
        private static readonly Color ColCircleLabelMaj = Color.FromArgb(205, 205, 225);
        private static readonly Color ColCircleLabelMin = Color.FromArgb(135, 135, 165);

        // ── Init (call from Form1_Load after InitScaleDropdowns) ─────────────
        private void InitCircleOfFifths()
        {
            cbShowCircle.CheckedChanged += (s, e) =>
            {
                pic.Invalidate();
                SaveSettings();
            };
        }

        // ── Called from SetChordLabelUi (Form1_AutoKey.cs) ───────────────────
        internal void UpdateCircleOfFifths(string chordText)
        {
            ParseChordForCircle(chordText, out int chordPc, out bool chordMaj);
            _circleChordRoot = chordPc;
            _circleChordIsMajor = chordMaj;

            if (InvokeRequired)
            {
                if (!IsDisposed && IsHandleCreated)
                    BeginInvoke((Action)SyncKeyStateAndInvalidate);
                return;
            }
            SyncKeyStateAndInvalidate();
        }

        private void SyncKeyStateAndInvalidate()
        {
            int rootIdx = cmbScaleRoot.SelectedIndex;  // 0=chromatic, 1..12=C..B
            int modeIdx = cmbScaleMode.SelectedIndex;
            _circleKeyRoot = rootIdx > 0 ? RootPitchClass[rootIdx] : -1;
            // Minor-flavoured modes: 5=Aeolian, 7=Harmonic Minor,
            //                        10=Pentatonic Minor, 11=Blues
            _circleKeyIsMajor = !(modeIdx == 5 || modeIdx == 7 ||
                                   modeIdx == 10 || modeIdx == 11);

            if (cbShowCircle.Checked && !pic.IsDisposed)
                pic.Invalidate();
        }

        // ── Main draw — called from Pic_Paint when cbShowCircle.Checked ──────
        internal void DrawCircleOfFifths(Graphics g, int w, int h)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Black);

            if (w < 80 || h < 80) return;

            float cx = w / 2f;
            float cy = h / 2f;
            float minDim = Math.Min(w, h);

            float rOuter = minDim * 0.470f;   // outer edge of major ring
            float rMid = minDim * 0.325f;   // major/minor ring boundary
            float rInner = minDim * 0.190f;   // inner edge of minor ring
            float rHole = minDim * 0.095f;   // centre hole radius

            bool useFlats = cbFlats?.Checked ?? false;
            var majLabels = useFlats ? CofMajorLabelFlat : CofMajorLabelSharp;
            var minLabels = useFlats ? CofMinorLabelFlat : CofMinorLabelSharp;

            // Snapshot volatile fields once.
            int chordPc = _circleChordRoot;
            bool chordMaj = _circleChordIsMajor;
            int keyPc = _circleKeyRoot;
            bool keyMaj = _circleKeyIsMajor;

            int keyCofIdx = FindCofIdx(keyPc, keyMaj);

            // ── Diatonic bracket (drawn first, behind wedges) ─────────────────
            if (keyCofIdx >= 0)
                DrawDiatonicBracket(g, cx, cy, rOuter, rInner, keyCofIdx);

            // ── Wedges ────────────────────────────────────────────────────────
            const float sweep = 360f / 12f;
            const float origin = -90f - sweep / 2f;  // 12 o'clock centred on wedge

            using var gridPen = new Pen(ColCircleGrid, 0.8f);

            for (int i = 0; i < 12; i++)
            {
                float sa = origin + i * sweep;

                // — Major outer wedge —
                bool majChordHit = chordMaj && chordPc == CofMajorPc[i];
                bool majKeyHit = keyMaj && keyPc == CofMajorPc[i];

                FillCofWedge(g, cx, cy, rMid, rOuter, sa, sweep,
                    majChordHit ? ColCircleChordMaj : ColCircleWedgeMaj);
                DrawCofWedgeBorder(g, cx, cy, rMid, rOuter, sa, sweep, gridPen);
                if (majKeyHit)
                    DrawCofWedgeBorder(g, cx, cy, rMid, rOuter, sa, sweep,
                        new Pen(ColCircleKeyLine, 2.5f));

                // — Minor inner wedge —
                bool minChordHit = !chordMaj && chordPc == CofMinorPc[i];
                bool minKeyHit = !keyMaj && keyPc == CofMinorPc[i];

                FillCofWedge(g, cx, cy, rInner, rMid, sa, sweep,
                    minChordHit ? ColCircleChordMin : ColCircleWedgeMin);
                DrawCofWedgeBorder(g, cx, cy, rInner, rMid, sa, sweep, gridPen);
                if (minKeyHit)
                    DrawCofWedgeBorder(g, cx, cy, rInner, rMid, sa, sweep,
                        new Pen(ColCircleKeyLine, 2.5f));

                // — Labels —
                float midRad = (sa + sweep / 2f) * (float)(Math.PI / 180.0);

                float lrMaj = (rMid + rOuter) / 2f;
                DrawCofLabel(g, majLabels[i],
                    cx + lrMaj * (float)Math.Cos(midRad),
                    cy + lrMaj * (float)Math.Sin(midRad),
                    majChordHit || majKeyHit ? Color.White : ColCircleLabelMaj,
                    majChordHit || majKeyHit ? 13f : 11f,
                    majChordHit || majKeyHit);

                float lrMin = (rInner + rMid) / 2f;
                DrawCofLabel(g, minLabels[i],
                    cx + lrMin * (float)Math.Cos(midRad),
                    cy + lrMin * (float)Math.Sin(midRad),
                    minChordHit || minKeyHit ? Color.White : ColCircleLabelMin,
                    minChordHit || minKeyHit ? 10f : 8.5f,
                    minChordHit || minKeyHit);
            }

            // ── Centre hole with chord symbol ─────────────────────────────────
            using (var b = new SolidBrush(Color.FromArgb(14, 14, 20)))
                g.FillEllipse(b, cx - rHole, cy - rHole, rHole * 2, rHole * 2);
            using (var p = new Pen(ColCircleGrid, 1f))
                g.DrawEllipse(p, cx - rHole, cy - rHole, rHole * 2, rHole * 2);

            string centreText = lastDetectedChordText ?? "—";
            if (centreText.Length > 7) centreText = centreText.Substring(0, 7);
            Color centreCol = chordPc < 0 ? Color.FromArgb(75, 75, 95)
                            : chordMaj ? ColCircleChordMaj
                                          : ColCircleChordMin;
            DrawCofLabel(g, centreText, cx, cy, centreCol, 14f, bold: true);
        }

        // ── Diatonic bracket ──────────────────────────────────────────────────
        private static void DrawDiatonicBracket(Graphics g,
            float cx, float cy, float rOuter, float rInner, int keyCofIdx)
        {
            const float sweep = 360f / 12f;
            const float origin = -90f - sweep / 2f;

            float startAngle = origin + ((keyCofIdx - 1 + 12) % 12) * sweep;
            float spanAngle = sweep * DiatonicSpan;

            FillCofWedge(g, cx, cy, rInner, rOuter, startAngle, spanAngle,
                Color.FromArgb(22, ColCircleDiatEdge));

            using var pen = new Pen(Color.FromArgb(175, ColCircleDiatEdge), 1.7f)
            { DashStyle = DashStyle.Dot };
            DrawCofWedgeBorder(g, cx, cy, rInner, rOuter, startAngle, spanAngle, pen);
        }

        // ── Geometry ──────────────────────────────────────────────────────────
        private static void FillCofWedge(Graphics g, float cx, float cy,
            float rIn, float rOut, float sa, float sw, Color color)
        {
            using var path = MakeCofWedgePath(cx, cy, rIn, rOut, sa, sw);
            using var brush = new SolidBrush(color);
            g.FillPath(brush, path);
        }

        private static void DrawCofWedgeBorder(Graphics g, float cx, float cy,
            float rIn, float rOut, float sa, float sw, Pen pen)
        {
            using var path = MakeCofWedgePath(cx, cy, rIn, rOut, sa, sw);
            g.DrawPath(pen, path);
        }

        private static GraphicsPath MakeCofWedgePath(float cx, float cy,
            float rIn, float rOut, float sa, float sw)
        {
            var p = new GraphicsPath();
            p.AddArc(cx - rOut, cy - rOut, rOut * 2, rOut * 2, sa, sw);
            p.AddArc(cx - rIn, cy - rIn, rIn * 2, rIn * 2, sa + sw, -sw);
            p.CloseFigure();
            return p;
        }

        private static void DrawCofLabel(Graphics g, string text,
            float x, float y, Color color, float emSize, bool bold = false)
        {
            using var font = new Font("Segoe UI", emSize,
                bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Point);
            var sz = g.MeasureString(text, font);
            using var brush = new SolidBrush(color);
            g.DrawString(text, font, brush, x - sz.Width / 2f, y - sz.Height / 2f);
        }

        // ── Lookup ────────────────────────────────────────────────────────────
        private static int FindCofIdx(int pc, bool major)
        {
            if (pc < 0) return -1;
            var arr = major ? CofMajorPc : CofMinorPc;
            for (int i = 0; i < 12; i++)
                if (arr[i] == pc) return i;
            return -1;
        }

        private static void ParseChordForCircle(string text, out int pc, out bool isMajor)
        {
            pc = -1;
            isMajor = true;
            if (string.IsNullOrWhiteSpace(text) || text == "—") return;

            text = text.Trim();
            pc = ParseCofRootPc(text, out int consumed);
            if (pc < 0) return;

            string rest = consumed < text.Length ? text.Substring(consumed) : "";
            // Minor: 'm' not followed by 'a' (which would be "maj").
            if (rest.Length >= 1 && rest[0] == 'm' &&
                !(rest.Length >= 3 &&
                  char.ToLower(rest[1]) == 'a' && char.ToLower(rest[2]) == 'j'))
                isMajor = false;
            else if (rest.StartsWith("min", StringComparison.OrdinalIgnoreCase))
                isMajor = false;
        }

        private static int ParseCofRootPc(string s, out int consumed)
        {
            consumed = 0;
            if (s.Length == 0) return -1;
            int idx = "ABCDEFG".IndexOf(char.ToUpper(s[0]));
            if (idx < 0) return -1;
            int[] base12 = { 9, 11, 0, 2, 4, 5, 7 };
            int pc = base12[idx];
            consumed = 1;
            if (s.Length > 1)
            {
                if (s[1] == '#') { pc = (pc + 1) % 12; consumed = 2; }
                else if (s[1] == 'b' || s[1] == '♭') { pc = (pc + 11) % 12; consumed = 2; }
            }
            return pc;
        }
    }
}
