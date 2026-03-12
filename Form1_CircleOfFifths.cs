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
// 2. Pic_Paint, before the bitmapLock block:
//        if (cbShowCircle.Checked)
//        {
//            DrawCircleOfFifths(e.Graphics, pic.Width, pic.Height);
//            return;
//        }
//
// Key outline tiers (CoF distance from detected key wedge):
//   Distance 0  — bright white, thick (the key itself)
//   Distance 1  — medium white, normal (dominant & subdominant — 1 sharp/flat away)
//   Distance 2  — dim white, thin (2 sharps/flats away)
//
// The detected key name is drawn outside the circle at the angle of its wedge.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace Spectrum
{
    public partial class Form1
    {
        // ── State ─────────────────────────────────────────────────────────────
        private volatile int _circleChordRoot = -1;
        private volatile bool _circleChordIsMajor = true;
        private volatile int _circleKeyRoot = -1;
        private volatile bool _circleKeyIsMajor = true;

        // ── Layout tables ─────────────────────────────────────────────────────
        // CoF order clockwise from 12 o'clock: C G D A E B F# Db Ab Eb Bb F
        private static readonly int[] CofMajorPc =
            { 0, 7, 2, 9, 4, 11, 6, 1, 8, 3, 10, 5 };

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

        private const int DiatonicSpan = 6;

        // ── Palette ───────────────────────────────────────────────────────────
        private static readonly Color ColCircleChordMaj = Color.FromArgb(220, 155, 50);
        private static readonly Color ColCircleChordMin = Color.FromArgb(95, 80, 210);
        private static readonly Color ColCircleWedgeMaj = Color.FromArgb(26, 26, 34);
        private static readonly Color ColCircleWedgeMin = Color.FromArgb(20, 20, 28);
        private static readonly Color ColCircleGrid = Color.FromArgb(52, 52, 62);
        private static readonly Color ColCircleLabelMaj = Color.FromArgb(205, 205, 225);
        private static readonly Color ColCircleLabelMin = Color.FromArgb(135, 135, 165);
        private static readonly Color ColCircleDiatEdge = Color.FromArgb(70, 210, 140);
        // Tiered key outlines
        private static readonly Color ColKeyOutline0 = Color.FromArgb(255, 255, 255); // key itself
        private static readonly Color ColKeyOutline1 = Color.FromArgb(220, 220, 255); // ±1 (dominant/subdominant)
        private static readonly Color ColKeyOutline2 = Color.FromArgb(145, 145, 185); // ±2
        // External key name label
        private static readonly Color ColKeyNameLabel = Color.FromArgb(255, 210, 60); // golden yellow
        // Chord degree label inside wedge corner
        private static readonly Color ColDegreeLabel = Color.FromArgb(80, 120, 180); // cyan-mint: visible on both amber and violet

        // ── Init ──────────────────────────────────────────────────────────────
        private void InitCircleOfFifths()
        {
            cbShowCircle.CheckedChanged += (s, e) =>
            {
                pic.Invalidate();
                SaveSettings();
            };
        }

        // ── Called from SetChordLabelUi ───────────────────────────────────────
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
            int rootIdx = cmbScaleRoot.SelectedIndex;
            int modeIdx = cmbScaleMode.SelectedIndex;
            _circleKeyRoot = rootIdx > 0 ? RootPitchClass[rootIdx] : -1;
            _circleKeyIsMajor = !(modeIdx == 5 || modeIdx == 7 ||
                                   modeIdx == 10 || modeIdx == 11);
            if (cbShowCircle.Checked && !pic.IsDisposed)
                pic.Invalidate();
        }

        // ── Main draw ─────────────────────────────────────────────────────────
        internal void DrawCircleOfFifths(Graphics g, int w, int h)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Black);

            if (w < 80 || h < 80) return;

            float cx = w / 2f;
            float cy = h / 2f;
            float minDim = Math.Min(w, h);

            // Shrunk to leave margin for external key label and outline glow.
            float rOuter = minDim * 0.370f;
            float rMid = minDim * 0.256f;
            float rInner = minDim * 0.150f;
            float rHole = minDim * 0.075f;
            // Radius where the key name sits outside the outer ring.
            float rKeyLabel = rOuter + minDim * 0.075f;

            bool useFlats = cbFlats?.Checked ?? false;
            var majLabels = useFlats ? CofMajorLabelFlat : CofMajorLabelSharp;
            var minLabels = useFlats ? CofMinorLabelFlat : CofMinorLabelSharp;

            int chordPc = _circleChordRoot;
            bool chordMaj = _circleChordIsMajor;
            int keyPc = _circleKeyRoot;
            bool keyMaj = _circleKeyIsMajor;

            int keyCofIdx = FindCofIdx(keyPc, keyMaj);

            // ── Diatonic bracket ──────────────────────────────────────────────
            if (keyCofIdx >= 0)
                DrawDiatonicBracket(g, cx, cy, rOuter, rInner, keyCofIdx);

            // ── Wedges ────────────────────────────────────────────────────────
            const float sweep = 360f / 12f;
            const float origin = -90f - sweep / 2f;

            using var gridPen = new Pen(ColCircleGrid, 0.8f);

            for (int i = 0; i < 12; i++)
            {
                float sa = origin + i * sweep;
                int cofDist = keyCofIdx >= 0 ? CofDistance(i, keyCofIdx) : int.MaxValue;

                // — Major outer wedge —
                bool majChordHit = chordMaj && chordPc == CofMajorPc[i];

                FillCofWedge(g, cx, cy, rMid, rOuter, sa, sweep,
                    majChordHit ? ColCircleChordMaj : ColCircleWedgeMaj);
                DrawCofWedgeBorder(g, cx, cy, rMid, rOuter, sa, sweep, gridPen);

                // Tiered outline on the major ring (primary when key is major, secondary when minor).
                if (cofDist <= 2)
                {
                    int majTier = keyMaj ? cofDist : cofDist + 1;
                    if (majTier <= 2)
                        DrawCofWedgeBorder(g, cx, cy, rMid, rOuter, sa, sweep,
                            KeyOutlinePen(majTier));
                }

                // — Minor inner wedge —
                bool minChordHit = !chordMaj && chordPc == CofMinorPc[i];

                FillCofWedge(g, cx, cy, rInner, rMid, sa, sweep,
                    minChordHit ? ColCircleChordMin : ColCircleWedgeMin);
                DrawCofWedgeBorder(g, cx, cy, rInner, rMid, sa, sweep, gridPen);

                // Tiered outline on the minor ring (primary when key is minor, secondary when major).
                if (cofDist <= 2)
                {
                    int minTier = !keyMaj ? cofDist : cofDist + 1;
                    if (minTier <= 2)
                        DrawCofWedgeBorder(g, cx, cy, rInner, rMid, sa, sweep,
                            KeyOutlinePen(minTier));
                }

                // — Labels —
                float midRad = (sa + sweep / 2f) * (float)(Math.PI / 180.0);
                bool majKeyHit = keyMaj && keyPc == CofMajorPc[i];
                bool minKeyHit = !keyMaj && keyPc == CofMinorPc[i];
                bool majActive = majChordHit || majKeyHit;
                bool minActive = minChordHit || minKeyHit;

                // Major chord label — ~1.75x bigger than before
                DrawCofLabel(g, majLabels[i],
                    cx + (rMid + rOuter) / 2f * (float)Math.Cos(midRad),
                    cy + (rMid + rOuter) / 2f * (float)Math.Sin(midRad),
                    majActive ? Color.White : ColCircleLabelMaj,
                    majActive ? 26f : 22f, majActive);

                // Minor chord label — ~1.75x bigger than before
                DrawCofLabel(g, minLabels[i],
                    cx + (rInner + rMid) / 2f * (float)Math.Cos(midRad),
                    cy + (rInner + rMid) / 2f * (float)Math.Sin(midRad),
                    minActive ? Color.White : ColCircleLabelMin,
                    minActive ? 19f : 16f, minActive);

                // Degree numerals in the corner of each outlined wedge.
                if (keyCofIdx >= 0 && cofDist <= 3)
                {
                    // Signed offset in CoF steps (+CW = sharper, -CCW = flatter).
                    int signedOffset = i - keyCofIdx;
                    if (signedOffset > 6) signedOffset -= 12;
                    if (signedOffset < -5) signedOffset += 12;

                    // Corner position: near the CW edge and inner arc of each ring.
                    float cornerAngle = sa + sweep * 0.80f;
                    float cornerRad = cornerAngle * (float)(Math.PI / 180.0);

                    // Major ring degree — only for diatonic major/dominant chords.
                    string majDegree = keyMaj
                        ? MajKeyMajRingDegree(signedOffset)
                        : MinKeyMajRingDegree(signedOffset);

                    // Minor ring degree — only for diatonic minor chords.
                    string minDegree = keyMaj
                        ? MajKeyMinRingDegree(signedOffset)
                        : MinKeyMinRingDegree(signedOffset);

                    if (majDegree != null)
                    {
                        float rCornerMaj = rMid + (rOuter - rMid) * 0.22f;
                        DrawCofLabel(g, majDegree,
                            cx + rCornerMaj * (float)Math.Cos(cornerRad),
                            cy + rCornerMaj * (float)Math.Sin(cornerRad),
                            ColDegreeLabel, 10f, bold: true);
                    }

                    if (minDegree != null)
                    {
                        float rCornerMin = rInner + (rMid - rInner) * 0.22f;
                        DrawCofLabel(g, minDegree,
                            cx + rCornerMin * (float)Math.Cos(cornerRad),
                            cy + rCornerMin * (float)Math.Sin(cornerRad),
                            ColDegreeLabel, 10f, bold: true);
                    }
                }
            }

            // ── Key name outside the ring at the key wedge's angle ────────────
            if (keyCofIdx >= 0)
            {
                float keyMidRad = (origin + keyCofIdx * sweep + sweep / 2f)
                                  * (float)(Math.PI / 180.0);
                DrawCofLabel(g, BuildKeyName(useFlats, keyMaj),
                    cx + rKeyLabel * (float)Math.Cos(keyMidRad),
                    cy + rKeyLabel * (float)Math.Sin(keyMidRad),
                    ColKeyNameLabel, 15f, bold: true);
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
            DrawCofLabel(g, centreText, cx, cy, centreCol, 13f, bold: true);
        }

        // ── CoF distance: shortest path around the 12-wedge circle (0–6) ─────
        private static int CofDistance(int idxA, int idxB)
        {
            int d = Math.Abs(idxA - idxB);
            return Math.Min(d, 12 - d);
        }

        // ── Pen for each outline tier ─────────────────────────────────────────
        private static Pen KeyOutlinePen(int dist) => dist switch
        {
            0 => new Pen(ColKeyOutline0, 3.5f),
            1 => new Pen(ColKeyOutline1, 2.4f),
            _ => new Pen(ColKeyOutline2, 1.5f),
        };

        // ── Key name string ───────────────────────────────────────────────────
        private string BuildKeyName(bool useFlats, bool keyMaj)
        {
            int rootIdx = cmbScaleRoot.SelectedIndex;
            if (rootIdx <= 0) return "";

            string root = cmbScaleRoot.Items[rootIdx]?.ToString() ?? "";
            // Items like "C#/D♭" — pick the appropriate side.
            if (root.Contains('/'))
            {
                var parts = root.Split('/');
                root = useFlats ? parts[1] : parts[0];
            }

            int modeIdx = cmbScaleMode.SelectedIndex;
            string suffix = modeIdx switch
            {
                0 => "",
                5 => "m",
                7 => "m♮",
                10 => "m pent",
                11 => "blues",
                _ => ""
            };
            return root + suffix;
        }

        // ── Diatonic degree Roman numerals ────────────────────────────────────
        // signedOffset = CoF steps from key wedge (+ = CW = sharper, - = CCW = flatter).
        //
        // For a MAJOR key:
        //   CoF:  -1    0    +1    +2    +3    -2    -3
        //   Maj:  IV    I     V    —     —     —     —
        //   Min:  ii    vi    iii   —     vii°  —     —
        //
        // For a MINOR (natural) key:
        //   CoF:  -1    0    +1    +2    +3    -2    -3
        //   Min:  iv    i     v    ii°   —     —     —
        //   Maj:  VI    III   VII   —     —     —     —
        //   (III/VI/VII are the borrowed/relative major chords)

        // Major key — major ring (I, IV, V only — all other major chords non-diatonic)
        private static string MajKeyMajRingDegree(int offset) => offset switch
        {
            0 => "I",
            -1 => "IV",
            +1 => "V",
            _ => null
        };

        // Major key — minor ring (ii, iii, vi, vii°)
        private static string MajKeyMinRingDegree(int offset) => offset switch
        {
            0 => "vi",
            -1 => "ii",
            +1 => "iii",
            +2 => "vii°",
            _ => null
        };

        // Minor key — minor ring (i, iv, v)
        private static string MinKeyMinRingDegree(int offset) => offset switch
        {
            0 => "i",
            -1 => "iv",
            +1 => "v",
            _ => null
        };

        // Minor key — major ring (III, VI, VII — the relative/borrowed majors)
        private static string MinKeyMajRingDegree(int offset) => offset switch
        {
            0 => "III",
            -1 => "VI",
            +1 => "VII",
            _ => null
        };

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
