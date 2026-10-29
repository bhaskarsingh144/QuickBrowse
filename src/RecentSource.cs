using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

internal sealed class RecentEntry
{
    public string Path;
    public string Title;
    public bool IsDirectory;
    public string LnkPath;
    public DateTime LastAccessUtc;
}

/// <summary>
/// Reads Windows Recent Items (FOLDERID_Recent .lnk shortcuts). Does not write into that folder
/// except via SHAddToRecentDocs when QuickBrowse opens something.
/// </summary>
internal static class RecentSource
{
    public const int DefaultCap = 24;

    private const uint SHARD_PATHW = 0x00000003;
    private const uint STGM_READ = 0x00000000;
    private const uint SLGP_RAWPATH = 0x4;

    public static IList<RecentEntry> Load(int max)
    {
        if (max < 1)
            max = DefaultCap;
        string folder = null;
        try
        {
            folder = Environment.GetFolderPath(Environment.SpecialFolder.Recent);
        }
        catch
        {
            folder = null;
        }
        return LoadFromFolder(folder, max);
    }

    public static IList<RecentEntry> LoadFromFolder(string recentFolder, int max)
    {
        List<RecentEntry> raw = new List<RecentEntry>();
        if (string.IsNullOrEmpty(recentFolder) || !Directory.Exists(recentFolder))
            return BuildList(raw, max);

        string[] links;
        try
        {
            links = Directory.GetFiles(recentFolder, "*.lnk");
        }
        catch
        {
            return BuildList(raw, max);
        }

        for (int i = 0; i < links.Length; i++)
        {
            string lnk = links[i];
            DateTime stamp = DateTime.MinValue;
            try { stamp = File.GetLastWriteTimeUtc(lnk); }
            catch { continue; }

            string target = ResolveShortcutTarget(lnk);
            if (string.IsNullOrEmpty(target))
                continue;

            bool isDir = false;
            bool isFile = false;
            try
            {
                isDir = Directory.Exists(target);
                isFile = !isDir && File.Exists(target);
            }
            catch
            {
                continue;
            }
            if (!isDir && !isFile)
                continue;

            raw.Add(new RecentEntry
            {
                Path = target,
                Title = PinStore.TitleFromPath(target),
                IsDirectory = isDir,
                LnkPath = lnk,
                LastAccessUtc = stamp
            });
        }

        return BuildList(raw, max);
    }

    /// <summary>
    /// Pure filter/dedupe/sort/cap for tests and LoadFromFolder.
    /// Drops .exe targets; keeps first occurrence after newest-first sort.
    /// </summary>
    public static IList<RecentEntry> BuildList(IList<RecentEntry> raw, int max)
    {
        List<RecentEntry> result = new List<RecentEntry>();
        if (raw == null || raw.Count == 0)
            return result;
        if (max < 1)
            max = DefaultCap;

        List<RecentEntry> sorted = new List<RecentEntry>(raw.Count);
        for (int i = 0; i < raw.Count; i++)
        {
            RecentEntry e = raw[i];
            if (e == null || string.IsNullOrEmpty(e.Path))
                continue;
            if (IsExecutable(e.Path))
                continue;
            sorted.Add(e);
        }

        sorted.Sort(CompareNewestFirst);

        HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < sorted.Count && result.Count < max; i++)
        {
            RecentEntry e = sorted[i];
            string key = PinStore.Normalize(e.Path);
            if (key == null)
                continue;
            if (!seen.Add(key))
                continue;
            result.Add(new RecentEntry
            {
                Path = key,
                Title = string.IsNullOrEmpty(e.Title) ? PinStore.TitleFromPath(key) : e.Title,
                IsDirectory = e.IsDirectory,
                LnkPath = e.LnkPath,
                LastAccessUtc = e.LastAccessUtc
            });
        }
        return result;
    }

    public static bool IsExecutable(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;
        try
        {
            string ext = Path.GetExtension(path);
            return string.Equals(ext, ".exe", StringComparison.OrdinalIgnoreCase)
                || string.Equals(ext, ".com", StringComparison.OrdinalIgnoreCase)
                || string.Equals(ext, ".bat", StringComparison.OrdinalIgnoreCase)
                || string.Equals(ext, ".cmd", StringComparison.OrdinalIgnoreCase)
                || string.Equals(ext, ".msi", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static void NotifyAccess(string path)
    {
        string norm = PinStore.Normalize(path);
        if (norm == null || IsExecutable(norm))
            return;
        try
        {
            if (!File.Exists(norm) && !Directory.Exists(norm))
                return;
            SHAddToRecentDocs(SHARD_PATHW, norm);
        }
        catch
        {
        }
    }

    public static IList<PinEntry> ToPinEntries(IList<RecentEntry> recent)
    {
        List<PinEntry> list = new List<PinEntry>();
        if (recent == null)
            return list;
        for (int i = 0; i < recent.Count; i++)
        {
            RecentEntry e = recent[i];
            if (e == null)
                continue;
            list.Add(new PinEntry
            {
                Path = e.Path,
                Title = e.Title,
                IsDirectory = e.IsDirectory
            });
        }
        return list;
    }

    private static int CompareNewestFirst(RecentEntry a, RecentEntry b)
    {
        int c = b.LastAccessUtc.CompareTo(a.LastAccessUtc);
        if (c != 0)
            return c;
        return string.Compare(a.Path, b.Path, StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveShortcutTarget(string lnkPath)
    {
        if (string.IsNullOrEmpty(lnkPath))
            return null;
        IShellLinkW link = null;
        IPersistFile persist = null;
        try
        {
            link = (IShellLinkW)new ShellLinkCoClass();
            persist = (IPersistFile)link;
            persist.Load(lnkPath, STGM_READ);
            StringBuilder sb = new StringBuilder(260);
            link.GetPath(sb, sb.Capacity, IntPtr.Zero, SLGP_RAWPATH);
            string path = sb.ToString();
            if (string.IsNullOrEmpty(path))
            {
                sb.Length = 0;
                link.GetPath(sb, sb.Capacity, IntPtr.Zero, 0);
                path = sb.ToString();
            }
            return string.IsNullOrEmpty(path) ? null : path;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (persist != null)
            {
                try { Marshal.ReleaseComObject(persist); }
                catch { }
            }
            if (link != null)
            {
                try { Marshal.ReleaseComObject(link); }
                catch { }
            }
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern void SHAddToRecentDocs(uint uFlags, string pv);

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLinkCoClass
    {
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch, IntPtr pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cch, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
        void Resolve(IntPtr hwnd, int fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("0000010b-0000-0000-C000-000000000046")]
    private interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        [PreserveSig]
        int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
    }
}
