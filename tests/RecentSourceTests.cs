using System;
using System.Collections.Generic;
using System.IO;

internal static class RecentSourceTests
{
    private static int _failed;

    private static void Main()
    {
        BuildListEmpty();
        BuildListDropsExe();
        BuildListDedupesNewestWins();
        BuildListCaps();
        LoadMissingFolderEmpty();
        IsExecutableExt();
        ToPinEntriesMaps();

        if (_failed != 0)
            Environment.Exit(1);
        Console.WriteLine("RecentSourceTests OK");
    }

    private static void BuildListEmpty()
    {
        IList<RecentEntry> r = RecentSource.BuildList(null, 10);
        Expect(r != null && r.Count == 0, "null raw empty");
        r = RecentSource.BuildList(new List<RecentEntry>(), 10);
        Expect(r.Count == 0, "empty raw empty");
    }

    private static void BuildListDropsExe()
    {
        List<RecentEntry> raw = new List<RecentEntry>();
        raw.Add(Entry(@"C:\Windows\notepad.exe", false, DateTime.UtcNow));
        raw.Add(Entry(@"C:\Users\x\doc.txt", false, DateTime.UtcNow.AddMinutes(-1)));
        raw.Add(Entry(@"C:\Users\x\Work", true, DateTime.UtcNow.AddMinutes(-2)));
        IList<RecentEntry> r = RecentSource.BuildList(raw, 24);
        Expect(r.Count == 2, "exe dropped count");
        Expect(!RecentSource.IsExecutable(r[0].Path), "first not exe");
        Expect(!RecentSource.IsExecutable(r[1].Path), "second not exe");
    }

    private static void BuildListDedupesNewestWins()
    {
        DateTime newer = DateTime.UtcNow;
        DateTime older = newer.AddHours(-1);
        List<RecentEntry> raw = new List<RecentEntry>();
        raw.Add(Entry(@"C:\Data\file.txt", false, older));
        raw.Add(Entry(@"C:\Data\file.txt", false, newer));
        IList<RecentEntry> r = RecentSource.BuildList(raw, 24);
        Expect(r.Count == 1, "dedupe one");
        Expect(r[0].LastAccessUtc == newer, "newest kept");
    }

    private static void BuildListCaps()
    {
        List<RecentEntry> raw = new List<RecentEntry>();
        DateTime t = DateTime.UtcNow;
        for (int i = 0; i < 30; i++)
            raw.Add(Entry(@"C:\T\item" + i + ".txt", false, t.AddMinutes(-i)));
        IList<RecentEntry> r = RecentSource.BuildList(raw, 5);
        Expect(r.Count == 5, "cap 5");
        Expect(r[0].Path.EndsWith("item0.txt", StringComparison.OrdinalIgnoreCase), "newest first");
    }

    private static void LoadMissingFolderEmpty()
    {
        string missing = Path.Combine(Path.GetTempPath(), "QB_Recent_Missing_" + Guid.NewGuid().ToString("N"));
        IList<RecentEntry> r = RecentSource.LoadFromFolder(missing, 10);
        Expect(r != null && r.Count == 0, "missing folder empty");
        r = RecentSource.LoadFromFolder(null, 10);
        Expect(r.Count == 0, "null folder empty");
    }

    private static void IsExecutableExt()
    {
        Expect(RecentSource.IsExecutable(@"C:\a.exe"), "exe");
        Expect(RecentSource.IsExecutable(@"C:\a.CMD"), "cmd");
        Expect(!RecentSource.IsExecutable(@"C:\a.txt"), "txt");
        Expect(!RecentSource.IsExecutable(@"C:\folder"), "folder");
    }

    private static void ToPinEntriesMaps()
    {
        List<RecentEntry> raw = new List<RecentEntry>();
        raw.Add(Entry(@"C:\Users\x\a.txt", false, DateTime.UtcNow));
        IList<PinEntry> pins = RecentSource.ToPinEntries(RecentSource.BuildList(raw, 10));
        Expect(pins.Count == 1, "one pin entry");
        Expect(!pins[0].IsDirectory, "file not directory");
        Expect(!string.IsNullOrEmpty(pins[0].Title), "title set");
    }

    private static RecentEntry Entry(string path, bool dir, DateTime utc)
    {
        return new RecentEntry
        {
            Path = path,
            Title = PinStore.TitleFromPath(path),
            IsDirectory = dir,
            LastAccessUtc = utc
        };
    }

    private static void Expect(bool cond, string name)
    {
        if (cond)
            return;
        _failed++;
        Console.WriteLine("FAIL  " + name);
    }
}
