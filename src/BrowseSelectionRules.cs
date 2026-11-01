using System;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Which context-menu verbs apply to the current selection (pure logic, no UI).
/// Single-target verbs (rename, open with, hash, properties, ...) require exactly one entry.
/// Batch verbs (cut/copy/delete/duplicate/shortcut/send to/compress) work on every selected item.
/// </summary>
internal struct BrowseMenuCapabilities
{
    public int Count;
    public bool AllHavePath;
    public bool AnyDrive;
    public bool AllDirectories;
    public bool AllFiles;
    public bool AllArchives;

    public bool CanCutCopyDelete;
    public bool CanRename;
    public bool CanOpen;
    public bool CanOpenWith;
    public bool CanOpenIn;
    public bool CanPin;
    public bool CanSendTo;
    public bool CanCompress;
    public bool CanExtract;
    public bool CanCopyPath;
    public bool CanHash;
    public bool CanDuplicate;
    public bool CanShortcut;
    public bool CanRunAsAdmin;
    public bool CanTerminal;
    public bool CanExplorer;
    public bool CanProperties;
}

internal static class BrowseSelectionRules
{
    /// <summary>True when count&gt;0, every entry has a path, and none is a drive root.</summary>
    public static bool CanCutCopyDelete(IList<DirEntry> entries)
    {
        if (entries == null || entries.Count == 0)
            return false;
        for (int i = 0; i < entries.Count; i++)
        {
            DirEntry e = entries[i];
            if (e == null || e.IsDrive || string.IsNullOrEmpty(e.FullPath))
                return false;
        }
        return true;
    }

    public static BrowseMenuCapabilities Compute(IList<DirEntry> entries)
    {
        BrowseMenuCapabilities c = new BrowseMenuCapabilities();
        int count = entries == null ? 0 : entries.Count;
        c.Count = count;
        if (count == 0)
            return c;

        bool allPath = true;
        bool anyDrive = false;
        bool allDirs = true;
        bool allFiles = true;
        bool allArchives = true;
        for (int i = 0; i < count; i++)
        {
            DirEntry e = entries[i];
            if (e == null)
            {
                allPath = false;
                allDirs = false;
                allFiles = false;
                allArchives = false;
                continue;
            }
            bool hasPath = !string.IsNullOrEmpty(e.FullPath);
            bool isFile = hasPath && !e.IsDirectory && !e.IsDrive;
            if (!hasPath) allPath = false;
            if (e.IsDrive) anyDrive = true;
            if (!e.IsDirectory || e.IsDrive) allDirs = false;
            if (!isFile) allFiles = false;
            if (!isFile || !ArchiveCatalog.IsArchivePath(e.FullPath)) allArchives = false;
        }

        c.AllHavePath = allPath;
        c.AnyDrive = anyDrive;
        c.AllDirectories = allDirs;
        c.AllFiles = allFiles;
        c.AllArchives = allArchives;

        bool single = count == 1;
        bool canEdit = allPath && !anyDrive;
        DirEntry one = single ? entries[0] : null;
        bool singleFile = single && allFiles;
        bool singleDir = single && allDirs && canEdit;
        bool singleArchive = single && allArchives;
        bool singleRunnable = singleFile && IsRunnablePath(one.FullPath);

        c.CanCutCopyDelete = canEdit;
        c.CanDuplicate = canEdit;
        c.CanShortcut = canEdit;
        c.CanSendTo = canEdit;
        c.CanRename = single && canEdit;
        c.CanOpen = allPath;
        c.CanCopyPath = allPath;
        c.CanOpenWith = singleFile;
        c.CanOpenIn = single && canEdit;
        c.CanPin = singleDir;
        c.CanTerminal = singleDir;
        c.CanCompress = canEdit && !singleArchive;
        c.CanExtract = singleArchive;
        c.CanHash = singleFile;
        c.CanRunAsAdmin = singleRunnable;
        c.CanExplorer = single && allPath;
        c.CanProperties = single && allPath;
        return c;
    }

    public static bool IsRunnablePath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;
        string ext;
        try { ext = Path.GetExtension(path); }
        catch { return false; }
        if (string.IsNullOrEmpty(ext))
            return false;
        return string.Equals(ext, ".exe", StringComparison.OrdinalIgnoreCase)
            || string.Equals(ext, ".bat", StringComparison.OrdinalIgnoreCase)
            || string.Equals(ext, ".cmd", StringComparison.OrdinalIgnoreCase)
            || string.Equals(ext, ".ps1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(ext, ".msi", StringComparison.OrdinalIgnoreCase);
    }
}
