//Copyright (c) 2026, Erik Martin
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Button;


namespace Spectrum
{
    public partial class Form1 : Form
    {
        // ── Tuning ──────────────────────────────────────────────────────────
        private double tuning; // default: AppSettings.Tuning = 440.0

        // ── FFT / STFT ───────────────────────────────────────────────────────
        // High-freq FFT — smaller window, better time resolution for high frequencies.
        // This is the primary FFT used for peak extraction and ridge tracking.
        private int FFT_SIZE; // default: AppSettings.FftSize = 4096
        // Low-freq FFT — larger window, better frequency resolution for low frequencies.
        // Only active when _lowFftEnabled is true.
        private int FFT_SIZE_L; // default: AppSettings.FftSizeLow = 8192
        // Ring buffer must hold enough samples for the larger of the two FFT windows.
        private int RingSize => _lowFftEnabled ? Math.Max(FFT_SIZE, FFT_SIZE_L) : FFT_SIZE;

        // When true, the low-freq FFT is computed and blended in below the crossover.
        private bool _lowFftEnabled; // default: AppSettings.LowFftEnabled = true
        // Crossover note (e.g. "G3") — the centre of the blend region.
        private string LOW_FFT_CROSSOVER_NOTE; // default: AppSettings.LowFftCrossoverNote = "C3"
        // Cached Hz value for the crossover centre.
        private double LOW_FFT_CROSSOVER_HZ; // default: C3 ≈ 130.81 Hz (set via TryParseNoteToMidi in LoadSettings)
        // Width of the blend region in semitones (total span; blend = 0 at centre-half, 1 at centre+half).
        private double LOW_FFT_CROSSOVER_SEMITONES; // default: AppSettings.LowFftCrossoverSemitones = 6.0
        private int HOP_SIZE;        // hop in samples; auto-set from TARGET_FPS when > 0; default: AppSettings.OverlapFactor = 240
        private double TARGET_FPS;   // target frames/sec; 0 = use HOP_SIZE directly; default: AppSettings.TargetFps = 120.0
        private double MAX_COL_SHIFT; // time-reassignment clamp in samples (clamp before /hopSize); default: AppSettings.MaxColShift = 128.0
        private int MAX_QUEUED_FRAMES; // default: AppSettings.MaxQueuedFrames = 20
        private double HARMONIC_SUPPRESSION; // default: AppSettings.HarmonicSuppression = 0.0
        const int TunerAvgFrames = 40;
        private int PEAK_MODE; // default: AppSettings.PeakMode = 0

        // Accumulator — one slot per tracked ridge, keyed by ridge Id.
        private struct RidgeAccum
        {
            public int Id;
            public double LogFreq;
            public double LogVel;
            public double TimeShiftCols;
            public double Intensity;
            public int Miss;
            public double WeightSum;
            public double DrawWidth;
        }

        private RidgeAccum[] _ridgeAccum = new RidgeAccum[MaxActiveRidges];
        private int _ridgeAccumCount = 0;

        // ── [OPT-6] Ridge ID → accumulator index dictionary for O(1) lookup ──
        private readonly Dictionary<int, int> _ridgeAccumIndex = new(MaxActiveRidges);

        // ── [OPT-7] Ridge ID → live ridge dictionary for O(1) DrawRidgesColumn lookup ──
        private readonly Dictionary<int, Ridge> _ridgeById = new(MaxActiveRidges);

        // ── Peak / Ridge ─────────────────────────────────────────────────────
        private int MAX_PEAKS_PER_FRAME; // default: AppSettings.MaxPeaks = 25
        private double PEAK_MIN_REL; // default: AppSettings.PeakMinRel = 0.03
        private double PEAK_MIN_SPACING_CENTS; // default: AppSettings.PeakMinSpacingCents = 0.0

        // Ridge match parameters
        private double RIDGE_MATCH_LOGHZ; // default: AppSettings.RidgeMatchLogHz = 0.022
        private double RIDGE_MATCH_LOGHZ_PRED_BOOST; // default: AppSettings.RidgeMatchLogHzPredBoost = 1.75
        private double RIDGE_MAX_CENTS_JUMP; // default: AppSettings.RidgeMaxCentsJump = 250.0
        private int RIDGE_MISS_MAX; // default: AppSettings.RidgeMissMax = 10
        private double RIDGE_FREQ_EMA; // default: AppSettings.RidgeFreqEma = 0.7
        private double RIDGE_VEL_EMA; // default: AppSettings.RidgeVelEma = 0.7
        private double RIDGE_INTENSITY_EMA; // default: AppSettings.RidgeIntensityEma = 0.1
        private double PEAK_GAMMA; // default: AppSettings.PeakGamma = 0.5
        private double RIDGE_MERGE_CENTS; // default: AppSettings.RidgeMergeCents = 20.0
        private double RIDGE_MERGE_BRIGHTNESS_BOOST; // default: AppSettings.RidgeMergeBrightnessBoost = 2.0
        private double RIDGE_MERGE_WIDTH_ADD; // default: AppSettings.RidgeMergeWidthAdd = 0.1
        private double RIDGE_MERGE_WIDTH_DECAY; // per-frame decay multiplier for merge width bonus; default: AppSettings.RidgeMergeWidthDecay = 0.95
        private const double TunerMinHz = 70.0;
        private const double TunerSwitchConfirmCents = 18.0;
        private const int TunerSwitchConfirmFrames = 4;
        private const int TunerSignalHoldFrames = 6;
        private const double TunerCandidateProximityCents = 35.0;
        private const int TunerHarmonicSupportMax = 6;
        private const int TunerFundamentalHypothesisMax = 5;
        private const double TunerHarmonicMatchCents = 28.0;
        private const double TunerFundamentalRelativeFloor = 0.10;
        private const double TunerFundamentalKeepScoreRatio = 0.72;

        // ── Harmonic family collapse ─────────────────────────────────────────
        private double HARMONIC_FAMILY_CENTS_TOL; // default: AppSettings.HarmonicFamilyCentsTol = 45.0
        private double HARMONIC_FAMILY_MAX_RATIO; // default: AppSettings.HarmonicFamilyMaxRatio = 12.0

        // ── Chord detection ──────────────────────────────────────────────────
        private int CHORD_AVG_FRAMES; // default: AppSettings.ChordAvgFrames = 12
        private double CHORD_OUT_PENALTY; // default: AppSettings.ChordOutPenalty = 0.35
        private int CHORD_RIDGES; // default: AppSettings.ChordRidges = 15

        private readonly object chordLock = new();
        private readonly Queue<double[]> chromaQueue = new();
        private readonly double[] chromaSum = new double[12];

        // [OPT-8] Reuse chroma arrays from a pool instead of allocating new double[12] each frame.
        private readonly Stack<double[]> _chromaPool = new(64);

        private volatile string lastDetectedChordText = "—";
        private volatile string lastDetectedCanonicalText = "";
        private volatile string lastDetectedNotesText = "";
        private volatile string lastDetectedHarmonicsText = "";

        private const double TunerJumpResetCents = 50.0;
        private long _tunerFreqHzBits = 0;
        private double _tunerLogFreqEma = 0.0;
        private double _tunerSignalStrength = 0.0; // smoothed ridge intensity for signal meter
        private double _tunerPendingLogFreq = 0.0;
        private int _tunerPendingFrames = 0;
        private int _tunerSignalHold = 0;

        // ── Pause / Scrub ────────────────────────────────────────────────────
        private volatile bool scrollPaused = false;
        private int lastMouseX = -1;

        // ── Filter / Display range ───────────────────────────────────────────
        private string HighPassNote; // default: AppSettings.HighPassNote = "A1"
        private string LowPassNote;  // default: AppSettings.LowPassNote = "C8"
        private double HighPass;     // set via TryParseNoteToMidi in LoadSettings
        private double LowPass;
        private string[] chordCols;
        private string[] detectedNotesCols;
        private string[] canonicalCols;
        private string[] harmonicsCols;
        // Per-column history for tuner freeze-scrub.
        // tunerFreqCols stores Hz (0 = no signal), tunerIntensityCols stores raw frameMaxVisible.
        private double[] tunerFreqCols;
        private double[] tunerIntensityCols;

        // Latest raw (pre-normalization) frame peak intensity — written by audio thread, read by UI.
        private long _rawFrameIntensityBits = 0;

        // ── Audio ────────────────────────────────────────────────────────────
        private WasapiCapture capture;
        private WaveFormat captureFormat;
        private float[] ring;
        private int ringWritePos, ringFilled, samplesSinceLastFrame, hopSize;
        private readonly object sampleLock = new();

        // ── Worker ───────────────────────────────────────────────────────────
        private BlockingCollection<float[]> frameQueue;
        private Thread worker;
        private volatile bool workerRunning;

        // ── Windows (reassignment) ───────────────────────────────────────────
        // High-freq FFT windows (FFT_SIZE).
        private double[] hann, hannD, hannT;
        // Low-freq FFT windows (FFT_SIZE_L) — only allocated when _lowFftEnabled.
        private double[] hannL, hannDL, hannTL;

        private Bitmap spectrogramBitmap;
        // Circular-buffer write pointer: the column we write the CURRENT frame into.
        // Advances by +1 each frame (mod bitmap width). Pic_Paint draws the two halves
        // in the correct order so the display still scrolls left visually.
        private int _bitmapWriteCol = 0;
        private readonly object bitmapLock = new();

        // ── Flat notation ─────────────────────────────────────────────────────
        private bool _useFlats; // default: AppSettings.UseFlats = false

        // ── Scale ────────────────────────────────────────────────────────────
        private float displayFmin = 20f, displayFmax = 20000f;
        private double _smoothedMaxIntensity = 1e-12;
        // Symmetric EMA alpha for level normalization smoothing.
        // 0 = disabled (re-normalize every frame to current peak, classic behavior).
        // >0 = EMA alpha applied symmetrically on both attack and decay.
        private double LEVEL_SMOOTH_EMA; // default: AppSettings.LevelSmoothEma = 0.0

        // ── Volume lock (manual sensitivity) ─────────────────────────────────
        // Written on the UI thread, read on the audio thread — must be volatile.
        private volatile bool _volumeLocked = false;
        private long _volumeLockedBits = 0;  // BitConverter reinterpret of double

        private double VolumeLockValue
        {
            get => BitConverter.Int64BitsToDouble(Interlocked.Read(ref _volumeLockedBits));
            set => Interlocked.Exchange(ref _volumeLockedBits, BitConverter.DoubleToInt64Bits(value));
        }

        private double TunerFreqHz
        {
            get => BitConverter.Int64BitsToDouble(Interlocked.Read(ref _tunerFreqHzBits));
            set => Interlocked.Exchange(ref _tunerFreqHzBits, BitConverter.DoubleToInt64Bits(value));
        }

        private double RawFrameIntensity
        {
            get => BitConverter.Int64BitsToDouble(Interlocked.Read(ref _rawFrameIntensityBits));
            set => Interlocked.Exchange(ref _rawFrameIntensityBits, BitConverter.DoubleToInt64Bits(value));
        }

        private static void RethrowUnexpectedException(Exception ex)
        {
            Debug.WriteLine(ex);
            ExceptionDispatchInfo.Capture(ex).Throw();
        }

        // ── Named constants ──────────────────────────────────────────────────
        private const int MaxActiveRidges = 160;

        // [OPT-1] Pre-computed pow table for RidgeMissFadePow^miss
        private double[] _missFadeTable = Array.Empty<double>();
        private double RidgeMissFadePow; // default: AppSettings.MissFadePow = 0.8
        private double RidgeAgeDecay;    // default: AppSettings.AgeDecay = 0.05
        private double MinDrawAlpha;     // default: AppSettings.MinDrawAlpha = 0.2

        private void RebuildMissFadeTable()
        {
            int maxMiss = RIDGE_MISS_MAX + 2;
            if (_missFadeTable.Length != maxMiss)
                _missFadeTable = new double[maxMiss];
            double v = 1.0;
            for (int i = 0; i < maxMiss; i++) { _missFadeTable[i] = v; v *= RidgeMissFadePow; }
        }

        // ────────────────────────────────────────────────────────────────────
        //  Ridge pool
        // ────────────────────────────────────────────────────────────────────
        private sealed class Ridge
        {
            public int Id;
            public double LogFreq, LogVel, Intensity, TimeShiftCols;
            public int Miss, Age;
            public double LastYf, LastXf;
            public bool HasLastPos;
            public double ParabolaWidthBins;
            public double DrawWidth;
            public double MergeWidthBonus;  // persistent extra width from merges; decays each frame

            public const int IntensityHistoryLen = 32;
            public readonly float[] IntensityHistory = new float[IntensityHistoryLen];
            public int HistoryHead;

            public void Reset(int id, double logFreq, double timeShiftCols, double intensity)
            {
                Id = id; LogFreq = logFreq; LogVel = 0.0;
                Intensity = intensity; TimeShiftCols = timeShiftCols;
                Miss = 0; Age = 0; HasLastPos = false;
                LastYf = LastXf = 0.0;
                ParabolaWidthBins = 0.0;
                DrawWidth = 0.0;
                MergeWidthBonus = 0.0;
                Array.Clear(IntensityHistory, 0, IntensityHistoryLen);
                HistoryHead = 0;
            }

            public void RecordHistory()
            {
                IntensityHistory[HistoryHead] = (float)Intensity;
                HistoryHead = (HistoryHead + 1) % IntensityHistoryLen;
            }
        }

        private readonly Stack<Ridge> _ridgePool = new(MaxActiveRidges);

        private Ridge RentRidge(int id, double logFreq, double timeShiftCols, double intensity)
        {
            if (!_ridgePool.TryPop(out var r)) r = new Ridge();
            r.Reset(id, logFreq, timeShiftCols, intensity);
            return r;
        }

        private void ReturnRidge(Ridge r) => _ridgePool.Push(r);

        // ────────────────────────────────────────────────────────────────────
        //  Peak as readonly struct
        // ────────────────────────────────────────────────────────────────────
        private readonly struct Peak
        {
            public readonly double FreqHz, LogFreq, Intensity, TimeShiftCols;
            public readonly double WidthBins;
            public Peak(double freqHz, double logFreq, double intensity, double timeShiftCols,
                        double widthBins = 0.0)
            {
                FreqHz = freqHz; LogFreq = logFreq;
                Intensity = intensity; TimeShiftCols = timeShiftCols;
                WidthBins = widthBins;
            }
        }

        private sealed class PeakDescendingIntensity : IComparer<Peak>
        {
            public static readonly PeakDescendingIntensity Instance = new();
            private PeakDescendingIntensity() { }
            public int Compare(Peak a, Peak b) => b.Intensity.CompareTo(a.Intensity);
        }

        private readonly object ridgeLock = new();
        private readonly List<Ridge> ridges = new();
        private int nextRidgeId = 1;

        // ────────────────────────────────────────────────────────────────────
        //  Pre-allocated RenderFrame buffers
        // ────────────────────────────────────────────────────────────────────
        private Complex[] _X, _Xd, _Xt;
        private double[] _mag, _fReassign, _tShiftCols, _fSm, _tSm;

        // ── Packed FFT buffer for the 2-FFT optimization ─────────────────────
        // _Z holds packed Z[n] = hannD[n]*s + i*hannT[n]*s before FFT,
        // then Z[k] = FFT(Xd)[k] packed with FFT(Xt)[k] after FFT.
        // Xd and Xt are unpacked from it into _Xd and _Xt after the transform.
        private Complex[] _Z;
        // ── Low-freq FFT buffers (FFT_SIZE_L) — only used when _lowFftEnabled ──
        private Complex[] _XL, _XdL, _XtL, _ZL;
        private double[] _magL, _fReassignL, _tShiftColsL;

        private Peak[] _peaksScratch = new Peak[2048];
        private int _peaksScratchCount;
        private Peak[] _thinnedScratch;

        // ────────────────────────────────────────────────────────────────────
        //  Pre-allocated UpdateRidges working buffers
        // ────────────────────────────────────────────────────────────────────
        private List<int> _candidateIdx = new(MaxActiveRidges);
        private int[] _sortedIdx = new int[MaxActiveRidges];
        private bool[] _matched = new bool[MaxActiveRidges];
        private List<Ridge> _newRidges = new(32);

        // [OPT-2] Static ridge frequency comparers — allocated once, never again.
        private RidgeLogFreqComparer _ridgeLogFreqComparer;

        private sealed class RidgeLogFreqComparer : IComparer<int>
        {
            public List<Ridge> Ridges;
            public int Compare(int a, int b) => Ridges[a].LogFreq.CompareTo(Ridges[b].LogFreq);
        }

        private RidgeIntensityComparer _ridgeIntensityComparer;

        private sealed class RidgeIntensityComparer : IComparer<Ridge>
        {
            public static readonly RidgeIntensityComparer Instance = new();
            private RidgeIntensityComparer() { }
            public int Compare(Ridge a, Ridge b) => b.Intensity.CompareTo(a.Intensity);
        }

        private RidgeLogFreqRidgeComparer _ridgeLogFreqRidgeComparer;

        private sealed class RidgeLogFreqRidgeComparer : IComparer<Ridge>
        {
            public static readonly RidgeLogFreqRidgeComparer Instance = new();
            private RidgeLogFreqRidgeComparer() { }
            public int Compare(Ridge a, Ridge b) => a.LogFreq.CompareTo(b.LogFreq);
        }

        // ────────────────────────────────────────────────────────────────────
        //  Pre-allocated UpdateChordLabelFromRidges tone buffer
        // ────────────────────────────────────────────────────────────────────
        private (double Freq, double W)[] _tones = new (double, double)[MaxActiveRidges];
        private int _tonesCount;

        // ────────────────────────────────────────────────────────────────────
        //  Pre-allocated DetectChord buffers
        // ────────────────────────────────────────────────────────────────────
        private readonly double[] _chromaNorm = new double[12];

        // ── Chord qualities ──────────────────────────────────────────────────
        private static readonly (string Suffix, int[] Intervals, int ThirdOffset, bool HasThird, double ObscurityPenalty)[]
            ChordQualities =
            {
                // ── Triads ──────────────────────────────────────────────────
                ("maj",      new[]{0,4,7},          4,  true, 0.00),
                ("maj(no5)",      new[]{0,4},          4,  true, 0.08),
                ("m",        new[]{0,3,7},          3,  true, 0.00),
                ("m(no5)",        new[]{0,3},          3,  true, 0.08),
                ("dim",      new[]{0,3,6},          3,  true, 0.05),
                ("aug",      new[]{0,4,8},          4,  true, 0.10),
                ("sus2",     new[]{0,2,7},          2,  false, 0.05),
                ("sus4",     new[]{0,5,7},          5,  false, 0.05),
                ("",         new[]{0,7},            -1, false, 0.10),

                // ── Dominant / Dominant-extended ────────────────────────────
                ("7",        new[]{0,4,7,10},       4,  true, 0.00),
                ("7(no5)",        new[]{0,4,10},       4,  true, 0.08),
                ("9",        new[]{0,4,7,10,2},     4,  true, 0.08),
                ("9(no5)",        new[]{0,4,10,2},     4,  true, 0.15),
                ("11",       new[]{0,4,7,10,2,5},   4,  true, 0.20),
                ("13",       new[]{0,4,7,10,9},     4,  true, 0.15),
                ("7sus4",    new[]{0,5,7,10},       5,  false, 0.08),
                ("7sus2",    new[]{0,2,7,10},       2,  false, 0.12),

                // ── Major seventh family ─────────────────────────────────────
                ("maj7",     new[]{0,4,7,11},       4,  true, 0.00),
                ("maj7(no5)", new[]{0,4,11},       4,  true, 0.10),
                ("maj9",     new[]{0,4,7,11,2},     4,  true, 0.10),
                ("maj9(no5)",     new[]{0,4,11,2},     4,  true, 0.18),
                ("maj11",    new[]{0,4,7,11,2,5},   4,  true, 0.25),
                ("maj13",    new[]{0,4,7,11,9},     4,  true, 0.20),

                // ── Minor seventh family ─────────────────────────────────────
                ("m7",       new[]{0,3,7,10},       3,  true, 0.00),
                ("m7(no5)",       new[]{0,3,10},       3,  true, 0.10),
                ("m9",       new[]{0,3,7,10,2},     3,  true, 0.10),
                ("m9(no5)",       new[]{0,3,10,2},     3,  true, 0.18),
                ("m11",      new[]{0,3,7,10,2,5},   3,  true, 0.25),
                ("m(maj7)",  new[]{0,3,7,11},       3,  true, 0.20),
                ("m(maj9)",  new[]{0,3,7,11,2},     3,  true, 0.28),

                // ── Diminished / Half-diminished ─────────────────────────────
                ("dim7",     new[]{0,3,6,9},        3,  true, 0.08),
                ("m7b5",     new[]{0,3,6,10},       3,  true, 0.10),

                // ── Augmented seventh ────────────────────────────────────────
                ("aug7",     new[]{0,4,8,10},       4,  true, 0.25),
                ("augMaj7",  new[]{0,4,8,11},       4,  true, 0.30),

                // ── Sixth chords ─────────────────────────────────────────────
                
                ("6",     new[]{0,4,7,9},        4,  true, 0.05),
                ("6(no5)",     new[]{0,4,9},        4,  true, 0.12),
                ("m6",     new[]{0,3,7,9},        3,  true, 0.08),
                ("m6(no5)",     new[]{0,3,9},        3,  true, 0.15),
                ("6/9",      new[]{0,4,7,9,2},      4,  true, 0.15),

                // ── Add chords ───────────────────────────────────────────────
                ("majAdd9",     new[]{0,2,4,7},        4,  true, 0.08),
                ("mAdd9",    new[]{0,2,3,7},        3,  true, 0.10),
                ("add4",     new[]{0,4,5,7},        4,  true, 0.15)
            };

        // ────────────────────────────────────────────────────────────────────
        //  Pre-allocated CollapseHarmonicFamilies buffer
        // ────────────────────────────────────────────────────────────────────
        private double[] _suppressFactor = new double[MaxActiveRidges];

        // [OPT-3] Cache Math.Exp(ridge.LogFreq) for all ridges before the O(n²) loop.
        private double[] _ridgeFreqCache = new double[MaxActiveRidges];

        // ────────────────────────────────────────────────────────────────────
        //  Hoisted NoteName tables
        // ────────────────────────────────────────────────────────────────────
        private static readonly string[] NoteNamesSharp =
            { "C","C#","D","D#","E","F","F#","G","G#","A","A#","B" };
        private static readonly string[] NoteNamesFlat =
            { "C","D♭","D","E♭","E","F","G♭","G","A♭","A","B♭","B" };

        private string[] NoteNames => _useFlats ? NoteNamesFlat : NoteNamesSharp;

        // ────────────────────────────────────────────────────────────────────
        //  Cached GDI objects for DrawNoteGrid
        // ────────────────────────────────────────────────────────────────────
        private Pen _gridPen;
        private Font _gridFont;
        private SolidBrush _gridBrush;

        // ────────────────────────────────────────────────────────────────────
        //  Cached BeginInvoke delegate for chord label updates
        // ────────────────────────────────────────────────────────────────────
        private string _pendingChordText;
        private string _pendingCanonicalNotes;
        private string _pendingDetectedNotes;
        private string _pendingHarmonicsDisplay;
        private Action _chordLabelAction;

        // [OPT-9] Reusable StringBuilder for ChordNotesText
        private readonly StringBuilder _chordNotesSb = new(128);

        // [OPT-9] Reusable sorted list for ChordNotesText
        private readonly (int pc, double frac)[] _chordNotesSorted = new (int, double)[12];

        // [OPT-4] Pre-allocated DrawEntry array for DrawRidgesColumn
        private DrawEntry[] _drawList = new DrawEntry[MaxActiveRidges];

        // ────────────────────────────────────────────────────────────────────
        //  Note grid bitmap cache
        // ────────────────────────────────────────────────────────────────────
        private Bitmap _gridCache;
        private float _gridCacheFmin, _gridCacheFmax;
        private double _gridCacheTuning;
        private bool _gridCacheShowLines;
        private bool _gridCacheUseFlats;

        // ────────────────────────────────────────────────────────────────────
        //  Profiling / timing
        // ────────────────────────────────────────────────────────────────────
        private readonly System.Diagnostics.Stopwatch _profSw = new();
        private double _profWindowing, _profFft, _profReassign, _profSmooth;
        private double _profPeaks, _profRidges, _profHarmonic, _profChord;
        private double _profAccum, _profDraw, _profTotal;
        private double _profInterFrame;       // wall-clock gap between RenderFrame entries
        private long _profLastFrameTick;    // timestamp at end of previous RenderFrame
        private long _profQueueDepthSum;    // accumulated queue depth samples
        private int _profFrameCount;
        private const int ProfInterval = 60;
        private long _profLastPrintTick;
        // FPS tracking (worker thread writes, UI thread reads via BeginInvoke)
        private long _fpsWindowStart = 0;
        private int _fpsFrameCount = 0;

