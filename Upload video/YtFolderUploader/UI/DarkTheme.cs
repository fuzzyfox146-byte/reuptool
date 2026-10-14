namespace YtFolderUploader;

static class DarkTheme
{
    public static readonly Color Bg = Color.FromArgb(24, 24, 27);
    public static readonly Color Header = Color.FromArgb(16, 16, 18);
    public static readonly Color Surface = Color.FromArgb(36, 36, 40);
    public static readonly Color Input = Color.FromArgb(30, 30, 34);
    public static readonly Color Button = Color.FromArgb(46, 46, 52);
    public static readonly Color ButtonHover = Color.FromArgb(62, 62, 70);
    public static readonly Color Text = Color.FromArgb(236, 236, 241);
    public static readonly Color Muted = Color.FromArgb(168, 168, 176);
    public static readonly Color Border = Color.FromArgb(68, 68, 76);
    public static readonly Color Accent = Color.FromArgb(204, 0, 0);
    public static readonly Color Select = Color.FromArgb(58, 78, 112);
    public static readonly Color GridAlt = Color.FromArgb(32, 32, 36);
    public static readonly Color Ok = Color.FromArgb(126, 214, 150);
    public static readonly Color Warn = Color.FromArgb(255, 196, 102);
    public static readonly Color Bad = Color.FromArgb(255, 128, 128);

    public static void Apply(Control root)
    {
        Style(root);
        foreach (Control child in root.Controls)
            Apply(child);
    }

    public static void StyleAccent(Button button)
    {
        button.Tag = "accent";
        button.FlatStyle = FlatStyle.Flat;
        button.UseVisualStyleBackColor = false;
        button.BackColor = Accent;
        button.ForeColor = Color.White;
        button.FlatAppearance.BorderColor = Color.FromArgb(150, 0, 0);
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(230, 40, 40);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(160, 0, 0);
    }

    public static void StyleMenu(ContextMenuStrip menu)
    {
        menu.Renderer = new ToolStripProfessionalRenderer(new DarkColorTable());
        menu.BackColor = Surface;
        menu.ForeColor = Text;
        foreach (ToolStripItem item in menu.Items)
        {
            item.BackColor = Surface;
            item.ForeColor = Text;
        }
    }

    static void Style(Control control)
    {
        switch (control)
        {
            case DarkTabControl tabs:
                tabs.BackColor = Bg;
                tabs.ForeColor = Text;
                break;
            case TabPage page:
                page.UseVisualStyleBackColor = false;
                page.BackColor = Bg;
                page.ForeColor = Text;
                break;
            case DataGridView grid:
                StyleGrid(grid);
                break;
            case Button button:
                if (button.Tag as string == "accent")
                    StyleAccent(button);
                else
                    StyleButton(button);
                break;
            case LinkLabel link:
                link.LinkColor = Color.FromArgb(147, 186, 255);
                link.ActiveLinkColor = Color.White;
                link.VisitedLinkColor = link.LinkColor;
                link.DisabledLinkColor = Muted;
                link.BackColor = Color.Transparent;
                link.ForeColor = Text;
                break;
            case Label label:
                label.ForeColor = control.Tag as string == "muted" ? Muted : Text;
                label.BackColor = Color.Transparent;
                break;
            case CheckBox check:
                check.FlatStyle = FlatStyle.Flat;
                check.ForeColor = Text;
                check.BackColor = Color.Transparent;
                check.UseVisualStyleBackColor = false;
                check.FlatAppearance.BorderColor = Color.FromArgb(186, 186, 196);
                check.FlatAppearance.BorderSize = 1;
                check.FlatAppearance.CheckedBackColor = Color.FromArgb(36, 120, 72);
                check.Paint -= PaintCheck;
                check.Paint += PaintCheck;
                break;
            case TextBox text:
                text.BackColor = Input;
                text.ForeColor = Text;
                text.BorderStyle = BorderStyle.FixedSingle;
                break;
            case ListBox list:
                list.BackColor = Input;
                list.ForeColor = Text;
                list.BorderStyle = BorderStyle.FixedSingle;
                break;
            case ComboBox combo:
                StyleCombo(combo);
                break;
            case NumericUpDown number:
                number.BackColor = Input;
                number.ForeColor = Text;
                break;
            case DateTimePicker date:
                date.CalendarMonthBackground = Input;
                date.CalendarForeColor = Text;
                date.CalendarTitleBackColor = Header;
                date.CalendarTitleForeColor = Text;
                break;
            case ProgressBar:
                break;
            case FlowLayoutPanel or TableLayoutPanel or Panel or SplitterPanel or FormStack:
                control.BackColor = control.Tag as string == "header" ? Header : Bg;
                control.ForeColor = Text;
                break;
            case SplitContainer split:
                split.BackColor = Border;
                split.ForeColor = Text;
                break;
            case Form form:
                form.BackColor = Bg;
                form.ForeColor = Text;
                break;
        }
    }

    static void PaintCheck(object? sender, PaintEventArgs e)
    {
        if (sender is not CheckBox box)
            return;

        var size = 15;
        var y = Math.Max(0, (box.Height - size) / 2);
        var rect = new Rectangle(1, y, size - 1, size - 1);
        using var fill = new SolidBrush(box.Checked ? Color.FromArgb(36, 120, 72) : Input);
        using var border = new Pen(Color.FromArgb(186, 186, 196));
        e.Graphics.FillRectangle(fill, rect);
        e.Graphics.DrawRectangle(border, rect);
        if (!box.Checked)
            return;

        using var tick = new Pen(Color.White, 2);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        e.Graphics.DrawLines(tick, new Point[]
        {
            new(rect.Left + 3, rect.Top + 7),
            new(rect.Left + 6, rect.Top + 11),
            new(rect.Left + 12, rect.Top + 3)
        });
    }

