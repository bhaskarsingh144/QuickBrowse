using System;

internal static class BrowseOverlayRulesTests
{
    private static int _failed;

    private static void Main()
    {
        LoadingWins();
        ErrorWinsOverEmpty();
        EmptyFolder();
        NoMatchesWhenFilter();
        NoneWhenItemsVisible();
        TitlesAndRetry();

        if (_failed != 0)
            Environment.Exit(1);
        Console.WriteLine("BrowseOverlayRulesTests OK");
    }

    private static void LoadingWins()
    {
        Expect(BrowseOverlayRules.Decide(true, "err", 0, false, 0) == BrowseOverlayKind.Loading, "loading beats error");
        Expect(BrowseOverlayRules.Decide(true, null, 12, true, 0) == BrowseOverlayKind.Loading, "loading beats filter");
    }

    private static void ErrorWinsOverEmpty()
    {
        Expect(BrowseOverlayRules.Decide(false, "Access denied", 0, false, 0) == BrowseOverlayKind.Error, "error");
        Expect(BrowseOverlayRules.Decide(false, "", 0, false, 0) == BrowseOverlayKind.Empty, "empty string not error");
    }

    private static void EmptyFolder()
    {
        Expect(BrowseOverlayRules.Decide(false, null, 0, false, 0) == BrowseOverlayKind.Empty, "empty");
        Expect(BrowseOverlayRules.Decide(false, null, 0, true, 0) == BrowseOverlayKind.Empty, "empty ignores filter");
    }

    private static void NoMatchesWhenFilter()
    {
        Expect(BrowseOverlayRules.Decide(false, null, 10, true, 0) == BrowseOverlayKind.NoMatches, "filter no matches");
        Expect(BrowseOverlayRules.Decide(false, null, 10, true, 3) == BrowseOverlayKind.None, "filter has matches");
    }

    private static void NoneWhenItemsVisible()
    {
        Expect(BrowseOverlayRules.Decide(false, null, 5, false, 5) == BrowseOverlayKind.None, "normal listing");
    }

    private static void TitlesAndRetry()
    {
        Expect(BrowseOverlayRules.Title(BrowseOverlayKind.Loading) == "Loading…", "loading title");
        Expect(BrowseOverlayRules.Title(BrowseOverlayKind.Empty).Length > 0, "empty title");
        Expect(BrowseOverlayRules.ShowsRetry(BrowseOverlayKind.Error), "retry on error");
        Expect(!BrowseOverlayRules.ShowsRetry(BrowseOverlayKind.Loading), "no retry loading");
        Expect(!BrowseOverlayRules.ShowsRetry(BrowseOverlayKind.Empty), "no retry empty");
    }

    private static void Expect(bool cond, string name)
    {
        if (cond)
        {
            Console.WriteLine("  PASS  " + name);
            return;
        }
        _failed++;
        Console.WriteLine("  FAIL  " + name);
    }
}
