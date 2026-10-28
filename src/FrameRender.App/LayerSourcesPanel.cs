using VideoAutoTool.Core.FrameBatch;

namespace FrameRender;

/// <summary>
/// Queue-tab panel: one row per template layer where the user picks the source
/// (own folder, auto-detect in a shared folder, or a pasted file/folder path).
/// Values are stored on the preset's layers, so they are saved with the template.
/// </summary>
public sealed class LayerSourcesPanel : UserControl
{
    private static readonly (LayerSourceMode Mode, string Text)[] Modes =
    [
        (LayerSourceMode.FromTemplate, "Theo mẫu (đã thiết kế)"),
        (LayerSourceMode.Folder, "Folder riêng"),
        (LayerSourceMode.AutoDetect, "Tự dò trong folder chung"),
        (LayerSourceMode.Path, "Đường dẫn (file / folder)")
    ];

    private readonly TextBox _autoFolder = new();
    private readonly TableLayoutPanel _grid = new();
    private FrameTemplatePreset? _preset;
    private bool _loading;

    public LayerSourcesPanel()
    {
        BackColor = Color.FromArgb(30, 30, 30);
        AutoScroll = true;
        Dock = DockStyle.Fill;

        _autoFolder.BackColor = Color.FromArgb(45, 45, 48);
        _autoFolder.ForeColor = Color.White;
        _autoFolder.BorderStyle = BorderStyle.FixedSingle;
        _autoFolder.Dock = DockStyle.Fill;
        _autoFolder.TextChanged += (_, _) =>
        {
            if (_loading || _preset is null)
            {
                return;
            }

            _preset.AutoSourceFolder = _autoFolder.Text.Trim();
            Changed?.Invoke();
        };

        _grid.Dock = DockStyle.Top;
        _grid.AutoSize = true;
        _grid.ColumnCount = 4;
        _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        Controls.Add(_grid);
    }

    /// <summary>Raised when any binding changed, so the owner can persist the session.</summary>
    public event Action? Changed;

    /// <summary>Folder shared by every layer set to auto-detect.</summary>
    public string AutoFolder => _autoFolder.Text.Trim();

    public void Bind(FrameTemplatePreset preset)
    {
        _preset = preset;
        _loading = true;
        try
        {
            _grid.SuspendLayout();
            _grid.Controls.Clear();
            _grid.RowStyles.Clear();
            _grid.RowCount = 0;
            _autoFolder.Text = preset.AutoSourceFolder;

            AddRow(
                new Label { Text = "Folder chung (Tự dò)", AutoSize = true, ForeColor = Color.White, Padding = new Padding(0, 6, 0, 0) },
                new Label { Text = "chứa video chính + các layer", AutoSize = true, ForeColor = Color.Silver, Padding = new Padding(0, 6, 0, 0) },
                _autoFolder,
                BrowseButton(_autoFolder));

            foreach (var layer in preset.Layers.Where(layer => layer.Enabled
                         && layer.Kind is not (FrameLayerKind.MainVideo or FrameLayerKind.StaticText)))
            {
                AddLayerRow(layer);
            }
        }
        finally
        {
            _grid.ResumeLayout();
            _loading = false;
        }
    }

    private void AddLayerRow(FrameTemplateLayer layer)
    {
        var box = new TextBox
        {
            Text = layer.QueueSourcePath,
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(45, 45, 48),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        var mode = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(45, 45, 48),
            ForeColor = Color.White
        };
        foreach (var item in Modes)
        {
            mode.Items.Add(item.Text);
        }

        mode.SelectedIndex = Math.Max(0, Array.FindIndex(Modes, item => item.Mode == layer.QueueSourceMode));
        var browse = BrowseButton(box, () => Modes[mode.SelectedIndex].Mode);

        void Refresh()
        {
            var selected = Modes[mode.SelectedIndex].Mode;
            box.Enabled = selected is LayerSourceMode.Folder or LayerSourceMode.Path;
            browse.Enabled = box.Enabled;
            if (selected == LayerSourceMode.AutoDetect)
            {
                box.Text = "(tự dò trong folder chung)";
            }
            else if (selected == LayerSourceMode.FromTemplate)
            {
                box.Text = string.IsNullOrWhiteSpace(layer.FixedFile) ? layer.Folder : layer.FixedFile;
            }
            else if (box.Text.StartsWith('(') || box.Text == layer.Folder || box.Text == layer.FixedFile)
            {
                box.Text = layer.QueueSourcePath;
            }
        }

        mode.SelectedIndexChanged += (_, _) =>
        {
            if (_loading)
            {
                return;
            }

            layer.QueueSourceMode = Modes[mode.SelectedIndex].Mode;
            Refresh();
            Changed?.Invoke();
        };
        box.TextChanged += (_, _) =>
        {
            if (_loading || !box.Enabled)
            {
                return;
            }

            layer.QueueSourcePath = box.Text.Trim();
            Changed?.Invoke();
        };

        _loading = true;
        Refresh();
        _loading = false;
        AddRow(
            new Label { Text = $"{layer.Name} [{layer.Kind}]", AutoSize = false, Dock = DockStyle.Fill, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleLeft },
            mode,
            box,
            browse);
    }

    private void AddRow(Control caption, Control mode, Control box, Control browse)
    {
        var row = _grid.RowCount++;
        _grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        _grid.Controls.Add(caption, 0, row);
        _grid.Controls.Add(mode, 1, row);
        _grid.Controls.Add(box, 2, row);
        _grid.Controls.Add(browse, 3, row);
    }

    private Button BrowseButton(TextBox box, Func<LayerSourceMode>? mode = null)
    {
        var button = new Button
        {
            Text = "Duyệt",
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(0, 120, 215),
            ForeColor = Color.White
        };
        button.FlatAppearance.BorderSize = 0;
        button.Click += (_, _) =>
        {
            // "Đường dẫn" accepts a file: pick a file, or hold Shift to pick a folder instead.
            if (mode?.Invoke() == LayerSourceMode.Path && (ModifierKeys & Keys.Shift) == 0)
            {
                using var file = new OpenFileDialog { CheckFileExists = true, Title = "Chọn file (giữ Shift khi bấm Duyệt để chọn folder)" };
                if (file.ShowDialog(this) == DialogResult.OK)
                {
                    box.Text = file.FileName;
                }

                return;
            }

            using var dialog = new FolderBrowserDialog();
            if (Directory.Exists(box.Text))
            {
                dialog.SelectedPath = box.Text;
            }

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                box.Text = dialog.SelectedPath;
            }
        };
        return button;
    }
}
