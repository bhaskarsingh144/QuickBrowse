/// <summary>Which centered list overlay to show over the browse pane content.</summary>
internal enum BrowseOverlayKind
{
    None = 0,
    Loading = 1,
    Empty = 2,
    NoMatches = 3,
    Error = 4
}

/// <summary>Pure rules for listing overlays (testable without WinForms layout).</summary>
internal static class BrowseOverlayRules
{
    /// <summary>
    /// Decide overlay for the current listing pass.
    /// loading wins; then error; then empty folder; then filter-no-matches; else none.
    /// </summary>
    public static BrowseOverlayKind Decide(bool loading, string error, int entryCount, bool filterActive, int visibleCount)
    {
        if (loading)
            return BrowseOverlayKind.Loading;
        if (!string.IsNullOrEmpty(error))
            return BrowseOverlayKind.Error;
        if (entryCount <= 0)
            return BrowseOverlayKind.Empty;
        if (filterActive && visibleCount <= 0)
            return BrowseOverlayKind.NoMatches;
        return BrowseOverlayKind.None;
    }

    public static string Title(BrowseOverlayKind kind)
    {
        switch (kind)
        {
            case BrowseOverlayKind.Loading: return "Loading…";
            case BrowseOverlayKind.Empty: return "This folder is empty";
            case BrowseOverlayKind.NoMatches: return "No matching items";
            case BrowseOverlayKind.Error: return "Couldn’t open this folder";
            default: return "";
        }
    }

    public static bool ShowsRetry(BrowseOverlayKind kind)
    {
        return kind == BrowseOverlayKind.Error;
    }
}
