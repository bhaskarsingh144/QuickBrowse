using System;
using System.Drawing;
using System.Drawing.Drawing2D;

/// <summary>
/// Small colored toolbar glyphs (vector-ish) for chrome chips.
/// </summary>
internal static class ChromeIcons
{
    public static readonly Color CopyInk = Color.FromArgb(255, 0x60, 0xCD, 0xFF);
    public static readonly Color DeleteInk = Color.FromArgb(255, 0xFF, 0x6B, 0x6B);
    public static readonly Color ExplorerInk = Color.FromArgb(255, 0xF2, 0xC4, 0x6D);
    public static readonly Color ListInk = Color.FromArgb(255, 0xC5, 0xCB, 0xD6);
    public static readonly Color TilesInk = Color.FromArgb(255, 0x9B, 0xD4, 0xFF);
    public static readonly Color IconsInk = Color.FromArgb(255, 0x8E, 0xC8, 0xFF);
    public static readonly Color ContentInk = Color.FromArgb(255, 0xA8, 0xD5, 0xA2);
    public static readonly Color DetailsInk = Color.FromArgb(255, 0xD0, 0xD4, 0xDC);
    public static readonly Color NavInk = Color.FromArgb(255, 0xE8, 0xEB, 0xF0);
    public static readonly Color CloseInk = Color.FromArgb(255, 0xB8, 0xBE, 0xC8);

