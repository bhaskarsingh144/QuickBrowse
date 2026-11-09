using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

/// <summary>
/// Resolves AppUserModelIDs for SHAddToRecentDocs(SHARD_APPIDINFO) so QuickBrowse
/// can update Explorer / app taskbar jump lists.
/// </summary>
internal static class AppIdResolver
{
    public const string ExplorerAppId = "Microsoft.Windows.Explorer";

    private static readonly Dictionary<string, string> CuratedByExe =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, string> CacheByExePath =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private static DateTime _shortcutScanUtc;
    private static Dictionary<string, string> _shortcutByExePath;

    private static readonly Guid PkeyFmt = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3");
    private static readonly PROPERTYKEY PkeyAppUserModelId = new PROPERTYKEY { fmtid = PkeyFmt, pid = 5 };
    private static readonly Guid IidShellItem2 = new Guid("7E9FB0D3-919F-4307-AB2E-9B1860310C93");

    static AppIdResolver()
    {
        CuratedByExe["explorer.exe"] = ExplorerAppId;
        CuratedByExe["notepad.exe"] = "Microsoft.Windows.Notepad";
        CuratedByExe["mspaint.exe"] = "Microsoft.Windows.Paint";
        CuratedByExe["Code.exe"] = "Microsoft.VisualStudioCode";
        CuratedByExe["devenv.exe"] = "VisualStudio.2022";
        CuratedByExe["WINWORD.EXE"] = "Microsoft.Office.WINWORD.EXE.15";
        CuratedByExe["EXCEL.EXE"] = "Microsoft.Office.EXCEL.EXE.15";
        CuratedByExe["POWERPNT.EXE"] = "Microsoft.Office.POWERPNT.EXE.15";
        CuratedByExe["AcroRd32.exe"] = "Adobe.Acrobat.Reader";
        CuratedByExe["Acrobat.exe"] = "Adobe.Acrobat";
        CuratedByExe["SumatraPDF.exe"] = "SumatraPDF";
        CuratedByExe["vlc.exe"] = "VLC.VLCMediaPlayer";
        CuratedByExe["PotPlayerMini64.exe"] = "PotPlayer";
        CuratedByExe["PotPlayer64.exe"] = "PotPlayer";
        CuratedByExe["PotPlayerMini.exe"] = "PotPlayer";
        CuratedByExe["PotPlayer.exe"] = "PotPlayer";
        CuratedByExe["mpc-hc64.exe"] = "MediaPlayerClassic.HomeCinema";
        CuratedByExe["mpc-hc.exe"] = "MediaPlayerClassic.HomeCinema";
        CuratedByExe["Photos.exe"] = "Microsoft.Windows.Photos_8wekyb3d8bbwe!App";
        CuratedByExe["msedge.exe"] = "MSEdge";
        CuratedByExe["chrome.exe"] = "Chrome";
        CuratedByExe["notepad++.exe"] = "Notepad++";
        CuratedByExe["sublime_text.exe"] = "SublimeText";
        CuratedByExe["idea64.exe"] = "JetBrains.IntelliJIDEA";
        CuratedByExe["Cursor.exe"] = "Cursor.Cursor";
    }

    /// <summary>Test hook — clear shortcut / path caches.</summary>
    public static void ClearCacheForTests()
    {
        CacheByExePath.Clear();
        _shortcutByExePath = null;
        _shortcutScanUtc = DateTime.MinValue;
    }

    public static string ResolveForExe(string exePath)
    {
        if (string.IsNullOrEmpty(exePath))
            return null;

        string full;
        try { full = Path.GetFullPath(exePath); }
        catch { full = exePath; }

        string cached;
        if (CacheByExePath.TryGetValue(full, out cached))
            return cached;

        string name;
        try { name = Path.GetFileName(full); }
        catch { name = null; }

        string id = null;
        if (!string.IsNullOrEmpty(name) && CuratedByExe.TryGetValue(name, out id))
        {
            CacheByExePath[full] = id;
            return id;
        }

        // Do not scan Start Menu on this hot path (UI notify / Open with). Curated map
        // covers everyday apps; unpackaged apps commonly use the exe path as AppID.
        id = full;
        CacheByExePath[full] = id;
        return id;
    }

    /// <summary>Default association executable for a document path (ASSOCSTR_EXECUTABLE).</summary>
    public static string DefaultHandlerExe(string documentPath)
    {
        if (string.IsNullOrEmpty(documentPath))
            return null;
        string ext;
        try { ext = Path.GetExtension(documentPath); }
        catch { return null; }
        if (string.IsNullOrEmpty(ext) || ext.Length < 2)
            return null;
        return AssocQueryStringExecutable(ext);
    }

    public static string ResolveForDocument(string documentPath)
    {
        string exe = DefaultHandlerExe(documentPath);
        if (string.IsNullOrEmpty(exe))
            return null;
        return ResolveForExe(exe);
    }

    private static string TryFromStartMenuShortcut(string exeFullPath)
    {
        Dictionary<string, string> map = ShortcutIndex();
        if (map == null)
            return null;
        string id;
        if (map.TryGetValue(exeFullPath, out id))
            return id;
        return null;
    }

    private static Dictionary<string, string> ShortcutIndex()
    {
        if (_shortcutByExePath != null && (DateTime.UtcNow - _shortcutScanUtc).TotalMinutes < 10)
            return _shortcutByExePath;

        Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string[] roots = new string[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
        };
        for (int i = 0; i < roots.Length; i++)
        {
            string root = roots[i];
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                continue;
            ScanShortcuts(root, map, 0);
        }
        _shortcutByExePath = map;
        _shortcutScanUtc = DateTime.UtcNow;
        return map;
    }

