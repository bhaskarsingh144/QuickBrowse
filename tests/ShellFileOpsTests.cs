using System;
using System.IO;

internal static class ShellFileOpsTests
{
    private static int _failed;

    private static void Main()
    {
        RecycleRejectsEmpty();
        RecycleMultiJoin();
        RenameRejectsBadInput();
        RenameMovesFile();
        DuplicateFile();
        Sha256HexFile();
        CreateTextDocumentInFolder();

        if (_failed != 0)
            Environment.Exit(1);
        Console.WriteLine("ShellFileOpsTests OK");
    }

    private static void RecycleRejectsEmpty()
    {
        Expect(!ShellFileOps.SendToRecycleBin(IntPtr.Zero, (string)null), "recycle null path false");
        Expect(!ShellFileOps.SendToRecycleBin(IntPtr.Zero, ""), "recycle empty path false");
        Expect(!ShellFileOps.SendToRecycleBin(IntPtr.Zero, (string[])null), "recycle null array false");
        Expect(!ShellFileOps.SendToRecycleBin(IntPtr.Zero, new string[0]), "recycle empty array false");
        Expect(!ShellFileOps.SendToRecycleBin(IntPtr.Zero, new string[] { null, "" }), "recycle blank array false");
    }

    private static void RecycleMultiJoin()
    {
        string joined = ShellDragDrop.JoinPathsForShFileOp(new string[] { @"C:\a", @"C:\b" });
        Expect(joined == "C:\\a\0C:\\b\0\0", "recycle multi join double null");
    }

    private static void RenameRejectsBadInput()
    {
        string error;
        Expect(ShellFileOps.Rename(null, "a", out error) == null, "rename null path");
        Expect(ShellFileOps.Rename(@"C:\temp\x", "", out error) == null, "rename empty name");
        Expect(ShellFileOps.Rename(@"C:\temp\x", "bad\\name", out error) == null, "rename invalid chars");
        Expect(ShellFileOps.ShowProperties(IntPtr.Zero, null) == false, "props null path");
        Expect(ShellFileOps.ShowProperties(IntPtr.Zero, "") == false, "props empty path");
    }

    private static void RenameMovesFile()
    {
        string dir = Path.Combine(Path.GetTempPath(), "qb-shellfileops-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string src = Path.Combine(dir, "old.txt");
            File.WriteAllText(src, "x");
            string error;
            string dest = ShellFileOps.Rename(src, "new.txt", out error);
            Expect(dest != null && File.Exists(dest) && !File.Exists(src), "rename moves file");
            Expect(string.Equals(Path.GetFileName(dest), "new.txt", StringComparison.OrdinalIgnoreCase), "rename dest name");
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    private static void DuplicateFile()
    {
        string dir = Path.Combine(Path.GetTempPath(), "qb-dup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string src = Path.Combine(dir, "note.txt");
            File.WriteAllText(src, "dup");
            string error;
            string copy = ShellFileOps.Duplicate(src, out error);
            Expect(copy != null && File.Exists(copy) && File.Exists(src), "duplicate creates copy");
            Expect(!string.Equals(src, copy, StringComparison.OrdinalIgnoreCase), "duplicate distinct path");
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    private static void Sha256HexFile()
    {
        string path = Path.Combine(Path.GetTempPath(), "qb-hash-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(path, "abc");
        try
        {
            string error;
            string hash = ShellFileOps.Sha256Hex(path, out error);
            Expect(hash != null && hash.Length == 64, "sha256 length");
            Expect(string.Equals(hash,
                "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",
                StringComparison.OrdinalIgnoreCase), "sha256 of abc");
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }

    private static void CreateTextDocumentInFolder()
    {
        string dir = Path.Combine(Path.GetTempPath(), "qb-newtxt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string created;
            string error;
            Expect(ShellFileOps.CreateTextDocument(dir, out created, out error), "create text doc");
            Expect(created != null && File.Exists(created), "text doc exists");
        }
        finally
        {
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
