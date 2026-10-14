using System.Diagnostics.CodeAnalysis;
using System.Drawing.Drawing2D;

namespace NetSet;

public class ModernButton : Control
{
    public enum Kind { Normal, Primary, Danger }

    public Kind Style { get; set; } = Kind.Normal;
    public bool LeftAlign { get; set; }
    public bool Chevron { get; set; }

    bool _hover, _down;

    public ModernButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.StandardClick, true);
        Cursor = Cursors.Hand;
        Font = Theme.Ui;
        AutoSize = true;
        Margin = new Padding(0, 0, 8, 0);
        Size = new Size(90, 36);
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var t = TextRenderer.MeasureText(Text, Font);
        return new Size(t.Width + this.S(32) + (Chevron ? this.S(22) : 0), this.S(36));
    }

    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Cursor = Enabled ? Cursors.Hand : Cursors.Default; Invalidate(); }
    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }
    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _down = true; Focus(); Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    protected override bool IsInputKey(Keys keyData) => keyData == Keys.Space || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter && Enabled) { OnClick(EventArgs.Empty); e.Handled = true; }
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Back);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        Color fill, text = Theme.Text, border = Color.Empty;
        switch (Style)
        {
            case Kind.Primary:
                fill = !Enabled ? Theme.Surface : _down ? Theme.AccentDown : _hover ? Theme.AccentHover : Theme.Accent;
                text = Color.White;
                break;
            case Kind.Danger:
                fill = !Enabled ? Theme.Surface : _down ? Color.FromArgb(90, 45, 50) : _hover ? Color.FromArgb(66, 40, 44) : Theme.Surface2;
                text = Theme.Danger;
                border = Theme.Border;
                break;
            default:
                fill = !Enabled ? Theme.Surface : _down ? Theme.Hover : _hover ? Color.FromArgb(56, 56, 64) : Theme.Surface2;
                border = Theme.Border;
                break;
        }
        if (!Enabled) text = Theme.TextFaint;

        var r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
        using (var path = Theme.RoundRect(r, this.S(8)))
        using (var b = new SolidBrush(fill))
        {
            g.FillPath(b, path);
            if (border != Color.Empty && Enabled) { using var pen = new Pen(border); g.DrawPath(pen, path); }
            if (Focused && ShowFocusCues) { using var pen = new Pen(Theme.Accent, 1.5f); g.DrawPath(pen, path); }
        }

        int pad = this.S(14), chev = Chevron ? this.S(24) : 0;
        var tr = new Rectangle(pad, 0, Width - pad * 2 - chev, Height);
        var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix |
                    (LeftAlign ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter);
        TextRenderer.DrawText(g, Text, Font, tr, text, flags);

        if (Chevron)
        {
            float cx = Width - this.S(20), cy = Height / 2f;
            using var pen = new Pen(Theme.TextDim, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLines(pen, new[] { new PointF(cx - 4, cy - 2), new PointF(cx, cy + 2), new PointF(cx + 4, cy - 2) });
        }
    }
}

/// <summary>Rounded surface that hosts other controls.</summary>
public class Card : Panel
{
    public Card()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Theme.Back;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Theme.RoundRect(new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), this.S(10));
        using var b = new SolidBrush(Theme.Surface);
        using var pen = new Pen(Theme.Border);
        g.FillPath(b, path);
        g.DrawPath(pen, path);
    }
}

public class InfoCard : Card
{
    List<(string Label, string Value)> _rows = new();

    public InfoCard()
    {
        int pad = this.S(16);
        Padding = new Padding(pad, this.S(12), pad, this.S(12));
        Height = Padding.Vertical + this.S(24) * 4;
    }

