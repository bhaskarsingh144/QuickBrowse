using System;
using System.Collections.Generic;

internal static class PlacesCatalogTests
{
    private static int _failed;

    private static void Main()
    {
        DefaultOrder();
        ThisPcHasNoPath();
        DownloadsResolves();
        ExpectedIdsPresent();
        NullPlaceResolve();
        if (_failed != 0)
            Environment.Exit(1);
        Console.WriteLine("PlacesCatalogTests OK");
    }

    private static void DefaultOrder()
    {
        IList<PlaceEntry> places = PlacesCatalog.DefaultPlaces();
        Expect(places.Count >= 8, "at least 8 default places");
        Expect(places[0].Kind == PlaceKind.ThisPc && places[0].Id == "thispc", "This PC first");
        Expect(places[1].Id == "desktop", "Desktop second");
        Expect(places[2].Id == "downloads", "Downloads third");
    }

    private static void ThisPcHasNoPath()
    {
        IList<PlaceEntry> places = PlacesCatalog.DefaultPlaces();
        Expect(PlacesCatalog.ResolvePath(places[0]) == null, "This PC resolve is null");
    }

    private static void DownloadsResolves()
    {
        IList<PlaceEntry> places = PlacesCatalog.DefaultPlaces();
        PlaceEntry downloads = null;
        foreach (PlaceEntry p in places)
        {
            if (p.Id == "downloads")
            {
                downloads = p;
                break;
            }
        }
        Expect(downloads != null, "downloads place exists");
        string path = PlacesCatalog.ResolvePath(downloads);
        Expect(!string.IsNullOrEmpty(path), "downloads path resolves: " + path);
    }

    private static void ExpectedIdsPresent()
    {
        IList<PlaceEntry> places = PlacesCatalog.DefaultPlaces();
        string[] need = new string[] { "thispc", "desktop", "downloads", "documents", "pictures", "music", "videos", "profile" };
        for (int i = 0; i < need.Length; i++)
        {
            bool found = false;
            for (int j = 0; j < places.Count; j++)
            {
                if (places[j].Id == need[i])
                {
                    found = true;
                    Expect(!string.IsNullOrEmpty(places[j].Title), need[i] + " has title");
                    break;
                }
            }
            Expect(found, "has id " + need[i]);
        }
    }

    private static void NullPlaceResolve()
    {
        Expect(PlacesCatalog.ResolvePath(null) == null, "null place → null path");
    }

    private static void Expect(bool ok, string label)
    {
        if (ok)
            return;
        _failed++;
        Console.Error.WriteLine("FAIL: " + label);
    }
}
