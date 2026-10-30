using System;
using System.IO;

internal static class ShellFileOpsTests
{
    private static int _failed;

    private static void Main()
    {
        RecycleRejectsEmpty();
        RenameRejectsBadInput();
        RenameMovesFile();

        if (_failed != 0)
            Environment.Exit(1);
        Console.WriteLine("ShellFileOpsTests OK");
    }

    private static void RecycleRejectsEmpty()
    {
        Expect(!ShellFileOps.SendToRecycleBin(IntPtr.Zero, null), "recycle null path false");
        Expect(!ShellFileOps.SendToRecycleBin(IntPtr.Zero, ""), "recycle empty path false");
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

    private static void Expect(bool cond, string name)
    {
        if (cond)
            return;
        _failed++;
        Console.WriteLine("FAIL  " + name);
    }
}
