//Copyright (c) 2026, Erik Martin
// Adds key-signature / mode filtering to the spectrogram grid overlay.
//
// Two ComboBoxes are added to the Analysis tab (see Form1_Designer patch):
//   cmbScaleRoot  — "Chromatic", "C", "C#/D♭", "D", … "B"
//   cmbScaleMode  — "Major (Ionian)", "Dorian", "Phrygian", …, "Minor (Aeolian)", etc.
//
// When a non-chromatic root+mode is chosen:
//   • DrawNoteGrid skips pitches not in the scale.
//   • The cbFlats checkbox is set to the conventional accidental for that key.
//   • _activeScaleNotes (HashSet<int>, pitch-classes 0–11) is rebuilt.
//   • The grid cache is invalidated so the overlay redraws immediately.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace Spectrum
{
    public partial class Form1
    {
        // ── Active scale filter ───────────────────────────────────────────────
        // null  ⟹ chromatic (show all 12 pitch classes).
        // Otherwise contains the 7 (or however many) pitch-class indices to show.
        private HashSet<int> _activeScaleNotes = null;   // null = chromatic

        // ── Scale interval patterns (semitones from root) ────────────────────
        // Diatonic modes share the same 7 interval steps, just rotated.
        private static readonly int[] IntervalsIonian = { 0, 2, 4, 5, 7, 9, 11 };
        private static readonly int[] IntervalsDorian = { 0, 2, 3, 5, 7, 9, 10 };
        private static readonly int[] IntervalsPhrygian = { 0, 1, 3, 5, 7, 8, 10 };
        private static readonly int[] IntervalsLydian = { 0, 2, 4, 6, 7, 9, 11 };
        private static readonly int[] IntervalsMixolydian = { 0, 2, 4, 5, 7, 9, 10 };
        private static readonly int[] IntervalsAeolian = { 0, 2, 3, 5, 7, 8, 10 };
        private static readonly int[] IntervalsLocrian = { 0, 1, 3, 5, 6, 8, 10 };
        // Extra useful scales
        private static readonly int[] IntervalsHarmonicMinor = { 0, 2, 3, 5, 7, 8, 11 };
        private static readonly int[] IntervalsMelodicMinor = { 0, 2, 3, 5, 7, 9, 11 };
        private static readonly int[] IntervalsWholeTone = { 0, 2, 4, 6, 8, 10 };
        private static readonly int[] IntervalsDiminished = { 0, 2, 3, 5, 6, 8, 9, 11 };
        private static readonly int[] IntervalsPentatonicMaj = { 0, 2, 4, 7, 9 };
        private static readonly int[] IntervalsPentatonicMin = { 0, 3, 5, 7, 10 };
        private static readonly int[] IntervalsBlues = { 0, 3, 5, 6, 7, 10 };

        // Display names for cmbScaleMode (order matches _modeIntervals below).
        private static readonly string[] ModeNames =
        {
            "Major (Ionian)",
            "Dorian",
            "Phrygian",
            "Lydian",
            "Mixolydian",
            "Minor (Aeolian)",
            "Locrian",
            "Harmonic Minor",
            "Melodic Minor",
            "Pentatonic Major",
            "Pentatonic Minor",
            "Blues",
            "Whole Tone",
            "Diminished",
        };

        private static readonly int[][] _modeIntervals =
        {
            IntervalsIonian,
            IntervalsDorian,
            IntervalsPhrygian,
            IntervalsLydian,
            IntervalsMixolydian,
            IntervalsAeolian,
            IntervalsLocrian,
            IntervalsHarmonicMinor,
            IntervalsMelodicMinor,
            IntervalsPentatonicMaj,
            IntervalsPentatonicMin,
            IntervalsBlues,
            IntervalsWholeTone,
            IntervalsDiminished,
        };

        // Root note display names for cmbScaleRoot.
        // Index 0 = "Chromatic" (special); indices 1–12 map to pitch-class 0–11.
        private static readonly string[] RootNames =
        {
            "Chromatic",
            "C", "C#/D♭", "D", "D#/E♭", "E", "F",
            "F#/G♭", "G", "G#/A♭", "A", "A#/B♭", "B",
        };

        // Pitch class for each root (index 0 unused / chromatic).
        private static readonly int[] RootPitchClass =
        {
            -1, // chromatic
             0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11
        };

        // Major keys that conventionally use flats (pitch-class of the major tonic).
        // F(5), B♭(10), E♭(3), A♭(8), D♭(1), G♭(6)
        private static readonly HashSet<int> FlatMajorRoots = new() { 5, 10, 3, 8, 1, 6 };

        // How many semitones above the mode root its relative MAJOR tonic sits.
        // Index matches _modeIntervals / ModeNames order.
        // Diatonic modes: Ionian=0, Dorian=10, Phrygian=8, Lydian=7, Mixolydian=5,
        //                 Aeolian=3, Locrian=1.
        // Non-diatonic scales: use 0 (treat root itself as the "major" reference).
        private static readonly int[] ModeToRelativeMajorOffset =
        {
             0,  // Major (Ionian)      — root IS the major tonic
            10,  // Dorian              — relative major is 2 semitones below root (i.e. +10 mod 12)
             8,  // Phrygian            — relative major is 4 semitones below root
             7,  // Lydian              — relative major is 5 semitones below root
             5,  // Mixolydian          — relative major is 7 semitones below root
             3,  // Minor (Aeolian)     — relative major is 3 semitones above root
             1,  // Locrian             — relative major is 11 semitones above root (= 1 below)
             0,  // Harmonic Minor      — use root directly (no standard relative major)
             0,  // Melodic Minor       — use root directly
             0,  // Pentatonic Major    — same logic as major
             3,  // Pentatonic Minor    — same relative-major offset as Aeolian
             3,  // Blues               — conventionally minor-rooted
             0,  // Whole Tone          — no key signature; use root
             0,  // Diminished          — symmetric; use root
        };

        // ── Wire up the two dropdowns ─────────────────────────────────────────
        // Called from Form1_Load (or the constructor) after InitializeComponent.
        private void InitScaleDropdowns()
        {
            // Populate root combo.
            cmbScaleRoot.Items.Clear();
            foreach (var name in RootNames)
                cmbScaleRoot.Items.Add(name);
            cmbScaleRoot.SelectedIndex = 0;   // "Chromatic"

            // Populate mode combo.
            cmbScaleMode.Items.Clear();
            foreach (var name in ModeNames)
                cmbScaleMode.Items.Add(name);
            cmbScaleMode.SelectedIndex = 0;   // "Major (Ionian)"
            cmbScaleMode.Enabled = false;     // disabled until a root is chosen

            cmbScaleRoot.SelectedIndexChanged += OnScaleSelectionChanged;
            cmbScaleMode.SelectedIndexChanged += OnScaleSelectionChanged;
        }

        private void OnScaleSelectionChanged(object sender, EventArgs e)
        {
            int rootIdx = cmbScaleRoot.SelectedIndex;  // 0 = chromatic, 1..12 = C..B

            if (rootIdx == 0)
            {
                // Chromatic — no filter.
                _activeScaleNotes = null;
                cmbScaleMode.Enabled = false;
            }
            else
            {
                cmbScaleMode.Enabled = true;
                int pc = RootPitchClass[rootIdx];
                int mode = Math.Max(0, cmbScaleMode.SelectedIndex);
                var intervals = _modeIntervals[mode];

                // Build the pitch-class set.
                _activeScaleNotes = new HashSet<int>(
                    intervals.Select(i => (pc + i) % 12));

                // Determine conventional accidentals by finding the relative major tonic
                // and checking whether that major key uses flats or sharps.
                int relMajorOffset = ModeToRelativeMajorOffset[mode];
                int relMajorPc = (pc + relMajorOffset) % 12;
                bool useFlats = FlatMajorRoots.Contains(relMajorPc);
                if (cbFlats.Checked != useFlats)
                {
                    cbFlats.Checked = useFlats;   // triggers cbFlats.CheckedChanged → _useFlats update
                }
            }

            // Invalidate grid cache so DrawNoteGrid redraws with new filter.
            _gridCache?.Dispose();
            _gridCache = null;
            if (!pic.IsDisposed && pic.IsHandleCreated)
                pic.Invalidate();
        }

        // ── Helper used by DrawNoteGrid ───────────────────────────────────────
        // Returns true if pitch-class n (0–11) should be drawn given the active scale.
        private bool IsNoteInActiveScale(int pitchClass)
        {
            return _activeScaleNotes == null || _activeScaleNotes.Contains(pitchClass % 12);
        }
    }
}
