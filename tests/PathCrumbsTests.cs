using System;
using System.Collections.Generic;
using System.IO;

internal static class PathCrumbsTests
{
    private static int _failed;

    private static void Main()
    {
        ThisPcOnly();
        DriveAndFolders();
        if (_failed != 0)
            Environment.Exit(1);
        Console.WriteLine("PathCrumbsTests OK");
    }

    private static void ThisPcOnly()
    {
        List<PathCrumbSeg> c = PathCrumbs.Build(true, null);
        Expect(c.Count == 1, "this pc count");
        Expect(c[0].IsThisPc, "this pc flag");
        Expect(c[0].Label == "This PC", "this pc label");
    }

    private static void DriveAndFolders()
    {
        string root = Path.GetPathRoot(Environment.SystemDirectory);
        string users = Path.Combine(root, "Users");
        List<PathCrumbSeg> c = PathCrumbs.Build(false, users);
        Expect(c.Count >= 3, "thispc + drive + Users");
        Expect(c[0].IsThisPc, "root crumb");
        Expect(!c[1].IsThisPc && !string.IsNullOrEmpty(c[1].TargetPath), "drive crumb");
        Expect(string.Equals(c[c.Count - 1].Label, "Users", StringComparison.OrdinalIgnoreCase), "last label Users");
    }

    private static void Expect(bool ok, string label)
    {
        if (ok)
            return;
        _failed++;
        Console.Error.WriteLine("FAIL: " + label);
    }
}