    public static Bitmap Make(int size, Action<Graphics, int> draw)
    {
        if (size < 12) size = 12;
        Bitmap bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            draw(g, size);
        }
        return bmp;
    }

    public static Bitmap Back(int size)
    {
        return Make(size, delegate(Graphics g, int s)
        {
            using (Pen pen = new Pen(NavInk, Math.Max(1.5f, s / 10f)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                float m = s * 0.22f;
                float cx = s * 0.55f;
                float cy = s * 0.5f;
                g.DrawLines(pen, new PointF[]
                {
                    new PointF(cx + s * 0.12f, m),
                    new PointF(m + s * 0.08f, cy),
                    new PointF(cx + s * 0.12f, s - m)
                });
            }
        });
    }

    public static Bitmap Up(int size)
    {
        return Make(size, delegate(Graphics g, int s)
        {
            using (Pen pen = new Pen(NavInk, Math.Max(1.5f, s / 10f)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                float m = s * 0.22f;
                float cx = s * 0.5f;
                float cy = s * 0.55f;
                g.DrawLines(pen, new PointF[]
                {
                    new PointF(m, cy + s * 0.12f),
                    new PointF(cx, m + s * 0.08f),
                    new PointF(s - m, cy + s * 0.12f)
                });
            }
        });
    }

    public static Bitmap Copy(int size)
    {
        return Make(size, delegate(Graphics g, int s)
        {
            float u = s / 16f;
            RectangleF back = new RectangleF(3.5f * u, 2.5f * u, 8f * u, 9.5f * u);
            RectangleF front = new RectangleF(5.5f * u, 4.5f * u, 8f * u, 9.5f * u);
            using (Pen pen = new Pen(CopyInk, Math.Max(1.4f, 1.4f * u)))
            {
                g.DrawRectangle(pen, back.X, back.Y, back.Width, back.Height);
                using (SolidBrush fill = new SolidBrush(Color.FromArgb(40, CopyInk)))
                    g.FillRectangle(fill, front);
                g.DrawRectangle(pen, front.X, front.Y, front.Width, front.Height);
            }
        });
    }

    public static Bitmap Delete(int size)
    {
        return Make(size, delegate(Graphics g, int s)
        {
            float u = s / 16f;
            using (Pen pen = new Pen(DeleteInk, Math.Max(1.4f, 1.5f * u)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                // lid
                g.DrawLine(pen, 4f * u, 5f * u, 12f * u, 5f * u);
                g.DrawLine(pen, 6.5f * u, 3.5f * u, 9.5f * u, 3.5f * u);
                // can
                g.DrawLine(pen, 5f * u, 5f * u, 5.8f * u, 13f * u);
                g.DrawLine(pen, 11f * u, 5f * u, 10.2f * u, 13f * u);
                g.DrawLine(pen, 5.8f * u, 13f * u, 10.2f * u, 13f * u);
                g.DrawLine(pen, 7.5f * u, 6.5f * u, 7.5f * u, 11.5f * u);
                g.DrawLine(pen, 9.2f * u, 6.5f * u, 9.2f * u, 11.5f * u);
            }
        });
    }

    public static Bitmap Explorer(int size)
    {
        return Make(size, delegate(Graphics g, int s)
        {
            float u = s / 16f;
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(70, ExplorerInk)))
            using (Pen pen = new Pen(ExplorerInk, Math.Max(1.3f, 1.4f * u)))
            {
                // tab
                g.FillRectangle(fill, 3f * u, 4f * u, 5f * u, 2.2f * u);
                g.DrawRectangle(pen, 3f * u, 4f * u, 5f * u, 2.2f * u);
                // body
                RectangleF body = new RectangleF(3f * u, 6f * u, 10f * u, 7f * u);
                g.FillRectangle(fill, body);
                g.DrawRectangle(pen, body.X, body.Y, body.Width, body.Height);
            }
        });
    }

    public static Bitmap IconsView(int size)
    {
        return Make(size, delegate(Graphics g, int s)
        {
            float u = s / 16f;
            using (Pen pen = new Pen(IconsInk, Math.Max(1.3f, 1.3f * u)))
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(55, IconsInk)))
            {
                RectangleF a = new RectangleF(3.2f * u, 3.2f * u, 4.2f * u, 4.2f * u);
                RectangleF b = new RectangleF(8.6f * u, 3.2f * u, 4.2f * u, 4.2f * u);
                RectangleF c = new RectangleF(3.2f * u, 8.6f * u, 4.2f * u, 4.2f * u);
                RectangleF d = new RectangleF(8.6f * u, 8.6f * u, 4.2f * u, 4.2f * u);
                g.FillRectangle(fill, a); g.DrawRectangle(pen, a.X, a.Y, a.Width, a.Height);
                g.FillRectangle(fill, b); g.DrawRectangle(pen, b.X, b.Y, b.Width, b.Height);
                g.FillRectangle(fill, c); g.DrawRectangle(pen, c.X, c.Y, c.Width, c.Height);
                g.FillRectangle(fill, d); g.DrawRectangle(pen, d.X, d.Y, d.Width, d.Height);
            }
        });
    }

    public static Bitmap TilesView(int size)
    {
        return Make(size, delegate(Graphics g, int s)
        {
            float u = s / 16f;
            using (Pen pen = new Pen(TilesInk, Math.Max(1.3f, 1.3f * u)))
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(55, TilesInk)))
            {
                RectangleF icon = new RectangleF(3f * u, 3.5f * u, 4.5f * u, 4.5f * u);
                g.FillRectangle(fill, icon);
                g.DrawRectangle(pen, icon.X, icon.Y, icon.Width, icon.Height);
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawLine(pen, 9f * u, 4.5f * u, 13f * u, 4.5f * u);
                g.DrawLine(pen, 9f * u, 7f * u, 12f * u, 7f * u);
                g.DrawLine(pen, 9f * u, 10.5f * u, 13f * u, 10.5f * u);
                g.DrawLine(pen, 9f * u, 12.5f * u, 11.5f * u, 12.5f * u);
            }
        });
    }

    public static Bitmap ContentView(int size)
    {
        return Make(size, delegate(Graphics g, int s)
        {
            float u = s / 16f;
            using (Pen pen = new Pen(ContentInk, Math.Max(1.3f, 1.3f * u)))
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(55, ContentInk)))
            {
                RectangleF icon = new RectangleF(3f * u, 4f * u, 5f * u, 8f * u);
                g.FillRectangle(fill, icon);
                g.DrawRectangle(pen, icon.X, icon.Y, icon.Width, icon.Height);
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawLine(pen, 9.5f * u, 5.5f * u, 13f * u, 5.5f * u);
                g.DrawLine(pen, 9.5f * u, 8.5f * u, 12.5f * u, 8.5f * u);
                g.DrawLine(pen, 9.5f * u, 11.5f * u, 13f * u, 11.5f * u);
            }
        });
    }

    public static Bitmap ListView(int size)
    {
        return Make(size, delegate(Graphics g, int s)
        {
            float u = s / 16f;
            using (Pen pen = new Pen(ListInk, Math.Max(1.4f, 1.5f * u)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                for (int i = 0; i < 3; i++)
                {
                    float y = (4.5f + i * 3.2f) * u;
                    g.DrawLine(pen, 3.5f * u, y, 5.2f * u, y);
                    g.DrawLine(pen, 6.5f * u, y, 12.5f * u, y);
                }
            }
        });
    }

    public static Bitmap DetailsView(int size)
    {
        return Make(size, delegate(Graphics g, int s)
        {
            float u = s / 16f;
            using (Pen pen = new Pen(DetailsInk, Math.Max(1.3f, 1.35f * u)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawLine(pen, 3f * u, 3.5f * u, 13f * u, 3.5f * u);
                for (int i = 0; i < 3; i++)
                {
                    float y = (6.2f + i * 2.8f) * u;
                    g.DrawLine(pen, 3.5f * u, y, 7.2f * u, y);
                    g.DrawLine(pen, 8.2f * u, y, 10.2f * u, y);
                    g.DrawLine(pen, 11f * u, y, 13f * u, y);
                }
            }
        });
    }

    public static Bitmap Close(int size)
    {
        return Make(size, delegate(Graphics g, int s)
        {
            using (Pen pen = new Pen(CloseInk, Math.Max(1.5f, s / 10f)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                float m = s * 0.28f;
                g.DrawLine(pen, m, m, s - m, s - m);
                g.DrawLine(pen, s - m, m, m, s - m);
            }
        });
    }

    public static readonly Color MoreInk = Color.FromArgb(255, 0xC5, 0xCB, 0xD6);
    public static readonly Color PinInk = Color.FromArgb(255, 0xF2, 0xC4, 0x6D);
    public static readonly Color RecentInk = Color.FromArgb(255, 0x9B, 0xD4, 0xFF);

    public static Bitmap More(int size)
    {
        return Make(size, delegate(Graphics g, int s)
        {
            float u = s / 16f;
            float r = Math.Max(1.2f, 1.4f * u);
            using (SolidBrush brush = new SolidBrush(MoreInk))
            {
                g.FillEllipse(brush, 3.2f * u, 7f * u, r * 2, r * 2);
                g.FillEllipse(brush, 7f * u, 7f * u, r * 2, r * 2);
                g.FillEllipse(brush, 10.8f * u, 7f * u, r * 2, r * 2);
            }
        });
    }

    public static Bitmap Pin(int size)
    {
        return Make(size, delegate(Graphics g, int s)
        {
            float u = s / 16f;
            using (Pen pen = new Pen(PinInk, Math.Max(1.4f, 1.45f * u)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                // pin head
                g.DrawEllipse(pen, 5.5f * u, 2.5f * u, 5f * u, 5f * u);
                // stem
                g.DrawLine(pen, 8f * u, 7.5f * u, 8f * u, 13.5f * u);
            }
        });
    }

    /// <summary>Clock / history glyph for the Recent rail chip.</summary>
    public static Bitmap Recent(int size)
    {
        return Make(size, delegate(Graphics g, int s)
        {
            float u = s / 16f;
            using (Pen pen = new Pen(RecentInk, Math.Max(1.4f, 1.45f * u)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawEllipse(pen, 3.2f * u, 3.2f * u, 9.6f * u, 9.6f * u);
                g.DrawLine(pen, 8f * u, 5.2f * u, 8f * u, 8.2f * u);
                g.DrawLine(pen, 8f * u, 8.2f * u, 10.4f * u, 9.6f * u);
            }
        });
    }
}
