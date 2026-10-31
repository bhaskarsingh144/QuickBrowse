using System;
using System.IO;

internal static class PinSessionTests
{
    private static int _failed;
    private static string _root;

    private static void Main()
    {
        _root = Path.Combine(Path.GetTempPath(), "QuickBrowsePinSession_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        try
        {
            PinsTogglesFlyout();
            PinsWhilePaneOpenClosesPane();
            PinsToggleClosesFlyoutOnly();
            RecentTogglesAndExcludesPins();
            RecentWhilePaneOpenClosesPane();
            OpenPinClosesFlyoutSetsPath();
            OpenRecentFolder();
            OpenRecentFile();
            OpenPlaceClosesFlyout();
            RetapSamePlaceClosesPane();
            PinAndUnpinCurrent();
            RejectPinFile();
            MissingPinSoftFail();
            IdleResets();
            ParkClosesFlyout();
            ActivityNotes();
        }
        finally
        {
            try { Directory.Delete(_root, true); }
            catch { }
        }
        if (_failed != 0)
            Environment.Exit(1);
        Console.WriteLine("PinSessionTests OK");
    }

    private static PinSession NewSession()
    {
        string dir = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return new PinSession(new PinStore(dir));
    }

    private static void PinsTogglesFlyout()
    {
        PinSession s = NewSession();
        Expect(!s.FlyoutOpen, "start closed");
        s.TogglePins();
        Expect(s.PinsFlyoutOpen, "pins opens");
        Expect(!s.PaneOpen, "pins does not open pane");
        s.TogglePins();
        Expect(!s.FlyoutOpen, "pins closes");
    }

    private static void PinsWhilePaneOpenClosesPane()
    {
        PinSession s = NewSession();
        s.OpenPlace(2, "downloads", @"C:\Users\Test\Downloads");
        Expect(s.PaneOpen, "pane open before pins");
        Expect(s.ActiveKind == PinSessionActive.Place, "place active");
        s.TogglePins();
        Expect(s.PinsFlyoutOpen, "flyout open");
        Expect(!s.PaneOpen, "pane closed by pins");
        Expect(s.ActiveKind == PinSessionActive.None, "active cleared");
        Expect(s.ActivePlaceIndex < 0, "place index cleared");
    }

    private static void PinsToggleClosesFlyoutOnly()
    {
        PinSession s = NewSession();
        s.OpenPlace(1, "desktop", @"C:\Users\Test\Desktop");
        s.TogglePins();
        Expect(s.PinsFlyoutOpen && !s.PaneOpen, "pins opened over closed pane");
        s.TogglePins();
        Expect(!s.FlyoutOpen, "second pins closes flyout");
        Expect(!s.PaneOpen, "pane stays closed");
        Expect(s.ActiveKind == PinSessionActive.None, "still cleared");
    }

    private static void RecentTogglesAndExcludesPins()
    {
        PinSession s = NewSession();
        s.TogglePins();
        Expect(s.PinsFlyoutOpen, "pins open");
        s.ToggleRecent();
        Expect(s.RecentFlyoutOpen, "switched to recent");
        Expect(!s.PinsFlyoutOpen, "pins closed");
        Expect(!s.PaneOpen, "pane still closed");
        s.ToggleRecent();
        Expect(!s.RecentFlyoutOpen && !s.FlyoutOpen, "recent closes");
    }

    private static void RecentWhilePaneOpenClosesPane()
    {
        PinSession s = NewSession();
        s.OpenPlace(2, "downloads", @"C:\Users\Test\Downloads");
        s.ToggleRecent();
        Expect(s.RecentFlyoutOpen, "recent open");
        Expect(!s.PaneOpen, "pane closed by recent");
        Expect(s.ActiveKind == PinSessionActive.None, "cleared");
    }

    private static void OpenPinClosesFlyoutSetsPath()
    {
        string dir = Path.Combine(_root, "proj");
        Directory.CreateDirectory(dir);
        PinSession s = NewSession();
        s.Store.Add(dir);
        s.TogglePins();
        Expect(s.FlyoutOpen, "flyout open before pin");
        bool ok = s.OpenPin(dir);
        Expect(ok, "open pin ok");
        Expect(!s.FlyoutOpen, "flyout closed after pin");
        Expect(s.PaneOpen, "pane open");
        Expect(s.ActiveKind == PinSessionActive.Pin, "active is pin");
        Expect(PathsEqual(s.ActivePath, dir), "active path");
        Expect(s.ActivePlaceIndex < 0, "no place index");
    }

    private static void OpenRecentFolder()
    {
        string dir = Path.Combine(_root, "recent_dir");
        Directory.CreateDirectory(dir);
        PinSession s = NewSession();
        s.ToggleRecent();
        bool ok = s.OpenRecentPath(dir);
        Expect(ok, "open recent folder");
        Expect(s.LastRecentActivate == RecentActivateKind.Folder, "kind folder");
        Expect(!s.RecentFlyoutOpen && !s.FlyoutOpen, "recent closed");
        Expect(s.PaneOpen, "pane open");
        Expect(PathsEqual(s.ActivePath, dir), "path");
    }

    private static void OpenRecentFile()
    {
        string file = Path.Combine(_root, "recent_note.txt");
        File.WriteAllText(file, "x");
        PinSession s = NewSession();
        s.ToggleRecent();
        bool ok = s.OpenRecentPath(file);
        Expect(ok, "open recent file");
        Expect(s.LastRecentActivate == RecentActivateKind.File, "kind file");
        Expect(PathsEqual(s.LastRecentPath, file), "launch path");
        Expect(!s.RecentFlyoutOpen && !s.FlyoutOpen, "recent closed");
        Expect(!s.PaneOpen, "pane stays closed for file");
    }

    private static void OpenPlaceClosesFlyout()
    {
        PinSession s = NewSession();
        s.TogglePins();
        s.OpenPlace(2, "downloads", @"C:\Users\Test\Downloads");
        Expect(!s.FlyoutOpen, "flyout closed on place");
        Expect(s.PaneOpen, "pane open");
        Expect(s.ActiveKind == PinSessionActive.Place, "active place");
        Expect(s.ActivePlaceIndex == 2, "place index");
    }

    private static void RetapSamePlaceClosesPane()
    {
        PinSession s = NewSession();
        s.OpenPlace(1, "desktop", @"C:\Users\Test\Desktop");
        Expect(s.PaneOpen, "pane open");
        s.OpenPlace(1, "desktop", @"C:\Users\Test\Desktop");
        Expect(!s.PaneOpen, "retap closes pane");
        Expect(s.ActiveKind == PinSessionActive.None, "cleared");
        Expect(s.ActivePlaceIndex < 0, "index cleared");
    }

    private static void PinAndUnpinCurrent()
    {
        string dir = Path.Combine(_root, "cur");
        Directory.CreateDirectory(dir);
        PinSession s = NewSession();
        s.OpenPath(dir);
        Expect(s.CanPinCurrent, "can pin folder");
        Expect(s.PinCurrent(), "pin current");
        Expect(s.Store.Contains(dir), "store has path");
        Expect(s.IsCurrentPinned, "toolbar shows pinned");
        Expect(s.UnpinCurrent(), "unpin");
        Expect(!s.Store.Contains(dir), "store cleared");
        Expect(!s.IsCurrentPinned, "toolbar unpinned");
    }

    private static void RejectPinFile()
    {
        string file = Path.Combine(_root, "note.txt");
        File.WriteAllText(file, "x");
        PinSession s = NewSession();
        s.OpenPath(file);
        Expect(!s.CanPinCurrent, "cannot pin file");
        Expect(!s.PinCurrent(), "pin file rejected");
        Expect(s.Store.List().Count == 0, "store empty");
    }

    private static void MissingPinSoftFail()
    {
        PinSession s = NewSession();
        string missing = Path.Combine(_root, "gone_folder_xyz");
        s.Store.Add(missing);
        bool ok = s.OpenPin(missing);
        Expect(!ok, "missing open fails");
        Expect(!string.IsNullOrEmpty(s.LastError), "soft message set");
        Expect(!s.PaneOpen || s.ActiveKind != PinSessionActive.Pin, "pane not showing missing pin");
    }

    private static void IdleResets()
    {
        string dir = Path.Combine(_root, "idle");
        Directory.CreateDirectory(dir);
        PinSession s = NewSession();
        s.Store.Add(dir);
        s.TogglePins();
        s.OpenPin(dir);
        Expect(s.PaneOpen && !s.FlyoutOpen, "setup");
        s.ToggleRecent();
        Expect(s.RecentFlyoutOpen, "recent flyout");
        s.IdleReset();
        Expect(!s.FlyoutOpen && !s.RecentFlyoutOpen, "idle closes flyouts");
        Expect(!s.PaneOpen, "idle closes pane");
        Expect(s.ActiveKind == PinSessionActive.None, "idle clears active");
    }

    private static void ParkClosesFlyout()
    {
        PinSession s = NewSession();
        s.ToggleRecent();
        Expect(s.RecentFlyoutOpen, "open");
        s.OnPark();
        Expect(!s.RecentFlyoutOpen && !s.FlyoutOpen, "park closes recent");
    }

    private static void ActivityNotes()
    {
        PinSession s = NewSession();
        DateTime before = s.LastActivityUtc;
        System.Threading.Thread.Sleep(20);
        s.NoteActivity();
        Expect(s.LastActivityUtc > before, "activity advances");
    }

    private static bool PathsEqual(string a, string b)
    {
        string na = PinStore.Normalize(a);
        string nb = PinStore.Normalize(b);
        return string.Equals(na, nb, StringComparison.OrdinalIgnoreCase);
    }

    private static void Expect(bool ok, string label)
    {
        if (ok)
            return;
        _failed++;
        Console.Error.WriteLine("FAIL: " + label);
    }
}