    private static void ScanShortcuts(string dir, Dictionary<string, string> map, int depth)
    {
        if (depth > 6 || map.Count > 400)
            return;
        string[] links;
        try { links = Directory.GetFiles(dir, "*.lnk"); }
        catch { links = null; }
        if (links != null)
        {
            for (int i = 0; i < links.Length; i++)
            {
                string target;
                string appId;
                if (!TryReadShortcut(links[i], out target, out appId))
                    continue;
                if (string.IsNullOrEmpty(target) || string.IsNullOrEmpty(appId))
                    continue;
                if (!map.ContainsKey(target))
                    map[target] = appId;
            }
        }
        string[] kids;
        try { kids = Directory.GetDirectories(dir); }
        catch { return; }
        for (int i = 0; i < kids.Length && map.Count <= 400; i++)
            ScanShortcuts(kids[i], map, depth + 1);
    }

    /// <summary>Reads target path + AppUserModelID from a .lnk (best-effort).</summary>
    public static bool TryReadShortcut(string lnkPath, out string targetExe, out string appUserModelId)
    {
        targetExe = null;
        appUserModelId = null;
        if (string.IsNullOrEmpty(lnkPath) || !File.Exists(lnkPath))
            return false;

        IShellLinkW link = null;
        IPersistFile persist = null;
        try
        {
            link = (IShellLinkW)new ShellLinkCoClass();
            persist = (IPersistFile)link;
            persist.Load(lnkPath, 0);
            StringBuilder sb = new StringBuilder(260);
            link.GetPath(sb, sb.Capacity, IntPtr.Zero, 0);
            string path = sb.ToString();
            if (string.IsNullOrEmpty(path))
                return false;
            try { targetExe = Path.GetFullPath(path); }
            catch { targetExe = path; }

            appUserModelId = TryReadAppIdFromShellItem(lnkPath);
            return !string.IsNullOrEmpty(targetExe);
        }
        catch
        {
            return false;
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

    private static string TryReadAppIdFromShellItem(string lnkPath)
    {
        IShellItem2 item = null;
        try
        {
            Guid iid = IidShellItem2;
            SHCreateItemFromParsingName(lnkPath, IntPtr.Zero, ref iid, out item);
            if (item == null)
                return null;
            IntPtr pStr;
            PROPERTYKEY key = PkeyAppUserModelId;
            int hr = item.GetString(ref key, out pStr);
            if (hr != 0 || pStr == IntPtr.Zero)
                return null;
            try
            {
                string s = Marshal.PtrToStringUni(pStr);
                return string.IsNullOrEmpty(s) ? null : s;
            }
            finally
            {
                Marshal.FreeCoTaskMem(pStr);
            }
        }
        catch
        {
            return null;
        }
        finally
        {
            if (item != null)
            {
                try { Marshal.ReleaseComObject(item); }
                catch { }
            }
        }
    }

    private static string AssocQueryStringExecutable(string ext)
    {
        const int assocStrExecutable = 2;
        const int assocFNone = 0;
        uint pcch = 0;
        AssocQueryString(assocFNone, assocStrExecutable, ext, null, null, ref pcch);
        if (pcch == 0)
            return null;
        StringBuilder sb = new StringBuilder((int)pcch);
        int hr = AssocQueryString(assocFNone, assocStrExecutable, ext, null, sb, ref pcch);
        if (hr != 0)
            return null;
        string s = sb.ToString().Trim().Trim('"');
        if (string.IsNullOrEmpty(s) || !File.Exists(s))
            return null;
        return s;
    }

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    private static extern int AssocQueryString(int flags, int str, string pszAssoc, string pszExtra,
        StringBuilder pszOut, ref uint pcchOut);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
        IntPtr pbc,
        ref Guid riid,
        out IShellItem2 ppv);

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct PROPERTYKEY
    {
        public Guid fmtid;
        public uint pid;
    }

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
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszPath);
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

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("7E9FB0D3-919F-4307-AB2E-9B1860310C93")]
    private interface IShellItem2
    {
        void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
        void GetParent(out IntPtr ppsi);
        void GetDisplayName(uint sigdnName, out IntPtr ppszName);
        void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
        void Compare(IntPtr psi, uint hint, out int piOrder);
        void GetPropertyStore(uint flags, ref Guid riid, out IntPtr ppv);
        void GetPropertyStoreWithCreateObject(uint flags, IntPtr punkCreateObject, ref Guid riid, out IntPtr ppv);
        void GetPropertyStoreForKeys(IntPtr rgKeys, uint cKeys, uint flags, ref Guid riid, out IntPtr ppv);
        void GetPropertyDescriptionList(ref PROPERTYKEY keyType, ref Guid riid, out IntPtr ppv);
        void Update(IntPtr pbc);
        void GetProperty(ref PROPERTYKEY key, IntPtr ppropvar);
        void GetCLSID(ref PROPERTYKEY key, out Guid pclsid);
        void GetFileTime(ref PROPERTYKEY key, out long pft);
        void GetInt32(ref PROPERTYKEY key, out int pi);
        [PreserveSig]
        int GetString(ref PROPERTYKEY key, out IntPtr ppsz);
        void GetUInt32(ref PROPERTYKEY key, out uint pui);
        void GetUInt64(ref PROPERTYKEY key, out ulong pull);
        void GetBool(ref PROPERTYKEY key, out int pf);
    }
}
