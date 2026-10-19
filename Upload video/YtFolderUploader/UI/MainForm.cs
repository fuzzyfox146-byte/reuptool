namespace YtFolderUploader;

public sealed partial class MainForm : Form
{
    readonly SettingsStore _store = new();
    readonly AppData _data;
    readonly UploadHistory _history;
    readonly List<QueueItem> _items = new();
    readonly YouTubeUploader _uploader = new();

    ChannelProfile? _current;
    int _sourceIndex = -1;
    int _loadDepth;
    bool _uploading;
    readonly HashSet<string> _busyChannelIds = new();
    CancellationTokenSource? _uploadCts;

    bool Loading => _loadDepth > 0;

    ListBox lstChannels = null!;
    TextBox txtListName = null!;
    bool _listNameDirty;
    ListBox lstSources = null!;
    TextBox txtChannelName = null!;
    TextBox txtCredential = null!;
    Label lblAuth = null!;
    Button btnAuthorize = null!;
    Button btnReauthorize = null!;
    Button btnTest = null!;
    CancellationTokenSource? _authCts;
    int _authGeneration;
    Button btnAddChannel = null!;
    Button btnRemoveChannel = null!;
    Button btnRenameChannel = null!;
    TextBox txtSourceName = null!;
    TextBox txtVideoFolder = null!;
    TextBox txtThumbFolder = null!;
    CheckBox chkSourceEnabled = null!;
    Label lblSourcePreview = null!;
    ListBox lstPreview = null!;
    DarkTabControl _tabs = null!;
    NumericUpDown numSourceStart = null!;
    NumericUpDown numFrom = null!;
    NumericUpDown numPerDay = null!;
    TextBox txtTimes = null!;
    DateTimePicker dtpStart = null!;
    ComboBox cboTimeZone = null!;
    ComboBox cboPrivacy = null!;
    TextBox txtDescription = null!;
    TextBox txtTags = null!;
    ComboBox cboCategory = null!;
    ComboBox cboRegion = null!;
    ComboBox cboLanguage = null!;
    ComboBox cboAudioLanguage = null!;
    ComboBox cboLocLanguage = null!;
    TextBox txtLocTitle = null!;
    TextBox txtLocDescription = null!;
    CheckBox chkRecording = null!;
    DateTimePicker dtpRecording = null!;
    CheckBox chkPaid = null!;
    CheckBox chkAutoLevels = null!;
    CheckBox chkStabilize = null!;
    ComboBox cboLicense = null!;
    CheckBox chkExactDays = null!;
    CheckBox chkDeleteAfter = null!;
    CheckBox chkKids = null!;
    System.Windows.Forms.Timer? _autoTimer;
    bool _autoDaily;
    bool _autoTickRunning;
    DateTimeOffset _autoNotBefore;
    readonly Dictionary<string, DateOnly> _autoDone = new();
    readonly HashSet<string> _autoStopped = new();
    readonly HashSet<string> _autoArmed = new();
    CheckBox chkSynthetic = null!;
    CheckBox chkEmbed = null!;
    CheckBox chkStats = null!;
    CheckBox chkNotify = null!;
    TextBox txtPlaylist = null!;
    ComboBox cboTemplate = null!;
    TextBox txtTemplateName = null!;
    Label lblTemplateHint = null!;
    DataGridView grid = null!;
    NumericUpDown numBatch = null!;
    Button btnScanOne = null!;
    Button btnScanAll = null!;
    Button btnClearQueue = null!;
    Button btnUpload = null!;
    Button btnUploadAll = null!;
    Button btnDeleteUploaded = null!;
    Button btnStop = null!;
    Button btnRetryThumbs = null!;
    Label lblSummary = null!;
    Label lblProgress = null!;
    DarkProgress progressBar = null!;
    TextBox txtLog = null!;

    public MainForm()
    {
        _data = _store.Load();
        _history = _store.LoadHistory();
        BuildUi();
        numBatch.Value = Math.Clamp(_data.MaxUploadsPerRun, (int)numBatch.Minimum, (int)numBatch.Maximum);
        RefreshChannelList(_data.Channels.FirstOrDefault()?.Id);
        Shown += (_, _) =>
        {
            RefreshPreview();
            ShowChannelQueue();
        };
        Log("Cấu hình lưu tại " + _store.DirectoryPath);
        if (!string.IsNullOrEmpty(_store.RecoveryNote))
            Log(_store.RecoveryNote);
        if (_data.Channels.Count == 0)
            Log("Hãy thêm kênh, dán JSON OAuth Desktop, rồi bấm Ủy quyền kênh này.");
    }

