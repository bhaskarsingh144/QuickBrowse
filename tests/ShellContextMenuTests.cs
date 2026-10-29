using System;

internal static class ShellContextMenuTests
{
    private static int _failed;

    private static void Main()
    {
        CustomIdsBelowShellBand();
        PinLabelDirectory();
        PinLabelFile();
        ShowForItemRejectsEmpty();
        ShowForBackgroundRejectsBadHwnd();
        RecycleRejectsEmpty();

        if (_failed != 0)
            Environment.Exit(1);
        Console.WriteLine("ShellContextMenuTests OK");
    }

    private static void CustomIdsBelowShellBand()
    {
        Expect(ShellContextMenu.CustomIdsBelowShellBand(), "custom ids below shell band");
        Expect(ShellContextMenu.CustomPinId < ShellContextMenu.ShellIdCmdFirst, "pin id < first");
        Expect(ShellContextMenu.CustomUnpinId < ShellContextMenu.ShellIdCmdFirst, "unpin id < first");
        Expect(ShellContextMenu.ShellIdCmdFirst == 100, "shell idCmdFirst is 100");
    }

    private static void PinLabelDirectory()
    {
        Expect(ShellContextMenu.PinCommandLabel(true, false) == "Pin folder", "unpinned folder label");
        Expect(ShellContextMenu.PinCommandLabel(true, true) == "Unpin folder", "pinned folder label");
    }

    private static void PinLabelFile()
    {
        Expect(ShellContextMenu.PinCommandLabel(false, false) == null, "file has no pin label");
        Expect(ShellContextMenu.PinCommandLabel(false, true) == null, "file pinned flag ignored");
    }

    private static void ShowForItemRejectsEmpty()
    {
        ShellContextMenu.ShowResult r = ShellContextMenu.ShowForItem(IntPtr.Zero, null, new System.Drawing.Point(0, 0), false, false, false);
        Expect(r != null && !r.Shown && !r.Invoked, "null hwnd/path does not show");
        r = ShellContextMenu.ShowForItem(new IntPtr(1), "", new System.Drawing.Point(0, 0), false, false, false);
        Expect(r != null && !r.Shown, "empty path does not show");
    }

    private static void ShowForBackgroundRejectsBadHwnd()
    {
        ShellContextMenu.ShowResult r = ShellContextMenu.ShowForBackground(IntPtr.Zero, @"C:\", new System.Drawing.Point(0, 0), false);
        Expect(r != null && !r.Shown, "zero hwnd background does not show");
    }

    private static void RecycleRejectsEmpty()
    {
        Expect(!ShellContextMenu.SendToRecycleBin(IntPtr.Zero, null), "recycle null path false");
        Expect(!ShellContextMenu.SendToRecycleBin(IntPtr.Zero, ""), "recycle empty path false");
    }

    private static void Expect(bool cond, string name)
    {
        if (cond)
            return;
        _failed++;
        Console.WriteLine("FAIL  " + name);
    }
}
