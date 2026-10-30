using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

/// <summary>Safe shell file helpers (no IContextMenu).</summary>
internal static class ShellFileOps
{
    private const uint FoDelete = 0x0003;
    private const ushort FofAllowUndo = 0x0040;
    private const ushort FofNoConfirmation = 0x0010;
    private const ushort FofSilent = 0x0004;
    private const uint ShopFilePath = 0x00000002;

    /// <summary>Send path to Recycle Bin (FO_DELETE + FOF_ALLOWUNDO).</summary>
    public static bool SendToRecycleBin(IntPtr hwnd, string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;
        try
        {
            SHFILEOPSTRUCT op = new SHFILEOPSTRUCT();
            op.hwnd = hwnd;
            op.wFunc = FoDelete;
            op.pFrom = path + "\0\0";
            op.pTo = null;
            op.fFlags = (short)(FofAllowUndo | FofNoConfirmation | FofSilent);
            op.fAnyOperationsAborted = false;
            op.hNameMappings = IntPtr.Zero;
            op.lpszProgressTitle = null;
            int rc = SHFileOperation(ref op);
            return rc == 0 && !op.fAnyOperationsAborted;
        }
        catch
        {
            return false;
        }
    }

    public static bool ShowProperties(IntPtr hwnd, string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;
        try
        {
            if (SHObjectProperties(hwnd, ShopFilePath, path, null))
                return true;
        }
        catch
        {
        }
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = "/select,\"" + path + "\"",
                UseShellExecute = true
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Rename file/folder. Returns new full path, or null and sets error.</summary>
    public static string Rename(string path, string newName, out string error)
    {
        error = null;
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(newName))
        {
            error = "Invalid name.";
            return null;
        }
        newName = newName.Trim();
        if (newName.Length == 0 || newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            error = "Invalid name.";
            return null;
        }
        try
        {
            string parent = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(parent))
            {
                error = "Cannot rename here.";
                return null;
            }
            string dest = Path.Combine(parent, newName);
            if (string.Equals(path, dest, StringComparison.OrdinalIgnoreCase))
                return path;
            if (File.Exists(dest) || Directory.Exists(dest))
            {
                error = "A file or folder with that name already exists.";
                return null;
            }
            if (Directory.Exists(path))
                Directory.Move(path, dest);
            else if (File.Exists(path))
                File.Move(path, dest);
            else
            {
                error = "Item not found.";
                return null;
            }
            return dest;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return null;
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        public string pFrom;
        public string pTo;
        public short fFlags;
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool SHObjectProperties(IntPtr hwnd, uint shopObjectType, string pszObjectName, string pszPropertyPage);
}
