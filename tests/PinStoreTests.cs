using System;
using System.Collections.Generic;
using System.IO;

internal static class PinStoreTests
{
    private static int _failed;
    private static string _root;

    private static void Main()
    {
        _root = Path.Combine(Path.GetTempPath(), "QuickBrowsePinTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        try
        {
            EmptyLoad();
            AddFolderPreservesOrder();
            DuplicateIsNoOp();
            RemoveByPath();
            ContainsNormalized();
            RoundTrip();
            CorruptFileYieldsEmpty();
            MissingFileYieldsEmpty();
            RejectBadPaths();
            DisplayNames();
        }
        finally
        {
            try { Directory.Delete(_root, true); }
            catch { }
        }

        if (_failed != 0)
            Environment.Exit(1);
        Console.WriteLine("PinStoreTests OK");
    }

    private static PinStore Store()
    {
        return new PinStore(_root);
    }

    private static void EmptyLoad()
    {
        PinStore store = Store();
        IList<PinEntry> list = store.List();
        Expect(list != null && list.Count == 0, "empty load");
    }

    private static void AddFolderPreservesOrder()
    {
        string dir = Path.Combine(_root, "data_add");
        Directory.CreateDirectory(dir);
        PinStore store = new PinStore(dir);
        Expect(store.Add(@"C:\Projects\Alpha"), "add Alpha");
        Expect(store.Add(@"C:\Projects\Beta"), "add Beta");
        IList<PinEntry> list = store.List();
        Expect(list.Count == 2, "two pins");
        Expect(list[0].Path.Equals(@"C:\Projects\Alpha", StringComparison.OrdinalIgnoreCase), "Alpha first");
        Expect(list[1].Path.Equals(@"C:\Projects\Beta", StringComparison.OrdinalIgnoreCase), "Beta second");
        Expect(list[0].Title == "Alpha", "Alpha title");
        Expect(list[1].Title == "Beta", "Beta title");
    }

    private static void DuplicateIsNoOp()
    {
        string dir = Path.Combine(_root, "data_dup");
        Directory.CreateDirectory(dir);
        PinStore store = new PinStore(dir);
        Expect(store.Add(@"C:\Work\Repo"), "first add");
        Expect(!store.Add(@"c:\work\repo"), "duplicate rejected");
        Expect(store.List().Count == 1, "still one pin");
    }

    private static void RemoveByPath()
    {
        string dir = Path.Combine(_root, "data_rm");
        Directory.CreateDirectory(dir);
        PinStore store = new PinStore(dir);
        store.Add(@"C:\A");
        store.Add(@"C:\B");
        Expect(store.Remove(@"c:\a"), "remove A");
        Expect(!store.Remove(@"C:\Missing"), "remove missing no-op false");
        Expect(store.List().Count == 1, "one left");
        Expect(store.List()[0].Path.Equals(@"C:\B", StringComparison.OrdinalIgnoreCase), "B remains");
    }

    private static void ContainsNormalized()
    {
        string dir = Path.Combine(_root, "data_has");
        Directory.CreateDirectory(dir);
        PinStore store = new PinStore(dir);
        store.Add(@"C:\Users\Me\Docs");
        Expect(store.Contains(@"c:\users\me\docs"), "contains ignore case");
        Expect(store.Contains(@"C:\Users\Me\Docs\"), "contains trailing slash");
        Expect(!store.Contains(@"C:\Users\Me\Other"), "missing false");
    }

    private static void RoundTrip()
    {
        string dir = Path.Combine(_root, "data_rt");
        Directory.CreateDirectory(dir);
        PinStore a = new PinStore(dir);
        a.Add(@"D:\Games");
        a.Add(@"E:\Photos\2024");
        PinStore b = new PinStore(dir);
        IList<PinEntry> list = b.List();
        Expect(list.Count == 2, "reload count");
        Expect(list[0].Title == "Games", "reload title 0");
        Expect(list[1].Path.IndexOf("Photos", StringComparison.OrdinalIgnoreCase) >= 0, "reload path 1");
    }

    private static void CorruptFileYieldsEmpty()
    {
        string dir = Path.Combine(_root, "data_bad");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "pins.json"), "{not-json!!!");
        PinStore store = new PinStore(dir);
        Expect(store.List().Count == 0, "corrupt → empty");
    }

    private static void MissingFileYieldsEmpty()
    {
        string dir = Path.Combine(_root, "data_miss");
        Directory.CreateDirectory(dir);
        PinStore store = new PinStore(dir);
        Expect(store.List().Count == 0, "missing file → empty");
    }

    private static void RejectBadPaths()
    {
        string dir = Path.Combine(_root, "data_badpath");
        Directory.CreateDirectory(dir);
        PinStore store = new PinStore(dir);
        Expect(!store.Add(null), "reject null");
        Expect(!store.Add(""), "reject empty");
        Expect(!store.Add("   "), "reject whitespace");
        Expect(store.List().Count == 0, "still empty after rejects");
    }

    private static void DisplayNames()
    {
        Expect(PinStore.TitleFromPath(@"C:\Projects\QuickBrowse") == "QuickBrowse", "folder name");
        Expect(PinStore.TitleFromPath(@"C:\") == "C:\\" || PinStore.TitleFromPath(@"C:\") == "C:", "drive root label");
        Expect(PinStore.TitleFromPath(@"D:\") != null && PinStore.TitleFromPath(@"D:\").Length > 0, "D drive label");
    }

    private static void Expect(bool ok, string label)
    {
        if (ok)
            return;
        _failed++;
        Console.Error.WriteLine("FAIL: " + label);
    }
}
