using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace NetSet;

/// <summary>
/// Draws a Windows 95-style "networked computers" icon: two beige CRT monitors
/// with teal screens, joined by a cable. Hard pixels, no anti-aliasing.
/// </summary>
public static class IconFactory
{
    static readonly Color Face = Color.FromArgb(192, 192, 192);
    static readonly Color Hi = Color.White;
    static readonly Color Shadow = Color.FromArgb(128, 128, 128);
    static readonly Color Screen = Color.FromArgb(0, 128, 128);
    static readonly Color Glint = Color.FromArgb(128, 255, 255);

    public static Bitmap Draw(int size)
    {
        // Hand-drawn at 16 and 32; other sizes are nearest-neighbour scaled from those.
        int basis = size <= 16 ? 16 : 32;
        var bmp = new Bitmap(basis, basis, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            if (basis == 16) Draw16(g); else Draw32(g);
        }
        if (size == basis) return bmp;

        var scaled = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(scaled))
        {
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(bmp, 0, 0, size, size);
        }
        bmp.Dispose();
        return scaled;
    }

    static void Px(Graphics g, Color c, int x, int y, int w = 1, int h = 1)
    {
        using var b = new SolidBrush(c);
        g.FillRectangle(b, x, y, w, h);
    }

    static void Monitor(Graphics g, int x, int y, int w, int h, bool detailed)
    {
        Px(g, Color.Black, x, y, w, h);
        Px(g, Face, x + 1, y + 1, w - 2, h - 2);
        Px(g, Hi, x + 1, y + 1, w - 2, 1);
        Px(g, Hi, x + 1, y + 1, 1, h - 2);
        if (detailed)
        {
            Px(g, Shadow, x + 2, y + h - 2, w - 3, 1);
            Px(g, Shadow, x + w - 2, y + 2, 1, h - 3);
        }

        int inset = detailed ? 3 : 2;
        int sx = x + inset, sy = y + inset, sw = w - inset * 2, sh = h - inset * 2 - (detailed ? 1 : 0);
        Px(g, Shadow, sx - 1, sy - 1, sw + 2, sh + 2);      // recessed bezel
        Px(g, Screen, sx, sy, sw, sh);
        Px(g, Glint, sx + 1, sy + 1, detailed ? 3 : 2, 1);   // glass reflection
        if (detailed) Px(g, Glint, sx + 1, sy + 2, 1, 1);
    }

    static void Stand(Graphics g, int cx, int y, int footW, bool detailed)
    {
        Px(g, Shadow, cx - 1, y, 3, 1);
        int fx = cx - footW / 2;
        Px(g, Color.Black, fx, y + 1, footW, detailed ? 3 : 2);
        Px(g, Face, fx + 1, y + 1, footW - 2, 1);
        if (detailed) Px(g, Shadow, fx + 1, y + 2, footW - 2, 1);
    }

    static void Draw32(Graphics g)
    {
        // Cable first so the machines sit on top of it.
        Px(g, Color.Black, 8, 19, 1, 11);
        Px(g, Color.Black, 8, 30, 16, 1);
        Px(g, Color.Black, 23, 28, 1, 3);

        Monitor(g, 0, 1, 17, 14, true);   // back machine
        Stand(g, 8, 15, 9, true);

        Monitor(g, 15, 10, 17, 14, true); // front machine
        Stand(g, 23, 24, 9, true);
    }

    static void Draw16(Graphics g)
    {
        Px(g, Color.Black, 4, 10, 1, 5);
        Px(g, Color.Black, 4, 15, 8, 1);

        Monitor(g, 0, 0, 9, 7, false);
        Stand(g, 4, 7, 5, false);

        Monitor(g, 7, 6, 9, 7, false);
        Stand(g, 11, 13, 5, false);
    }

    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);

    public static Icon CreateIcon(int size)
    {
        using var bmp = Draw(size);
        IntPtr h = bmp.GetHicon();
        try { return (Icon)Icon.FromHandle(h).Clone(); }
        finally { DestroyIcon(h); }
    }

    /// <summary>Writes a multi-size .ico (PNG-compressed frames) for the exe icon.</summary>
    public static void ExportIco(string path)
    {
        int[] sizes = { 16, 32, 48, 256 };
        var frames = new List<byte[]>();
        foreach (int s in sizes)
        {
            using var bmp = Draw(s);
            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            frames.Add(ms.ToArray());
        }

        using var fs = File.Create(path);
        using var w = new BinaryWriter(fs);
        w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
        int offset = 6 + 16 * sizes.Length;
        for (int i = 0; i < sizes.Length; i++)
        {
            byte dim = (byte)(sizes[i] >= 256 ? 0 : sizes[i]);
            w.Write(dim); w.Write(dim);
            w.Write((byte)0); w.Write((byte)0);
            w.Write((short)1); w.Write((short)32);
            w.Write(frames[i].Length); w.Write(offset);
            offset += frames[i].Length;
        }
        foreach (var f in frames) w.Write(f);
    }
}
