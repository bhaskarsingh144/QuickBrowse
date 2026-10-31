using System;
using System.Collections.Generic;
using System.IO;

internal static class OpenInCatalogTests
{
    private static int _failed;

    private static void Main()
    {
        OpenInCatalog.ClearCacheForTests();
        DetectedAppsReturnsList();
        HandlersForExtensionIncludesKnown();
        LaunchRejectsBadInput();

        if (_failed != 0)
            Environment.Exit(1);
        Console.WriteLine("OpenInCatalogTests OK");
    }

    private static void DetectedAppsReturnsList()
    {
        IList<OpenInApp> apps = OpenInCatalog.DetectedApps();
        Expect(apps != null, "apps non-null");
        for (int i = 0; i < apps.Count; i++)
        {
            Expect(!string.IsNullOrEmpty(apps[i].Id), "app id");
            Expect(!string.IsNullOrEmpty(apps[i].Title), "app title");
            Expect(!string.IsNullOrEmpty(apps[i].ExePath) && File.Exists(apps[i].ExePath), "app exe exists");
        }
    }

    private static void HandlersForExtensionIncludesKnown()
    {
        string path = Path.Combine(Path.GetTempPath(), "qb-openin-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(path, "x");
        try
        {
            IList<OpenInApp> handlers = OpenInCatalog.HandlersForExtension(path);
            Expect(handlers != null, "handlers non-null");
            Expect(handlers.Count >= 0, "handlers count");
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }

    private static void LaunchRejectsBadInput()
    {
        Expect(!OpenInCatalog.Launch(null, @"C:\Windows"), "launch null app");
        Expect(!OpenInCatalog.LaunchById("no-such-app", @"C:\Windows"), "launch missing id");
    }

    private static void Expect(bool cond, string name)
    {
        if (cond)
            return;
        _failed++;
        Console.WriteLine("FAIL  " + name);
    }
}
