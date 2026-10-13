namespace YtFolderUploader;

sealed class FormStack : Panel
{
    bool _busy;

    public FormStack()
    {
        AutoScroll = true;
        Dock = DockStyle.Fill;
        BackColor = DarkTheme.Bg;
        ForeColor = DarkTheme.Text;
        Resize += (_, _) => Reflow();
    }

    public Label AddLabel(string text)
    {
        var label = new Label
        {
            Text = text,
            AutoSize = true,
            Margin = Padding.Empty
        };
        Controls.Add(label);
        return label;
    }

    public T Add<T>(T control, int height) where T : Control
    {
        control.Height = height;
        Controls.Add(control);
        return control;
    }

    public void Finish() => Reflow();

    void Reflow()
    {
        if (_busy)
            return;
        _busy = true;
        try
        {
            var width = Math.Max(320, (ClientSize.Width > 80 ? ClientSize.Width : 720) - 28);
            var y = 10;
            foreach (Control control in Controls)
            {
                control.Left = 12;
                control.Top = y;
                if (control is Label or LinkLabel)
                {
                    control.MaximumSize = new Size(width, 0);
                    y += control.Height + 4;
                }
                else
                {
                    control.Width = width;
                    y += control.Height + 8;
                }
            }

            AutoScrollMinSize = new Size(0, y + 16);
        }
        finally
        {
            _busy = false;
        }
    }
}
