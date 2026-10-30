using System;
using System.IO;

internal static class ZipOpsTests
{
    private static int _failed;

    private static void Main()
    {
        IsZipDetectsExtension();
        CompressAndExtractRoundTrip();
        CompressRejectsMissing();

        if (_failed != 0)
            Environment.Exit(1);
        Console.WriteLine("ZipOpsTests OK");
    }

    private static void IsZipDetectsExtension()
    {
        Expect(ZipOps.IsZipPath(@"C:\a\b.zip"), "zip extension");
        Expect(ZipOps.IsZipPath(@"C:\a\b.ZIP"), "zip case");
        Expect(!ZipOps.IsZipPath(@"C:\a\b.txt"), "non-zip");
        Expect(!ZipOps.IsZipPath(null), "null path");
    }

    private static void CompressAndExtractRoundTrip()
    {
        string root = Path.Combine(Path.GetTempPath(), "qb-zipops-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string src = Path.Combine(root, "hello.txt");
            File.WriteAllText(src, "quickbrowse");
            string error;
            string zip = ZipOps.Compress(src, out error);
            Expect(zip != null && File.Exists(zip), "compress creates zip");
            Expect(string.IsNullOrEmpty(error), "compress no error");

            string folder = ZipOps.ExtractAll(zip, out error);
            Expect(folder != null && Directory.Exists(folder), "extract creates folder");
            string extracted = Path.Combine(folder, "hello.txt");
            Expect(File.Exists(extracted) && File.ReadAllText(extracted) == "quickbrowse", "extract content");
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    private static void CompressRejectsMissing()
    {
        string error;
        Expect(ZipOps.Compress(Path.Combine(Path.GetTempPath(), "no-such-" + Guid.NewGuid().ToString("N")), out error) == null, "missing src");
        Expect(!string.IsNullOrEmpty(error), "missing sets error");
    }

    private static void Expect(bool cond, string name)
    {
        if (cond)
            return;
        _failed++;
        Console.WriteLine("FAIL  " + name);
    }
}
