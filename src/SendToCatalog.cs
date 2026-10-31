using System;
using System.Collections.Generic;
using System.IO;

internal sealed class SendToDest
{
    public string Id;
    public string Title;
    public string Path;
}

/// <summary>Curated Send to destinations (known folders + removable drives — no shell SendTo COM).</summary>
internal static class SendToCatalog
{
    private static readonly Guid FolderDesktop = new Guid("B4BFCC3A-DB2C-424C-B029-7FE99A87C641");
    private static readonly Guid FolderDocuments = new Guid("FDD39AD0-238F-46AF-ADB4-6C85480369C7");
    private static readonly Guid FolderDownloads = new Guid("374DE290-123F-4565-9164-39C4925E467B");

    public static IList<SendToDest> Destinations()
    {
        List<SendToDest> list = new List<SendToDest>();
        Add(list, "desktop", "Desktop", FolderDesktop);
        Add(list, "documents", "Documents", FolderDocuments);
        Add(list, "downloads", "Downloads", FolderDownloads);
        try
        {
            DriveInfo[] drives = DriveInfo.GetDrives();
            for (int i = 0; i < drives.Length; i++)
            {
                DriveInfo d = drives[i];
                try
                {
                    if (!d.IsReady)
                        continue;
                    if (d.DriveType != DriveType.Removable && d.DriveType != DriveType.Network)
                        continue;
                    string root = d.RootDirectory.FullName;
                    string label = d.VolumeLabel;
                    string title = string.IsNullOrEmpty(label)
                        ? ("Drive " + root.TrimEnd('\\'))
                        : (label + " (" + root.TrimEnd('\\') + ")");
                    list.Add(new SendToDest
                    {
                        Id = "drive:" + root,
                        Title = title,
                        Path = root
                    });
                }
                catch
                {
                }
            }
        }
        catch
        {
        }
        return list;
    }

    public static string CopyTo(string destId, string srcPath, out string error)
    {
        error = null;
        if (string.IsNullOrEmpty(destId) || string.IsNullOrEmpty(srcPath))
        {
            error = "Nothing to send.";
            return null;
        }
        IList<SendToDest> all = Destinations();
        SendToDest match = null;
        for (int i = 0; i < all.Count; i++)
        {
            if (string.Equals(all[i].Id, destId, StringComparison.OrdinalIgnoreCase))
            {
                match = all[i];
                break;
            }
        }
        if (match == null || string.IsNullOrEmpty(match.Path) || !Directory.Exists(match.Path))
        {
            error = "Destination not available.";
            return null;
        }
        return FileCopy.CopyInto(match.Path, srcPath, out error);
    }

    private static void Add(List<SendToDest> list, string id, string title, Guid folderId)
    {
        string path = PlacesCatalog.KnownFolderPath(folderId);
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            return;
        list.Add(new SendToDest { Id = id, Title = title, Path = path });
    }
}