        // ── UI-thread throttle ────────────────────────────────────────────────
        // Analysis can run faster than the display refresh rate.  These gates
        // prevent flooding the WinForms message pump with BeginInvoke calls,
        // which would starve label/control updates and cause bursty scrolling.
        private double UI_MAX_FPS = 60.0;   // cap UI repaints (independent of analysis rate)
        private long _lastUiInvalidateTick = 0;
        private long _lastTunerTick = 0;
        private long _lastChordTick = 0;
        private long _lastHarmonicsTick = 0;

        private bool UiThrottleReady(ref long lastTick)
        {
            long now = Stopwatch.GetTimestamp();
            double elapsed = (now - lastTick) / (double)Stopwatch.Frequency;
            if (elapsed < 1.0 / UI_MAX_FPS) return false;
            lastTick = now;
            return true;
        }

        // ── Reusable bitmaps for tuner / harmonics (avoid per-frame alloc) ───
        private Bitmap _tunerBitmap;
        private Bitmap _harmonicsBitmap;

        // ── Recording ────────────────────────────────────────────────────────
        private volatile bool _isRecording = false;
        private WaveFileWriter _waveWriter;
        private readonly object _recordLock = new();
        private int _recordSampleRate = 44100;
        // Per-ridge phase dictionary keyed by ridge Id, for continuous phase tracking
        private readonly Dictionary<int, double> _ridgePhase = new(MaxActiveRidges);
        // Per-ridge previous amplitude — for click-free linear interpolation between frames
        private readonly Dictionary<int, double> _ridgePrevAmp = new(MaxActiveRidges);
        // Per-ridge last known frequency — needed to continue fading out a ridge after it disappears
        private readonly Dictionary<int, double> _ridgeLastFreq = new(MaxActiveRidges);
        // Tracks how many input samples have been captured since recording started,
        // so we write exactly the right number of output samples even when FFT frames are dropped.
        private long _recordSamplesCapured = 0;
        private long _recordSamplesWritten = 0;

        // ====================================================================
        //  Constructor
        // ====================================================================
        public Form1()
        {
            InitializeComponent();
            ParamHint.Install(this);    // Form1_ParamHint.cs — must precede Register calls
            RegisterParamHints();       // Form1_ParamHintReg.cs

            ring = new float[RingSize];

            // [OPT-2] Create static comparers once.
            _ridgeLogFreqComparer = new RidgeLogFreqComparer { Ridges = ridges };
            _ridgeLogFreqRidgeComparer = RidgeLogFreqRidgeComparer.Instance;

            _chordLabelAction = () =>
            {
                if (!chordlabel.IsDisposed) chordlabel.Text = _pendingChordText;
                if (!tbCanonicalNotes.IsDisposed) tbCanonicalNotes.Text = _pendingCanonicalNotes;
                if (!tbDetectedNotes.IsDisposed) tbDetectedNotes.Text = _pendingDetectedNotes;
                DrawHarmonicsFromText(_pendingHarmonicsDisplay);
            };

            pic.BackColor = Color.Black;
            pic.PaintSpectrogram += Pic_Paint;

            btnRecord.Click += BtnRecord_Click;
            pic.Resize += (_, __) => RecreateBitmap();
            pic.MouseClick += Pic_MouseClick;
            pic.MouseMove += Pic_MouseMove;
            pic.MouseLeave += (_, __) =>
            {
                lastMouseX = -1;
                if (scrollPaused) UpdateChordLabelFromMouse();
            };

            tbFFTSize.Leave += (_, __) => ApplyFftSizeExponent();

            // Low FFT size is always FFT_SIZE * 2 — no separate textbox needed.

            cbLowFft.CheckedChanged += (_, __) =>
            {
                _lowFftEnabled = cbLowFft.Checked;
                tbLowFftCrossoverNote.Enabled = _lowFftEnabled;
                tbLowFftCrossoverSemitones.Enabled = _lowFftEnabled;
                RebuildWindows();
                ReallocateFrameBuffers();
                lock (sampleLock)
                {
                    ring = new float[RingSize];
                    ringWritePos = ringFilled = samplesSinceLastFrame = 0;
                }
            };

            tbLowFftCrossoverNote.Leave += (_, __) =>
            {
                string s = (tbLowFftCrossoverNote.Text ?? "").Trim();
                if (string.IsNullOrWhiteSpace(s)) s = "G3";
                if (TryParseNoteToMidi(s, out int midi))
                {
                    LOW_FFT_CROSSOVER_NOTE = MidiToNoteName(midi);
                    LOW_FFT_CROSSOVER_HZ = MidiToHz(midi);
                    tbLowFftCrossoverNote.Text = LOW_FFT_CROSSOVER_NOTE;
                }
                else
                {
                    tbLowFftCrossoverNote.Text = LOW_FFT_CROSSOVER_NOTE;
                }
            };

            tbLowFftCrossoverSemitones.Leave += (_, __) =>
                ApplyDouble(tbLowFftCrossoverSemitones, ref LOW_FFT_CROSSOVER_SEMITONES, min: 0.0, max: 48.0);

            tbMaxPeaksPerFrame.Leave += (_, __) =>
                ApplyInt(tbMaxPeaksPerFrame, ref MAX_PEAKS_PER_FRAME, min: 1, max: 2000);

            tbPeakMinRel.Leave += (_, __) =>
                ApplyDouble(tbPeakMinRel, ref PEAK_MIN_REL, min: 0.0, max: 1.0);

            tbPeakMode.Leave += (_, __) =>
                ApplyInt(tbPeakMode, ref PEAK_MODE, min: 0, max: 2000);

            tbRidgeMaxCentsJump.Leave += (_, __) =>
                ApplyDouble(tbRidgeMaxCentsJump, ref RIDGE_MAX_CENTS_JUMP, min: 0.0, max: 5000.0);

            tbRidgeMissMax.Leave += (_, __) =>
            {
                ApplyInt(tbRidgeMissMax, ref RIDGE_MISS_MAX, min: 0, max: 500);
                RebuildMissFadeTable();
            };

            tbRidgeFreqEMA.Leave += (_, __) =>
                ApplyDouble(tbRidgeFreqEMA, ref RIDGE_FREQ_EMA, min: 0.0, max: 1.0);

            tbRidgeVelEMA.Leave += (_, __) =>
                ApplyDouble(tbRidgeVelEMA, ref RIDGE_VEL_EMA, min: 0.0, max: 1.0);

            tbRidgeIntensityEMA.Leave += (_, __) =>
                ApplyDouble(tbRidgeIntensityEMA, ref RIDGE_INTENSITY_EMA, min: 0.0, max: 1.0);

            tbPeakGamma.Leave += (_, __) =>
                ApplyDouble(tbPeakGamma, ref PEAK_GAMMA, min: 0.01, max: 10.0);

            tbRidgeMergeBrightnessBoost.Leave += (_, __) =>
                ApplyDouble(tbRidgeMergeBrightnessBoost, ref RIDGE_MERGE_BRIGHTNESS_BOOST, min: 0.0, max: 5.0);

            tbRidgeMergeWidthAdd.Leave += (_, __) =>
                ApplyDouble(tbRidgeMergeWidthAdd, ref RIDGE_MERGE_WIDTH_ADD, min: 0.0, max: 20.0);

            tbRidgeMergeWidthDecay.Leave += (_, __) =>
                ApplyDouble(tbRidgeMergeWidthDecay, ref RIDGE_MERGE_WIDTH_DECAY, min: 0.0, max: 1.0);

            tbMissFadePow.Leave += (_, __) =>
            {
                ApplyDouble(tbMissFadePow, ref RidgeMissFadePow, min: 0.01, max: 1.0);
                RebuildMissFadeTable();
            };

            tbAgeDecay.Leave += (_, __) =>
                ApplyDouble(tbAgeDecay, ref RidgeAgeDecay, min: 0.01, max: 1.0);

            tbMinDrawAlpha.Leave += (_, __) =>
                ApplyDouble(tbMinDrawAlpha, ref MinDrawAlpha, min: 0.0, max: 1.0);

            tbLevelSmoothEma.Leave += (_, __) =>
                ApplyDouble(tbLevelSmoothEma, ref LEVEL_SMOOTH_EMA, min: 0.0, max: 1.0);

            tbHarmonicFamilyCentsTol.Leave += (_, __) =>
                ApplyDouble(tbHarmonicFamilyCentsTol, ref HARMONIC_FAMILY_CENTS_TOL, min: 0.0, max: 200.0);

            tbHarmonicFamilyMaxRatio.Leave += (_, __) =>
                ApplyDouble(tbHarmonicFamilyMaxRatio, ref HARMONIC_FAMILY_MAX_RATIO, min: 1.0, max: 128.0);

            tbPeakMinSpacingCents.Leave += (_, __) =>
                ApplyDouble(tbPeakMinSpacingCents, ref PEAK_MIN_SPACING_CENTS, min: 0.0, max: 200.0);

            tbRidgeMergeCents.Leave += (_, __) =>
                ApplyDouble(tbRidgeMergeCents, ref RIDGE_MERGE_CENTS, min: 0.0, max: 200.0);


            tbChordAvgFrames.Leave += (_, __) =>
                ApplyInt(tbChordAvgFrames, ref CHORD_AVG_FRAMES, min: 1, max: 120);

            tbChordOutPenalty.Leave += (_, __) =>
                ApplyDouble(tbChordOutPenalty, ref CHORD_OUT_PENALTY, min: 0.0, max: 10.0);

            tbChordRidges.Leave += (_, __) =>
                ApplyInt(tbChordRidges, ref CHORD_RIDGES, min: 0, max: 15);

            tbKeyEmaSeconds.Leave += (_, __) =>
                ApplyDouble(tbKeyEmaSeconds, ref KEY_HOLD_DECAY_SECONDS, min: 1.0, max: 300.0);

            tbRidgeMatchLogHz.Leave += (_, __) =>
                ApplyDouble(tbRidgeMatchLogHz, ref RIDGE_MATCH_LOGHZ, min: 0.001, max: 1.0);

            tbRidgeMatchLogHzPredBoost.Leave += (_, __) =>
                ApplyDouble(tbRidgeMatchLogHzPredBoost, ref RIDGE_MATCH_LOGHZ_PRED_BOOST, min: 0.0, max: 10.0);

            tbMaxColShift.Leave += (_, __) =>
                ApplyDouble(tbMaxColShift, ref MAX_COL_SHIFT, min: 0.0, max: 1e6);

            tbTargetFps.Leave += (_, __) =>
                ApplyDouble(tbTargetFps, ref TARGET_FPS, min: 0.0, max: 10000.0, onChanged: () =>
                {
                    ApplyTargetFps();
                    RestartCaptureIfRunning(clearSpectrogram: false);
                });

            tbHighPass.Leave += (_, __) =>
            {
                ApplyNote(tbHighPass, ref HighPassNote, ref HighPass, defaultNote: "C1");
                float oldFmin = displayFmin, oldFmax = displayFmax;
                RefreshDisplayRange();
                if (Math.Abs(displayFmin - oldFmin) > 0.01f || Math.Abs(displayFmax - oldFmax) > 0.01f)
                    RetrofitSpectrogramToNewScale(oldFmin, oldFmax);
                pic.Invalidate();
            };

            tbLowPass.Leave += (_, __) =>
            {
                ApplyNote(tbLowPass, ref LowPassNote, ref LowPass, defaultNote: "C8");
                float oldFmin = displayFmin, oldFmax = displayFmax;
                RefreshDisplayRange();
                if (Math.Abs(displayFmin - oldFmin) > 0.01f || Math.Abs(displayFmax - oldFmax) > 0.01f)
                    RetrofitSpectrogramToNewScale(oldFmin, oldFmax);
                pic.Invalidate();
            };

            tbHarmonicSuppression.Leave += (_, __) =>
                ApplyDouble(tbHarmonicSuppression, ref HARMONIC_SUPPRESSION, min: 0.0, max: 1.0);

            tbtuning.Leave += (_, __) =>
            {
                ApplyDouble(tbtuning, ref tuning, min: 300, max: 600, onChanged: () =>
                {
                    RefreshDisplayRange();
                    pic.Invalidate();
                });
            };

            cblines.CheckedChanged += (_, __) =>
            {
                if (!pic.IsDisposed && pic.IsHandleCreated) pic.Invalidate();
            };

            cbFlats.CheckedChanged += (_, __) =>
            {
                _useFlats = cbFlats.Checked;
                RefreshEnharmonicUiForNotation();
                DrawPbKey();
                if (!pic.IsDisposed && pic.IsHandleCreated) pic.Invalidate();
            };

            cbVolume.CheckedChanged += (_, __) => ApplyVolumeLock();
            volume.ValueChanged += (_, __) => ApplyVolumeLock();

            RebuildWindows();
            ReallocateFrameBuffers();
            RebuildMissFadeTable();
            Load += Form1_Load;
            FormClosing += Form1_FormClosing;
        }

        // ────────────────────────────────────────────────────────────────────
        //  Frame buffer allocation / reallocation
        // ────────────────────────────────────────────────────────────────────
        private void ReallocateFrameBuffers()
        {
            int N = FFT_SIZE;
            int half = N / 2;

            _X = new Complex[N];
            _Xd = new Complex[N];
            _Xt = new Complex[N];
            _Z = new Complex[N];   // packed buffer for 2-FFT optimization

            _mag = new double[half];
            _fReassign = new double[half];
            _tShiftCols = new double[half];
            _fSm = new double[half];
            _tSm = new double[half];

            // ── Low-freq FFT buffers (only when enabled) ──────────────────────
            if (_lowFftEnabled)
            {
                int NL = Math.Max(FFT_SIZE_L, N); // low-freq FFT must be >= high-freq
                int halfL = NL / 2;
                _XL = new Complex[NL];
                _XdL = new Complex[NL];
                _XtL = new Complex[NL];
                _ZL = new Complex[NL];
                _magL = new double[halfL];
                _fReassignL = new double[halfL];
                _tShiftColsL = new double[halfL];
            }
            else
            {
                _XL = _XdL = _XtL = _ZL = null;
                _magL = _fReassignL = _tShiftColsL = null;
            }

            if (_peaksScratch.Length < half / 2)
                _peaksScratch = new Peak[half / 2];
        }

        // ====================================================================
        //  Mouse / Pause
        // ====================================================================
        private void Pic_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            scrollPaused = !scrollPaused;
            if (scrollPaused) { lastMouseX = e.X; UpdateChordLabelFromMouse(); }
            else
            {
                _pendingHarmonicsText = null;
                SetChordLabelUi(lastDetectedChordText, lastDetectedCanonicalText, lastDetectedNotesText, lastDetectedHarmonicsText);
                // Restore live tuner immediately on unpause.
                DrawTunerUi(TunerFreqHz, RawFrameIntensity);
            }
        }

        private void Pic_MouseMove(object sender, MouseEventArgs e)
        {
            lastMouseX = e.X;
            if (scrollPaused) UpdateChordLabelFromMouse();
        }

        private void UpdateChordLabelFromMouse()
        {
            int w = pic.ClientSize.Width;
            if (w <= 0) return;
            int x = lastMouseX;
            if (x < 0 || x >= w) return;

            string chord = null;
            string canonical = null;
            string detectedNotes = null;
            string harmonics = null;
            double tunerFreq = 0.0;
            double tunerIntensity = 0.0;
            lock (bitmapLock)
            {
                if (chordCols != null && x < chordCols.Length)
                    chord = chordCols[x];
                if (canonicalCols != null && x < canonicalCols.Length)
                    canonical = canonicalCols[x];
                if (detectedNotesCols != null && x < detectedNotesCols.Length)
                    detectedNotes = detectedNotesCols[x];
                if (harmonicsCols != null && x < harmonicsCols.Length)
                    harmonics = harmonicsCols[x];
                if (tunerFreqCols != null && x < tunerFreqCols.Length)
                    tunerFreq = tunerFreqCols[x];
                if (tunerIntensityCols != null && x < tunerIntensityCols.Length)
                    tunerIntensity = tunerIntensityCols[x];
            }

            // Refresh tuner to show the historical position under the cursor.
            DrawTunerUi(tunerFreq, tunerIntensity);

            if (string.IsNullOrWhiteSpace(chord) || chord == "—")
            {
                SetChordLabelUi("—", "", detectedNotes ?? "", harmonics ?? "");
                return;
            }

            SetChordLabelUi(chord, canonical ?? "", detectedNotes ?? "", harmonics ?? "");
        }
        /*
        private void SetChordLabelUi(string text, string canonical = "", string detectedNotes = null, string harmonics = null)
        {
            ApplyAutoKeyDetection();        // ← auto key detect pump
            string detected = detectedNotes ?? "";
            string harm = harmonics ?? "";

            if (_pendingChordText == text &&
                _pendingCanonicalNotes == canonical &&
                _pendingDetectedNotes == detected &&
                _pendingHarmonicsDisplay == harm)
                return;

            _pendingChordText = text;
            _pendingCanonicalNotes = canonical;
            _pendingDetectedNotes = detected;
            _pendingHarmonicsDisplay = harm;

            if (!IsDisposed && IsHandleCreated && UiThrottleReady(ref _lastChordTick))
            {
                try { BeginInvoke(_chordLabelAction); } catch (Exception ex) { RethrowUnexpectedException(ex); }
            }
        }
        */
        // ====================================================================
        //  Note / MIDI helpers
        // ====================================================================
        private void ApplyNote(TextBox tb, ref string noteField, ref double hzField, string defaultNote)
        {
            string s = (tb.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(s)) s = defaultNote;

            if (!TryParseNoteToMidi(s, out int midi))
            {
                if (string.IsNullOrWhiteSpace(noteField)) noteField = defaultNote;
                tb.Text = noteField;
                hzField = MidiToHz(ClampMidiToC0C8(NoteToMidiSafe(noteField, defaultNote)));
                return;
            }

            midi = ClampMidiToC0C8(midi);
            string norm = MidiToNoteName(midi);
            noteField = norm;
            tb.Text = norm;
            hzField = MidiToHz(midi);
        }

        private int NoteToMidiSafe(string note, string fallback)
        {
            if (TryParseNoteToMidi(note, out int m)) return ClampMidiToC0C8(m);
            if (TryParseNoteToMidi(fallback, out int f)) return ClampMidiToC0C8(f);
            return 12;
        }

        private static int ClampMidiToC0C8(int midi) => Math.Clamp(midi, 12, 108);
        private double MidiToHz(int midi) => tuning * Math.Pow(2.0, (midi - 69) / 12.0);

        private static bool TryParseNoteToMidi(string text, out int midi)
        {
            midi = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string s = text.Trim();
            char letter = char.ToUpperInvariant(s[0]);
            int idx = 1;

            int semitoneFromC = letter switch
            {
                'C' => 0,
                'D' => 2,
                'E' => 4,
                'F' => 5,
                'G' => 7,
                'A' => 9,
                'B' => 11,
                _ => -1
            };
            if (semitoneFromC < 0) return false;

            if (idx < s.Length && (s[idx] is '#' or 'b' or 'B'))
            {
                semitoneFromC += s[idx] == '#' ? 1 : -1;
                idx++;
            }

            if (idx >= s.Length) return false;
            if (!int.TryParse(s.AsSpan(idx), NumberStyles.Integer, CultureInfo.InvariantCulture, out int oct))
                return false;

            semitoneFromC = (semitoneFromC % 12 + 12) % 12;
            midi = 12 + oct * 12 + semitoneFromC;
            return true;
        }

        private string MidiToNoteName(int midi)
        {
            int pc = ((midi % 12) + 12) % 12;
            int oct = (midi / 12) - 1;
            return NoteNames[pc] + oct.ToString(CultureInfo.InvariantCulture);
        }

        private void RefreshEnharmonicUiForNotation()
        {
            if (tbHighPass != null)
            {
                int midi = NoteToMidiSafe(HighPassNote, "C1");
                string norm = MidiToNoteName(midi);
                HighPassNote = norm;
                tbHighPass.Text = norm;
            }

            if (tbLowPass != null)
            {
                int midi = NoteToMidiSafe(LowPassNote, "C8");
                string norm = MidiToNoteName(midi);
                LowPassNote = norm;
                tbLowPass.Text = norm;
            }
        }


        // [OPT-9] Zero-allocation ChordNotesText using pre-allocated scratch buffers.
        private string ChordNotesText(double[] normChroma)
        {
            if (normChroma == null) return "";

            int count = 0;
            for (int i = 0; i < 12; i++)
            {
                double frac = normChroma[i];
                if (frac >= 0.05) _chordNotesSorted[count++] = (i, frac);
            }
            if (count == 0) return "";

            for (int i = 1; i < count; i++)
            {
                var key = _chordNotesSorted[i];
                int j = i - 1;
                while (j >= 0 && _chordNotesSorted[j].frac < key.frac) { _chordNotesSorted[j + 1] = _chordNotesSorted[j]; j--; }
                _chordNotesSorted[j + 1] = key;
            }

            _chordNotesSb.Clear();
            for (int i = 0; i < count; i++)
            {
                var (pc, frac) = _chordNotesSorted[i];
                if (_chordNotesSb.Length > 0) _chordNotesSb.Append("  ");
                _chordNotesSb.Append(NoteName(pc));
                _chordNotesSb.Append('(');
                _chordNotesSb.Append((int)(frac * 100.0 + 0.5));
                _chordNotesSb.Append("%)");
            }
            return _chordNotesSb.ToString();
        }


        // ====================================================================
        //  Param helpers
        // ====================================================================
        private static bool IsPowerOfTwo(int x) => x > 0 && (x & (x - 1)) == 0;

        private static int NextPowerOfTwo(int x)
        {
            if (x < 1) return 1;
            int p = 1;
            while (p < x && p > 0) p <<= 1;
            return p > 0 ? p : 1 << 30;
        }

        // Returns floor(log2(x)) for x >= 1.
        private static int Log2(int x)
        {
            int n = 0;
            while (x > 1) { x >>= 1; n++; }
            return n;
        }

        // Called when tbFFTSize loses focus.  The user types a power-of-two exponent
        // (e.g. 13 → 8192).  Low FFT is always one power higher (FFT_SIZE_L = FFT_SIZE * 2).
        private void ApplyFftSizeExponent()
        {
            if (!int.TryParse(tbFFTSize.Text.Trim(), out int exp))
                exp = 13; // default 8192
            exp = Math.Clamp(exp, 8, 20); // 256 … 1 048 576
            FFT_SIZE = 1 << exp;
            FFT_SIZE_L = FFT_SIZE * 2;    // always one power of two higher
            tbFFTSize.Text = exp.ToString(CultureInfo.InvariantCulture);
            RebuildWindows();
            ReallocateFrameBuffers();
            lock (sampleLock)
            {
                ring = new float[RingSize];
                ringWritePos = ringFilled = samplesSinceLastFrame = 0;
            }
            ApplyTargetFps();
            RefreshDisplayRange();
            RestartCaptureIfRunning(clearSpectrogram: false);
            pic.Invalidate();
        }

        private void RebuildWindows()
        {
            BuildHannWindows(FFT_SIZE, out hann, out hannD, out hannT);
            if (_lowFftEnabled)
            {
                int NL = Math.Max(FFT_SIZE_L, FFT_SIZE);
                BuildHannWindows(NL, out hannL, out hannDL, out hannTL);
            }
            else
            {
                hannL = hannDL = hannTL = null;
            }
        }

        private static void BuildHannWindows(int N,
            out double[] w, out double[] wd, out double[] wt)
        {
            w = new double[N];
            wd = new double[N];
            wt = new double[N];
            double denom = N - 1, center = 0.5 * denom;
            for (int n = 0; n < N; n++)
            {
                double a = 2.0 * Math.PI * n / denom;
                w[n] = 0.5 * (1.0 - Math.Cos(a));
                wd[n] = (Math.PI / denom) * Math.Sin(a);
                wt[n] = (n - center) * w[n];
            }
        }

