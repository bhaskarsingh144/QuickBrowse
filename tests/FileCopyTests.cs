using System;
using System.IO;

internal static class FileCopyTests
{
    private static int _failed;

    private static void Main()
    {
        UniqueDestSuffix();
        CopyFileInto();

        if (_failed != 0)
            Environment.Exit(1);
        Console.WriteLine("FileCopyTests OK");
    }

    private static void UniqueDestSuffix()
    {
        string dir = Path.Combine(Path.GetTempPath(), "qb-filecopy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "a.txt"), "1");
            string second = FileCopy.UniqueDest(dir, "a.txt", false);
            Expect(string.Equals(Path.GetFileName(second), "a (2).txt", StringComparison.OrdinalIgnoreCase), "unique (2)");
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    private static void CopyFileInto()
    {
        string dir = Path.Combine(Path.GetTempPath(), "qb-filecopy2-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string src = Path.Combine(Path.GetTempPath(), "qb-src-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(src, "x");
        try
        {
            string error;
            string dest = FileCopy.CopyInto(dir, src, out error);
            Expect(dest != null && File.Exists(dest), "copy into");
            Expect(File.ReadAllText(dest) == "x", "copy content");
        }
        finally
        {
            try { File.Delete(src); } catch { }
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    private static void Expect(bool cond, string name)
    {
        if (cond)
            return;
        _failed++;
        Console.WriteLine("FAIL  " + name);
    }
}
