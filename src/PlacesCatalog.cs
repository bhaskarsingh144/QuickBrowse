using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

internal enum PlaceKind
{
    ThisPc,
    KnownFolder,
    Profile
}

internal sealed class PlaceEntry
{
    public string Id;
    public string Title;
    public PlaceKind Kind;
    public string Path;
    public Guid KnownFolderId;
}

internal static class PlacesCatalog
{
    // FOLDERID_* from KnownFolders.h
    private static readonly Guid FolderDesktop = new Guid("B4BFCC3A-DB2C-424C-B029-7FE99A87C641");
    private static readonly Guid FolderDocuments = new Guid("FDD39AD0-238F-46AF-ADB4-6C85480369C7");
    private static readonly Guid FolderDownloads = new Guid("374DE290-123F-4565-9164-39C4925E467B");
    private static readonly Guid FolderPictures = new Guid("33E28130-4E1E-4676-835A-98395C3BC3BB");
    private static readonly Guid FolderMusic = new Guid("4BD8D571-6D19-48D3-BE97-422220080E43");
    private static readonly Guid FolderVideos = new Guid("18989B1D-99B5-455B-841C-AB7C74E4DDFC");
    private static readonly Guid FolderProfile = new Guid("5E6C858F-0E22-4760-9AFE-EA3317B67173");

    public static IList<PlaceEntry> DefaultPlaces()
    {
        List<PlaceEntry> list = new List<PlaceEntry>();
        list.Add(new PlaceEntry
        {
            Id = "thispc",
            Title = "This PC",
            Kind = PlaceKind.ThisPc,
            Path = null
        });
        AddKnown(list, "desktop", "Desktop", FolderDesktop);
        AddKnown(list, "downloads", "Downloads", FolderDownloads);
        AddKnown(list, "documents", "Documents", FolderDocuments);
        AddKnown(list, "pictures", "Pictures", FolderPictures);
        AddKnown(list, "music", "Music", FolderMusic);
        AddKnown(list, "videos", "Videos", FolderVideos);
        AddKnown(list, "profile", "Profile", FolderProfile, PlaceKind.Profile);
        return list;
    }

    public static string ResolvePath(PlaceEntry place)
    {
        if (place == null)
            return null;
        if (place.Kind == PlaceKind.ThisPc)
            return null;
        if (!string.IsNullOrEmpty(place.Path) && Directory.Exists(place.Path))
            return place.Path;
        if (place.KnownFolderId != Guid.Empty)
        {
            string path = KnownFolderPath(place.KnownFolderId);
            if (!string.IsNullOrEmpty(path))
                return path;
        }
        return place.Path;
    }

    public static string KnownFolderPath(Guid folderId)
    {
        IntPtr pPath = IntPtr.Zero;
        try
        {
            int hr = SHGetKnownFolderPath(ref folderId, 0, IntPtr.Zero, out pPath);
            if (hr != 0 || pPath == IntPtr.Zero)
                return null;
            return Marshal.PtrToStringUni(pPath);
        }
        catch
        {
            return null;
        }
        finally
        {
            if (pPath != IntPtr.Zero)
                Marshal.FreeCoTaskMem(pPath);
        }
    }

    private static void AddKnown(List<PlaceEntry> list, string id, string title, Guid folderId)
    {
        AddKnown(list, id, title, folderId, PlaceKind.KnownFolder);
    }

    private static void AddKnown(List<PlaceEntry> list, string id, string title, Guid folderId, PlaceKind kind)
    {
        string path = KnownFolderPath(folderId);
        list.Add(new PlaceEntry
        {
            Id = id,
            Title = title,
            Kind = kind,
            Path = path,
            KnownFolderId = folderId
        });
    }

    [DllImport("shell32.dll")]
    private static extern int SHGetKnownFolderPath(
        ref Guid rfid,
        uint dwFlags,
        IntPtr hToken,
        out IntPtr ppszPath);
}
