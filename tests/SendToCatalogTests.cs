using System;
using System.Collections.Generic;
using System.IO;

internal static class SendToCatalogTests
{
    private static int _failed;

    private static void Main()
    {
        DestinationsAvailable();
        CopyToDesktop();
        CopyRejectsBadId();

        if (_failed != 0)
            Environment.Exit(1);
        Console.WriteLine("SendToCatalogTests OK");
    }

    private static void DestinationsAvailable()
    {
        IList<SendToDest> dests = SendToCatalog.Destinations();
        Expect(dests != null && dests.Count >= 1, "at least one destination");
        bool hasDesktop = false;
        for (int i = 0; i < dests.Count; i++)
        {
            Expect(!string.IsNullOrEmpty(dests[i].Id), "dest id");
            Expect(!string.IsNullOrEmpty(dests[i].Path) && Directory.Exists(dests[i].Path), "dest path exists");
            if (dests[i].Id == "desktop")
                hasDesktop = true;
        }
        Expect(hasDesktop, "desktop destination present");
    }

    private static void CopyToDesktop()
    {
        string src = Path.Combine(Path.GetTempPath(), "qb-sendto-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(src, "send");
        string copied = null;
        try
        {
            string error;
            copied = SendToCatalog.CopyTo("desktop", src, out error);
            Expect(copied != null && File.Exists(copied), "copy to desktop");
            Expect(File.ReadAllText(copied) == "send", "desktop content");
        }
        finally
        {
            try { File.Delete(src); } catch { }
            if (!string.IsNullOrEmpty(copied))
            {
                try { File.Delete(copied); } catch { }
            }
        }
    }

    private static void CopyRejectsBadId()
    {
        string error;
        Expect(SendToCatalog.CopyTo("nope", @"C:\Windows\notepad.exe", out error) == null, "bad id");
        Expect(!string.IsNullOrEmpty(error), "bad id error");
    }

    private static void Expect(bool cond, string name)
    {
        if (cond)
            return;
        _failed++;
        Console.WriteLine("FAIL  " + name);
    }
}
