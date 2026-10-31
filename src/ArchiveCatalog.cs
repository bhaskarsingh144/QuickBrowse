using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

internal enum ArchiveProviderKind
{
    ZipManaged,
    WinRar,
    SevenZip,
    NanaZip
}

internal sealed class ArchiveProvider
{
    public ArchiveProviderKind Kind;
    public string Title;
    public string ExePath;
}

/// <summary>Detects WinRAR / 7-Zip / NanaZip and builds CLI compress/extract commands.</summary>
internal static class ArchiveCatalog
{
    private static List<ArchiveProvider> _cached;
    private static DateTime _cachedUtc;

    public static bool IsArchivePath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;
        string ext;
        try { ext = Path.GetExtension(path); }
        catch { return false; }
        if (string.IsNullOrEmpty(ext))
            return false;
        return string.Equals(ext, ".zip", StringComparison.OrdinalIgnoreCase)
            || string.Equals(ext, ".rar", StringComparison.OrdinalIgnoreCase)
            || string.Equals(ext, ".7z", StringComparison.OrdinalIgnoreCase)
            || string.Equals(ext, ".tar", StringComparison.OrdinalIgnoreCase)
            || string.Equals(ext, ".gz", StringComparison.OrdinalIgnoreCase)
            || string.Equals(ext, ".bz2", StringComparison.OrdinalIgnoreCase);
    }

    public static IList<ArchiveProvider> DetectedProviders()
    {
        if (_cached != null && (DateTime.UtcNow - _cachedUtc).TotalSeconds < 60)
            return _cached;

        List<ArchiveProvider> list = new List<ArchiveProvider>();
        list.Add(new ArchiveProvider { Kind = ArchiveProviderKind.ZipManaged, Title = "ZIP (QuickBrowse)", ExePath = null });

        string winrar = FirstExisting(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WinRAR", "WinRAR.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "WinRAR", "WinRAR.exe"),
            AppPath("WinRAR.exe"));
        if (!string.IsNullOrEmpty(winrar))
            list.Add(new ArchiveProvider { Kind = ArchiveProviderKind.WinRar, Title = "WinRAR", ExePath = winrar });

        string seven = FirstExisting(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "7-Zip", "7z.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "7-Zip", "7z.exe"),
            AppPath("7z.exe"));
        if (!string.IsNullOrEmpty(seven))
            list.Add(new ArchiveProvider { Kind = ArchiveProviderKind.SevenZip, Title = "7-Zip", ExePath = seven });

        string nana = FirstExisting(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft", "WindowsApps", "NanaZip.exe"),
            FindUnder(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "NanaZip"), "NanaZip.exe"));
        if (!string.IsNullOrEmpty(nana))
            list.Add(new ArchiveProvider { Kind = ArchiveProviderKind.NanaZip, Title = "NanaZip", ExePath = nana });

        _cached = list;
        _cachedUtc = DateTime.UtcNow;
        return list;
    }

    public static void ClearCacheForTests()
    {
        _cached = null;
    }

    public static string CompressArgs(ArchiveProviderKind kind, string srcPath, string destArchive)
    {
        string qSrc = Quote(srcPath);
        string qDest = Quote(destArchive);
        switch (kind)
        {
            case ArchiveProviderKind.WinRar:
                return "a -afzip -ep1 " + qDest + " " + qSrc;
            case ArchiveProviderKind.SevenZip:
            case ArchiveProviderKind.NanaZip:
                return "a -tzip " + qDest + " " + qSrc;
            default:
                return null;
        }
    }

    public static string ExtractHereArgs(ArchiveProviderKind kind, string archivePath)
    {
        string q = Quote(archivePath);
        string dir = Quote(Path.GetDirectoryName(archivePath) ?? ".");
        switch (kind)
        {
            case ArchiveProviderKind.WinRar:
                return "x -y " + q + " " + dir + "\\";
            case ArchiveProviderKind.SevenZip:
            case ArchiveProviderKind.NanaZip:
                return "x -y " + q + " -o" + dir + "\\";
            default:
                return null;
        }
    }

    public static string ExtractToFolderArgs(ArchiveProviderKind kind, string archivePath, string destFolder)
    {
        string q = Quote(archivePath);
        string d = Quote(destFolder);
        switch (kind)
        {
            case ArchiveProviderKind.WinRar:
                return "x -y " + q + " " + d + "\\";
            case ArchiveProviderKind.SevenZip:
            case ArchiveProviderKind.NanaZip:
                return "x -y " + q + " -o" + d + "\\";
            default:
                return null;
        }
    }

    public static bool Run(ArchiveProvider provider, string args, out string error)
    {
        error = null;
        if (provider == null || provider.Kind == ArchiveProviderKind.ZipManaged)
        {
            error = "Use managed ZIP.";
            return false;
        }
        if (string.IsNullOrEmpty(provider.ExePath) || !File.Exists(provider.ExePath))
        {
            error = "Archive tool not found.";
            return false;
        }
        try
        {
            Process p = Process.Start(new ProcessStartInfo
            {
                FileName = provider.ExePath,
                Arguments = args ?? "",
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (p == null)
            {
                error = "Could not start " + provider.Title;
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static string SuggestZipPath(string srcPath)
    {
        if (string.IsNullOrEmpty(srcPath))
            return null;
        string trimmed = srcPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string parent = Path.GetDirectoryName(trimmed);
        string baseName = Path.GetFileName(trimmed);
        // Folders keep their name; files drop the extension (even if the path does not exist yet).
        bool treatAsFile = File.Exists(srcPath)
            || (!Directory.Exists(srcPath) && !string.IsNullOrEmpty(Path.GetExtension(trimmed)));
        if (treatAsFile)
            baseName = Path.GetFileNameWithoutExtension(trimmed);
        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(baseName))
            return null;
        return Path.Combine(parent, baseName + ".zip");
    }

    public static string SuggestExtractFolder(string archivePath)
    {
        if (string.IsNullOrEmpty(archivePath))
            return null;
        string parent = Path.GetDirectoryName(archivePath);
        string name = Path.GetFileNameWithoutExtension(archivePath);
        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
            return null;
        return Path.Combine(parent, name);
    }

    private static string Quote(string path)
    {
        if (string.IsNullOrEmpty(path))
            return "\"\"";
        if (path.IndexOf(' ') >= 0)
            return "\"" + path + "\"";
        return path;
    }

    private static string FirstExisting(params string[] paths)
    {
        for (int i = 0; i < paths.Length; i++)
        {
            if (!string.IsNullOrEmpty(paths[i]) && File.Exists(paths[i]))
                return paths[i];
        }
        return null;
    }

    private static string AppPath(string exeName)
    {
        try
        {
            using (RegistryKey k = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\" + exeName))
            {
                if (k == null)
                    return null;
                object v = k.GetValue(null);
                string path = v as string;
                return (!string.IsNullOrEmpty(path) && File.Exists(path)) ? path : null;
            }
        }
        catch
        {
            return null;
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
}
