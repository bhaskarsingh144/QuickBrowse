using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
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
        return SendToRecycleBin(hwnd, new string[] { path });
    }

    /// <summary>Send several paths to the Recycle Bin in one shell operation.</summary>
    public static bool SendToRecycleBin(IntPtr hwnd, string[] paths)
    {
        string from = ShellDragDrop.JoinPathsForShFileOp(paths);
        if (from == null)
            return false;
        try
        {
            SHFILEOPSTRUCT op = new SHFILEOPSTRUCT();
            op.hwnd = hwnd;
            op.wFunc = FoDelete;
            op.pFrom = from;
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

    public static bool OpenDefault(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Shows the Windows Open with dialog (verb openas).</summary>
    public static bool OpenWith(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return false;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                Verb = "openas",
                UseShellExecute = true
            });
            return true;
        }
        catch
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "rundll32.exe",
                    Arguments = "shell32.dll,OpenAs_RunDLL " + path,
                    UseShellExecute = false
                });
                return true;
            }
            catch
            {
                return false;
            }
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

    public static string Duplicate(string path, out string error)
    {
        error = null;
        if (string.IsNullOrEmpty(path))
        {
            error = "Nothing to duplicate.";
            return null;
        }
        try
        {
            string parent = Path.GetDirectoryName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrEmpty(parent))
            {
                error = "Cannot duplicate here.";
                return null;
            }
            return FileCopy.CopyInto(parent, path, out error);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return null;
        }
    }

    public static string CreateShortcut(string targetPath, out string error)
    {
        error = null;
        if (string.IsNullOrEmpty(targetPath))
        {
            error = "Nothing to link.";
            return null;
        }
        try
        {
            bool isDir = Directory.Exists(targetPath);
            if (!isDir && !File.Exists(targetPath))
            {
                error = "Item not found.";
                return null;
            }
            string parent = Path.GetDirectoryName(targetPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            string baseName = Path.GetFileName(targetPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(baseName))
            {
                error = "Cannot create shortcut here.";
                return null;
            }
            string lnk = Path.Combine(parent, baseName + ".lnk");
            int n = 2;
            while (File.Exists(lnk))
            {
                lnk = Path.Combine(parent, baseName + " (" + n + ").lnk");
                n++;
            }
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
            {
                error = "Shortcut helper unavailable.";
                return null;
            }
            object shell = Activator.CreateInstance(shellType);
            object shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnk });
            Type shortcutType = shortcut.GetType();
            shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { targetPath });
            if (isDir)
                shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { targetPath });
            else
            {
                string work = Path.GetDirectoryName(targetPath) ?? "";
                shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { work });
            }
            shortcutType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { baseName });
            shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
            return lnk;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return null;
        }
    }

    public static string Sha256Hex(string filePath, out string error)
    {
        error = null;
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            error = "File not found.";
            return null;
        }
        try
        {
            using (FileStream fs = File.OpenRead(filePath))
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(fs);
                StringBuilder sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                    sb.Append(hash[i].ToString("x2"));
                return sb.ToString();
            }
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return null;
        }
    }

    public static bool RunAsAdministrator(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return false;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                Verb = "runas",
                UseShellExecute = true
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool OpenTerminalHere(string folderPath, string kind)
    {
        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
            return false;
        kind = (kind ?? "wt").ToLowerInvariant();
        try
        {
            if (kind == "wt")
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "wt.exe",
                    Arguments = "-d \"" + folderPath + "\"",
                    UseShellExecute = true
                });
                return true;
            }
            if (kind == "powershell")
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoExit -Command \"Set-Location -LiteralPath '" + folderPath.Replace("'", "''") + "'\"",
                    WorkingDirectory = folderPath,
                    UseShellExecute = false
                });
                return true;
            }
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/k cd /d \"" + folderPath + "\"",
                WorkingDirectory = folderPath,
                UseShellExecute = false
            });
            return true;
        }
        catch
        {
            if (kind == "wt")
                return OpenTerminalHere(folderPath, "powershell");
            return false;
        }
    }

    public static bool CreateTextDocument(string folderPath, out string createdPath, out string error)
    {
        createdPath = null;
        error = null;
        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
        {
            error = "Folder not available.";
            return false;
        }
        try
        {
            string path = Path.Combine(folderPath, "New Text Document.txt");
            int n = 2;
            while (File.Exists(path))
            {
                path = Path.Combine(folderPath, "New Text Document (" + n + ").txt");
                n++;
            }
            File.WriteAllText(path, "", Encoding.UTF8);
            createdPath = path;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
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