    public void SetRows(List<(string Label, string Value)> rows) { _rows = rows; Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        int rowH = this.S(24), labelW = this.S(92), gap = this.S(16);
        int colW = (Width - Padding.Horizontal - gap) / 2;
        const TextFormatFlags f = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.Left;

        for (int i = 0; i < _rows.Count; i++)
        {
            int col = i < 4 ? 0 : 1, row = i < 4 ? i : i - 4;
            int x = Padding.Left + col * (colW + gap), y = Padding.Top + row * rowH;
            var (label, value) = _rows[i];

            TextRenderer.DrawText(e.Graphics, label, Theme.Ui, new Rectangle(x, y, labelW, rowH), Theme.TextDim, f);

            int vx = x + labelW;
            if (label == "Status")
            {
                var dot = value == "Up" ? Theme.Good : Theme.TextFaint;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var b = new SolidBrush(dot);
                int d = this.S(8);
                e.Graphics.FillEllipse(b, vx, y + (rowH - d) / 2, d, d);
                vx += d + this.S(8);
            }
            TextRenderer.DrawText(e.Graphics, value, Theme.Ui, new Rectangle(vx, y, x + colW - vx, rowH), Theme.Text, f);
        }
    }
}

public class ModernList : ListView
{
    int _hot = -1;
    readonly ImageList _rowSize = new();

    public ModernList()
    {
        DoubleBuffered = true;
        OwnerDraw = true;
        View = View.Details;
        FullRowSelect = true;
        MultiSelect = false;
        HideSelection = false;
        BorderStyle = BorderStyle.None;
        HeaderStyle = ColumnHeaderStyle.Nonclickable;
        BackColor = Theme.Surface;
        ForeColor = Theme.Text;
        Font = Theme.Ui;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        _rowSize.ImageSize = new Size(1, this.S(40)); // forces the row height
        SmallImageList = _rowSize;
        // Header and rows are owner-drawn; the unthemed control has no column-divider lines in the empty area.
        Theme.SetWindowTheme(Handle, "", "");
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int idx = GetItemAt(e.X, e.Y)?.Index ?? -1;
        if (idx == _hot) return;
        int old = _hot; _hot = idx;
        if (old >= 0 && old < Items.Count) RedrawItems(old, old, false);
        if (idx >= 0) RedrawItems(idx, idx, false);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        int old = _hot; _hot = -1;
        if (old >= 0 && old < Items.Count) RedrawItems(old, old, false);
    }

