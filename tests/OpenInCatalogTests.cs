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
        FamilyForExtensionEveryday();
        HandlersVideoExcludesOffice();
        HandlersPdfExcludesVlcUnlessAssoc();
        HandlersFolderEditorsOnly();
        HandlersDedupesByExe();
        LaunchRejectsBadInput();
        LaunchByExePathWorks();
        AssocLaunchUsesShellOpen();
        MatchesFamilyHelper();

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
            Expect(apps[i].Families != 0, "app has family");
        }
    }

    private static void FamilyForExtensionEveryday()
    {
        Expect(OpenInCatalog.FamilyForExtension(".mp4") == OpenAppFamily.Video, "mp4 video");
        Expect(OpenInCatalog.FamilyForExtension(".MP3") == OpenAppFamily.Audio, "mp3 audio");
        Expect(OpenInCatalog.FamilyForExtension(".png") == OpenAppFamily.Image, "png image");
        Expect(OpenInCatalog.FamilyForExtension(".pdf") == OpenAppFamily.Pdf, "pdf");
        Expect(OpenInCatalog.FamilyForExtension(".xlsx") == OpenAppFamily.Spreadsheet, "xlsx");
        Expect(OpenInCatalog.FamilyForExtension(".docx") == OpenAppFamily.Document, "docx");
        Expect(OpenInCatalog.FamilyForExtension(".pptx") == OpenAppFamily.Presentation, "pptx");
        Expect(OpenInCatalog.FamilyForExtension(".cs") == OpenAppFamily.Editor, "cs editor");
        Expect(OpenInCatalog.FamilyForExtension(".xyz") == OpenAppFamily.None, "unknown none");
    }

    private static void HandlersVideoExcludesOffice()
    {
        string path = Path.Combine(Path.GetTempPath(), "qb-open-" + Guid.NewGuid().ToString("N") + ".mp4");
        File.WriteAllText(path, "x");
        try
        {
            IList<OpenInApp> handlers = OpenInCatalog.HandlersFor(path);
            Expect(handlers != null, "video handlers non-null");
            for (int i = 0; i < handlers.Count; i++)
            {
                OpenInApp a = handlers[i];
                if (string.Equals(a.Id, "assoc", StringComparison.OrdinalIgnoreCase)
                    || a.Id.StartsWith("ow:", StringComparison.OrdinalIgnoreCase))
                    continue;
                Expect(!string.Equals(a.Id, "excel", StringComparison.OrdinalIgnoreCase), "video no excel");
                Expect(!string.Equals(a.Id, "winword", StringComparison.OrdinalIgnoreCase), "video no word");
                Expect(OpenInCatalog.MatchesFamily(a, (int)(OpenAppFamily.Video | OpenAppFamily.Audio))
                    || a.Families == 0, "video curated is media");
            }
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }

    private static void HandlersPdfExcludesVlcUnlessAssoc()
    {
        string path = Path.Combine(Path.GetTempPath(), "qb-open-" + Guid.NewGuid().ToString("N") + ".pdf");
        File.WriteAllText(path, "%PDF");
        try
        {
            IList<OpenInApp> handlers = OpenInCatalog.HandlersFor(path);
            Expect(handlers != null, "pdf handlers non-null");
            for (int i = 0; i < handlers.Count; i++)
            {
                OpenInApp a = handlers[i];
                if (string.Equals(a.Id, "assoc", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (a.Id.StartsWith("ow:", StringComparison.OrdinalIgnoreCase))
                    continue;
                Expect(!string.Equals(a.Id, "vlc", StringComparison.OrdinalIgnoreCase), "pdf no curated vlc");
                Expect(!string.Equals(a.Id, "potplayer", StringComparison.OrdinalIgnoreCase), "pdf no potplayer");
            }
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }

    private static void HandlersFolderEditorsOnly()
    {
        string dir = Path.Combine(Path.GetTempPath(), "qb-open-dir-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            IList<OpenInApp> handlers = OpenInCatalog.HandlersFor(dir);
            Expect(handlers != null, "folder handlers");
            for (int i = 0; i < handlers.Count; i++)
            {
                Expect(OpenInCatalog.MatchesFamily(handlers[i], (int)OpenAppFamily.Editor), "folder editor only");
            }
        }
        finally
        {
            try { Directory.Delete(dir); } catch { }
        }
    }

    private static void HandlersDedupesByExe()
    {
        string path = Path.Combine(Path.GetTempPath(), "qb-open-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(path, "x");
        try
        {
            IList<OpenInApp> handlers = OpenInCatalog.HandlersFor(path);
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < handlers.Count; i++)
            {
                Expect(seen.Add(handlers[i].ExePath), "unique exe " + handlers[i].ExePath);
            }
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
        Expect(!OpenInCatalog.LaunchById("assoc", null), "launch null path");
    }

    private static void LaunchByExePathWorks()
    {
        // Synthetic app pointing at notepad — launch is best-effort; we only assert API accepts ExePath.
        string notepad = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "notepad.exe");
        if (!File.Exists(notepad))
        {
            Expect(true, "notepad missing skip");
            return;
        }
        string path = Path.Combine(Path.GetTempPath(), "qb-launch-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(path, "hello");
        try
        {
            OpenInApp app = new OpenInApp
            {
                Id = "assoc",
                Title = "Notepad",
                ExePath = notepad,
                Families = (int)OpenAppFamily.Editor
            };
            Expect(OpenInCatalog.Launch(app, path), "launch by exe path");
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }

    private static void AssocLaunchUsesShellOpen()
    {
        // assoc must not require a resolvable ExePath — shell opens the document.
        string path = Path.Combine(Path.GetTempPath(), "qb-assoc-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(path, "assoc");
        try
        {
            OpenInApp assoc = new OpenInApp
            {
                Id = "assoc",
                Title = "Default",
                ExePath = null,
                Families = (int)OpenAppFamily.Editor
            };
            Expect(OpenInCatalog.Launch(assoc, path), "assoc shell open without exe");
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }

    private static void MatchesFamilyHelper()
    {
        OpenInApp video = new OpenInApp { Families = (int)(OpenAppFamily.Video | OpenAppFamily.Audio) };
        Expect(OpenInCatalog.MatchesFamily(video, (int)OpenAppFamily.Video), "video match");
        Expect(!OpenInCatalog.MatchesFamily(video, (int)OpenAppFamily.Pdf), "video not pdf");
        Expect(!OpenInCatalog.MatchesFamily(null, (int)OpenAppFamily.Video), "null app");
    }

    private static void Expect(bool cond, string name)
    {
        if (cond)
            return;
        _failed++;
        Console.WriteLine("FAIL  " + name);
    }
}
