using System.Windows.Forms;

namespace Spectrum
{
    /// <summary>
    /// Lightweight double-buffered control that replaces PictureBox for the
    /// spectrogram display.  Key differences from PictureBox:
    ///   • OptimizedDoubleBuffer + AllPaintingInWmPaint eliminates flicker
    ///   • Opaque flag suppresses WM_ERASEBKGND (we paint every pixel)
    ///   • No internal Image property overhead or ISupportInitialize
    /// Wire your paint handler to the PaintSpectrogram event.
    /// </summary>
    public class SpectrogramPanel : Control
    {
        public SpectrogramPanel()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer | 
                ControlStyles.Opaque,
                
                true);
            UpdateStyles();
        }

        public event PaintEventHandler PaintSpectrogram;

        protected override void OnPaint(PaintEventArgs e)
        {
            PaintSpectrogram?.Invoke(this, e);
        }

        // Suppress background erase — we paint every pixel in OnPaint.
        protected override void OnPaintBackground(PaintEventArgs e) { }
    }
}
