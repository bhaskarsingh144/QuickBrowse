using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

internal sealed class OpenInApp
{
    public string Id;
    public string Title;
    public string ExePath;
}

/// <summary>
/// Detects editors/IDEs for Open in / Open with cascades (no IContextMenu).
/// </summary>
internal static class OpenInCatalog
{
    private static List<OpenInApp> _cached;
    private static DateTime _cachedUtc;

    public static IList<OpenInApp> DetectedApps()
    {
        if (_cached != null && (DateTime.UtcNow - _cachedUtc).TotalSeconds < 60)
            return _cached;

        List<OpenInApp> list = new List<OpenInApp>();
        TryAdd(list, "cursor", "Cursor",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "cursor", "Cursor.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "cursor", "Cursor.exe"));
        TryAdd(list, "code", "Visual Studio Code",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "Microsoft VS Code", "Code.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Microsoft VS Code", "Code.exe"));
        TryAdd(list, "idea", "IntelliJ IDEA",
            FindUnder(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "JetBrains"), "idea64.exe"),
            FindUnder(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "IntelliJ IDEA"), "idea64.exe"));
        TryAdd(list, "devenv", "Visual Studio",
            FindFromAppPaths("devenv.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Microsoft Visual Studio", "2022", "Community", "Common7", "IDE", "devenv.exe"));
        TryAdd(list, "notepadpp", "Notepad++",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Notepad++", "notepad++.exe"),
            FindFromAppPaths("notepad++.exe"));
        TryAdd(list, "sublime", "Sublime Text",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Sublime Text", "sublime_text.exe"),
            FindFromAppPaths("sublime_text.exe"));

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
        if (app == null || string.IsNullOrEmpty(app.ExePath) || string.IsNullOrEmpty(path))
            return false;
        if (!File.Exists(app.ExePath))
            return false;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = app.ExePath,
                Arguments = "\"" + path + "\"",
                UseShellExecute = false
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool LaunchById(string id, string path)
    {
        IList<OpenInApp> apps = DetectedApps();
        for (int i = 0; i < apps.Count; i++)
        {
            if (string.Equals(apps[i].Id, id, StringComparison.OrdinalIgnoreCase))
                return Launch(apps[i], path);
        }
        return false;
    }

    /// <summary>Friendly handlers from OpenWithList / AssocQueryString (best-effort, read-only).</summary>
    public static IList<OpenInApp> HandlersForExtension(string path)
    {
        List<OpenInApp> list = new List<OpenInApp>();
        if (string.IsNullOrEmpty(path))
            return list;
        string ext;
        try { ext = Path.GetExtension(path); }
        catch { return list; }
        if (string.IsNullOrEmpty(ext) || ext.Length < 2)
            return list;

        // Prefer detected IDEs that commonly handle this type.
        IList<OpenInApp> known = DetectedApps();
        for (int i = 0; i < known.Count; i++)
            list.Add(known[i]);

        try
        {
            string exe = AssocQueryStringSafe(ext);
            if (!string.IsNullOrEmpty(exe) && File.Exists(exe))
            {
                string title = Path.GetFileNameWithoutExtension(exe);
                bool dup = false;
                for (int i = 0; i < list.Count; i++)
                {
                    if (string.Equals(list[i].ExePath, exe, StringComparison.OrdinalIgnoreCase))
                    {
                        dup = true;
                        break;
                    }
                }
                if (!dup)
                    list.Insert(0, new OpenInApp { Id = "assoc", Title = title, ExePath = exe });
            }
        }
        catch
        {
        }
        return list;
    }

    private static void TryAdd(List<OpenInApp> list, string id, string title, params string[] candidates)
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
            list.Add(new OpenInApp { Id = id, Title = title, ExePath = p });
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
        try
        {
            using (RegistryKey k = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\" + exeName))
            {
                if (k != null)
                {
                    object v = k.GetValue(null);
                    string path = v as string;
                    if (!string.IsNullOrEmpty(path) && File.Exists(path))
                        return path;
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
        // ASSOCSTR_EXECUTABLE = 2
        const int assocStrExecutable = 2;
        const int assocFNone = 0;
        uint pcch = 0;
        AssocQueryString(assocFNone, assocStrExecutable, ext, null, null, ref pcch);
        if (pcch == 0)
            return null;
        System.Text.StringBuilder sb = new System.Text.StringBuilder((int)pcch);
        int hr = AssocQueryString(assocFNone, assocStrExecutable, ext, null, sb, ref pcch);
        if (hr != 0)
            return null;
        string s = sb.ToString().Trim().Trim('"');
        return string.IsNullOrEmpty(s) ? null : s;
    }

    [System.Runtime.InteropServices.DllImport("shlwapi.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int AssocQueryString(int flags, int str, string pszAssoc, string pszExtra,
        System.Text.StringBuilder pszOut, ref uint pcchOut);
}
