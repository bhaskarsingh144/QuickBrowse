using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;

/// <summary>
/// Shell icons via SHGetFileInfo. Callers must not Dispose returned images from Get.
/// </summary>
internal static class IconCache
{
    private const uint SHGFI_ICON = 0x000000100;
    private const uint SHGFI_SMALLICON = 0x000000001;
    private const uint SHGFI_LARGEICON = 0x000000000;
    private const uint SHGFI_SYSICONINDEX = 0x000004000;
    private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
    private const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x80;

    private const int SHIL_SMALL = 0x1;
    private const int SHIL_EXTRALARGE = 0x2;
    private const int SHIL_JUMBO = 0x4;
    private const int ILD_TRANSPARENT = 0x1;
    private const uint SIID_DESKTOPPC = 67;
    private const int ThisPcImageresIndex = -109;

    private const string ThisPcParsingName = "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}";

    private const int MaxEntries = 256;
    private static readonly Dictionary<string, Bitmap> Map = new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);
    private static readonly LinkedList<string> Lru = new LinkedList<string>();

    public static Bitmap GetForPath(string path, bool isDirectory, bool large)
    {
        string key = (large ? "L|" : "S|") + (isDirectory ? "D|" : "F|") + (path ?? "");
        lock (Map)
        {
            Bitmap hit;
            if (Map.TryGetValue(key, out hit) && hit != null)
            {
                Touch(key);
                return hit;
            }
        }

        Bitmap loaded = Load(path, isDirectory, large);
        if (loaded == null)
            return null;

        lock (Map)
        {
            Bitmap existing;
            if (Map.TryGetValue(key, out existing) && existing != null)
            {
                loaded.Dispose();
                Touch(key);
                return existing;
            }
            EvictIfNeeded();
            Map[key] = loaded;
            Lru.AddFirst(key);
            return loaded;
        }
    }

    public static Bitmap GetOwned(string path, bool isDirectory, bool large)
    {
        Bitmap shared = GetForPath(path, isDirectory, large);
        if (shared == null)
            return null;
        try
        {
            return new Bitmap(shared);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Center <paramref name="src"/> on a transparent canvas so ImageList / LargeIcon
    /// cells do not pin the glyph to the top-left (shell icons are often 32×32 in a larger slot).
    /// Disposes <paramref name="src"/>.
    /// </summary>
    public static Bitmap FitCentered(Bitmap src, int canvasW, int canvasH)
    {
        if (canvasW < 1) canvasW = 1;
        if (canvasH < 1) canvasH = 1;
        Bitmap canvas = new Bitmap(canvasW, canvasH, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        if (src == null)
            return canvas;
        try
        {
            using (Graphics g = Graphics.FromImage(canvas))
            {
                g.Clear(Color.Transparent);
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

                // Keep aspect; leave a little margin so labels don't collide with glyph edges.
                int maxW = Math.Max(1, (int)(canvasW * 0.82));
                int maxH = Math.Max(1, (int)(canvasH * 0.82));
                float scale = Math.Min((float)maxW / src.Width, (float)maxH / src.Height);
                if (scale > 1f)
                    scale = 1f; // never upscale fuzzy shell icons
                int dw = Math.Max(1, (int)Math.Round(src.Width * scale));
                int dh = Math.Max(1, (int)Math.Round(src.Height * scale));
                int x = (canvasW - dw) / 2;
                int y = (canvasH - dh) / 2;
                g.DrawImage(src, new Rectangle(x, y, dw, dh));
            }
        }
        finally
        {
            try { src.Dispose(); }
            catch { }
        }
        return canvas;
    }

    /// <summary>
    /// Exact Explorer / Start-menu "This PC" glyph (blue desktop PC).
    /// Prefer imageres.dll,-109, then SIID_DESKTOPPC — never USEFILEATTRIBUTES on the
    /// CLSID path (that falsely returns a generic folder icon).
    /// </summary>
    public static Bitmap GetThisPc(bool large)
    {
        string key = large ? "STOCK|THISPC|XL" : "STOCK|THISPC|SM";
        lock (Map)
        {
            Bitmap hit;
            if (Map.TryGetValue(key, out hit) && hit != null)
            {
                Touch(key);
                return hit;
            }
        }

        Bitmap loaded = LoadFromImageres(ThisPcImageresIndex, large);
        if (loaded == null)
            loaded = LoadStock(SIID_DESKTOPPC, large);
        if (loaded == null)
            loaded = LoadThisPcFromShell(large);
        if (loaded == null)
            return null;

        lock (Map)
        {
            Bitmap existing;
            if (Map.TryGetValue(key, out existing) && existing != null)
            {
                loaded.Dispose();
                Touch(key);
                return existing;
            }
            EvictIfNeeded();
            Map[key] = loaded;
            Lru.AddFirst(key);
            return loaded;
        }
    }

    private static Bitmap LoadFromImageres(int index, bool large)
    {
        try
        {
            string path = Path.Combine(Environment.SystemDirectory, "imageres.dll");
            if (!File.Exists(path))
                return null;
            // Resource id (e.g. -109) — SHDefExtractIcon, not ExtractIconEx.
            uint cx = large ? 48u : 16u;
            uint sizePack = cx | (16u << 16);
            IntPtr largeIcon = IntPtr.Zero;
            IntPtr smallIcon = IntPtr.Zero;
            int hr = SHDefExtractIcon(path, index, 0, out largeIcon, out smallIcon, sizePack);
            if (hr != 0)
                return null;
            IntPtr use = large ? largeIcon : smallIcon;
            IntPtr other = large ? smallIcon : largeIcon;
            try
            {
                if (other != IntPtr.Zero && other != use)
                    DestroyIcon(other);
                if (use == IntPtr.Zero)
                    return null;
                using (Icon icon = Icon.FromHandle(use))
                    return icon.ToBitmap();
            }
            finally
            {
                if (use != IntPtr.Zero)
                    DestroyIcon(use);
            }
        }
        catch
        {
            return null;
        }
    }

    private static Bitmap LoadThisPcFromShell(bool large)
    {
        try
        {
            // Do NOT pass SHGFI_USEFILEATTRIBUTES + DIRECTORY — that yields a folder glyph.
            SHFILEINFO info = new SHFILEINFO();
            uint flags = SHGFI_SYSICONINDEX;
            IntPtr result = SHGetFileInfo(
                ThisPcParsingName,
                0,
                ref info,
                (uint)Marshal.SizeOf(typeof(SHFILEINFO)),
                flags);
            if (result == IntPtr.Zero || info.iIcon < 0)
                return null;

            int shil = large ? SHIL_JUMBO : SHIL_SMALL;
            Guid iid = new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950"); // IID_IImageList
            IImageList iml;
            int hr = SHGetImageList(shil, ref iid, out iml);
            if (hr != 0 || iml == null)
            {
                shil = large ? SHIL_EXTRALARGE : SHIL_SMALL;
                hr = SHGetImageList(shil, ref iid, out iml);
            }
            if (hr != 0 || iml == null)
                return null;

            IntPtr hIcon = IntPtr.Zero;
            hr = iml.GetIcon(info.iIcon, ILD_TRANSPARENT, ref hIcon);
            if (hr != 0 || hIcon == IntPtr.Zero)
                return null;
            try
            {
                using (Icon icon = Icon.FromHandle(hIcon))
                    return icon.ToBitmap();
            }
            finally
            {
                DestroyIcon(hIcon);
            }
        }
        catch
        {
            return null;
        }
    }

    private static Bitmap LoadStock(uint siid, bool large)
    {
        try
        {
            SHSTOCKICONINFO info = new SHSTOCKICONINFO();
            info.cbSize = (uint)Marshal.SizeOf(typeof(SHSTOCKICONINFO));
            uint flags = 0x000000100; // SHGSI_ICON
            flags |= large ? 0u : 0x000000001u; // SHGSI_SMALLICON
            int hr = SHGetStockIconInfo(siid, flags, ref info);
            if (hr != 0 || info.hIcon == IntPtr.Zero)
                return null;
            try
            {
                using (Icon icon = Icon.FromHandle(info.hIcon))
                    return icon.ToBitmap();
            }
            finally
            {
                DestroyIcon(info.hIcon);
            }
        }
        catch
        {
            return null;
        }
    }

    public static void Clear()
    {
        lock (Map)
        {
            foreach (KeyValuePair<string, Bitmap> kv in Map)
            {
                if (kv.Value != null)
                {
                    try { kv.Value.Dispose(); }
                    catch { }
                }
            }
            Map.Clear();
            Lru.Clear();
        }
    }

    private static Bitmap Load(string path, bool isDirectory, bool large)
    {
        try
        {
            SHFILEINFO info = new SHFILEINFO();
            uint flags = SHGFI_ICON | (large ? SHGFI_LARGEICON : SHGFI_SMALLICON);
            IntPtr result;
            if (string.IsNullOrEmpty(path) || (!isDirectory && !System.IO.File.Exists(path) && !System.IO.Directory.Exists(path)))
            {
                flags |= SHGFI_USEFILEATTRIBUTES;
                uint attrs = isDirectory ? FILE_ATTRIBUTE_DIRECTORY : FILE_ATTRIBUTE_NORMAL;
                result = SHGetFileInfo(path ?? "folder", attrs, ref info, (uint)Marshal.SizeOf(typeof(SHFILEINFO)), flags);
            }
            else
            {
                result = SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf(typeof(SHFILEINFO)), flags);
            }
            if (result == IntPtr.Zero || info.hIcon == IntPtr.Zero)
                return null;
            try
            {
                using (Icon icon = Icon.FromHandle(info.hIcon))
                {
                    return icon.ToBitmap();
                }
            }
            finally
            {
                DestroyIcon(info.hIcon);
            }
        }
        catch
        {
            return null;
        }
    }

    private static void Touch(string key)
    {
        LinkedListNode<string> node = Lru.Find(key);
        if (node != null)
        {
            Lru.Remove(node);
            Lru.AddFirst(node);
        }
    }

    private static void EvictIfNeeded()
    {
        while (Map.Count >= MaxEntries && Lru.Count > 0)
        {
            string old = Lru.Last.Value;
            Lru.RemoveLast();
            Bitmap bmp;
            if (Map.TryGetValue(old, out bmp))
            {
                Map.Remove(old);
                if (bmp != null)
                {
                    try { bmp.Dispose(); }
                    catch { }
                }
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHSTOCKICONINFO
    {
        public uint cbSize;
        public IntPtr hIcon;
        public int iSysImageIndex;
        public int iIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szPath;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHGetStockIconInfo(uint siid, uint uFlags, ref SHSTOCKICONINFO psii);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHDefExtractIcon(string pszIconFile, int iIndex, uint uFlags, out IntPtr phiconLarge, out IntPtr phiconSmall, uint nIconSize);

    [DllImport("shell32.dll", EntryPoint = "#727")]
    private static extern int SHGetImageList(int iImageList, ref Guid riid, out IImageList ppv);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [ComImport]
    [Guid("46EB5926-582E-4017-9FDF-E8998DAA0950")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IImageList
    {
        [PreserveSig] int Add(IntPtr hbmImage, IntPtr hbmMask, ref int pi);
        [PreserveSig] int ReplaceIcon(int i, IntPtr hicon, ref int pi);
        [PreserveSig] int SetOverlayImage(int iImage, int iOverlay);
        [PreserveSig] int Replace(int i, IntPtr hbmImage, IntPtr hbmMask);
        [PreserveSig] int AddMasked(IntPtr hbmImage, int crMask, ref int pi);
        [PreserveSig] int Draw(IntPtr pimldp);
        [PreserveSig] int Remove(int i);
        [PreserveSig] int GetIcon(int i, int flags, ref IntPtr picon);
    }
}