    protected override void OnDrawColumnHeader(DrawListViewColumnHeaderEventArgs e)
    {
        using (var b = new SolidBrush(Theme.Surface)) e.Graphics.FillRectangle(b, e.Bounds);
        var r = new Rectangle(e.Bounds.X + this.S(12), e.Bounds.Y, e.Bounds.Width - this.S(12), e.Bounds.Height);
        TextRenderer.DrawText(e.Graphics, e.Header?.Text ?? "", Theme.UiSmallBold, r, Theme.TextDim,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        using var pen = new Pen(Theme.Border);
        e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
    }

    protected override void OnDrawItem(DrawListViewItemEventArgs e)
    {
        var r = new Rectangle(0, e.Bounds.Y, ClientSize.Width, e.Bounds.Height);
        var c = e.Item.Selected ? Theme.SelRow : e.ItemIndex == _hot ? Theme.RowHover : Theme.Surface;
        using (var b = new SolidBrush(c)) e.Graphics.FillRectangle(b, r);
        using var pen = new Pen(Theme.Divider);
        e.Graphics.DrawLine(pen, r.Left, r.Bottom - 1, r.Right, r.Bottom - 1);
    }

    protected override void OnDrawSubItem(DrawListViewSubItemEventArgs e)
    {
        var b = e.Bounds;
        if (e.ColumnIndex == 0) b.Width = Columns[0].Width;

        if (e.ColumnIndex == 0)
        {
            bool fav = !string.IsNullOrEmpty(e.SubItem?.Text);
            TextRenderer.DrawText(e.Graphics, fav ? "★" : "☆", Theme.Symbol, b,
                fav ? Theme.Gold : Theme.TextFaint,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            return;
        }

        var r = new Rectangle(b.X + this.S(12), b.Y, b.Width - this.S(12), b.Height);
        var color = e.ColumnIndex == 1 ? Theme.Text : Theme.TextDim;
        TextRenderer.DrawText(e.Graphics, e.SubItem?.Text ?? "", Theme.Ui, r, color,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }
}

/// <summary>
/// TextBox that shows a gray suggestion while empty (native cue banner); Tab in an empty box accepts it.
/// </summary>
public class GhostTextBox : TextBox
{
    const int EM_SETCUEBANNER = 0x1501;
    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

    string _ghost = "";

    public string Ghost
    {
        get => _ghost;
        set { _ghost = value ?? ""; Apply(); }
    }

    /// <summary>Suggestions are only shown (and accepted) while the box is editable.</summary>
    public void Apply()
    {
        if (!IsHandleCreated) return;
        SendMessage(Handle, EM_SETCUEBANNER, (IntPtr)1, ReadOnly ? "" : _ghost); // 1 = keep showing while focused
    }

    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Apply(); }
    protected override void OnReadOnlyChanged(EventArgs e) { base.OnReadOnlyChanged(e); Apply(); }

    protected override bool ProcessDialogKey(Keys keyData)
    {
        if (keyData == Keys.Tab && Text.Length == 0 && _ghost.Length > 0 && !ReadOnly)
        {
            Text = _ghost; // accept the suggestion, then fall through so Tab also moves to the next field
        }
        return base.ProcessDialogKey(keyData);
    }
}

public class ModernTextBox : Panel
{
    readonly GhostTextBox _tb = new()
    {
        BorderStyle = BorderStyle.None,
        BackColor = Theme.Surface2,
        ForeColor = Theme.Text,
        Font = Theme.Ui,
    };

    /// <summary>Gray suggestion shown while empty; press Tab to accept it.</summary>
    public string Placeholder { get => _tb.Ghost; set => _tb.Ghost = value; }

    public ModernTextBox()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Theme.Back;
        Height = _tb.PreferredHeight + 18;
        Controls.Add(_tb);
        _tb.Enter += (_, _) => Invalidate();
        _tb.Leave += (_, _) => Invalidate();
        Click += (_, _) => _tb.Focus();
    }

    /// <summary>Focuses the inner text box (a plain Panel cannot take focus).</summary>
    public new bool Focus() => _tb.Focus();

    [AllowNull]
    public override string Text { get => _tb.Text; set => _tb.Text = value ?? ""; }

    public bool Editable
    {
        get => !_tb.ReadOnly;
        set { _tb.ReadOnly = !value; _tb.TabStop = value; _tb.ForeColor = value ? Theme.Text : Theme.TextFaint; Invalidate(); }
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        int pad = this.S(12);
        _tb.SetBounds(pad, (Height - _tb.Height) / 2, Math.Max(10, Width - pad * 2), _tb.Height);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Theme.RoundRect(new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), this.S(8));
        using var b = new SolidBrush(Editable ? Theme.Surface2 : Theme.Surface);
        using var pen = new Pen(_tb.Focused ? Theme.Accent : Theme.Border, _tb.Focused ? 1.5f : 1f);
        g.FillPath(b, path);
        g.DrawPath(pen, path);
        _tb.BackColor = Editable ? Theme.Surface2 : Theme.Surface;
    }
}

/// <summary>Radio button or check box drawn to match the theme.</summary>
public class Choice : Control
{
    bool _checked, _hover;
    public bool IsRadio { get; set; }
    public event EventHandler? CheckedChanged;

