using System;
using System.IO;

internal enum BrowseViewMode
{
    Icons,
    Tiles,
    Content,
    List,
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
                return BrowseViewMode.Icons;
            string text = File.ReadAllText(path).Trim().ToLowerInvariant();
            if (text == "tiles" || text == "tile")
                return BrowseViewMode.Tiles;
            if (text == "content")
                return BrowseViewMode.Content;
            if (text == "list")
                return BrowseViewMode.List;
            if (text == "details" || text == "detail")
                return BrowseViewMode.Details;
            if (text == "icons" || text == "icon" || text == "grid" || text == "large")
                return BrowseViewMode.Icons;
        }
        catch
        {
        }
        return BrowseViewMode.Icons;
    }

    public static void SaveViewMode(BrowseViewMode mode)
    {
        try
        {
            Directory.CreateDirectory(Dir());
            string text;
            switch (mode)
            {
                case BrowseViewMode.Tiles: text = "tiles"; break;
                case BrowseViewMode.Content: text = "content"; break;
                case BrowseViewMode.List: text = "list"; break;
                case BrowseViewMode.Details: text = "details"; break;
                default: text = "icons"; break;
            }
            File.WriteAllText(ViewPath(), text);
        }
        catch
        {
        }
    }
}
