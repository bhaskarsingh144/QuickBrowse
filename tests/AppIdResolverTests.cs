using System;
using System.IO;

internal static class AppIdResolverTests
{
    private static int _failed;

    private static void Main()
    {
        AppIdResolver.ClearCacheForTests();

        ExplorerCuratedId();
        NullAndEmptySafe();
        UnknownExeFallsBackToPath();
        CuratedVlcAndPotPlayer();
        DefaultHandlerExeMissingExt();

        if (_failed != 0)
            Environment.Exit(1);
        Console.WriteLine("AppIdResolverTests OK");
    }

    private static void ExplorerCuratedId()
    {
        string id = AppIdResolver.ResolveForExe(@"C:\Windows\explorer.exe");
        Expect(string.Equals(id, AppIdResolver.ExplorerAppId, StringComparison.Ordinal), "explorer AUMID");
    }

    private static void NullAndEmptySafe()
    {
        Expect(AppIdResolver.ResolveForExe(null) == null, "null exe");
        Expect(AppIdResolver.ResolveForExe("") == null, "empty exe");
        Expect(AppIdResolver.DefaultHandlerExe(null) == null, "null doc");
        Expect(AppIdResolver.DefaultHandlerExe("") == null, "empty doc");
        Expect(AppIdResolver.ResolveForDocument(null) == null, "null resolve doc");
    }

    private static void UnknownExeFallsBackToPath()
    {
        string fake = Path.Combine(Path.GetTempPath(), "QB_FakeApp_" + Guid.NewGuid().ToString("N") + ".exe");
        string id = AppIdResolver.ResolveForExe(fake);
        Expect(!string.IsNullOrEmpty(id), "fallback non-empty");
        Expect(id.IndexOf("QB_FakeApp_", StringComparison.OrdinalIgnoreCase) >= 0, "fallback uses path");
    }

    private static void CuratedVlcAndPotPlayer()
    {
        string vlc = AppIdResolver.ResolveForExe(@"C:\Program Files\VideoLAN\VLC\vlc.exe");
        Expect(string.Equals(vlc, "VLC.VLCMediaPlayer", StringComparison.Ordinal), "vlc curated");
        string pot = AppIdResolver.ResolveForExe(@"C:\Program Files\DAUM\PotPlayer\PotPlayerMini64.exe");
        Expect(string.Equals(pot, "PotPlayer", StringComparison.Ordinal), "potplayer curated");
    }

    private static void DefaultHandlerExeMissingExt()
    {
        Expect(AppIdResolver.DefaultHandlerExe(@"C:\noext") == null, "no extension");
        Expect(AppIdResolver.DefaultHandlerExe(@"C:\.") == null, "dot only");
    }

    private static void Expect(bool cond, string name)
    {
        if (cond)
            return;
        _failed++;
        Console.WriteLine("FAIL  " + name);
    }
}
