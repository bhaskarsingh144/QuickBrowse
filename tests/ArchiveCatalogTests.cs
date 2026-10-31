using System;
using System.Collections.Generic;
using System.IO;

internal static class ArchiveCatalogTests
{
    private static int _failed;

    private static void Main()
    {
        ArchiveCatalog.ClearCacheForTests();
        IsArchivePathRecognizes();
        DetectedProvidersAlwaysHasZip();
        CompressArgsWinRarAndSeven();
        ExtractArgsWinRarAndSeven();
        SuggestPaths();

        if (_failed != 0)
            Environment.Exit(1);
        Console.WriteLine("ArchiveCatalogTests OK");
    }

    private static void IsArchivePathRecognizes()
    {
        Expect(ArchiveCatalog.IsArchivePath(@"C:\a.zip"), "zip");
        Expect(ArchiveCatalog.IsArchivePath(@"C:\a.RAR"), "rar");
        Expect(ArchiveCatalog.IsArchivePath(@"C:\a.7z"), "7z");
        Expect(!ArchiveCatalog.IsArchivePath(@"C:\a.txt"), "txt not archive");
        Expect(!ArchiveCatalog.IsArchivePath(null), "null");
        Expect(!ArchiveCatalog.IsArchivePath(""), "empty");
    }

    private static void DetectedProvidersAlwaysHasZip()
    {
        IList<ArchiveProvider> list = ArchiveCatalog.DetectedProviders();
        Expect(list != null && list.Count >= 1, "at least zip");
        Expect(list[0].Kind == ArchiveProviderKind.ZipManaged, "first is managed zip");
    }

    private static void CompressArgsWinRarAndSeven()
    {
        string a = ArchiveCatalog.CompressArgs(ArchiveProviderKind.WinRar, @"C:\src\file.txt", @"C:\out\a.zip");
        Expect(a != null && a.IndexOf("a ", StringComparison.Ordinal) >= 0, "winrar compress");
        Expect(a.IndexOf("file.txt", StringComparison.OrdinalIgnoreCase) >= 0, "winrar src");
        string b = ArchiveCatalog.CompressArgs(ArchiveProviderKind.SevenZip, @"C:\src\file.txt", @"C:\out\a.zip");
        Expect(b != null && b.StartsWith("a ", StringComparison.Ordinal), "7z compress");
        Expect(ArchiveCatalog.CompressArgs(ArchiveProviderKind.ZipManaged, "x", "y") == null, "managed no cli");
    }

    private static void ExtractArgsWinRarAndSeven()
    {
        string here = ArchiveCatalog.ExtractHereArgs(ArchiveProviderKind.WinRar, @"C:\src\a.rar");
        Expect(here != null && here.IndexOf("x ", StringComparison.Ordinal) >= 0, "winrar extract here");
        string folder = ArchiveCatalog.ExtractToFolderArgs(ArchiveProviderKind.SevenZip, @"C:\src\a.7z", @"C:\out\dir");
        Expect(folder != null && folder.IndexOf("-o", StringComparison.Ordinal) >= 0, "7z extract to");
    }

    private static void SuggestPaths()
    {
        string zip = ArchiveCatalog.SuggestZipPath(@"C:\temp\notes.txt");
        Expect(zip != null && zip.EndsWith("notes.zip", StringComparison.OrdinalIgnoreCase), "suggest zip");
        string folder = ArchiveCatalog.SuggestExtractFolder(@"C:\temp\pack.zip");
        Expect(folder != null && folder.EndsWith("pack", StringComparison.OrdinalIgnoreCase), "suggest extract folder");
    }

    private static void Expect(bool cond, string name)
    {
        if (cond)
            return;
        _failed++;
        Console.WriteLine("FAIL  " + name);
    }
}
