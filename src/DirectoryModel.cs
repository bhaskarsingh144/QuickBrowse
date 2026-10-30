using System;
using System.Collections.Generic;
using System.IO;

internal sealed class DirEntry
{
    public string Name;
    public string FullPath;
    public bool IsDirectory;
    public bool IsDrive;
    public long Length;
    public DateTime LastWriteTime;
    public string TypeName;
    public bool HasSize;
    public bool HasDate;
}

internal sealed class DirectoryListing
{
    public string Path;
    public string Error;
    public IList<DirEntry> Entries;
}

internal static class DirectoryModel
{
    public static DirectoryListing ListThisPc()
    {
        DirectoryListing result = new DirectoryListing();
        result.Path = "This PC";
        result.Entries = new List<DirEntry>();
        try
        {
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady)
                    continue;
                string label = drive.Name.TrimEnd('\\');
                try
                {
                    if (!string.IsNullOrEmpty(drive.VolumeLabel))
                        label = drive.VolumeLabel + " (" + drive.Name.TrimEnd('\\') + ")";
                }
                catch
                {
                }
                DirEntry entry = new DirEntry
                {
                    Name = label,
                    FullPath = drive.RootDirectory.FullName,
                    IsDirectory = true,
                    IsDrive = true,
                    TypeName = "Drive",
                    HasSize = false,
                    HasDate = false
                };
                try
                {
                    entry.LastWriteTime = drive.RootDirectory.LastWriteTime;
                    entry.HasDate = true;
                }
                catch
                {
                }
                result.Entries.Add(entry);
            }
            SortEntries((List<DirEntry>)result.Entries);
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
        }
        return result;
    }

    public static DirectoryListing ListPath(string path)
    {
        return ListPath(path, false);
    }

    public static DirectoryListing ListPath(string path, bool showHidden)
    {
        DirectoryListing result = new DirectoryListing();
        result.Path = path;
        result.Entries = new List<DirEntry>();
        if (string.IsNullOrEmpty(path))
        {
            result.Error = "Path is empty.";
            return result;
        }
        try
        {
            if (!Directory.Exists(path))
            {
                result.Error = "Folder not found.";
                return result;
            }
            DirectoryInfo root = new DirectoryInfo(path);
            foreach (DirectoryInfo dir in root.GetDirectories())
            {
                if (ShouldSkip(dir.Attributes, showHidden))
                    continue;
                result.Entries.Add(new DirEntry
                {
                    Name = dir.Name,
                    FullPath = dir.FullName,
                    IsDirectory = true,
                    TypeName = "File folder",
                    LastWriteTime = dir.LastWriteTime,
                    HasDate = true,
                    HasSize = false
                });
            }
            foreach (FileInfo file in root.GetFiles())
            {
                if (ShouldSkip(file.Attributes, showHidden))
                    continue;
                result.Entries.Add(new DirEntry
                {
                    Name = file.Name,
                    FullPath = file.FullName,
                    IsDirectory = false,
                    TypeName = TypeLabel(file),
                    Length = file.Length,
                    LastWriteTime = file.LastWriteTime,
                    HasDate = true,
                    HasSize = true
                });
            }
            SortEntries((List<DirEntry>)result.Entries);
        }
        catch (UnauthorizedAccessException)
        {
            result.Error = "Access denied.";
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
        }
        return result;
    }

    public static void SortEntries(List<DirEntry> entries)
    {
        if (entries == null)
            return;
        entries.Sort(CompareEntries);
    }

    public static int CompareEntries(DirEntry a, DirEntry b)
    {
        if (a == null && b == null)
            return 0;
        if (a == null)
            return 1;
        if (b == null)
            return -1;
        if (a.IsDirectory != b.IsDirectory)
            return a.IsDirectory ? -1 : 1;
        return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
    }

    public static int CompareByColumn(DirEntry a, DirEntry b, int column, bool ascending)
    {
        int dir = ascending ? 1 : -1;
        if (a == null && b == null)
            return 0;
        if (a == null)
            return 1;
        if (b == null)
            return -1;
        // Folders first always.
        if (a.IsDirectory != b.IsDirectory)
            return a.IsDirectory ? -1 : 1;

        int cmp;
        switch (column)
        {
            case 1: // Date
                cmp = a.LastWriteTime.CompareTo(b.LastWriteTime);
                break;
            case 2: // Type
                cmp = string.Compare(a.TypeName ?? "", b.TypeName ?? "", StringComparison.OrdinalIgnoreCase);
                break;
            case 3: // Size
                cmp = a.Length.CompareTo(b.Length);
                break;
            default:
                cmp = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                break;
        }
        if (cmp == 0)
            cmp = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        return cmp * dir;
    }

    public static string ParentPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return null;
        try
        {
            DirectoryInfo parent = Directory.GetParent(path);
            if (parent == null)
                return null;
            return parent.FullName;
        }
        catch
        {
            return null;
        }
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
            return bytes + " B";
        double kb = bytes / 1024.0;
        if (kb < 1024)
            return kb.ToString("0.#") + " KB";
        double mb = kb / 1024.0;
        if (mb < 1024)
            return mb.ToString("0.#") + " MB";
        double gb = mb / 1024.0;
        return gb.ToString("0.##") + " GB";
    }

    public static string FormatDate(DirEntry entry)
    {
        if (entry == null || !entry.HasDate || entry.LastWriteTime == DateTime.MinValue)
            return "";
        return entry.LastWriteTime.ToString("g");
    }

    public static string FormatSize(DirEntry entry)
    {
        if (entry == null || entry.IsDirectory || !entry.HasSize)
            return "";
        return FormatBytes(entry.Length);
    }

    private static string TypeLabel(FileInfo file)
    {
        string ext = file.Extension;
        if (string.IsNullOrEmpty(ext))
            return "File";
        return ext.TrimStart('.').ToUpperInvariant() + " File";
    }

    /// <summary>
    /// Always skip System (and Hidden+System protected files).
    /// When <paramref name="showHidden"/> is false, also skip Hidden-only items.
    /// </summary>
    internal static bool ShouldSkip(FileAttributes attrs, bool showHidden)
    {
        if ((attrs & FileAttributes.System) != 0)
            return true;
        if ((attrs & FileAttributes.Hidden) != 0 && !showHidden)
            return true;
        return false;
    }
}
