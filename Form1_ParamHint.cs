//Copyright (c) 2026, Erik Martin
// Replaces the standard ToolTip with a custom square popup that shows:
//   • A description of the parameter
//   • A recommended LOW value + its use case
//   • A recommended HIGH value + its use case
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace Spectrum
{
    // ── Data container ───────────────────────────────────────────────────────
    internal sealed class ParamHintInfo
    {
        public string Description { get; }
        public string LowValue { get; }
        public string LowUse { get; }
        public string HighValue { get; }
        public string HighUse { get; }

        public ParamHintInfo(string description,
                             string lowValue, string lowUse,
                             string highValue, string highUse)
        {
            Description = description;
            LowValue = lowValue;
            LowUse = lowUse;
            HighValue = highValue;
            HighUse = highUse;
        }
    }

    // ── Custom popup window ──────────────────────────────────────────────────
    internal sealed class ParamHintPopup : Form
    {
        // ── Visual constants ─────────────────────────────────────────────────
        private const int PAD = 14;
        private const int WIDTH = 310;
        private const int CORNER = 6;
        private const float DESC_SIZE = 9.5f;
        private const float LABEL_SIZE = 7.5f;
        private const float VALUE_SIZE = 9.0f;

        private static readonly Color BG = Color.FromArgb(24, 24, 32);
        private static readonly Color BORDER = Color.FromArgb(80, 80, 110);
        private static readonly Color DESC_COL = Color.FromArgb(210, 210, 220);
        private static readonly Color DIVIDER = Color.FromArgb(55, 55, 70);
        private static readonly Color LOW_LABEL = Color.FromArgb(100, 200, 130);
        private static readonly Color HIGH_LABEL = Color.FromArgb(200, 130, 100);
        private static readonly Color VALUE_COL = Color.FromArgb(255, 230, 140);
        private static readonly Color USE_COL = Color.FromArgb(170, 170, 185);

        private static readonly Font FontDesc = new Font("Segoe UI", DESC_SIZE, FontStyle.Regular);
        private static readonly Font FontSectionLbl = new Font("Segoe UI", LABEL_SIZE, FontStyle.Bold);
        private static readonly Font FontValue = new Font("Segoe UI", VALUE_SIZE, FontStyle.Bold);
        private static readonly Font FontUse = new Font("Segoe UI", LABEL_SIZE, FontStyle.Regular);

        private ParamHintInfo _info;

        public ParamHintPopup()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = BG;
            DoubleBuffered = true;
            // Allow the OS to click through to the underlying control.
            SetStyle(ControlStyles.Selectable, false);
        }

        public void Show(Control owner, ParamHintInfo info)
        {
            _info = info;

            // Measure the required height.
            int h = MeasureHeight();
            Size = new Size(WIDTH, h);

            // Position below + right of the cursor; keep on screen.
            Point cursor = Cursor.Position;
            Rectangle screen = Screen.FromPoint(cursor).WorkingArea;
            int x = Math.Min(cursor.X + 12, screen.Right - WIDTH);
            int y = Math.Min(cursor.Y + 20, screen.Bottom - h);
            Location = new Point(x, y);

            // Don't steal focus.
            Visible = true;
        }

        // Override ShowWithoutActivation so we never steal keyboard focus.
        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                return cp;
            }
        }

        private int MeasureHeight()
        {
            using var g = Graphics.FromHwnd(IntPtr.Zero);
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            int innerW = WIDTH - PAD * 2;
            float y = PAD;

            // Description
            y += MeasureWrapped(g, _info.Description, FontDesc, innerW).Height + 10;

            // Divider
            y += 1 + 8;

            bool hasLow = !string.IsNullOrEmpty(_info.LowValue) || !string.IsNullOrEmpty(_info.LowUse);
            bool hasHigh = !string.IsNullOrEmpty(_info.HighValue) || !string.IsNullOrEmpty(_info.HighUse);

            // LOW block (only if present)
            if (hasLow)
            {
                y += MeasureWrapped(g, _info.LowUse ?? "", FontUse, innerW - 70).Height
                   + Math.Max(FontSectionLbl.Height, FontValue.Height) + 6;
            }

            // Gap between LOW and HIGH (only if both present)
            if (hasLow && hasHigh)
            {
                y += 6;
            }

            // HIGH block (only if present)
            if (hasHigh)
            {
                y += MeasureWrapped(g, _info.HighUse ?? "", FontUse, innerW - 70).Height
                   + Math.Max(FontSectionLbl.Height, FontValue.Height) + 6;
            }

            y += PAD;
            return (int)Math.Ceiling(y);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);

            // Background + border
            using var bgBrush = new SolidBrush(BG);
            using var path = RoundedRect(rect, CORNER);
            g.FillPath(bgBrush, path);
            using var pen = new Pen(BORDER);
            g.DrawPath(pen, path);

            int innerW = WIDTH - PAD * 2;
            float y = PAD;

            // ── Description ─────────────────────────────────────────────────
            var descSize = DrawWrapped(g, _info.Description, FontDesc, DESC_COL,
                                       new PointF(PAD, y), innerW);
            y += descSize.Height + 10;

            // ── Divider ──────────────────────────────────────────────────────
            g.DrawLine(new Pen(DIVIDER), PAD, y, WIDTH - PAD, y);
            y += 1 + 8;

            bool hasLow = !string.IsNullOrEmpty(_info.LowValue) || !string.IsNullOrEmpty(_info.LowUse);
            bool hasHigh = !string.IsNullOrEmpty(_info.HighValue) || !string.IsNullOrEmpty(_info.HighUse);

            // ── LOW block ────────────────────────────────────────────────────
            if (hasLow)
            {
                y = DrawValueBlock(g, "LOW", _info.LowValue ?? "", _info.LowUse ?? "",
                                   LOW_LABEL, y, innerW);
            }

            // ── Gap between LOW and HIGH ─────────────────────────────────────
            if (hasLow && hasHigh)
            {
                y += 6;
            }

            // ── HIGH block ───────────────────────────────────────────────────
            if (hasHigh)
            {
                DrawValueBlock(g, "HIGH", _info.HighValue ?? "", _info.HighUse ?? "",
                               HIGH_LABEL, y, innerW);
            }
        }

        // Draws one LOW / HIGH block; returns the new Y after drawing.
        private float DrawValueBlock(Graphics g,
                                     string sectionLabel,
                                     string value,
                                     string use,
                                     Color labelColor,
                                     float y,
                                     int innerW)
        {
            // "LOW" / "HIGH" label on the left
            using var lblBrush = new SolidBrush(labelColor);
            g.DrawString(sectionLabel, FontSectionLbl, lblBrush, PAD, y);

            // Value to the right of the label
            float valueX = PAD + 44;
            using var valBrush = new SolidBrush(VALUE_COL);
            g.DrawString(value, FontValue, valBrush, valueX, y - 1);

            float rowH = Math.Max(FontSectionLbl.GetHeight(g), FontValue.GetHeight(g));
            y += rowH + 3;

            // Use-case text, indented
            using var useBrush = new SolidBrush(USE_COL);
            var useSize = DrawWrapped(g, use, FontUse, USE_COL,
                                      new PointF(PAD + 4, y), innerW - 4);
            y += useSize.Height;

            return y;
        }

        // Measures wrapped text height.
        private static SizeF MeasureWrapped(Graphics g, string text, Font font, int maxW)
        {
            return g.MeasureString(text, font, maxW, StringFormat.GenericDefault);
        }

        // Draws wrapped text and returns its measured size.
        private static SizeF DrawWrapped(Graphics g, string text, Font font,
                                         Color color, PointF origin, int maxW)
        {
            var size = g.MeasureString(text, font, maxW, StringFormat.GenericDefault);
            using var brush = new SolidBrush(color);
            var layoutRect = new RectangleF(origin, new SizeF(maxW, size.Height + 4));
            g.DrawString(text, font, brush, layoutRect);
            return size;
        }

        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }
    }

    // ── Static registry — call ParamHint.Register() from the designer ────────
    internal static class ParamHint
    {
        private static ParamHintPopup _popup;
        private static readonly Dictionary<Control, ParamHintInfo> _map = new();
        private static System.Windows.Forms.Timer _showTimer;
        private static Control _pending;
        private static Control _active;
        private const int DELAY_MS = 450;

        // Call once from Form1 constructor (or InitializeComponent area) to wire up.
        public static void Install(Form owner)
        {
            _popup = new ParamHintPopup();
            _showTimer = new System.Windows.Forms.Timer { Interval = DELAY_MS };
            _showTimer.Tick += (s, e) =>
            {
                _showTimer.Stop();
                if (_pending != null && _map.TryGetValue(_pending, out var info))
                {
                    _active = _pending;
                    _popup.Show(_active, info);
                }
            };
            owner.FormClosed += (s, e) =>
            {
                _popup?.Close();
                _popup?.Dispose();
                _showTimer?.Dispose();
            };
        }

        public static void Register(Control control, ParamHintInfo info)
        {
            _map[control] = info;
            control.MouseEnter += OnMouseEnter;
            control.MouseLeave += OnMouseLeave;
        }

        private static void OnMouseEnter(object sender, EventArgs e)
        {
            _pending = sender as Control;
            _showTimer.Stop();
            _showTimer.Start();
        }

        private static void OnMouseLeave(object sender, EventArgs e)
        {
            _showTimer.Stop();
            _pending = null;
            if (_active == sender as Control)
            {
                _active = null;
                _popup.Visible = false;
            }
        }
    }
}
