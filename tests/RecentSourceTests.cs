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
        BucketForTodayYesterdayOlder();
        GroupByDateBucketOrder();
        FormatRelativeTimeShapes();
        DefaultCapRaised();

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
        for (int i = 0; i < 40; i++)
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

    private static void BucketForTodayYesterdayOlder()
    {
        DateTime now = new DateTime(2026, 10, 31, 15, 0, 0); // Friday
        Expect(RecentSource.BucketFor(now.ToUniversalTime(), now) == RecentDateBucket.Today, "today");
        Expect(RecentSource.BucketFor(now.AddDays(-1).ToUniversalTime(), now) == RecentDateBucket.Yesterday, "yesterday");
        Expect(RecentSource.BucketFor(now.AddDays(-3).ToUniversalTime(), now) == RecentDateBucket.EarlierThisWeek, "earlier week");
        Expect(RecentSource.BucketFor(now.AddDays(-20).ToUniversalTime(), now) == RecentDateBucket.Older, "older");
    }

    private static void GroupByDateBucketOrder()
    {
        DateTime now = new DateTime(2026, 10, 31, 12, 0, 0);
        List<RecentEntry> raw = new List<RecentEntry>();
        raw.Add(Entry(@"C:\a\today.txt", false, now.ToUniversalTime()));
        raw.Add(Entry(@"C:\a\old.txt", false, now.AddDays(-14).ToUniversalTime()));
        raw.Add(Entry(@"C:\a\yest.txt", false, now.AddDays(-1).ToUniversalTime()));
        IList<RecentBucketGroup> groups = RecentSource.GroupByDateBucket(raw, now);
        Expect(groups.Count == 3, "three buckets");
        Expect(groups[0].Bucket == RecentDateBucket.Today, "first today");
        Expect(groups[1].Bucket == RecentDateBucket.Yesterday, "second yesterday");
        Expect(groups[2].Bucket == RecentDateBucket.Older, "third older");
        Expect(groups[0].Entries.Count == 1, "today one");
        Expect(string.Equals(groups[0].Title, "Today", StringComparison.Ordinal), "today title");
    }

    private static void FormatRelativeTimeShapes()
    {
        DateTime now = DateTime.UtcNow;
        string just = RecentSource.FormatRelativeTime(now.AddSeconds(-10), now);
        Expect(just == "Just now", "just now");
        string mins = RecentSource.FormatRelativeTime(now.AddMinutes(-12), now);
        Expect(mins == "12m ago", "minutes");
        string yest = RecentSource.FormatRelativeTime(now.AddDays(-1), now);
        Expect(yest.StartsWith("Yesterday ", StringComparison.Ordinal), "yesterday prefix");
    }

    private static void DefaultCapRaised()
    {
        Expect(RecentSource.DefaultCap >= 36, "default cap 36");
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
