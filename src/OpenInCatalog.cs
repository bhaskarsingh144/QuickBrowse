using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Win32;

internal sealed class OpenInApp
{
    public string Id;
    public string Title;
    public string ExePath;
    /// <summary>Bit flags from <see cref="OpenAppFamily"/>.</summary>
    public int Families;
}

[Flags]
internal enum OpenAppFamily
{
    None = 0,
    Editor = 1,
    Video = 2,
    Audio = 4,
    Image = 8,
    Pdf = 16,
    Spreadsheet = 32,
    Document = 64,
    Presentation = 128,
    Browser = 256
}

/// <summary>
/// Detects everyday apps for the unified Open with cascade (no IContextMenu).
/// </summary>
internal static class OpenInCatalog
{
    private const int OpenWithListCap = 8;

    private static List<OpenInApp> _cached;
    private static DateTime _cachedUtc;

    public static IList<OpenInApp> DetectedApps()
    {
        if (_cached != null && (DateTime.UtcNow - _cachedUtc).TotalSeconds < 60)
            return _cached;

        List<OpenInApp> list = new List<OpenInApp>();

        // Editors / IDEs (folders + text/code)
        TryAdd(list, "cursor", "Cursor", (int)OpenAppFamily.Editor,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "cursor", "Cursor.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "cursor", "Cursor.exe"));
        TryAdd(list, "code", "Visual Studio Code", (int)OpenAppFamily.Editor,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "Microsoft VS Code", "Code.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Microsoft VS Code", "Code.exe"));
        TryAdd(list, "idea", "IntelliJ IDEA", (int)OpenAppFamily.Editor,
            FindUnder(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "JetBrains"), "idea64.exe"),
            FindUnder(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "IntelliJ IDEA"), "idea64.exe"));
        TryAdd(list, "devenv", "Visual Studio", (int)OpenAppFamily.Editor,
            FindFromAppPaths("devenv.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Microsoft Visual Studio", "2022", "Community", "Common7", "IDE", "devenv.exe"));
        TryAdd(list, "notepadpp", "Notepad++", (int)OpenAppFamily.Editor,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Notepad++", "notepad++.exe"),
            FindFromAppPaths("notepad++.exe"));
        TryAdd(list, "sublime", "Sublime Text", (int)OpenAppFamily.Editor,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Sublime Text", "sublime_text.exe"),
            FindFromAppPaths("sublime_text.exe"));
        TryAdd(list, "notepad", "Notepad", (int)OpenAppFamily.Editor,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "notepad.exe"),
            FindFromAppPaths("notepad.exe"));

        // Video / audio
        int media = (int)(OpenAppFamily.Video | OpenAppFamily.Audio);
        TryAdd(list, "vlc", "VLC media player", media,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "VideoLAN", "VLC", "vlc.exe"),
            FindFromAppPaths("vlc.exe"));
        TryAdd(list, "potplayer", "PotPlayer", media,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "DAUM", "PotPlayer", "PotPlayerMini64.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "DAUM", "PotPlayer", "PotPlayerMini64.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "DAUM", "PotPlayer", "PotPlayer64.exe"),
            FindFromAppPaths("PotPlayerMini64.exe"));
        TryAdd(list, "mpchc", "MPC-HC", media,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "MPC-HC", "mpc-hc64.exe"),
            FindFromAppPaths("mpc-hc64.exe"),
            FindFromAppPaths("mpc-hc.exe"));

        // Images
        TryAdd(list, "mspaint", "Paint", (int)OpenAppFamily.Image,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "mspaint.exe"),
            FindFromAppPaths("mspaint.exe"));
        TryAdd(list, "paintnet", "Paint.NET", (int)OpenAppFamily.Image,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "paint.net", "PaintDotNet.exe"),
            FindFromAppPaths("PaintDotNet.exe"));

        // PDF / browser
        TryAdd(list, "sumatra", "SumatraPDF", (int)OpenAppFamily.Pdf,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SumatraPDF", "SumatraPDF.exe"),
            FindFromAppPaths("SumatraPDF.exe"));
        TryAdd(list, "acrobat", "Adobe Acrobat", (int)OpenAppFamily.Pdf,
            FindFromAppPaths("Acrobat.exe"),
            FindFromAppPaths("AcroRd32.exe"));
        TryAdd(list, "msedge", "Microsoft Edge", (int)(OpenAppFamily.Pdf | OpenAppFamily.Browser | OpenAppFamily.Image),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Microsoft", "Edge", "Application", "msedge.exe"),
            FindFromAppPaths("msedge.exe"));

        // Office
        TryAdd(list, "excel", "Excel", (int)OpenAppFamily.Spreadsheet,
            FindFromAppPaths("excel.exe"));
        TryAdd(list, "winword", "Word", (int)OpenAppFamily.Document,
            FindFromAppPaths("winword.exe"));
        TryAdd(list, "powerpnt", "PowerPoint", (int)OpenAppFamily.Presentation,
            FindFromAppPaths("powerpnt.exe"));

        _cached = list;
        _cachedUtc = DateTime.UtcNow;
        return list;
    }

    /// <summary>Test hook — clear cache.</summary>
    public static void ClearCacheForTests()
    {
        _cached = null;
    }

    public static bool Launch(OpenInApp app, string path)
    {
        if (app == null || string.IsNullOrEmpty(path))
            return false;

        // Default association must go through the shell (same as Open), not exe+args.
        // UWP / COM / delegate handlers often fail when spawned as FileName=exe Arguments=doc.
        if (string.Equals(app.Id, "assoc", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                RecentSource.NotifyFileAccess(path);
                return true;
            }
            catch
            {
                return false;
            }
        }

        if (string.IsNullOrEmpty(app.ExePath) || !File.Exists(app.ExePath))
            return false;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = app.ExePath,
                Arguments = "\"" + path + "\"",
                UseShellExecute = false
            });
            RecentSource.NotifyFileOpenedWith(path, app.ExePath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool LaunchById(string id, string path)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(path))
            return false;
        OpenInApp app = FindHandler(id, path);
        return Launch(app, path);
    }

    /// <summary>Unified Open with handlers for a file or folder path.</summary>
    public static IList<OpenInApp> HandlersFor(string path)
    {
        return HandlersForExtension(path);
    }

    /// <summary>Friendly handlers: default assoc, type-relevant apps, OpenWithList (best-effort).</summary>
    public static IList<OpenInApp> HandlersForExtension(string path)
    {
        List<OpenInApp> list = new List<OpenInApp>();
        if (string.IsNullOrEmpty(path))
            return list;

        bool isDir = false;
        try { isDir = Directory.Exists(path); }
        catch { isDir = false; }

        if (isDir)
        {
            AppendFamilyMatches(list, (int)OpenAppFamily.Editor);
            return list;
        }

        string ext;
        try { ext = Path.GetExtension(path); }
        catch { return list; }
        if (string.IsNullOrEmpty(ext) || ext.Length < 2)
        {
            AppendFamilyMatches(list, (int)OpenAppFamily.Editor);
            return list;
        }

        OpenAppFamily family = FamilyForExtension(ext);
        string assocExe = AssocQueryStringSafe(ext);
        if (!string.IsNullOrEmpty(assocExe) && File.Exists(assocExe))
        {
            string title = Path.GetFileNameWithoutExtension(assocExe);
            AddUnique(list, new OpenInApp
            {
                Id = "assoc",
                Title = title,
                ExePath = assocExe,
                Families = (int)family
            });
        }

        if (family != OpenAppFamily.None)
            AppendFamilyMatches(list, (int)family);
        else
            AppendFamilyMatches(list, (int)OpenAppFamily.Editor);

        AppendOpenWithList(list, ext);
        return list;
    }

    /// <summary>Maps everyday extensions to app families (pure — for tests).</summary>
    public static OpenAppFamily FamilyForExtension(string ext)
    {
        if (string.IsNullOrEmpty(ext))
            return OpenAppFamily.None;
        if (ext[0] != '.')
            ext = "." + ext;
        string e = ext.ToLowerInvariant();

        switch (e)
        {
            case ".mp4":
            case ".mkv":
            case ".avi":
            case ".mov":
            case ".wmv":
            case ".webm":
            case ".m4v":
            case ".flv":
            case ".mpeg":
            case ".mpg":
            case ".m2ts":
            case ".mts":
                return OpenAppFamily.Video;
            case ".mp3":
            case ".flac":
            case ".wav":
            case ".aac":
            case ".m4a":
            case ".ogg":
            case ".wma":
            case ".opus":
                return OpenAppFamily.Audio;
            case ".jpg":
            case ".jpeg":
            case ".png":
            case ".gif":
            case ".webp":
            case ".bmp":
            case ".tif":
            case ".tiff":
            case ".heic":
            case ".svg":
                return OpenAppFamily.Image;
            case ".pdf":
                return OpenAppFamily.Pdf;
            case ".xlsx":
            case ".xls":
            case ".xlsm":
            case ".csv":
                return OpenAppFamily.Spreadsheet;
            case ".docx":
            case ".doc":
            case ".rtf":
            case ".odt":
                return OpenAppFamily.Document;
            case ".pptx":
            case ".ppt":
            case ".ppsx":
                return OpenAppFamily.Presentation;
            case ".txt":
            case ".md":
            case ".cs":
            case ".js":
            case ".ts":
            case ".tsx":
            case ".jsx":
            case ".json":
            case ".xml":
            case ".html":
            case ".htm":
            case ".css":
            case ".py":
            case ".java":
            case ".go":
            case ".rs":
            case ".cpp":
            case ".c":
            case ".h":
            case ".hpp":
            case ".yml":
            case ".yaml":
            case ".toml":
            case ".ini":
            case ".log":
            case ".ps1":
            case ".bat":
            case ".cmd":
            case ".sql":
            case ".sh":
                return OpenAppFamily.Editor;
            default:
                return OpenAppFamily.None;
        }
    }

    /// <summary>True when app families overlap the requested mask (pure).</summary>
    public static bool MatchesFamily(OpenInApp app, int familyMask)
    {
        if (app == null || familyMask == 0)
            return false;
        return (app.Families & familyMask) != 0;
    }

    private static OpenInApp FindHandler(string id, string path)
    {
        IList<OpenInApp> known = DetectedApps();
        for (int i = 0; i < known.Count; i++)
        {
            if (string.Equals(known[i].Id, id, StringComparison.OrdinalIgnoreCase))
                return known[i];
        }
        IList<OpenInApp> handlers = HandlersFor(path);
        for (int i = 0; i < handlers.Count; i++)
        {
            if (string.Equals(handlers[i].Id, id, StringComparison.OrdinalIgnoreCase))
                return handlers[i];
        }
        return null;
    }

    private static void AppendFamilyMatches(List<OpenInApp> list, int familyMask)
    {
        IList<OpenInApp> known = DetectedApps();
        for (int i = 0; i < known.Count; i++)
        {
            OpenInApp app = known[i];
            if (!MatchesFamily(app, familyMask))
                continue;
            AddUnique(list, app);
        }
    }

    private static void AppendOpenWithList(List<OpenInApp> list, string ext)
    {
        if (string.IsNullOrEmpty(ext))
            return;
        string key = ext.StartsWith(".", StringComparison.Ordinal) ? ext : ("." + ext);
        int added = 0;
        added += AppendOpenWithListHive(list, Registry.CurrentUser, key, added);
        if (added < OpenWithListCap)
            AppendOpenWithListHive(list, Registry.LocalMachine, key, added);
    }

    private static int AppendOpenWithListHive(List<OpenInApp> list, RegistryKey root, string ext, int already)
    {
        int added = 0;
        try
        {
            using (RegistryKey k = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\" + ext + @"\OpenWithList"))
            {
                if (k != null)
                {
                    object mrulist = k.GetValue("MRUList");
                    string order = mrulist as string;
                    if (!string.IsNullOrEmpty(order))
                    {
                        for (int i = 0; i < order.Length && already + added < OpenWithListCap; i++)
                        {
                            object v = k.GetValue(order[i].ToString());
                            string exeName = v as string;
                            if (AddOpenWithExeName(list, exeName))
                                added++;
                        }
                    }
                    else
                    {
                        string[] names = k.GetValueNames();
                        for (int i = 0; i < names.Length && already + added < OpenWithListCap; i++)
                        {
                            if (string.Equals(names[i], "MRUList", StringComparison.OrdinalIgnoreCase))
                                continue;
                            object v = k.GetValue(names[i]);
                            if (AddOpenWithExeName(list, v as string))
                                added++;
                        }
                    }
                }
            }
        }
        catch
        {
        }
        return added;
    }

    private static bool AddOpenWithExeName(List<OpenInApp> list, string exeName)
    {
        if (string.IsNullOrEmpty(exeName))
            return false;
        if (exeName.IndexOf("dllhost", StringComparison.OrdinalIgnoreCase) >= 0
            || exeName.IndexOf("OpenWith.exe", StringComparison.OrdinalIgnoreCase) >= 0
            || exeName.IndexOf("rundll32", StringComparison.OrdinalIgnoreCase) >= 0)
            return false;
        string path = FindFromAppPaths(exeName);
        if (string.IsNullOrEmpty(path))
        {
            try
            {
                string sys = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), exeName);
                if (File.Exists(sys))
                    path = sys;
            }
            catch
            {
            }
        }
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return false;
        string id = "ow:" + Path.GetFileNameWithoutExtension(exeName).ToLowerInvariant();
        string title = Path.GetFileNameWithoutExtension(path);
        return AddUnique(list, new OpenInApp { Id = id, Title = title, ExePath = path, Families = 0 });
    }

    private static bool AddUnique(List<OpenInApp> list, OpenInApp app)
    {
        if (app == null || string.IsNullOrEmpty(app.ExePath))
            return false;
        for (int i = 0; i < list.Count; i++)
        {
            if (string.Equals(list[i].ExePath, app.ExePath, StringComparison.OrdinalIgnoreCase))
                return false;
            if (string.Equals(list[i].Id, app.Id, StringComparison.OrdinalIgnoreCase))
                return false;
        }
        list.Add(app);
        return true;
    }

    private static void TryAdd(List<OpenInApp> list, string id, string title, int families, params string[] candidates)
    {
        for (int i = 0; i < candidates.Length; i++)
        {
            string p = candidates[i];
            if (string.IsNullOrEmpty(p) || !File.Exists(p))
                continue;
            for (int j = 0; j < list.Count; j++)
            {
                if (string.Equals(list[j].Id, id, StringComparison.OrdinalIgnoreCase))
                    return;
            }
            list.Add(new OpenInApp { Id = id, Title = title, ExePath = p, Families = families });
            return;
        }
    }

    private static string FindUnder(string root, string fileName)
    {
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            return null;
        try
        {
            string[] hits = Directory.GetFiles(root, fileName, SearchOption.AllDirectories);
            if (hits != null && hits.Length > 0)
                return hits[0];
        }
        catch
        {
        }
        return null;
    }

    private static string FindFromAppPaths(string exeName)
    {
        if (string.IsNullOrEmpty(exeName))
            return null;
        string hit = FindFromAppPathsHive(Registry.LocalMachine, exeName);
        if (!string.IsNullOrEmpty(hit))
            return hit;
        return FindFromAppPathsHive(Registry.CurrentUser, exeName);
    }

    private static string FindFromAppPathsHive(RegistryKey root, string exeName)
    {
        try
        {
            using (RegistryKey k = root.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\" + exeName))
            {
                if (k != null)
                {
                    object v = k.GetValue(null);
                    string path = v as string;
                    if (!string.IsNullOrEmpty(path))
                    {
                        path = path.Trim().Trim('"');
                        if (File.Exists(path))
                            return path;
                    }
                }
            }
        }
        catch
        {
        }
        return null;
    }

    private static string AssocQueryStringSafe(string ext)
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
        return string.IsNullOrEmpty(s) ? null : s;
    }

    [System.Runtime.InteropServices.DllImport("shlwapi.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int AssocQueryString(int flags, int str, string pszAssoc, string pszExtra,
        StringBuilder pszOut, ref uint pcchOut);
}
