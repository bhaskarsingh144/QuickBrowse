using System;
using System.IO;

internal static class CrashNotifyTests
{
    private static int _failed;
    private static string _dir;

    private static void Main()
    {
        _dir = Path.Combine(Path.GetTempPath(), "qb-crashnotify-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        CrashLog.SetStorageDirForTests(_dir);
        try
        {
            UncleanDetectsRunningFlag();
            SessionLifecycleClearsFlag();
            UnackedFatalUntilAck();
            SummarizeReadsTypeAndMessage();
        }
        finally
        {
            CrashLog.SetStorageDirForTests(null);
            try { Directory.Delete(_dir, true); } catch { }
        }

        if (_failed != 0)
            Environment.Exit(1);
        Console.WriteLine("CrashNotifyTests OK");
    }

    private static void UncleanDetectsRunningFlag()
    {
        Expect(!CrashNotify.DetectUncleanPriorSession(), "no flag initially");
        CrashNotify.MarkSessionRunning();
        Expect(CrashNotify.DetectUncleanPriorSession(), "flag means unclean");
        CrashNotify.MarkSessionEnded();
        Expect(!CrashNotify.DetectUncleanPriorSession(), "cleared after end");
    }

    private static void SessionLifecycleClearsFlag()
    {
        CrashNotify.MarkSessionRunning();
        Expect(File.Exists(CrashNotify.RunningFlagPath()), "running file written");
        CrashNotify.MarkSessionEnded();
        Expect(!File.Exists(CrashNotify.RunningFlagPath()), "running file removed");
    }

    private static void UnackedFatalUntilAck()
    {
        Expect(!CrashNotify.HasUnackedFatal(), "no fatal yet");
        CrashLog.WriteFatal(new Exception("notify-me"), "test");
        Expect(CrashNotify.HasUnackedFatal(), "fatal unacked");
        CrashNotify.AcknowledgeFatal();
        Expect(!CrashNotify.HasUnackedFatal(), "acked");
        // New fatal after ack should show again
        System.Threading.Thread.Sleep(20);
        CrashLog.WriteFatal(new Exception("again"), "test2");
        Expect(CrashNotify.HasUnackedFatal(), "new fatal unacked");
        CrashNotify.AcknowledgeFatal();
        Expect(!CrashNotify.HasUnackedFatal(), "acked again");
    }

    private static void SummarizeReadsTypeAndMessage()
    {
        CrashLog.WriteFatal(new InvalidOperationException("summary-boom"), "sum");
        string s = CrashNotify.SummarizeLastFatal(400);
        Expect(s.IndexOf("InvalidOperationException", StringComparison.Ordinal) >= 0, "summary type");
        Expect(s.IndexOf("summary-boom", StringComparison.Ordinal) >= 0, "summary message");
    }

    private static void Expect(bool cond, string name)
    {
        if (cond)
            return;
        _failed++;
        Console.WriteLine("FAIL  " + name);
    }
}