    void BuildUi()
    {
        Text = "YouTube Folder Uploader";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1100, 720);
        Size = new Size(1280, 860);
        Font = new Font("Segoe UI", 9f);
        KeyPreview = true;
        KeyDown += (_, e) =>
        {
            if (!e.Control || e.KeyCode != Keys.S)
                return;
            SaveAll();
            RefreshQueueFromSettings();
            e.SuppressKeyPress = true;
            e.Handled = true;
        };
        BackColor = DarkTheme.Bg;
        ForeColor = DarkTheme.Text;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));

        var header = new Panel { Dock = DockStyle.Fill, BackColor = DarkTheme.Header, Tag = "header" };
        header.Controls.Add(new Label
        {
            Text = "YouTube Folder Uploader",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(16, 4)
        });
        header.Controls.Add(new Label
        {
            Text = "Quét folder video, lấy tên từ ảnh thumb, hẹn giờ đăng riêng cho từng kênh",
            ForeColor = DarkTheme.Muted,
            Tag = "muted",
            AutoSize = true,
            Location = new Point(18, 28)
        });

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            FixedPanel = FixedPanel.Panel1,
            Panel1MinSize = 168
        };
        split.Panel1.Controls.Add(BuildChannelList());
        split.Panel2.Controls.Add(BuildTabs());

        var logHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 4, 8, 8) };
        logHost.Controls.Add(new Label { Text = "Nhật ký", Dock = DockStyle.Top, Height = 18 });
        txtLog = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 8.5f)
        };
        logHost.Controls.Add(txtLog);
        txtLog.BringToFront();

        root.Controls.Add(header, 0, 0);
        root.Controls.Add(split, 0, 1);
        root.Controls.Add(logHost, 0, 2);
        Controls.Add(root);
        DarkTheme.Apply(this);
        DarkTheme.StyleAccent(btnUpload);
        DarkTheme.StyleAccent(btnUploadAll);

        Shown += (_, _) =>
        {
            try
            {
                split.SplitterDistance = 196;
            }
            catch (InvalidOperationException)
            {
            }
        };
        FormClosing += (_, args) =>
        {
            if (_uploading)
            {
                args.Cancel = true;
                _uploadCts?.Cancel();
                MessageBox.Show(this, "Đang dừng upload. Khi nhật ký báo đã dừng, hãy đóng cửa sổ lại.", "Đang đăng");
                return;
            }

            _authCts?.Cancel();
            WriteCurrent();
            _data.MaxUploadsPerRun = (int)numBatch.Value;
            SaveCurrentTemplate(announce: false);
            _store.Save(_data);
        };
    }

    Control BuildChannelList()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(8) };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 108));
        lstChannels = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 22 };
        lstChannels.SelectedIndexChanged += ChannelSelected;
        lstChannels.DrawItem += DrawChannel;
        txtListName = new TextBox { Dock = DockStyle.Fill };
        txtListName.TextChanged += (_, _) =>
        {
            if (!Loading)
                _listNameDirty = true;
        };
        txtListName.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter)
                return;
            e.SuppressKeyPress = true;
            ApplyListName(save: true);
        };

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true };
        btnAddChannel = MakeButton("Thêm kênh", AddChannel);
        btnRemoveChannel = MakeButton("Xóa kênh", RemoveChannel);
        btnRenameChannel = MakeButton("Đổi tên", () => ApplyListName(save: true));
        buttons.Controls.Add(btnAddChannel);
        buttons.Controls.Add(btnRemoveChannel);
        buttons.Controls.Add(btnRenameChannel);
        buttons.Controls.Add(MakeButton("Lưu", SaveAll));
        layout.Controls.Add(lstChannels, 0, 0);
        layout.Controls.Add(txtListName, 0, 1);
        layout.Controls.Add(buttons, 0, 2);
        return layout;
    }

    void DrawChannel(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || lstChannels.Items[e.Index] is not ChannelProfile channel)
            return;

        var selected = (e.State & DrawItemState.Selected) != 0;
        using var back = new SolidBrush(selected ? DarkTheme.Select : lstChannels.BackColor);
        e.Graphics.FillRectangle(back, e.Bounds);
        var text = channel.ShortSource ? channel + "   thiếu video" : channel.ToString();
        var color = channel.ShortSource ? DarkTheme.Bad : DarkTheme.Text;
        var textRect = new Rectangle(e.Bounds.X + 8, e.Bounds.Y, Math.Max(0, e.Bounds.Width - 12), e.Bounds.Height);
        TextRenderer.DrawText(e.Graphics, text, e.Font ?? Font, textRect, color, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.Left);
    }

    Control BuildTabs()
    {
        _tabs = new DarkTabControl { Dock = DockStyle.Fill };
        _tabs.TabPages.Add(BuildQueuePage());
        _tabs.TabPages.Add(BuildChannelPage());
        _tabs.TabPages.Add(BuildSourcePage());
        _tabs.TabPages.Add(BuildOptionsPage());
        _tabs.SelectedIndexChanged += (_, _) =>
        {
            if (!Loading && _tabs.SelectedTab?.Text == "Nguồn video")
                RefreshPreview();
        };
        return _tabs;
    }

    TabPage BuildQueuePage()
    {
        var page = new TabPage("Hàng đợi") { Padding = new Padding(8) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 132));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));

        var tools = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true };
        btnScanOne = MakeButton("Quét kênh đang chọn", () => Scan(SelectedChannelsOnly: true));
        btnScanAll = MakeButton("Quét tất cả kênh bật", () => Scan(SelectedChannelsOnly: false));
        btnUpload = MakeButton("Đăng video đã chọn", () => _ = UploadAsync());
        btnUpload.Tag = "accent";
        btnUploadAll = MakeButton("Đăng hết ngày mai", () => _ = UploadTomorrowAllAsync());
        btnUploadAll.Tag = "accent";
        btnDeleteUploaded = MakeButton("Xóa video đã đăng mọi kênh", DeleteAllUploadedFiles);
        btnStop = MakeButton("Dừng", StopAuto);
        btnStop.Enabled = false;
        btnRetryThumbs = MakeButton("Gắn lại thumbnail", () => _ = RetryThumbnailsAsync());
        tools.Controls.Add(btnScanOne);
        tools.Controls.Add(btnScanAll);
        tools.Controls.Add(btnUpload);
        tools.Controls.Add(btnUploadAll);
        tools.Controls.Add(btnStop);
        tools.Controls.Add(btnRetryThumbs);
        tools.Controls.Add(MakeButton("Xóa video và thumb đã đăng", DeleteUploadedFiles));
        tools.Controls.Add(btnDeleteUploaded);
        btnClearQueue = MakeButton("Xóa hàng đợi", ClearChannelQueue);
        tools.Controls.Add(btnClearQueue);
        tools.Controls.Add(MakeButton("Chọn video chờ", () => SetChecks(item => string.IsNullOrEmpty(item.YoutubeVideoId))));
        tools.Controls.Add(MakeButton("Bỏ chọn", () => SetChecks(_ => false)));
        var batch = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(8, 0, 0, 0) };
        batch.Controls.Add(new Label { Text = "Mỗi lần", AutoSize = true, Margin = new Padding(0, 8, 4, 0) });
        numBatch = new NumericUpDown { Minimum = 0, Maximum = 500, Value = 10, Width = 58, Margin = new Padding(0, 4, 6, 0) };
        batch.Controls.Add(numBatch);
        batch.Controls.Add(new Label { Text = "0 = tất cả", AutoSize = true, Margin = new Padding(0, 8, 0, 0) });
        tools.Controls.Add(batch);
        var from = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(8, 0, 0, 0) };
        from.Controls.Add(new Label { Text = "Từ số", AutoSize = true, Margin = new Padding(0, 8, 4, 0) });
        numFrom = new NumericUpDown { Minimum = 0, Maximum = 9999, Width = 64, Margin = new Padding(0, 4, 6, 0) };
        numFrom.ValueChanged += (_, _) => RefreshQueueFromSettings();
        from.Controls.Add(numFrom);
        from.Controls.Add(MakeButton("Bỏ số nhỏ hơn", () => RefreshQueueFromSettings()));
        tools.Controls.Add(from);
        lblSummary = new Label { Text = "Chưa quét", AutoSize = true, Margin = new Padding(12, 8, 0, 0) };
        tools.Controls.Add(lblSummary);

        grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = true,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = DarkTheme.Bg,
            BorderStyle = BorderStyle.None,
            EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2,
            ShowCellToolTips = true
        };
        grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "select", HeaderText = "", FillWeight = 24, MinimumWidth = 32, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, Width = 36 });
        AddTextColumn("channel", "Kênh", 72, 64);
        AddTextColumn("source", "Nguồn", 64, 58);
        AddTextColumn("index", "Số", 36, 36);
        var title = AddTextColumn("title", "Tiêu đề", 220, 140);
        title.ReadOnly = false;
        AddTextColumn("schedule", "Giờ đăng", 96, 84);
        AddTextColumn("size", "Dung lượng", 88, 84);
        AddTextColumn("video", "File video", 110, 88);
        AddTextColumn("thumb", "Ảnh thumb", 110, 88);
        AddTextColumn("videoId", "ID", 64, 52);
        AddTextColumn("status", "Trạng thái", 120, 96);
        grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (grid.IsCurrentCellDirty)
                grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        grid.CellValueChanged += GridCellValueChanged;
        grid.CellFormatting += GridCellFormatting;
        grid.CellToolTipTextNeeded += GridToolTip;
        grid.DataError += (_, args) => args.ThrowException = false;
        var menu = new ContextMenuStrip();
        menu.Items.Add("Mở thư mục video", null, (_, _) => OpenSelectedFile(item => item.VideoPath));
        menu.Items.Add("Mở ảnh thumb", null, (_, _) => OpenSelectedFile(item => item.ThumbPath));
        DarkTheme.StyleMenu(menu);
        grid.ContextMenuStrip = menu;

        var progress = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        progress.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        progress.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        progressBar = new DarkProgress { Dock = DockStyle.Fill, Margin = new Padding(0, 6, 8, 4) };
        lblProgress = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Text = "" };
        progress.Controls.Add(progressBar, 0, 0);
        progress.Controls.Add(lblProgress, 1, 0);

        layout.Controls.Add(tools, 0, 0);
        layout.Controls.Add(grid, 0, 1);
        layout.Controls.Add(progress, 0, 2);
        page.Controls.Add(layout);
        return page;
    }

    TabPage BuildChannelPage()
    {
        var page = new TabPage("Kênh và cấp quyền");
        var stack = new FormStack();
        stack.AddLabel("Tên trong app");
        txtChannelName = stack.Add(new TextBox(), 28);
        txtChannelName.Leave += (_, _) =>
        {
            if (Loading || _current == null)
                return;
            _current.Name = txtChannelName.Text.Trim();
            BeginLoad();
            txtListName.Text = _current.Name;
            EndLoad();
            _listNameDirty = false;
            UpdateChannelListText();
        };
        stack.AddLabel("Dán JSON OAuth Desktop, hoặc JSON đã có refresh_token. Mỗi kênh một token của đúng tài khoản đó.");
        txtCredential = stack.Add(new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            AcceptsReturn = true,
            AcceptsTab = true,
            Font = new Font("Consolas", 8.5f)
        }, 150);
        txtCredential.TextChanged += OnEdited;
        lblAuth = stack.AddLabel("Chưa có kênh.");
        var row = new FlowLayoutPanel { Height = 72, WrapContents = true };
        btnAuthorize = MakeButton("Ủy quyền kênh này", () => _ = AuthorizeAsync());
        btnReauthorize = MakeButton("Ủy quyền lại", () => _ = AuthorizeAsync());
        btnTest = MakeButton("Kiểm tra kết nối", () => _ = TestConnectionAsync());
        row.Controls.Add(btnAuthorize);
        row.Controls.Add(btnReauthorize);
        row.Controls.Add(btnTest);
        row.Controls.Add(MakeButton("Mở thư mục cấu hình", OpenDataFolder));
        stack.Add(row, 72);
        stack.AddLabel(
            "Cách lấy quyền đăng\r\n" +
            "1. Tạo project trên Google Cloud Console. Mỗi project có hạn mức upload riêng.\r\n" +
            "2. Bật YouTube Data API v3.\r\n" +
            "3. OAuth consent screen: External, thêm Gmail của kênh vào Test users.\r\n" +
            "4. Credentials → OAuth client ID → Desktop app → tải JSON.\r\n" +
            "5. Dán JSON vào ô trên, bấm Ủy quyền kênh này, đăng nhập đúng chủ kênh.\r\n" +
            "   Có 10 phút. Hết giờ hoặc đóng nhầm trình duyệt thì bấm Ủy quyền lại.\r\n" +
            "   Nếu Google báo ứng dụng chưa xác minh, chọn Advanced rồi tiếp tục. Đây là app OAuth do bạn tạo.\r\n" +
            "6. JSON có refresh_token có thể copy sang máy khác.\r\n\r\n" +
            "App xin scope youtube.upload và youtube.force-ssl. Scope thứ hai dùng để gắn thumbnail, đưa vào playlist và sửa metadata.\r\n" +
            "videos.insert thuộc hạn mức Video Uploads. Tài liệu API hiện ghi mặc định 100 video/ngày/project. Gắn thumbnail tốn 50 đơn vị trong quỹ 10.000 đơn vị/ngày. Nhiều kênh dùng chung client_id thì dùng chung hạn mức.\r\n" +
            "Consent screen đang Testing thì refresh token hết hạn sau 7 ngày. Publish app nếu bạn muốn giữ token lâu hơn.\r\n" +
            "Thumbnail tùy chỉnh yêu cầu kênh đã xác minh số điện thoại.\r\n" +
            "Video hẹn giờ được upload private kèm status.publishAt. YouTube tự public đúng giờ. Giờ trong quá khứ sẽ public ngay, nên app chỉ xếp giờ cách hiện tại ít nhất 20 phút.");
        var links = new FlowLayoutPanel { Height = 32, WrapContents = false };
        links.Controls.Add(MakeLink("Bật YouTube Data API", "https://console.cloud.google.com/apis/library/youtube.googleapis.com"));
        links.Controls.Add(MakeLink("Tạo OAuth client", "https://console.cloud.google.com/apis/credentials"));
        stack.Add(links, 32);
        stack.Finish();
        page.Controls.Add(stack);
        return page;
    }

    TabPage BuildSourcePage()
    {
        var page = new TabPage("Nguồn video");
        var stack = new FormStack();
        stack.AddLabel("Chọn thư mục gốc. Video có ảnh cùng số mới được đăng. Bấm Đưa video vào hàng đợi, rồi sang tab Hàng đợi bấm Đăng video đã chọn.");
        lstSources = stack.Add(new ListBox { IntegralHeight = false }, 72);
        lstSources.AllowDrop = true;
        lstSources.SelectedIndexChanged += SourceSelected;
        lstSources.DragEnter += (_, args) =>
        {
            if (args.Data?.GetDataPresent(DataFormats.FileDrop) == true)
                args.Effect = DragDropEffects.Copy;
        };
        lstSources.DragDrop += (_, args) =>
        {
            if (args.Data?.GetData(DataFormats.FileDrop) is not string[] paths)
                return;
            foreach (var path in paths.Where(Directory.Exists))
                AddRoot(path);
        };
        var row = new FlowLayoutPanel { Height = 72, WrapContents = true };
        row.Controls.Add(MakeButton("Thêm thư mục gốc", AddSourceFromDialog));
        row.Controls.Add(MakeButton("Xóa nguồn", RemoveSource));
        var loadQueue = MakeButton("Đưa video vào hàng đợi", LoadSelectedSourceIntoQueue);
        loadQueue.Tag = "accent";
        row.Controls.Add(loadQueue);
        stack.Add(row, 40);
        lblSourcePreview = stack.AddLabel("Chưa có video.");
        lstPreview = stack.Add(new ListBox { IntegralHeight = false }, 200);
        stack.AddLabel("Tên nguồn");
        txtSourceName = stack.Add(new TextBox(), 28);
        txtSourceName.TextChanged += OnEdited;
        txtSourceName.Leave += (_, _) => RefreshSourceListText();
        stack.AddLabel("Thư mục video");
        txtVideoFolder = stack.Add(new TextBox(), 28);
        txtVideoFolder.TextChanged += OnEdited;
        stack.Add(MakeButton("Chọn thư mục video", () => BrowseInto(txtVideoFolder)), 34);
        stack.AddLabel("Thư mục thumbnail");
        txtThumbFolder = stack.Add(new TextBox(), 28);
        txtThumbFolder.TextChanged += OnEdited;
        stack.Add(MakeButton("Chọn thư mục thumb", () => BrowseInto(txtThumbFolder)), 34);
        chkSourceEnabled = stack.Add(new CheckBox { Text = "Dùng nguồn này khi quét", Checked = true }, 28);
        chkSourceEnabled.CheckedChanged += (_, _) =>
        {
            if (!Loading)
                WriteSelectedSource();
        };
        stack.AddLabel("Chỉ đăng từ số này trở đi. 0 là tất cả. Số nhỏ hơn bị bỏ, không chiếm giờ đăng. Video đã có trong lịch sử vẫn bị bỏ dù số lớn hơn.");
        numSourceStart = stack.Add(new NumericUpDown { Minimum = 0, Maximum = 9999 }, 28);
        numSourceStart.ValueChanged += OnEdited;
        stack.Finish();
        page.Controls.Add(stack);
        return page;
    }

    TabPage BuildOptionsPage()
    {
        var page = new TabPage("Tùy chọn đăng");
        var stack = new FormStack();
        stack.AddLabel("Mẫu dùng chung. Đổi giờ rồi bấm Lưu hoặc Ctrl+S thì sửa đúng mẫu đang chọn, mọi kênh dùng mẫu đó đổi theo. Muốn bộ giờ khác: bấm Thêm mẫu trước, rồi mới đổi giờ.");
        cboTemplate = stack.Add(new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList }, 28);
        cboTemplate.SelectedIndexChanged += (_, _) => ApplySelectedTemplate();
        stack.AddLabel("Tên mẫu. Gõ tên riêng thì đổi giờ không đổi tên.");
        txtTemplateName = stack.Add(new TextBox(), 28);
        txtTemplateName.Leave += (_, _) => RenameTemplate();
        var templateRow = new FlowLayoutPanel { Height = 72, WrapContents = true };
        templateRow.Controls.Add(MakeButton("Thêm mẫu", AddTemplate));
        templateRow.Controls.Add(MakeButton("Xóa mẫu", DeleteTemplate));
        templateRow.Controls.Add(MakeButton("Dùng mẫu này", ApplySelectedTemplate));
        stack.Add(templateRow, 72);
        lblTemplateHint = stack.AddLabel("Chưa có mẫu.");
        stack.AddLabel("Số video công khai mỗi ngày");
        numPerDay = stack.Add(new NumericUpDown { Minimum = 1, Maximum = 50, Value = 3 }, 28);
        numPerDay.ValueChanged += (sender, e) =>
        {
            OnEdited(sender, e);
            RefreshQueueFromSettings();
        };
        stack.AddLabel("Giờ công khai, mỗi dòng một giờ. Bật ô dưới thì mỗi ngày chỉ dùng đúng các giờ này, không tự thêm giờ.");
        txtTimes = stack.Add(new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, AcceptsReturn = true }, 80);
        txtTimes.TextChanged += OnEdited;
        txtTimes.Leave += (_, _) => RefreshQueueFromSettings();
        chkExactDays = stack.Add(new CheckBox { Text = "Lên lịch trước 1 ngày, đúng các giờ này. App còn mở thì tự đăng ngày tiếp theo. Thiếu video cho một ngày thì dừng và tên kênh báo đỏ.", Checked = true }, 52);
        chkExactDays.CheckedChanged += (_, _) =>
        {
            if (Loading || _current == null)
                return;
            _current.Options.ExactDailyTimes = chkExactDays.Checked;
            if (!chkExactDays.Checked)
                StopAuto();
            RefreshQueueFromSettings();
        };
        chkDeleteAfter = stack.Add(new CheckBox { Text = "Sau khi đăng thành công, xóa vĩnh viễn cả video và thumbnail. Không đưa vào thùng rác.", Checked = false }, 36);
        chkDeleteAfter.CheckedChanged += (_, _) =>
        {
            if (Loading || _current == null)
                return;
            _current.Options.DeleteSourceAfterUpload = chkDeleteAfter.Checked;
        };
        stack.AddLabel("Ngày bắt đầu xếp lịch");
        dtpStart = stack.Add(new DateTimePicker { Format = DateTimePickerFormat.Short }, 28);
        dtpStart.MinDate = DateTime.Today.AddDays(-1);
        dtpStart.MaxDate = DateTime.Today.AddYears(2);
        dtpStart.ValueChanged += OnEdited;
        stack.AddLabel("Múi giờ của giờ công khai");
        cboTimeZone = stack.Add(new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList }, 28);
        foreach (var (id, label) in TimeZones.Choices)
            cboTimeZone.Items.Add(new LabeledCode { Code = id, Label = label });
        cboTimeZone.SelectedIndexChanged += OnEdited;
        stack.AddLabel("Chế độ đăng");
        cboPrivacy = stack.Add(new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList }, 28);
        AddCodes(cboPrivacy,
            ("scheduled", "Hẹn giờ — upload riêng tư, public đúng giờ đã xếp"),
            ("private", "Riêng tư ngay"),
            ("unlisted", "Không công khai ngay"),
            ("public", "Công khai ngay"));
        cboPrivacy.SelectedIndexChanged += OnEdited;
        stack.AddLabel("Mô tả. Có thể dùng {title} {index} {source} {filename}");
        txtDescription = stack.Add(new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, AcceptsReturn = true }, 90);
        txtDescription.TextChanged += OnEdited;
        stack.AddLabel("Thẻ, cách nhau bởi dấu phẩy. YouTube giới hạn 500 ký tự.");
        txtTags = stack.Add(new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical }, 60);
        txtTags.TextChanged += OnEdited;
        stack.AddLabel("Quốc gia dùng để tải danh sách thể loại");
        cboRegion = stack.Add(new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList }, 28);
        AddCodes(cboRegion, ("JP", "Nhật Bản"), ("VN", "Việt Nam"), ("US", "Hoa Kỳ"), ("KR", "Hàn Quốc"), ("TW", "Đài Loan"), ("TH", "Thái Lan"));
        cboRegion.SelectedIndexChanged += OnEdited;
        stack.AddLabel("Thể loại");
        cboCategory = stack.Add(new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList }, 28);
        cboCategory.SelectedIndexChanged += OnEdited;
        stack.Add(MakeButton("Tải thể loại từ YouTube", () => _ = ReloadCategoriesAsync()), 34);
        stack.AddLabel("Ngôn ngữ tiêu đề và mô tả (snippet.defaultLanguage)");
        cboLanguage = stack.Add(new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList }, 28);
        AddCodes(cboLanguage, Languages("Không đặt"));
        cboLanguage.SelectedIndexChanged += OnEdited;
        stack.AddLabel("Ngôn ngữ tiếng nói (snippet.defaultAudioLanguage)");
        cboAudioLanguage = stack.Add(new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList }, 28);
        AddCodes(cboAudioLanguage, Languages("Giống ngôn ngữ tiêu đề"));
        cboAudioLanguage.SelectedIndexChanged += OnEdited;
        stack.AddLabel("Bản địa phương khác (localizations). Chọn ngôn ngữ rồi điền tiêu đề. Có thể dùng {title}.");
        cboLocLanguage = stack.Add(new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList }, 28);
        AddCodes(cboLocLanguage, Languages("Không gửi bản địa phương"));
        cboLocLanguage.SelectedIndexChanged += OnEdited;
        txtLocTitle = stack.Add(new TextBox(), 28);
        txtLocTitle.TextChanged += OnEdited;
        txtLocDescription = stack.Add(new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, AcceptsReturn = true }, 70);
        txtLocDescription.TextChanged += OnEdited;
        chkKids = stack.Add(new CheckBox { Text = "Video dành cho trẻ em (status.selfDeclaredMadeForKids)" }, 28);
        chkSynthetic = stack.Add(new CheckBox { Text = "Có nội dung AI / giả lập trông như thật (status.containsSyntheticMedia)" }, 28);
        chkEmbed = stack.Add(new CheckBox { Text = "Cho phép nhúng (status.embeddable)" }, 28);
        chkStats = stack.Add(new CheckBox { Text = "Hiện số lượt xem mở rộng (status.publicStatsViewable)" }, 28);
        chkNotify = stack.Add(new CheckBox { Text = "Báo người đăng ký (notifySubscribers)" }, 28);
        chkPaid = stack.Add(new CheckBox { Text = "Có nội dung trả phí / đặt sản phẩm (paidProductPlacementDetails)" }, 28);
        chkAutoLevels = stack.Add(new CheckBox { Text = "Tự cân màu khi upload (autoLevels)" }, 28);
        chkStabilize = stack.Add(new CheckBox { Text = "Tự chống rung khi upload (stabilize)" }, 28);
        chkKids.CheckedChanged += OnEdited;
        chkSynthetic.CheckedChanged += OnEdited;
        chkEmbed.CheckedChanged += OnEdited;
        chkStats.CheckedChanged += OnEdited;
        chkNotify.CheckedChanged += OnEdited;
        chkPaid.CheckedChanged += OnEdited;
        chkAutoLevels.CheckedChanged += OnEdited;
        chkStabilize.CheckedChanged += OnEdited;
        chkRecording = stack.Add(new CheckBox { Text = "Gửi ngày ghi hình (recordingDetails.recordingDate)" }, 28);
        chkRecording.CheckedChanged += (sender, args) =>
        {
            if (dtpRecording != null)
                dtpRecording.Enabled = chkRecording.Checked;
            OnEdited(sender, args);
        };
        dtpRecording = stack.Add(new DateTimePicker { Format = DateTimePickerFormat.Short }, 28);
        dtpRecording.MinDate = new DateTime(1970, 1, 1);
        dtpRecording.MaxDate = DateTime.Today.AddYears(1);
        dtpRecording.ValueChanged += OnEdited;
        stack.AddLabel("Giấy phép");
        cboLicense = stack.Add(new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList }, 28);
        AddCodes(cboLicense, ("youtube", "Giấy phép YouTube chuẩn"), ("creativeCommon", "Creative Commons"));
        cboLicense.SelectedIndexChanged += OnEdited;
        stack.AddLabel("Playlist, dán ID hoặc link playlist. Để trống nếu không thêm.");
        txtPlaylist = stack.Add(new TextBox(), 28);
        txtPlaylist.TextChanged += OnEdited;
        stack.AddLabel("Sau khi đổi số video mỗi ngày hoặc giờ đăng, bấm Quét lại. Bảng hàng đợi giữ tiêu đề bạn đã sửa cho đến lần quét sau.");
        stack.Finish();
        page.Controls.Add(stack);
        return page;
    }

    void BeginLoad() => _loadDepth++;

    void EndLoad() => _loadDepth--;

    void OnEdited(object? sender, EventArgs e) => WriteCurrent();

    void ChannelSelected(object? sender, EventArgs e)
    {
        if (Loading || lstChannels.SelectedItem is not ChannelProfile selected || ReferenceEquals(_current, selected))
            return;
        WriteCurrent();
        SaveCurrentTemplate(announce: false);
        _store.Save(_data);
        _current = selected;
        LoadCurrent();
        ShowChannelQueue();
    }

    void SourceSelected(object? sender, EventArgs e)
    {
        if (Loading || _current == null)
            return;
        WriteSelectedSource();
        _sourceIndex = lstSources.SelectedIndex;
        ReadSelectedSource();
    }

    void RefreshChannelList(string? selectId)
    {
        BeginLoad();
        lstChannels.Items.Clear();
        foreach (var channel in _data.Channels)
            lstChannels.Items.Add(channel);
        var index = _data.Channels.FindIndex(channel => channel.Id == selectId);
        if (index < 0 && _data.Channels.Count > 0)
            index = 0;
        if (index >= 0)
            lstChannels.SelectedIndex = index;
        _current = index >= 0 ? _data.Channels[index] : null;
        EndLoad();
        LoadCurrent();
    }

    void ApplyListName(bool save)
    {
        if (Loading || _current == null || IsBusy(_current))
            return;
        var name = txtListName.Text.Trim();
        if (name.Length == 0)
        {
            BeginLoad();
            txtListName.Text = _current.Name;
            EndLoad();
            _listNameDirty = false;
            MessageBox.Show(this, "Tên kênh không được để trống.", "Đổi tên");
            return;
        }

        _current.Name = name;
        BeginLoad();
        txtChannelName.Text = name;
        txtListName.Text = name;
        EndLoad();
        _listNameDirty = false;
        UpdateChannelListText();
        if (!save)
            return;
        _store.Save(_data);
        Log($"Đã lưu tên kênh: {name}");
    }

    void UpdateChannelListText()
    {
        if (_current == null)
            return;
        var selectedId = _current.Id;
        BeginLoad();
        lstChannels.Items.Clear();
        foreach (var channel in _data.Channels)
            lstChannels.Items.Add(channel);
        var index = _data.Channels.FindIndex(channel => channel.Id == selectedId);
        if (index >= 0)
            lstChannels.SelectedIndex = index;
        EndLoad();
    }

    void LoadCurrent()
    {
        BeginLoad();
        var has = _current != null;
        txtChannelName.Enabled = has;
        txtListName.Enabled = has;
        txtCredential.Enabled = has;
        btnAuthorize.Enabled = has;
        btnReauthorize.Enabled = has;
        btnTest.Enabled = has;
        if (!has)
        {
            txtChannelName.Text = "";
            txtListName.Text = "";
            txtCredential.Text = "";
            lblAuth.Text = "Hãy thêm một kênh.";
            lstSources.Items.Clear();
            cboTemplate.Items.Clear();
            txtTemplateName.Text = "";
            lblTemplateHint.Text = "Hãy chọn một kênh.";
            EndLoad();
            return;
        }

        txtChannelName.Text = _current!.Name;
        txtListName.Text = _current.Name;
        _listNameDirty = false;
        txtCredential.Text = _current.CredentialText;
        ReadOptions(_current);
        BindTemplates();
        EndLoad();
        LoadSourceList();
        UpdateAuthLabel();
        ApplyEditorLock();
    }

    bool IsBusy(ChannelProfile? channel) => channel != null && _busyChannelIds.Contains(channel.Id);

    void ApplyEditorLock()
    {
        var locked = IsBusy(_current);
        if (_tabs != null)
        {
            foreach (TabPage page in _tabs.TabPages)
            {
                if (page.Text is "Kênh và cấp quyền" or "Nguồn video" or "Tùy chọn đăng")
                    SetInputsEnabled(page, _current != null && !locked);
            }
        }

        if (txtListName != null)
            txtListName.ReadOnly = locked;
        if (btnRenameChannel != null)
            btnRenameChannel.Enabled = _current != null && !locked;
        if (grid != null)
            grid.ReadOnly = locked;
        if (btnRemoveChannel != null)
            btnRemoveChannel.Enabled = _current != null && !locked;
        if (btnScanOne != null)
            btnScanOne.Enabled = _current != null && !locked;
        if (btnClearQueue != null)
            btnClearQueue.Enabled = _current != null && !locked;
        if (!_uploading)
        {
            if (btnUpload != null)
                btnUpload.Enabled = true;
            if (btnUploadAll != null)
                btnUploadAll.Enabled = true;
            if (btnDeleteUploaded != null)
                btnDeleteUploaded.Enabled = true;
            if (btnScanAll != null)
                btnScanAll.Enabled = true;
            if (btnRetryThumbs != null)
                btnRetryThumbs.Enabled = true;
            if (btnAddChannel != null)
                btnAddChannel.Enabled = true;
        }
    }

    static void SetInputsEnabled(Control root, bool enabled)
    {
        foreach (Control child in root.Controls)
        {
            if (child is TextBox or ComboBox or NumericUpDown or CheckBox or DateTimePicker or Button or LinkLabel)
                child.Enabled = enabled;
            else
                SetInputsEnabled(child, enabled);
        }
    }

    void WriteCurrent()
    {
        if (Loading || _current == null)
            return;
        if (_listNameDirty)
            ApplyListName(save: false);
        else
            _current.Name = txtChannelName.Text.Trim();
        _current.CredentialText = txtCredential.Text;
        ApplyCredential(_current, txtCredential.Text);
        WriteOptions();
        WriteSelectedSource();
        UpdateAuthLabel();
    }

    static void ApplyCredential(ChannelProfile channel, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            channel.ClientId = "";
            channel.ClientSecret = "";
            channel.RefreshToken = "";
            return;
        }

        var parsed = CredentialParser.Parse(text);
        if (!string.IsNullOrEmpty(parsed.ClientId))
            channel.ClientId = parsed.ClientId;
        if (!string.IsNullOrEmpty(parsed.ClientSecret))
            channel.ClientSecret = parsed.ClientSecret;
        if (!string.IsNullOrEmpty(parsed.RefreshToken))
            channel.RefreshToken = parsed.RefreshToken;
        if (!string.IsNullOrEmpty(parsed.ChannelId))
            channel.YoutubeChannelId = parsed.ChannelId;
        if (!string.IsNullOrEmpty(parsed.ChannelTitle) && string.IsNullOrEmpty(channel.YoutubeChannelTitle))
            channel.YoutubeChannelTitle = parsed.ChannelTitle;
    }

    void UpdateAuthLabel()
    {
        if (_current == null)
        {
            lblAuth.Text = "Hãy thêm một kênh.";
            return;
        }

        if (string.IsNullOrWhiteSpace(_current.ClientId))
            lblAuth.Text = "Chưa đọc được client_id.";
        else if (string.IsNullOrWhiteSpace(_current.RefreshToken))
            lblAuth.Text = "Đã có client. Bấm Ủy quyền kênh này. Nếu hết giờ, bấm Ủy quyền lại.";
        else if (!string.IsNullOrWhiteSpace(_current.YoutubeChannelTitle))
            lblAuth.Text = $"Sẵn sàng: {_current.YoutubeChannelTitle} ({_current.YoutubeChannelId})";
        else
            lblAuth.Text = "Đã có refresh token. Bấm Kiểm tra kết nối.";
    }

    void ReadOptions(ChannelProfile channel)
    {
        var options = channel.Options;
        numPerDay.Value = Math.Clamp(options.VideosPerDay, (int)numPerDay.Minimum, (int)numPerDay.Maximum);
        txtTimes.Text = options.TimeSlotsText;
        ApplyCheck(chkExactDays, true, options.ExactDailyTimes);
        ApplyCheck(chkDeleteAfter, true, options.DeleteSourceAfterUpload);
        var start = options.ScheduleStartDate.ToDateTime(TimeOnly.MinValue);
        if (start < dtpStart.MinDate)
            start = dtpStart.MinDate;
        if (start > dtpStart.MaxDate)
            start = dtpStart.MaxDate;
        dtpStart.Value = start;
        SelectCode(cboTimeZone, options.TimeZoneId);
        SelectCode(cboPrivacy, options.PrivacyMode);
        txtDescription.Text = options.DescriptionTemplate;
        txtTags.Text = options.Tags;
        SelectCode(cboRegion, options.CategoryRegion);
        BindCategories();
        SelectCode(cboLanguage, options.DefaultLanguage);
        SelectCode(cboAudioLanguage, options.DefaultAudioLanguage);
        SelectCode(cboLocLanguage, options.LocalizationLanguage);
        txtLocTitle.Text = options.LocalizationTitle;
        txtLocDescription.Text = options.LocalizationDescription;
        chkKids.Checked = options.MadeForKids;
        chkSynthetic.Checked = options.ContainsSyntheticMedia;
        chkEmbed.Checked = options.Embeddable;
        chkStats.Checked = options.PublicStatsViewable;
        chkNotify.Checked = options.NotifySubscribers;
        chkPaid.Checked = options.HasPaidProductPlacement;
        chkAutoLevels.Checked = options.AutoLevels;
        chkStabilize.Checked = options.Stabilize;
        chkRecording.Checked = options.SendRecordingDate;
        dtpRecording.Enabled = options.SendRecordingDate;
        var recorded = options.RecordingDate.ToDateTime(TimeOnly.MinValue);
        if (recorded < dtpRecording.MinDate)
            recorded = dtpRecording.MinDate;
        if (recorded > dtpRecording.MaxDate)
            recorded = dtpRecording.MaxDate;
        dtpRecording.Value = recorded;
        SelectCode(cboLicense, options.License);
        txtPlaylist.Text = options.PlaylistId;
    }

    void WriteOptions()
    {
        if (_current == null)
            return;
        var options = _current.Options;
        options.VideosPerDay = (int)numPerDay.Value;
        options.TimeSlotsText = txtTimes.Text;
        options.ScheduleStartDate = DateOnly.FromDateTime(dtpStart.Value);
        options.TimeZoneId = SelectedCode(cboTimeZone, "SE Asia Standard Time");
        options.PrivacyMode = SelectedCode(cboPrivacy, "scheduled");
        options.DescriptionTemplate = txtDescription.Text;
        options.Tags = txtTags.Text;
        options.CategoryRegion = SelectedCode(cboRegion, "JP");
        if (cboCategory.SelectedItem is CategoryChoice category)
            options.CategoryId = category.Id;
        options.DefaultLanguage = SelectedCode(cboLanguage, "ja");
        options.DefaultAudioLanguage = SelectedCode(cboAudioLanguage, "");
        options.LocalizationLanguage = SelectedCode(cboLocLanguage, "");
        options.LocalizationTitle = txtLocTitle.Text;
        options.LocalizationDescription = txtLocDescription.Text;
        options.MadeForKids = chkKids.Checked;
        options.ContainsSyntheticMedia = chkSynthetic.Checked;
        options.Embeddable = chkEmbed.Checked;
        options.PublicStatsViewable = chkStats.Checked;
        options.NotifySubscribers = chkNotify.Checked;
        options.HasPaidProductPlacement = chkPaid.Checked;
        options.AutoLevels = chkAutoLevels.Checked;
        options.Stabilize = chkStabilize.Checked;
        options.SendRecordingDate = chkRecording.Checked;
        options.RecordingDate = DateOnly.FromDateTime(dtpRecording.Value);
        options.License = SelectedCode(cboLicense, "youtube");
        options.PlaylistId = txtPlaylist.Text.Trim();
        UpdateTemplateHint();
    }

    void BindTemplates()
    {
        BeginLoad();
        cboTemplate.Items.Clear();
        foreach (var template in _data.Templates)
            cboTemplate.Items.Add(template);
        UploadTemplate? selected = null;
        if (_current != null)
        {
            selected = _data.Templates.FirstOrDefault(template => template.Id == _current.TemplateId)
                ?? _data.Templates.FirstOrDefault(template => template.Key == UploadTemplates.KeyOf(_current.Options));
            if (selected != null)
                _current.TemplateId = selected.Id;
        }

        cboTemplate.SelectedItem = selected;
        txtTemplateName.Text = selected?.Name ?? "";
        EndLoad();
        UpdateTemplateHint();
    }

    void UpdateTemplateHint()
    {
        if (lblTemplateHint == null)
            return;
        if (_current == null)
        {
            lblTemplateHint.Text = "Hãy chọn một kênh.";
            return;
        }

        var match = _data.Templates.FirstOrDefault(template => template.Key == UploadTemplates.KeyOf(_current.Options));
        if (match == null)
        {
            var linked = _data.Templates.FirstOrDefault(template => template.Id == _current.TemplateId);
            lblTemplateHint.Text = linked == null
                ? "Kênh này chưa gắn mẫu. Chọn một mẫu rồi bấm Dùng mẫu này."
                : "Bên dưới đang khác mẫu đang chọn. Bấm Dùng mẫu này để đổ số video, giờ và các mục còn lại.";
            return;
        }

        lblTemplateHint.Text = $"Bên dưới đang khớp mẫu \"{match.Name}\".";
    }

    void AddTemplate()
    {
        if (_current == null)
            return;
        WriteCurrent();
        var created = UploadTemplates.AddCopy(_data, _current);
        _store.Save(_data);
        BindTemplates();
        Log($"Đã thêm mẫu \"{created.Name}\" cho {_current.Name}. Mẫu cũ giữ nguyên. Đổi giờ rồi lưu thì chỉ mẫu mới đổi.");
    }

    void DeleteTemplate()
    {
        if (cboTemplate.SelectedItem is not UploadTemplate selected)
            return;
        if (_data.Templates.Count <= 1)
        {
            MessageBox.Show(this, "Còn một mẫu. Hãy thêm mẫu khác trước khi xóa mẫu này.", "Xóa mẫu");
            return;
        }

        var users = _data.Channels.Count(channel => channel.TemplateId == selected.Id);
        if (MessageBox.Show(this, $"Xóa mẫu \"{selected.Name}\"? {users} kênh đang dùng sẽ bỏ liên kết mẫu này.", "Xóa mẫu", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        _data.Templates.Remove(selected);
        foreach (var channel in _data.Channels)
        {
            if (channel.TemplateId == selected.Id)
                channel.TemplateId = "";
        }

        _store.Save(_data);
        BindTemplates();
        Log($"Đã xóa mẫu \"{selected.Name}\".");
    }

    void RenameTemplate()
    {
        if (Loading || _current == null || cboTemplate.SelectedItem is not UploadTemplate selected)
            return;
        var name = txtTemplateName.Text.Trim();
        if (name.Length == 0 || name == selected.Name)
            return;
        selected.Name = name;
        selected.NameIsCustom = true;
        _store.Save(_data);
        BindTemplates();
        Log($"Đã đổi tên mẫu thành \"{name}\".");
    }

    void ApplySelectedTemplate()
    {
        if (Loading || _current == null || cboTemplate.SelectedItem is not UploadTemplate template)
            return;

        UploadTemplates.Apply(_current, template);
        BeginLoad();
        ReadOptions(_current);
        EndLoad();
        _store.Save(_data);
        RefreshQueueFromSettings();
        var times = template.Options.TimeSlotsText
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Log($"Đã áp mẫu \"{template.Name}\" cho {_current.Name}: {template.Options.VideosPerDay} video/ngày, giờ {string.Join(", ", times)}.");
    }

    void SaveCurrentTemplate(bool announce)
    {
        if (_current == null)
            return;
        var template = _data.Templates.FirstOrDefault(item => item.Id == _current.TemplateId);
        if (template == null)
            return;
        var before = template.Key;
        UploadTemplates.SaveInto(_data, _current, channel => _busyChannelIds.Contains(channel.Id));
        BindTemplates();
        if (announce && before != template.Key)
            Log($"Đã cập nhật mẫu \"{template.Name}\". Mọi kênh đang dùng mẫu này đổi theo.");
    }

    void BindCategories()
    {
        if (_current == null)
            return;
        var selected = _current.Options.CategoryId;
        var list = _current.Options.Categories.Count > 0 ? _current.Options.Categories : CategoryChoice.Defaults();
        cboCategory.Items.Clear();
        foreach (var category in list)
            cboCategory.Items.Add(category);
        CategoryChoice? match = null;
        foreach (CategoryChoice category in cboCategory.Items)
        {
            if (category.Id == selected)
                match = category;
        }

        cboCategory.SelectedItem = match ?? (cboCategory.Items.Count > 0 ? cboCategory.Items[0] : null);
    }

    void LoadSourceList()
    {
        BeginLoad();
        lstSources.Items.Clear();
        if (_current == null)
        {
            _sourceIndex = -1;
            EndLoad();
            ReadSelectedSource();
            return;
        }

        foreach (var source in _current.Sources)
            lstSources.Items.Add(source);
        if (_current.Sources.Count > 0)
            lstSources.SelectedIndex = 0;
        _sourceIndex = lstSources.SelectedIndex;
        EndLoad();
        ReadSelectedSource();
    }

    void ReadSelectedSource()
    {
        BeginLoad();
        var source = CurrentSource();
        var has = source != null;
        txtSourceName.Enabled = has;
        txtVideoFolder.Enabled = has;
        txtThumbFolder.Enabled = has;
        chkSourceEnabled.Enabled = has;
        numSourceStart.Enabled = has;
        txtSourceName.Text = source?.Name ?? "";
        txtVideoFolder.Text = source?.VideoFolder ?? "";
        txtThumbFolder.Text = source?.ThumbFolder ?? "";
        ApplyCheck(chkSourceEnabled, has, source?.Enabled == true);
        numSourceStart.Value = Math.Clamp(source?.StartIndex ?? 0, (int)numSourceStart.Minimum, (int)numSourceStart.Maximum);
        if (numFrom != null)
            numFrom.Value = numSourceStart.Value;
        EndLoad();
        RefreshPreview();
    }

    void ApplyCheck(CheckBox box, bool canUse, bool isChecked)
    {
        box.Enabled = true;
        box.Checked = isChecked;
        box.Enabled = canUse;
        if (!box.IsHandleCreated || box.Checked == isChecked)
            return;

        BeginInvoke(() =>
        {
            BeginLoad();
            box.Enabled = true;
            box.Checked = isChecked;
            box.Enabled = canUse;
            EndLoad();
        });
    }

    void LoadSelectedSourceIntoQueue()
    {
        if (CurrentSource() == null)
        {
            MessageBox.Show(this, "Hãy chọn một nguồn trước.", "Hàng đợi");
            return;
        }

        BeginLoad();
        ApplyCheck(chkSourceEnabled, true, true);
        EndLoad();
        WriteSelectedSource();
        CurrentSource()!.Enabled = true;
        _store.Save(_data);
        Scan(SelectedChannelsOnly: true);
        _tabs.SelectedIndex = 0;
    }

    void RefreshPreview()
    {
        if (lstPreview == null || lblSourcePreview == null)
            return;

        lstPreview.Items.Clear();
        var source = CurrentSource();
        if (source == null || string.IsNullOrWhiteSpace(source.VideoFolder))
        {
            lblSourcePreview.Text = "Chưa có thư mục video.";
            return;
        }

        var scan = FolderScanner.Scan(source);
        var ready = 0;
        var missing = 0;
        foreach (var video in scan.Videos)
        {
            if (video.MissingThumb)
            {
                missing++;
                continue;
            }

            if (FileNames.IsBeforeStart(video.Index, source.StartIndex))
                continue;

            var uploaded = _history.FindExact(_current!.Id, video.VideoPath, video.FileSize) != null;
            lstPreview.Items.Add(uploaded
                ? $"{video.Index}  {video.Title}  — đã đăng"
                : $"{video.Index}  {video.Title}");
            if (!uploaded)
                ready++;
        }

        lblSourcePreview.Text = scan.Videos.Count == 0
            ? $"Không thấy video trong {source.VideoFolder}."
            : $"{ready} video có ảnh, sẵn sàng đăng. {missing} video thiếu ảnh, không đưa vào hàng.";
    }

    void WriteSelectedSource()
    {
        var source = CurrentSource();
        if (source == null)
            return;
        source.Name = txtSourceName.Text.Trim();
        source.VideoFolder = txtVideoFolder.Text.Trim();
        source.ThumbFolder = txtThumbFolder.Text.Trim();
        source.Enabled = chkSourceEnabled.Checked;
        source.StartIndex = (int)numSourceStart.Value;
    }

    VideoSource? CurrentSource()
    {
        if (_current == null || _sourceIndex < 0 || _sourceIndex >= _current.Sources.Count)
            return null;
        return _current.Sources[_sourceIndex];
    }

    void RefreshSourceListText()
    {
        if (Loading || _current == null)
            return;
        WriteSelectedSource();
        var selected = _sourceIndex;
        BeginLoad();
        lstSources.Items.Clear();
        foreach (var source in _current.Sources)
            lstSources.Items.Add(source);
        if (selected >= 0 && selected < lstSources.Items.Count)
            lstSources.SelectedIndex = selected;
        _sourceIndex = lstSources.SelectedIndex;
        EndLoad();
    }

    void AddChannel()
    {
        WriteCurrent();
        var channel = new ChannelProfile
        {
            Name = "Kênh " + (_data.Channels.Count + 1),
            TemplateId = _current?.TemplateId ?? "",
            Options = _current?.Options.Clone() ?? UploadOptions.CreateDefault()
        };
        _data.Channels.Add(channel);
        RefreshChannelList(channel.Id);
        _store.Save(_data);
        Log($"Đã thêm {channel.Name}. Tùy chọn đăng được copy từ kênh vừa xem. Hãy dán token và chọn nguồn riêng.");
    }

    void RemoveChannel()
    {
        if (_current == null)
            return;
        if (MessageBox.Show(this, $"Xóa \"{_current.Name}\" khỏi app? Video đã đăng trên YouTube không bị xóa.", "Xóa kênh", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;
        _data.Channels.Remove(_current);
        _current = null;
        RefreshChannelList(null);
        _store.Save(_data);
    }

    void AddSourceFromDialog()
    {
        var path = PickFolder("Chọn thư mục gốc, ví dụ D:\\HD036");
        if (path != null)
            AddRoot(path);
    }

    void AddRoot(string root)
    {
        if (_current == null)
        {
            MessageBox.Show(this, "Hãy thêm và chọn một kênh trước.", "Nguồn video");
            return;
        }

        WriteSelectedSource();
        var source = new VideoSource { Name = new DirectoryInfo(root).Name };
        FolderScanner.ApplyRoot(source, root);
        source.Enabled = true;
        _current.Sources.Add(source);
        LoadSourceList();
        lstSources.SelectedIndex = _current.Sources.Count - 1;
        Log($"{_current.Name}: thêm nguồn {source.Name}. Video: {source.VideoFolder}. Thumb: {source.ThumbFolder}. Bấm Đưa video vào hàng đợi.");
        RefreshPreview();
    }

    void RemoveSource()
    {
        var source = CurrentSource();
        if (_current == null || source == null)
            return;
        _current.Sources.Remove(source);
        LoadSourceList();
    }

    void BrowseInto(TextBox target)
    {
        var path = PickFolder("Chọn thư mục");
        if (path == null)
            return;
        target.Text = path;
        WriteSelectedSource();
        RefreshPreview();
    }

    string? PickFolder(string description)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = description,
            UseDescriptionForTitle = true
        };
        return dialog.ShowDialog(this) == DialogResult.OK ? dialog.SelectedPath : null;
    }

    async Task AuthorizeAsync()
    {
        if (_current == null)
            return;
        WriteCurrent();
        if (string.IsNullOrWhiteSpace(_current.ClientId) || string.IsNullOrWhiteSpace(_current.ClientSecret))
        {
            MessageBox.Show(this, "Cần client_id và client_secret. Hãy dán JSON OAuth client kiểu Desktop app.", "Ủy quyền");
            return;
        }

        _authCts?.Cancel();
        var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        _authCts = cts;
        var generation = ++_authGeneration;
        var channel = _current;

        try
        {
            btnAuthorize.Enabled = false;
            Log("Mở trình duyệt để ủy quyền. Có 10 phút để đăng nhập đúng tài khoản kênh này. Hết giờ thì bấm Ủy quyền lại.");
            var credential = await YouTubeAuth.AuthorizeInteractiveAsync(channel.ClientId, channel.ClientSecret, cts.Token);
            if (generation != _authGeneration || !ReferenceEquals(_current, channel))
                return;

            var refresh = credential.Token.RefreshToken;
            if (string.IsNullOrWhiteSpace(refresh))
                throw new InvalidOperationException("Google không trả refresh token. Hãy gỡ app tại myaccount.google.com/permissions rồi bấm Ủy quyền lại.");

            channel.RefreshToken = refresh;
            using var service = YouTubeAuth.CreateService(channel);
            var identity = await YouTubeAuth.GetMyChannelAsync(service, CancellationToken.None);
            channel.YoutubeChannelId = identity.Id;
            channel.YoutubeChannelTitle = identity.Title;
            if (string.IsNullOrWhiteSpace(channel.Name) || channel.Name.StartsWith("Kênh ", StringComparison.Ordinal))
            {
                channel.Name = identity.Title;
                txtChannelName.Text = identity.Title;
            }

            var parsed = new ParsedCredential
            {
                ClientId = channel.ClientId,
                ClientSecret = channel.ClientSecret,
                RefreshToken = refresh,
                ChannelId = identity.Id,
                ChannelTitle = identity.Title
            };
            txtCredential.Text = CredentialParser.ToPortableJson(parsed, identity.Id, identity.Title);
            channel.CredentialText = txtCredential.Text;
            await RememberCategoriesAsync(service);
            UpdateChannelListText();
            UpdateAuthLabel();
            _store.Save(_data);
            Log($"Đã ủy quyền {channel.YoutubeChannelTitle} ({channel.YoutubeChannelId}).");
        }
        catch (Exception) when (generation != _authGeneration)
        {
            Log("Đã hủy lần ủy quyền trước để mở lại.");
        }
        catch (OperationCanceledException)
        {
            Log("Hết 10 phút mà chưa đăng nhập xong. Bấm Ủy quyền lại để mở trình duyệt lần nữa.");
            MessageBox.Show(this, "Hết thời gian ủy quyền. Bấm Ủy quyền lại, rồi đăng nhập trong vòng 10 phút.", "Ủy quyền");
        }
        catch (Exception ex)
        {
            var message = ApiErrors.Explain(ex);
            Log(message);
            MessageBox.Show(this, message + "\n\nBấm Ủy quyền lại để thử lần nữa.", "Ủy quyền");
        }
        finally
        {
            if (generation == _authGeneration)
            {
                btnAuthorize.Enabled = _current != null;
                btnReauthorize.Enabled = _current != null;
                cts.Dispose();
                if (ReferenceEquals(_authCts, cts))
                    _authCts = null;
            }
        }
    }

    async Task TestConnectionAsync()
    {
        if (_current == null)
            return;
        WriteCurrent();
        await RunChannelAction(async () =>
        {
            using var service = YouTubeAuth.CreateService(_current);
            var identity = await YouTubeAuth.GetMyChannelAsync(service, CancellationToken.None);
            _current.YoutubeChannelId = identity.Id;
            _current.YoutubeChannelTitle = identity.Title;
            await RememberCategoriesAsync(service);
            UpdateChannelListText();
            UpdateAuthLabel();
            _store.Save(_data);
            Log($"Đã nối {_current.YoutubeChannelTitle} ({_current.YoutubeChannelId}).");
        });
    }

    async Task ReloadCategoriesAsync()
    {
        if (_current == null)
            return;
        WriteCurrent();
        await RunChannelAction(async () =>
        {
            using var service = YouTubeAuth.CreateService(_current);
            await RememberCategoriesAsync(service);
            _store.Save(_data);
            Log($"Đã tải thể loại vùng {_current.Options.CategoryRegion}.");
        });
    }

    async Task RememberCategoriesAsync(Google.Apis.YouTube.v3.YouTubeService service)
    {
        if (_current == null)
            return;
        var categories = await YouTubeAuth.LoadCategoriesAsync(service, _current.Options.CategoryRegion, CancellationToken.None);
        if (categories.Count == 0)
            return;
        _current.Options.Categories = categories;
        BeginLoad();
        BindCategories();
        EndLoad();
    }

    async Task RunChannelAction(Func<Task> action)
    {
        try
        {
            btnAuthorize.Enabled = false;
            btnTest.Enabled = false;
            await action();
        }
        catch (Exception ex)
        {
            var message = ApiErrors.Explain(ex);
            Log(message);
            MessageBox.Show(this, message, "YouTube");
        }
        finally
        {
            btnAuthorize.Enabled = _current != null;
            btnTest.Enabled = _current != null;
        }
    }

    void SaveAll()
    {
        WriteCurrent();
        _data.MaxUploadsPerRun = (int)numBatch.Value;
        SaveCurrentTemplate(announce: true);
        _store.Save(_data);
        Log("Đã lưu cấu hình.");
    }

    void OpenDataFolder() => System.Diagnostics.Process.Start("explorer.exe", _store.DirectoryPath);

    void Log(string message)
    {
        void write()
        {
            txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        }

        if (txtLog.InvokeRequired)
            txtLog.BeginInvoke(write);
        else
            write();
    }

    static (string Code, string Label)[] Languages(string emptyLabel) =>
    [
        ("", emptyLabel),
        ("ja", "ja — Nhật"),
        ("vi", "vi — Tiếng Việt"),
        ("en", "en — English"),
        ("ko", "ko — Hàn"),
        ("zh-Hans", "zh-Hans — Trung giản thể"),
        ("zh-Hant", "zh-Hant — Trung phồn thể"),
        ("th", "th — Thái"),
        ("id", "id — Indonesia")
    ];

    static void AddCodes(ComboBox box, params (string Code, string Label)[] items)
    {
        foreach (var (code, label) in items)
            box.Items.Add(new LabeledCode { Code = code, Label = label });
    }

    static void SelectCode(ComboBox box, string code)
    {
        foreach (LabeledCode item in box.Items)
        {
            if (item.Code == code)
            {
                box.SelectedItem = item;
                return;
            }
        }

        if (box.Items.Count > 0 && box.SelectedIndex < 0)
            box.SelectedIndex = 0;
    }

    static string SelectedCode(ComboBox box, string fallback) =>
        box.SelectedItem is LabeledCode item ? item.Code : fallback;

    DataGridViewTextBoxColumn AddTextColumn(string name, string header, int weight, int minimumWidth)
    {
        var column = new DataGridViewTextBoxColumn
        {
            Name = name,
            HeaderText = header,
            FillWeight = weight,
            MinimumWidth = minimumWidth,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            ReadOnly = true
        };
        grid.Columns.Add(column);
        return column;
    }

    static Button MakeButton(string text, Action click)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            Padding = new Padding(8, 2, 8, 2),
            Margin = new Padding(0, 2, 6, 2),
            FlatStyle = FlatStyle.Flat,
            BackColor = DarkTheme.Button,
            ForeColor = DarkTheme.Text,
            UseVisualStyleBackColor = false,
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderColor = DarkTheme.Border;
        button.FlatAppearance.MouseOverBackColor = DarkTheme.ButtonHover;
        button.Click += (_, _) => click();
        return button;
    }

    static LinkLabel MakeLink(string text, string url)
    {
        var link = new LinkLabel { Text = text, AutoSize = true, Margin = new Padding(0, 6, 16, 0) };
        link.LinkClicked += (_, _) =>
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        };
        return link;
    }
}
