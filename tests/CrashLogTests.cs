using System;
using System.IO;

internal static class CrashLogTests
{
    private static int _failed;
    private static string _dir;

    private static void Main()
    {
        _dir = Path.Combine(Path.GetTempPath(), "qb-crashlog-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        CrashLog.SetStorageDirForTests(_dir);
        try
        {
            RootUnderTestDir();
            FatalWritesFiles();
            ErrorAppendsAppLog();
            FatalTrimsOldestWhenCapExceeded();
        }
        finally
        {
            CrashLog.SetStorageDirForTests(null);
            try { Directory.Delete(_dir, true); } catch { }
        }

        if (_failed != 0)
            Environment.Exit(1);
        Console.WriteLine("CrashLogTests OK");
    }

    private static void RootUnderTestDir()
    {
        Expect(string.Equals(CrashLog.RootDir(), _dir, StringComparison.OrdinalIgnoreCase), "root uses test dir");
        Expect(CrashLog.AppLogPath().StartsWith(_dir, StringComparison.OrdinalIgnoreCase), "app.log under root");
        Expect(CrashLog.LastFatalPath().EndsWith("last-fatal.log", StringComparison.OrdinalIgnoreCase), "last-fatal name");
    }

    private static void FatalWritesFiles()
    {
        string path = CrashLog.WriteFatal(new InvalidOperationException("boom"), "unit-test");
        Expect(path != null && File.Exists(path), "fatal crash file exists");
        Expect(File.Exists(CrashLog.LastFatalPath()), "last-fatal exists");
        string body = File.ReadAllText(CrashLog.LastFatalPath());
        Expect(body.IndexOf("boom", StringComparison.Ordinal) >= 0, "last-fatal has message");
        Expect(body.IndexOf("unit-test", StringComparison.Ordinal) >= 0, "last-fatal has context");
        Expect(File.Exists(CrashLog.AppLogPath()), "app.log after fatal");
        string app = File.ReadAllText(CrashLog.AppLogPath());
        Expect(app.IndexOf("[FATAL]", StringComparison.Ordinal) >= 0, "app.log FATAL line");
    }

    private static void ErrorAppendsAppLog()
    {
        long before = new FileInfo(CrashLog.AppLogPath()).Length;
        CrashLog.WriteError(new Exception("soft"), "soft-path");
        long after = new FileInfo(CrashLog.AppLogPath()).Length;
        Expect(after > before, "error grows app.log");
        string app = File.ReadAllText(CrashLog.AppLogPath());
        Expect(app.IndexOf("[ERROR]", StringComparison.Ordinal) >= 0, "app.log ERROR line");
        Expect(app.IndexOf("soft-path", StringComparison.Ordinal) >= 0, "error context present");
        // Soft errors must not rewrite last-fatal with "soft"
        string fatal = File.ReadAllText(CrashLog.LastFatalPath());
        Expect(fatal.IndexOf("soft-path", StringComparison.Ordinal) < 0, "error does not clobber last-fatal");
    }

    private static void FatalTrimsOldestWhenCapExceeded()
    {
        // Seed oversized crash_* files then write one more fatal to trigger trim.
        for (int i = 0; i < 8; i++)
        {
            string p = Path.Combine(_dir, "crash_old" + i + "_Pad.log");
            // ~700 KB each → over 5 MB cap
            File.WriteAllText(p, new string('x', 700 * 1024));
            File.SetLastWriteTimeUtc(p, DateTime.UtcNow.AddMinutes(-60 + i));
        }
        CrashLog.WriteFatal(new Exception("trim-me"), "trim");
        long total = 0;
        foreach (string f in Directory.GetFiles(_dir, "crash_*.log"))
            total += new FileInfo(f).Length;
        Expect(total <= 5L * 1024 * 1024, "crash dir under 5MB after trim (got " + total + ")");
        Expect(Directory.GetFiles(_dir, "crash_*.log").Length < 8, "oldest crash files removed");
    }

    private static void Expect(bool cond, string name)
    {
        if (cond)
            return;
        _failed++;
        Console.WriteLine("FAIL  " + name);
    }
}
