namespace Spectrum
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            panel1 = new Panel();
            lblAdd9MinNinthEnergy = new Label();
            tbHighPass = new TextBox();
            label4 = new Label();
            tbLowPass = new TextBox();
            cblines = new CheckBox();
            cbFlats = new CheckBox();
            tabControl1 = new TabControl();
            tabAnalysis = new TabPage();
            label2 = new Label();
            cmbScaleMode = new ComboBox();
            cbAutoKey = new CheckBox();
            cmbScaleRoot = new ComboBox();
            pbHarmonics = new PictureBox();
            chordlabel = new Label();
            tbCanonicalNotes = new TextBox();
            tbDetectedNotes = new TextBox();
            pbTuner = new PictureBox();
            comboBox1 = new ComboBox();
            cbVolume = new CheckBox();
            volume = new TrackBar();
            btnRecord = new Button();
            pbKey = new PictureBox();
            lbEmail = new Label();
            tabSettings = new TabPage();
            settingsScroll = new Panel();
            lblFFTSize = new Label();
            tbFFTSize = new TextBox();
            cbLowFft = new CheckBox();
            lblLowFftCrossoverNote = new Label();
            tbLowFftCrossoverNote = new TextBox();
            lblLowFftCrossoverSemitones = new Label();
            tbLowFftCrossoverSemitones = new TextBox();
            lblOverlapFactor = new Label();
            tbOverlapFactor = new TextBox();
            lblLevelSmoothEma = new Label();
            tbLevelSmoothEma = new TextBox();
            label3 = new Label();
            tbtuning = new TextBox();
            lblMaxPeaksPerFrame = new Label();
            tbMaxPeaksPerFrame = new TextBox();
            lblPeakMinRel = new Label();
            tbPeakMinRel = new TextBox();
            lblPeakMinSpacingCents = new Label();
            tbPeakMinSpacingCents = new TextBox();
            lblPeakGamma = new Label();
            tbPeakGamma = new TextBox();
            lblPeakMode = new Label();
            tbPeakMode = new TextBox();
            lblRidgeMaxCentsJump = new Label();
            tbRidgeMaxCentsJump = new TextBox();
            lblRidgeMissMax = new Label();
            tbRidgeMissMax = new TextBox();
            lblRidgeFreqEMA = new Label();
            tbRidgeFreqEMA = new TextBox();
            lblRidgeVelEMA = new Label();
            tbRidgeVelEMA = new TextBox();
            lblRidgeIntensityEMA = new Label();
            tbRidgeIntensityEMA = new TextBox();
            lblMissFadePow = new Label();
            tbMissFadePow = new TextBox();
            lblAgeDecay = new Label();
            tbAgeDecay = new TextBox();
            lblMinDrawAlpha = new Label();
            tbMinDrawAlpha = new TextBox();
            lblRidgeMergeCents = new Label();
            tbRidgeMergeCents = new TextBox();
            lblRidgeMergeBrightnessBoost = new Label();
            tbRidgeMergeBrightnessBoost = new TextBox();
            lblRidgeMergeWidthAdd = new Label();
            tbRidgeMergeWidthAdd = new TextBox();
            lblRidgeMergeWidthDecay = new Label();
            tbRidgeMergeWidthDecay = new TextBox();
            lblHarmonicFamilyCentsTol = new Label();
            tbHarmonicFamilyCentsTol = new TextBox();
            lblHarmonicFamilyMaxRatio = new Label();
            tbHarmonicFamilyMaxRatio = new TextBox();
            lblHarmonicSuppression = new Label();
            tbHarmonicSuppression = new TextBox();
            lblChordAvgFrames = new Label();
            tbChordAvgFrames = new TextBox();
            lblChordOutPenalty = new Label();
            tbChordOutPenalty = new TextBox();
            lblAdd9ExtraScore = new Label();
            tbChordRidges = new TextBox();
            lblKeyEmaSeconds = new Label();
            tbKeyEmaSeconds = new TextBox();
            lblRidgeMatchLogHz = new Label();
            tbRidgeMatchLogHz = new TextBox();
            lblRidgeMatchLogHzPredBoost = new Label();
            tbRidgeMatchLogHzPredBoost = new TextBox();
            lblMaxColShift = new Label();
            tbMaxColShift = new TextBox();
            lblTargetFps = new Label();
            tbTargetFps = new TextBox();
            lblFpsReadout = new Label();
            panel2 = new Panel();
            pic = new SpectrogramPanel();
            panel1.SuspendLayout();
            tabControl1.SuspendLayout();
            tabAnalysis.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pbHarmonics).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pbTuner).BeginInit();
            ((System.ComponentModel.ISupportInitialize)volume).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pbKey).BeginInit();
            tabSettings.SuspendLayout();
            settingsScroll.SuspendLayout();
            panel2.SuspendLayout();
            SuspendLayout();
            // 
            // panel1
            // 
            panel1.Controls.Add(lblAdd9MinNinthEnergy);
            panel1.Controls.Add(tbHighPass);
            panel1.Controls.Add(label4);
            panel1.Controls.Add(tbLowPass);
            panel1.Controls.Add(cblines);
            panel1.Controls.Add(cbFlats);
            panel1.Controls.Add(tabControl1);
            panel1.Dock = DockStyle.Left;
            panel1.Location = new Point(0, 0);
            panel1.Name = "panel1";
            panel1.Size = new Size(310, 976);
            panel1.TabIndex = 0;
            // 
            // lblAdd9MinNinthEnergy
            // 
            lblAdd9MinNinthEnergy.AutoSize = true;
            lblAdd9MinNinthEnergy.Location = new Point(6, 7);
            lblAdd9MinNinthEnergy.Name = "lblAdd9MinNinthEnergy";
            lblAdd9MinNinthEnergy.Size = new Size(76, 15);
            lblAdd9MinNinthEnergy.TabIndex = 57;
            lblAdd9MinNinthEnergy.Text = "Bottom Note";
            // 
            // tbHighPass
            // 
            tbHighPass.Location = new Point(90, 4);
            tbHighPass.Name = "tbHighPass";
            tbHighPass.Size = new Size(60, 23);
            tbHighPass.TabIndex = 58;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(160, 7);
            label4.Name = "label4";
            label4.Size = new Size(56, 15);
            label4.TabIndex = 63;
            label4.Text = "Top Note";
            // 
            // tbLowPass
            // 
            tbLowPass.Location = new Point(228, 4);
            tbLowPass.Name = "tbLowPass";
            tbLowPass.Size = new Size(60, 23);
            tbLowPass.TabIndex = 64;
            // 
            // cblines
            // 
            cblines.AutoSize = true;
            cblines.Location = new Point(6, 30);
            cblines.Name = "cblines";
            cblines.Size = new Size(50, 19);
            cblines.TabIndex = 5;
            cblines.Text = "lines";
            cblines.UseVisualStyleBackColor = true;
            // 
            // cbFlats
            // 
            cbFlats.AutoSize = true;
            cbFlats.Location = new Point(58, 30);
            cbFlats.Name = "cbFlats";
            cbFlats.Size = new Size(48, 19);
            cbFlats.TabIndex = 80;
            cbFlats.Text = "flats";
            cbFlats.UseVisualStyleBackColor = true;
            // 
            // tabControl1
            // 
            tabControl1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            tabControl1.Controls.Add(tabAnalysis);
            tabControl1.Controls.Add(tabSettings);
            tabControl1.Location = new Point(0, 54);
            tabControl1.Name = "tabControl1";
            tabControl1.SelectedIndex = 0;
            tabControl1.Size = new Size(310, 922);
            tabControl1.TabIndex = 5;
            // 
            // tabAnalysis
            // 
            tabAnalysis.Controls.Add(label2);
            tabAnalysis.Controls.Add(cmbScaleMode);
            tabAnalysis.Controls.Add(cbAutoKey);
            tabAnalysis.Controls.Add(cmbScaleRoot);
            tabAnalysis.Controls.Add(pbHarmonics);
            tabAnalysis.Controls.Add(chordlabel);
            tabAnalysis.Controls.Add(tbCanonicalNotes);
            tabAnalysis.Controls.Add(tbDetectedNotes);
            tabAnalysis.Controls.Add(pbTuner);
            tabAnalysis.Controls.Add(comboBox1);
            tabAnalysis.Controls.Add(cbVolume);
            tabAnalysis.Controls.Add(volume);
            tabAnalysis.Controls.Add(btnRecord);
            tabAnalysis.Controls.Add(pbKey);
            tabAnalysis.Controls.Add(lbEmail);
            tabAnalysis.Location = new Point(4, 24);
            tabAnalysis.Name = "tabAnalysis";
            tabAnalysis.Padding = new Padding(3);
            tabAnalysis.Size = new Size(302, 894);
            tabAnalysis.TabIndex = 0;
            tabAnalysis.Text = "Analysis";
            tabAnalysis.UseVisualStyleBackColor = true;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(7, 392);
            label2.Name = "label2";
            label2.Size = new Size(79, 15);
            label2.TabIndex = 307;
            label2.Text = "Key Signature";
            // 
            // cmbScaleMode
            // 
            cmbScaleMode.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbScaleMode.DropDownWidth = 200;
            cmbScaleMode.FormattingEnabled = true;
            cmbScaleMode.Location = new Point(6, 448);
            cmbScaleMode.Name = "cmbScaleMode";
            cmbScaleMode.Size = new Size(121, 23);
            cmbScaleMode.TabIndex = 306;
            // 
            // cbAutoKey
            // 
            cbAutoKey.AutoSize = true;
            cbAutoKey.Checked = true;
            cbAutoKey.CheckState = CheckState.Checked;
            cbAutoKey.Location = new Point(9, 359);
            cbAutoKey.Name = "cbAutoKey";
            cbAutoKey.Size = new Size(111, 19);
            cbAutoKey.TabIndex = 312;
            cbAutoKey.Text = "Auto-detect key";
            cbAutoKey.UseVisualStyleBackColor = true;
            // 
            // cmbScaleRoot
            // 
            cmbScaleRoot.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbScaleRoot.DropDownWidth = 160;
            cmbScaleRoot.FormattingEnabled = true;
            cmbScaleRoot.Location = new Point(6, 419);
            cmbScaleRoot.Name = "cmbScaleRoot";
            cmbScaleRoot.Size = new Size(121, 23);
            cmbScaleRoot.TabIndex = 305;
            // 
            // pbHarmonics
            // 
            pbHarmonics.Location = new Point(6, 117);
            pbHarmonics.Name = "pbHarmonics";
            pbHarmonics.Size = new Size(284, 69);
            pbHarmonics.TabIndex = 304;
            pbHarmonics.TabStop = false;
            // 
            // chordlabel
            // 
            chordlabel.Font = new Font("Segoe UI", 25F);
            chordlabel.Location = new Point(6, 6);
            chordlabel.Name = "chordlabel";
            chordlabel.Size = new Size(284, 51);
            chordlabel.TabIndex = 42;
            chordlabel.Text = "--";
            // 
            // tbCanonicalNotes
            // 
            tbCanonicalNotes.Location = new Point(6, 60);
            tbCanonicalNotes.Multiline = true;
            tbCanonicalNotes.Name = "tbCanonicalNotes";
            tbCanonicalNotes.Size = new Size(284, 24);
            tbCanonicalNotes.TabIndex = 60;
            tbCanonicalNotes.WordWrap = false;
            // 
            // tbDetectedNotes
            // 
            tbDetectedNotes.Location = new Point(6, 87);
            tbDetectedNotes.Multiline = true;
            tbDetectedNotes.Name = "tbDetectedNotes";
            tbDetectedNotes.Size = new Size(284, 24);
            tbDetectedNotes.TabIndex = 73;
            tbDetectedNotes.WordWrap = false;
            // 
            // pbTuner
            // 
            pbTuner.Location = new Point(6, 192);
            pbTuner.Name = "pbTuner";
            pbTuner.Size = new Size(284, 67);
            pbTuner.TabIndex = 72;
            pbTuner.TabStop = false;
            // 
            // comboBox1
            // 
            comboBox1.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBox1.DropDownWidth = 400;
            comboBox1.Location = new Point(2, 265);
            comboBox1.Name = "comboBox1";
            comboBox1.Size = new Size(284, 23);
            comboBox1.TabIndex = 4;
            // 
            // cbVolume
            // 
            cbVolume.AutoSize = true;
            cbVolume.Location = new Point(9, 313);
            cbVolume.Name = "cbVolume";
            cbVolume.Size = new Size(15, 14);
            cbVolume.TabIndex = 302;
            cbVolume.UseVisualStyleBackColor = true;
            // 
            // volume
            // 
            volume.AutoSize = false;
            volume.Location = new Point(29, 312);
            volume.Maximum = 255;
            volume.Minimum = 1;
            volume.Name = "volume";
            volume.Size = new Size(264, 18);
            volume.TabIndex = 301;
            volume.Value = 100;
            // 
            // btnRecord
            // 
            btnRecord.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            btnRecord.BackColor = Color.FromArgb(139, 0, 0);
            btnRecord.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnRecord.ForeColor = Color.White;
            btnRecord.Location = new Point(19, 789);
            btnRecord.Name = "btnRecord";
            btnRecord.Size = new Size(101, 30);
            btnRecord.TabIndex = 300;
            btnRecord.Text = "⏺ Record";
            btnRecord.UseVisualStyleBackColor = false;
            btnRecord.Visible = false;
            // 
            // pbKey
            // 
            pbKey.Location = new Point(160, 382);
            pbKey.Name = "pbKey";
            pbKey.Size = new Size(130, 154);
            pbKey.TabIndex = 65;
            pbKey.TabStop = false;
            // 
            // lbEmail
            // 
            lbEmail.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            lbEmail.AutoSize = true;
            lbEmail.Location = new Point(9, 874);
            lbEmail.Name = "lbEmail";
            lbEmail.Size = new Size(169, 15);
            lbEmail.TabIndex = 81;
            lbEmail.Text = "development@erikmartin.com";
            lbEmail.Click += lbEmail_Click;
            // 
            // tabSettings
            // 
            tabSettings.Controls.Add(settingsScroll);
            tabSettings.Location = new Point(4, 24);
            tabSettings.Name = "tabSettings";
            tabSettings.Size = new Size(302, 894);
            tabSettings.TabIndex = 1;
            tabSettings.Text = "Settings";
            tabSettings.UseVisualStyleBackColor = true;
            // 
            // settingsScroll
            // 
            settingsScroll.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            settingsScroll.AutoScroll = true;
            settingsScroll.Controls.Add(lblFFTSize);
            settingsScroll.Controls.Add(tbFFTSize);
            settingsScroll.Controls.Add(cbLowFft);
            settingsScroll.Controls.Add(lblLowFftCrossoverNote);
            settingsScroll.Controls.Add(tbLowFftCrossoverNote);
            settingsScroll.Controls.Add(lblLowFftCrossoverSemitones);
            settingsScroll.Controls.Add(tbLowFftCrossoverSemitones);
            settingsScroll.Controls.Add(lblOverlapFactor);
            settingsScroll.Controls.Add(tbOverlapFactor);
            settingsScroll.Controls.Add(lblLevelSmoothEma);
            settingsScroll.Controls.Add(tbLevelSmoothEma);
            settingsScroll.Controls.Add(label3);
            settingsScroll.Controls.Add(tbtuning);
            settingsScroll.Controls.Add(lblMaxPeaksPerFrame);
            settingsScroll.Controls.Add(tbMaxPeaksPerFrame);
            settingsScroll.Controls.Add(lblPeakMinRel);
            settingsScroll.Controls.Add(tbPeakMinRel);
            settingsScroll.Controls.Add(lblPeakMinSpacingCents);
            settingsScroll.Controls.Add(tbPeakMinSpacingCents);
            settingsScroll.Controls.Add(lblPeakGamma);
            settingsScroll.Controls.Add(tbPeakGamma);
            settingsScroll.Controls.Add(lblPeakMode);
            settingsScroll.Controls.Add(tbPeakMode);
            settingsScroll.Controls.Add(lblRidgeMaxCentsJump);
            settingsScroll.Controls.Add(tbRidgeMaxCentsJump);
            settingsScroll.Controls.Add(lblRidgeMissMax);
            settingsScroll.Controls.Add(tbRidgeMissMax);
            settingsScroll.Controls.Add(lblRidgeFreqEMA);
            settingsScroll.Controls.Add(tbRidgeFreqEMA);
            settingsScroll.Controls.Add(lblRidgeVelEMA);
            settingsScroll.Controls.Add(tbRidgeVelEMA);
            settingsScroll.Controls.Add(lblRidgeIntensityEMA);
            settingsScroll.Controls.Add(tbRidgeIntensityEMA);
            settingsScroll.Controls.Add(lblMissFadePow);
            settingsScroll.Controls.Add(tbMissFadePow);
            settingsScroll.Controls.Add(lblAgeDecay);
            settingsScroll.Controls.Add(tbAgeDecay);
            settingsScroll.Controls.Add(lblMinDrawAlpha);
            settingsScroll.Controls.Add(tbMinDrawAlpha);
            settingsScroll.Controls.Add(lblRidgeMergeCents);
            settingsScroll.Controls.Add(tbRidgeMergeCents);
            settingsScroll.Controls.Add(lblRidgeMergeBrightnessBoost);
            settingsScroll.Controls.Add(tbRidgeMergeBrightnessBoost);
            settingsScroll.Controls.Add(lblRidgeMergeWidthAdd);
            settingsScroll.Controls.Add(tbRidgeMergeWidthAdd);
            settingsScroll.Controls.Add(lblRidgeMergeWidthDecay);
            settingsScroll.Controls.Add(tbRidgeMergeWidthDecay);
            settingsScroll.Controls.Add(lblHarmonicFamilyCentsTol);
            settingsScroll.Controls.Add(tbHarmonicFamilyCentsTol);
            settingsScroll.Controls.Add(lblHarmonicFamilyMaxRatio);
            settingsScroll.Controls.Add(tbHarmonicFamilyMaxRatio);
            settingsScroll.Controls.Add(lblHarmonicSuppression);
            settingsScroll.Controls.Add(tbHarmonicSuppression);
            settingsScroll.Controls.Add(lblChordAvgFrames);
            settingsScroll.Controls.Add(tbChordAvgFrames);
            settingsScroll.Controls.Add(lblChordOutPenalty);
            settingsScroll.Controls.Add(tbChordOutPenalty);
            settingsScroll.Controls.Add(lblAdd9ExtraScore);
            settingsScroll.Controls.Add(tbChordRidges);
            settingsScroll.Controls.Add(lblKeyEmaSeconds);
            settingsScroll.Controls.Add(tbKeyEmaSeconds);
            settingsScroll.Controls.Add(lblRidgeMatchLogHz);
            settingsScroll.Controls.Add(tbRidgeMatchLogHz);
            settingsScroll.Controls.Add(lblRidgeMatchLogHzPredBoost);
            settingsScroll.Controls.Add(tbRidgeMatchLogHzPredBoost);
            settingsScroll.Controls.Add(lblMaxColShift);
            settingsScroll.Controls.Add(tbMaxColShift);
            settingsScroll.Controls.Add(lblTargetFps);
            settingsScroll.Controls.Add(tbTargetFps);
            settingsScroll.Controls.Add(lblFpsReadout);
            settingsScroll.Location = new Point(0, 0);
            settingsScroll.Name = "settingsScroll";
            settingsScroll.Size = new Size(302, 894);
            settingsScroll.TabIndex = 0;
            // 
            // lblFFTSize
            // 
            lblFFTSize.AutoSize = true;
            lblFFTSize.Location = new Point(4, 11);
            lblFFTSize.Name = "lblFFTSize";
            lblFFTSize.Size = new Size(71, 15);
            lblFFTSize.TabIndex = 6;
            lblFFTSize.Text = "FFT Size (2ⁿ)";
            // 
            // tbFFTSize
            // 
            tbFFTSize.Location = new Point(188, 8);
            tbFFTSize.Name = "tbFFTSize";
            tbFFTSize.Size = new Size(90, 23);
            tbFFTSize.TabIndex = 7;
            // 
            // cbLowFft
            // 
            cbLowFft.AutoSize = true;
            cbLowFft.Location = new Point(170, 37);
            cbLowFft.Name = "cbLowFft";
            cbLowFft.Size = new Size(108, 19);
            cbLowFft.TabIndex = 403;
            cbLowFft.Text = "Enable Low FFT";
            // 
            // lblLowFftCrossoverNote
            // 
            lblLowFftCrossoverNote.AutoSize = true;
            lblLowFftCrossoverNote.Location = new Point(4, 61);
            lblLowFftCrossoverNote.Name = "lblLowFftCrossoverNote";
            lblLowFftCrossoverNote.Size = new Size(88, 15);
            lblLowFftCrossoverNote.TabIndex = 404;
            lblLowFftCrossoverNote.Text = "Crossover Note";
            // 
            // tbLowFftCrossoverNote
            // 
            tbLowFftCrossoverNote.Location = new Point(188, 58);
            tbLowFftCrossoverNote.Name = "tbLowFftCrossoverNote";
            tbLowFftCrossoverNote.Size = new Size(90, 23);
            tbLowFftCrossoverNote.TabIndex = 405;
            // 
            // lblLowFftCrossoverSemitones
            // 
            lblLowFftCrossoverSemitones.AutoSize = true;
            lblLowFftCrossoverSemitones.Location = new Point(4, 88);
            lblLowFftCrossoverSemitones.Name = "lblLowFftCrossoverSemitones";
            lblLowFftCrossoverSemitones.Size = new Size(117, 15);
            lblLowFftCrossoverSemitones.TabIndex = 406;
            lblLowFftCrossoverSemitones.Text = "Crossover Semitones";
            // 
            // tbLowFftCrossoverSemitones
            // 
            tbLowFftCrossoverSemitones.Location = new Point(188, 85);
            tbLowFftCrossoverSemitones.Name = "tbLowFftCrossoverSemitones";
            tbLowFftCrossoverSemitones.Size = new Size(90, 23);
            tbLowFftCrossoverSemitones.TabIndex = 407;
            // 
            // lblOverlapFactor
            // 
            lblOverlapFactor.AutoSize = true;
            lblOverlapFactor.Enabled = false;
            lblOverlapFactor.Location = new Point(4, 115);
            lblOverlapFactor.Name = "lblOverlapFactor";
            lblOverlapFactor.Size = new Size(90, 15);
            lblOverlapFactor.TabIndex = 8;
            lblOverlapFactor.Text = "Computed Hop";
            // 
            // tbOverlapFactor
            // 
            tbOverlapFactor.BackColor = SystemColors.Control;
            tbOverlapFactor.Location = new Point(188, 112);
            tbOverlapFactor.Name = "tbOverlapFactor";
            tbOverlapFactor.ReadOnly = true;
            tbOverlapFactor.Size = new Size(90, 23);
            tbOverlapFactor.TabIndex = 9;
            // 
            // lblLevelSmoothEma
            // 
            lblLevelSmoothEma.AutoSize = true;
            lblLevelSmoothEma.Location = new Point(4, 143);
            lblLevelSmoothEma.Name = "lblLevelSmoothEma";
            lblLevelSmoothEma.Size = new Size(107, 15);
            lblLevelSmoothEma.TabIndex = 110;
            lblLevelSmoothEma.Text = "Level Smooth EMA";
            // 
            // tbLevelSmoothEma
            // 
            tbLevelSmoothEma.Location = new Point(188, 140);
            tbLevelSmoothEma.Name = "tbLevelSmoothEma";
            tbLevelSmoothEma.Size = new Size(90, 23);
            tbLevelSmoothEma.TabIndex = 111;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            label3.Location = new Point(4, 170);
            label3.Name = "label3";
            label3.Size = new Size(70, 15);
            label3.TabIndex = 61;
            label3.Text = "Tuning (Hz)";
            // 
            // tbtuning
            // 
            tbtuning.Location = new Point(188, 167);
            tbtuning.Name = "tbtuning";
            tbtuning.Size = new Size(90, 23);
            tbtuning.TabIndex = 62;
            // 
            // lblMaxPeaksPerFrame
            // 
            lblMaxPeaksPerFrame.AutoSize = true;
            lblMaxPeaksPerFrame.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblMaxPeaksPerFrame.Location = new Point(4, 202);
            lblMaxPeaksPerFrame.Name = "lblMaxPeaksPerFrame";
            lblMaxPeaksPerFrame.Size = new Size(93, 15);
            lblMaxPeaksPerFrame.TabIndex = 12;
            lblMaxPeaksPerFrame.Text = "Peaks Captured";
            // 
            // tbMaxPeaksPerFrame
            // 
            tbMaxPeaksPerFrame.Location = new Point(188, 199);
            tbMaxPeaksPerFrame.Name = "tbMaxPeaksPerFrame";
            tbMaxPeaksPerFrame.Size = new Size(90, 23);
            tbMaxPeaksPerFrame.TabIndex = 13;
            // 
            // lblPeakMinRel
            // 
            lblPeakMinRel.AutoSize = true;
            lblPeakMinRel.Location = new Point(4, 229);
            lblPeakMinRel.Name = "lblPeakMinRel";
            lblPeakMinRel.Size = new Size(75, 15);
            lblPeakMinRel.TabIndex = 14;
            lblPeakMinRel.Text = "Peak Min Rel";
            // 
            // tbPeakMinRel
            // 
            tbPeakMinRel.Location = new Point(188, 226);
            tbPeakMinRel.Name = "tbPeakMinRel";
            tbPeakMinRel.Size = new Size(90, 23);
            tbPeakMinRel.TabIndex = 15;
            // 
            // lblPeakMinSpacingCents
            // 
            lblPeakMinSpacingCents.AutoSize = true;
            lblPeakMinSpacingCents.Location = new Point(4, 256);
            lblPeakMinSpacingCents.Name = "lblPeakMinSpacingCents";
            lblPeakMinSpacingCents.Size = new Size(140, 15);
            lblPeakMinSpacingCents.TabIndex = 43;
            lblPeakMinSpacingCents.Text = "Peak Min Spacing (cents)";
            // 
            // tbPeakMinSpacingCents
            // 
            tbPeakMinSpacingCents.Location = new Point(188, 253);
            tbPeakMinSpacingCents.Name = "tbPeakMinSpacingCents";
            tbPeakMinSpacingCents.Size = new Size(90, 23);
            tbPeakMinSpacingCents.TabIndex = 44;
            // 
            // lblPeakGamma
            // 
            lblPeakGamma.AutoSize = true;
            lblPeakGamma.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblPeakGamma.Location = new Point(4, 283);
            lblPeakGamma.Name = "lblPeakGamma";
            lblPeakGamma.Size = new Size(92, 15);
            lblPeakGamma.TabIndex = 32;
            lblPeakGamma.Text = "Display Gamma";
            // 
            // tbPeakGamma
            // 
            tbPeakGamma.Location = new Point(188, 280);
            tbPeakGamma.Name = "tbPeakGamma";
            tbPeakGamma.Size = new Size(90, 23);
            tbPeakGamma.TabIndex = 33;
            // 
            // lblPeakMode
            // 
            lblPeakMode.AutoSize = true;
            lblPeakMode.Location = new Point(4, 310);
            lblPeakMode.Name = "lblPeakMode";
            lblPeakMode.Size = new Size(66, 15);
            lblPeakMode.TabIndex = 68;
            lblPeakMode.Text = "Peak Mode";
            // 
            // tbPeakMode
            // 
            tbPeakMode.Location = new Point(188, 307);
            tbPeakMode.Name = "tbPeakMode";
            tbPeakMode.Size = new Size(90, 23);
            tbPeakMode.TabIndex = 69;
            // 
            // lblRidgeMaxCentsJump
            // 
            lblRidgeMaxCentsJump.AutoSize = true;
            lblRidgeMaxCentsJump.Location = new Point(4, 342);
            lblRidgeMaxCentsJump.Name = "lblRidgeMaxCentsJump";
            lblRidgeMaxCentsJump.Size = new Size(127, 15);
            lblRidgeMaxCentsJump.TabIndex = 20;
            lblRidgeMaxCentsJump.Text = "Ridge Max Cents Jump";
            // 
            // tbRidgeMaxCentsJump
            // 
            tbRidgeMaxCentsJump.Location = new Point(188, 339);
            tbRidgeMaxCentsJump.Name = "tbRidgeMaxCentsJump";
            tbRidgeMaxCentsJump.Size = new Size(90, 23);
            tbRidgeMaxCentsJump.TabIndex = 21;
            // 
            // lblRidgeMissMax
            // 
            lblRidgeMissMax.AutoSize = true;
            lblRidgeMissMax.Location = new Point(4, 369);
            lblRidgeMissMax.Name = "lblRidgeMissMax";
            lblRidgeMissMax.Size = new Size(89, 15);
            lblRidgeMissMax.TabIndex = 22;
            lblRidgeMissMax.Text = "Ridge Miss Max";
            // 
            // tbRidgeMissMax
            // 
            tbRidgeMissMax.Location = new Point(188, 366);
            tbRidgeMissMax.Name = "tbRidgeMissMax";
            tbRidgeMissMax.Size = new Size(90, 23);
            tbRidgeMissMax.TabIndex = 23;
            // 
            // lblRidgeFreqEMA
            // 
            lblRidgeFreqEMA.AutoSize = true;
            lblRidgeFreqEMA.Location = new Point(4, 396);
            lblRidgeFreqEMA.Name = "lblRidgeFreqEMA";
            lblRidgeFreqEMA.Size = new Size(91, 15);
            lblRidgeFreqEMA.TabIndex = 26;
            lblRidgeFreqEMA.Text = "Ridge Freq EMA";
            // 
            // tbRidgeFreqEMA
            // 
            tbRidgeFreqEMA.Location = new Point(188, 393);
            tbRidgeFreqEMA.Name = "tbRidgeFreqEMA";
            tbRidgeFreqEMA.Size = new Size(90, 23);
            tbRidgeFreqEMA.TabIndex = 27;
            // 
            // lblRidgeVelEMA
            // 
            lblRidgeVelEMA.AutoSize = true;
            lblRidgeVelEMA.Location = new Point(4, 423);
            lblRidgeVelEMA.Name = "lblRidgeVelEMA";
            lblRidgeVelEMA.Size = new Size(83, 15);
            lblRidgeVelEMA.TabIndex = 28;
            lblRidgeVelEMA.Text = "Ridge Vel EMA";
            // 
            // tbRidgeVelEMA
            // 
            tbRidgeVelEMA.Location = new Point(188, 420);
            tbRidgeVelEMA.Name = "tbRidgeVelEMA";
            tbRidgeVelEMA.Size = new Size(90, 23);
            tbRidgeVelEMA.TabIndex = 29;
            // 
            // lblRidgeIntensityEMA
            // 
            lblRidgeIntensityEMA.AutoSize = true;
            lblRidgeIntensityEMA.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblRidgeIntensityEMA.Location = new Point(4, 450);
            lblRidgeIntensityEMA.Name = "lblRidgeIntensityEMA";
            lblRidgeIntensityEMA.Size = new Size(119, 15);
            lblRidgeIntensityEMA.TabIndex = 204;
            lblRidgeIntensityEMA.Text = "Ridge Intensity EMA";
            // 
            // tbRidgeIntensityEMA
            // 
            tbRidgeIntensityEMA.Location = new Point(188, 447);
            tbRidgeIntensityEMA.Name = "tbRidgeIntensityEMA";
            tbRidgeIntensityEMA.Size = new Size(90, 23);
            tbRidgeIntensityEMA.TabIndex = 205;
            // 
            // lblMissFadePow
            // 
            lblMissFadePow.AutoSize = true;
            lblMissFadePow.Location = new Point(4, 477);
            lblMissFadePow.Name = "lblMissFadePow";
            lblMissFadePow.Size = new Size(85, 15);
            lblMissFadePow.TabIndex = 82;
            lblMissFadePow.Text = "Miss Fade Pow";
            // 
            // tbMissFadePow
            // 
            tbMissFadePow.Location = new Point(188, 474);
            tbMissFadePow.Name = "tbMissFadePow";
            tbMissFadePow.Size = new Size(90, 23);
            tbMissFadePow.TabIndex = 83;
            // 
            // lblAgeDecay
            // 
            lblAgeDecay.AutoSize = true;
            lblAgeDecay.Location = new Point(4, 504);
            lblAgeDecay.Name = "lblAgeDecay";
            lblAgeDecay.Size = new Size(63, 15);
            lblAgeDecay.TabIndex = 84;
            lblAgeDecay.Text = "Age Decay";
            // 
            // tbAgeDecay
            // 
            tbAgeDecay.Location = new Point(188, 501);
            tbAgeDecay.Name = "tbAgeDecay";
            tbAgeDecay.Size = new Size(90, 23);
            tbAgeDecay.TabIndex = 85;
            // 
            // lblMinDrawAlpha
            // 
            lblMinDrawAlpha.AutoSize = true;
            lblMinDrawAlpha.Location = new Point(4, 531);
            lblMinDrawAlpha.Name = "lblMinDrawAlpha";
            lblMinDrawAlpha.Size = new Size(92, 15);
            lblMinDrawAlpha.TabIndex = 86;
            lblMinDrawAlpha.Text = "Min Draw Alpha";
            // 
            // tbMinDrawAlpha
            // 
            tbMinDrawAlpha.Location = new Point(188, 528);
            tbMinDrawAlpha.Name = "tbMinDrawAlpha";
            tbMinDrawAlpha.Size = new Size(90, 23);
            tbMinDrawAlpha.TabIndex = 87;
            // 
            // lblRidgeMergeCents
            // 
            lblRidgeMergeCents.AutoSize = true;
            lblRidgeMergeCents.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblRidgeMergeCents.Location = new Point(4, 563);
            lblRidgeMergeCents.Name = "lblRidgeMergeCents";
            lblRidgeMergeCents.Size = new Size(120, 15);
            lblRidgeMergeCents.TabIndex = 45;
            lblRidgeMergeCents.Text = "Ridge Merge (cents)";
            // 
            // tbRidgeMergeCents
            // 
            tbRidgeMergeCents.Location = new Point(188, 560);
            tbRidgeMergeCents.Name = "tbRidgeMergeCents";
            tbRidgeMergeCents.Size = new Size(90, 23);
            tbRidgeMergeCents.TabIndex = 46;
            // 
            // lblRidgeMergeBrightnessBoost
            // 
            lblRidgeMergeBrightnessBoost.AutoSize = true;
            lblRidgeMergeBrightnessBoost.Location = new Point(4, 590);
            lblRidgeMergeBrightnessBoost.Name = "lblRidgeMergeBrightnessBoost";
            lblRidgeMergeBrightnessBoost.Size = new Size(132, 15);
            lblRidgeMergeBrightnessBoost.TabIndex = 76;
            lblRidgeMergeBrightnessBoost.Text = "Merge Brightness Boost";
            // 
            // tbRidgeMergeBrightnessBoost
            // 
            tbRidgeMergeBrightnessBoost.Location = new Point(188, 587);
            tbRidgeMergeBrightnessBoost.Name = "tbRidgeMergeBrightnessBoost";
            tbRidgeMergeBrightnessBoost.Size = new Size(90, 23);
            tbRidgeMergeBrightnessBoost.TabIndex = 77;
            // 
            // lblRidgeMergeWidthAdd
            // 
            lblRidgeMergeWidthAdd.AutoSize = true;
            lblRidgeMergeWidthAdd.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblRidgeMergeWidthAdd.Location = new Point(4, 617);
            lblRidgeMergeWidthAdd.Name = "lblRidgeMergeWidthAdd";
            lblRidgeMergeWidthAdd.Size = new Size(106, 15);
            lblRidgeMergeWidthAdd.TabIndex = 78;
            lblRidgeMergeWidthAdd.Text = "Merge Width Add";
            // 
            // tbRidgeMergeWidthAdd
            // 
            tbRidgeMergeWidthAdd.Location = new Point(188, 614);
            tbRidgeMergeWidthAdd.Name = "tbRidgeMergeWidthAdd";
            tbRidgeMergeWidthAdd.Size = new Size(90, 23);
            tbRidgeMergeWidthAdd.TabIndex = 79;
            // 
            // lblRidgeMergeWidthDecay
            // 
            lblRidgeMergeWidthDecay.AutoSize = true;
            lblRidgeMergeWidthDecay.Location = new Point(4, 644);
            lblRidgeMergeWidthDecay.Name = "lblRidgeMergeWidthDecay";
            lblRidgeMergeWidthDecay.Size = new Size(111, 15);
            lblRidgeMergeWidthDecay.TabIndex = 202;
            lblRidgeMergeWidthDecay.Text = "Merge Width Decay";
            // 
            // tbRidgeMergeWidthDecay
            // 
            tbRidgeMergeWidthDecay.Location = new Point(188, 641);
            tbRidgeMergeWidthDecay.Name = "tbRidgeMergeWidthDecay";
            tbRidgeMergeWidthDecay.Size = new Size(90, 23);
            tbRidgeMergeWidthDecay.TabIndex = 203;
            // 
            // lblHarmonicFamilyCentsTol
            // 
            lblHarmonicFamilyCentsTol.AutoSize = true;
            lblHarmonicFamilyCentsTol.Location = new Point(4, 676);
            lblHarmonicFamilyCentsTol.Name = "lblHarmonicFamilyCentsTol";
            lblHarmonicFamilyCentsTol.Size = new Size(150, 15);
            lblHarmonicFamilyCentsTol.TabIndex = 38;
            lblHarmonicFamilyCentsTol.Text = "Harmonic Family Cents Tol";
            // 
            // tbHarmonicFamilyCentsTol
            // 
            tbHarmonicFamilyCentsTol.Location = new Point(188, 673);
            tbHarmonicFamilyCentsTol.Name = "tbHarmonicFamilyCentsTol";
            tbHarmonicFamilyCentsTol.Size = new Size(90, 23);
            tbHarmonicFamilyCentsTol.TabIndex = 39;
            // 
            // lblHarmonicFamilyMaxRatio
            // 
            lblHarmonicFamilyMaxRatio.AutoSize = true;
            lblHarmonicFamilyMaxRatio.Location = new Point(4, 703);
            lblHarmonicFamilyMaxRatio.Name = "lblHarmonicFamilyMaxRatio";
            lblHarmonicFamilyMaxRatio.Size = new Size(153, 15);
            lblHarmonicFamilyMaxRatio.TabIndex = 40;
            lblHarmonicFamilyMaxRatio.Text = "Harmonic Family Max Ratio";
            // 
            // tbHarmonicFamilyMaxRatio
            // 
            tbHarmonicFamilyMaxRatio.Location = new Point(188, 700);
            tbHarmonicFamilyMaxRatio.Name = "tbHarmonicFamilyMaxRatio";
            tbHarmonicFamilyMaxRatio.Size = new Size(90, 23);
            tbHarmonicFamilyMaxRatio.TabIndex = 41;
            // 
            // lblHarmonicSuppression
            // 
            lblHarmonicSuppression.AutoSize = true;
            lblHarmonicSuppression.Location = new Point(4, 730);
            lblHarmonicSuppression.Name = "lblHarmonicSuppression";
            lblHarmonicSuppression.Size = new Size(127, 15);
            lblHarmonicSuppression.TabIndex = 70;
            lblHarmonicSuppression.Text = "Harmonic Suppression";
            // 
            // tbHarmonicSuppression
            // 
            tbHarmonicSuppression.Location = new Point(188, 727);
            tbHarmonicSuppression.Name = "tbHarmonicSuppression";
            tbHarmonicSuppression.Size = new Size(90, 23);
            tbHarmonicSuppression.TabIndex = 71;
            // 
            // lblChordAvgFrames
            // 
            lblChordAvgFrames.AutoSize = true;
            lblChordAvgFrames.Location = new Point(4, 762);
            lblChordAvgFrames.Name = "lblChordAvgFrames";
            lblChordAvgFrames.Size = new Size(105, 15);
            lblChordAvgFrames.TabIndex = 49;
            lblChordAvgFrames.Text = "Chord Avg Frames";
            // 
            // tbChordAvgFrames
            // 
            tbChordAvgFrames.Location = new Point(188, 759);
            tbChordAvgFrames.Name = "tbChordAvgFrames";
            tbChordAvgFrames.Size = new Size(90, 23);
            tbChordAvgFrames.TabIndex = 50;
            // 
            // lblChordOutPenalty
            // 
            lblChordOutPenalty.AutoSize = true;
            lblChordOutPenalty.Location = new Point(4, 789);
            lblChordOutPenalty.Name = "lblChordOutPenalty";
            lblChordOutPenalty.Size = new Size(105, 15);
            lblChordOutPenalty.TabIndex = 53;
            lblChordOutPenalty.Text = "Chord Out Penalty";
            // 
            // tbChordOutPenalty
            // 
            tbChordOutPenalty.Location = new Point(188, 786);
            tbChordOutPenalty.Name = "tbChordOutPenalty";
            tbChordOutPenalty.Size = new Size(90, 23);
            tbChordOutPenalty.TabIndex = 54;
            // 
            // lblAdd9ExtraScore
            // 
            lblAdd9ExtraScore.AutoSize = true;
            lblAdd9ExtraScore.Location = new Point(4, 816);
            lblAdd9ExtraScore.Name = "lblAdd9ExtraScore";
            lblAdd9ExtraScore.Size = new Size(124, 15);
            lblAdd9ExtraScore.TabIndex = 55;
            lblAdd9ExtraScore.Text = "Chord Analysis Ridges";
            // 
            // tbChordRidges
            // 
            tbChordRidges.Location = new Point(188, 813);
            tbChordRidges.Name = "tbChordRidges";
            tbChordRidges.Size = new Size(90, 23);
            tbChordRidges.TabIndex = 56;
            // 
            // lblKeyEmaSeconds
            // 
            lblKeyEmaSeconds.AutoSize = true;
            lblKeyEmaSeconds.Location = new Point(4, 845);
            lblKeyEmaSeconds.Name = "lblKeyEmaSeconds";
            lblKeyEmaSeconds.Size = new Size(101, 15);
            lblKeyEmaSeconds.TabIndex = 57;
            lblKeyEmaSeconds.Text = "Key EMA Seconds";
            // 
            // tbKeyEmaSeconds
            // 
            tbKeyEmaSeconds.Location = new Point(188, 842);
            tbKeyEmaSeconds.Name = "tbKeyEmaSeconds";
            tbKeyEmaSeconds.Size = new Size(90, 23);
            tbKeyEmaSeconds.TabIndex = 58;
            // 
            // lblRidgeMatchLogHz
            // 
            lblRidgeMatchLogHz.AutoSize = true;
            lblRidgeMatchLogHz.Location = new Point(4, 904);
            lblRidgeMatchLogHz.Name = "lblRidgeMatchLogHz";
            lblRidgeMatchLogHz.Size = new Size(111, 15);
            lblRidgeMatchLogHz.TabIndex = 0;
            lblRidgeMatchLogHz.Text = "Ridge Match LogHz";
            // 
            // tbRidgeMatchLogHz
            // 
            tbRidgeMatchLogHz.Location = new Point(188, 901);
            tbRidgeMatchLogHz.Name = "tbRidgeMatchLogHz";
            tbRidgeMatchLogHz.Size = new Size(90, 23);
            tbRidgeMatchLogHz.TabIndex = 0;
            // 
            // lblRidgeMatchLogHzPredBoost
            // 
            lblRidgeMatchLogHzPredBoost.AutoSize = true;
            lblRidgeMatchLogHzPredBoost.Location = new Point(4, 874);
            lblRidgeMatchLogHzPredBoost.Name = "lblRidgeMatchLogHzPredBoost";
            lblRidgeMatchLogHzPredBoost.Size = new Size(101, 15);
            lblRidgeMatchLogHzPredBoost.TabIndex = 0;
            lblRidgeMatchLogHzPredBoost.Text = "Match Pred Boost";
            // 
            // tbRidgeMatchLogHzPredBoost
            // 
            tbRidgeMatchLogHzPredBoost.Location = new Point(188, 874);
            tbRidgeMatchLogHzPredBoost.Name = "tbRidgeMatchLogHzPredBoost";
            tbRidgeMatchLogHzPredBoost.Size = new Size(90, 23);
            tbRidgeMatchLogHzPredBoost.TabIndex = 0;
            // 
            // lblMaxColShift
            // 
            lblMaxColShift.AutoSize = true;
            lblMaxColShift.Location = new Point(4, 935);
            lblMaxColShift.Name = "lblMaxColShift";
            lblMaxColShift.Size = new Size(77, 15);
            lblMaxColShift.TabIndex = 0;
            lblMaxColShift.Text = "Max Col Shift";
            // 
            // tbMaxColShift
            // 
            tbMaxColShift.Location = new Point(188, 932);
            tbMaxColShift.Name = "tbMaxColShift";
            tbMaxColShift.Size = new Size(90, 23);
            tbMaxColShift.TabIndex = 0;
            // 
            // lblTargetFps
            // 
            lblTargetFps.AutoSize = true;
            lblTargetFps.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblTargetFps.Location = new Point(3, 965);
            lblTargetFps.Name = "lblTargetFps";
            lblTargetFps.Size = new Size(66, 15);
            lblTargetFps.TabIndex = 0;
            lblTargetFps.Text = "Target FPS";
            // 
            // tbTargetFps
            // 
            tbTargetFps.Location = new Point(188, 962);
            tbTargetFps.Name = "tbTargetFps";
            tbTargetFps.Size = new Size(90, 23);
            tbTargetFps.TabIndex = 0;
            // 
            // lblFpsReadout
            // 
            lblFpsReadout.AutoSize = true;
            lblFpsReadout.Location = new Point(4, 993);
            lblFpsReadout.Name = "lblFpsReadout";
            lblFpsReadout.Size = new Size(81, 15);
            lblFpsReadout.TabIndex = 0;
            lblFpsReadout.Text = "Actual FPS: —";
            // 
            // panel2
            // 
            panel2.BackColor = Color.Black;
            panel2.Controls.Add(pic);
            panel2.Dock = DockStyle.Fill;
            panel2.Location = new Point(310, 0);
            panel2.Name = "panel2";
            panel2.Size = new Size(1178, 976);
            panel2.TabIndex = 1;
            // 
            // pic
            // 
            pic.Dock = DockStyle.Fill;
            pic.Location = new Point(0, 0);
            pic.Name = "pic";
            pic.Size = new Size(1178, 976);
            pic.TabIndex = 0;
            pic.TabStop = false;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1488, 976);
            Controls.Add(panel2);
            Controls.Add(panel1);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "Form1";
            Text = "SpectrumNotes";
            panel1.ResumeLayout(false);
            panel1.PerformLayout();
            tabControl1.ResumeLayout(false);
            tabAnalysis.ResumeLayout(false);
            tabAnalysis.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)pbHarmonics).EndInit();
            ((System.ComponentModel.ISupportInitialize)pbTuner).EndInit();
            ((System.ComponentModel.ISupportInitialize)volume).EndInit();
            ((System.ComponentModel.ISupportInitialize)pbKey).EndInit();
            tabSettings.ResumeLayout(false);
            settingsScroll.ResumeLayout(false);
            settingsScroll.PerformLayout();
            panel2.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        // ── Designer field declarations ───────────────────────────────────────
        private Button btnRecord;
        private Panel panel1;
        private Panel panel2;
        private SpectrogramPanel pic;
        private ComboBox comboBox1;
        private CheckBox cblines;
        private CheckBox cbFlats;
        private TabControl tabControl1;
        private TabPage tabAnalysis;
        private TabPage tabSettings;
        private Panel settingsScroll;

        private TextBox tbFFTSize;
        private TextBox tbOverlapFactor;
        // Dual-FFT controls
        // lblLowFftSize and tbLowFftSize removed
        private CheckBox cbLowFft;
        private Label lblLowFftCrossoverNote;
        private TextBox tbLowFftCrossoverNote;
        private Label lblLowFftCrossoverSemitones;
        private TextBox tbLowFftCrossoverSemitones;
        private TextBox tbMaxPeaksPerFrame;
        private TextBox tbPeakMinRel;
        private TextBox tbRidgeMatchLogHz;
        private TextBox tbRidgeMaxCentsJump;
        private TextBox tbRidgeMissMax;
        private TextBox tbRidgeFreqEMA;
        private TextBox tbRidgeVelEMA;
        private Label lblRidgeIntensityEMA;
        private TextBox tbRidgeIntensityEMA;
        private TextBox tbPeakGamma;
        private TextBox tbHarmonicFamilyCentsTol;
        private TextBox tbHarmonicFamilyMaxRatio;

        private Label lblFFTSize;
        private Label lblOverlapFactor;
        private Label lblMaxPeaksPerFrame;
        private Label lblPeakMinRel;
        private Label lblRidgeMatchLogHz;
        private Label lblRidgeMatchLogHzPredBoost;
        private Label lblRidgeMaxCentsJump;
        private Label lblRidgeMissMax;
        private Label lblRidgeFreqEMA;
        private Label lblRidgeVelEMA;
        private Label lblPeakGamma;
        private Label lblHarmonicFamilyCentsTol;
        private Label lblHarmonicFamilyMaxRatio;
        private Label chordlabel;

        private TextBox tbPeakMinSpacingCents;
        private Label lblPeakMinSpacingCents;

        private TextBox tbRidgeMergeCents;
        private Label lblRidgeMergeCents;

        private TextBox tbRidgeMergeBrightnessBoost;
        private Label lblRidgeMergeBrightnessBoost;

        private TextBox tbRidgeMergeWidthAdd;
        private Label lblRidgeMergeWidthAdd;
        private Label lblRidgeMergeWidthDecay;
        private TextBox tbRidgeMergeWidthDecay;

        private Label lblMissFadePow;
        private TextBox tbMissFadePow;
        private Label lblAgeDecay;
        private TextBox tbAgeDecay;
        private Label lblMinDrawAlpha;
        private TextBox tbMinDrawAlpha;

        private TextBox tbChordAvgFrames;
        private Label lblChordAvgFrames;

        private TextBox tbChordOutPenalty;
        private Label lblChordOutPenalty;

        private TextBox tbChordRidges;
        private Label lblAdd9ExtraScore;
        private Label lblLevelSmoothEma;
        private TextBox tbLevelSmoothEma;
        private TextBox tbCanonicalNotes;
        private Label label3;
        private TextBox tbtuning;
        private PictureBox pbKey;
        private Label lblPeakMode;
        private TextBox tbPeakMode;
        private Label lblHarmonicSuppression;
        private TextBox tbHarmonicSuppression;
        private Label label4;
        private TextBox tbLowPass;
        private Label lblAdd9MinNinthEnergy;
        private TextBox tbHighPass;
        private PictureBox pbTuner;
        private TextBox tbDetectedNotes;

        private Label lbEmail;
        private TrackBar volume;
        private CheckBox cbVolume;
        private PictureBox pbHarmonics;
        private Label lblMaxColShift;
        private TextBox tbMaxColShift;
        private Label lblTargetFps;
        private TextBox tbTargetFps;
        private Label lblFpsReadout;
        private Label label2;
        private ComboBox cmbScaleMode;
        private CheckBox cbAutoKey;
        private Label lblKeyEmaSeconds;
        private TextBox tbKeyEmaSeconds;
        private ComboBox cmbScaleRoot;
        private TextBox tbRidgeMatchLogHzPredBoost;
    }
}
