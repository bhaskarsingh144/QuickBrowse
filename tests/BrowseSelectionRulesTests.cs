using System;
using System.Collections.Generic;

internal static class BrowseSelectionRulesTests
{
    private static int _failed;

    private static void Main()
    {
        EmptySelection();
        SingleFile();
        SingleFolder();
        SingleArchive();
        SingleRunnable();
        MultiMixed();
        MultiFiles();
        Drives();
        MissingPath();
        CutCopyDeleteHelper();

        if (_failed != 0)
            Environment.Exit(1);
        Console.WriteLine("BrowseSelectionRulesTests OK");
    }

    private static DirEntry File(string path)
    {
        DirEntry e = new DirEntry();
        e.FullPath = path;
        e.Name = System.IO.Path.GetFileName(path);
        return e;
    }

    private static DirEntry Dir(string path)
    {
        DirEntry e = File(path);
        e.IsDirectory = true;
        return e;
    }

    private static DirEntry Drive(string path)
    {
        DirEntry e = Dir(path);
        e.IsDrive = true;
        return e;
    }

    private static IList<DirEntry> List(params DirEntry[] items)
    {
        return new List<DirEntry>(items);
    }

    private static void EmptySelection()
    {
        BrowseMenuCapabilities c = BrowseSelectionRules.Compute(new List<DirEntry>());
        Expect(c.Count == 0, "empty count");
        Expect(!c.CanCutCopyDelete && !c.CanRename && !c.CanOpen && !c.CanProperties, "empty nothing enabled");
        Expect(!BrowseSelectionRules.Compute(null).CanCutCopyDelete, "null nothing enabled");
        Expect(!BrowseSelectionRules.CanCutCopyDelete(null), "null cut/copy/delete");
    }

    private static void SingleFile()
    {
        BrowseMenuCapabilities c = BrowseSelectionRules.Compute(List(File(@"C:\data\note.txt")));
        Expect(c.Count == 1 && c.AllFiles && !c.AllDirectories, "file flags");
        Expect(c.CanCutCopyDelete, "file cut/copy/delete");
        Expect(c.CanRename, "file rename");
        Expect(c.CanOpen && c.CanOpenWith && c.CanOpenIn, "file open verbs");
        Expect(c.CanHash, "file hash");
        Expect(c.CanDuplicate && c.CanShortcut && c.CanSendTo && c.CanCompress, "file batch verbs");
        Expect(c.CanProperties && c.CanExplorer && c.CanCopyPath, "file props/explorer/copy path");
        Expect(!c.CanPin && !c.CanTerminal, "file no pin/terminal");
        Expect(!c.CanExtract, "txt not extractable");
        Expect(!c.CanRunAsAdmin, "txt not runnable");
    }

    private static void SingleFolder()
    {
        BrowseMenuCapabilities c = BrowseSelectionRules.Compute(List(Dir(@"C:\data\docs")));
        Expect(c.AllDirectories && !c.AllFiles, "folder flags");
        Expect(c.CanCutCopyDelete && c.CanRename, "folder edit");
        Expect(c.CanPin && c.CanTerminal, "folder pin/terminal");
        Expect(c.CanOpen && c.CanOpenIn && c.CanProperties, "folder open/in/props");
        Expect(!c.CanOpenWith && !c.CanHash && !c.CanRunAsAdmin && !c.CanExtract, "folder no file-only verbs");
        Expect(c.CanCompress && c.CanSendTo && c.CanDuplicate && c.CanShortcut, "folder batch verbs");
    }

    private static void SingleArchive()
    {
        BrowseMenuCapabilities c = BrowseSelectionRules.Compute(List(File(@"C:\data\pack.zip")));
        Expect(c.AllArchives, "archive flag");
        Expect(c.CanExtract, "archive extract");
        Expect(!c.CanCompress, "archive not compressed again");
        Expect(c.CanHash && c.CanOpenWith, "archive still a file");
    }

    private static void SingleRunnable()
    {
        Expect(BrowseSelectionRules.Compute(List(File(@"C:\bin\tool.EXE"))).CanRunAsAdmin, "exe run as admin");
        Expect(BrowseSelectionRules.Compute(List(File(@"C:\bin\setup.msi"))).CanRunAsAdmin, "msi run as admin");
        Expect(!BrowseSelectionRules.Compute(List(File(@"C:\bin\a.exe"), File(@"C:\bin\b.exe"))).CanRunAsAdmin, "multi no run as admin");
        Expect(!BrowseSelectionRules.IsRunnablePath(null), "runnable null");
        Expect(!BrowseSelectionRules.IsRunnablePath(@"C:\bin\noext"), "runnable no ext");
    }

