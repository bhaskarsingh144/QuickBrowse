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

    private static string HiddenPath()
    {
        return Path.Combine(Dir(), "hidden.txt");
    }

    private static string SortPath()
    {
        return Path.Combine(Dir(), "sort.txt");
    }

    public static bool LoadShowHidden()
    {
        try
        {
            string path = HiddenPath();
            if (!File.Exists(path))
                return false;
            string text = File.ReadAllText(path).Trim().ToLowerInvariant();
            return text == "1" || text == "true" || text == "yes" || text == "on";
        }
        catch
        {
        }
        return false;
    }

    public static void SaveShowHidden(bool show)
    {
        try
        {
            Directory.CreateDirectory(Dir());
            File.WriteAllText(HiddenPath(), show ? "1" : "0");
        }
        catch
        {
        }
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

    /// <summary>Details-column sort. column is 0–3; asc true = ascending.</summary>
    public static void LoadSort(out int column, out bool ascending)
    {
        column = 0;
        ascending = true;
        try
        {
            string path = SortPath();
            if (!File.Exists(path))
                return;
            string text = File.ReadAllText(path).Trim();
            if (string.IsNullOrEmpty(text))
                return;
            string[] parts = text.Split(',');
            if (parts.Length < 1)
                return;
            int col;
            if (!int.TryParse(parts[0].Trim(), out col))
                return;
            if (col < 0) col = 0;
            if (col > 3) col = 3;
            column = col;
            if (parts.Length >= 2)
            {
                string a = parts[1].Trim().ToLowerInvariant();
                ascending = !(a == "0" || a == "desc" || a == "false");
            }
        }
        catch
        {
        }
    }

    public static void SaveSort(int column, bool ascending)
    {
        try
        {
            if (column < 0) column = 0;
            if (column > 3) column = 3;
            Directory.CreateDirectory(Dir());
            File.WriteAllText(SortPath(), column + "," + (ascending ? "1" : "0"));
        }
        catch
        {
        }
    }
}
