using System;

internal static class BrowseStatusTextTests
{
    private static int _failed;

    private static void Main()
    {
        Expect(BrowseStatusText.FormatListing(0, 0) == "Empty folder", "empty");
        Expect(BrowseStatusText.FormatListing(1, 0) == "1 folder", "one folder");
        Expect(BrowseStatusText.FormatListing(3, 0) == "3 folders", "folders");
        Expect(BrowseStatusText.FormatListing(0, 1) == "1 file", "one file");
        Expect(BrowseStatusText.FormatListing(0, 4) == "4 files", "files");
        Expect(BrowseStatusText.FormatListing(2, 5) == "2 folders, 5 files", "mixed");
        Expect(BrowseStatusText.FormatFilter(3, 10) == "3 of 10 items", "filter");
        Expect(BrowseStatusText.FormatSelected(1) == null, "single selected null");
        Expect(BrowseStatusText.FormatSelected(0) == null, "zero selected null");
        Expect(BrowseStatusText.FormatSelected(4) == "4 selected", "multi selected");

        if (_failed != 0)
            Environment.Exit(1);
        Console.WriteLine("BrowseStatusTextTests OK");
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
