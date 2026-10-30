using System;
using System.IO;

internal enum BrowseViewMode
{
    Tiles,
    Content,
    Details
}

internal static class Prefs
{
    private static string _testDir;

    /// <summary>Test hook — pass null to restore default AppData path.</summary>
    public static void SetStorageDirForTests(string directory)
    {
        _testDir = directory;
    }

    private static string Dir()
    {
        if (!string.IsNullOrEmpty(_testDir))
            return _testDir;
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "QuickBrowse");
    }

    private static string ViewPath()
    {
        return Path.Combine(Dir(), "view.txt");
    }

    public static BrowseViewMode LoadViewMode()
    {
        try
        {
            string path = ViewPath();
            if (!File.Exists(path))
                return BrowseViewMode.Tiles;
            string text = File.ReadAllText(path).Trim().ToLowerInvariant();
            if (text == "tiles" || text == "tile")
                return BrowseViewMode.Tiles;
            if (text == "content")
                return BrowseViewMode.Content;
            if (text == "details" || text == "detail")
                return BrowseViewMode.Details;
            // Retired modes (icons / list / grid) map to Tiles.
            if (text == "icons" || text == "icon" || text == "grid" || text == "large"
                || text == "list")
                return BrowseViewMode.Tiles;
        }
        catch
        {
        }
        return BrowseViewMode.Tiles;
    }

    public static void SaveViewMode(BrowseViewMode mode)
    {
        try
        {
            Directory.CreateDirectory(Dir());
            string text;
            switch (mode)
            {
                case BrowseViewMode.Content: text = "content"; break;
                case BrowseViewMode.Details: text = "details"; break;
                default: text = "tiles"; break;
            }
            File.WriteAllText(ViewPath(), text);
        }
        catch
        {
        }
    }
}
