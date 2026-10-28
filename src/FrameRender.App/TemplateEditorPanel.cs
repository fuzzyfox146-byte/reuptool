using System.Drawing.Drawing2D;
using System.Globalization;
using VideoAutoTool.Core.FrameBatch;

namespace FrameRender;

public sealed class TemplateEditorPanel : UserControl
{
    private readonly List<FrameTemplatePreset> _presets = [];
    private FrameTemplatePreset _currentPreset = FrameTemplatePreset.CreateDefault();
    private FrameTemplateLayer? _selectedLayer;
    private readonly FrameTemplateHistory _history = new();
    private bool _suppressEvents;
    private Button _undoBtn = null!;
    private Button _redoBtn = null!;
    private readonly Label _historyStatus = new() { AutoSize = true, ForeColor = Color.FromArgb(140, 220, 160), Padding = new Padding(8, 5, 4, 0) };

    private readonly ComboBox _presetCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240 };
    private readonly TextBox _presetName = new() { Width = 200 };
    private readonly CheckBox _audioOnly = new() { Text = "Chỉ lấy tiếng nguồn (Truyện/Podcast)", AutoSize = true };
    private readonly ComboBox _mirrorMode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
    private readonly TextBox _audioSpeed = new() { Width = 48, Text = "1.00" };
    private readonly TextBox _audioPitch = new() { Width = 48, Text = "0" };
    private readonly TextBox _audioVolume = new() { Width = 48, Text = "1.00" };

    private readonly ListBox _layerList = new() { IntegralHeight = false, Dock = DockStyle.Fill };
    private readonly ComboBox _addKindCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
    private readonly CanvasPreviewBox _canvas = new() { Dock = DockStyle.Fill };

    // Effect Controls
    private readonly TextBox _layerName = new() { Width = 180 };
    private readonly CheckBox _layerEnabled = new() { Text = "Bật layer (Hiện)", AutoSize = true };
    private readonly ComboBox _layerPick = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    private readonly TextBox _layerFolder = new() { Width = 210 };
    private readonly TextBox _layerFixedFile = new() { Width = 210 };

    private readonly CheckBox _fullCanvas = new() { Text = "Phủ kín khung 1280×720", AutoSize = true };
    private readonly CheckBox _useRefHole = new() { Text = "Khớp ô khung chuẩn", AutoSize = true };
    private readonly TextBox _posX = new() { Width = 54 };
    private readonly TextBox _posY = new() { Width = 54 };
    private readonly TextBox _sizeW = new() { Width = 54 };
    private readonly TextBox _sizeH = new() { Width = 54 };
    private readonly TextBox _scale = new() { Width = 54 };
    private readonly TextBox _opacity = new() { Width = 54 };

    private readonly CheckBox _flipH = new() { Text = "Lật ngang (Flip H)", AutoSize = true };
    private readonly CheckBox _flipV = new() { Text = "Lật dọc (Flip V)", AutoSize = true };
    private readonly CheckBox _mirrorWithSide = new() { Text = "Đổi bên theo gương", AutoSize = true };

    private readonly TextBox _cropL = new() { Width = 48 };
    private readonly TextBox _cropR = new() { Width = 48 };
    private readonly TextBox _cropT = new() { Width = 48 };
    private readonly TextBox _cropB = new() { Width = 48 };

    private readonly ComboBox _keyMode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
    private readonly TextBox _keyColor = new() { Width = 76 };
    private readonly Panel _keyColorSwatch = new() { Width = 22, Height = 22, BorderStyle = BorderStyle.FixedSingle };
    private readonly TextBox _keyTol = new() { Width = 50 };
    private readonly TextBox _keySoft = new() { Width = 50 };
    private Button _eyedropperBtn = null!;

    private readonly TextBox _brightness = new() { Width = 50 };
    private readonly TextBox _contrast = new() { Width = 50 };
    private readonly TextBox _saturation = new() { Width = 50 };
    private readonly TextBox _blur = new() { Width = 50 };
    private readonly TextBox _speed = new() { Width = 50 };

    private readonly TextBox _fontName = new() { Width = 130 };
    private readonly TextBox _fontSize = new() { Width = 50 };
    private readonly TextBox _textColor = new() { Width = 180 };
    private readonly TextBox _outlineColor = new() { Width = 80 };
    private readonly TextBox _outlineWidth = new() { Width = 50 };
    private readonly TextBox _shadowDepth = new() { Width = 50 };
    private readonly TextBox _staticText = new() { Width = 220 };
    private readonly CheckBox _stripEmoji = new() { Text = "Bỏ Emoji", AutoSize = true };
    private readonly CheckBox _uppercase = new() { Text = "CHỮ IN HOA", AutoSize = true };

    public event Action? PresetsChanged;

    public TemplateEditorPanel()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(30, 30, 30);
        ForeColor = Color.White;
        BuildUi();
    }

    public IReadOnlyList<FrameTemplatePreset> Presets => _presets;

    public FrameTemplatePreset CurrentPreset => _currentPreset;

    public void LoadPresets(
        IReadOnlyList<FrameTemplatePreset>? savedPresets,
        string? selectedName,
        string defaultFrames,
        string defaultBackgrounds,
        double defaultScale,
        double defaultOpacity,
        bool defaultRandom)
    {
        _presets.Clear();
        if (savedPresets is { Count: > 0 })
        {
            foreach (var preset in savedPresets)
            {
                _presets.Add(preset.Clone());
            }
        }
        else
        {
            _presets.Add(FrameTemplatePreset.CreateDefault(defaultFrames, defaultBackgrounds, defaultScale, defaultOpacity, defaultRandom));
            _presets.Add(FrameTemplatePreset.CreateStoryPreset(defaultBackgrounds));
        }

        var chosen = _presets.FirstOrDefault(p => string.Equals(p.Name, selectedName, StringComparison.OrdinalIgnoreCase))
            ?? _presets[0];
        SelectPreset(chosen);
        RefreshPresetCombo();
    }

    public void SelectPresetByName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var found = _presets.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        if (found is not null && !ReferenceEquals(found, _currentPreset))
        {
            SelectPreset(found);
            RefreshPresetCombo();
        }
    }

    public void SyncLegacyFieldsIfDefault(string frames, string backgrounds, double scale, double opacity, bool random)
    {
        if (_presets.Count == 0)
        {
            return;
        }

        foreach (var layer in _currentPreset.Layers)
        {
            if (layer.Kind == FrameLayerKind.FrameOverlay && string.IsNullOrWhiteSpace(layer.Folder))
            {
                layer.Folder = frames;
            }
            else if (layer.Kind == FrameLayerKind.BackgroundVideo && string.IsNullOrWhiteSpace(layer.Folder))
            {
                layer.Folder = backgrounds;
            }
        }

        if (_selectedLayer is not null)
        {
            LoadLayerToControls(_selectedLayer);
        }

        _canvas.Invalidate();
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.FromArgb(30, 30, 30),
            Padding = new Padding(6)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // Row 0: Preset bar
        var presetBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true,
            BackColor = Color.FromArgb(38, 38, 42),
            Padding = new Padding(6)
        };
        StyleCombo(_presetCombo);
        StyleBox(_presetName);
        _presetCombo.SelectedIndexChanged += (_, _) =>
        {
            if (_suppressEvents || _presetCombo.SelectedIndex < 0 || _presetCombo.SelectedIndex >= _presets.Count)
            {
                return;
            }

            SelectPreset(_presets[_presetCombo.SelectedIndex]);
            PresetsChanged?.Invoke();
        };

        presetBar.Controls.Add(MakeLabel("Mẫu chủ đề:"));
        presetBar.Controls.Add(_presetCombo);
        presetBar.Controls.Add(MakeLabel("Tên mẫu:"));
        presetBar.Controls.Add(_presetName);
        presetBar.Controls.Add(MakeButton("Lưu mẫu (Ctrl+S)", (_, _) => SaveCurrentPresetName(), Color.FromArgb(0, 120, 215)));
        _undoBtn = MakeButton("↶ Hoàn tác (Ctrl+Z)", (_, _) => Undo());
        _redoBtn = MakeButton("↷ Làm lại (Ctrl+Shift+Z)", (_, _) => Redo());
        presetBar.Controls.Add(_undoBtn);
        presetBar.Controls.Add(_redoBtn);
        presetBar.Controls.Add(MakeButton("+ Nhân bản mẫu", (_, _) => DuplicatePreset()));
        presetBar.Controls.Add(MakeButton("+ Mẫu Reup Khung", (_, _) => AddBuiltInPreset(story: false)));
        presetBar.Controls.Add(MakeButton("+ Mẫu Truyện / Sub / Wave", (_, _) => AddBuiltInPreset(story: true)));
        presetBar.Controls.Add(MakeButton("Xóa mẫu", (_, _) => DeleteCurrentPreset(), Color.FromArgb(165, 40, 35)));
        presetBar.Controls.Add(_historyStatus);

        // Row 1: Global template audio & mirror bar
        var globalBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true,
            BackColor = Color.FromArgb(34, 34, 38),
            Padding = new Padding(6, 4, 6, 4)
        };
        StyleCheck(_audioOnly);
        _mirrorMode.Items.AddRange(["Không lật gương", "Luôn bên phải", "Luôn bên trái", "Xen kẽ Chẵn / Lẻ"]);
        StyleCombo(_mirrorMode);
        StyleBox(_audioSpeed);
        StyleBox(_audioPitch);
        StyleBox(_audioVolume);

        _audioOnly.CheckedChanged += (_, _) => ApplyPresetHeaderFromControls();
        _mirrorMode.SelectedIndexChanged += (_, _) => ApplyPresetHeaderFromControls();
        _audioSpeed.Leave += (_, _) => ApplyPresetHeaderFromControls();
        _audioPitch.Leave += (_, _) => ApplyPresetHeaderFromControls();
        _audioVolume.Leave += (_, _) => ApplyPresetHeaderFromControls();

        globalBar.Controls.Add(_audioOnly);
        globalBar.Controls.Add(MakeLabel("Lật bố cục:"));
        globalBar.Controls.Add(_mirrorMode);
        globalBar.Controls.Add(MakeLabel("Tốc độ tiếng:"));
        globalBar.Controls.Add(_audioSpeed);
        globalBar.Controls.Add(MakeLabel("Pitch (bán âm):"));
        globalBar.Controls.Add(_audioPitch);
        globalBar.Controls.Add(MakeLabel("Âm lượng:"));
        globalBar.Controls.Add(_audioVolume);

        // Row 2: 3-column studio (Left: Layer Stack, Center: Canvas Preview, Right: Effect Controls)
        var studio = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.FromArgb(30, 30, 30)
        };
        studio.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 265));
        studio.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        studio.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 430));

        studio.Controls.Add(BuildLayerStackPanel(), 0, 0);
        studio.Controls.Add(BuildCanvasPanel(), 1, 0);
        studio.Controls.Add(BuildEffectControlsPanel(), 2, 0);

        root.Controls.Add(presetBar, 0, 0);
        root.Controls.Add(globalBar, 0, 1);
        root.Controls.Add(studio, 0, 2);
        Controls.Add(root);
    }

    private Control BuildLayerStackPanel()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.FromArgb(35, 35, 38),
            Padding = new Padding(6)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var caption = new Label
        {
            Text = "Thứ tự Layer (Dưới → Trên)",
            AutoSize = true,
            ForeColor = Color.FromArgb(180, 220, 255),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Padding = new Padding(0, 0, 0, 4)
        };

        _layerList.BackColor = Color.FromArgb(45, 45, 48);
        _layerList.ForeColor = Color.White;
        _layerList.BorderStyle = BorderStyle.FixedSingle;
        _layerList.SelectedIndexChanged += (_, _) =>
        {
            if (_suppressEvents || _layerList.SelectedIndex < 0 || _layerList.SelectedIndex >= _currentPreset.Layers.Count)
            {
                return;
            }

            _selectedLayer = _currentPreset.Layers[_layerList.SelectedIndex];
            LoadLayerToControls(_selectedLayer);
            _canvas.SelectedLayer = _selectedLayer;
            _canvas.Invalidate();
        };

        var bottomTools = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            WrapContents = true,
            BackColor = Color.FromArgb(35, 35, 38)
        };
        _addKindCombo.Items.AddRange(
        [
            "Video nền",
            "Video chính",
            "Khung PNG tràn viền",
            "Ảnh / Avatar / Logo",
            "Video lặp / Wave / Effect",
            "Phụ đề (SRT/ASS)",
            "Chữ cố định"
        ]);
        _addKindCombo.SelectedIndex = 3;
        StyleCombo(_addKindCombo);

        bottomTools.Controls.Add(_addKindCombo);
        bottomTools.Controls.Add(MakeButton("+ Thêm", (_, _) => AddLayer(), Color.FromArgb(0, 120, 215)));
        bottomTools.Controls.Add(MakeButton("▲ Lên", (_, _) => MoveLayer(-1)));
        bottomTools.Controls.Add(MakeButton("▼ Xuống", (_, _) => MoveLayer(1)));
        bottomTools.Controls.Add(MakeButton("Nhân bản", (_, _) => CloneSelectedLayer()));
        bottomTools.Controls.Add(MakeButton("Xóa", (_, _) => RemoveSelectedLayer(), Color.FromArgb(165, 40, 35)));

        layout.Controls.Add(caption, 0, 0);
        layout.Controls.Add(_layerList, 0, 1);
        layout.Controls.Add(bottomTools, 0, 2);
        return layout;
    }

    private Control BuildCanvasPanel()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.FromArgb(25, 25, 28),
            Padding = new Padding(6)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var hint = new Label
        {
            Text = "Canvas 1280×720: Kéo giữa ô để di chuyển X/Y · Kéo 8 chấm vàng (4 góc / 4 cạnh) để co giãn kích thước · Bấm '🎯 Chấm màu trên ảnh' để tách nền Ultra Key",
            AutoSize = true,
            ForeColor = Color.LightGray,
            Padding = new Padding(0, 0, 0, 4)
        };

        _canvas.LayerMovedOrResized += layer =>
        {
            _selectedLayer = layer;
            RefreshLayerListLabels();
            LoadLayerToControls(layer);
            NotifyPresetModified("Đã cập nhật vị trí/kích thước layer");
        };
        _canvas.ColorPickedFromCanvas += (layer, hexColor) =>
        {
            _selectedLayer = layer;
            layer.KeyMode = FrameKeyMode.CustomColorKey;
            layer.KeyColor = hexColor;
            UpdateEyedropperButtonState(false);
            LoadLayerToControls(layer);
            _canvas.InvalidateKeyCache();
            _canvas.Invalidate();
            NotifyPresetModified($"Đã chấm màu Ultra Key {hexColor}");
        };

        layout.Controls.Add(hint, 0, 0);
        layout.Controls.Add(_canvas, 0, 1);
        return layout;
    }

    private Control BuildEffectControlsPanel()
    {
        var scroll = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.FromArgb(35, 35, 38),
            Padding = new Padding(8)
        };

        var stack = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.FromArgb(35, 35, 38)
        };

        stack.Controls.Add(SectionHeader("1. Nguồn & Quy tắc chọn file (Pick Mode)"));
        StyleBox(_layerName);
        StyleCheck(_layerEnabled);
        _layerPick.Items.AddRange(
        [
            "Xoay vòng lần lượt (RoundRobin)",
            "Ngẫu nhiên (Random)",
            "Cố định 1 file (Fixed)",
            "Ghép cùng số 001 (MatchByNumber)"
        ]);
        StyleCombo(_layerPick);
        StyleBox(_layerFolder);
        StyleBox(_layerFixedFile);

        stack.Controls.Add(Line("Tên layer:", _layerName, _layerEnabled));
        stack.Controls.Add(Line("Chế độ lấy:", _layerPick));
        stack.Controls.Add(FolderLine("Folder nguồn:", _layerFolder));
        stack.Controls.Add(FileLine("File cố định:", _layerFixedFile));

        stack.Controls.Add(SectionHeader("2. Vị trí, Kích thước & Cắt viền (Motion & Crop)"));
        StyleCheck(_fullCanvas);
        StyleCheck(_useRefHole);
        StyleBox(_posX);
        StyleBox(_posY);
        StyleBox(_sizeW);
        StyleBox(_sizeH);
        StyleBox(_scale);
        StyleBox(_opacity);
        StyleCheck(_flipH);
        StyleCheck(_flipV);
        StyleCheck(_mirrorWithSide);
        StyleBox(_cropL);
        StyleBox(_cropR);
        StyleBox(_cropT);
        StyleBox(_cropB);

        stack.Controls.Add(Line("", _fullCanvas, _useRefHole));
        stack.Controls.Add(Line("X:", _posX, MakeLabel("Y:"), _posY, MakeLabel("Rộng:"), _sizeW, MakeLabel("Cao:"), _sizeH));
        stack.Controls.Add(Line("Scale ô:", _scale, MakeLabel("Opacity (0-1):"), _opacity));
        stack.Controls.Add(Line("", _flipH, _flipV, _mirrorWithSide));
        stack.Controls.Add(Line("Crop Trái:", _cropL, MakeLabel("Phải:"), _cropR, MakeLabel("Trên:"), _cropT, MakeLabel("Dưới:"), _cropB));

        stack.Controls.Add(SectionHeader("3. Ultra Key Tách nền, Chỉnh màu & Tốc độ (Effects)"));
        _keyMode.Items.AddRange(
        [
            "Không tách nền",
            "Tách nền đen (Luma Key)",
            "Tách phông xanh (Green Key)",
            "Ultra Key — Tách màu bất kỳ (#Hex)"
        ]);
        StyleCombo(_keyMode);
        StyleBox(_keyColor);
        StyleBox(_keyTol);
        StyleBox(_keySoft);
        StyleBox(_brightness);
        StyleBox(_contrast);
        StyleBox(_saturation);
        StyleBox(_blur);
        StyleBox(_speed);

        _eyedropperBtn = MakeButton("🎯 Chấm màu trên ảnh", (_, _) => ToggleEyedropper(), Color.FromArgb(0, 120, 215));
        var autoCornerBtn = MakeButton("🪄 Tự hút màu góc ảnh", (_, _) => AutoPickCornerKeyColor());
        var colorDialogBtn = MakeButton("Bảng màu...", (_, _) => PickKeyColorWithDialog());

        stack.Controls.Add(Line("Keying:", _keyMode));
        stack.Controls.Add(Line("Màu tách:", _keyColor, _keyColorSwatch, _eyedropperBtn, autoCornerBtn, colorDialogBtn));
        stack.Controls.Add(Line("Độ nhạy (0.02-0.6):", _keyTol, MakeLabel("Mềm viền (0-0.5):"), _keySoft, MakeLabel("Speed:"), _speed));
        stack.Controls.Add(Line("Sáng (-1..1):", _brightness, MakeLabel("Tương phản:"), _contrast, MakeLabel("Bão hòa:"), _saturation));
        stack.Controls.Add(Line("Làm mờ (Blur 0-30):", _blur));

        stack.Controls.Add(SectionHeader("4. Phụ đề SRT & Chữ (Subtitle Style)"));
        StyleBox(_fontName);
        StyleBox(_fontSize);
        StyleBox(_textColor);
        StyleBox(_outlineColor);
        StyleBox(_outlineWidth);
        StyleBox(_shadowDepth);
        StyleBox(_staticText);
        StyleCheck(_stripEmoji);
        StyleCheck(_uppercase);

        stack.Controls.Add(Line("Font:", _fontName, MakeLabel("Cỡ chữ:"), _fontSize));
        stack.Controls.Add(Line("Màu chữ (nhiều mã cách phẩy):", _textColor));
        stack.Controls.Add(Line("Màu viền:", _outlineColor, MakeLabel("Dày viền:"), _outlineWidth, MakeLabel("Bóng:"), _shadowDepth));
        stack.Controls.Add(Line("Chữ cố định:", _staticText));
        stack.Controls.Add(Line("", _stripEmoji, _uppercase));

        var applyBtn = MakeButton("Áp dụng thông số Layer (Enter / Ctrl+S)", (_, _) => WriteControlsToLayer(), Color.FromArgb(0, 120, 215));
        applyBtn.Margin = new Padding(4, 10, 4, 10);
        stack.Controls.Add(applyBtn);

        WireInstantApply(
            _layerName, _posX, _posY, _sizeW, _sizeH, _scale, _opacity,
            _cropL, _cropR, _cropT, _cropB, _keyColor, _keyTol, _keySoft,
            _brightness, _contrast, _saturation, _blur, _speed,
            _fontName, _fontSize, _textColor, _outlineColor, _outlineWidth, _shadowDepth, _staticText,
            _layerFolder, _layerFixedFile);

        _layerEnabled.CheckedChanged += (_, _) => WriteControlsToLayer();
        _layerPick.SelectedIndexChanged += (_, _) => WriteControlsToLayer();
        _fullCanvas.CheckedChanged += (_, _) => WriteControlsToLayer();
        _useRefHole.CheckedChanged += (_, _) => WriteControlsToLayer();
        _flipH.CheckedChanged += (_, _) => WriteControlsToLayer();
        _flipV.CheckedChanged += (_, _) => WriteControlsToLayer();
        _mirrorWithSide.CheckedChanged += (_, _) => WriteControlsToLayer();
        _keyMode.SelectedIndexChanged += (_, _) => WriteControlsToLayer();
        _stripEmoji.CheckedChanged += (_, _) => WriteControlsToLayer();
        _uppercase.CheckedChanged += (_, _) => WriteControlsToLayer();

        scroll.Controls.Add(stack);
        return scroll;
    }

    private void WireInstantApply(params TextBox[] boxes)
    {
        foreach (var box in boxes)
        {
            box.Leave += (_, _) => WriteControlsToLayer();
            box.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    WriteControlsToLayer();
                    e.SuppressKeyPress = true;
                }
            };
        }
    }

    private void SelectPreset(FrameTemplatePreset preset, bool resetHistory = true)
    {
        _suppressEvents = true;
        try
        {
            _currentPreset = preset;
            if (resetHistory)
            {
                _history.Reset(preset);
                UpdateUndoRedoButtons();
            }

            _presetName.Text = preset.Name;
            _audioOnly.Checked = preset.AudioOnlyDriver;
            _mirrorMode.SelectedIndex = preset.MirrorSide switch
            {
                FrameMirrorMode.Right => 1,
                FrameMirrorMode.Left => 2,
                FrameMirrorMode.Alternate => 3,
                _ => 0
            };
            _audioSpeed.Text = preset.AudioSpeed.ToString("0.##", CultureInfo.InvariantCulture);
            _audioPitch.Text = preset.AudioPitchSemitones.ToString("0.#", CultureInfo.InvariantCulture);
            _audioVolume.Text = preset.AudioVolume.ToString("0.##", CultureInfo.InvariantCulture);

            RefreshLayerListLabels();
            _selectedLayer = _currentPreset.Layers.FirstOrDefault();
            if (_currentPreset.Layers.Count > 0)
            {
                _layerList.SelectedIndex = 0;
            }

            if (_selectedLayer is not null)
            {
                LoadLayerToControls(_selectedLayer);
            }

            _canvas.Preset = _currentPreset;
            _canvas.SelectedLayer = _selectedLayer;
            _canvas.Invalidate();
        }
        finally
        {
            _suppressEvents = false;
        }
    }

    private void RefreshPresetCombo()
    {
        _suppressEvents = true;
        try
        {
            _presetCombo.Items.Clear();
            foreach (var preset in _presets)
            {
                _presetCombo.Items.Add(preset.Name);
            }

            var idx = _presets.IndexOf(_currentPreset);
            _presetCombo.SelectedIndex = idx >= 0 ? idx : 0;
        }
        finally
        {
            _suppressEvents = false;
        }
    }

    private void RefreshLayerListLabels()
    {
        _suppressEvents = true;
        try
        {
            var prev = _selectedLayer;
            _layerList.Items.Clear();
            for (var i = 0; i < _currentPreset.Layers.Count; i++)
            {
                var layer = _currentPreset.Layers[i];
                var eye = layer.Enabled ? "👁" : "🚫";
                var pickTag = layer.Kind is FrameLayerKind.MainVideo or FrameLayerKind.StaticText
                    ? ""
                    : $" [{ShortPick(layer.Pick)}]";
                _layerList.Items.Add($"{i + 1}. {eye} {layer.Name} ({KindLabel(layer.Kind)}){pickTag}");
            }

            if (prev is not null)
            {
                var idx = _currentPreset.Layers.IndexOf(prev);
                if (idx >= 0)
                {
                    _layerList.SelectedIndex = idx;
                }
            }
        }
        finally
        {
            _suppressEvents = false;
        }
    }

    private void LoadLayerToControls(FrameTemplateLayer layer)
    {
        _suppressEvents = true;
        try
        {
            _layerName.Text = layer.Name;
            _layerEnabled.Checked = layer.Enabled;
            _layerPick.SelectedIndex = layer.Pick switch
            {
                FrameLayerPick.Random => 1,
                FrameLayerPick.Fixed => 2,
                FrameLayerPick.MatchByNumber => 3,
                _ => 0
            };
            _layerFolder.Text = layer.Folder;
            _layerFixedFile.Text = layer.FixedFile;

            _fullCanvas.Checked = layer.FullCanvas;
            _useRefHole.Checked = layer.UseReferenceHole;
            _posX.Text = layer.X.ToString(CultureInfo.InvariantCulture);
            _posY.Text = layer.Y.ToString(CultureInfo.InvariantCulture);
            _sizeW.Text = layer.Width.ToString(CultureInfo.InvariantCulture);
            _sizeH.Text = layer.Height.ToString(CultureInfo.InvariantCulture);
            _scale.Text = layer.Scale.ToString("0.##", CultureInfo.InvariantCulture);
            _opacity.Text = layer.Opacity.ToString("0.##", CultureInfo.InvariantCulture);

            _flipH.Checked = layer.FlipHorizontal;
            _flipV.Checked = layer.FlipVertical;
            _mirrorWithSide.Checked = layer.MirrorWithSide;

            _cropL.Text = layer.CropLeft.ToString("0.##", CultureInfo.InvariantCulture);
            _cropR.Text = layer.CropRight.ToString("0.##", CultureInfo.InvariantCulture);
            _cropT.Text = layer.CropTop.ToString("0.##", CultureInfo.InvariantCulture);
            _cropB.Text = layer.CropBottom.ToString("0.##", CultureInfo.InvariantCulture);

            _keyMode.SelectedIndex = layer.KeyMode switch
            {
                FrameKeyMode.BlackKey => 1,
                FrameKeyMode.GreenKey => 2,
                FrameKeyMode.CustomColorKey => 3,
                _ => 0
            };
            _keyColor.Text = layer.KeyColor;
            _keyColorSwatch.BackColor = ParseHexColor(layer.KeyColor);
            _keyTol.Text = layer.KeyTolerance.ToString("0.##", CultureInfo.InvariantCulture);
            _keySoft.Text = layer.KeySoftness.ToString("0.##", CultureInfo.InvariantCulture);

            _brightness.Text = layer.Brightness.ToString("0.##", CultureInfo.InvariantCulture);
            _contrast.Text = layer.Contrast.ToString("0.##", CultureInfo.InvariantCulture);
            _saturation.Text = layer.Saturation.ToString("0.##", CultureInfo.InvariantCulture);
            _blur.Text = layer.Blur.ToString("0.#", CultureInfo.InvariantCulture);
            _speed.Text = layer.Speed.ToString("0.##", CultureInfo.InvariantCulture);

            _fontName.Text = layer.FontName;
            _fontSize.Text = layer.FontSize.ToString(CultureInfo.InvariantCulture);
            _textColor.Text = layer.TextColor;
            _outlineColor.Text = layer.OutlineColor;
            _outlineWidth.Text = layer.OutlineWidth.ToString("0.#", CultureInfo.InvariantCulture);
            _shadowDepth.Text = layer.ShadowDepth.ToString("0.#", CultureInfo.InvariantCulture);
            _staticText.Text = layer.StaticText;
            _stripEmoji.Checked = layer.StripEmoji;
            _uppercase.Checked = layer.Uppercase;
        }
        finally
        {
            _suppressEvents = false;
        }
    }

    private void WriteControlsToLayer()
    {
        if (_suppressEvents || _selectedLayer is null)
        {
            return;
        }

        var layer = _selectedLayer;
        if (!string.IsNullOrWhiteSpace(_layerName.Text))
        {
            layer.Name = _layerName.Text.Trim();
        }

        layer.Enabled = _layerEnabled.Checked;
        layer.Pick = _layerPick.SelectedIndex switch
        {
            1 => FrameLayerPick.Random,
            2 => FrameLayerPick.Fixed,
            3 => FrameLayerPick.MatchByNumber,
            _ => FrameLayerPick.RoundRobin
        };
        layer.Folder = _layerFolder.Text.Trim();
        layer.FixedFile = _layerFixedFile.Text.Trim();

        layer.FullCanvas = _fullCanvas.Checked;
        layer.UseReferenceHole = _useRefHole.Checked;
        layer.X = ParseInt(_posX.Text, layer.X);
        layer.Y = ParseInt(_posY.Text, layer.Y);
        layer.Width = Math.Max(16, ParseInt(_sizeW.Text, layer.Width));
        layer.Height = Math.Max(16, ParseInt(_sizeH.Text, layer.Height));
        layer.Scale = Math.Clamp(ParseDouble(_scale.Text, layer.Scale), 0.1, 3.0);
        layer.Opacity = Math.Clamp(ParseDouble(_opacity.Text, layer.Opacity), 0.0, 1.0);

        layer.FlipHorizontal = _flipH.Checked;
        layer.FlipVertical = _flipV.Checked;
        layer.MirrorWithSide = _mirrorWithSide.Checked;

        layer.CropLeft = Math.Clamp(ParseDouble(_cropL.Text, layer.CropLeft), 0.0, 0.45);
        layer.CropRight = Math.Clamp(ParseDouble(_cropR.Text, layer.CropRight), 0.0, 0.45);
        layer.CropTop = Math.Clamp(ParseDouble(_cropT.Text, layer.CropTop), 0.0, 0.45);
        layer.CropBottom = Math.Clamp(ParseDouble(_cropB.Text, layer.CropBottom), 0.0, 0.45);

        layer.KeyMode = _keyMode.SelectedIndex switch
        {
            1 => FrameKeyMode.BlackKey,
            2 => FrameKeyMode.GreenKey,
            3 => FrameKeyMode.CustomColorKey,
            _ => FrameKeyMode.None
        };
        layer.KeyColor = _keyColor.Text.Trim();
        _keyColorSwatch.BackColor = ParseHexColor(layer.KeyColor);
        layer.KeyTolerance = Math.Clamp(ParseDouble(_keyTol.Text, layer.KeyTolerance), 0.01, 0.80);
        layer.KeySoftness = Math.Clamp(ParseDouble(_keySoft.Text, layer.KeySoftness), 0.0, 0.80);

        layer.Brightness = Math.Clamp(ParseDouble(_brightness.Text, layer.Brightness), -1.0, 1.0);
        layer.Contrast = Math.Clamp(ParseDouble(_contrast.Text, layer.Contrast), 0.1, 3.0);
        layer.Saturation = Math.Clamp(ParseDouble(_saturation.Text, layer.Saturation), 0.0, 3.0);
        layer.Blur = Math.Clamp(ParseDouble(_blur.Text, layer.Blur), 0.0, 40.0);
        layer.Speed = Math.Clamp(ParseDouble(_speed.Text, layer.Speed), 0.2, 4.0);

        layer.FontName = string.IsNullOrWhiteSpace(_fontName.Text) ? "Arial" : _fontName.Text.Trim();
        layer.FontSize = Math.Clamp(ParseInt(_fontSize.Text, layer.FontSize), 12, 160);
        layer.TextColor = string.IsNullOrWhiteSpace(_textColor.Text) ? "#FFFFFF" : _textColor.Text.Trim();
        layer.OutlineColor = string.IsNullOrWhiteSpace(_outlineColor.Text) ? "#000000" : _outlineColor.Text.Trim();
        layer.OutlineWidth = Math.Clamp(ParseDouble(_outlineWidth.Text, layer.OutlineWidth), 0.0, 12.0);
        layer.ShadowDepth = Math.Clamp(ParseDouble(_shadowDepth.Text, layer.ShadowDepth), 0.0, 12.0);
        layer.StaticText = _staticText.Text;
        layer.StripEmoji = _stripEmoji.Checked;
        layer.Uppercase = _uppercase.Checked;

        RefreshLayerListLabels();
        _canvas.InvalidateKeyCache();
        _canvas.Invalidate();
        NotifyPresetModified();
    }

    public bool Undo()
    {
        var restored = _history.Undo();
        if (restored is null)
        {
            _historyStatus.Text = "Không còn bước để hoàn tác (Ctrl+Z).";
            return false;
        }

        ApplyRestoredPreset(restored, "↶ Đã hoàn tác (Ctrl+Z)");
        return true;
    }

    public bool Redo()
    {
        var restored = _history.Redo();
        if (restored is null)
        {
            _historyStatus.Text = "Không còn bước để làm lại (Ctrl+Shift+Z).";
            return false;
        }

        ApplyRestoredPreset(restored, "↷ Đã làm lại (Ctrl+Shift+Z)");
        return true;
    }

    public string CommitAndSaveCurrentPreset()
    {
        SaveCurrentPresetName();
        _historyStatus.Text = $"✓ Đã lưu mẫu '{_currentPreset.Name}' ({DateTime.Now:HH:mm:ss})";
        return _currentPreset.Name;
    }

    private void ApplyRestoredPreset(FrameTemplatePreset restored, string message)
    {
        var selectedId = _selectedLayer?.Id;
        var idx = _presets.IndexOf(_currentPreset);
        if (idx >= 0)
        {
            _presets[idx] = restored;
        }

        SelectPreset(restored, resetHistory: false);
        if (!string.IsNullOrEmpty(selectedId))
        {
            var match = _currentPreset.Layers.FirstOrDefault(l => l.Id == selectedId);
            if (match is not null)
            {
                _selectedLayer = match;
                var layerIdx = _currentPreset.Layers.IndexOf(match);
                _suppressEvents = true;
                try
                {
                    if (layerIdx >= 0 && layerIdx < _layerList.Items.Count)
                    {
                        _layerList.SelectedIndex = layerIdx;
                    }
                }
                finally
                {
                    _suppressEvents = false;
                }

                LoadLayerToControls(match);
                _canvas.SelectedLayer = match;
            }
        }

        _canvas.InvalidateKeyCache();
        _canvas.Invalidate();
        RefreshPresetCombo();
        UpdateUndoRedoButtons();
        _historyStatus.Text = message;
        PresetsChanged?.Invoke();
    }

    private void NotifyPresetModified(string? statusMessage = null)
    {
        if (_history.RecordChange(_currentPreset) && !string.IsNullOrEmpty(statusMessage))
        {
            _historyStatus.Text = statusMessage;
        }
        else if (!string.IsNullOrEmpty(statusMessage))
        {
            _historyStatus.Text = statusMessage;
        }

        UpdateUndoRedoButtons();
        PresetsChanged?.Invoke();
    }

    private void UpdateUndoRedoButtons()
    {
        if (_undoBtn is not null)
        {
            _undoBtn.Enabled = _history.CanUndo;
        }

        if (_redoBtn is not null)
        {
            _redoBtn.Enabled = _history.CanRedo;
        }
    }

    private void ToggleEyedropper()
    {
        var active = !_canvas.EyedropperMode;
        UpdateEyedropperButtonState(active);
    }

    private void UpdateEyedropperButtonState(bool active)
    {
        _canvas.EyedropperMode = active;
        _eyedropperBtn.BackColor = active ? Color.FromArgb(215, 130, 0) : Color.FromArgb(0, 120, 215);
        _eyedropperBtn.Text = active ? "🎯 Click vào nền ảnh trên Canvas..." : "🎯 Chấm màu trên ảnh";
    }

    private void AutoPickCornerKeyColor()
    {
        if (_selectedLayer is null)
        {
            return;
        }

        var hex = _canvas.TrySampleCornerHex(_selectedLayer);
        if (hex is null)
        {
            MessageBox.Show("Hãy chọn Folder nguồn hoặc File cố định có ảnh (.png/.jpg/.webp) cho layer này trước để hút màu nền.");
            return;
        }

        _selectedLayer.KeyMode = FrameKeyMode.CustomColorKey;
        _selectedLayer.KeyColor = hex;
        LoadLayerToControls(_selectedLayer);
        _canvas.InvalidateKeyCache();
        _canvas.Invalidate();
        NotifyPresetModified($"Đã tự hút màu góc ảnh {hex}");
    }

    private void PickKeyColorWithDialog()
    {
        if (_selectedLayer is null)
        {
            return;
        }

        using var dlg = new ColorDialog
        {
            Color = ParseHexColor(_selectedLayer.KeyColor),
            FullOpen = true
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            _selectedLayer.KeyMode = FrameKeyMode.CustomColorKey;
            _selectedLayer.KeyColor = $"#{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}";
            LoadLayerToControls(_selectedLayer);
            _canvas.InvalidateKeyCache();
            _canvas.Invalidate();
            NotifyPresetModified($"Đã chọn màu tách {_selectedLayer.KeyColor}");
        }
    }

    private static Color ParseHexColor(string? hex)
    {
        var clean = (hex ?? "").Trim().TrimStart('#');
        if (clean.Length == 6
            && int.TryParse(clean[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r)
            && int.TryParse(clean.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g)
            && int.TryParse(clean.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
        {
            return Color.FromArgb(r, g, b);
        }

        return Color.FromArgb(0, 255, 0);
    }

    private void ApplyPresetHeaderFromControls()
    {
        if (_suppressEvents)
        {
            return;
        }

        _currentPreset.AudioOnlyDriver = _audioOnly.Checked;
        _currentPreset.MirrorSide = _mirrorMode.SelectedIndex switch
        {
            1 => FrameMirrorMode.Right,
            2 => FrameMirrorMode.Left,
            3 => FrameMirrorMode.Alternate,
            _ => FrameMirrorMode.None
        };
        _currentPreset.AudioSpeed = Math.Clamp(ParseDouble(_audioSpeed.Text, _currentPreset.AudioSpeed), 0.5, 2.0);
        _currentPreset.AudioPitchSemitones = Math.Clamp(ParseDouble(_audioPitch.Text, _currentPreset.AudioPitchSemitones), -12.0, 12.0);
        _currentPreset.AudioVolume = Math.Clamp(ParseDouble(_audioVolume.Text, _currentPreset.AudioVolume), 0.1, 4.0);
        _canvas.Invalidate();
        NotifyPresetModified();
    }

    private void SaveCurrentPresetName()
    {
        WriteControlsToLayer();
        ApplyPresetHeaderFromControls();
        var newName = _presetName.Text.Trim();
        if (!string.IsNullOrWhiteSpace(newName))
        {
            _currentPreset.Name = newName;
        }

        RefreshPresetCombo();
        NotifyPresetModified($"✓ Đã lưu mẫu '{_currentPreset.Name}'");
    }

    private void DuplicatePreset()
    {
        var copy = _currentPreset.Clone();
        copy.Name = UniquePresetName(copy.Name + " (Bản sao)");
        _presets.Add(copy);
        SelectPreset(copy);
        RefreshPresetCombo();
        PresetsChanged?.Invoke();
    }

    private void AddBuiltInPreset(bool story)
    {
        var preset = story
            ? FrameTemplatePreset.CreateStoryPreset()
            : FrameTemplatePreset.CreateDefault();
        preset.Name = UniquePresetName(preset.Name);
        _presets.Add(preset);
        SelectPreset(preset);
        RefreshPresetCombo();
        PresetsChanged?.Invoke();
    }

    private void DeleteCurrentPreset()
    {
        if (_presets.Count <= 1)
        {
            MessageBox.Show("Cần giữ ít nhất 1 mẫu chủ đề.");
            return;
        }

        _presets.Remove(_currentPreset);
        SelectPreset(_presets[0]);
        RefreshPresetCombo();
        PresetsChanged?.Invoke();
    }

    private string UniquePresetName(string baseName)
    {
        var candidate = baseName;
        var n = 2;
        while (_presets.Any(p => string.Equals(p.Name, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{baseName} {n++}";
        }

        return candidate;
    }

    private void AddLayer()
    {
        var kind = _addKindCombo.SelectedIndex switch
        {
            0 => FrameLayerKind.BackgroundVideo,
            1 => FrameLayerKind.MainVideo,
            2 => FrameLayerKind.FrameOverlay,
            3 => FrameLayerKind.ImageOverlay,
            4 => FrameLayerKind.LoopVideo,
            5 => FrameLayerKind.Subtitle,
            6 => FrameLayerKind.StaticText,
            _ => FrameLayerKind.ImageOverlay
        };

        var layer = new FrameTemplateLayer
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Kind = kind,
            Name = KindLabel(kind),
            FullCanvas = kind is FrameLayerKind.BackgroundVideo or FrameLayerKind.FrameOverlay,
            UseReferenceHole = kind == FrameLayerKind.MainVideo,
            Pick = kind == FrameLayerKind.Subtitle ? FrameLayerPick.MatchByNumber : FrameLayerPick.RoundRobin,
            KeyMode = kind == FrameLayerKind.LoopVideo ? FrameKeyMode.BlackKey : FrameKeyMode.None,
            X = kind == FrameLayerKind.Subtitle ? 420 : 120,
            Y = kind == FrameLayerKind.Subtitle ? 540 : 100,
            Width = kind == FrameLayerKind.Subtitle ? 520 : 420,
            Height = kind == FrameLayerKind.Subtitle ? 180 : 320
        };

        _currentPreset.Layers.Add(layer);
        _selectedLayer = layer;
        RefreshLayerListLabels();
        LoadLayerToControls(layer);
        _canvas.SelectedLayer = layer;
        _canvas.Invalidate();
        NotifyPresetModified($"Đã thêm layer '{layer.Name}'");
    }

    private void MoveLayer(int delta)
    {
        if (_selectedLayer is null)
        {
            return;
        }

        var idx = _currentPreset.Layers.IndexOf(_selectedLayer);
        var target = idx + delta;
        if (idx < 0 || target < 0 || target >= _currentPreset.Layers.Count)
        {
            return;
        }

        (_currentPreset.Layers[idx], _currentPreset.Layers[target]) = (_currentPreset.Layers[target], _currentPreset.Layers[idx]);
        RefreshLayerListLabels();
        _canvas.Invalidate();
        NotifyPresetModified("Đã đổi thứ tự layer");
    }

    private void CloneSelectedLayer()
    {
        if (_selectedLayer is null)
        {
            return;
        }

        var clone = _selectedLayer.Clone();
        clone.Id = Guid.NewGuid().ToString("N")[..8];
        clone.Name += " (2)";
        clone.X += 24;
        clone.Y += 24;
        _currentPreset.Layers.Add(clone);
        _selectedLayer = clone;
        RefreshLayerListLabels();
        LoadLayerToControls(clone);
        _canvas.SelectedLayer = clone;
        _canvas.Invalidate();
        NotifyPresetModified($"Đã nhân bản '{clone.Name}'");
    }

    private void RemoveSelectedLayer()
    {
        if (_selectedLayer is null || _currentPreset.Layers.Count <= 1)
        {
            return;
        }

        var removedName = _selectedLayer.Name;
        _currentPreset.Layers.Remove(_selectedLayer);
        _selectedLayer = _currentPreset.Layers.FirstOrDefault();
        RefreshLayerListLabels();
        if (_selectedLayer is not null)
        {
            LoadLayerToControls(_selectedLayer);
        }

        _canvas.SelectedLayer = _selectedLayer;
        _canvas.Invalidate();
        NotifyPresetModified($"Đã xóa layer '{removedName}'");
    }

    private static string KindLabel(FrameLayerKind kind) => kind switch
    {
        FrameLayerKind.BackgroundVideo => "Video nền",
        FrameLayerKind.MainVideo => "Video chính",
        FrameLayerKind.FrameOverlay => "Khung PNG",
        FrameLayerKind.ImageOverlay => "Ảnh / Avatar",
        FrameLayerKind.LoopVideo => "Wave / Effect",
        FrameLayerKind.Subtitle => "Phụ đề SRT",
        FrameLayerKind.StaticText => "Chữ cố định",
        _ => "Layer"
    };

    private static string ShortPick(FrameLayerPick pick) => pick switch
    {
        FrameLayerPick.Random => "Random",
        FrameLayerPick.Fixed => "Cố định",
        FrameLayerPick.MatchByNumber => "Theo số 001",
        _ => "Xoay vòng"
    };

    private static int ParseInt(string text, int fallback) =>
        int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    private static double ParseDouble(string text, double fallback) =>
        double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    private static Label SectionHeader(string title) => new()
    {
        Text = title,
        AutoSize = true,
        ForeColor = Color.FromArgb(130, 200, 255),
        Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
        Padding = new Padding(0, 8, 0, 2)
    };

    private static Label MakeLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = Color.White,
        Padding = new Padding(4, 5, 2, 0)
    };

    private static FlowLayoutPanel Line(string label, params Control[] controls)
    {
        var row = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = true,
            BackColor = Color.FromArgb(35, 35, 38),
            Margin = new Padding(0, 1, 0, 1)
        };
        if (!string.IsNullOrEmpty(label))
        {
            row.Controls.Add(MakeLabel(label));
        }

        foreach (var ctrl in controls)
        {
            row.Controls.Add(ctrl);
        }

        return row;
    }

    private FlowLayoutPanel FolderLine(string label, TextBox box)
    {
        var browse = MakeButton("Chọn...", (_, _) =>
        {
            using var dialog = new FolderBrowserDialog();
            if (Directory.Exists(box.Text))
            {
                dialog.SelectedPath = box.Text;
            }

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                box.Text = dialog.SelectedPath;
                WriteControlsToLayer();
            }
        });
        return Line(label, box, browse);
    }

    private FlowLayoutPanel FileLine(string label, TextBox box)
    {
        var browse = MakeButton("Chọn file...", (_, _) =>
        {
            using var dialog = new OpenFileDialog();
            if (File.Exists(box.Text))
            {
                dialog.FileName = box.Text;
            }

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                box.Text = dialog.FileName;
                WriteControlsToLayer();
            }
        });
        return Line(label, box, browse);
    }

    private static void StyleBox(TextBox box)
    {
        box.BackColor = Color.FromArgb(48, 48, 52);
        box.ForeColor = Color.White;
        box.BorderStyle = BorderStyle.FixedSingle;
    }

    private static void StyleCombo(ComboBox combo)
    {
        combo.BackColor = Color.FromArgb(48, 48, 52);
        combo.ForeColor = Color.White;
        combo.FlatStyle = FlatStyle.Flat;
    }

    private static void StyleCheck(CheckBox check)
    {
        check.ForeColor = Color.White;
        check.Padding = new Padding(4, 3, 4, 0);
    }

    private static Button MakeButton(string text, EventHandler click, Color? back = null)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            BackColor = back ?? Color.FromArgb(55, 55, 60),
            ForeColor = Color.White,
            Padding = new Padding(4, 1, 4, 1),
            Margin = new Padding(3, 2, 3, 2)
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(85, 85, 90);
        button.Click += click;
        return button;
    }

    private sealed class CanvasPreviewBox : Control
    {
        private enum DragHandle
        {
            None,
            Move,
            NW,
            N,
            NE,
            E,
            SE,
            S,
            SW,
            W
        }

        private DragHandle _activeHandle = DragHandle.None;
        private Point _dragStart;
        private (int Left, int Top, int Width, int Height) _startRect;
        private readonly Dictionary<string, (string Path, Bitmap Raw)> _rawImageCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, (string KeySig, Bitmap Processed)> _keyedImageCache = new(StringComparer.Ordinal);

        public FrameTemplatePreset? Preset { get; set; }

        public FrameTemplateLayer? SelectedLayer { get; set; }

        public bool EyedropperMode { get; set; }

        public event Action<FrameTemplateLayer>? LayerMovedOrResized;

        public event Action<FrameTemplateLayer, string>? ColorPickedFromCanvas;

        public CanvasPreviewBox()
        {
            DoubleBuffered = true;
            BackColor = Color.FromArgb(18, 18, 20);
            Cursor = Cursors.Default;
        }

        public void InvalidateKeyCache()
        {
            foreach (var item in _keyedImageCache.Values)
            {
                item.Processed.Dispose();
            }

            _keyedImageCache.Clear();
        }

        public string? TrySampleCornerHex(FrameTemplateLayer layer)
        {
            var raw = GetOrLoadRawBitmap(layer);
            if (raw is null || raw.Width < 2 || raw.Height < 2)
            {
                return null;
            }

            var c = raw.GetPixel(Math.Min(4, raw.Width - 1), Math.Min(4, raw.Height - 1));
            return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            var viewport = GetViewport();
            DrawCheckerboard(g, viewport);
            using var borderPen = new Pen(Color.FromArgb(90, 90, 95), 1.5f);
            g.DrawRectangle(borderPen, viewport);

            if (Preset is null)
            {
                return;
            }

            var settings = new FrameSettings();
            var mirroredLeft = Preset.MirrorSide == FrameMirrorMode.Left;
            foreach (var layer in Preset.Layers.Where(l => l.Enabled))
            {
                if (Preset.AudioOnlyDriver && layer.Kind == FrameLayerKind.MainVideo)
                {
                    continue;
                }

                var rect = ToScreenRect(layer, settings, mirroredLeft, viewport);
                var color = LayerColor(layer.Kind);
                var previewBmp = GetOrBuildPreviewBitmap(layer);
                if (previewBmp is not null)
                {
                    g.DrawImage(previewBmp, rect);
                }
                else
                {
                    var alpha = (int)Math.Round(Math.Clamp(layer.Opacity, 0.15, 1.0) * 110);
                    using var fill = new SolidBrush(Color.FromArgb(alpha, color));
                    g.FillRectangle(fill, rect);
                }

                var isSelected = ReferenceEquals(layer, SelectedLayer);
                using var pen = new Pen(isSelected ? Color.FromArgb(255, 215, 0) : color, isSelected ? 2.2f : 1.2f);
                if (layer.Kind == FrameLayerKind.Subtitle)
                {
                    pen.DashStyle = DashStyle.Dash;
                }

                g.DrawRectangle(pen, rect);

                var tag = $"{layer.Name} ({layer.Kind})";
                if (layer.KeyMode != FrameKeyMode.None)
                {
                    tag += $" · Key:{layer.KeyColor}";
                }

                if (layer.Kind is FrameLayerKind.Subtitle or FrameLayerKind.StaticText)
                {
                    var sample = layer.Kind == FrameLayerKind.StaticText && !string.IsNullOrWhiteSpace(layer.StaticText)
                        ? layer.StaticText
                        : "Phụ đề mẫu (SRT)...";
                    tag += "\n" + sample;
                }

                using var labelBg = new SolidBrush(Color.FromArgb(150, 15, 15, 18));
                var textSize = g.MeasureString(tag, Font, Math.Max(60, rect.Width - 10));
                g.FillRectangle(labelBg, rect.X + 4, rect.Y + 4, Math.Min(rect.Width - 8, textSize.Width + 6), Math.Min(rect.Height - 8, textSize.Height + 4));
                using var textBrush = new SolidBrush(Color.White);
                var textRect = new RectangleF(rect.X + 6, rect.Y + 5, Math.Max(40, rect.Width - 12), Math.Max(20, rect.Height - 8));
                g.DrawString(tag, Font, textBrush, textRect);
            }

            // Always draw 8 resize handles on the selected layer so the user can resize any layer freely
            if (SelectedLayer is { Enabled: true } sel)
            {
                var selRect = ToScreenRect(sel, settings, mirroredLeft, viewport);
                foreach (var (_, handleRect) in GetHandles(selRect))
                {
                    using var handleBrush = new SolidBrush(Color.FromArgb(255, 215, 0));
                    using var handleOutline = new Pen(Color.Black, 1f);
                    g.FillRectangle(handleBrush, handleRect);
                    g.DrawRectangle(handleOutline, handleRect);
                }
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (Preset is null || e.Button != MouseButtons.Left)
            {
                return;
            }

            var viewport = GetViewport();
            var settings = new FrameSettings();
            var mirroredLeft = Preset.MirrorSide == FrameMirrorMode.Left;

            if (EyedropperMode)
            {
                if (TryPickColorAtPoint(e.Location, viewport, settings, mirroredLeft, out var pickedLayer, out var hex))
                {
                    EyedropperMode = false;
                    Cursor = Cursors.Default;
                    ColorPickedFromCanvas?.Invoke(pickedLayer, hex);
                }

                return;
            }

            // 1. Check 8 resize handles of the currently selected layer first
            if (SelectedLayer is { Enabled: true } currentSel)
            {
                var currentScreenRect = ToScreenRect(currentSel, settings, mirroredLeft, viewport);
                var hitHandle = HitTestHandle(currentScreenRect, e.Location);
                if (hitHandle != DragHandle.None)
                {
                    BeginDragOrResize(currentSel, hitHandle, e.Location, settings, mirroredLeft);
                    return;
                }
            }

            // 2. Hit test from top layer to bottom layer
            for (var i = Preset.Layers.Count - 1; i >= 0; i--)
            {
                var layer = Preset.Layers[i];
                if (!layer.Enabled || (Preset.AudioOnlyDriver && layer.Kind == FrameLayerKind.MainVideo))
                {
                    continue;
                }

                var rect = ToScreenRect(layer, settings, mirroredLeft, viewport);
                var hitHandle = HitTestHandle(rect, e.Location);
                if (hitHandle != DragHandle.None)
                {
                    SelectedLayer = layer;
                    BeginDragOrResize(layer, hitHandle, e.Location, settings, mirroredLeft);
                    return;
                }

                if (rect.Contains(e.Location))
                {
                    SelectedLayer = layer;
                    BeginDragOrResize(layer, DragHandle.Move, e.Location, settings, mirroredLeft);
                    return;
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var viewport = GetViewport();
            if (viewport.Width <= 10 || viewport.Height <= 10 || Preset is null)
            {
                return;
            }

            if (EyedropperMode)
            {
                Cursor = Cursors.Cross;
                return;
            }

            var settings = new FrameSettings();
            var mirroredLeft = Preset.MirrorSide == FrameMirrorMode.Left;

            if (_activeHandle == DragHandle.None || SelectedLayer is null)
            {
                if (SelectedLayer is { Enabled: true } sel)
                {
                    var selRect = ToScreenRect(sel, settings, mirroredLeft, viewport);
                    var hover = HitTestHandle(selRect, e.Location);
                    Cursor = CursorForHandle(hover != DragHandle.None ? hover : selRect.Contains(e.Location) ? DragHandle.Move : DragHandle.None);
                }
                else
                {
                    Cursor = Cursors.Default;
                }

                return;
            }

            var dx = (int)Math.Round((e.X - _dragStart.X) * (FrameDesign.Width / (double)viewport.Width));
            var dy = (int)Math.Round((e.Y - _dragStart.Y) * (FrameDesign.Height / (double)viewport.Height));
            if (dx == 0 && dy == 0)
            {
                return;
            }

            // Unlock FullCanvas / UseReferenceHole automatically when the user drags or resizes on Canvas
            if (SelectedLayer.FullCanvas || SelectedLayer.UseReferenceHole)
            {
                SelectedLayer.FullCanvas = false;
                SelectedLayer.UseReferenceHole = false;
            }

            var left = _startRect.Left;
            var top = _startRect.Top;
            var right = _startRect.Left + _startRect.Width;
            var bottom = _startRect.Top + _startRect.Height;
            const int minSize = 32;

            switch (_activeHandle)
            {
                case DragHandle.Move:
                    left += dx;
                    top += dy;
                    right += dx;
                    bottom += dy;
                    break;
                case DragHandle.NW:
                    left = Math.Min(right - minSize, left + dx);
                    top = Math.Min(bottom - minSize, top + dy);
                    break;
                case DragHandle.N:
                    top = Math.Min(bottom - minSize, top + dy);
                    break;
                case DragHandle.NE:
                    right = Math.Max(left + minSize, right + dx);
                    top = Math.Min(bottom - minSize, top + dy);
                    break;
                case DragHandle.E:
                    right = Math.Max(left + minSize, right + dx);
                    break;
                case DragHandle.SE:
                    right = Math.Max(left + minSize, right + dx);
                    bottom = Math.Max(top + minSize, bottom + dy);
                    break;
                case DragHandle.S:
                    bottom = Math.Max(top + minSize, bottom + dy);
                    break;
                case DragHandle.SW:
                    left = Math.Min(right - minSize, left + dx);
                    bottom = Math.Max(top + minSize, bottom + dy);
                    break;
                case DragHandle.W:
                    left = Math.Min(right - minSize, left + dx);
                    break;
            }

            var newW = Math.Clamp(right - left, minSize, FrameDesign.Width * 2);
            var newH = Math.Clamp(bottom - top, minSize, FrameDesign.Height * 2);
            if (SelectedLayer.Kind is FrameLayerKind.Subtitle or FrameLayerKind.StaticText)
            {
                SelectedLayer.Width = newW;
                SelectedLayer.Height = newH;
                SelectedLayer.X = left + newW / 2;
                SelectedLayer.Y = top + newH / 2;
            }
            else
            {
                SelectedLayer.X = left;
                SelectedLayer.Y = top;
                SelectedLayer.Width = newW;
                SelectedLayer.Height = newH;
            }

            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_activeHandle != DragHandle.None && SelectedLayer is not null)
            {
                _activeHandle = DragHandle.None;
                LayerMovedOrResized?.Invoke(SelectedLayer);
            }
        }

        private void BeginDragOrResize(
            FrameTemplateLayer layer,
            DragHandle handle,
            Point mouseLocation,
            FrameSettings settings,
            bool mirroredLeft)
        {
            _activeHandle = handle;
            _dragStart = mouseLocation;
            _startRect = ResolveCanvasLogicalRect(layer, settings, mirroredLeft);
            Invalidate();
        }

        private static (int Left, int Top, int Width, int Height) ResolveCanvasLogicalRect(
            FrameTemplateLayer layer,
            FrameSettings settings,
            bool mirroredLeft)
        {
            if (layer.Kind is FrameLayerKind.Subtitle or FrameLayerKind.StaticText)
            {
                var w = Math.Max(80, layer.Width);
                var h = Math.Max(50, layer.Height);
                return (layer.X - w / 2, layer.Y - h / 2, w, h);
            }

            var box = FrameTemplateRenderer.ResolveLayerBox(layer, settings, mirroredLeft);
            return (box.X, box.Y, box.W, box.H);
        }

        private static IEnumerable<(DragHandle Handle, Rectangle Rect)> GetHandles(Rectangle r)
        {
            const int s = 10;
            var hs = s / 2;
            var mx = r.X + r.Width / 2;
            var my = r.Y + r.Height / 2;
            yield return (DragHandle.NW, new Rectangle(r.Left - hs, r.Top - hs, s, s));
            yield return (DragHandle.N, new Rectangle(mx - hs, r.Top - hs, s, s));
            yield return (DragHandle.NE, new Rectangle(r.Right - hs, r.Top - hs, s, s));
            yield return (DragHandle.E, new Rectangle(r.Right - hs, my - hs, s, s));
            yield return (DragHandle.SE, new Rectangle(r.Right - hs, r.Bottom - hs, s, s));
            yield return (DragHandle.S, new Rectangle(mx - hs, r.Bottom - hs, s, s));
            yield return (DragHandle.SW, new Rectangle(r.Left - hs, r.Bottom - hs, s, s));
            yield return (DragHandle.W, new Rectangle(r.Left - hs, my - hs, s, s));
        }

        private static DragHandle HitTestHandle(Rectangle rect, Point pt)
        {
            foreach (var (handle, hRect) in GetHandles(rect))
            {
                var inflated = Rectangle.Inflate(hRect, 4, 4);
                if (inflated.Contains(pt))
                {
                    return handle;
                }
            }

            return DragHandle.None;
        }

        private static Cursor CursorForHandle(DragHandle handle) => handle switch
        {
            DragHandle.NW or DragHandle.SE => Cursors.SizeNWSE,
            DragHandle.NE or DragHandle.SW => Cursors.SizeNESW,
            DragHandle.N or DragHandle.S => Cursors.SizeNS,
            DragHandle.E or DragHandle.W => Cursors.SizeWE,
            DragHandle.Move => Cursors.SizeAll,
            _ => Cursors.Default
        };

        private bool TryPickColorAtPoint(
            Point pt,
            Rectangle viewport,
            FrameSettings settings,
            bool mirroredLeft,
            out FrameTemplateLayer pickedLayer,
            out string hexColor)
        {
            pickedLayer = null!;
            hexColor = "#00FF00";
            if (Preset is null)
            {
                return false;
            }

            for (var i = Preset.Layers.Count - 1; i >= 0; i--)
            {
                var layer = Preset.Layers[i];
                if (!layer.Enabled)
                {
                    continue;
                }

                var rect = ToScreenRect(layer, settings, mirroredLeft, viewport);
                if (!rect.Contains(pt))
                {
                    continue;
                }

                var raw = GetOrLoadRawBitmap(layer);
                if (raw is null || rect.Width <= 2 || rect.Height <= 2)
                {
                    continue;
                }

                var u = Math.Clamp((pt.X - rect.X) / (double)rect.Width, 0.0, 0.999);
                var v = Math.Clamp((pt.Y - rect.Y) / (double)rect.Height, 0.0, 0.999);
                var px = (int)(u * raw.Width);
                var py = (int)(v * raw.Height);
                var color = raw.GetPixel(px, py);
                pickedLayer = layer;
                hexColor = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
                return true;
            }

            return false;
        }

        private Bitmap? GetOrLoadRawBitmap(FrameTemplateLayer layer)
        {
            var path = ResolveSampleImagePath(layer);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            if (_rawImageCache.TryGetValue(layer.Id, out var cached) && string.Equals(cached.Path, path, StringComparison.OrdinalIgnoreCase))
            {
                return cached.Raw;
            }

            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var loaded = Image.FromStream(stream);
                var maxDim = 480;
                var scale = Math.Min(1.0, maxDim / (double)Math.Max(loaded.Width, loaded.Height));
                var w = Math.Max(2, (int)Math.Round(loaded.Width * scale));
                var h = Math.Max(2, (int)Math.Round(loaded.Height * scale));
                var bmp = new Bitmap(w, h);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(loaded, 0, 0, w, h);
                }

                if (_rawImageCache.TryGetValue(layer.Id, out var old))
                {
                    old.Raw.Dispose();
                }

                _rawImageCache[layer.Id] = (path, bmp);
                return bmp;
            }
            catch
            {
                return null;
            }
        }

        private Bitmap? GetOrBuildPreviewBitmap(FrameTemplateLayer layer)
        {
            var raw = GetOrLoadRawBitmap(layer);
            if (raw is null)
            {
                return null;
            }

            var sig = $"{layer.KeyMode}|{layer.KeyColor}|{layer.KeyTolerance:0.###}|{layer.KeySoftness:0.###}|{layer.Opacity:0.##}|{layer.FlipHorizontal}|{layer.FlipVertical}";
            if (_keyedImageCache.TryGetValue(layer.Id, out var cached) && cached.KeySig == sig)
            {
                return cached.Processed;
            }

            var result = new Bitmap(raw.Width, raw.Height);
            var keyRgb = ParseHexColor(layer.KeyColor);
            var tol = Math.Clamp(layer.KeyTolerance, 0.01, 0.85) * 441.67; // Max Euclidean RGB distance is sqrt(3*255^2) = 441.67
            var soft = Math.Clamp(layer.KeySoftness, 0.0, 0.85) * 441.67;
            var opacityFactor = Math.Clamp(layer.Opacity, 0.0, 1.0);

            for (var y = 0; y < raw.Height; y++)
            {
                var sy = layer.FlipVertical ? raw.Height - 1 - y : y;
                for (var x = 0; x < raw.Width; x++)
                {
                    var sx = layer.FlipHorizontal ? raw.Width - 1 - x : x;
                    var c = raw.GetPixel(sx, sy);
                    double alpha = c.A;

                    if (layer.KeyMode != FrameKeyMode.None && alpha > 0)
                    {
                        var target = layer.KeyMode switch
                        {
                            FrameKeyMode.BlackKey => Color.Black,
                            FrameKeyMode.GreenKey => Color.FromArgb(0, 255, 0),
                            _ => keyRgb
                        };
                        var dr = c.R - target.R;
                        var dg = c.G - target.G;
                        var db = c.B - target.B;
                        var dist = Math.Sqrt(dr * dr + dg * dg + db * db);
                        if (dist <= tol)
                        {
                            alpha = 0;
                        }
                        else if (soft > 1 && dist < tol + soft)
                        {
                            alpha *= (dist - tol) / soft;
                        }
                    }

                    var finalA = (int)Math.Clamp(alpha * opacityFactor, 0, 255);
                    result.SetPixel(x, y, Color.FromArgb(finalA, c.R, c.G, c.B));
                }
            }

            if (_keyedImageCache.TryGetValue(layer.Id, out var prev))
            {
                prev.Processed.Dispose();
            }

            _keyedImageCache[layer.Id] = (sig, result);
            return result;
        }

        private static string? ResolveSampleImagePath(FrameTemplateLayer layer)
        {
            string[] imgExts = [".png", ".jpg", ".jpeg", ".bmp"];
            if (!string.IsNullOrWhiteSpace(layer.FixedFile)
                && File.Exists(layer.FixedFile)
                && imgExts.Contains(Path.GetExtension(layer.FixedFile), StringComparer.OrdinalIgnoreCase))
            {
                return layer.FixedFile;
            }

            if (!string.IsNullOrWhiteSpace(layer.Folder) && Directory.Exists(layer.Folder))
            {
                return Directory.EnumerateFiles(layer.Folder)
                    .FirstOrDefault(f => imgExts.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase));
            }

            return null;
        }

        private static void DrawCheckerboard(Graphics g, Rectangle viewport)
        {
            using var dark = new SolidBrush(Color.FromArgb(16, 16, 18));
            g.FillRectangle(dark, viewport);
        }

        private Rectangle GetViewport()
        {
            var pad = 12;
            var availW = Math.Max(64, Width - pad * 2);
            var availH = Math.Max(36, Height - pad * 2);
            var scale = Math.Min(availW / (double)FrameDesign.Width, availH / (double)FrameDesign.Height);
            var w = (int)Math.Round(FrameDesign.Width * scale);
            var h = (int)Math.Round(FrameDesign.Height * scale);
            var x = (Width - w) / 2;
            var y = (Height - h) / 2;
            return new Rectangle(x, y, w, h);
        }

        private static Rectangle ToScreenRect(
            FrameTemplateLayer layer,
            FrameSettings settings,
            bool mirroredLeft,
            Rectangle viewport)
        {
            var scaleX = viewport.Width / (double)FrameDesign.Width;
            var scaleY = viewport.Height / (double)FrameDesign.Height;
            var logical = ResolveCanvasLogicalRect(layer, settings, mirroredLeft);
            return new Rectangle(
                viewport.X + (int)Math.Round(logical.Left * scaleX),
                viewport.Y + (int)Math.Round(logical.Top * scaleY),
                Math.Max(16, (int)Math.Round(logical.Width * scaleX)),
                Math.Max(16, (int)Math.Round(logical.Height * scaleY)));
        }

        private static Color LayerColor(FrameLayerKind kind) => kind switch
        {
            FrameLayerKind.BackgroundVideo => Color.FromArgb(45, 110, 170),
            FrameLayerKind.MainVideo => Color.FromArgb(40, 165, 95),
            FrameLayerKind.FrameOverlay => Color.FromArgb(140, 95, 190),
            FrameLayerKind.ImageOverlay => Color.FromArgb(215, 125, 45),
            FrameLayerKind.LoopVideo => Color.FromArgb(0, 175, 185),
            FrameLayerKind.Subtitle => Color.FromArgb(220, 185, 40),
            FrameLayerKind.StaticText => Color.FromArgb(210, 80, 120),
            _ => Color.SteelBlue
        };
    }
}