    private static void MultiMixed()
    {
        BrowseMenuCapabilities c = BrowseSelectionRules.Compute(List(
            File(@"C:\data\a.txt"), Dir(@"C:\data\docs"), File(@"C:\data\b.zip")));
        Expect(c.Count == 3, "mixed count");
        Expect(!c.AllFiles && !c.AllDirectories && !c.AllArchives, "mixed flags");
        Expect(c.CanCutCopyDelete && c.CanDuplicate && c.CanShortcut && c.CanSendTo, "mixed batch verbs");
        Expect(c.CanCompress, "mixed compress");
        Expect(c.CanOpen && c.CanCopyPath, "mixed open/copy path");
        Expect(!c.CanRename, "mixed no rename");
        Expect(!c.CanOpenWith && !c.CanHash && !c.CanRunAsAdmin, "mixed no single-file verbs");
        Expect(!c.CanPin && !c.CanTerminal && !c.CanExtract, "mixed no pin/terminal/extract");
        Expect(!c.CanProperties && !c.CanExplorer && !c.CanOpenIn, "mixed no single-target verbs");
    }

    private static void MultiFiles()
    {
        BrowseMenuCapabilities c = BrowseSelectionRules.Compute(List(File(@"C:\d\a.zip"), File(@"C:\d\b.zip")));
        Expect(c.AllFiles && c.AllArchives, "multi archives flags");
        Expect(!c.CanExtract, "multi archives no extract");
        Expect(c.CanCompress, "multi archives may be compressed");

        BrowseMenuCapabilities folders = BrowseSelectionRules.Compute(List(Dir(@"C:\d\x"), Dir(@"C:\d\y")));
        Expect(folders.AllDirectories, "multi folders flag");
        Expect(!folders.CanPin && !folders.CanTerminal && !folders.CanRename, "multi folders single-only verbs off");
        Expect(folders.CanCutCopyDelete, "multi folders cut/copy/delete");
    }

    private static void Drives()
    {
        BrowseMenuCapabilities c = BrowseSelectionRules.Compute(List(Drive(@"C:\")));
        Expect(c.AnyDrive, "drive flag");
        Expect(!c.AllDirectories, "drive is not a normal folder");
        Expect(!c.CanCutCopyDelete && !c.CanRename && !c.CanDuplicate && !c.CanShortcut, "drive no edit verbs");
        Expect(!c.CanSendTo && !c.CanCompress && !c.CanPin && !c.CanTerminal && !c.CanOpenIn, "drive no folder extras");
        Expect(c.CanOpen && c.CanProperties && c.CanExplorer, "drive open/props/explorer");

        BrowseMenuCapabilities mix = BrowseSelectionRules.Compute(List(File(@"C:\a.txt"), Drive(@"D:\")));
        Expect(mix.AnyDrive && !mix.CanCutCopyDelete, "drive in multi blocks edit");
    }

    private static void MissingPath()
    {
        DirEntry noPath = new DirEntry();
        noPath.Name = "x";
        BrowseMenuCapabilities c = BrowseSelectionRules.Compute(List(noPath));
        Expect(!c.AllHavePath, "missing path flag");
        Expect(!c.CanCutCopyDelete && !c.CanOpen && !c.CanProperties && !c.CanRename, "missing path nothing enabled");

        Expect(!BrowseSelectionRules.Compute(List(File(@"C:\a.txt"), noPath)).CanCutCopyDelete, "one missing path blocks batch");
    }

    private static void CutCopyDeleteHelper()
    {
        Expect(BrowseSelectionRules.CanCutCopyDelete(List(File(@"C:\a"), Dir(@"C:\b"))), "helper mixed ok");
        Expect(!BrowseSelectionRules.CanCutCopyDelete(List()), "helper empty");
        Expect(!BrowseSelectionRules.CanCutCopyDelete(List(Drive(@"C:\"))), "helper drive");
        Expect(!BrowseSelectionRules.CanCutCopyDelete(List(File(@"C:\a"), null)), "helper null entry");
    }

    private static void Expect(bool cond, string name)
    {
        if (cond)
            return;
        _failed++;
        Console.WriteLine("FAIL  " + name);
    }
}
