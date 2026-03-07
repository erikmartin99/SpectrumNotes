//Copyright (c) 2026, Erik Martin
// Contains only RegisterParamHints(), called once from the Form1 constructor
// immediately after InitializeComponent().  All ParamHint.Register() calls live
// here so the Designer never touches them.
namespace Spectrum
{
    public partial class Form1
    {
        private void RegisterParamHints()
        {
            // ── Display range ────────────────────────────────────────────────
            ParamHint.Register(lblAdd9MinNinthEnergy, new ParamHintInfo(
                "Sets the lower frequency boundary of the spectrogram display and analysis range. Use standard note notation, e.g. C1.",
                "C0", "Captures sub-bass and the lowest piano register; requires a large low-freq FFT for useful resolution down there.",
                "C3", "Focuses on mid-range and up; saves CPU and keeps the display uncluttered when bass content is not needed."));

            ParamHint.Register(label4, new ParamHintInfo(
                "Sets the upper frequency boundary of the spectrogram display and analysis range. Use standard note notation, e.g. C8.",
                "C6", "Covers most instruments without wasting bins on inaudible ultrasonics; lighter memory footprint.",
                "C9", "Extends into the very top of the piano and the upper harmonics of high-pitched overtones."));

            // ── FFT ──────────────────────────────────────────────────────────
            ParamHint.Register(lblFFTSize, new ParamHintInfo(
                "Enter the exponent n so that the high-freq FFT window = 2^n (e.g. 13 → 8192 samples). The low-freq FFT is automatically set to 2^(n+1). Must be 8–20.",
                "11  (2048)", "Fast attack response; good for when you need high time resolution, for example to see quick vocal runs, though with lower pitch resolution.",
                "14  (16384)", "Very precise frequency resolution, but without thhe movement detail. Best for use with the tuner."));

            ParamHint.Register(cbLowFft, new ParamHintInfo(
                "When checked, the low-frequencyuency FFT is computed and blended in below the crossover note. Uncheck to use only the high-freq FFT.",
                "Unchecked", "Saves roughly 30–50 % CPU; acceptable when bass content is not a focus of analysis or when speed is more important.",
                "Checked", "Enables superior bass note separation; recommended for piano, organ, or any bass-heavy material."));

            ParamHint.Register(lblLowFftCrossoverNote, new ParamHintInfo(
                "Centre note of the blend region between high and low FFTs. Below this note the low-freq FFT dominates; above it the high-freq FFT dominates.",
                "C2", "Hands most of the bass register to the low FFT; good for upright bass or very low instruments only.",
                "C5", "Lets the low FFT handle a wide mid-range; useful if the primary FFT is too small for accurate mid-register pitch resolution."));

            ParamHint.Register(lblLowFftCrossoverSemitones, new ParamHintInfo(
                "Total width of the crossover blend region in semitones, centred on the crossover note. E.g. 12 = one octave blend zone.",
                "2", "Hard crossover; can show a visible seam if the two FFTs disagree on brightness at the boundary.",
                "24", "Wide two-octave fade; eliminates seams but spreads the benefit of each FFT across a broader range."));

            // ── Timing / scroll ──────────────────────────────────────────────
            ParamHint.Register(lblTargetFps, new ParamHintInfo(
                "Desired analysis and display frames per second (scroll speed). ",
                "20", "Light CPU; spectrogram scrolls slowly and ridge response is sluggish but sustainable on slower machines.",
                "120", "Very high rate; smooth scrolling and fast note-attack detection — demands significant CPU, especially with large FFT sizes."));

            ParamHint.Register(lblOverlapFactor, new ParamHintInfo(
                "Read-only. Hop size in samples, computed as sampleRate ÷ Target FPS. Shows how far audio advances between successive analysis frames.",
                "small (≈ 100–400)", "High frame rate; smooth scrolling and fast response but heavier CPU load.",
                "large (≈ 1000+)", "Low frame rate; reduces CPU but makes the display choppy and slows ridge response."));


            ParamHint.Register(lblMaxColShift, new ParamHintInfo(
                "Clamps the time-reassignment offset in samples before it is scaled to column pixels. Prevents peaks scattering too far horizontally at large FFT sizes.",
                "32", "Very tight clamping; peaks cluster near the frame boundary — sharp dot display but reduced reassignment benefit.",
                "512", "Wide clamping; offsets up to 512 samples are honoured — peaks shift further toward their true temporal location."));

            // ── Auto-gain ────────────────────────────────────────────────────
            ParamHint.Register(lblLevelSmoothEma, new ParamHintInfo(
                "EMA alpha applied to the running peak-magnitude reference before peak extraction. Lower = slower auto-gain response, more stable brightness.",
                "0.002", "Very slow auto-gain; brightness stays stable during loud transients but takes several seconds to settle at a new volume level.",
                "0.1", "Fast auto-gain; display brightness snaps quickly to volume changes but may flutter during dynamic passages."));

            // ── Peaks ────────────────────────────────────────────────────────
            ParamHint.Register(lblMaxPeaksPerFrame, new ParamHintInfo(
                "Hard cap on spectral peaks extracted each frame. Raise to capture more simultaneous notes; lower to reduce CPU and suppress false ridges.",
                "5", "Sparse polyphony only; ideal for solo or monophonic instruments where harmonics would otherwise spawn phantom ridges.",
                "30", "Dense polyphony such as full piano chords or orchestral material; risks more CPU use and more phantom ridges from noise."));

            ParamHint.Register(lblPeakMinRel, new ParamHintInfo(
                "Amplitude threshold relative to the loudest peak in the frame. Peaks below this fraction are discarded before ridge matching.",
                "0.01", "Very inclusive; captures quiet overtones and soft background notes but increases noise and false ridge count.",
                "0.15", "Strict; only strong peaks survive — cleaner display suited to loud, well-separated sources."));

            ParamHint.Register(lblPeakMinSpacingCents, new ParamHintInfo(
                "Minimum pitch distance between two retained peaks. Closer peaks are thinned to the stronger one, preventing one note spawning duplicate ridges.",
                "10", "Allows closely spaced peaks to coexist; useful for microtonal content or chorus and detune effects.",
                "50", "Half-semitone guard zone; aggressively removes near-duplicate peaks caused by a single note's spectral cluster."));

            ParamHint.Register(lblPeakGamma, new ParamHintInfo(
                "Power-law exponent applied to ridge brightness before rendering. Below 1 brightens dim ridges; above 1 increases contrast.",
                "0.4", "Lifts quiet notes to near-full brightness; good for exploring soft harmonics but compresses dynamic range.",
                "1.5", "High contrast; loud notes blaze while quiet ones fade — useful when only the most prominent pitches should be visible."));

            ParamHint.Register(lblPeakMode, new ParamHintInfo(
                "Selects the peak-picking algorithm variant. 0 = standard parabolic interpolation. Other values enable experimental alternatives.",
                "0", "Standard parabolic peak interpolation; stable and accurate for most polyphonic material.",
                "2", "Experimental mode; may improve sub-cent accuracy for sustained pure tones but less tested on complex polyphony."));

            // ── Ridge tracking ───────────────────────────────────────────────
            ParamHint.Register(lblRidgeMaxCentsJump, new ParamHintInfo(
                "Maximum pitch shift allowed between consecutive frames for a ridge to be considered continuous. Larger = ridges survive fast glides; smaller = fewer wrong-note merges.",
                "50", "Very strict; only near-stationary pitches form ridges — good for tuning analysis of steady tones.",
                "700", "Allows jumps up to ~7 semitones per frame; keeps ridges alive through rapid slides and aggressive vibrato."));

            ParamHint.Register(lblRidgeMissMax, new ParamHintInfo(
                "How many consecutive frames a ridge can go unmatched before it is killed. Higher = ridges survive brief gaps; lower = phantom trails clean up faster.",
                "5", "Fast cleanup; ridges vanish quickly after a note ends — good for staccato or clean rhythmic content.",
                "60", "Long survival; ridges persist through vibrato troughs, microphone dropouts, or heavily reverberant decay tails."));

            ParamHint.Register(lblRidgeMatchLogHz, new ParamHintInfo(
                "Base search radius in log-Hz units for assigning new peaks to existing ridges. Increase if fast-moving notes drop out; decrease if adjacent notes merge incorrectly.",
                "0.008", "Very tight gate; each ridge tracks only near-stationary pitches — ideal for stable tuning analysis.",
                "0.05", "Wide gate; ridges latch onto peaks up to ~85 cents away — helps track fast vibrato but risks cross-capture between adjacent notes."));

            ParamHint.Register(lblRidgeMatchLogHzPredBoost, new ParamHintInfo(
                "Multiplier that widens the match gate in the direction a ridge is already moving. Helps track fast pitch glides without widening the gate in all directions.",
                "1.0", "No directional boost; the predictor uses the same radius in all directions — safe and conservative.",
                "4.0", "Strong forward-prediction; gate extends far ahead of the motion direction — excellent for fast portamento but may cause runaway drift on noisy estimates."));

            // ── Ridge EMA ────────────────────────────────────────────────────
            ParamHint.Register(lblRidgeFreqEMA, new ParamHintInfo(
                "EMA alpha for the ridge's tracked frequency. Higher = faster response to pitch changes; lower = smoother but slower to follow bends and vibrato.",
                "0.1", "Heavy smoothing; drawn pitch barely moves even through aggressive vibrato — good for stable chord label display.",
                "0.9", "Near-instant tracking; the ridge follows every micro-fluctuation — best for portamento or glide visualisation."));

            ParamHint.Register(lblRidgeVelEMA, new ParamHintInfo(
                "EMA alpha for the ridge's pitch velocity in cents/frame. Used by the predictor to widen the match gate in the direction of motion.",
                "0.05", "Predictor reacts slowly; safe for most material, avoids over-steering on noisy pitch estimates.",
                "0.5", "Predictor reacts quickly; helps track fast portamento or violin slides but can mis-steer on abrupt note changes."));

            ParamHint.Register(lblRidgeIntensityEMA, new ParamHintInfo(
                "EMA alpha for the ridge's loudness. Low = long brightness tail after a note ends; high = tighter tracking of transients.",
                "0.02", "Very long brightness decay; notes glow for many frames — creates a visually rich trail on sustained instruments.",
                "0.5", "Fast brightness tracking; note onsets flash brightly and die quickly — ideal for percussive or staccato material."));

            // ── Ridge fade / age ─────────────────────────────────────────────
            ParamHint.Register(lblMissFadePow, new ParamHintInfo(
                "Exponent controlling how quickly a ridge dims while unmatched. Closer to 1 = stays bright longer; near 0 = rapid dimming.",
                "0.2", "Aggressive dimming; unmatched ridges vanish almost immediately — clean display with minimal ghosting.",
                "0.95", "Slow fade; ridges stay near full brightness throughout their miss tolerance — good for legato or reverb-heavy sources."));

            ParamHint.Register(lblAgeDecay, new ParamHintInfo(
                "Fade-in rate for newly born ridges. Each frame the alpha is multiplied by this factor until it reaches 1. Lower = slower fade-in; 1 = instant full brightness.",
                "0.5", "Slow two-frame fade-in; new ridges emerge gently, reducing flickering from single-frame noise spikes.",
                "1.0", "Instant full-brightness appearance; every detected peak is immediately visible — useful for fast transient tracking."));

            ParamHint.Register(lblMinDrawAlpha, new ParamHintInfo(
                "Ridges whose combined alpha falls below this threshold are skipped entirely, avoiding near-invisible pixel noise.",
                "0.01", "Almost all ridges are drawn; faint ghost trails remain visible — useful for inspecting very quiet harmonics.",
                "0.2", "Only reasonably bright ridges render; cleaner display that hides noise at the cost of losing soft notes."));

            // ── Ridge merge ──────────────────────────────────────────────────
            ParamHint.Register(lblRidgeMergeCents, new ParamHintInfo(
                "Two ridges closer in pitch than this are collapsed into one, preventing a single note from forking into parallel ridges due to FFT artifacts.",
                "20", "Only nearly-identical frequencies merge; allows closely voiced harmonics or chorus effects to stay as separate ridges.",
                "200", "Merges ridges up to 2 semitones apart; strongly suppresses leakage forks but may swallow genuinely close notes."));

            ParamHint.Register(lblRidgeMergeBrightnessBoost, new ParamHintInfo(
                "Extra brightness applied to the surviving ridge after a merge, compensating for the absorbed ridge's energy. 0 = no boost; 1 = doubles brightness.",
                "0.0", "No compensation; the surviving ridge keeps its own brightness unmodified after absorbing a neighbour.",
                "1.5", "Strong boost; the merged ridge becomes noticeably brighter, clearly signalling that energy was consolidated."));

            ParamHint.Register(lblRidgeMergeWidthAdd, new ParamHintInfo(
                "Extra pixel thickness added to a ridge immediately after absorbing a neighbour. Decays each frame by the Merge Width Decay factor.",
                "0.0", "No width bonus; merges are invisible in terms of ridge thickness.",
                "3.0", "Ridge briefly fattens after each merge, giving a visual pulse that indicates energy consolidation."));

            ParamHint.Register(lblRidgeMergeWidthDecay, new ParamHintInfo(
                "Per-frame multiplier applied to the extra width bonus after a merge. 1 = bonus never fades; 0 = disappears after one frame.",
                "0.5", "Width bonus halves every frame; merge pulse lasts only a few frames before returning to normal thickness.",
                "0.98", "Bonus fades very slowly; ridge stays thickened for many frames — a persistent visual indicator of the merge."));

            // ── Harmonic suppression ─────────────────────────────────────────
            ParamHint.Register(lblHarmonicFamilyCentsTol, new ParamHintInfo(
                "How far a ridge's frequency can deviate from an ideal integer harmonic ratio and still be counted as part of that harmonic series.",
                "15", "Tight tolerance; only near-perfectly in-tune overtones are grouped — best for electronic or synthesised sources.",
                "80", "Wide tolerance; accommodates stretched piano tuning, inharmonic strings, or instruments with significant tuning drift."));

            ParamHint.Register(lblHarmonicFamilyMaxRatio, new ParamHintInfo(
                "Highest harmonic number checked when building a harmonic series from a fundamental. E.g. 12 checks up to the 12th partial.",
                "4", "Only the first four partials are considered; fast and sufficient for flute, sine waves, or low-harmonic sources.",
                "20", "Checks 20 harmonics; captures the rich overtone series of brass, bowed strings, or complex acoustic instruments."));

            ParamHint.Register(lblHarmonicSuppression, new ParamHintInfo(
                "How much harmonic overtones are attenuated before chord scoring. Higher values treat the fundamental and its overtones as a single pitch class.",
                "0.0", "No suppression; all overtones vote in chord scoring — can skew results toward chords that match common harmonic series.",
                "0.95", "Near-total suppression; only the fundamental contributes — cleaner detection but may miss chords built on upper partials."));

            // ── Chord detection ──────────────────────────────────────────────
            ParamHint.Register(lblChordAvgFrames, new ParamHintInfo(
                "Number of recent frames whose chroma energy is averaged before chord scoring. More frames = stabler labels but slower response to chord changes.",
                "8", "Fast response; chord labels update almost immediately — good for rapid harmonic rhythm or transcription work.",
                "72", "Very stable labels; smooths over passing tones and ornaments but will lag by over a second on quick chord changes."));

            ParamHint.Register(lblChordOutPenalty, new ParamHintInfo(
                "Score deduction for each active pitch class not part of the candidate chord template. Higher = favours simpler, more exact matches.",
                "0.05", "Lenient; added tones barely hurt the score — the detector readily finds 9th and 13th chords.",
                "1.0", "Strict; any note outside the template heavily penalises the candidate — favours triads and clean dyads over complex voicings."));

            ParamHint.Register(lblAdd9ExtraScore, new ParamHintInfo(
                "How many of the strongest active ridges are fed into chord scoring each frame. Too few misses quiet notes; too many lets noise corrupt the chroma vector.",
                "2", "Only the two loudest ridges vote; reliable for simple dyads or monophonic instruments.",
                "10", "Top ten ridges contribute; better coverage of full voicings but quiet noise ridges may distort the chroma."));

            // ── Misc ─────────────────────────────────────────────────────────
            ParamHint.Register(label3, new ParamHintInfo(
                "Concert pitch in Hz. 440 is standard.",
                "432", "Alternative tuning used by some orchestras and new-age recordings.",
                "466", "Baroque pitch. "));

            ParamHint.Register(lblKeyEmaSeconds, new ParamHintInfo(
                "Half-life of the long-term EMA chroma profile used for auto key detection. Higher = slower response to key changes; lower = more reactive but less stable.",
                "10.0", "Fast response; the detected key can change within a few seconds of a key modulation, if you're trying to catch a fast modulation, but it won't be stable.",
                "30.0", "Very stable key detection, if you just want the key and don't expect modulations."));

            ParamHint.Register(lbEmail, new ParamHintInfo(
                "If you want to improve the program, look for SpectrumNotes on GitHub, fork the project, submit a pull request, and shoot me an email. "
                "Click to copy email address to clipboard.", null, null, null, null));

        }
    }
}
