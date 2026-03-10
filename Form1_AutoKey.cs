//Copyright (c) 2026, Erik Martin
// Auto key/mode detection using ridge-weighted long-term EMA chroma profile.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace Spectrum
{
    public partial class Form1
    {
        static Form1()
        {
            InitDiatonicRemap();
        }

        private double KEY_EMA_SECONDS = 20.0;
        private double KEY_MODE_BIAS = 0.75;

        /// <summary>
        /// Fraction by which a ridge's key-EMA contribution is reduced when it
        /// appears to be a harmonic overtone of a stronger ridge.  0 = no
        /// suppression; 1 = full suppression.
        /// </summary>
        private double KEY_HARMONIC_SUPPRESSION = 0.5;

        /// <summary>
        /// Weight of the coverage-confidence term in the final composite score.
        /// 0 = pure Pearson/KK correlation; 1 = pure coverage.
        /// </summary>
        private double KEY_COVERAGE_WEIGHT = 0.55;

        /// <summary>
        /// Score bonus added to the current key candidate each detection cycle.
        /// A challenger must outscore the current key by at least this margin to
        /// trigger a switch. Prevents momentary chords (e.g. D in a C-G-D
        /// progression) from flipping the detected key. ~0.05–0.10 is a good range.
        /// </summary>
        private double KEY_HYSTERESIS = 0.05;

        /// <summary>
        /// Maximum peak-bin share of total EMA energy at which key detection is
        /// suppressed (returns "no key"). A single chord peaks at ~0.35–0.45 since
        /// 3 notes dominate; after several chords the spread drops below ~0.25.
        /// Set to 0 to disable the gate entirely.
        /// </summary>
        private double KEY_MIN_CONFIDENCE = 0.30;

        private int _hysteresisRoot = -1;
        private int _hysteresisMode = -1;

        private readonly double[] _keyEma = new double[12];

        private struct KeyRidgeEntry { public double Hz; public double W; }
        private KeyRidgeEntry[] _keyRidgeScratch = new KeyRidgeEntry[MaxActiveRidges];
        private int _keyRidgeScratchCount;

        private static readonly double[] KKMajor =
        {
            6.35, 2.23, 3.48, 2.33, 4.38, 4.09,
            2.52, 5.19, 2.39, 3.66, 2.29, 2.88
        };
        private static readonly double[] KKMinor =
        {
            6.33, 2.68, 3.52, 5.38, 2.60, 3.53,
            2.54, 4.75, 3.98, 2.69, 3.34, 3.17
        };

        private static double[] NormalizeProfile(double[] p)
        {
            double mean = 0; foreach (var v in p) mean += v; mean /= p.Length;
            double norm = 0; foreach (var v in p) norm += (v - mean) * (v - mean);
            norm = Math.Sqrt(norm);
            var r = new double[p.Length];
            for (int i = 0; i < p.Length; i++) r[i] = norm > 1e-12 ? (p[i] - mean) / norm : 0;
            return r;
        }

        private static readonly double[] KKMajorNorm = NormalizeProfile(KKMajor);
        private static readonly double[] KKMinorNorm = NormalizeProfile(KKMinor);

        private static readonly Dictionary<int, int> _diatonicToMajorOffset
            = new Dictionary<int, int>();

        private static readonly int[] MajorIntervals = { 0, 2, 4, 5, 7, 9, 11 };

        private static void InitDiatonicRemap()
        {
            var majorSet = new HashSet<int>(MajorIntervals);
            int[] diatonicIndices = { 0, 1, 2, 3, 4, 5, 6 };

            foreach (int mi in diatonicIndices)
            {
                var ivs = _modeIntervals[mi];
                if (ivs.Length != 7) continue;

                for (int d = 0; d < 12; d++)
                {
                    var shifted = new HashSet<int>(ivs.Select(iv => (iv + d) % 12));
                    if (shifted.SetEquals(majorSet))
                    {
                        _diatonicToMajorOffset[mi] = d;
                        break;
                    }
                }
            }
        }

        private void UpdateKeyEmaFromRidges()
        {
            _keyRidgeScratchCount = 0;
            lock (ridgeLock)
            {
                int count = ridges.Count;
                if (_keyRidgeScratch.Length < count)
                    _keyRidgeScratch = new KeyRidgeEntry[count * 2];

                for (int i = 0; i < count; i++)
                {
                    var r = ridges[i];
                    if (r.Miss > 2) continue;
                    if (r.Intensity < 0.01) continue;

                    double hz = Math.Exp(r.LogFreq + 0.5 * r.LogVel);
                    if (hz < 20.0 || hz > 20000.0) continue;

                    // Sustain bonus: ramp from 0.65→1.0 over first 32 frames.
                    double sustainBoost = 0.65 + 0.35 * (1.0 - Math.Pow(RidgeAgeDecay, Math.Min(32, r.Age)));
                    double w = r.Intensity * sustainBoost;

                    _keyRidgeScratch[_keyRidgeScratchCount++] = new KeyRidgeEntry { Hz = hz, W = w };
                }
            }

            if (_keyRidgeScratchCount == 0) return;

            // Harmonic deduplication: identify and suppress harmonics of stronger ridges
            double harmTolLog = 25.0 * Math.Log2(2.0) / 1200.0 * Math.Log(2.0);
            double suppFactor = 1.0 - Math.Clamp(KEY_HARMONIC_SUPPRESSION, 0.0, 1.0);

            // Sort by descending weight
            for (int i = 1; i < _keyRidgeScratchCount; i++)
            {
                var tmp = _keyRidgeScratch[i];
                int j = i - 1;
                while (j >= 0 && _keyRidgeScratch[j].W < tmp.W)
                {
                    _keyRidgeScratch[j + 1] = _keyRidgeScratch[j];
                    j--;
                }
                _keyRidgeScratch[j + 1] = tmp;
            }

            // Check if each ridge is a harmonic multiple of a stronger one
            for (int i = 1; i < _keyRidgeScratchCount; i++)
            {
                double lnHz_i = Math.Log(_keyRidgeScratch[i].Hz);
                bool isHarmonic = false;
                for (int j = 0; j < i && !isHarmonic; j++)
                {
                    double lnHz_j = Math.Log(_keyRidgeScratch[j].Hz);
                    if (lnHz_i <= lnHz_j) continue;
                    double ratio = _keyRidgeScratch[i].Hz / _keyRidgeScratch[j].Hz;
                    for (int h = 2; h <= 6; h++)
                    {
                        if (Math.Abs(Math.Log(ratio / h)) < harmTolLog)
                        {
                            isHarmonic = true;
                            break;
                        }
                    }
                }
                if (isHarmonic)
                    _keyRidgeScratch[i].W *= suppFactor;
            }

            // Build pitch-class accumulation with fractional distribution
            Span<double> pcAccum = stackalloc double[12];
            double totalW = 0.0;

            for (int i = 0; i < _keyRidgeScratchCount; i++)
            {
                double hz = _keyRidgeScratch[i].Hz;
                double w = _keyRidgeScratch[i].W;

                double midi = 69.0 + 12.0 * Math.Log2(hz / tuning);

                // Distribute weight between two nearest pitch classes
                double midiMod12 = ((midi % 12.0) + 12.0) % 12.0;
                int pcLow = (int)Math.Floor(midiMod12) % 12;
                int pcHigh = (pcLow + 1) % 12;
                double fracHigh = midiMod12 - Math.Floor(midiMod12);
                double fracLow = 1.0 - fracHigh;

                pcAccum[pcLow] += w * fracLow;
                pcAccum[pcHigh] += w * fracHigh;
                totalW += w;
            }

            if (totalW < 1e-9) return;

            for (int i = 0; i < 12; i++) pcAccum[i] /= totalW;

            double alpha = KeyEmaAlpha;
            lock (chordLock)
            {
                for (int i = 0; i < 12; i++)
                    _keyEma[i] = alpha * pcAccum[i] + (1.0 - alpha) * _keyEma[i];
            }
        }

        [Obsolete("Use UpdateKeyEmaFromRidges() instead")]
        private void UpdateKeyEma(double[] chroma) { }

        private double KeyEmaAlpha
        {
            get
            {
                double fps = TARGET_FPS > 1.0 ? TARGET_FPS : 60.0;
                double halfLifeFrames = KEY_EMA_SECONDS * fps;
                return halfLifeFrames > 0.5 ? 1.0 - Math.Pow(0.5, 1.0 / halfLifeFrames) : 1.0;
            }
        }

        private void ResetKeyEma()
        {
            lock (chordLock) { Array.Clear(_keyEma, 0, 12); }
            _hysteresisRoot = -1;
            _hysteresisMode = -1;
        }

        private (int rootComboIndex, int modeComboIndex) DetectKey()
        {
            double[] snap = new double[12];
            lock (chordLock) { Array.Copy(_keyEma, snap, 12); }

            double totalEnergy = 0.0;
            for (int i = 0; i < 12; i++) totalEnergy += snap[i];
            if (totalEnergy < 1e-9) return (0, 0);

            // Don't commit to a key until the EMA has accumulated enough evidence.
            // A single chord produces a peaked but thin EMA; after several chords
            // the max/mean ratio becomes more reliable. Gate on the peak bin's
            // share: if the strongest pitch class dominates too heavily, we haven't
            // seen enough variety to disambiguate keys that share most of their notes.
            double maxBin = 0.0;
            for (int i = 0; i < 12; i++) if (snap[i] > maxBin) maxBin = snap[i];
            double peakShare = maxBin / totalEnergy;  // 1/12 = flat, 1.0 = single note
            if (peakShare > KEY_MIN_CONFIDENCE) return (0, 0);

            // Normalize for Pearson correlation (zero-mean, unit-length)
            double[] snapNorm = new double[12];
            double mean = totalEnergy / 12.0;
            double norm = 0.0;
            for (int i = 0; i < 12; i++) { double v = snap[i] - mean; norm += v * v; }
            norm = Math.Sqrt(norm);
            if (norm < 1e-9) return (0, 0);
            for (int i = 0; i < 12; i++) snapNorm[i] = (snap[i] - mean) / norm;

            double[] snapRaw = new double[12];
            for (int i = 0; i < 12; i++) snapRaw[i] = snap[i] / totalEnergy;

            double bestScore = double.NegativeInfinity;
            int bestRoot = 0;
            int bestMode = 0;

            double bias = Math.Clamp(KEY_MODE_BIAS, 0.0, 1.0);
            double coverageWt = Math.Clamp(KEY_COVERAGE_WEIGHT, 0.0, 1.0);

            for (int root = 0; root < 12; root++)
            {
                for (int mi = 0; mi < _modeIntervals.Length; mi++)
                {
                    double pearson = ScoreKeyCandidate(snapNorm, root, mi);
                    double coverage = CoverageScore(snapRaw, root, mi);
                    double score = (1.0 - coverageWt) * pearson + coverageWt * coverage;

                    // Hysteresis: give the current key a bonus so a challenger must
                    // clearly outscore it, not just edge it out on a momentary chord.
                    if (root == _hysteresisRoot && mi == _hysteresisMode)
                        score += KEY_HYSTERESIS;

                    // Bias toward Major and Natural Minor
                    if (mi == 0 || mi == 5)
                        score += bias * (1.0 - score) * 0.5;
                    else if (mi == 9 || mi == 10 || mi == 11)
                        score += bias * 0.25 * (1.0 - score) * 0.5;

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestRoot = root;
                        bestMode = mi;
                    }
                }
            }

            _hysteresisRoot = bestRoot;
            _hysteresisMode = bestMode;

            // For diatonic modes, remap to major if characteristic note is absent
            if (bestMode != 0 && bestMode != 5 &&
                _diatonicToMajorOffset.TryGetValue(bestMode, out int offset))
            {
                double charEnergy = CharacteristicNoteEnergy(snapRaw, bestRoot, bestMode);
                if (charEnergy <= KEY_MODE_CHAR_THRESHOLD)
                {
                    bestRoot = (bestRoot - offset + 12) % 12;
                    bestMode = 0;
                }
            }

            return (bestRoot + 1, bestMode);
        }

        /// <summary>
        /// Minimum fraction of total EMA energy that a mode's characteristic
        /// note must carry to confirm the mode. At uniform distribution, each
        /// pitch class holds ~0.083; 0.06 means the note must be at least
        /// slightly below average.
        /// </summary>
        private double KEY_MODE_CHAR_THRESHOLD = 0.06;

        /// <summary>
        /// Returns the maximum EMA energy for the pitch classes that most strongly
        /// distinguish the given diatonic mode from its parallel major scale.
        /// </summary>
        private static double CharacteristicNoteEnergy(double[] snapRaw, int root, int modeIndex)
        {
            ReadOnlySpan<int> chars = modeIndex switch
            {
                1 => stackalloc int[] { 3, 9 },
                2 => stackalloc int[] { 1 },
                3 => stackalloc int[] { 6 },
                4 => stackalloc int[] { 10 },
                6 => stackalloc int[] { 1, 6 },
                _ => stackalloc int[] { }
            };

            double maxEnergy = 0.0;
            foreach (int semitone in chars)
            {
                int pc = (root + semitone) % 12;
                if (snapRaw[pc] > maxEnergy) maxEnergy = snapRaw[pc];
            }
            return maxEnergy;
        }

        /// <summary>
        /// Measures how well the EMA chroma matches the given scale in terms of
        /// note presence. Returns ~[-1, 1] where +1 means all scale notes are
        /// present with no out-of-scale energy, and -1 means all energy is
        /// out-of-scale.
        /// </summary>
        private static double CoverageScore(double[] snapRaw, int root, int modeIndex)
        {
            // Nonlinear (squared) out-of-scale penalty: notes with substantial
            // accumulated EMA energy are penalized quadratically, so a tone that
            // appears consistently (e.g. F# from repeated D chords) hurts much more
            // than a trace/passing note (e.g. F# lingering briefly from one chord).
            // This lets the long-term EMA correctly identify the key even when both
            // the key-confirming and key-excluding notes are present in the mix.
            //
            // At e=0.04 (trace):    out contribution ≈ 0.05
            // At e=0.12 (real):     out contribution ≈ 0.23
            // At e=0.20 (dominant): out contribution ≈ 0.52

            var intervals = _modeIntervals[modeIndex];
            var inSet = new bool[12];
            foreach (int iv in intervals) inSet[(root + iv) % 12] = true;

            double threshold = 1.0 / (intervals.Length * 3.0);

            double inScore = 0.0;
            double outScore = 0.0;
            for (int pc = 0; pc < 12; pc++)
            {
                double e = snapRaw[pc];
                if (inSet[pc])
                    inScore += e > threshold ? e : e * 0.5;
                else
                    outScore += e + 8.0 * e * e;
            }

            return inScore - outScore;
        }

        private static double ScoreKeyCandidate(double[] snapNorm, int root, int modeIndex)
        {
            if (modeIndex == 0) return PearsonWithProfile(snapNorm, root, KKMajorNorm);
            if (modeIndex == 5) return PearsonWithProfile(snapNorm, root, KKMinorNorm);

            // For other modes, use flat binary presence profile
            var intervals = _modeIntervals[modeIndex];
            Span<double> profile = stackalloc double[12];
            foreach (int iv in intervals) profile[(root + iv) % 12] = 1.0;

            double m = 0;
            for (int i = 0; i < 12; i++) m += profile[i];
            m /= 12;

            double n2 = 0;
            for (int i = 0; i < 12; i++) { double v = profile[i] - m; n2 += v * v; }
            double nn = Math.Sqrt(n2);
            if (nn < 1e-12) return 0;
            for (int i = 0; i < 12; i++) profile[i] = (profile[i] - m) / nn;

            double dot = 0;
            for (int i = 0; i < 12; i++) dot += snapNorm[i] * profile[i];
            return dot;
        }

        private static double PearsonWithProfile(double[] snapNorm, int root, double[] profileNorm)
        {
            double dot = 0;
            for (int i = 0; i < 12; i++)
                dot += snapNorm[i] * profileNorm[(i - root + 12) % 12];
            return dot;
        }

        private void ApplyAutoKeyDetection()
        {
            if (!cbAutoKey.Checked) return;

            var (rootIdx, modeIdx) = DetectKey();

            _autoKeyUpdating = true;
            try
            {
                if (cmbScaleRoot.SelectedIndex != rootIdx)
                    cmbScaleRoot.SelectedIndex = rootIdx;
                if (rootIdx > 0 && cmbScaleMode.SelectedIndex != modeIdx)
                    cmbScaleMode.SelectedIndex = modeIdx;
            }
            finally
            {
                _autoKeyUpdating = false;
            }

            OnScaleSelectionChanged(this, EventArgs.Empty);
        }

        private bool _autoKeyUpdating = false;

        private void SetChordLabelUi(string text, string canonical = "", string detectedNotes = null, string harmonics = null)
        {
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

            // Keep Circle of Fifths state in sync.
            UpdateCircleOfFifths(text);

            if (!IsDisposed && IsHandleCreated && UiThrottleReady(ref _lastChordTick))
            {
                try
                {
                    BeginInvoke((Action)(() =>
                    {
                        ApplyAutoKeyDetection();
                        if (!chordlabel.IsDisposed) _chordLabelAction?.Invoke();
                    }));
                }
                catch (Exception ex) { RethrowUnexpectedException(ex); }
            }
        }
    }
}