        // ────────────────────────────────────────────────────────────────────
        //  Low-freq FFT reassignment pass
        // ────────────────────────────────────────────────────────────────────
        // Runs the full 2-FFT reassignment on the most recent FFT_SIZE_L samples
        // from the ring (the low-freq FFT has a larger window so it always fits).
        // Stores per-bin reassigned frequency estimates in _fReassignL[], magnitudes in _magL[].
        // The caller blends these into fSm[] for bins below the crossover.
        private bool RunLowFftReassignment(float[] samples, double fs, double hopSizeLocal)
        {
            int NL = _XL?.Length ?? 0;
            if (NL < 2) return false;
            int halfL = NL / 2;
            var XL = _XL;
            var XdL = _XdL;
            var XtL = _XtL;
            var ZL = _ZL;
            var magL = _magL;
            var frL = _fReassignL;
            var tcL = _tShiftColsL;
            var wL = hannL;
            var wdL = hannDL;
            var wtL = hannTL;

            if (XL.Length != NL || XdL.Length != NL || XtL.Length != NL || ZL.Length != NL) return false;
            if (magL.Length != halfL || frL.Length != halfL || tcL.Length != halfL) return false;
            if (wL.Length != NL || wdL.Length != NL || wtL.Length != NL) return false;
            if (samples.Length < NL) return false; // ring not yet large enough (shouldn't happen)

            // Use the most recent NL samples (the full low-freq window = the entire snapshot).
            int offset = samples.Length - NL;
            double maxShift = MAX_COL_SHIFT;
            const double INV_2PI = 1.0 / (2.0 * Math.PI);

            for (int n = 0; n < NL; n++)
            {
                double s = samples[offset + n];
                XL[n] = new Complex(s * wL[n], 0.0);
                ZL[n] = new Complex(s * wdL[n], s * wtL[n]);
            }

            FFT(XL);
            FFT(ZL);

            // Unpack Xd and Xt from ZL
            {
                Complex z0 = ZL[0];
                XdL[0] = new Complex(z0.Real, 0.0);
                XtL[0] = new Complex(z0.Imaginary, 0.0);

                for (int k = 1; k < halfL; k++)
                {
                    Complex zk = ZL[k];
                    Complex znk = ZL[NL - k];
                    Complex cZnk = new Complex(znk.Real, -znk.Imaginary);

                    XdL[k] = new Complex(
                        (zk.Real + cZnk.Real) * 0.5,
                        (zk.Imaginary + cZnk.Imaginary) * 0.5);

                    double dR = cZnk.Real - zk.Real;
                    double dI = cZnk.Imaginary - zk.Imaginary;
                    XtL[k] = new Complex(-dI * 0.5, dR * 0.5);
                }

                Complex zh = ZL[halfL];
                XdL[halfL] = new Complex(zh.Real, 0.0);
                XtL[halfL] = new Complex(zh.Imaginary, 0.0);
            }

            // Reassignment
            for (int k = 1; k < halfL; k++)
            {
                Complex x = XL[k];
                double m = x.Magnitude;
                magL[k] = m;
                double fk = (double)k * fs / NL;

                if (m > 1e-12)
                {
                    Complex ratioD = XdL[k] / x;
                    double fhat = fk - (fs * INV_2PI) * ratioD.Imaginary;
                    if (double.IsNaN(fhat) || double.IsInfinity(fhat)) fhat = fk;
                    frL[k] = Math.Clamp(fhat, 0.0, fs * 0.5);

                    double rawShift = -(XtL[k] / x).Real;
                    if (double.IsNaN(rawShift) || double.IsInfinity(rawShift)) rawShift = 0.0;
                    tcL[k] = Math.Clamp(rawShift, -maxShift, maxShift) / hopSizeLocal;
                }
                else
                {
                    frL[k] = fk;
                    tcL[k] = 0.0;
                }
            }
            return true;
        }

        // ────────────────────────────────────────────────────────────────────
        //  Blend low-freq FFT reassignment into fSm[]
        // ────────────────────────────────────────────────────────────────────
        // Above (crossoverHz + halfBlendHz) → high-freq FFT only (weight=0 for low).
        // Below (crossoverHz - halfBlendHz) → low-freq FFT only (weight=1 for low).
        // In between → linear blend.
        // halfBlendHz = crossoverHz * (2^(semitones/24) - 1)  (half the semitone span in Hz)
        private void BlendLowFftReassignment(
            double[] fSm, double[] tSm, double[] mag,
            int half, double fs, double crossoverHz, double semitones)
        {
            var frL = _fReassignL;
            var tcL = _tShiftColsL;
            if (frL == null || tcL == null) return;

            int NL = _XL?.Length ?? 0;
            int halfL = NL / 2;
            if (halfL < 1) return;

            int N = FFT_SIZE;

            // Compute blend region edges in Hz using equal-temperament cents arithmetic.
            double halfSemi = semitones * 0.5;
            double loBoundHz = crossoverHz * Math.Pow(2.0, -halfSemi / 12.0);
            double hiBoundHz = crossoverHz * Math.Pow(2.0, halfSemi / 12.0);
            double blendRangeHz = Math.Max(hiBoundHz - loBoundHz, 1e-6);

            for (int k = 2; k < half - 2; k++)
            {
                double fk = (double)k * fs / N;

                // w = 1 → use low FFT fully; w = 0 → use high FFT fully.
                double w;
                if (fk <= loBoundHz)
                    w = 1.0;
                else if (fk >= hiBoundHz)
                    w = 0.0;
                else
                    w = 1.0 - (fk - loBoundHz) / blendRangeHz;

                if (w < 1e-4) continue; // effectively zero, skip

                // Nearest low-FFT bin
                int j = (int)Math.Round(fk * NL / fs);
                if ((uint)j >= (uint)halfL) continue;

                fSm[k] = (1.0 - w) * fSm[k] + w * frL[j];
                tSm[k] = (1.0 - w) * tSm[k] + w * tcL[j];
            }
        }


        private DeviceItem CurrentDevice() => comboBox1.SelectedItem as DeviceItem;

        private void RestartCaptureIfRunning(bool clearSpectrogram = true)
        {
            var di = CurrentDevice();
            bool wasRunning = capture != null && di != null;
            StopCapture();
            if (clearSpectrogram) ClearSpectrogram();
            if (wasRunning) StartCapture(di);
        }

        private void RestartWorkerIfRunning()
        {
            if (capture == null) return;
            StopWorker();
            StartWorker();
        }

        private void ApplyInt(TextBox tb, ref int field, int min, int max,
            Action onChanged = null, Func<int, int> normalize = null)
        {
            if (!int.TryParse(tb.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
            { tb.Text = field.ToString(CultureInfo.InvariantCulture); return; }

            if (normalize != null) v = normalize(v);
            v = Math.Clamp(v, min, max);

            bool changed = v != field;
            field = v;
            tb.Text = field.ToString(CultureInfo.InvariantCulture);
            if (changed) onChanged?.Invoke();
        }

        private void ApplyDouble(TextBox tb, ref double field, double min, double max,
            Action onChanged = null)
        {
            if (!double.TryParse(tb.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v)
                || double.IsNaN(v) || double.IsInfinity(v))
            { tb.Text = field.ToString("0.########", CultureInfo.InvariantCulture); return; }

            v = Math.Clamp(v, min, max);
            bool changed = Math.Abs(v - field) > 1e-15;
            field = v;
            tb.Text = field.ToString("0.########", CultureInfo.InvariantCulture);
            if (changed) onChanged?.Invoke();
        }

        // ====================================================================
        //  Form
        // ====================================================================
        private void Form1_Load(object sender, EventArgs e)
        {
            LoadSettings(out bool restoredShowGridLines, out string restoredDeviceName, out bool restoredUseFlats);
            _useFlats = restoredUseFlats;

            ring = new float[RingSize];
            hopSize = Math.Max(1, HOP_SIZE); // TARGET_FPS will refine after device selected
            RebuildWindows();
            ReallocateFrameBuffers();
            RebuildMissFadeTable();

            tbFFTSize.Text = Log2(FFT_SIZE).ToString(CultureInfo.InvariantCulture);
            // FFT_SIZE_L is always FFT_SIZE * 2; no separate textbox
            cbLowFft.Checked = _lowFftEnabled;
            tbLowFftCrossoverNote.Text = LOW_FFT_CROSSOVER_NOTE;
            tbLowFftCrossoverSemitones.Text = LOW_FFT_CROSSOVER_SEMITONES.ToString("0.##", CultureInfo.InvariantCulture);
            tbLowFftCrossoverNote.Enabled = _lowFftEnabled;
            tbLowFftCrossoverSemitones.Enabled = _lowFftEnabled;
            //tbOverlapFactor.Text = HOP_SIZE.ToString(CultureInfo.InvariantCulture);
            tbMaxPeaksPerFrame.Text = MAX_PEAKS_PER_FRAME.ToString(CultureInfo.InvariantCulture);
            tbPeakMinRel.Text = PEAK_MIN_REL.ToString("0.########", CultureInfo.InvariantCulture);
            tbPeakMode.Text = PEAK_MODE.ToString(CultureInfo.InvariantCulture);
            tbRidgeMaxCentsJump.Text = RIDGE_MAX_CENTS_JUMP.ToString("0.########", CultureInfo.InvariantCulture);
            tbRidgeMissMax.Text = RIDGE_MISS_MAX.ToString(CultureInfo.InvariantCulture);
            tbRidgeFreqEMA.Text = RIDGE_FREQ_EMA.ToString("0.########", CultureInfo.InvariantCulture);
            tbRidgeVelEMA.Text = RIDGE_VEL_EMA.ToString("0.########", CultureInfo.InvariantCulture);
            tbRidgeIntensityEMA.Text = RIDGE_INTENSITY_EMA.ToString("0.########", CultureInfo.InvariantCulture);
            tbPeakGamma.Text = PEAK_GAMMA.ToString("0.########", CultureInfo.InvariantCulture);
            tbMissFadePow.Text = RidgeMissFadePow.ToString("0.########", CultureInfo.InvariantCulture);
            tbAgeDecay.Text = RidgeAgeDecay.ToString("0.########", CultureInfo.InvariantCulture);
            tbMinDrawAlpha.Text = MinDrawAlpha.ToString("0.########", CultureInfo.InvariantCulture);
            tbLevelSmoothEma.Text = LEVEL_SMOOTH_EMA.ToString("0.########", CultureInfo.InvariantCulture);
            tbHarmonicFamilyCentsTol.Text = HARMONIC_FAMILY_CENTS_TOL.ToString("0.########", CultureInfo.InvariantCulture);
            tbHarmonicFamilyMaxRatio.Text = HARMONIC_FAMILY_MAX_RATIO.ToString("0.########", CultureInfo.InvariantCulture);
            tbPeakMinSpacingCents.Text = PEAK_MIN_SPACING_CENTS.ToString("0.########", CultureInfo.InvariantCulture);
            tbRidgeMergeCents.Text = RIDGE_MERGE_CENTS.ToString("0.########", CultureInfo.InvariantCulture);
            tbRidgeMergeBrightnessBoost.Text = RIDGE_MERGE_BRIGHTNESS_BOOST.ToString("0.########", CultureInfo.InvariantCulture);
            tbRidgeMergeWidthAdd.Text = RIDGE_MERGE_WIDTH_ADD.ToString("0.########", CultureInfo.InvariantCulture);
            tbRidgeMergeWidthDecay.Text = RIDGE_MERGE_WIDTH_DECAY.ToString("0.########", CultureInfo.InvariantCulture);
            tbChordAvgFrames.Text = CHORD_AVG_FRAMES.ToString(CultureInfo.InvariantCulture);
            tbChordOutPenalty.Text = CHORD_OUT_PENALTY.ToString("0.########", CultureInfo.InvariantCulture);
            tbChordRidges.Text = CHORD_RIDGES.ToString("0", CultureInfo.InvariantCulture);
            tbKeyEmaSeconds.Text = KEY_HOLD_DECAY_SECONDS.ToString("0.##", CultureInfo.InvariantCulture);
            tbtuning.Text = tuning.ToString("0.########", CultureInfo.InvariantCulture);
            tbHighPass.Text = HighPassNote;
            tbLowPass.Text = LowPassNote;
            tbHarmonicSuppression.Text = HARMONIC_SUPPRESSION.ToString("0.########", CultureInfo.InvariantCulture);
            tbRidgeMatchLogHz.Text = RIDGE_MATCH_LOGHZ.ToString("0.########", CultureInfo.InvariantCulture);
            tbRidgeMatchLogHzPredBoost.Text = RIDGE_MATCH_LOGHZ_PRED_BOOST.ToString("0.########", CultureInfo.InvariantCulture);
            tbMaxColShift.Text = MAX_COL_SHIFT.ToString("0.########", CultureInfo.InvariantCulture);
            tbTargetFps.Text = TARGET_FPS.ToString("0.##", CultureInfo.InvariantCulture);

            ApplyNote(tbHighPass, ref HighPassNote, ref HighPass, defaultNote: "C1");
            ApplyNote(tbLowPass, ref LowPassNote, ref LowPass, defaultNote: "C8");
            RefreshDisplayRange();

            PopulateDevices();

            bool deviceRestored = false;
            if (!string.IsNullOrWhiteSpace(restoredDeviceName))
            {
                for (int i = 0; i < comboBox1.Items.Count; i++)
                {
                    if (comboBox1.Items[i] is DeviceItem di &&
                        di.Name.Equals(restoredDeviceName, StringComparison.OrdinalIgnoreCase))
                    {
                        comboBox1.SelectedIndex = i;
                        deviceRestored = true;
                        break;
                    }
                }
            }
            if (!deviceRestored && comboBox1.Items.Count > 0)
                comboBox1.SelectedIndex = 0;

            cblines.Checked = restoredShowGridLines;
            cbFlats.Checked = _useFlats;

            RefreshDisplayRange();
            RecreateBitmap();
            SetChordLabelUi("―");
            DrawPbKey();
            InitScaleDropdowns();
            InitCircleOfFifths();
        }

        private void DrawPbKey()
        {
            if (pbKey == null) return;
            int w = Math.Max(1, pbKey.Width);
            int h = Math.Max(1, pbKey.Height);

            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Black);
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                float rowHeight = (float)h / 12f;
                float padding = Math.Max(2f, w * 0.04f);
                float barWidth = Math.Clamp(w * 0.18f, 10f, w * 0.4f);
                float barHeight = Math.Max(6f, rowHeight * 0.6f);
                float fontSize = Math.Max(8f, Math.Min(14f, rowHeight * 0.6f));

                using var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Point);
                var sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };

                for (int pc = 0; pc < 12; pc++)
                {
                    float top = (11 - pc) * rowHeight;
                    var barRect = new RectangleF(padding, top + (rowHeight - barHeight) * 0.5f, barWidth, barHeight);

                    var (r, gcol, bcol) = PitchColor(NoteFrequency(pc, 4), 0.95);

                    using (var brush = new SolidBrush(Color.FromArgb(r, gcol, bcol)))
                        g.FillRectangle(brush, barRect);

                    using (var pen = new Pen(Color.FromArgb(120, Color.Black)))
                        g.DrawRectangle(pen, barRect.X, barRect.Y, barRect.Width, barRect.Height);

                    var textRect = new RectangleF(barRect.Right + padding, top, w - (barRect.Right + 2 * padding), rowHeight);
                    using var textBrush = new SolidBrush(Color.White);
                    g.DrawString(NoteName(pc), font, textBrush, textRect, sf);
                }
            }

            var old = pbKey.Image;
            pbKey.Image = bmp;
            old?.Dispose();
            pbKey.SizeMode = PictureBoxSizeMode.Normal;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_isRecording) StopRecording();
            SaveSettings();
            StopCapture();
            lock (bitmapLock)
            {
                spectrogramBitmap?.Dispose(); spectrogramBitmap = null;
            }

            _gridPen?.Dispose(); _gridPen = null;
            _gridFont?.Dispose(); _gridFont = null;
            _gridBrush?.Dispose(); _gridBrush = null;
            _chordOverlayFont?.Dispose(); _chordOverlayFont = null;
            _chordOverlayFontQuality?.Dispose(); _chordOverlayFontQuality = null;
        }

        // ====================================================================
        //  Real-time tuner display
        // ====================================================================
        private void DrawTuner()
        {
            double freq = TunerFreqHz;
            double rawIntensity = RawFrameIntensity;
            if (!IsHandleCreated || IsDisposed) return;
            // When paused the UI uses history; only update the "live" tuner when not paused.
            if (!scrollPaused && UiThrottleReady(ref _lastTunerTick))
                try { BeginInvoke((Action)(() => DrawTunerUi(freq, rawIntensity))); } catch (Exception ex) { RethrowUnexpectedException(ex); }
        }

        private void DrawTunerUi(double freq, double rawIntensity)
        {
            if (pbTuner == null || pbTuner.IsDisposed) return;
            int w = Math.Max(1, pbTuner.Width);
            int h = Math.Max(1, pbTuner.Height);

            // Reuse bitmap — only reallocate on size change
            if (_tunerBitmap == null || _tunerBitmap.Width != w || _tunerBitmap.Height != h)
            {
                _tunerBitmap?.Dispose();
                _tunerBitmap = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            }
            var bmp = _tunerBitmap;
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(Color.Black);

            int cx = w / 2;
            using var centerPen = new Pen(Color.FromArgb(100, 100, 100), 1);
            g.DrawLine(centerPen, cx, 0, cx, h);

            if (freq <= 0.0)
            {
                pbTuner.Image = bmp;
                return;
            }

            double midi = 69.0 + 12.0 * Math.Log2(freq / tuning);
            int nearestMidi = (int)Math.Round(midi);
            double cents = (midi - nearestMidi) * 100.0;

            int noteIdx = ((nearestMidi % 12) + 12) % 12;
            int octave = nearestMidi / 12 - 1;
            string label = NoteName(noteIdx) + octave;

            var (cr, cg, cb) = PitchColor(freq, 1.0);
            var noteColor = Color.FromArgb(cr, cg, cb);

            double xFrac = (cents + 50.0) / 100.0;
            int noteX = (int)Math.Round(xFrac * (w - 1));

            int barW = 2;
            var barRect = new Rectangle(noteX - barW / 2, 1, barW, h - 2);
            using var barBrush = new SolidBrush(noteColor);
            g.FillRectangle(barBrush, barRect);

            float fontSize = Math.Max(8f, Math.Min(14f, h * 0.45f));
            using var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Point);
            SizeF textSz = g.MeasureString(label, font);
            float textX = Math.Clamp(noteX - textSz.Width * 0.5f, 0, w - textSz.Width);
            float textY = (h - textSz.Height) * 0.5f;

            using var outlineBrush = new SolidBrush(Color.Black);
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if (dx != 0 || dy != 0)
                        g.DrawString(label, font, outlineBrush, textX + dx, textY + dy);

            g.DrawString(label, font, barBrush, textX, textY);

            // ── Absolute intensity meter (bottom 10 px, starts 25 px from left) ──────
            // Shows raw FFT peak amplitude before any normalization, on a log scale
            // so that quiet signals are still visible and loud signals don't saturate.
            // Range: 1e-5 (silence) → 1e-1 (loud) maps to 0..1 on meter.
            {
                int meterY = h - 10;
                int meterLeft = 25;
                int meterRight = w - 1;
                int meterW = Math.Max(0, meterRight - meterLeft);
                int meterH = 9;

                // Background trough
                using var troughBrush = new SolidBrush(Color.FromArgb(40, 40, 40));
                g.FillRectangle(troughBrush, meterLeft, meterY, meterW, meterH);

                // Log-scale the estimated amplitude: map [0.001 .. 1.0] → [0 .. 1]
                const double logMin = -3.0, logMax = 0.0;
                double logVal = rawIntensity > 1e-12 ? Math.Log10(rawIntensity) : logMin - 1.0;
                double displayFrac = Math.Clamp((logVal - logMin) / (logMax - logMin), 0.0, 1.0);
                int fillW = (int)Math.Round(displayFrac * meterW);
                if (fillW > 0)
                {
                    // Colour: green → yellow → red as level increases
                    int r2 = (int)Math.Clamp(255 * Math.Min(1.0, displayFrac * 2.0), 0, 255);
                    int g2 = (int)Math.Clamp(255 * Math.Min(1.0, (1.0 - displayFrac) * 2.0), 0, 255);
                    using var fillBrush = new SolidBrush(Color.FromArgb(r2, g2, 60));
                    g.FillRectangle(fillBrush, meterLeft, meterY, fillW, meterH);
                }

                // Trough outline
                using var troughPen = new Pen(Color.FromArgb(80, 80, 80), 1);
                g.DrawRectangle(troughPen, meterLeft, meterY, meterW, meterH);
            }

            pbTuner.Image = bmp;
        }

        // ====================================================================
        //  Devices
        // ====================================================================
        private record DeviceItem(string Id, string Name, bool Loopback)
        {
            public override string ToString() => Name;
        }

        private void PopulateDevices()
        {
            comboBox1.Items.Clear();
            using var en = new MMDeviceEnumerator();
            foreach (var d in en.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
                comboBox1.Items.Add(new DeviceItem(d.ID, "Input: " + d.FriendlyName, false));
            foreach (var d in en.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
                comboBox1.Items.Add(new DeviceItem(d.ID, "Loopback: " + d.FriendlyName, true));

            comboBox1.SelectedIndexChanged += (_, __) =>
            {
                if (comboBox1.SelectedItem is DeviceItem di) StartCapture(di);
            };
        }

        // ====================================================================
        //  Capture
        // ====================================================================
        // ── ApplyTargetFps ────────────────────────────────────────────────────
        // Call any time TARGET_FPS, FFT_SIZE, or the sample rate changes.
        // Must be called on the UI thread; also updates the hop textbox.
        private void ApplyTargetFps()
        {
            if (TARGET_FPS > 0 && captureFormat != null)
            {
                int computed = Math.Max(1, (int)Math.Round(captureFormat.SampleRate / TARGET_FPS));
                HOP_SIZE = computed;
                //tbOverlapFactor.Text = HOP_SIZE.ToString(CultureInfo.InvariantCulture);
                lock (sampleLock) { hopSize = HOP_SIZE; samplesSinceLastFrame = 0; }
            }
            else if (TARGET_FPS > 0)
            {
                // No capture yet — textbox will update when capture starts
            }
        }

        private void StartCapture(DeviceItem item)
        {
            StopCapture();
            using var en = new MMDeviceEnumerator();
            var dev = en.GetDevice(item.Id);
            capture = item.Loopback ? new WasapiLoopbackCapture(dev) : new WasapiCapture(dev);
            captureFormat = capture.WaveFormat;
            // Compute or restore hop size now that we know the sample rate
            if (TARGET_FPS > 0)
            {
                HOP_SIZE = Math.Max(1, (int)Math.Round(captureFormat.SampleRate / TARGET_FPS));
                //tbOverlapFactor.Text = HOP_SIZE.ToString(CultureInfo.InvariantCulture);
            }
            hopSize = Math.Max(1, HOP_SIZE);
            ringWritePos = ringFilled = samplesSinceLastFrame = 0;

            lock (ridgeLock)
            {
                foreach (var r in ridges) ReturnRidge(r);
                ridges.Clear();
                _ridgeById.Clear();
                nextRidgeId = 1;
            }

            lock (chordLock)
            {
                while (chromaQueue.Count > 0)
                {
                    var arr = chromaQueue.Dequeue();
                    _chromaPool.Push(arr);
                }
                Array.Clear(chromaSum, 0, chromaSum.Length);
                lastDetectedChordText = "—";
            }

            lock (bitmapLock)
            {
                if (chordCols != null) Array.Fill(chordCols, "—");
                if (detectedNotesCols != null) Array.Fill(detectedNotesCols, "");
                if (canonicalCols != null) Array.Fill(canonicalCols, "");
            }

            scrollPaused = false;
            SetChordLabelUi("—");
            StartWorker();
            capture.DataAvailable += Capture_DataAvailable;
            capture.StartRecording();
        }

        private void StopCapture()
        {
            if (capture != null)
            {
                try { capture.DataAvailable -= Capture_DataAvailable; } catch (Exception ex) { RethrowUnexpectedException(ex); }
                try { capture.StopRecording(); } catch (Exception ex) { RethrowUnexpectedException(ex); }
                try { capture.Dispose(); } catch (Exception ex) { RethrowUnexpectedException(ex); }
                capture = null;
            }
            StopWorker();
            captureFormat = null;
        }

        // ====================================================================
        //  Data
        // ====================================================================
        private void Capture_DataAvailable(object sender, WaveInEventArgs e)
        {
            var fmt = captureFormat;
            if (fmt == null) return;

            int bytesPerSample = fmt.BitsPerSample / 8;
            int bytesPerFrame = bytesPerSample * fmt.Channels;
            if (bytesPerFrame <= 0) return;

            int frames = e.BytesRecorded / bytesPerFrame;

            for (int f = 0; f < frames; f++)
            {
                float sample = 0f;
                for (int c = 0; c < fmt.Channels; c++)
                    sample += ReadSample(e.Buffer, f * bytesPerFrame + c * bytesPerSample, fmt);
                sample /= Math.Max(1, fmt.Channels);

                lock (sampleLock)
                {
                    // Snapshot ring reference — the UI thread may replace ring[] under
                    // this same lock when FFT_SIZE changes, so work only through r.
                    var r = ring;
                    int fftSize = r.Length;

                    r[ringWritePos++] = sample;
                    if (ringWritePos >= fftSize) ringWritePos = 0;
                    if (ringFilled < fftSize) ringFilled++;
                    samplesSinceLastFrame++;
                    if (_isRecording) _recordSamplesCapured++;

                    var q = frameQueue;
                    if (ringFilled != fftSize || samplesSinceLastFrame < hopSize
                        || q == null || q.IsAddingCompleted) continue;

                    while (samplesSinceLastFrame >= hopSize)
                    {
                        samplesSinceLastFrame -= hopSize;
                        if (q.Count >= MAX_QUEUED_FRAMES) break;

                        float[] snap = ArrayPool<float>.Shared.Rent(fftSize);
                        int src = ringWritePos;
                        for (int i = 0; i < fftSize; i++)
                        {
                            snap[i] = r[src++];
                            if (src >= fftSize) src = 0;
                        }
                        try { q.Add(snap); } catch (Exception ex) { ArrayPool<float>.Shared.Return(snap, clearArray: false); RethrowUnexpectedException(ex); }
                    }
                }
            }
        }

        private static float ReadSample(byte[] buffer, int offset, WaveFormat fmt)
        {
            if (fmt.Encoding == WaveFormatEncoding.IeeeFloat && fmt.BitsPerSample == 32)
                return BitConverter.ToSingle(buffer, offset);

            if (fmt.Encoding == WaveFormatEncoding.Pcm)
            {
                return fmt.BitsPerSample switch
                {
                    16 => BitConverter.ToInt16(buffer, offset) / 32768f,
                    24 => Read24BitSample(buffer, offset),
                    32 => BitConverter.ToInt32(buffer, offset) / 2147483648f,
                    _ => 0f
                };
            }

            if (fmt.Encoding == WaveFormatEncoding.Extensible)
            {
                if (fmt.BitsPerSample == 32) return BitConverter.ToSingle(buffer, offset);
                if (fmt.BitsPerSample == 16) return BitConverter.ToInt16(buffer, offset) / 32768f;
            }
            return 0f;
        }

        private static float Read24BitSample(byte[] buffer, int offset)
        {
            int s = buffer[offset] | (buffer[offset + 1] << 8) | (buffer[offset + 2] << 16);
            if ((s & 0x800000) != 0) s |= unchecked((int)0xFF000000);
            return s / 8388608f;
        }

        // ====================================================================
        //  Worker
        // ====================================================================
        private void StartWorker()
        {
            StopWorker();
            frameQueue = new BlockingCollection<float[]>(MAX_QUEUED_FRAMES);
            workerRunning = true;

            worker = new Thread(() =>
            {
                try
                {
                    foreach (var frame in frameQueue.GetConsumingEnumerable())
                    {
                        try
                        {
                            if (!workerRunning) break;
                            RenderFrame(frame);
                        }
                        finally
                        {
                            ArrayPool<float>.Shared.Return(frame, clearArray: false);
                        }
                    }
                }
                catch (Exception ex) { RethrowUnexpectedException(ex); }
            })
            { IsBackground = true, Priority = ThreadPriority.Normal };

            worker.Start();
        }

        private void StopWorker()
        {
            workerRunning = false;
            var q = frameQueue;
            if (q != null) { try { q.CompleteAdding(); } catch (Exception ex) { RethrowUnexpectedException(ex); } }
            if (worker?.IsAlive == true) { try { worker.Join(); } catch (Exception ex) { RethrowUnexpectedException(ex); } }
            worker = null;
            if (q != null) { try { q.Dispose(); } catch (Exception ex) { RethrowUnexpectedException(ex); } }
            frameQueue = null;
        }

        // ====================================================================
        //  Frame pipeline
        // ====================================================================
        private void RenderFrame(float[] samples)
        {
            var fmt = captureFormat;
            if (fmt == null) return;

            // ── Inter-frame gap + queue depth (profiling) ─────────────────────
            long _t_entry = System.Diagnostics.Stopwatch.GetTimestamp();
            double ticksToMsLocal = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            if (_profLastFrameTick != 0)
                _profInterFrame += (_t_entry - _profLastFrameTick) * ticksToMsLocal;
            _profQueueDepthSum += frameQueue?.Count ?? 0;

            // ── Snapshot every shared array into locals FIRST ─────────────────
            // The UI thread may call ReallocateFrameBuffers() or RebuildWindows()
            // at any time. Reading each field exactly once, then working only
            // through locals, eliminates all torn-read / size-mismatch races.
            int N = FFT_SIZE;
            int half = N / 2;
            double fs = fmt.SampleRate;
            var X = _X;
            var Xd = _Xd;
            var Xt = _Xt;
            var Z = _Z;
            var mag = _mag;
            var fReassign = _fReassign;
            var tShiftCols = _tShiftCols;
            var fSm = _fSm;
            var tSm = _tSm;
            var hann_ = hann;
            var hannD_ = hannD;
            var hannT_ = hannT;
            double maxShift = MAX_COL_SHIFT;
            // Low-FFT blend parameters — snapshot once so the audio thread never races with UI.
            bool lowFftSnap = _lowFftEnabled;
            double crossoverHzSnap = LOW_FFT_CROSSOVER_HZ;
            double crossoverSemiSnap = LOW_FFT_CROSSOVER_SEMITONES;
            int nLowSnap = lowFftSnap ? Math.Max(FFT_SIZE_L, N) : 0;

            // The snapshot length equals RingSize = max(FFT_SIZE, FFT_SIZE_L when enabled).
            // Validate size: the snapshot must be at least N samples.
            if (samples.Length < N) return;
            if (X.Length != N) return;
            if (Xd.Length != N) return;
            if (Xt.Length != N) return;
            if (Z.Length != N) return;
            if (hann_.Length != N) return;
            if (hannD_.Length != N) return;
            if (hannT_.Length != N) return;
            if (mag.Length != half) return;
            if (fReassign.Length != half) return;
            if (tShiftCols.Length != half) return;
            if (fSm.Length != half) return;
            if (tSm.Length != half) return;

            // Primary FFT uses the most recent N samples (tail of the snapshot).
            int offset = samples.Length - N;

            _profSw.Restart();
            long t0 = _profSw.ElapsedTicks;

            // ── Windowing ────────────────────────────────────────────────────
            // X[n]  = hann[n] * s   (purely real, as before)
            // Z[n]  = hannD[n]*s + i*hannT[n]*s  (pack two real signals into one complex buffer)
            for (int n = 0; n < N; n++)
            {
                double s = samples[offset + n];
                X[n] = new Complex(s * hann_[n], 0.0);
                Z[n] = new Complex(s * hannD_[n], s * hannT_[n]);
            }
            long t1 = _profSw.ElapsedTicks;

            // ── FFT (2× instead of 3×) ───────────────────────────────────────
            // FFT(X) as before.
            // FFT(Z) gives us FFT(Xd_window) and FFT(Xt_window) packed together.
            // We unpack them below using the split-real (even/odd symmetry) trick.
            FFT(X);
            FFT(Z);
            long t2 = _profSw.ElapsedTicks;

            // ── Unpack FFT(Xd) and FFT(Xt) from the packed transform Z ───────
            // For real sequences a[n] and b[n] packed as z[n] = a[n] + i*b[n]:
            //   FFT(a)[k] = (Z[k] + conj(Z[N-k])) / 2
            //   FFT(b)[k] = (Z[k] - conj(Z[N-k])) / (2i)
            //             = (conj(Z[N-k]) - Z[k]) * i / 2
            // This is exact — no approximation.
            {
                // k=0: Z[0] = A[0] + i*B[0] (both real for DC bin)
                Complex z0 = Z[0];
                Xd[0] = new Complex(z0.Real, 0.0);
                Xt[0] = new Complex(z0.Imaginary, 0.0);

                for (int k = 1; k < half; k++)
                {
                    Complex zk = Z[k];
                    Complex znk = Z[N - k];
                    Complex conjZnk = new Complex(znk.Real, -znk.Imaginary);

                    // FFT(Xd_window)[k] = (Z[k] + conj(Z[N-k])) / 2
                    Xd[k] = new Complex(
                        (zk.Real + conjZnk.Real) * 0.5,
                        (zk.Imaginary + conjZnk.Imaginary) * 0.5);

                    // FFT(Xt_window)[k] = (Z[k] - conj(Z[N-k])) / (2i)
                    //                   = i * (conj(Z[N-k]) - Z[k]) / 2
                    double diffR = conjZnk.Real - zk.Real;
                    double diffI = conjZnk.Imaginary - zk.Imaginary;
                    Xt[k] = new Complex(
                        -diffI * 0.5,   // Re(i * diff / 2)
                         diffR * 0.5);  // Im(i * diff / 2)
                }

                // k=half (Nyquist): same as k=0 — purely real
                Complex zh = Z[half];
                Xd[half] = new Complex(zh.Real, 0.0);
                Xt[half] = new Complex(zh.Imaginary, 0.0);
            }

            // Apply frequency-domain filter after unpacking so the filter sees
            // Xd and Xt in their final unpacked form.
            ApplyFrequencyDomainFilter(X, Xd, Xt, fs);

            // ── Reassignment ─────────────────────────────────────────────────
            // ── Reassignment ─────────────────────────────────────────────────
            double frameMaxAll = 1e-12;
            const double INV_2PI = 1.0 / (2.0 * Math.PI);

            for (int k = 1; k < half; k++)
            {
                Complex x = X[k];
                double m = x.Magnitude;
                mag[k] = m;
                if (m > frameMaxAll) frameMaxAll = m;

                double fk = (double)k * fs / N;

                if (m > 1e-12)
                {
                    Complex ratioD = Xd[k] / x;
                    double fhat = fk - (fs * INV_2PI) * ratioD.Imaginary;
                    if (double.IsNaN(fhat) || double.IsInfinity(fhat)) fhat = fk;
                    fhat = Math.Clamp(fhat, 0.0, fs * 0.5);
                    fReassign[k] = fhat;

                    // Clamp the raw time-shift in *samples* before dividing by hopSize.
                    // This keeps tShiftCols well-behaved at any hop size including 1.
                    // maxShift ≈ N/4 (Hann window support); genuine tones produce tiny shifts.
                    double rawShift = -(Xt[k] / x).Real;
                    if (double.IsNaN(rawShift) || double.IsInfinity(rawShift)) rawShift = 0.0;
                    rawShift = Math.Clamp(rawShift, -maxShift, maxShift);
                    tShiftCols[k] = rawShift / hopSize;
                }
                else
                {
                    fReassign[k] = fk;
                    tShiftCols[k] = 0.0;
                }
            }
            long t3 = _profSw.ElapsedTicks;

            // ── Smoothing + visible max ───────────────────────────────────────

            fSm[0] = fReassign[0]; tSm[0] = tShiftCols[0];
            fSm[half - 1] = fReassign[half - 1]; tSm[half - 1] = tShiftCols[half - 1];

            for (int k = 1; k < half - 1; k++)
            {
                fSm[k] = 0.25 * fReassign[k - 1] + 0.5 * fReassign[k] + 0.25 * fReassign[k + 1];
                tSm[k] = 0.25 * tShiftCols[k - 1] + 0.5 * tShiftCols[k] + 0.25 * tShiftCols[k + 1];
            }

            // ── Low-freq FFT blend ────────────────────────────────────────────
            if (lowFftSnap
                && hannL?.Length == nLowSnap
                && RunLowFftReassignment(samples, fs, hopSize))
            {
                BlendLowFftReassignment(fSm, tSm, mag, half, fs, crossoverHzSnap, crossoverSemiSnap);
            }

            double frameMaxVisible = 1e-12;
            for (int k = 1; k < half; k++)
            {
                double fk = fSm[k];
                if (fk >= displayFmin && fk <= displayFmax && mag[k] > frameMaxVisible)
                    frameMaxVisible = mag[k];
            }
            // Store raw (pre-normalization) frame peak for the intensity meter.
            // Use a decayed EMA so the meter tracks signal level without spiking per-frame.
            // Convert FFT peak magnitude to an approximate linear signal amplitude.
            //
            // Raw FFT magnitudes scale with FFT size and the window, so they are not in a
            // useful 0..1 range for the tuner meter. For a Hann window, the coherent gain
            // is the average of the window, i.e. sum(hann)/N. Undo that so a strong tone
            // lands roughly in a normal amplitude range.
            double hannSum = 0.0;
            for (int i = 0; i < hann.Length; i++) hannSum += hann[i];
            double coherentGain = hannSum / N;

            // Approximate time-domain amplitude from FFT peak magnitude.
            // Clamp defensively so the UI meter has a sane source value.
            double meterAmplitude = frameMaxVisible > 0.0
                ? (2.0 * frameMaxVisible) / Math.Max(1e-12, hannSum)
                : 0.0;

            meterAmplitude = Math.Clamp(meterAmplitude, 0.0, 1.0);

            // Smoothed meter value for UI
            RawFrameIntensity = RawFrameIntensity * 0.90 + Math.Clamp((2.0 * frameMaxVisible) / Math.Max(1.0, hann.Sum()), 0.0, 1.0) * 0.10;
            long t4 = _profSw.ElapsedTicks;

            // Level normalization smoothing.
            // LEVEL_SMOOTH_EMA = 0: disabled — pass raw frame peak, re-normalized per frame (classic).
            // LEVEL_SMOOTH_EMA > 0: _smoothedMaxIntensity tracks the real peak via symmetric EMA.
            //   All peaks are normalized to _smoothedMaxIntensity — sounds louder than the
            //   reference just clamp at 1.
            double frameMaxForPeaks;
            if (_volumeLocked)
            {
                // Volume lock active: pin _smoothedMaxIntensity to the trackbar value,
                // bypassing auto-gain entirely.  Read volatile fields — never touch UI controls
                // from the audio thread.
                _smoothedMaxIntensity = VolumeLockValue;
                frameMaxForPeaks = _smoothedMaxIntensity;
            }
            else if (LEVEL_SMOOTH_EMA <= 0.0)
            {
                frameMaxForPeaks = frameMaxVisible;
            }
            else
            {
                _smoothedMaxIntensity += LEVEL_SMOOTH_EMA * (frameMaxVisible - _smoothedMaxIntensity);
                _smoothedMaxIntensity = Math.Max(_smoothedMaxIntensity, 1e-12);
                frameMaxForPeaks = _smoothedMaxIntensity;
            }

            // ── Peak extraction ───────────────────────────────────────────────
            ExtractPeaksParabolicFit(mag, fSm, tSm, frameMaxForPeaks, PEAK_MODE);
            long t5 = _profSw.ElapsedTicks;

            // ── Ridge tracking ────────────────────────────────────────────────
            UpdateRidges();
            long t6 = _profSw.ElapsedTicks;

            // ── Harmonic suppression ──────────────────────────────────────────
            CollapseHarmonicFamilies(HARMONIC_SUPPRESSION);
            long t7 = _profSw.ElapsedTicks;

            // ── Chord / tuner ─────────────────────────────────────────────────
            UpdateChordLabelFromRidges();
            long t8 = _profSw.ElapsedTicks;

            // ── Recording ────────────────────────────────────────────────────
            if (_isRecording) RecordRidgeFrame();

            // ── Accumulate + draw ─────────────────────────────────────────────
            long t9 = t8, t10 = t8;
            if (!scrollPaused)
            {
                AccumulateRidges();
                t9 = _profSw.ElapsedTicks;
                DrawRidgesColumn();
                t10 = _profSw.ElapsedTicks;
            }
            else
            {

            }

            // ── Accumulate profiling stats ────────────────────────────────────
            double ticksToMs = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            _profWindowing += (t1 - t0) * ticksToMs;
            _profFft += (t2 - t1) * ticksToMs;
            _profReassign += (t3 - t2) * ticksToMs;
            _profSmooth += (t4 - t3) * ticksToMs;
            _profPeaks += (t5 - t4) * ticksToMs;
            _profRidges += (t6 - t5) * ticksToMs;
            _profHarmonic += (t7 - t6) * ticksToMs;
            _profChord += (t8 - t7) * ticksToMs;
            _profAccum += (t9 - t8) * ticksToMs;
            _profDraw += (t10 - t9) * ticksToMs;
            _profTotal += (t10 - t0) * ticksToMs;
            _profLastFrameTick = t10;
            _profFrameCount++;

            // ── Live FPS readout ──────────────────────────────────────────────
            _fpsFrameCount++;
            long nowTick = System.Diagnostics.Stopwatch.GetTimestamp();
            if (_fpsWindowStart == 0) _fpsWindowStart = nowTick;
            double elapsedSec = (nowTick - _fpsWindowStart) / (double)System.Diagnostics.Stopwatch.Frequency;
            if (elapsedSec >= 1.0)
            {
                int fps2 = (int)Math.Round(_fpsFrameCount / elapsedSec);
                _fpsFrameCount = 0;
                _fpsWindowStart = nowTick;
                // Marshal to UI thread — lblFpsReadout is a WinForms control
                if (lblFpsReadout.IsHandleCreated && !lblFpsR.IsDisposed)
                    lblFpsReadout.BeginInvoke((Action)(() =>
                        lblFpsReadout.Text = $"{fps2}"));
            }
            
            if (_profFrameCount >= ProfInterval)
            {
                double n2 = _profFrameCount;
                double fps = n2 / (_profTotal / 1000.0);
                double avgGap = _profInterFrame / n2;
                double avgQ = _profQueueDepthSum / n2;
                System.Diagnostics.Debug.WriteLine(
                    $"[PROF {n2:0}fr | {fps:0.0} fps-cpu-capacity]  " +
                    $"Total={_profTotal / n2:0.00}ms  Gap={avgGap:0.00}ms  Q={avgQ:0.0}  " +
                    $"Win={_profWindowing / n2:0.00}  " +
                    $"FFT={_profFft / n2:0.00}  " +
                    $"Reass={_profReassign / n2:0.00}  " +
                    $"Smooth={_profSmooth / n2:0.00}  " +
                    $"Peaks={_profPeaks / n2:0.00}  " +
                    $"Ridges={_profRidges / n2:0.00}  " +
                    $"Harm={_profHarmonic / n2:0.00}  " +
                    $"Chord={_profChord / n2:0.00}  " +
                    $"Accum={_profAccum / n2:0.00}  " +
                    $"Draw={_profDraw / n2:0.00}ms");

                _profWindowing = _profFft = _profReassign = _profSmooth = 0;
                _profPeaks = _profRidges = _profHarmonic = _profChord = 0;
                _profAccum = _profDraw = _profTotal = 0;
                _profInterFrame = 0;
                _profQueueDepthSum = 0;
                _profFrameCount = 0;
            }
        }

        // ====================================================================
        //  Parabolic-fit peak extraction
        // ====================================================================
        private const double CentsPerGridBin = 5.0;

        private double[] _centsGridMag;
        private double[] _centsGridTsh;
        private double[] _centsGridHz;
        private double[] _centsGridW;
        private double[] _logMagScratch;
        private double[] _residualMag;
        private double[] _residualLinear;
        private int _centsGridSize;
        private double _centsGridFmin;
        private double _centsGridFmax;

        private const int MaxFitRadius = 16;

        private struct ParabolaFitResult
        {
            public double DeltaBins;
            public double PeakLogMag;
            public double HalfWidthCents;
            public double Score;
            public int BestR;
            public bool Valid;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ParabolaFitResult FitBestParabolaAtBin(
            double[] logMag, int g, int minR, int maxR, int gridSize)
        {
            double relBin = (g + 1.0) / Math.Max(1.0, gridSize);
            double freqScale = 1.0 + 0.4 * (0.5 - relBin);
            const double BasePenalty = 0.05;
            double penaltyPerBin = BasePenalty * freqScale;

            var best = new ParabolaFitResult { Score = double.NegativeInfinity, BestR = minR };

            for (int R = minR; R <= maxR; R++)
            {
                if (g - R < 0 || g + R >= gridSize) continue;

                int N = 2 * R + 1;
                double sumY = 0, sumXY = 0, sumX2Y = 0, sumX4 = 0, sumX2 = 0;
                for (int dx = -R; dx <= R; dx++)
                {
                    double y = logMag[g + dx];
                    double x2 = (double)(dx * dx);
                    sumY += y;
                    sumXY += dx * y;
                    sumX2Y += x2 * y;
                    sumX2 += x2;
                    sumX4 += x2 * x2;
                }

                double c = sumY / N;
                double denomB = sumX2;
                double denomA = sumX4 - sumX2 * sumX2 / N;
                if (Math.Abs(denomB) < 1e-12 || Math.Abs(denomA) < 1e-12) continue;

                double b = sumXY / denomB;
                double a = (sumX2Y - c * sumX2) / denomA;
                if (a >= -1e-12) continue;

                double delta = Math.Clamp(-b / (2.0 * a), -(double)R, (double)R);
                double vertexY = c - b * b / (4.0 * a);

                double sse = 0.0;
                for (int dx = -R; dx <= R; dx++)
                {
                    double err = logMag[g + dx] - (a * (dx - delta) * (dx - delta) + vertexY);
                    sse += err * err;
                }
                double rms = Math.Sqrt(sse / N);
                double score = -rms - penaltyPerBin * R;

                if (score > best.Score)
                {
                    double hwBins = Math.Sqrt(Math.Log(2.0) / (-2.0 * a));

                    best.DeltaBins = delta;
                    best.PeakLogMag = vertexY;
                    best.HalfWidthCents = hwBins * CentsPerGridBin;
                    best.Score = score;
                    best.BestR = R;
                    best.Valid = true;
                }
            }
            return best;
        }

        private void EnsureCentsGrid()
        {
            if (_centsGridHz != null &&
                _centsGridFmin == displayFmin &&
                _centsGridFmax == displayFmax)
                return;

            double totalCents = 1200.0 * Math.Log2(displayFmax / displayFmin);
            int size = Math.Max(4, (int)Math.Ceiling(totalCents / CentsPerGridBin) + 1);

            if (_centsGridHz == null || _centsGridHz.Length < size)
            {
                _centsGridHz = new double[size];
                _centsGridMag = new double[size];
                _centsGridTsh = new double[size];
            }

            for (int g = 0; g < size; g++)
                _centsGridHz[g] = displayFmin * Math.Pow(2.0, g * CentsPerGridBin / 1200.0);

            _centsGridSize = size;
            _centsGridFmin = displayFmin;
            _centsGridFmax = displayFmax;
        }

        private void ExtractPeaksParabolicFit(
            double[] mag, double[] fReassign, double[] tShiftCols,
            double frameMax, int fixedCount)
        {
            _peaksScratchCount = 0;

            int half = mag.Length;
            const double EPS = 1e-30;
            const double MaxColShiftDraw = 1.25;

            EnsureCentsGrid();
            int gridSize = _centsGridSize;
            var gridMag = _centsGridMag;
            var gridTsh = _centsGridTsh;
            var gridHz = _centsGridHz;

            for (int g = 0; g < gridSize; g++) { gridMag[g] = 0.0; gridTsh[g] = 0.0; }

            if (_centsGridW == null || _centsGridW.Length < gridSize)
                _centsGridW = new double[gridSize];
            else
                Array.Clear(_centsGridW, 0, gridSize);

            double fs_local = captureFormat?.SampleRate ?? 44100.0;
            double binHz_scatter = fs_local / (mag.Length * 2);

            for (int k = 1; k < half; k++)
            {
                double m = mag[k];
                if (m < 1e-30) continue;

                double fk = k * binHz_scatter;
                double f = fReassign[k];
                if (f < displayFmin || f > displayFmax) f = fk;
                if (f < displayFmin || f > displayFmax) continue;

                double cents = 1200.0 * Math.Log2(f / displayFmin);
                double gExact = cents / CentsPerGridBin;
                int g0 = (int)Math.Floor(gExact);
                int g1 = g0 + 1;
                double t = gExact - g0;

                if ((uint)g0 < (uint)gridSize)
                {
                    double w = m * (1.0 - t);
                    gridMag[g0] += w;
                    gridTsh[g0] += tShiftCols[k] * w;
                    _centsGridW[g0] += w;
                }
                if ((uint)g1 < (uint)gridSize)
                {
                    double w = m * t;
                    gridMag[g1] += w;
                    gridTsh[g1] += tShiftCols[k] * w;
                    _centsGridW[g1] += w;
                }
            }

            double frameMaxGrid = 1e-12;
            for (int g = 0; g < gridSize; g++)
            {
                double w = _centsGridW[g];
                gridTsh[g] = w > 1e-30 ? gridTsh[g] / w : 0.0;
                gridMag[g] = w;
                if (w > frameMaxGrid) frameMaxGrid = w;
            }

            // frameMaxGrid = current frame's actual peak, used only for the noise threshold.
            // frameMax     = smoothed reference. All peaks normalized to it; louder sounds clamp at 1.
            // When smoothing is disabled, frameMax == frameMaxGrid.
            double normRef = (LEVEL_SMOOTH_EMA <= 0.0 && !_volumeLocked) ? frameMaxGrid : Math.Max(frameMax, 1e-12);

            // Gap-fill
            for (int g = 0; g < gridSize;)
            {
                if (gridMag[g] > EPS) { g++; continue; }
                int left = g - 1;
                int right = g + 1;
                while (right < gridSize && gridMag[right] <= EPS) right++;
                if (left < 0 && right >= gridSize) { g = gridSize; }
                else if (left < 0) { for (int i = g; i < right; i++) gridMag[i] = gridMag[right]; }
                else if (right >= gridSize) { for (int i = g; i < gridSize; i++) gridMag[i] = gridMag[left]; }
                else
                {
                    double vL = gridMag[left], vR = gridMag[right];
                    int span = right - left;
                    for (int i = g; i < right; i++)
                        gridMag[i] = vL + (vR - vL) * (i - left) / span;
                }
                g = right + 1;
            }
            for (int g = 0; g < gridSize; g++)
                gridMag[g] = Math.Log(gridMag[g] + EPS);

            double minMagGrid = normRef * PEAK_MIN_REL;

            int minR = 1;
            int maxR = Math.Min(MaxFitRadius, gridSize / 4);

            if (fixedCount == 0)
            {
                for (int g = 1; g < gridSize - 1; g++)
                {
                    if (!(gridMag[g] > gridMag[g - 1] && gridMag[g] > gridMag[g + 1])) continue;
                    double mLin = Math.Exp(gridMag[g]);
                    if (mLin < minMagGrid) continue;

                    var fit = FitBestParabolaAtBin(gridMag, g, minR, maxR, gridSize);
                    if (!fit.Valid) continue;

                    double mPeak = Math.Exp(fit.PeakLogMag);
                    if (mPeak < minMagGrid) continue;

                    double gExact = g + fit.DeltaBins;
                    int gi0 = Math.Clamp((int)Math.Floor(gExact), 0, gridSize - 2);
                    double gt = gExact - gi0;
                    double f = gridHz[gi0] + gt * (gridHz[gi0 + 1] - gridHz[gi0]);
                    if (f < displayFmin || f > displayFmax) continue;

                    int R = fit.BestR;
                    double sumW = 0, tSum = 0;
                    for (int dx = -R; dx <= R; dx++)
                    {
                        double w = Math.Exp(gridMag[g + dx]);
                        sumW += w; tSum += gridTsh[g + dx] * w;
                    }
                    double tsh = sumW > 1e-30
                        ? Math.Clamp(tSum / sumW, -MaxColShiftDraw, MaxColShiftDraw)
                        : 0.0;

                    if (_peaksScratchCount >= _peaksScratch.Length)
                        Array.Resize(ref _peaksScratch, _peaksScratch.Length * 2);
                    _peaksScratch[_peaksScratchCount++] = new Peak(
                        f, Math.Log(f),
                        Clamp01(mPeak / normRef),
                        tsh,
                        fit.HalfWidthCents);
                }

                Array.Sort(_peaksScratch, 0, _peaksScratchCount, PeakDescendingIntensity.Instance);
                int maxThinned = MAX_PEAKS_PER_FRAME;
                if (_thinnedScratch == null || _thinnedScratch.Length < maxThinned)
                    _thinnedScratch = new Peak[maxThinned];
                int thinnedCount = 0;
                for (int i = 0; i < _peaksScratchCount && thinnedCount < maxThinned; i++)
                {
                    var p = _peaksScratch[i];
                    bool tooClose = false;
                    for (int j = 0; j < thinnedCount; j++)
                        if (Math.Abs(HzToCents(p.FreqHz, _thinnedScratch[j].FreqHz)) < PEAK_MIN_SPACING_CENTS)
                        { tooClose = true; break; }
                    if (!tooClose) _thinnedScratch[thinnedCount++] = p;
                }
                _peaksScratchCount = thinnedCount;
            }
            else
            {
                if (_residualMag == null || _residualMag.Length < gridSize)
                    _residualMag = new double[gridSize];
                if (_residualLinear == null || _residualLinear.Length < gridSize)
                    _residualLinear = new double[gridSize];

                for (int g = 0; g < gridSize; g++)
                {
                    _residualLinear[g] = Math.Exp(gridMag[g]);
                    _residualMag[g] = gridMag[g];
                }

                if (_thinnedScratch == null || _thinnedScratch.Length < fixedCount)
                    _thinnedScratch = new Peak[fixedCount];

                int found = 0;
                while (found < fixedCount)
                {
                    int bestG = -1;
                    double bestLin = minMagGrid;
                    for (int g = 1; g < gridSize - 1; g++)
                    {
                        if (_residualLinear[g] > bestLin)
                        { bestLin = _residualLinear[g]; bestG = g; }
                    }
                    if (bestG < 0) break;

                    var fit = FitBestParabolaAtBin(_residualMag, bestG, minR, maxR, gridSize);
                    if (!fit.Valid) { _residualLinear[bestG] = 0; _residualMag[bestG] = Math.Log(EPS); continue; }

                    double mPeak = Math.Exp(fit.PeakLogMag);
                    if (mPeak < minMagGrid) { _residualLinear[bestG] = 0; _residualMag[bestG] = Math.Log(EPS); continue; }

                    double gExact = bestG + fit.DeltaBins;
                    int gi0 = Math.Clamp((int)Math.Floor(gExact), 0, gridSize - 2);
                    double gt = gExact - gi0;
                    double f = gridHz[gi0] + gt * (gridHz[gi0 + 1] - gridHz[gi0]);

                    if (f >= displayFmin && f <= displayFmax)
                    {
                        int R = fit.BestR;
                        double sumW = 0, tSum = 0;
                        for (int dx = -R; dx <= R; dx++)
                        {
                            double w = Math.Exp(gridMag[bestG + dx]);
                            sumW += w; tSum += gridTsh[bestG + dx] * w;
                        }
                        double tsh = sumW > 1e-30
                            ? Math.Clamp(tSum / sumW, -MaxColShiftDraw, MaxColShiftDraw)
                            : 0.0;

                        _thinnedScratch[found++] = new Peak(
                            f, Math.Log(f),
                            Clamp01(mPeak / normRef),
                            tsh,
                            fit.HalfWidthCents);
                    }

                    double hwBins = fit.HalfWidthCents / CentsPerGridBin;
                    int subR = Math.Max(fit.BestR, (int)Math.Ceiling(hwBins * 3.0));
                    subR = Math.Min(subR, gridSize / 2);
                    double aCoeff = -Math.Log(2.0) / (2.0 * hwBins * hwBins);
                    for (int dx = -subR; dx <= subR; dx++)
                    {
                        int gg = bestG + dx;
                        if (gg < 0 || gg >= gridSize) continue;
                        double fittedLin = Math.Exp(fit.PeakLogMag + aCoeff * (dx - fit.DeltaBins) * (dx - fit.DeltaBins));
                        _residualLinear[gg] = Math.Max(0.0, _residualLinear[gg] - fittedLin);
                        _residualMag[gg] = Math.Log(Math.Max(EPS, _residualLinear[gg]));
                    }
                }
                _peaksScratchCount = found;
            }
        }

        // ====================================================================
        //  Frequency-domain filter
        // ====================================================================
        private void ApplyFrequencyDomainFilter(Complex[] X, Complex[] Xd, Complex[] Xt, double fs)
        {
            bool hpActive = HighPass > 0.0, lpActive = LowPass > 0.0;
            if (!hpActive && !lpActive) return;

            int N = X.Length;
            int half = N / 2;
            double binHz = fs / N;
            double rampHz = Math.Max(binHz * 3.0, 1.0);

            for (int k = 1; k < half; k++)
            {
                double fk = k * binHz, gain = 1.0;

                if (lpActive)
                {
                    double edge = LowPass;
                    if (fk >= edge + rampHz) gain = 0.0;
                    else if (fk > edge - rampHz)
                    {
                        double x = Math.Clamp((fk - (edge - rampHz)) / (2.0 * rampHz), 0.0, 1.0);
                        gain *= 0.5 * (1.0 + Math.Cos(Math.PI * x));
                    }
                }

                if (hpActive && gain > 0.0)
                {
                    double edge = HighPass;
                    if (fk <= edge - rampHz) gain = 0.0;
                    else if (fk < edge + rampHz)
                    {
                        double x = Math.Clamp((fk - (edge - rampHz)) / (2.0 * rampHz), 0.0, 1.0);
                        gain *= 0.5 * (1.0 - Math.Cos(Math.PI * x));
                    }
                }

                if (gain <= 0.0) X[k] = Xd[k] = Xt[k] = Complex.Zero;
                else if (gain < 1.0) { X[k] *= gain; Xd[k] *= gain; Xt[k] *= gain; }
            }
        }

        // ====================================================================
        //  Ridge linking — zero heap allocations per frame
        // ====================================================================
        private void UpdateRidges()
        {
            lock (ridgeLock)
            {
                int ridgeCount = ridges.Count;

                for (int i = 0; i < ridgeCount; i++)
                {
                    ridges[i].Miss++;
                    ridges[i].Age++;
                    ridges[i].MergeWidthBonus *= RIDGE_MERGE_WIDTH_DECAY;
                    ridges[i].DrawWidth = 1.0 + ridges[i].MergeWidthBonus;
                }

                if (_matched.Length < ridgeCount)
                    Array.Resize(ref _matched, ridgeCount * 2);
                Array.Clear(_matched, 0, ridgeCount);

                _candidateIdx.Clear();
                for (int i = 0; i < ridgeCount; i++)
                {
                    double f = Math.Exp(ridges[i].LogFreq + 0.5 * ridges[i].LogVel);
                    if (f >= displayFmin && f <= displayFmax) _candidateIdx.Add(i);
                }

                int rc = _candidateIdx.Count;
                if (_sortedIdx.Length < rc) Array.Resize(ref _sortedIdx, rc * 2);

                for (int i = 0; i < rc; i++) _sortedIdx[i] = _candidateIdx[i];

                Array.Sort(_sortedIdx, 0, rc, _ridgeLogFreqComparer);

                _newRidges.Clear();

                int peakCount = _peaksScratchCount;
                double gate = RIDGE_MATCH_LOGHZ * RIDGE_MATCH_LOGHZ_PRED_BOOST;

                for (int pi = 0; pi < peakCount; pi++)
                {
                    ref readonly Peak p = ref _thinnedScratch[pi];

                    int best = -1;
                    double bestD = double.MaxValue;

                    int lo = 0, hi = rc;
                    double searchKey = p.LogFreq - gate;
                    while (lo < hi)
                    {
                        int mid = (lo + hi) >> 1;
                        if (ridges[_sortedIdx[mid]].LogFreq + ridges[_sortedIdx[mid]].LogVel < searchKey)
                            lo = mid + 1;
                        else hi = mid;
                    }

                    for (int si = lo; si < rc; si++)
                    {
                        int i = _sortedIdx[si];
                        var r = ridges[i];
                        double predicted = r.LogFreq + r.LogVel;
                        if (predicted > p.LogFreq + gate) break;
                        if (_matched[i]) continue;

                        double d = Math.Abs(p.LogFreq - predicted);
                        if (d >= bestD) continue;
                        if (Math.Abs(HzToCents(p.FreqHz, Math.Exp(r.LogFreq))) > RIDGE_MAX_CENTS_JUMP) continue;
                        bestD = d; best = i;
                    }

                    if (best >= 0 && bestD <= gate)
                    {
                        var r = ridges[best];
                        double innov = p.LogFreq - (r.LogFreq + r.LogVel);
                        r.LogVel += RIDGE_VEL_EMA * innov;
                        r.LogFreq = (1.0 - RIDGE_FREQ_EMA) * r.LogFreq + RIDGE_FREQ_EMA * p.LogFreq;
                        r.Intensity = (1.0 - RIDGE_INTENSITY_EMA) * r.Intensity + RIDGE_INTENSITY_EMA * p.Intensity;
                        r.TimeShiftCols = p.TimeShiftCols;
                        r.Miss = 0;
                        if (p.WidthBins > 0.0)
                            r.ParabolaWidthBins = (r.ParabolaWidthBins <= 0.0)
                                ? p.WidthBins
                                : 0.7 * r.ParabolaWidthBins + 0.3 * p.WidthBins;
                        _matched[best] = true;
                    }
                    else
                    {
                        bool nearExisting = false;
                        for (int ci = 0; ci < rc; ci++)
                        {
                            if (Math.Abs(HzToCents(p.FreqHz, Math.Exp(ridges[_candidateIdx[ci]].LogFreq))) < RIDGE_MERGE_CENTS)
                            { nearExisting = true; break; }
                        }

                        if (!nearExisting)
                        {
                            var nr = RentRidge(nextRidgeId++, p.LogFreq, p.TimeShiftCols, p.Intensity * 0.35);
                            nr.ParabolaWidthBins = p.WidthBins;
                            _newRidges.Add(nr);
                        }
                    }
                }

                if (_newRidges.Count > 0) ridges.AddRange(_newRidges);

                // [OPT-10] Remove dying ridges without O(n) RemoveAt.
                int writeIdx = 0;
                for (int i = 0; i < ridges.Count; i++)
                {
                    if (ridges[i].Miss > RIDGE_MISS_MAX)
                    {
                        _ridgeById.Remove(ridges[i].Id);
                        ReturnRidge(ridges[i]);
                    }
                    else
                    {
                        if (writeIdx != i) ridges[writeIdx] = ridges[i];
                        writeIdx++;
                    }
                }
                int removeCount = ridges.Count - writeIdx;
                if (removeCount > 0) ridges.RemoveRange(writeIdx, removeCount);

                ridges.Sort(RidgeLogFreqRidgeComparer.Instance);

                // Merge near-duplicates
                for (int i = 0; i < ridges.Count - 1;)
                {
                    var a = ridges[i]; var b = ridges[i + 1];
                    if (Math.Abs(HzToCents(Math.Exp(a.LogFreq), Math.Exp(b.LogFreq))) <= RIDGE_MERGE_CENTS)
                    {
                        Ridge keep = (b.Intensity * 0.7 + Math.Min(1.0, b.Age / 12.0) * 0.3) >
                                     (a.Intensity * 0.7 + Math.Min(1.0, a.Age / 12.0) * 0.3) ? b : a;
                        Ridge drop = keep == a ? b : a;

                        double wa = keep.Intensity + 1e-6, wb = drop.Intensity + 1e-6, ws = wa + wb;
                        keep.LogFreq = (keep.LogFreq * wa + drop.LogFreq * wb) / ws;
                        keep.LogVel = (keep.LogVel * wa + drop.LogVel * wb) / ws;
                        keep.Intensity = Math.Min(1.0, keep.Intensity + drop.Intensity * RIDGE_MERGE_BRIGHTNESS_BOOST);
                        keep.Miss = Math.Min(keep.Miss, drop.Miss);
                        keep.Age = Math.Max(keep.Age, drop.Age);
                        keep.MergeWidthBonus = Math.Min(20.0, keep.MergeWidthBonus + drop.MergeWidthBonus + RIDGE_MERGE_WIDTH_ADD);
                        keep.DrawWidth = 1.0 + keep.MergeWidthBonus;
                        if (!keep.HasLastPos && drop.HasLastPos)
                        { keep.LastXf = drop.LastXf; keep.LastYf = drop.LastYf; keep.HasLastPos = true; }

                        _ridgeById.Remove(drop.Id);
                        ReturnRidge(drop);
                        int dropIdx = object.ReferenceEquals(drop, a) ? i : i + 1;
                        int keepIdx = object.ReferenceEquals(keep, a) ? i : i + 1;
                        ridges.RemoveAt(dropIdx);
                        if (i > 0) i--;
                        continue;
                    }
                    i++;
                }

                ridges.Sort(RidgeIntensityComparer.Instance);

                while (ridges.Count > MaxActiveRidges)
                {
                    int last = ridges.Count - 1;
                    _ridgeById.Remove(ridges[last].Id);
                    ReturnRidge(ridges[last]);
                    ridges.RemoveAt(last);
                }

                // Rebuild _ridgeById from the authoritative ridges list.
                _ridgeById.Clear();
                for (int i = 0; i < ridges.Count; i++)
                    _ridgeById[ridges[i].Id] = ridges[i];

                for (int i = 0; i < ridges.Count; i++)
                    ridges[i].RecordHistory();
            }
        }

        // ====================================================================
        //  Harmonic family collapse
        // ====================================================================
        private const double HarmOnsetWeight = 0.45;
        private const double HarmCorrWeight = 0.35;
        private const double HarmStrengthWeight = 0.20;
        private const int HarmOnsetWindow = 12;
        private const double HarmMinConfidence = 0.30;

        private void CollapseHarmonicFamilies(double strength = 0.65)
        {
            if (strength <= 0.0) return;
            strength = Math.Min(strength, 1.0);

            lock (ridgeLock)
            {
                int count = ridges.Count;
                if (count < 2) return;

                if (_suppressFactor.Length < count)
                    Array.Resize(ref _suppressFactor, count);
                Array.Clear(_suppressFactor, 0, count);

                if (_ridgeFreqCache.Length < count)
                    Array.Resize(ref _ridgeFreqCache, count);
                for (int i = 0; i < count; i++)
                    _ridgeFreqCache[i] = Math.Exp(ridges[i].LogFreq);

                for (int fi = 0; fi < count - 1; fi++)
                {
                    var fund = ridges[fi];
                    double fFreq = _ridgeFreqCache[fi];

                    for (int hi = fi + 1; hi < count; hi++)
                    {
                        var harm = ridges[hi];
                        if (harm.Intensity >= fund.Intensity) continue;

                        double hFreq = _ridgeFreqCache[hi];

                        if (!AreHarmonicallyRelated(fFreq, hFreq, HARMONIC_FAMILY_CENTS_TOL, HARMONIC_FAMILY_MAX_RATIO))
                            continue;

                        double ratio = Math.Max(fFreq, hFreq) / Math.Min(fFreq, hFreq);
                        double nearest = Math.Max(1.0, Math.Round(ratio));
                        double freqSnap = Math.Max(0.0,
                            1.0 - Math.Abs(HzToCents(Math.Max(fFreq, hFreq),
                                           Math.Min(fFreq, hFreq) * nearest))
                                  / Math.Max(1.0, HARMONIC_FAMILY_CENTS_TOL));

                        double onsetScore;
                        int minAge = Math.Min(fund.Age, harm.Age);
                        if (minAge < 4)
                        {
                            onsetScore = 1.0;
                        }
                        else
                        {
                            double ageDiff = Math.Abs(fund.Age - harm.Age);
                            onsetScore = Math.Max(0.0, 1.0 - ageDiff / HarmOnsetWindow);
                        }

                        double corrScore = 0.5;
                        int histLen = Ridge.IntensityHistoryLen;
                        int validFrames = Math.Min(fund.Age + 1, Math.Min(harm.Age + 1, histLen));
                        if (validFrames >= 8)
                        {
                            double sumF = 0, sumH = 0, sumFF = 0, sumHH = 0, sumFH = 0;
                            int fHead = fund.HistoryHead;
                            int hHead = harm.HistoryHead;
                            for (int t = 0; t < validFrames; t++)
                            {
                                int fi2 = (fHead - 1 - t + histLen) % histLen;
                                int hi2 = (hHead - 1 - t + histLen) % histLen;
                                double fv = fund.IntensityHistory[fi2];
                                double hv = harm.IntensityHistory[hi2];
                                sumF += fv; sumH += hv;
                                sumFF += fv * fv; sumHH += hv * hv;
                                sumFH += fv * hv;
                            }
                            double n = validFrames;
                            double varF = sumFF - sumF * sumF / n;
                            double varH = sumHH - sumH * sumH / n;
                            if (varF > 1e-9 && varH > 1e-9)
                            {
                                double r = (sumFH - sumF * sumH / n) / Math.Sqrt(varF * varH);
                                corrScore = Math.Max(0.0, r);
                            }
                        }

                        double expectedRelative = 1.0 / Math.Max(1.0, nearest);
                        double actualRelative = harm.Intensity / Math.Max(1e-6, fund.Intensity);
                        double strengthScore = Math.Max(0.0,
                            1.0 - Math.Max(0.0, actualRelative - expectedRelative) / Math.Max(1e-6, expectedRelative));

                        double confidence = HarmOnsetWeight * onsetScore
                                          + HarmCorrWeight * corrScore
                                          + HarmStrengthWeight * strengthScore;

                        if (confidence < HarmMinConfidence) continue;

                        double suppress = strength * confidence * freqSnap;
                        if (suppress > _suppressFactor[hi]) _suppressFactor[hi] = suppress;
                    }
                }

                const double MinIntensity = 0.01;
                int writeIdx = 0;
                for (int i = 0; i < count; i++)
                {
                    if (_suppressFactor[i] > 0.0)
                        ridges[i].Intensity *= 1.0 - _suppressFactor[i];

                    if (ridges[i].Intensity < MinIntensity)
                    {
                        _ridgeById.Remove(ridges[i].Id);
                        ReturnRidge(ridges[i]);
                    }
                    else
                    {
                        if (writeIdx != i) ridges[writeIdx] = ridges[i];
                        writeIdx++;
                    }
                }
                int removeCount = count - writeIdx;
                if (removeCount > 0) ridges.RemoveRange(writeIdx, removeCount);
            }
        }

        private static bool AreHarmonicallyRelated(double f1, double f2, double centsTol, double maxRatio)
        {
            if (f1 <= 0 || f2 <= 0) return false;
            double ratio = Math.Max(f1, f2) / Math.Min(f1, f2);
            if (ratio > maxRatio) return false;
            double nearest = Math.Round(ratio);
            if (nearest < 1.0) return false;
            return Math.Abs(HzToCents(Math.Max(f1, f2), Math.Min(f1, f2) * nearest)) <= centsTol;
        }

        // ====================================================================
        //  Sub-pixel plotting
        // ====================================================================
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void PlotSubPixelXY(
            byte* basePtr, int stride, int w, int h,
            double x, double y, byte r, byte g, byte b, double alphaScale = 1.0)
        {
            if (x < 0.0 || x > w - 1 || y < 0.0 || y > h - 1) return;
            int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
            int x1 = x0 + 1, y1 = y0 + 1;
            double tx = x - x0, ty = y - y0;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void write(int xx, int yy, double ww)
            {
                if ((uint)xx >= (uint)w || (uint)yy >= (uint)h) return;
                int rv = (int)(r * ww), gv = (int)(g * ww), bv = (int)(b * ww);
                if (rv <= 0 && gv <= 0 && bv <= 0) return;
                byte* px = basePtr + yy * stride + xx * 4;
                if (bv > px[0]) px[0] = (byte)bv;
                if (gv > px[1]) px[1] = (byte)gv;
                if (rv > px[2]) px[2] = (byte)rv;
                px[3] = 255;
            }

            write(x0, y0, (1.0 - tx) * (1.0 - ty) * alphaScale);
            write(x1, y0, tx * (1.0 - ty) * alphaScale);
            write(x0, y1, (1.0 - tx) * ty * alphaScale);
            write(x1, y1, tx * ty * alphaScale);
        }

        private (byte R, byte G, byte B) PitchColor(double freqHz, double brightness)
        {
            if (freqHz <= 0.0 || double.IsNaN(freqHz) || double.IsInfinity(freqHz)) return (0, 0, 0);
            double midi = 69.0 + 12.0 * Math.Log2(freqHz / tuning);
            double pitchClass = ((midi % 12.0) + 12.0) % 12.0;
            double dToBWrapped = pitchClass < 2.0 ? pitchClass + 12.0 : pitchClass;
            double hue = dToBWrapped switch
            {
                < 7.0 => 120.0 * (dToBWrapped - 2.0) / 5.0,
                < 11.0 => 120.0 + 120.0 * (dToBWrapped - 7.0) / 4.0,
                _ => 240.0 + 120.0 * (dToBWrapped - 11.0) / 3.0
            };

            hue = pitchClass * 30.0 - 60.0;

            hue = ((hue % 360.0) + 360.0) % 360.0;
            double blueBoost = HueRegionWeight(hue, 240.0, 70.0);
            return HsvToRgb(hue, 1.0 - blueBoost * 0.4, Clamp01(brightness));
        }

        private static double HueRegionWeight(double hue, double center, double halfWidth)
        {
            double delta = Math.Abs(((hue % 360.0) + 360.0) % 360.0 - ((center % 360.0) + 360.0) % 360.0);
            if (delta > 180.0) delta = 360.0 - delta;
            return delta >= halfWidth ? 0.0 : 1.0 - delta / halfWidth;
        }

        private static (byte R, byte G, byte B) HsvToRgb(double hue, double sat, double val)
        {
            sat = Clamp01(sat); val = Clamp01(val);
            double c = val * sat, hp = ((hue % 360.0) + 360.0) % 360.0 / 60.0;
            double x = c * (1.0 - Math.Abs(hp % 2.0 - 1.0));
            (double r1, double g1, double b1) = (int)hp switch
            {
                0 => (c, x, 0.0),
                1 => (x, c, 0.0),
                2 => (0.0, c, x),
                3 => (0.0, x, c),
                4 => (x, 0.0, c),
                _ => (c, 0.0, x)
            };
            double m = val - c;
            return ((byte)Math.Round((r1 + m) * 255.0),
                    (byte)Math.Round((g1 + m) * 255.0),
                    (byte)Math.Round((b1 + m) * 255.0));
        }

        // ====================================================================
        //  Ridge accumulator for merged-frame display
        // ====================================================================
        private void AccumulateRidges()
        {
            lock (ridgeLock)
            {
                for (int ri = 0; ri < ridges.Count; ri++)
                {
                    var r = ridges[ri];
                    double w = r.Intensity;

                    if (_ridgeAccumIndex.TryGetValue(r.Id, out int slot))
                    {
                        ref var a = ref _ridgeAccum[slot];
                        if (r.Intensity > a.Intensity) a.Intensity = r.Intensity;
                        if (r.Miss < a.Miss) a.Miss = r.Miss;
                        if (r.DrawWidth > a.DrawWidth) a.DrawWidth = r.DrawWidth;
                        a.LogFreq += r.LogFreq * w;
                        a.TimeShiftCols += r.TimeShiftCols * w;
                        a.LogVel = r.LogVel;
                        a.WeightSum += w;
                    }
                    else
                    {
                        if (_ridgeAccumCount >= _ridgeAccum.Length)
                            Array.Resize(ref _ridgeAccum, _ridgeAccum.Length * 2);

                        slot = _ridgeAccumCount++;
                        _ridgeAccumIndex[r.Id] = slot;
                        _ridgeAccum[slot] = new RidgeAccum
                        {
                            Id = r.Id,
                            LogFreq = r.LogFreq * w,
                            LogVel = r.LogVel,
                            TimeShiftCols = r.TimeShiftCols * w,
                            Intensity = r.Intensity,
                            Miss = r.Miss,
                            WeightSum = w,
                            DrawWidth = r.DrawWidth
                        };
                    }
                }
            }
        }

        // ====================================================================
        //  Draw ridges column
        // ====================================================================
        private struct DrawEntry
        {
            public double YNow, XNow, LastYf, LastXf;
            public byte CR, CG, CB;
            public double Alpha;
            public bool ShouldConnect;
            public double DrawWidth;
        }

        private void DrawRidgesColumn()
        {
            if (captureFormat == null) return;

            int w, h;

            lock (bitmapLock)
            {
                if (spectrogramBitmap == null) return;
                w = spectrogramBitmap.Width;
                h = spectrogramBitmap.Height;
                if (w < 2 || h < 2) return;

                if (chordCols == null || chordCols.Length != w) InitChordCols(w);

                // Advance write pointer before writing so _bitmapWriteCol always points
                // at the column that represents "now" (rightmost in logical display order).
                _bitmapWriteCol = (_bitmapWriteCol + 1) % w;
                int writeCol = _bitmapWriteCol;

                BitmapData bd = spectrogramBitmap.LockBits(
                    new Rectangle(0, 0, w, h),
                    ImageLockMode.ReadWrite,
                    PixelFormat.Format32bppArgb);

                try
                {
                    unsafe
                    {
                        byte* basePtr = (byte*)bd.Scan0;
                        int stride = bd.Stride;

                        // Clear only the write column — no full-bitmap memcpy needed.
                        int xOff = writeCol * 4;
                        for (int y = 0; y < h; y++)
                        {
                            byte* px = basePtr + y * stride + xOff;
                            px[0] = px[1] = px[2] = 0; px[3] = 255;
                        }

                        double logMin = Math.Log(displayFmin), logMax = Math.Log(displayFmax);
                        double invLog = 1.0 / (logMax - logMin);

                        int drawCount = 0;
                        if (_drawList.Length < _ridgeAccumCount)
                            _drawList = new DrawEntry[Math.Max(_ridgeAccumCount, MaxActiveRidges)];

                        double drawMaxIntensity = 1e-12;
                        if (LEVEL_SMOOTH_EMA <= 0.0)
                        {
                            for (int ai = 0; ai < _ridgeAccumCount; ai++)
                                if (_ridgeAccum[ai].Intensity > drawMaxIntensity)
                                    drawMaxIntensity = _ridgeAccum[ai].Intensity;
                        }
                        else
                        {
                            drawMaxIntensity = 1.0;
                        }

                        lock (ridgeLock)
                        {
                            for (int ai = 0; ai < _ridgeAccumCount; ai++)
                            {
                                ref var a = ref _ridgeAccum[ai];

                                double ws = a.WeightSum > 0 ? a.WeightSum : 1.0;
                                double freq = Math.Exp((a.LogFreq / ws) + 0.5 * a.LogVel);
                                double tsh = a.TimeShiftCols / ws;

                                if (freq < displayFmin || freq > displayFmax) continue;

                                double yFrac = (Math.Log(freq) - logMin) * invLog;
                                double yNowF = (h - 1) - yFrac * (h - 1);
                                if (yNowF < 0.0 || yNowF > h - 1) continue;

                                // X in circular bitmap coordinates:
                                // writeCol is "now"; time-reassignment shifts left (into the past).
                                // Use modulo to wrap cleanly; tsh is typically small so one mod suffices.
                                double xNow = ((writeCol - tsh) % w + w) % w;

                                double alpha = 1.0;
                                if (a.Miss > 0)
                                {
                                    int missIdx = Math.Min(a.Miss, _missFadeTable.Length - 1);
                                    alpha = _missFadeTable[missIdx];
                                    if (alpha < MinDrawAlpha) continue;
                                }

                                double v = Math.Pow(Clamp01(a.Intensity / drawMaxIntensity), PEAK_GAMMA);
                                var (cr, cg, cb) = PitchColor(freq, v);

                                Ridge liveRidge = null;
                                _ridgeById.TryGetValue(a.Id, out liveRidge);

                                bool hasLast = liveRidge?.HasLastPos ?? false;
                                double lastYF = hasLast ? liveRidge.LastYf : yNowF;
                                // LastXf is stored in circular bitmap coords from the previous frame.
                                double lastXF = hasLast ? liveRidge.LastXf : xNow;

                                if (liveRidge != null)
                                {
                                    liveRidge.LastYf = yNowF;
                                    liveRidge.LastXf = xNow;
                                    liveRidge.HasLastPos = true;
                                }

                                ref var de = ref _drawList[drawCount++];
                                de.YNow = yNowF; de.XNow = xNow;
                                de.LastYf = lastYF; de.LastXf = lastXF;
                                de.CR = cr; de.CG = cg; de.CB = cb;
                                de.Alpha = alpha;
                                de.ShouldConnect = hasLast && a.Miss <= 3;
                                de.DrawWidth = 1 + a.DrawWidth;
                            }
                        }

                        // Reset accumulator for next frame window
                        _ridgeAccumCount = 0;
                        _ridgeAccumIndex.Clear();

                        // Draw
                        for (int di = 0; di < drawCount; di++)
                        {
                            ref var de = ref _drawList[di];
                            int drawW = (int)Math.Round(de.DrawWidth);
                            if (drawW < 1) drawW = 1;

                            if (de.ShouldConnect)
                            {
                                double dx = de.XNow - de.LastXf;
                                double dy = de.YNow - de.LastYf;

                                // Skip connectors that cross the circular wrap boundary —
                                // they would draw a streak across the full bitmap width.
                                if (Math.Abs(dx) > w / 2) goto drawDot;
                                if (Math.Abs(dy) > 1) goto drawDot;

                                int steps = Math.Max(12, (int)Math.Ceiling(Math.Sqrt(dx * dx + dy * dy) * 2.0));
                                for (int s = 0; s <= steps; s++)
                                {
                                    double t = (double)s / steps;
                                    double xx = de.LastXf + t * dx, yy = de.LastYf + t * dy;
                                    for (int ow = -(drawW - 1); ow <= (drawW - 1); ow++)
                                        PlotSubPixelXY(basePtr, stride, w, h, xx, yy + ow, de.CR, de.CG, de.CB, de.Alpha);
                                }
                                continue;
                            }

                        drawDot:
                            for (int ow = -(drawW - 1); ow <= (drawW - 1); ow++)
                                PlotSubPixelXY(basePtr, stride, w, h, de.XNow, de.YNow + ow, de.CR, de.CG, de.CB, de.Alpha);
                        }
                    }
                }
                finally
                {
                    spectrogramBitmap.UnlockBits(bd);
                }

                // Update chord columns — these stay linear (newest at index [w-1]).
                if (chordCols?.Length == w)
                {
                    Array.Copy(chordCols, 1, chordCols, 0, w - 1);
                    chordCols[w - 1] = string.IsNullOrWhiteSpace(lastDetectedChordText) ? "—" : lastDetectedChordText;
                }
                if (canonicalCols?.Length == w)
                {
                    Array.Copy(canonicalCols, 1, canonicalCols, 0, w - 1);
                    canonicalCols[w - 1] = lastDetectedCanonicalText ?? "";
                }
                if (detectedNotesCols?.Length == w)
                {
                    Array.Copy(detectedNotesCols, 1, detectedNotesCols, 0, w - 1);
                    detectedNotesCols[w - 1] = lastDetectedNotesText ?? "";
                }
                if (harmonicsCols?.Length == w)
                {
                    Array.Copy(harmonicsCols, 1, harmonicsCols, 0, w - 1);
                    harmonicsCols[w - 1] = lastDetectedHarmonicsText ?? "";
                }
                if (tunerFreqCols?.Length == w)
                {
                    Array.Copy(tunerFreqCols, 1, tunerFreqCols, 0, w - 1);
                    tunerFreqCols[w - 1] = TunerFreqHz;
                }
                if (tunerIntensityCols?.Length == w)
                {
                    Array.Copy(tunerIntensityCols, 1, tunerIntensityCols, 0, w - 1);
                    tunerIntensityCols[w - 1] = RawFrameIntensity;
                }
            }

            if (UiThrottleReady(ref _lastUiInvalidateTick) && !pic.IsDisposed && pic.IsHandleCreated)
                try { pic.BeginInvoke((Action)pic.Invalidate); } catch (Exception ex) { RethrowUnexpectedException(ex); }
        }

        private Font _chordOverlayFont;
        private Font _chordOverlayFontQuality;
        private const int ChordOverlayY = 2;
        private const int ChordTextSpacing = 40; // doubled frequency (was 80)

        // ====================================================================
        //  Chord detection
        // ====================================================================
        private void UpdateChordLabelFromRidges()
        {
            _tonesCount = 0;
            double strongestIntensity = 0.0;
            double tunerCandidateLogFreq = 0.0;
            lock (ridgeLock)
            {
                int ridgesToUse = CHORD_RIDGES > 0 ? Math.Min(CHORD_RIDGES, ridges.Count) : ridges.Count;
                for (int i = 0; i < ridgesToUse; i++)
                {
                    var r = ridges[i];
                    double f = Math.Exp(r.LogFreq + 0.5 * r.LogVel);
                    if (f < displayFmin || f > displayFmax) continue;
                    double w = Clamp01(r.Intensity);
                    if (w < 0.01) continue;
                    double ageBoost = 1.0 - Math.Pow(RidgeAgeDecay, Math.Min(24, r.Age));
                    w *= 0.65 + 0.35 * ageBoost;
                    if (_tonesCount < _tones.Length) _tones[_tonesCount++] = (f, w);
                }

                double peakIntensity = 0.0;
                for (int i = 0; i < ridges.Count; i++)
                {
                    var r = ridges[i];
                    if (r.Miss > 2) continue;
                    double f = Math.Exp(r.LogFreq + 0.5 * r.LogVel);
                    if (f < TunerMinHz) continue;
                    if (f < displayFmin || f > displayFmax) continue;
                    if (r.Intensity > peakIntensity) peakIntensity = r.Intensity;
                }

                if (peakIntensity > 0.05)
                {
                    bool haveLock = _tunerLogFreqEma > 0.0;
                    double proximityTolLog = TunerCandidateProximityCents * Math.Log(2.0) / 1200.0;
                    double harmonicTolLog = TunerHarmonicMatchCents * Math.Log(2.0) / 1200.0;

                    Span<double> ridgeLogFreqs = stackalloc double[Math.Min(ridges.Count, MaxActiveRidges)];
                    Span<double> ridgeIntensities = stackalloc double[Math.Min(ridges.Count, MaxActiveRidges)];
                    Span<int> ridgeAges = stackalloc int[Math.Min(ridges.Count, MaxActiveRidges)];
                    int ridgeCount = 0;

                    for (int i = 0; i < ridges.Count && ridgeCount < ridgeLogFreqs.Length; i++)
                    {
                        var r = ridges[i];
                        if (r.Miss > 2) continue;
                        if (r.Intensity < peakIntensity * TunerFundamentalRelativeFloor) continue;

                        double lf = r.LogFreq + 0.5 * r.LogVel;
                        double f = Math.Exp(lf);
                        if (f < TunerMinHz) continue;
                        if (f < displayFmin || f > displayFmax) continue;

                        ridgeLogFreqs[ridgeCount] = lf;
                        ridgeIntensities[ridgeCount] = r.Intensity;
                        ridgeAges[ridgeCount] = r.Age;
                        ridgeCount++;
                    }

                    if (ridgeCount > 0)
                    {
                        Span<double> candidateLogFreqs = stackalloc double[Math.Min(ridgeCount * TunerFundamentalHypothesisMax, MaxActiveRidges * TunerFundamentalHypothesisMax)];
                        int candidateCount = 0;

                        for (int i = 0; i < ridgeCount; i++)
                        {
                            double ridgeLf = ridgeLogFreqs[i];
                            for (int h = 1; h <= TunerFundamentalHypothesisMax; h++)
                            {
                                double candLf = ridgeLf - Math.Log(h);
                                double candHz = Math.Exp(candLf);
                                if (candHz < TunerMinHz || candHz < displayFmin || candHz > displayFmax) continue;

                                bool duplicate = false;
                                for (int j = 0; j < candidateCount; j++)
                                {
                                    if (Math.Abs(candidateLogFreqs[j] - candLf) <= harmonicTolLog)
                                    {
                                        duplicate = true;
                                        break;
                                    }
                                }
                                if (!duplicate)
                                    candidateLogFreqs[candidateCount++] = candLf;
                            }
                        }

                        double bestScore = double.NegativeInfinity;
                        double bestLogFreq = 0.0;
                        double bestDirectSupport = 0.0;

                        for (int i = 0; i < candidateCount; i++)
                        {
                            double candLf = candidateLogFreqs[i];
                            double candHz = Math.Exp(candLf);
                            double score = 0.0;
                            double directSupport = 0.0;
                            double harmonicSupport = 0.0;
                            int matchedHarmonics = 0;

                            for (int j = 0; j < ridgeCount; j++)
                            {
                                double ratio = Math.Exp(ridgeLogFreqs[j] - candLf);
                                if (ratio < 0.90 || ratio > TunerHarmonicSupportMax + 0.35) continue;

                                int harmonic = (int)Math.Round(ratio);
                                if (harmonic < 1 || harmonic > TunerHarmonicSupportMax) continue;

                                double harmonicLf = candLf + Math.Log(harmonic);
                                double diffLog = Math.Abs(ridgeLogFreqs[j] - harmonicLf);
                                if (diffLog > harmonicTolLog) continue;

                                double ageBoost = 1.0 + 0.05 * Math.Min(10, ridgeAges[j]);
                                double weight = ridgeIntensities[j] * ageBoost / Math.Pow(harmonic, 0.65);
                                score += weight;
                                if (harmonic == 1)
                                    directSupport = Math.Max(directSupport, ridgeIntensities[j] * ageBoost);
                                else
                                    harmonicSupport += weight;
                                matchedHarmonics++;
                            }

                            if (matchedHarmonics == 0) continue;
                            if (directSupport <= 0.0 && harmonicSupport < peakIntensity * 0.12) continue;

                            score += 0.55 * directSupport;
                            score += 0.18 * Math.Max(0.0, Math.Log(220.0 / candHz, 2.0));

                            if (haveLock)
                            {
                                double diffLog = Math.Abs(candLf - _tunerLogFreqEma);
                                double proximity = Math.Exp(-0.5 * Math.Pow(diffLog / proximityTolLog, 2.0));
                                score *= 0.65 + 0.85 * proximity;
                            }

                            if (score > bestScore)
                            {
                                bestScore = score;
                                bestLogFreq = candLf;
                                bestDirectSupport = directSupport;
                            }
                        }

                        if (bestScore > double.NegativeInfinity)
                        {
                            double lowestKeptLogFreq = bestLogFreq;
                            double lowestKeptDirectSupport = bestDirectSupport;

                            for (int i = 0; i < candidateCount; i++)
                            {
                                double candLf = candidateLogFreqs[i];
                                if (candLf >= lowestKeptLogFreq) continue;

                                double candHz = Math.Exp(candLf);
                                double score = 0.0;
                                double directSupport = 0.0;
                                double harmonicSupport = 0.0;
                                int matchedHarmonics = 0;

                                for (int j = 0; j < ridgeCount; j++)
                                {
                                    double ratio = Math.Exp(ridgeLogFreqs[j] - candLf);
                                    if (ratio < 0.90 || ratio > TunerHarmonicSupportMax + 0.35) continue;

                                    int harmonic = (int)Math.Round(ratio);
                                    if (harmonic < 1 || harmonic > TunerHarmonicSupportMax) continue;

                                    double harmonicLf = candLf + Math.Log(harmonic);
                                    double diffLog = Math.Abs(ridgeLogFreqs[j] - harmonicLf);
                                    if (diffLog > harmonicTolLog) continue;

                                    double ageBoost = 1.0 + 0.05 * Math.Min(10, ridgeAges[j]);
                                    double weight = ridgeIntensities[j] * ageBoost / Math.Pow(harmonic, 0.65);
                                    score += weight;
                                    if (harmonic == 1)
                                        directSupport = Math.Max(directSupport, ridgeIntensities[j] * ageBoost);
                                    else
                                        harmonicSupport += weight;
                                    matchedHarmonics++;
                                }

                                if (matchedHarmonics == 0) continue;
                                if (directSupport <= 0.0 && harmonicSupport < peakIntensity * 0.12) continue;

                                score += 0.55 * directSupport;
                                score += 0.18 * Math.Max(0.0, Math.Log(220.0 / candHz, 2.0));

                                if (haveLock)
                                {
                                    double diffLog = Math.Abs(candLf - _tunerLogFreqEma);
                                    double proximity = Math.Exp(-0.5 * Math.Pow(diffLog / proximityTolLog, 2.0));
                                    score *= 0.65 + 0.85 * proximity;
                                }

                                if (score >= bestScore * TunerFundamentalKeepScoreRatio)
                                {
                                    lowestKeptLogFreq = candLf;
                                    lowestKeptDirectSupport = directSupport;
                                }
                            }

                            tunerCandidateLogFreq = lowestKeptLogFreq;
                            strongestIntensity = lowestKeptDirectSupport > 0.0 ? lowestKeptDirectSupport : peakIntensity;
                        }
                    }
                }
            }

            const double TunerAlpha = 2.0 / (TunerAvgFrames + 1);
            if (tunerCandidateLogFreq > 0.0)
            {
                _tunerSignalHold = TunerSignalHoldFrames;

                if (_tunerLogFreqEma <= 0.0)
                {
                    _tunerLogFreqEma = tunerCandidateLogFreq;
                    _tunerPendingLogFreq = 0.0;
                    _tunerPendingFrames = 0;
                }
                else
                {
                    double centsDiff = Math.Abs(1200.0 * (tunerCandidateLogFreq - _tunerLogFreqEma) / Math.Log(2.0));
                    if (centsDiff <= TunerSwitchConfirmCents)
                    {
                        _tunerPendingLogFreq = 0.0;
                        _tunerPendingFrames = 0;
                        _tunerLogFreqEma += TunerAlpha * (tunerCandidateLogFreq - _tunerLogFreqEma);
                    }
                    else
                    {
                        double pendingCents = _tunerPendingLogFreq > 0.0
                            ? Math.Abs(1200.0 * (tunerCandidateLogFreq - _tunerPendingLogFreq) / Math.Log(2.0))
                            : double.MaxValue;

                        if (_tunerPendingLogFreq <= 0.0 || pendingCents > TunerSwitchConfirmCents)
                        {
                            _tunerPendingLogFreq = tunerCandidateLogFreq;
                            _tunerPendingFrames = 1;
                        }
                        else
                        {
                            _tunerPendingFrames++;
                        }

                        if (centsDiff > TunerJumpResetCents || _tunerPendingFrames >= TunerSwitchConfirmFrames)
                        {
                            double snapBlend = centsDiff > TunerJumpResetCents ? 1.0 : 0.55;
                            _tunerLogFreqEma += snapBlend * (_tunerPendingLogFreq - _tunerLogFreqEma);
                            _tunerPendingLogFreq = 0.0;
                            _tunerPendingFrames = 0;
                        }
                    }
                }

                TunerFreqHz = Math.Exp(_tunerLogFreqEma);
                _tunerSignalStrength += 0.18 * (strongestIntensity - _tunerSignalStrength);
            }
            else
            {
                if (_tunerSignalHold > 0)
                {
                    _tunerSignalHold--;
                    _tunerPendingLogFreq = 0.0;
                    _tunerPendingFrames = 0;
                }
                else
                {
                    _tunerLogFreqEma *= 0.92;
                    if (_tunerLogFreqEma < 0.01) _tunerLogFreqEma = 0.0;
                    _tunerPendingLogFreq = 0.0;
                    _tunerPendingFrames = 0;
                }

                TunerFreqHz = _tunerLogFreqEma > 0.0 ? Math.Exp(_tunerLogFreqEma) : 0.0;
                _tunerSignalStrength *= 0.90; // decay when no signal
            }

            double[] chroma;
            lock (_chromaPool) { chroma = _chromaPool.Count > 0 ? _chromaPool.Pop() : new double[12]; }
            Array.Clear(chroma, 0, 12);

            double total = 0.0;
            for (int i = 0; i < _tonesCount; i++)
            {
                var (freq, w) = _tones[i];
                int pc = PitchClassFromHz(freq);
                chroma[pc] += w;
                total += w;
            }

            // Silence detection: flush stale history
            if (total < 0.02)
            {
                lock (_chromaPool) { _chromaPool.Push(chroma); }
                lock (chordLock)
                {
                    while (chromaQueue.Count > 0)
                    {
                        var old = chromaQueue.Dequeue();
                        for (int i = 0; i < 12; i++) chromaSum[i] -= old[i];
                        lock (_chromaPool) { _chromaPool.Push(old); }
                    }
                    for (int i = 0; i < 12; i++) if (chromaSum[i] < 0) chromaSum[i] = 0;
                }
                lastDetectedChordText = "—";
                lastDetectedNotesText = "";
                lastDetectedHarmonicsText = "";
                _lastChordRoot = -1;
                _lastChordQi = -1;
                if (!scrollPaused) SetChordLabelUi("—", "");
                DrawTuner();
                UpdateHarmonicsDisplay();
                return;
            }

            for (int i = 0; i < 12; i++) chroma[i] /= total;

            if (!scrollPaused) UpdateKeyMaxHoldFromRidges();

            string notesInstant = ChordNotesText(chroma);
            lastDetectedNotesText = notesInstant;

            double[] avg;
            lock (chordLock)
            {
                chromaQueue.Enqueue(chroma);
                for (int i = 0; i < 12; i++) chromaSum[i] += chroma[i];

                int windowSize = Math.Max(1, CHORD_AVG_FRAMES);
                while (chromaQueue.Count > windowSize)
                {
                    var old = chromaQueue.Dequeue();
                    for (int i = 0; i < 12; i++) chromaSum[i] -= old[i];
                    lock (_chromaPool) { _chromaPool.Push(old); }
                }

                if (chromaQueue.Count < Math.Max(1, windowSize / 3))
                {
                    lastDetectedChordText = "—";
                    if (!scrollPaused) SetChordLabelUi("—", "", lastDetectedNotesText, lastDetectedHarmonicsText);
                    DrawTuner();
                    UpdateHarmonicsDisplay();
                    return;
                }

                int cnt = chromaQueue.Count;
                avg = _chromaNorm;
                for (int i = 0; i < 12; i++) avg[i] = chromaSum[i] / cnt;
            }

            double avgSum = 0;
            for (int i = 0; i < 12; i++) avgSum += avg[i];

            Span<double> normAvg = stackalloc double[12];
            if (avgSum > 1e-12)
                for (int i = 0; i < 12; i++) normAvg[i] = avg[i] / avgSum;

            var (chord, canonicalNotes) = DetectChord(avg);
            string notesStr = ChordNotesText(normAvg);
            lastDetectedChordText = chord;
            lastDetectedCanonicalText = canonicalNotes;
            lastDetectedNotesText = notesStr;
            if (!scrollPaused) SetChordLabelUi(chord, canonicalNotes, notesStr, lastDetectedHarmonicsText);
            DrawTuner();
            UpdateHarmonicsDisplay();
        }

        // ====================================================================
        //  Harmonic analysis display
        // ====================================================================
        // Tolerance in cents for attributing a ridge to a harmonic slot.
        private const double HarmonicDisplayCentsTol = 50.0;
        // Minimum intensity for a ridge to qualify as the fundamental.
        private const double HarmonicFundamentalMinIntensity = 0.04;
        // Minimum Hz for the fundamental (avoids 60 Hz hum).
        private const double HarmonicFundamentalMinHz = 70.0;
        // Maximum harmonic number to display.
        private const int HarmonicMaxN = 16;
        // EMA alpha for smoothing harmonic slot intensities across frames.
        // Lower = smoother/slower, higher = faster response.
        private const double HarmonicSlotEmaAlpha = 0.08;

        // Per-slot EMA accumulators (1-indexed, slot 0 unused).
        private readonly double[] _harmonicSlotEma = new double[HarmonicMaxN + 1];

        private string _pendingHarmonicsText = "";

        private void UpdateHarmonicsDisplay()
        {
            // Compute raw per-slot intensities for this frame.
            Span<double> rawSlots = stackalloc double[HarmonicMaxN + 1];
            rawSlots.Clear();
            bool hasSignal = ComputeHarmonicSlots(rawSlots);

            // EMA-smooth each slot: decay toward zero when no signal.
            double alpha = HarmonicSlotEmaAlpha;
            for (int n = 1; n <= HarmonicMaxN; n++)
                _harmonicSlotEma[n] = _harmonicSlotEma[n] * (1.0 - alpha) + rawSlots[n] * alpha;

            // Clear EMA on silence.
            if (!hasSignal)
            {
                for (int n = 1; n <= HarmonicMaxN; n++)
                    _harmonicSlotEma[n] = 0.0;
            }

            // Compute total for normalization and build history string.
            double totalEma = 0.0;
            for (int n = 1; n <= HarmonicMaxN; n++) totalEma += _harmonicSlotEma[n];

            string text = "";
            if (totalEma > 1e-9)
            {
                var sb = new System.Text.StringBuilder(64);
                for (int n = 1; n <= HarmonicMaxN; n++)
                {
                    if (_harmonicSlotEma[n] < 1e-9) continue;
                    int pct = (int)Math.Round(_harmonicSlotEma[n] / totalEma * 100.0);
                    if (pct < 1) continue;
                    if (sb.Length > 0) sb.Append(' ');
                    sb.Append(pct);
                }
                text = sb.ToString();
            }

            // Store for history column.
            lastDetectedHarmonicsText = text;

            // When paused, the UI is driven by mouse position via UpdateChordLabelFromMouse.
            if (scrollPaused) return;

            // Build normalized slots array for bar chart rendering.
            // Each slot value is its fraction of the total; 50% (or the highest single
            // harmonic) represents full bar height so a dominant fundamental fills the bar.
            double norm = totalEma > 1e-9 ? totalEma : 1.0;
            double maxSlotFrac = 0.0;
            double[] slots = new double[HarmonicMaxN + 1];
            for (int n = 1; n <= HarmonicMaxN; n++)
            {
                slots[n] = _harmonicSlotEma[n] / norm;
                if (slots[n] > maxSlotFrac) maxSlotFrac = slots[n];
            }
            // Scale so that the largest bar fills the height, with 0.5 as the minimum ceiling.
            // This means if the top harmonic is >50%, it still fills 100% of the bar area.
            double barCeiling = Math.Max(0.5, maxSlotFrac);
            for (int n = 1; n <= HarmonicMaxN; n++)
                slots[n] = Math.Min(1.0, slots[n] / barCeiling);

            // Dedup: compare text representation to avoid redundant redraws.
            if (text == _pendingHarmonicsText) return;
            _pendingHarmonicsText = text;
            if (!IsHandleCreated || IsDisposed) return;
            if (!UiThrottleReady(ref _lastHarmonicsTick)) return;
            try { BeginInvoke((Action)(() => DrawHarmonicsBar(slots))); }
            catch (Exception ex) { RethrowUnexpectedException(ex); }
        }

        // Called on UI thread to render a bar chart from a history string (mouse-over path).
        private void DrawHarmonicsFromText(string text)
        {
            if (pbHarmonics.IsDisposed) return;
            double[] slots = new double[HarmonicMaxN + 1];
            if (!string.IsNullOrWhiteSpace(text))
            {
                string[] parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                int n = 1;
                foreach (string part in parts)
                {
                    if (n > HarmonicMaxN) break;
                    if (int.TryParse(part, out int pct))
                        slots[n] = pct / 100.0;
                    n++;
                }
            }
            // Apply same 50%-ceiling scaling as the live path.
            double maxSlotFrac = 0.0;
            for (int n = 1; n <= HarmonicMaxN; n++)
                if (slots[n] > maxSlotFrac) maxSlotFrac = slots[n];
            double barCeiling = Math.Max(0.5, maxSlotFrac);
            for (int n = 1; n <= HarmonicMaxN; n++)
                slots[n] = Math.Min(1.0, slots[n] / barCeiling);
            DrawHarmonicsBar(slots);
        }

        // Called on UI thread. Renders normalized slots (index 1..HarmonicMaxN) as a bar chart.
        private void DrawHarmonicsBar(double[] slots)
        {
            if (pbHarmonics.IsDisposed) return;
            int w = pbHarmonics.ClientSize.Width;
            int h = pbHarmonics.ClientSize.Height;
            if (w <= 0 || h <= 0) return;

            const int numBars = 12; // harmonics 1..12
            const int labelH = 14; // pixels reserved at bottom for numbers
            const int barPad = 2;  // gap between bars

            int barAreaH = h - labelH;
            int barW = Math.Max(1, (w - barPad) / numBars - barPad);
            int totalSlotW = barW + barPad;

            // Reuse bitmap — only reallocate on size change
            if (_harmonicsBitmap == null || _harmonicsBitmap.Width != w || _harmonicsBitmap.Height != h)
            {
                _harmonicsBitmap?.Dispose();
                _harmonicsBitmap = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            }
            var bmp = _harmonicsBitmap;
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(20, 20, 20));

                using var labelFont = new Font("Segoe UI", 7f);
                using var barBrush = new SolidBrush(Color.FromArgb(80, 180, 255));
                using var textBrush = new SolidBrush(Color.FromArgb(180, 180, 180));

                for (int n = 1; n <= numBars; n++)
                {
                    double v = (slots != null && n < slots.Length) ? Math.Min(1.0, slots[n]) : 0.0;
                    int bh = (int)Math.Round(v * barAreaH);
                    int x = barPad + (n - 1) * totalSlotW;
                    int y = barAreaH - bh;

                    if (bh > 0)
                        g.FillRectangle(barBrush, x, y, barW, bh);

                    // Label
                    string lbl = n.ToString();
                    var sz = g.MeasureString(lbl, labelFont);
                    float lx = x + barW / 2f - sz.Width / 2f;
                    g.DrawString(lbl, labelFont, textBrush, lx, barAreaH + 1);
                }
            }

            pbHarmonics.Image = bmp;
        }

        private bool ComputeHarmonicSlots(Span<double> slots)
        {
            // Returns true if a valid fundamental was found.
            lock (ridgeLock)
            {
                double peakIntensity = 0.0;
                for (int i = 0; i < ridges.Count; i++)
                {
                    var r = ridges[i];
                    if (r.Miss > 2) continue;
                    if (r.Intensity > peakIntensity) peakIntensity = r.Intensity;
                }

                double threshold = peakIntensity * HarmonicFundamentalMinIntensity;
                if (threshold < 0.01) threshold = 0.01;
                if (peakIntensity < 0.01) return false;

                // Find lowest qualified ridge as fundamental.
                double fundamentalHz = 0.0;
                double lowestLogFreq = double.MaxValue;
                for (int i = 0; i < ridges.Count; i++)
                {
                    var r = ridges[i];
                    if (r.Miss > 2) continue;
                    if (r.Intensity < threshold) continue;
                    double f = Math.Exp(r.LogFreq + 0.5 * r.LogVel);
                    if (f < HarmonicFundamentalMinHz) continue;
                    if (f < displayFmin || f > displayFmax) continue;
                    double lf = r.LogFreq + 0.5 * r.LogVel;
                    if (lf < lowestLogFreq)
                    {
                        lowestLogFreq = lf;
                        fundamentalHz = f;
                    }
                }

                if (fundamentalHz <= 0.0) return false;

                // Accumulate ridge intensities into harmonic slots.
                for (int i = 0; i < ridges.Count; i++)
                {
                    var r = ridges[i];
                    if (r.Miss > 4) continue;
                    double f = Math.Exp(r.LogFreq + 0.5 * r.LogVel);
                    if (f < displayFmin || f > displayFmax) continue;

                    double ratio = f / fundamentalHz;
                    int n = (int)Math.Round(ratio);
                    if (n < 1 || n > HarmonicMaxN) continue;

                    // Check cents deviation from exact harmonic.
                    double exactHz = fundamentalHz * n;
                    double centsDev = Math.Abs(1200.0 * Math.Log2(f / exactHz));
                    if (centsDev > HarmonicDisplayCentsTol) continue;

                    slots[n] += r.Intensity;
                }

                return true;
            }
        }

        // [OPT-8] Overload that accepts Span<double>
        private string ChordNotesText(Span<double> normChroma)
        {
            int count = 0;
            for (int i = 0; i < 12; i++)
            {
                double frac = normChroma[i];
                if (frac >= 0.05) _chordNotesSorted[count++] = (i, frac);
            }
            if (count == 0) return "";

            for (int i = 1; i < count; i++)
            {
                var key = _chordNotesSorted[i];
                int j = i - 1;
                while (j >= 0 && _chordNotesSorted[j].frac < key.frac) { _chordNotesSorted[j + 1] = _chordNotesSorted[j]; j--; }
                _chordNotesSorted[j + 1] = key;
            }

            _chordNotesSb.Clear();
            for (int i = 0; i < count; i++)
            {
                var (pc, frac) = _chordNotesSorted[i];
                if (_chordNotesSb.Length > 0) _chordNotesSb.Append("  ");
                _chordNotesSb.Append(NoteName(pc));
                _chordNotesSb.Append('(');
                _chordNotesSb.Append((int)(frac * 100.0 + 0.5));
                _chordNotesSb.Append("%)");
            }
            return _chordNotesSb.ToString();
        }

        private int PitchClassFromHz(double hz)
        {
            double midi = 69.0 + 12.0 * Math.Log2(hz / tuning);
            int pc = (int)Math.Round(midi) % 12;
            return pc < 0 ? pc + 12 : pc;
        }

        private const double ChordNoteThreshold = 0.10;
        private const double ChordNoteThresholdLow = 0.05;

        /// <summary>
        /// Returns a score bonus for a chord candidate based on the currently
        /// detected (or manually selected) key.  If the candidate is a major triad
        /// (or major-family chord) whose root+minor-third IS diatonic to the key
        /// but whose root+major-third is NOT, a strong bias is added to the
        /// equivalent minor interpretation instead.  This prevents G major being
        /// wrongly preferred over G minor when the key context (e.g. Bb major or
        /// G minor) makes the major reading impossible.
        /// </summary>
        private double KeyContextChordBias(int root, int qi)
        {
            // Only apply when a key is established
            if (_hysteresisRoot < 0 || _hysteresisMode < 0) return 0.0;

            int keyRoot = _hysteresisRoot;
            var keyIntervals = _modeIntervals[_hysteresisMode];

            // Build set of diatonic pitch classes for the current key
            var diatonic = new bool[12];
            foreach (int iv in keyIntervals) diatonic[(keyRoot + iv) % 12] = true;

            ref readonly var q = ref ChordQualities[qi];

            // Determine if this quality has a major third (interval 4) or minor third (interval 3)
            bool hasMajorThird = false, hasMinorThird = false;
            foreach (int iv in q.Intervals)
            {
                if (iv == 4) hasMajorThird = true;
                if (iv == 3) hasMinorThird = true;
            }

            if (!hasMajorThird && !hasMinorThird) return 0.0;

            int majorThirdPc = (root + 4) % 12;
            int minorThirdPc = (root + 3) % 12;

            bool majorThirdDiatonic = diatonic[majorThirdPc];
            bool minorThirdDiatonic = diatonic[minorThirdPc];

            // Scale the bias by key confidence (margin between best and second-best key
            // score).  When the key is ambiguous (e.g. relative major/minor toss-up,
            // confidence ~0.02) the bias shrinks toward zero so we don't enforce a
            // diatonic interpretation we're not sure about.  A short ramp over [0, 0.08]
            // means full bias kicks in once the margin is comfortably clear.
            double confidenceScale = Math.Clamp(_keyConfidence / 0.08, 0.0, 1.0);
            double bias = CHORD_KEY_CONTEXT_BIAS * confidenceScale;

            // Minor is diatonic, major is not → bias toward minor quality
            if (hasMinorThird && minorThirdDiatonic && !majorThirdDiatonic)
                return bias;

            // Do NOT bias toward major even if the major third is diatonic.
            // Key context should only rescue a minor chord from being mis-labelled
            // major, never the other way around.  ScoreCandidate's thirdBonus is
            // capped at root presence so chords whose third exceeds their root are
            // naturally down-scored relative to root-dominant readings.
            return 0.0;
        }

        private const double CHORD_KEY_CONTEXT_BIAS = 0.60;

        private double ScoreCandidate(
            Span<double> c, int root,
            in (string Suffix, int[] Intervals, int ThirdOffset, bool HasThird, double ObscurityPenalty) q,
            double threshold)
        {
            for (int ki = 0; ki < q.Intervals.Length; ki++)
                if (c[(root + q.Intervals[ki]) % 12] < threshold)
                    return double.NegativeInfinity;

            double inChord = 0.0;
            for (int ki = 0; ki < q.Intervals.Length; ki++)
                inChord += c[(root + q.Intervals[ki]) % 12];

            double outChord = 1.0 - inChord;

            double minPresence = 1.0;
            for (int ki = 0; ki < q.Intervals.Length; ki++)
            {
                double v = c[(root + q.Intervals[ki]) % 12];
                if (v < minPresence) minPresence = v;
            }
            double completenessBonus = 0.20 * minPresence;

            double avgInChord = inChord / Math.Max(1, q.Intervals.Length);
            double rootBonus = 0.35 * Math.Min(1.0, c[root] / Math.Max(1e-6, avgInChord));

            // Third bonus: scaled relative to the root so a chord whose third is
            // louder than its root (e.g. Dm where F > D) doesn't get a free ride.
            // We cap the ratio at 1.0: a third equal to the root earns the full bonus;
            // a third weaker than the root earns proportionally less.
            double rootPresence = c[root];
            double thirdPresence = q.HasThird ? c[(root + q.ThirdOffset) % 12] : 0.0;
            double thirdBonus = q.HasThird
                ? 0.25 * Math.Min(1.0, thirdPresence / Math.Max(1e-6, rootPresence)) * rootPresence
                : 0.0;
            double fifthBonus = 0.08 * c[(root + 7) % 12];

            // Complexity penalty: scaled by how weakly the extra notes are present.
            // Increased coefficient so extended chords require clear evidence.
            int extraNotes = q.Intervals.Length - 3;
            double complexityPenalty = extraNotes > 0
                ? 0.04 * extraNotes * (1.0 - inChord / Math.Max(1e-6, q.Intervals.Length * threshold))
                : 0.0;

            // Obscurity penalty: flat deduction that a common chord does not pay.
            // Rare/exotic chords must outscore common ones by this margin to win.
            double obscurityPenalty = q.ObscurityPenalty;

            return inChord
                 - CHORD_OUT_PENALTY * outChord
                 + completenessBonus
                 + rootBonus
                 + thirdBonus
                 + fifthBonus
                 - complexityPenalty
                 - obscurityPenalty;
        }

        private (string Label, string Canonical) DetectChord(double[] chroma)
        {
            double sum = 0;
            for (int i = 0; i < 12; i++) sum += chroma[i];
            if (sum <= 1e-12) return ("—", "");

            Span<double> c = stackalloc double[12];
            for (int i = 0; i < 12; i++) c[i] = chroma[i] / sum;

            // Pass 1: strict threshold
            double bestScore = double.NegativeInfinity;
            int bestRoot = 0;
            int bestQi = -1;
            bool anyMatch = false;

            for (int root = 0; root < 12; root++)
            {
                for (int qi = 0; qi < ChordQualities.Length; qi++)
                {
                    ref readonly var q = ref ChordQualities[qi];
                    double score = ScoreCandidate(c, root, q, ChordNoteThreshold);
                    if (score > double.NegativeInfinity) anyMatch = true;
                    score += KeyContextChordBias(root, qi);
                    if (score > bestScore) { bestScore = score; bestRoot = root; bestQi = qi; }
                }
            }

            // Always run Pass 2 at the relaxed threshold and prefer it if it finds
            // a strictly richer chord than Pass 1. This lets e.g. a dominant-7 at
            // 8% beat a bare triad that happened to pass at 10%.
            int bestRootStrict = bestRoot;
            int bestQiStrict = bestQi;
            int strictLen = anyMatch ? ChordQualities[bestQi].Intervals.Length : 0;

            // Pass 2: relaxed threshold
            bestScore = double.NegativeInfinity;
            bool anyMatchLow = false;
            for (int root = 0; root < 12; root++)
            {
                for (int qi = 0; qi < ChordQualities.Length; qi++)
                {
                    ref readonly var q = ref ChordQualities[qi];
                    double score = ScoreCandidate(c, root, q, ChordNoteThresholdLow);
                    if (score > double.NegativeInfinity) anyMatchLow = true;
                    score += KeyContextChordBias(root, qi);
                    if (score > bestScore) { bestScore = score; bestRoot = root; bestQi = qi; }
                }
            }

            if (!anyMatchLow) return anyMatch ? BuildResult(bestRootStrict, bestQiStrict) : ("—", "");

            // Prefer Pass 2 only when Pass 1 found nothing or only a two-note chord,
            // AND Pass 2 found something richer. A valid triad (3+ notes) at 10% locks
            // out any relaxed-threshold upgrade.
            if (anyMatch && (strictLen >= 3 || ChordQualities[bestQi].Intervals.Length <= strictLen))
            { bestRoot = bestRootStrict; bestQi = bestQiStrict; }

            // Chord label hysteresis: suppress flickering between extensions/variants
            // of the same chord (e.g. Cmaj vs Cmaj7 vs Csus2) by requiring the new
            // winner to beat the second-best score by a meaningful margin, AND by
            // preferring the currently-displayed chord when it ties with a variant.
            if (_lastChordRoot >= 0)
            {
                bool sameCore = bestRoot == _lastChordRoot &&
                                ChordCoreQuality(bestQi) == ChordCoreQuality(_lastChordQi);

                if (sameCore)
                {
                    // Same root + family: only upgrade to a richer extension if it
                    // clearly outscore the current displayed quality right now.
                    double currentScore = ScoreCandidate(c, _lastChordRoot,
                        ChordQualities[_lastChordQi], ChordNoteThresholdLow)
                        + KeyContextChordBias(_lastChordRoot, _lastChordQi);
                    if (bestScore < currentScore + CHORD_HYSTERESIS_EXTENSION)
                        return BuildResult(_lastChordRoot, _lastChordQi);
                }
                else
                {
                    // Different chord: re-score the currently displayed chord and
                    // require the challenger to beat it by a clear margin.
                    double currentScore = ScoreCandidate(c, _lastChordRoot,
                        ChordQualities[_lastChordQi], ChordNoteThresholdLow)
                        + KeyContextChordBias(_lastChordRoot, _lastChordQi);
                    if (bestScore < currentScore + CHORD_HYSTERESIS_CHANGE)
                        return BuildResult(_lastChordRoot, _lastChordQi);
                }
            }

            _lastChordRoot = bestRoot;
            _lastChordQi = bestQi;
            return BuildResult(bestRoot, bestQi);
        }

        // Maps a chord quality index to a coarse category so that e.g. maj/maj7/maj9
        // are all treated as the same "major" family for hysteresis purposes.
        private static int ChordCoreQuality(int qi)
        {
            if (qi < 0) return -1;
            ref readonly var q = ref ChordQualities[qi];
            bool hasMaj3 = false, hasMin3 = false, hasDim5 = false, hasAug5 = false, hasSus = false;
            foreach (int iv in q.Intervals)
            {
                if (iv == 4) hasMaj3 = true;
                if (iv == 3) hasMin3 = true;
                if (iv == 6) hasDim5 = true;
                if (iv == 8) hasAug5 = true;
                if (iv == 2 || iv == 5) hasSus = true;
            }
            if (hasSus && !hasMaj3 && !hasMin3) return 4; // sus family
            if (hasDim5 && hasMin3) return 3;              // dim family
            if (hasAug5 && hasMaj3) return 2;              // aug family
            if (hasMin3) return 1;                         // minor family
            return 0;                                      // major family
        }

        private int _lastChordRoot = -1;
        private int _lastChordQi = -1;

        // Margin required to switch to a richer extension of the same root+quality.
        private const double CHORD_HYSTERESIS_EXTENSION = 0.04;
        // Margin required to switch to a genuinely different chord.
        private const double CHORD_HYSTERESIS_CHANGE = 0.08;

        private string BuildCanonical(int rootPc, int qi)
        {
            var intervals = ChordQualities[qi].Intervals;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < intervals.Length; i++)
            {
                if (i > 0) sb.Append("  ");
                sb.Append(NoteName((rootPc + intervals[i]) % 12));
            }
            return sb.ToString();
        }

        private (string Label, string Canonical) BuildResult(int rootPc, int qi)
        {
            string label = NoteName(rootPc) + ChordQualities[qi].Suffix;
            string canonical = BuildCanonical(rootPc, qi);
            return (label, canonical);
        }

        // ====================================================================
        //  Spectrogram & Grid
        // ====================================================================
        private void InitChordCols(int width)
        {
            chordCols = new string[width];
            detectedNotesCols = new string[width];
            canonicalCols = new string[width];
            harmonicsCols = new string[width];
            tunerFreqCols = new double[width];
            tunerIntensityCols = new double[width];
            Array.Fill(chordCols, "—");
            Array.Fill(detectedNotesCols, "");
            Array.Fill(canonicalCols, "");
            Array.Fill(harmonicsCols, "");
            // tunerFreqCols / tunerIntensityCols default to 0.0 (no signal)
        }

        private void ClearSpectrogram()
        {
            lock (bitmapLock)
            {
                if (spectrogramBitmap == null) return;
                using var g = Graphics.FromImage(spectrogramBitmap);
                g.Clear(Color.Black);
                InitChordCols(spectrogramBitmap.Width);
            }

            lock (ridgeLock)
            {
                foreach (var r in ridges) ReturnRidge(r);
                ridges.Clear();
                _ridgeById.Clear();
                nextRidgeId = 1;
            }

            _ridgeAccumCount = 0;
            _ridgeAccumIndex.Clear();
            _bitmapWriteCol = 0;
            _smoothedMaxIntensity = 1e-12;

            lock (chordLock)
            {
                while (chromaQueue.Count > 0)
                {
                    var arr = chromaQueue.Dequeue();
                    lock (_chromaPool) { _chromaPool.Push(arr); }
                }
                Array.Clear(chromaSum, 0, chromaSum.Length);
                lastDetectedChordText = "—";
            }

            if (!scrollPaused) SetChordLabelUi("―");
            else UpdateChordLabelFromMouse();
        }

        private void Pic_Paint(object sender, PaintEventArgs e)
        {

            if (cbShowCircle.Checked)
            {
                DrawCircleOfFifths(e.Graphics, pic.Width, pic.Height);
                return;
            }
            lock (bitmapLock)
            {
                if (spectrogramBitmap != null)
                {
                    int w = spectrogramBitmap.Width;
                    int h = spectrogramBitmap.Height;
                    int wc = _bitmapWriteCol;

                    // The write column is the newest data (logical right edge).
                    // Columns [wc+1 .. w-1] are the oldest (logical left), drawn first.
                    // Columns [0 .. wc] are newer, drawn flush-right.
                    if (wc == w - 1)
                    {
                        e.Graphics.DrawImageUnscaled(spectrogramBitmap, 0, 0);
                    }
                    else
                    {
                        int rightSrcX = wc + 1;
                        int rightWidth = w - rightSrcX;
                        int leftWidth = wc + 1;

                        e.Graphics.DrawImage(spectrogramBitmap,
                            new Rectangle(0, 0, rightWidth, h),
                            new Rectangle(rightSrcX, 0, rightWidth, h),
                            GraphicsUnit.Pixel);

                        e.Graphics.DrawImage(spectrogramBitmap,
                            new Rectangle(rightWidth, 0, leftWidth, h),
                            new Rectangle(0, 0, leftWidth, h),
                            GraphicsUnit.Pixel);
                    }
                }
            }

            DrawNoteGrid(e.Graphics);
            DrawChordTextOverlay(e.Graphics);
        }

        // Split a chord label (e.g. "C#maj7") into (pitchClass="C#", quality="maj7").
        private static (string PitchClass, string Quality) SplitChordLabel(string label)
        {
            if (string.IsNullOrEmpty(label) || label == "—")
                return (label, "");
            // Root is always the first character (a letter).
            // Check if second character is an accidental: '#', '♭', or 'b' used as flat.
            int rootLen = 1;
            if (label.Length > 1)
            {
                char c = label[1];
                if (c == '#' || c == '♭')
                    rootLen = 2;
                else if (c == 'b' && label.Length > 2 && !char.IsUpper(label[2]))
                    rootLen = 2; // e.g. "Bb7"
            }
            string pc = label.Substring(0, rootLen);
            string quality = label.Length > rootLen ? label.Substring(rootLen) : "";
            return (pc, quality);
        }

        private void DrawChordTextOverlay(Graphics g)
        {
            string[] cols;
            int w;
            lock (bitmapLock)
            {
                if (chordCols == null) return;
                cols = chordCols;
                w = cols.Length;
            }
            if (w == 0) return;

            _chordOverlayFont ??= new Font("Segoe UI", 9f, FontStyle.Bold);
            _chordOverlayFontQuality ??= new Font("Segoe UI", 7.5f, FontStyle.Regular);

            float pcHeight = _chordOverlayFont.GetHeight(g);

            for (int x = w - 1; x >= 0; x -= ChordTextSpacing)
            {
                string label = null;
                int spanEnd = x;
                int spanStart = Math.Max(0, x - ChordTextSpacing + 1);
                for (int ix = spanEnd; ix >= spanStart; ix--)
                {
                    string chord = cols[ix];
                    if (!string.IsNullOrWhiteSpace(chord) && chord != "—")
                    { label = chord; break; }
                }

                if (label == null) continue;

                var (pitchClass, quality) = SplitChordLabel(label);

                // Row 1: pitch class (root note) in white bold
                SizeF szPc = g.MeasureString(pitchClass, _chordOverlayFont);
                float drawX = Math.Max(0, x - szPc.Width * 0.5f);
                g.DrawString(pitchClass, _chordOverlayFont, Brushes.White, drawX, ChordOverlayY);

                // Row 2: quality (chord suffix) in light gray
                if (!string.IsNullOrEmpty(quality))
                {
                    SizeF szQ = g.MeasureString(quality, _chordOverlayFontQuality);
                    float drawXQ = Math.Max(0, x - szQ.Width * 0.5f);
                    g.DrawString(quality, _chordOverlayFontQuality, Brushes.LightGray, drawXQ, ChordOverlayY + pcHeight);
                }
            }
        }

        private void RecreateBitmap()
        {
            lock (bitmapLock)
            {
                int w = Math.Max(1, pic.ClientSize.Width);
                int h = Math.Max(1, pic.ClientSize.Height);

                var newBitmap = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(newBitmap))
                {
                    g.Clear(Color.Black);

                    if (spectrogramBitmap != null)
                    {
                        int oldW = spectrogramBitmap.Width;
                        int oldH = spectrogramBitmap.Height;

                        // The scroll history scrolls left, so the right edge is "now".
                        // Preserve the rightmost min(oldW,newW) columns, stretched vertically
                        // to fit the new height (log-scale is the same, just different pixel height).
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear;
                        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;

                        if (oldW <= w)
                        {
                            // New bitmap is wider — old content goes flush-right, left remainder stays black.
                            int destX = w - oldW;
                            g.DrawImage(spectrogramBitmap,
                                new Rectangle(destX, 0, oldW, h),
                                new Rectangle(0, 0, oldW, oldH),
                                GraphicsUnit.Pixel);
                        }
                        else
                        {
                            // New bitmap is narrower — take only the rightmost newW columns of old.
                            int srcX = oldW - w;
                            g.DrawImage(spectrogramBitmap,
                                new Rectangle(0, 0, w, h),
                                new Rectangle(srcX, 0, w, oldH),
                                GraphicsUnit.Pixel);
                        }
                    }
                }

                spectrogramBitmap?.Dispose();
                spectrogramBitmap = newBitmap;
                // After a resize the content was drawn flush-right into the new bitmap
                // by DrawImage above, so logical column w-1 is at bitmap column w-1.
                _bitmapWriteCol = w - 1;

                // Remap chord columns to match new width.
                if (chordCols != null)
                {
                    int oldW = chordCols.Length;
                    var newChord = new string[w];
                    var newCanon = new string[w];
                    var newDetected = new string[w];
                    var newHarmonics = new string[w];
                    Array.Fill(newChord, "—");
                    Array.Fill(newCanon, "");
                    Array.Fill(newDetected, "");
                    Array.Fill(newHarmonics, "");

                    string[] safeCanon = canonicalCols ?? Array.Empty<string>();
                    string[] safeDetected = detectedNotesCols ?? Array.Empty<string>();
                    string[] safeHarmonics = harmonicsCols ?? Array.Empty<string>();

                    if (oldW <= w)
                    {
                        // Old content fits — place flush-right.
                        int destX = w - oldW;
                        Array.Copy(chordCols, 0, newChord, destX, oldW);
                        int cn = Math.Min(oldW, safeCanon.Length);
                        if (cn > 0) Array.Copy(safeCanon, 0, newCanon, destX, cn);
                        int dn = Math.Min(oldW, safeDetected.Length);
                        if (dn > 0) Array.Copy(safeDetected, 0, newDetected, destX, dn);
                        int hn = Math.Min(oldW, safeHarmonics.Length);
                        if (hn > 0) Array.Copy(safeHarmonics, 0, newHarmonics, destX, hn);
                    }
                    else
                    {
                        // New bitmap is narrower — keep only the rightmost w columns.
                        int srcX = oldW - w;
                        Array.Copy(chordCols, srcX, newChord, 0, w);
                        int cn = Math.Min(w, Math.Max(0, safeCanon.Length - srcX));
                        if (cn > 0) Array.Copy(safeCanon, srcX, newCanon, 0, cn);
                        int dn = Math.Min(w, Math.Max(0, safeDetected.Length - srcX));
                        if (dn > 0) Array.Copy(safeDetected, srcX, newDetected, 0, dn);
                        int hn = Math.Min(w, Math.Max(0, safeHarmonics.Length - srcX));
                        if (hn > 0) Array.Copy(safeHarmonics, srcX, newHarmonics, 0, hn);
                    }

                    chordCols = newChord;
                    canonicalCols = newCanon;
                    detectedNotesCols = newDetected;
                    harmonicsCols = newHarmonics;
                }
                else
                {
                    InitChordCols(w);
                }
            }

            // Discard any pre-resize accumulated ridge positions and display coords.
            // Stale LastXf/LastYf values from the old size cause streaks on first draw.
            _ridgeAccumCount = 0;
            _ridgeAccumIndex.Clear();

            lock (ridgeLock)
            {
                for (int i = 0; i < ridges.Count; i++)
                    ridges[i].HasLastPos = false;
            }

            _gridCache?.Dispose();
            _gridCache = null;
        }

        private void RefreshDisplayRange()
        {
            double lo = HighPass > 0.0 ? HighPass : MidiToHz(24);
            double hi = LowPass > 0.0 ? LowPass : MidiToHz(108);
            if (hi <= lo) hi = lo + 1.0;
            displayFmin = (float)Math.Max(1.0, lo);
            displayFmax = (float)hi;
        }

        // Remap the existing spectrogram bitmap from oldFmin/oldFmax log-scale
        // to the new displayFmin/displayFmax log-scale so the display is preserved.
        private void RetrofitSpectrogramToNewScale(float oldFmin, float oldFmax)
        {
            lock (bitmapLock)
            {
                if (spectrogramBitmap == null) return;
                int w = spectrogramBitmap.Width;
                int h = spectrogramBitmap.Height;
                if (w < 1 || h < 1) return;

                double oldLogMin = Math.Log(oldFmin);
                double oldLogMax = Math.Log(oldFmax);
                double newLogMin = Math.Log(displayFmin);
                double newLogMax = Math.Log(displayFmax);
                double oldLogRange = oldLogMax - oldLogMin;
                double newLogRange = newLogMax - newLogMin;
                if (oldLogRange < 1e-10 || newLogRange < 1e-10) return;

                BitmapData bd = spectrogramBitmap.LockBits(
                    new Rectangle(0, 0, w, h),
                    ImageLockMode.ReadWrite,
                    PixelFormat.Format32bppArgb);

                try
                {
                    unsafe
                    {
                        byte* basePtr = (byte*)bd.Scan0;
                        int stride = bd.Stride;

                        // Temp buffer for one column at a time
                        byte[] tempCol = new byte[h * 4];

                        for (int x = 0; x < w; x++)
                        {
                            // Copy source column into temp buffer
                            for (int y = 0; y < h; y++)
                            {
                                byte* src = basePtr + y * stride + x * 4;
                                int ti = y * 4;
                                tempCol[ti] = src[0];
                                tempCol[ti + 1] = src[1];
                                tempCol[ti + 2] = src[2];
                                tempCol[ti + 3] = src[3];
                            }

                            // Write remapped pixels
                            for (int y = 0; y < h; y++)
                            {
                                // Frequency this destination row represents in the new scale
                                double yFrac = (h - 1 - y) / (double)(h - 1);
                                double logFreq = newLogMin + yFrac * newLogRange;

                                // Where did this frequency live in the old scale?
                                double oldYFrac = (logFreq - oldLogMin) / oldLogRange;
                                double oldYf = (h - 1) - oldYFrac * (h - 1);

                                byte* dst = basePtr + y * stride + x * 4;

                                if (oldYf < 0.0 || oldYf > h - 1)
                                {
                                    // Frequency was outside the old visible range → black
                                    dst[0] = dst[1] = dst[2] = 0;
                                    dst[3] = 255;
                                }
                                else
                                {
                                    // Bilinear interpolation between two source rows
                                    int y0 = (int)Math.Floor(oldYf);
                                    int y1 = Math.Min(y0 + 1, h - 1);
                                    double t = oldYf - y0;

                                    int t0 = y0 * 4, t1 = y1 * 4;
                                    dst[0] = (byte)(tempCol[t0] + t * (tempCol[t1] - tempCol[t0]));
                                    dst[1] = (byte)(tempCol[t0 + 1] + t * (tempCol[t1 + 1] - tempCol[t0 + 1]));
                                    dst[2] = (byte)(tempCol[t0 + 2] + t * (tempCol[t1 + 2] - tempCol[t0 + 2]));
                                    dst[3] = 255;
                                }
                            }
                        }
                    }
                }
                finally
                {
                    spectrogramBitmap.UnlockBits(bd);
                }
            }

            // Ridge last-positions reference old pixel Y coordinates — reset them
            // so the first new frame doesn't draw erroneous connecting lines.
            _ridgeAccumCount = 0;
            _ridgeAccumIndex.Clear();
            lock (ridgeLock)
            {
                for (int i = 0; i < ridges.Count; i++)
                    ridges[i].HasLastPos = false;
            }

            // Grid cache is stale with the old scale.
            _gridCache?.Dispose();
            _gridCache = null;
        }

        private void DrawNoteGrid(Graphics g)
        {
            int h = pic.ClientSize.Height;
            int w = pic.ClientSize.Width;
            bool showLines = cblines.Checked;
            double logMin = Math.Log(displayFmin);
            double logMax = Math.Log(displayFmax);
            double invLog = 1.0 / (logMax - logMin);

            _gridPen ??= new Pen(Color.FromArgb(100, Color.Gray));
            _gridFont ??= new Font("Segoe UI", 8);
            _gridBrush ??= new SolidBrush(Color.FromArgb(180, Color.Lime));

            int loOct = (int)Math.Floor(Math.Log2(displayFmin / tuning) + 69.0 / 12.0) - 1;
            int hiOct = (int)Math.Ceiling(Math.Log2(displayFmax / tuning) + 69.0 / 12.0) + 1;
            loOct = Math.Max(-2, loOct);
            hiOct = Math.Min(12, hiOct);

            for (int oct = loOct; oct <= hiOct; oct++)
            {
                for (int n = 0; n < 12; n++)
                {
                    double freq = NoteFrequency(n, oct);
                    if (freq < displayFmin || freq > displayFmax) continue;
                    if (!IsNoteInActiveScale(n)) continue;

                    double yFrac = (Math.Log(freq) - logMin) * invLog;
                    int y = h - 1 - (int)(yFrac * (h - 1));

                    if (showLines) g.DrawLine(_gridPen, 0, y, w, y);
                    g.DrawString(NoteName(n) + oct, _gridFont, _gridBrush, 2, y - 9);
                }
            }
        }

        private double NoteFrequency(int noteIndexFromC, int octave)
        {
            int midi = 12 + octave * 12 + noteIndexFromC;
            return tuning * Math.Pow(2.0, (midi - 69) / 12.0);
        }

        private string NoteName(int n) => NoteNames[n % 12];

        // ====================================================================
        //  Recording
        // ====================================================================
        private void BtnRecord_Click(object sender, EventArgs e)
        {
            if (!_isRecording)
                StartRecording();
            else
                StopRecording();
        }

        private void StartRecording()
        {
            var fmt = captureFormat;
            _recordSampleRate = fmt?.SampleRate ?? 44100;

            string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string path = System.IO.Path.Combine(docs,
                $"SpectrumNotes_{DateTime.Now:yyyyMMdd_HHmmss}.wav");

            lock (_recordLock)
            {
                _ridgePhase.Clear();
                _ridgePrevAmp.Clear();
                _ridgeLastFreq.Clear();
                _recordSamplesCapured = 0;
                _recordSamplesWritten = 0;
                _waveWriter = new WaveFileWriter(path, new WaveFormat(_recordSampleRate, 16, 1));
                _isRecording = true;
            }

            btnRecord.Text = "⏹ Stop";
            btnRecord.BackColor = Color.FromArgb(180, 0, 0);
        }

        private void StopRecording()
        {
            string path = null;
            lock (_recordLock)
            {
                _isRecording = false;

                // Write any remaining captured samples as silence to fill the gap
                if (_waveWriter != null)
                {
                    long remaining = _recordSamplesCapured - _recordSamplesWritten;
                    if (remaining > 0 && remaining < _recordSampleRate * 10) // sanity cap
                    {
                        var silence = new byte[remaining * 2];
                        _waveWriter.Write(silence, 0, silence.Length);
                    }
                }

                path = _waveWriter?.Filename;
                _waveWriter?.Flush();
                _waveWriter?.Dispose();
                _waveWriter = null;
                _ridgePhase.Clear();
                _ridgePrevAmp.Clear();
                _ridgeLastFreq.Clear();
            }

            btnRecord.Text = "⏺ Record";
            btnRecord.BackColor = Color.FromArgb(139, 0, 0);

            if (path != null)
            {
                string folder = System.IO.Path.GetDirectoryName(path);
                try { System.Diagnostics.Process.Start("explorer.exe", folder); } catch (Exception ex) { RethrowUnexpectedException(ex); }
            }
        }

        /// <summary>
        /// Called once per RenderFrame. Synthesizes sine waves for each active ridge,
        /// writing exactly as many samples as have been captured since the last write —
        /// so dropped FFT frames still produce silence and timing is always correct.
        /// </summary>
        private void RecordRidgeFrame()
        {
            WaveFileWriter writer;
            lock (_recordLock)
            {
                writer = _waveWriter;
                if (!_isRecording || writer == null) return;
            }

            int sampleRate = _recordSampleRate;

            // How many samples have been captured but not yet written?
            long captured = Interlocked.Read(ref _recordSamplesCapured);
            int numSamples = (int)Math.Max(0, captured - _recordSamplesWritten);
            if (numSamples == 0) return;

            // Snapshot ridges under lock
            int snapCount = 0;
            (double freq, double intensity, int ridgeId)[] snapshot;
            lock (ridgeLock)
            {
                snapshot = new (double, double, int)[ridges.Count];
                for (int i = 0; i < ridges.Count; i++)
                {
                    var r = ridges[i];
                    if (r.Miss > 2) continue;
                    double freq = Math.Exp(r.LogFreq + 0.5 * r.LogVel);
                    if (freq < displayFmin || freq > displayFmax) continue;

                    int hLen = Ridge.IntensityHistoryLen;
                    int validFrames = Math.Min(r.Age + 1, hLen);
                    double sum = 0;
                    for (int k = 0; k < validFrames; k++)
                    {
                        int idx = (r.HistoryHead - 1 - k + hLen) % hLen;
                        sum += r.IntensityHistory[idx];
                    }
                    double smoothed = validFrames > 0 ? sum / validFrames : r.Intensity;

                    if (!_ridgePhase.ContainsKey(r.Id))
                        _ridgePhase[r.Id] = 0.0;

                    snapshot[snapCount++] = (freq, Math.Clamp(smoothed, 0, 1), r.Id);
                }
            }

            double totalIntensity = 0;
            for (int i = 0; i < snapCount; i++) totalIntensity += snapshot[i].intensity;
            double normalize = totalIntensity > 1e-12 ? Math.Min(1.0, 0.85 / totalIntensity) : 0;

            double twoPiOverSr = 2.0 * Math.PI / sampleRate;
            double invN = numSamples > 1 ? 1.0 / (numSamples - 1) : 1.0;

            // Build the set of all ridge IDs that need to be rendered this frame:
            // active ridges (current snapshot) + fading-out ridges (had prev amp > 0, now gone)
            var renderSet = new Dictionary<int, (double freq, double targetAmp)>(snapCount + 8);
            for (int i = 0; i < snapCount; i++)
                renderSet[snapshot[i].ridgeId] = (snapshot[i].freq, snapshot[i].intensity * normalize);

            // Any ridge with a stored prev amp that's no longer active needs to fade to zero
            foreach (var kv in _ridgePrevAmp)
                if (kv.Value > 1e-9 && !renderSet.ContainsKey(kv.Key))
                    if (_ridgePhase.TryGetValue(kv.Key, out double ph))
                        renderSet[kv.Key] = (0.0, 0.0); // freq=0 signals fade-out; phase still valid

            // We need freq for fade-outs too — store it alongside phase
            // Rebuild render entries with correct frequencies for fading ridges
            // (use last known freq stored in _ridgeLastFreq)
            var renderList = new List<(int id, double freq, double startAmp, double endAmp)>(renderSet.Count);
            foreach (var kv in renderSet)
            {
                int id = kv.Key;
                double targetAmp = kv.Value.targetAmp;
                double startAmp = _ridgePrevAmp.TryGetValue(id, out double prev) ? prev : 0.0;
                double freq = kv.Value.freq > 0 ? kv.Value.freq
                              : (_ridgeLastFreq.TryGetValue(id, out double lf) ? lf : 0.0);
                if (freq <= 0) continue; // no freq info, skip
                renderList.Add((id, freq, startAmp, targetAmp));
            }

            var pcmBytes = new byte[numSamples * 2];
            for (int s = 0; s < numSamples; s++)
            {
                double t = s * invN; // 0 → 1 across the chunk
                double sample = 0.0;
                foreach (var (id, freq, startAmp, endAmp) in renderList)
                {
                    double amp = startAmp + (endAmp - startAmp) * t;
                    if (amp < 1e-9) continue;
                    double phase = _ridgePhase[id] + twoPiOverSr * freq * s;
                    sample += amp * Math.Sin(phase);
                }
                short val = (short)Math.Clamp((int)(sample * 32767.0), -32767, 32767);
                pcmBytes[s * 2] = (byte)(val & 0xFF);
                pcmBytes[s * 2 + 1] = (byte)((val >> 8) & 0xFF);
            }

            // Update phase, prevAmp, and lastFreq for next frame
            foreach (var (id, freq, startAmp, endAmp) in renderList)
            {
                _ridgePhase[id] = (_ridgePhase[id] + twoPiOverSr * freq * numSamples) % (2.0 * Math.PI);
                _ridgePrevAmp[id] = endAmp;
                _ridgeLastFreq[id] = freq;
            }

            // Clean up state for ridges that have fully faded out
            var toRemove = new List<int>();
            foreach (var kv in _ridgePrevAmp)
                if (kv.Value < 1e-9 && !renderSet.ContainsKey(kv.Key))
                    toRemove.Add(kv.Key);
            foreach (var id in toRemove)
            {
                _ridgePrevAmp.Remove(id);
                _ridgePhase.Remove(id);
                _ridgeLastFreq.Remove(id);
            }

            lock (_recordLock)
            {
                if (_waveWriter != null)
                {
                    _waveWriter.Write(pcmBytes, 0, pcmBytes.Length);
                    _recordSamplesWritten += numSamples;
                }
            }
        }

        // ====================================================================
        //  FFT — precomputed twiddle table
        // ====================================================================
        private static int _twiddleN = 0;
        private static Complex[] _twiddleTable = Array.Empty<Complex>();

        private static void EnsureTwiddleTable(int n)
        {
            if (_twiddleN == n) return;
            var tbl = new Complex[n / 2];
            double invN = -2.0 * Math.PI / n;
            for (int k = 0; k < n / 2; k++)
                tbl[k] = new Complex(Math.Cos(invN * k), Math.Sin(invN * k));
            _twiddleTable = tbl;
            _twiddleN = n;
        }

        private static void FFT(Complex[] buf)
        {
            int n = buf.Length;

            // Bit-reversal permutation
            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1) j ^= bit;
                j ^= bit;
                if (i < j) (buf[i], buf[j]) = (buf[j], buf[i]);
            }

            EnsureTwiddleTable(n);
            var tbl = _twiddleTable;

            for (int len = 2; len <= n; len <<= 1)
            {
                int half = len >> 1;
                int stride = n / len;
                for (int i = 0; i < n; i += len)
                {
                    for (int j = 0; j < half; j++)
                    {
                        Complex w = tbl[j * stride];
                        Complex u = buf[i + j];
                        Complex v = buf[i + j + half] * w;
                        buf[i + j] = u + v;
                        buf[i + j + half] = u - v;
                    }
                }
            }
        }

        // ====================================================================
        //  Math helpers
        // ====================================================================
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double Clamp01(double x) => Math.Clamp(x, 0.0, 1.0);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double HzToCents(double hz, double refHz)
        {
            if (hz <= 0 || refHz <= 0) return 0;
            return 1200.0 * Math.Log2(hz / refHz);
        }

        private void lbEmail_Click(object sender, EventArgs e)
        {
            //copy the label text to clipboard 
            try
            {
                Clipboard.SetText(lbEmail.Text);
            }
            catch (Exception ex)
            {

            }
        }
    }
}