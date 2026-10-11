using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace NetSet;

/// <summary>Palette, fonts and window chrome for the dark UI.</summary>
public static class Theme
{
    public static readonly Color Back = Color.FromArgb(24, 24, 27);
    public static readonly Color Surface = Color.FromArgb(34, 34, 39);
    public static readonly Color Surface2 = Color.FromArgb(46, 46, 53);
    public static readonly Color Hover = Color.FromArgb(58, 58, 67);
    public static readonly Color Border = Color.FromArgb(63, 63, 72);
    public static readonly Color Divider = Color.FromArgb(44, 44, 51);
    public static readonly Color Text = Color.FromArgb(244, 244, 245);
    public static readonly Color TextDim = Color.FromArgb(150, 150, 162);
    public static readonly Color TextFaint = Color.FromArgb(95, 95, 106);
    public static readonly Color Accent = Color.FromArgb(59, 130, 246);
    public static readonly Color AccentHover = Color.FromArgb(96, 150, 248);
    public static readonly Color AccentDown = Color.FromArgb(37, 99, 235);
    public static readonly Color SelRow = Color.FromArgb(36, 52, 88);
    public static readonly Color RowHover = Color.FromArgb(42, 42, 49);
    public static readonly Color Danger = Color.FromArgb(248, 113, 113);
    public static readonly Color Good = Color.FromArgb(74, 222, 128);
    public static readonly Color Gold = Color.FromArgb(250, 204, 21);

    public static readonly Font Ui = new("Segoe UI", 9.5f);
    public static readonly Font UiMenu = new("Segoe UI", 10f);
    public static readonly Font UiSmallBold = new("Segoe UI Semibold", 8f);
    public static readonly Font UiBold = new("Segoe UI Semibold", 9.5f);
    public static readonly Font Symbol = new("Segoe UI Symbol", 11f);

    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] public static extern int SetWindowTheme(IntPtr hwnd, string? subApp, string? subIdList);

    /// <summary>Scale a 96-dpi pixel value to the control's DPI.</summary>
    public static int S(this Control c, int v) => (int)Math.Round(v * c.DeviceDpi / 96.0);

    static int Bgr(Color c) => c.R | (c.G << 8) | (c.B << 16);

    public static void Init(Form form)
    {
        form.AutoScaleMode = AutoScaleMode.None;
        form.BackColor = Back;
        form.ForeColor = Text;
        form.Font = Ui;
        form.HandleCreated += (_, _) =>
        {
            int on = 1, cap = Bgr(Back), border = Bgr(Border), txt = Bgr(Text);
            DwmSetWindowAttribute(form.Handle, 20, ref on, 4);     // dark title bar
            DwmSetWindowAttribute(form.Handle, 35, ref cap, 4);    // caption colour (Win11)
            DwmSetWindowAttribute(form.Handle, 34, ref border, 4); // window border (Win11)
            DwmSetWindowAttribute(form.Handle, 36, ref txt, 4);    // caption text (Win11)
        };
    }

    public static Label Label(string text, bool dim = false, bool small = false) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = dim ? TextDim : Text,
        Font = small ? UiSmallBold : Ui,
        BackColor = Color.Transparent,
    };

    public static GraphicsPath RoundRect(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        float d = radius * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static void StyleMenu(ToolStripDropDownMenu menu)
    {
        menu.Renderer = MenuRenderer;
        menu.Font = UiMenu;
        menu.ShowImageMargin = false;
        menu.ShowCheckMargin = true;
        menu.Padding = new Padding(0, 4, 0, 4);
    }

    public static ToolStripRenderer MenuRenderer { get; } = new DarkRenderer();

    class DarkRenderer : ToolStripProfessionalRenderer
    {
        public DarkRenderer() { RoundedEdges = false; }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using var b = new SolidBrush(Surface);
            e.Graphics.FillRectangle(b, e.AffectedBounds);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using var pen = new Pen(Border);
            e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Enabled || !e.Item.Selected) return;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = RoundRect(new RectangleF(5, 1, e.Item.Width - 10, e.Item.Height - 2), 5);
            using var b = new SolidBrush(Hover);
            g.FillPath(b, path);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            using var pen = new Pen(Border);
            int y = e.Item.Height / 2;
            e.Graphics.DrawLine(pen, 10, y, e.Item.Width - 10, y);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Text : TextDim;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = TextDim;
            base.OnRenderArrow(e);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            TextRenderer.DrawText(e.Graphics, "✓", Symbol, e.ImageRectangle, Accent,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }
}
