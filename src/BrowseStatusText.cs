/// <summary>Status-line formatting for listings and selection (pure, testable).</summary>
internal static class BrowseStatusText
{
    public static string FormatListing(int folders, int files)
    {
        if (folders < 0) folders = 0;
        if (files < 0) files = 0;
        int total = folders + files;
        if (total == 0)
            return "Empty folder";
        if (folders == 0)
            return files == 1 ? "1 file" : (files + " files");
        if (files == 0)
            return folders == 1 ? "1 folder" : (folders + " folders");
        return folders + " folders, " + files + " files";
    }

    public static string FormatFilter(int shown, int total)
    {
        if (total < 0) total = 0;
        if (shown < 0) shown = 0;
        return shown + " of " + total + " items";
    }

    public static string FormatSelected(int selectedCount)
    {
        if (selectedCount <= 1)
            return null;
        return selectedCount + " selected";
    }
}