    public Choice()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.StandardClick, true);
        Cursor = Cursors.Hand;
        Font = Theme.Ui;
        Height = 30;
    }

    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value) return;
            _checked = value;
            if (value && IsRadio && Parent != null)
                foreach (var s in Parent.Controls.OfType<Choice>().Where(c => c != this && c.IsRadio)) s.Checked = false;
            if (IsRadio && IsHandleCreated) TabStop = value;
            Invalidate();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override bool IsInputKey(Keys k) =>
        k == Keys.Space || (IsRadio && k is Keys.Up or Keys.Down or Keys.Left or Keys.Right) || base.IsInputKey(k);

    // A radio group is a single tab stop (the checked one); arrow keys move within the group.
    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); TabStop = !IsRadio || _checked; }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space) { OnClick(EventArgs.Empty); e.Handled = true; }
        else if (IsRadio && e.KeyCode is Keys.Up or Keys.Down or Keys.Left or Keys.Right && Parent != null)
        {
            var group = Parent.Controls.OfType<Choice>().Where(c => c.IsRadio && c.Enabled).ToList();
            int step = e.KeyCode is Keys.Up or Keys.Left ? -1 : 1;
            var next = group[(group.IndexOf(this) + step + group.Count) % group.Count];
            next.Focus();
            next.Checked = true;
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }
    protected override void OnClick(EventArgs e) { Focus(); Checked = IsRadio || !Checked; base.OnClick(e); }
    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Back);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        int d = this.S(18);
        var box = new RectangleF(1, (Height - d) / 2f, d - 1, d - 1);
        using (var path = IsRadio ? EllipsePath(box) : Theme.RoundRect(box, this.S(5)))
        {
            if (_checked)
            {
                using var b = new SolidBrush(Theme.Accent);
                g.FillPath(b, path);
            }
            else
            {
                using var b = new SolidBrush(Theme.Surface2);
                g.FillPath(b, path);
            }
            using var pen = new Pen(_checked ? Theme.Accent : _hover ? Theme.TextDim : Theme.Border, Focused && ShowFocusCues ? 2f : 1f);
            g.DrawPath(pen, path);
        }

        if (_checked)
        {
            using var pen = new Pen(Color.White, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            using var b = new SolidBrush(Color.White);
            float cx = box.X + box.Width / 2, cy = box.Y + box.Height / 2;
            if (IsRadio) g.FillEllipse(b, cx - d * 0.17f, cy - d * 0.17f, d * 0.34f, d * 0.34f);
            else g.DrawLines(pen, new[] { new PointF(cx - d * .24f, cy), new PointF(cx - d * .06f, cy + d * .18f), new PointF(cx + d * .26f, cy - d * .18f) });
        }

        TextRenderer.DrawText(g, Text, Font, new Rectangle(d + this.S(10), 0, Width - d - this.S(10), Height), Theme.Text,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }

    static GraphicsPath EllipsePath(RectangleF r)
    {
        var p = new GraphicsPath();
        p.AddEllipse(r);
        return p;
    }
}

/// <summary>Dark replacement for MessageBox.</summary>
public static class ThemedBox
{
    public static bool Show(IWin32Window? owner, string message, string title, bool confirm = false) =>
        Choose(owner, message, title, confirm ? new[] { "Yes", "No" } : new[] { "OK" }) == 0;

    /// <summary>Shows a dark dialog with one button per option; returns the clicked index, or -1 if dismissed.</summary>
    public static int Choose(IWin32Window? owner, string message, string title, params string[] options)
    {
        int result = -1;
        using var f = new BoxForm();
        Theme.Init(f);
        f.Text = title;
        f.FormBorderStyle = FormBorderStyle.FixedDialog;
        f.MaximizeBox = f.MinimizeBox = false;
        f.ShowInTaskbar = false;
        f.StartPosition = owner == null ? FormStartPosition.CenterScreen : FormStartPosition.CenterParent;
        f.AutoSize = true;
        f.AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var root = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Padding = new Padding(f.S(22)), Dock = DockStyle.Fill };
        var label = Theme.Label(message);
        label.MaximumSize = new Size(f.S(380), 0);
        label.Margin = new Padding(0, 0, 0, f.S(20));

        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, Margin = Padding.Empty };
        ModernButton? first = null;
        // Right-to-left flow: add last option first so they read left-to-right as given.
        for (int i = options.Length - 1; i >= 0; i--)
        {
            int index = i;
            var b = new ModernButton
            {
                Text = options[i],
                Style = i == 0 ? ModernButton.Kind.Primary : ModernButton.Kind.Normal,
                Margin = new Padding(8, 0, 0, 0),
            };
            b.Click += (_, _) => { result = index; f.DialogResult = DialogResult.OK; };
            buttons.Controls.Add(b);
            if (i == 0) first = b;
        }
        root.Controls.Add(label);
        root.Controls.Add(buttons);
        f.Controls.Add(root);
        f.Shown += (_, _) => first?.Focus();
        f.ShowDialog(owner);
        return result;
    }

    class BoxForm : Form
    {
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { DialogResult = DialogResult.Cancel; return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
