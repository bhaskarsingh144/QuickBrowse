using System;
using System.IO;
using System.Windows.Forms;

internal static class ShellDragDropTests
{
    private static int _failed;

    private const int KeyLButton = 1;
    private const int KeyRButton = 2;
    private const int KeyShift = 4;
    private const int KeyCtrl = 8;

    private const DragDropEffects All = DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link;

    [STAThread]
    private static void Main()
    {
        JoinPaths();
        ResolveEffects();
        ResolveEffectFallbacks();
        DropDataObject();
        BringCopy();
        BringMove();
        BringUniqueNames();
        BringRejectsBadInput();
        BringFolderIntoItself();

        if (_failed != 0)
            Environment.Exit(1);
        Console.WriteLine("ShellDragDropTests OK");
    }

    private static void JoinPaths()
    {
        Expect(ShellDragDrop.JoinPathsForShFileOp(null) == null, "join null");
        Expect(ShellDragDrop.JoinPathsForShFileOp(new string[0]) == null, "join empty");
        Expect(ShellDragDrop.JoinPathsForShFileOp(new string[] { null, "" }) == null, "join only blanks");
        Expect(ShellDragDrop.JoinPathsForShFileOp(new string[] { @"C:\a" }) == "C:\\a\0\0", "join single");
        Expect(ShellDragDrop.JoinPathsForShFileOp(new string[] { @"C:\a", @"C:\b" }) == "C:\\a\0C:\\b\0\0", "join two");
        Expect(ShellDragDrop.JoinPathsForShFileOp(new string[] { @"C:\a", "", null, @"C:\b" }) == "C:\\a\0C:\\b\0\0", "join skips blanks");
    }

    private static void ResolveEffects()
    {
        Expect(ShellDragDrop.ResolveDropEffect(All, KeyLButton) == DragDropEffects.Copy, "default copy");
        Expect(ShellDragDrop.ResolveDropEffect(All, KeyLButton | KeyRButton) == DragDropEffects.Copy, "buttons only copy");
        Expect(ShellDragDrop.ResolveDropEffect(All, KeyLButton | KeyCtrl) == DragDropEffects.Copy, "ctrl copy");
        Expect(ShellDragDrop.ResolveDropEffect(All, KeyLButton | KeyShift) == DragDropEffects.Move, "shift move");
        Expect(ShellDragDrop.ResolveDropEffect(All, KeyLButton | KeyShift | KeyCtrl) == DragDropEffects.Link, "ctrl+shift link");
        Expect(ShellDragDrop.ResolveDropEffect(All, 0, true) == DragDropEffects.Move, "default move when requested");
        Expect(ShellDragDrop.ResolveDropEffect(All, KeyCtrl, true) == DragDropEffects.Copy, "ctrl overrides default move");
    }

    private static void ResolveEffectFallbacks()
    {
        Expect(ShellDragDrop.ResolveDropEffect(DragDropEffects.Move, KeyLButton) == DragDropEffects.Move, "copy not allowed falls to move");
        Expect(ShellDragDrop.ResolveDropEffect(DragDropEffects.Copy, KeyShift) == DragDropEffects.Copy, "move not allowed falls to copy");
        Expect(ShellDragDrop.ResolveDropEffect(DragDropEffects.Copy | DragDropEffects.Move, KeyShift | KeyCtrl) == DragDropEffects.Copy, "link not allowed falls to copy");
        Expect(ShellDragDrop.ResolveDropEffect(DragDropEffects.None, KeyLButton) == DragDropEffects.None, "none allowed");
        Expect(ShellDragDrop.ResolveDropEffect(DragDropEffects.Link, KeyLButton) == DragDropEffects.None, "link-only default none");
        Expect(ShellDragDrop.ResolveDropEffect((DragEventArgs)null) == DragDropEffects.None, "null args none");
    }

    private static void DropDataObject()
    {
        Expect(ShellDragDrop.BuildFileDropDataObject(null, false) == null, "data null paths");
        Expect(ShellDragDrop.BuildFileDropDataObject(new string[] { "" }, false) == null, "data blank paths");

        DataObject copy = ShellDragDrop.BuildFileDropDataObject(new string[] { @"C:\a.txt", @"C:\b.txt" }, false);
        Expect(copy != null && copy.GetDataPresent(DataFormats.FileDrop), "data has file drop");
        string[] files = copy == null ? null : copy.GetData(DataFormats.FileDrop) as string[];
        Expect(files != null && files.Length == 2 && files[0] == @"C:\a.txt", "data file list");
        Expect(!ShellDragDrop.IsPreferredMove(copy), "copy effect not move");

        DataObject move = ShellDragDrop.BuildFileDropDataObject(new string[] { @"C:\a.txt" }, true);
        Expect(ShellDragDrop.IsPreferredMove(move), "move effect");
        Expect(!ShellDragDrop.IsPreferredMove(null), "null not move");
    }

