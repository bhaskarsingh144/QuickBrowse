using System;
using System.IO;
using System.Windows.Forms;

/// <summary>
/// File Copy / Cut / Paste via the shell clipboard (CF_HDROP + Preferred DropEffect).
/// </summary>
internal static class ShellClipboard
{
    public static bool SetFiles(string[] paths, bool cut)
    {
        try
        {
            DataObject data = ShellDragDrop.BuildFileDropDataObject(paths, cut);
            if (data == null)
                return false;
            Clipboard.SetDataObject(data, true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool CanPaste()
    {
        try
        {
            IDataObject data = Clipboard.GetDataObject();
            return data != null && data.GetDataPresent(DataFormats.FileDrop);
        }
        catch
        {
            return false;
        }
    }

    public static bool PasteInto(string destFolder, out string status)
    {
        status = null;
        if (string.IsNullOrEmpty(destFolder) || !Directory.Exists(destFolder))
        {
            status = "Cannot paste here.";
            return false;
        }
        try
        {
            IDataObject data = Clipboard.GetDataObject();
            if (data == null || !data.GetDataPresent(DataFormats.FileDrop))
            {
                status = "Clipboard has no files.";
                return false;
            }
            string[] files = data.GetData(DataFormats.FileDrop) as string[];
            if (files == null || files.Length == 0)
            {
                status = "Clipboard has no files.";
                return false;
            }

            bool move = ShellDragDrop.IsPreferredMove(data);
            bool ok = ShellDragDrop.BringPathsInto(destFolder, files, move, out status);
            if (move && ok)
            {
                try { Clipboard.Clear(); }
                catch { }
            }
            return ok;
        }
        catch (Exception ex)
        {
            status = ex.Message;
            return false;
        }
    }
}