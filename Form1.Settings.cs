//Copyright (c) 2026, Erik Martin
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Spectrum
{
    public partial class Form1
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SpectrumNotes",
            "SpectrumNotes_settings.json");

        private sealed class AppSettings
        {
            // High-freq FFT — primary FFT used for all bins, better time resolution.
            [JsonPropertyName("fft_size")] public int FftSize { get; set; } = 4096;
            // Low-freq FFT — larger window for better frequency resolution below the crossover.
            [JsonPropertyName("fft_size_low")] public int FftSizeLow { get; set; } = 8192;
            // When true the low-freq FFT is computed and blended in below the crossover.
            [JsonPropertyName("low_fft_enabled")] public bool LowFftEnabled { get; set; } = true;
            // Crossover centre note (e.g. "G3") — below this the low-FFT is preferred.
            [JsonPropertyName("low_fft_crossover_note")] public string LowFftCrossoverNote { get; set; } = "C3";
            // Blend region width in semitones (total span centred on the crossover note).
            [JsonPropertyName("low_fft_crossover_semitones")] public double LowFftCrossoverSemitones { get; set; } = 6.0;
            [JsonPropertyName("overlap_factor")] public int OverlapFactor { get; set; } = 240;
            [JsonPropertyName("max_queued_frames")] public int MaxQueuedFrames { get; set; } = 20;
            [JsonPropertyName("scroll_decimate")] public int ScrollDecimate { get; set; } = 1;

            [JsonPropertyName("max_peaks")] public int MaxPeaks { get; set; } = 25;
            [JsonPropertyName("peak_min_rel")] public double PeakMinRel { get; set; } = 0.03;
            [JsonPropertyName("peak_min_spacing_cents")] public double PeakMinSpacingCents { get; set; } = 0.0;
            [JsonPropertyName("peak_gamma")] public double PeakGamma { get; set; } = 0.5;
            [JsonPropertyName("peak_mode")] public int PeakMode { get; set; } = 0;

            [JsonPropertyName("ridge_max_cents_jump")] public double RidgeMaxCentsJump { get; set; } = 250.0;
            [JsonPropertyName("ridge_miss_max")] public int RidgeMissMax { get; set; } = 10;
            [JsonPropertyName("ridge_merge_cents")] public double RidgeMergeCents { get; set; } = 20.0;
            [JsonPropertyName("ridge_merge_brightness_boost")] public double RidgeMergeBrightnessBoost { get; set; } = 2.0;
            [JsonPropertyName("ridge_merge_width_add")] public double RidgeMergeWidthAdd { get; set; } = 0.1;
            [JsonPropertyName("ridge_merge_width_decay")] public double RidgeMergeWidthDecay { get; set; } = 0.95;

            [JsonPropertyName("ridge_intensity_ema")] public double RidgeIntensityEma { get; set; } = 0.1;
            [JsonPropertyName("ridge_freq_ema")] public double RidgeFreqEma { get; set; } = 0.7;
            [JsonPropertyName("ridge_vel_ema")] public double RidgeVelEma { get; set; } = 0.7;

            [JsonPropertyName("ridge_thickness_max")] public int RidgeThicknessMax { get; set; } = 2;

            [JsonPropertyName("miss_fade_pow")] public double MissFadePow { get; set; } = 0.8;
            [JsonPropertyName("age_decay")] public double AgeDecay { get; set; } = 0.05;
            [JsonPropertyName("min_draw_alpha")] public double MinDrawAlpha { get; set; } = 0.2;
            [JsonPropertyName("level_smooth_ema")] public double LevelSmoothEma { get; set; } = 0.0;

            [JsonPropertyName("harmonic_family_cents_tol")] public double HarmonicFamilyCentsTol { get; set; } = 45.0;
            [JsonPropertyName("harmonic_family_max_ratio")] public double HarmonicFamilyMaxRatio { get; set; } = 12.0;
            [JsonPropertyName("harmonic_suppression")] public double HarmonicSuppression { get; set; } = 0.0;

            [JsonPropertyName("chord_avg_frames")] public int ChordAvgFrames { get; set; } = 12;
            [JsonPropertyName("chord_min_score")] public double ChordMinScore { get; set; } = 0.5;
            [JsonPropertyName("chord_out_penalty")] public double ChordOutPenalty { get; set; } = 0.35;
            [JsonPropertyName("chord_ridges")] public int ChordRidges { get; set; } = 15;
            [JsonPropertyName("key_ema_seconds")] public double KeyEmaSeconds { get; set; } = 10.0;
            [JsonPropertyName("key_mode_bias")] public double KeyModeBias { get; set; } = 0.75;

            // High Pass = bottom of display (lowest note shown).
            // Low Pass  = top of display (highest note shown).
            [JsonPropertyName("high_pass_note")] public string HighPassNote { get; set; } = "A1";
            [JsonPropertyName("low_pass_note")] public string LowPassNote { get; set; } = "C8";

            [JsonPropertyName("tuning")] public double Tuning { get; set; } = 440.0;

            [JsonPropertyName("target_fps")] public double TargetFps { get; set; } = 120.0;
            [JsonPropertyName("max_col_shift")] public double MaxColShift { get; set; } = 128.0;
            [JsonPropertyName("ridge_match_loghz")] public double RidgeMatchLogHz { get; set; } = 0.022;
            [JsonPropertyName("ridge_match_loghz_pred_boost")] public double RidgeMatchLogHzPredBoost { get; set; } = 1.75;
            [JsonPropertyName("show_grid_lines")] public bool ShowGridLines { get; set; } = true;
            [JsonPropertyName("use_flats")] public bool UseFlats { get; set; } = false;
            [JsonPropertyName("selected_device_name")] public string SelectedDeviceName { get; set; } = "Loopback: VoiceMeeter Input (VB-Audio VoiceMeeter VAIO)";

            // Manual sensitivity lock: when lock_volume is true the auto-gain EMA is bypassed
            // and _smoothedMaxIntensity is pinned to the value derived from volume_value.
            // volume_value maps the trackbar range (0–255) to _smoothedMaxIntensity on a log
            // scale: 0 → 1e-5 (very sensitive / dim signal still fills display),
            //       255 → 1.0  (very insensitive / only loud signals reach full brightness).
            // The trackbar default of 100 lands at roughly 5e-4, a comfortable mid-point.
            [JsonPropertyName("lock_volume")] public bool LockVolume { get; set; } = false;
            [JsonPropertyName("volume_value")] public int VolumeValue { get; set; } = 153;

            // Key / mode / auto-detect controls.
            [JsonPropertyName("scale_root")] public string ScaleRoot { get; set; } = "E";
            [JsonPropertyName("scale_mode")] public string ScaleMode { get; set; } = "Major (Ionian)";
            [JsonPropertyName("auto_detect_key")] public bool AutoDetectKey { get; set; } = true;
            [JsonPropertyName("show_circle_of_fifths")] public bool ShowCircleOfFifths { get; set; } = false;
        }

        // ── Trackbar ↔ _smoothedMaxIntensity conversion ─────────────────────
        // Log scale: trackbar 0 → 1e-5, trackbar 255 → 1.0
        private static double VolumeTrackbarToSmoothedMax(int trackbarValue)
        {
            return trackbarValue;
            double t = Math.Clamp(trackbarValue, 0, 255) / 255.0;   // 0..1
            return Math.Pow(10.0, t * 5.0 - 5.0);                   // 1e-5..1.0
        }

        // ── Apply the locked sensitivity ──────────────────────────────────────
        // Called on the UI thread from checkBox1.CheckedChanged / volume.ValueChanged.
        // Writes to volatile fields so the audio thread reads a consistent snapshot
        // without touching UI controls cross-thread.
        private void ApplyVolumeLock()
        {
            VolumeLockValue = VolumeTrackbarToSmoothedMax(volume.Value);
            _volumeLocked = cbVolume.Checked;
            volume.Enabled = _volumeLocked;
            // Also prime _smoothedMaxIntensity immediately so the very next frame
            // already uses the correct value even before the audio thread runs.
            if (_volumeLocked)
                _smoothedMaxIntensity = VolumeLockValue;
        }

        private void LoadSettings(out bool showGridLines, out string savedDeviceName, out bool useFlats)
        {
            showGridLines = false;
            savedDeviceName = "";
            useFlats = false;

            AppSettings s;
            try
            {
                if (!File.Exists(SettingsPath)) return;
                s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath));
                if (s == null) return;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Settings] Load failed: {ex.Message}");
                return;
            }

            FFT_SIZE = NextPowerOfTwo(Math.Clamp(s.FftSize, 256, 1 << 20));
            FFT_SIZE_L = NextPowerOfTwo(Math.Clamp(s.FftSizeLow, 256, 1 << 20));
            _lowFftEnabled = s.LowFftEnabled;
            if (TryParseNoteToMidi(s.LowFftCrossoverNote ?? "G3", out int xoMidi))
            {
                LOW_FFT_CROSSOVER_NOTE = MidiToNoteName(xoMidi);
                LOW_FFT_CROSSOVER_HZ = MidiToHz(xoMidi);
            }
            LOW_FFT_CROSSOVER_SEMITONES = Math.Clamp(s.LowFftCrossoverSemitones, 0.0, 48.0);
            HOP_SIZE = Math.Clamp(s.OverlapFactor, 1, 1 << 20);
            TARGET_FPS = Math.Clamp(s.TargetFps, 0.0, 10000.0);
            MAX_COL_SHIFT = Math.Clamp(s.MaxColShift, 0.0, 1e6);
            RIDGE_MATCH_LOGHZ = Math.Clamp(s.RidgeMatchLogHz, 0.001, 1.0);
            RIDGE_MATCH_LOGHZ_PRED_BOOST = Math.Clamp(s.RidgeMatchLogHzPredBoost, 0.0, 10.0);
            MAX_QUEUED_FRAMES = Math.Clamp(s.MaxQueuedFrames, 1, 256);

            MAX_PEAKS_PER_FRAME = Math.Clamp(s.MaxPeaks, 1, 2000);
            PEAK_MIN_REL = Math.Clamp(s.PeakMinRel, 0.0, 1.0);
            PEAK_MIN_SPACING_CENTS = Math.Clamp(s.PeakMinSpacingCents, 0.0, 200.0);
            PEAK_GAMMA = Math.Clamp(s.PeakGamma, 0.01, 10.0);
            PEAK_MODE = Math.Clamp(s.PeakMode, 0, 2000);

            RIDGE_MAX_CENTS_JUMP = Math.Clamp(s.RidgeMaxCentsJump, 0.0, 5000.0);
            RIDGE_MISS_MAX = Math.Clamp(s.RidgeMissMax, 0, 500);
            RIDGE_MERGE_CENTS = Math.Clamp(s.RidgeMergeCents, 0.0, 200.0);
            RIDGE_MERGE_BRIGHTNESS_BOOST = Math.Clamp(s.RidgeMergeBrightnessBoost, 0.0, 5.0);
            RIDGE_MERGE_WIDTH_ADD = Math.Clamp(s.RidgeMergeWidthAdd, 0.0, 20.0);
            RIDGE_MERGE_WIDTH_DECAY = Math.Clamp(s.RidgeMergeWidthDecay, 0.0, 1.0);

            RIDGE_FREQ_EMA = Math.Clamp(s.RidgeFreqEma, 0.0, 1.0);
            RIDGE_VEL_EMA = Math.Clamp(s.RidgeVelEma, 0.0, 1.0);
            RIDGE_INTENSITY_EMA = Math.Clamp(s.RidgeIntensityEma, 0.0, 1.0);

            RidgeMissFadePow = Math.Clamp(s.MissFadePow, 0.01, 1.0);
            RidgeAgeDecay = Math.Clamp(s.AgeDecay, 0.01, 1.0);
            MinDrawAlpha = Math.Clamp(s.MinDrawAlpha, 0.0, 1.0);
            LEVEL_SMOOTH_EMA = Math.Clamp(s.LevelSmoothEma, 0.0, 1.0);

            HARMONIC_FAMILY_CENTS_TOL = Math.Clamp(s.HarmonicFamilyCentsTol, 0.0, 200.0);
            HARMONIC_FAMILY_MAX_RATIO = Math.Clamp(s.HarmonicFamilyMaxRatio, 1.0, 128.0);
            HARMONIC_SUPPRESSION = Math.Clamp(s.HarmonicSuppression, 0.0, 1.0);

            CHORD_AVG_FRAMES = Math.Clamp(s.ChordAvgFrames, 1, 120);
            CHORD_OUT_PENALTY = Math.Clamp(s.ChordOutPenalty, 0.0, 10.0);
            CHORD_RIDGES = Math.Clamp(s.ChordRidges, 0, 15);
            KEY_EMA_SECONDS = Math.Clamp(s.KeyEmaSeconds, 0.5, 600.0);
            KEY_MODE_BIAS = Math.Clamp(s.KeyModeBias, 0.0, 1.0);

            if (TryParseNoteToMidi(s.HighPassNote, out int hpMidi))
            {
                hpMidi = ClampMidiToC0C8(hpMidi);
                HighPassNote = MidiToNoteName(hpMidi);
                HighPass = MidiToHz(hpMidi);
            }
            if (TryParseNoteToMidi(s.LowPassNote, out int lpMidi))
            {
                lpMidi = ClampMidiToC0C8(lpMidi);
                LowPassNote = MidiToNoteName(lpMidi);
                LowPass = MidiToHz(lpMidi);
            }

            tuning = Math.Clamp(s.Tuning, 300.0, 600.0);

            showGridLines = s.ShowGridLines;
            useFlats = s.UseFlats;
            savedDeviceName = s.SelectedDeviceName ?? "";

            // Restore volume lock state.  Set the UI controls first, then update the
            // volatile backing fields so the audio thread gets a consistent snapshot.
            volume.Value = Math.Clamp(s.VolumeValue, volume.Minimum, volume.Maximum);
            cbVolume.Checked = s.LockVolume;
            VolumeLockValue = VolumeTrackbarToSmoothedMax(volume.Value);
            _volumeLocked = s.LockVolume;
            if (s.LockVolume)
                _smoothedMaxIntensity = VolumeLockValue;

            // Restore key / mode / auto-detect.  The combo boxes must already be
            // populated before LoadSettings is called (i.e. items added before this call).
            if (!string.IsNullOrEmpty(s.ScaleRoot))
            {
                int rootIdx = cmbScaleRoot.FindStringExact(s.ScaleRoot);
                if (rootIdx >= 0) cmbScaleRoot.SelectedIndex = rootIdx;
            }
            if (!string.IsNullOrEmpty(s.ScaleMode))
            {
                int modeIdx = cmbScaleMode.FindStringExact(s.ScaleMode);
                if (modeIdx >= 0) cmbScaleMode.SelectedIndex = modeIdx;
            }
            cbAutoKey.Checked = s.AutoDetectKey;
            cbShowCircle.Checked = s.ShowCircleOfFifths;
        }

        private void SaveSettings()
        {
            try
            {
                var s = new AppSettings
                {
                    FftSize = FFT_SIZE,
                    FftSizeLow = FFT_SIZE_L,
                    LowFftEnabled = _lowFftEnabled,
                    LowFftCrossoverNote = LOW_FFT_CROSSOVER_NOTE,
                    LowFftCrossoverSemitones = LOW_FFT_CROSSOVER_SEMITONES,
                    OverlapFactor = HOP_SIZE,
                    TargetFps = TARGET_FPS,
                    MaxColShift = MAX_COL_SHIFT,
                    RidgeMatchLogHz = RIDGE_MATCH_LOGHZ,
                    RidgeMatchLogHzPredBoost = RIDGE_MATCH_LOGHZ_PRED_BOOST,
                    MaxQueuedFrames = MAX_QUEUED_FRAMES,

                    MaxPeaks = MAX_PEAKS_PER_FRAME,
                    PeakMinRel = PEAK_MIN_REL,
                    PeakMinSpacingCents = PEAK_MIN_SPACING_CENTS,
                    PeakGamma = PEAK_GAMMA,
                    PeakMode = PEAK_MODE,

                    RidgeMaxCentsJump = RIDGE_MAX_CENTS_JUMP,
                    RidgeMissMax = RIDGE_MISS_MAX,
                    RidgeMergeCents = RIDGE_MERGE_CENTS,
                    RidgeMergeBrightnessBoost = RIDGE_MERGE_BRIGHTNESS_BOOST,
                    RidgeMergeWidthAdd = RIDGE_MERGE_WIDTH_ADD,
                    RidgeMergeWidthDecay = RIDGE_MERGE_WIDTH_DECAY,

                    RidgeFreqEma = RIDGE_FREQ_EMA,
                    RidgeVelEma = RIDGE_VEL_EMA,
                    RidgeIntensityEma = RIDGE_INTENSITY_EMA,

                    MissFadePow = RidgeMissFadePow,
                    AgeDecay = RidgeAgeDecay,
                    MinDrawAlpha = MinDrawAlpha,
                    LevelSmoothEma = LEVEL_SMOOTH_EMA,

                    HarmonicFamilyCentsTol = HARMONIC_FAMILY_CENTS_TOL,
                    HarmonicFamilyMaxRatio = HARMONIC_FAMILY_MAX_RATIO,
                    HarmonicSuppression = HARMONIC_SUPPRESSION,

                    ChordAvgFrames = CHORD_AVG_FRAMES,
                    ChordOutPenalty = CHORD_OUT_PENALTY,
                    ChordRidges = CHORD_RIDGES,
                    KeyEmaSeconds = KEY_EMA_SECONDS,
                    KeyModeBias = KEY_MODE_BIAS,

                    HighPassNote = HighPassNote,
                    LowPassNote = LowPassNote,

                    Tuning = tuning,
                    ShowGridLines = cblines.Checked,
                    UseFlats = cbFlats.Checked,
                    SelectedDeviceName = (comboBox1.SelectedItem as DeviceItem)?.Name ?? "",

                    LockVolume = cbVolume.Checked,
                    VolumeValue = volume.Value,

                    ScaleRoot = cmbScaleRoot.SelectedItem?.ToString() ?? "",
                    ScaleMode = cmbScaleMode.SelectedItem?.ToString() ?? "",
                    AutoDetectKey = cbAutoKey.Checked,
                    ShowCircleOfFifths = cbShowCircle.Checked,
                };

                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
                File.WriteAllText(SettingsPath,
                    JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Settings] Save failed: {ex.Message}");
            }
        }
    }
}
