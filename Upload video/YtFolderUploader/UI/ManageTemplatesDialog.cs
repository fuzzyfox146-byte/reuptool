namespace YtFolderUploader;

public sealed class ManageTemplatesDialog : Form
{
    private readonly AppData _data;
    private readonly ChannelProfile _current;
    private readonly ListBox _lstTemplates;
    private readonly TextBox _txtName;
    private readonly NumericUpDown _numPerDay;
    private readonly TextBox _txtTimes;
    private readonly Label _lblInfo;
    private readonly Button _btnApply;
    private readonly Button _btnDelete;
    private readonly Button _btnSave;
    private bool _loading;

    public bool Changed { get; private set; }

    public ManageTemplatesDialog(AppData data, ChannelProfile current)
    {
        _data = data;
        _current = current;

        Text = "Quản lý mẫu đăng tải";
        Size = new Size(720, 520);
        MinimumSize = new Size(600, 420);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = DarkTheme.Bg;
        ForeColor = DarkTheme.Text;
        Font = new Font("Segoe UI", 9f);
        ShowIcon = false;
        MaximizeBox = false;
        MinimizeBox = false;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(12)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));

        // Cột trái: Danh sách mẫu & nút Thêm
        var leftPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = new Padding(0, 0, 8, 0)
        };
        leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        leftPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));

        leftPanel.Controls.Add(new Label { Text = "Danh sách mẫu:", ForeColor = DarkTheme.Muted, AutoSize = true }, 0, 0);

        _lstTemplates = new ListBox
        {
            Dock = DockStyle.Fill,
            BackColor = DarkTheme.Surface,
            ForeColor = DarkTheme.Text,
            BorderStyle = BorderStyle.FixedSingle,
            IntegralHeight = false
        };
        _lstTemplates.SelectedIndexChanged += (_, _) => OnTemplateSelected();
        leftPanel.Controls.Add(_lstTemplates, 0, 1);

        var btnAdd = MakeButton("+ Thêm mẫu mới", OnAddTemplate);
        btnAdd.Dock = DockStyle.Fill;
        leftPanel.Controls.Add(btnAdd, 0, 2);

        root.Controls.Add(leftPanel, 0, 0);

        // Cột phải: Chi tiết mẫu đang chọn
        var rightPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(6, 0, 0, 0)
        };
        var form = new FormStack();

        form.AddLabel("Tên mẫu:");
        _txtName = form.Add(new TextBox(), 28);
        _txtName.TextChanged += (_, _) => OnDetailChanged();

        form.AddLabel("Số video công khai mỗi ngày:");
        _numPerDay = form.Add(new NumericUpDown { Minimum = 1, Maximum = 50, Value = 3 }, 28);
        _numPerDay.ValueChanged += (_, _) => OnDetailChanged();

        form.AddLabel("Giờ công khai (mỗi dòng một giờ, vd 09:00):");
        _txtTimes = form.Add(new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, AcceptsReturn = true }, 110);
        _txtTimes.TextChanged += (_, _) => OnDetailChanged();

        _lblInfo = form.AddLabel("");
        _lblInfo.ForeColor = DarkTheme.Muted;

        var actRow = new FlowLayoutPanel { Height = 40, WrapContents = false, AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
        _btnSave = MakeButton("Lưu thay đổi", OnSaveSelectedTemplate);
        _btnDelete = MakeButton("Xóa mẫu này", OnDeleteSelectedTemplate);
        actRow.Controls.Add(_btnSave);
        actRow.Controls.Add(_btnDelete);
        form.Add(actRow, 40);

        form.Finish();
        rightPanel.Controls.Add(form);
        root.Controls.Add(rightPanel, 1, 0);

        // Hàng dưới: Nút áp dụng cho kênh hiện tại & Đóng
        var bottom = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 8, 0, 0)
        };
        var btnClose = MakeButton("Đóng", () => Close());
        _btnApply = MakeButton($"Áp dụng mẫu này cho kênh {_current.Name}", OnApplyToCurrentChannel);
        _btnApply.BackColor = Color.FromArgb(0, 120, 215);
        _btnApply.ForeColor = Color.White;

        bottom.Controls.Add(btnClose);
        bottom.Controls.Add(_btnApply);
        root.Controls.Add(bottom, 0, 1);
        root.SetColumnSpan(bottom, 2);

        Controls.Add(root);

        ReloadTemplatesList(_current.TemplateId);
    }

    private void ReloadTemplatesList(string? selectId = null)
    {
        _loading = true;
        _lstTemplates.Items.Clear();
        foreach (var t in _data.Templates)
            _lstTemplates.Items.Add(t);

        UploadTemplate? target = null;
        if (!string.IsNullOrEmpty(selectId))
            target = _data.Templates.FirstOrDefault(t => t.Id == selectId);

        target ??= _data.Templates.FirstOrDefault();
        _lstTemplates.SelectedItem = target;
        _loading = false;
        LoadSelectedTemplate();
    }

    private void OnTemplateSelected()
    {
        if (_loading) return;
        LoadSelectedTemplate();
    }

    private void LoadSelectedTemplate()
    {
        _loading = true;
        if (_lstTemplates.SelectedItem is not UploadTemplate t)
        {
            _txtName.Text = "";
            _txtName.Enabled = false;
            _numPerDay.Enabled = false;
            _txtTimes.Text = "";
            _txtTimes.Enabled = false;
            _btnSave.Enabled = false;
            _btnDelete.Enabled = false;
            _btnApply.Enabled = false;
            _lblInfo.Text = "Chưa có mẫu nào.";
            _loading = false;
            return;
        }

        _txtName.Enabled = true;
        _numPerDay.Enabled = true;
        _txtTimes.Enabled = true;
        _btnSave.Enabled = true;
        _btnDelete.Enabled = true;
        _btnApply.Enabled = true;

        _txtName.Text = t.Name;
        _numPerDay.Value = Math.Clamp(t.Options.VideosPerDay, (int)_numPerDay.Minimum, (int)_numPerDay.Maximum);
        _txtTimes.Text = t.Options.TimeSlotsText;

        var usingCount = _data.Channels.Count(c => c.TemplateId == t.Id);
        _lblInfo.Text = usingCount > 0
            ? $"Có {usingCount} kênh đang áp dụng mẫu này."
            : "Chưa có kênh nào dùng mẫu này.";

        _btnApply.Text = $"Áp dụng mẫu này cho kênh {_current.Name}";
        _loading = false;
    }

    private void OnDetailChanged()
    {
        // người dùng đang gõ
    }

    private void OnAddTemplate()
    {
        var copy = UploadTemplates.AddCopy(_data, _current);
        Changed = true;
        ReloadTemplatesList(copy.Id);
    }

    private void OnSaveSelectedTemplate()
    {
        if (_lstTemplates.SelectedItem is not UploadTemplate t) return;
        var name = _txtName.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            MessageBox.Show(this, "Tên mẫu không được để trống.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        t.Name = name;
        t.NameIsCustom = true;
        t.Options.VideosPerDay = (int)_numPerDay.Value;
        t.Options.TimeSlotsText = _txtTimes.Text;
        t.Key = UploadTemplates.KeyOf(t.Options);

        // Đồng bộ các kênh đang gắn mẫu này
        foreach (var ch in _data.Channels)
        {
            if (ch.TemplateId == t.Id)
                ch.Options = UploadTemplates.CloneFor(t.Options, ch.Options);
        }

        Changed = true;
        var idx = _lstTemplates.SelectedIndex;
        _lstTemplates.Items[idx] = t; // cập nhật lại hiển thị tên
        MessageBox.Show(this, $"Đã lưu mẫu \"{t.Name}\" thành công!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void OnDeleteSelectedTemplate()
    {
        if (_lstTemplates.SelectedItem is not UploadTemplate t) return;
        var usingCount = _data.Channels.Count(c => c.TemplateId == t.Id);
        var msg = $"Bạn có chắc muốn xóa mẫu \"{t.Name}\"?\n({usingCount} kênh đang dùng mẫu này sẽ chuyển thành cấu hình riêng).";
        if (MessageBox.Show(this, msg, "Xác nhận xóa", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        _data.Templates.Remove(t);
        foreach (var ch in _data.Channels)
        {
            if (ch.TemplateId == t.Id)
                UploadTemplates.Detach(ch);
        }

        Changed = true;
        ReloadTemplatesList();
    }

    private void OnApplyToCurrentChannel()
    {
        if (_lstTemplates.SelectedItem is not UploadTemplate t) return;
        UploadTemplates.Apply(_current, t);
        Changed = true;
        MessageBox.Show(this, $"Đã áp dụng mẫu \"{t.Name}\" cho kênh {_current.Name}!", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
        DialogResult = DialogResult.OK;
        Close();
    }

    private static Button MakeButton(string text, Action click)
    {
        var b = new Button
        {
            Text = text,
            AutoSize = true,
            Padding = new Padding(10, 4, 10, 4),
            FlatStyle = FlatStyle.Flat,
            BackColor = DarkTheme.Button,
            ForeColor = DarkTheme.Text,
            UseVisualStyleBackColor = false,
            Cursor = Cursors.Hand
        };
        b.FlatAppearance.BorderColor = DarkTheme.Border;
        b.FlatAppearance.MouseOverBackColor = DarkTheme.ButtonHover;
        b.Click += (_, _) => click();
        return b;
    }
}