    static void StyleButton(Button button)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.UseVisualStyleBackColor = false;
        button.BackColor = Button;
        button.ForeColor = Text;
        button.FlatAppearance.BorderColor = Border;
        button.FlatAppearance.MouseOverBackColor = ButtonHover;
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(78, 78, 88);
    }

    static void StyleCombo(ComboBox combo)
    {
        combo.FlatStyle = FlatStyle.Flat;
        combo.BackColor = Input;
        combo.ForeColor = Text;
        combo.DrawMode = DrawMode.OwnerDrawFixed;
        combo.ItemHeight = 22;
        combo.DrawItem -= DrawComboItem;
        combo.DrawItem += DrawComboItem;
    }

    static void DrawComboItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not ComboBox combo || e.Index < 0)
            return;
        var selected = (e.State & DrawItemState.Selected) != 0;
        using var brush = new SolidBrush(selected ? Select : Input);
        e.Graphics.FillRectangle(brush, e.Bounds);
        var text = combo.Items[e.Index]?.ToString() ?? "";
        var bounds = new Rectangle(e.Bounds.X + 6, e.Bounds.Y, Math.Max(0, e.Bounds.Width - 8), e.Bounds.Height);
        TextRenderer.DrawText(e.Graphics, text, e.Font ?? combo.Font, bounds, Text, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
    }

    static void StyleGrid(DataGridView grid)
    {
        grid.EnableHeadersVisualStyles = false;
        grid.BackgroundColor = Bg;
        grid.BorderStyle = BorderStyle.None;
        grid.GridColor = Border;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grid.RowHeadersVisible = false;

        grid.ColumnHeadersDefaultCellStyle.BackColor = Header;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Text;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Header;
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Text;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.25f);
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(3, 2, 3, 2);
        grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
        grid.ColumnHeadersHeight = 26;

        grid.DefaultCellStyle.BackColor = Surface;
        grid.DefaultCellStyle.ForeColor = Text;
        grid.DefaultCellStyle.SelectionBackColor = Select;
        grid.DefaultCellStyle.SelectionForeColor = Text;
        grid.DefaultCellStyle.Font = new Font("Segoe UI", 8.5f);
        grid.DefaultCellStyle.Padding = new Padding(3, 1, 3, 1);
        grid.RowTemplate.Height = 24;
        grid.AlternatingRowsDefaultCellStyle.BackColor = GridAlt;
        grid.AlternatingRowsDefaultCellStyle.ForeColor = Text;
        grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = Select;
        grid.AlternatingRowsDefaultCellStyle.SelectionForeColor = Text;
        grid.RowsDefaultCellStyle.BackColor = Surface;
        grid.RowsDefaultCellStyle.ForeColor = Text;
    }
}

sealed class DarkTabControl : TabControl
{
    public DarkTabControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        SizeMode = TabSizeMode.Fixed;
        ItemSize = new Size(168, 28);
        BackColor = DarkTheme.Bg;
        ForeColor = DarkTheme.Text;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(DarkTheme.Bg);
        for (var i = 0; i < TabCount; i++)
        {
            var rect = GetTabRect(i);
            var selected = i == SelectedIndex;
            using var brush = new SolidBrush(selected ? DarkTheme.Surface : DarkTheme.Header);
            e.Graphics.FillRectangle(brush, rect);
            TextRenderer.DrawText(
                e.Graphics,
                TabPages[i].Text,
                Font,
                rect,
                selected ? DarkTheme.Text : DarkTheme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if (!selected)
                continue;
            using var pen = new Pen(DarkTheme.Accent, 2);
            e.Graphics.DrawLine(pen, rect.Left + 12, rect.Bottom - 2, rect.Right - 12, rect.Bottom - 2);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        for (var i = 0; i < TabCount; i++)
        {
            if (GetTabRect(i).Contains(e.Location))
            {
                SelectedIndex = i;
                break;
            }
        }
        base.OnMouseDown(e);
    }
}

sealed class DarkProgress : Control
{
    int _value;

    public DarkProgress()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Height = 10;
    }

    public int Value
    {
        get => _value;
        set
        {
            _value = Math.Clamp(value, 0, 100);
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        using var track = new SolidBrush(DarkTheme.Input);
        e.Graphics.FillRectangle(track, ClientRectangle);
        var width = (int)Math.Round(ClientSize.Width * (_value / 100.0));
        if (width <= 0)
            return;
        using var fill = new SolidBrush(DarkTheme.Accent);
        e.Graphics.FillRectangle(fill, 0, 0, width, ClientSize.Height);
    }
}

sealed class DarkColorTable : ProfessionalColorTable
{
    public override Color MenuItemSelected => DarkTheme.Select;
    public override Color MenuItemSelectedGradientBegin => DarkTheme.Select;
    public override Color MenuItemSelectedGradientEnd => DarkTheme.Select;
    public override Color MenuItemBorder => DarkTheme.Select;
    public override Color MenuBorder => DarkTheme.Border;
    public override Color MenuItemPressedGradientBegin => DarkTheme.ButtonHover;
    public override Color MenuItemPressedGradientEnd => DarkTheme.ButtonHover;
    public override Color ToolStripDropDownBackground => DarkTheme.Surface;
    public override Color ImageMarginGradientBegin => DarkTheme.Surface;
    public override Color ImageMarginGradientMiddle => DarkTheme.Surface;
    public override Color ImageMarginGradientEnd => DarkTheme.Surface;
    public override Color SeparatorDark => DarkTheme.Border;
    public override Color SeparatorLight => DarkTheme.Border;
}
