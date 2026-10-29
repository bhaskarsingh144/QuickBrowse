using System;
using System.IO;

internal static class PrefsTests
{
    private static int _failed;
    private static string _dir;
    private static string _viewPath;

    private static void Main()
    {
        _dir = Path.Combine(Path.GetTempPath(), "QuickBrowsePrefs_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _viewPath = Path.Combine(_dir, "view.txt");
        try
        {
            Prefs.SetStorageDirForTests(_dir);
            DefaultWhenMissing();
            RoundTripAllModes();
            LegacyStrings();
            UnknownDefaultsToIcons();
        }
        finally
        {
            Prefs.SetStorageDirForTests(null);
            try { Directory.Delete(_dir, true); }
            catch { }
        }
        if (_failed != 0)
            Environment.Exit(1);
        Console.WriteLine("PrefsTests OK");
    }

    private static void DefaultWhenMissing()
    {
        if (File.Exists(_viewPath))
            File.Delete(_viewPath);
        Expect(Prefs.LoadViewMode() == BrowseViewMode.Icons, "default Icons");
    }

    private static void RoundTripAllModes()
    {
        BrowseViewMode[] modes = new BrowseViewMode[]
        {
            BrowseViewMode.Icons,
            BrowseViewMode.Tiles,
            BrowseViewMode.Content,
            BrowseViewMode.List,
            BrowseViewMode.Details
        };
        for (int i = 0; i < modes.Length; i++)
        {
            Prefs.SaveViewMode(modes[i]);
            Expect(Prefs.LoadViewMode() == modes[i], "round-trip " + modes[i]);
        }
    }

    private static void LegacyStrings()
    {
        File.WriteAllText(_viewPath, "tiles");
        Expect(Prefs.LoadViewMode() == BrowseViewMode.Tiles, "legacy tiles");
        File.WriteAllText(_viewPath, "list");
        Expect(Prefs.LoadViewMode() == BrowseViewMode.List, "legacy list");
        File.WriteAllText(_viewPath, "grid");
        Expect(Prefs.LoadViewMode() == BrowseViewMode.Icons, "legacy grid → Icons");
    }

    private static void UnknownDefaultsToIcons()
    {
        File.WriteAllText(_viewPath, "nope");
        Expect(Prefs.LoadViewMode() == BrowseViewMode.Icons, "unknown → Icons");
    }

    private static void Expect(bool ok, string label)
    {
        if (ok)
            return;
        _failed++;
        Console.Error.WriteLine("FAIL: " + label);
    }
}