    private static string MakeTemp(string tag)
    {
        string dir = Path.Combine(Path.GetTempPath(), "qb-dnd-" + tag + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void Cleanup(string dir)
    {
        try { Directory.Delete(dir, true); } catch { }
    }

    private static void BringCopy()
    {
        string root = MakeTemp("copy");
        try
        {
            string src = Path.Combine(root, "src");
            string dest = Path.Combine(root, "dest");
            Directory.CreateDirectory(src);
            Directory.CreateDirectory(dest);
            string file = Path.Combine(src, "a.txt");
            File.WriteAllText(file, "a");
            string folder = Path.Combine(src, "sub");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "b.txt"), "b");

            string status;
            bool ok = ShellDragDrop.BringPathsInto(dest, new string[] { file, folder }, false, out status);
            Expect(ok, "copy ok");
            Expect(status == "Copied 2 item(s)", "copy status: " + status);
            Expect(File.Exists(Path.Combine(dest, "a.txt")) && File.Exists(file), "copy file keeps source");
            Expect(File.Exists(Path.Combine(dest, "sub", "b.txt")) && Directory.Exists(folder), "copy folder keeps source");
        }
        finally
        {
            Cleanup(root);
        }
    }

    private static void BringMove()
    {
        string root = MakeTemp("move");
        try
        {
            string src = Path.Combine(root, "src");
            string dest = Path.Combine(root, "dest");
            Directory.CreateDirectory(src);
            Directory.CreateDirectory(dest);
            string file = Path.Combine(src, "a.txt");
            File.WriteAllText(file, "a");
            string folder = Path.Combine(src, "sub");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "b.txt"), "b");

            string status;
            bool ok = ShellDragDrop.BringPathsInto(dest, new string[] { file, folder }, true, out status);
            Expect(ok, "move ok");
            Expect(status == "Moved 2 item(s)", "move status: " + status);
            Expect(File.Exists(Path.Combine(dest, "a.txt")) && !File.Exists(file), "move file removes source");
            Expect(File.Exists(Path.Combine(dest, "sub", "b.txt")) && !Directory.Exists(folder), "move folder removes source");

            // Moving into the folder an item already lives in is a no-op.
            string again;
            bool same = ShellDragDrop.BringPathsInto(dest, new string[] { Path.Combine(dest, "a.txt") }, true, out again);
            Expect(!same, "move into same folder no-op");
            Expect(File.Exists(Path.Combine(dest, "a.txt")) && !File.Exists(Path.Combine(dest, "a (2).txt")), "move same folder leaves file");
        }
        finally
        {
            Cleanup(root);
        }
    }

    private static void BringUniqueNames()
    {
        string root = MakeTemp("uniq");
        try
        {
            string src = Path.Combine(root, "src");
            string dest = Path.Combine(root, "dest");
            Directory.CreateDirectory(src);
            Directory.CreateDirectory(dest);
            string file = Path.Combine(src, "a.txt");
            File.WriteAllText(file, "new");
            File.WriteAllText(Path.Combine(dest, "a.txt"), "old");

            string status;
            Expect(ShellDragDrop.BringPathsInto(dest, new string[] { file }, false, out status), "unique copy ok");
            Expect(File.ReadAllText(Path.Combine(dest, "a.txt")) == "old", "existing untouched");
            Expect(File.Exists(Path.Combine(dest, "a (2).txt")), "unique name used");
        }
        finally
        {
            Cleanup(root);
        }
    }

    private static void BringRejectsBadInput()
    {
        string root = MakeTemp("bad");
        try
        {
            string status;
            Expect(!ShellDragDrop.BringPathsInto(null, new string[] { "x" }, false, out status) && status != null, "null dest");
            Expect(!ShellDragDrop.BringPathsInto(Path.Combine(root, "missing"), new string[] { "x" }, false, out status), "missing dest");
            Expect(!ShellDragDrop.BringPathsInto(root, null, false, out status) && status != null, "null sources");
            Expect(!ShellDragDrop.BringPathsInto(root, new string[] { null, "", Path.Combine(root, "nope.txt") }, false, out status)
                && status == "Nothing pasted.", "no valid sources");
        }
        finally
        {
            Cleanup(root);
        }
    }

    private static void BringFolderIntoItself()
    {
        string root = MakeTemp("self");
        try
        {
            string folder = Path.Combine(root, "f");
            string inner = Path.Combine(folder, "inner");
            Directory.CreateDirectory(inner);
            string status;
            Expect(!ShellDragDrop.BringPathsInto(inner, new string[] { folder }, false, out status), "copy into own subfolder rejected");
            Expect(!ShellDragDrop.BringPathsInto(inner, new string[] { folder }, true, out status), "move into own subfolder rejected");
            Expect(!ShellDragDrop.BringPathsInto(folder, new string[] { folder }, false, out status), "copy into self rejected");
            Expect(Directory.Exists(inner), "folder intact");
        }
        finally
        {
            Cleanup(root);
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
