using System;
using System.Collections.Specialized;
using System.IO;
using System.Windows.Forms;

/// <summary>
/// File Copy / Cut / Paste via the shell clipboard (CF_HDROP + Preferred DropEffect).
/// </summary>
internal static class ShellClipboard
{
    private const string DropEffectFormat = "Preferred DropEffect";
    private const int DropEffectCopy = 1;
    private const int DropEffectMove = 2;

    public static bool SetFiles(string[] paths, bool cut)
    {
        if (paths == null || paths.Length == 0)
            return false;
        try
        {
            StringCollection list = new StringCollection();
            for (int i = 0; i < paths.Length; i++)
            {
                if (!string.IsNullOrEmpty(paths[i]))
                    list.Add(paths[i]);
            }
            if (list.Count == 0)
                return false;

            DataObject data = new DataObject();
            data.SetFileDropList(list);
            MemoryStream effect = new MemoryStream(BitConverter.GetBytes(cut ? DropEffectMove : DropEffectCopy));
            data.SetData(DropEffectFormat, true, effect);
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

            bool move = IsCut(data);
            int ok = 0;
            for (int i = 0; i < files.Length; i++)
            {
                string src = files[i];
                if (string.IsNullOrEmpty(src))
                    continue;
                string name = Path.GetFileName(src.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (string.IsNullOrEmpty(name))
                    continue;
                string dest = UniqueDest(destFolder, name, Directory.Exists(src));
                try
                {
                    if (Directory.Exists(src))
                    {
                        if (move)
                            Directory.Move(src, dest);
                        else
                            CopyDirectory(src, dest);
                    }
                    else if (File.Exists(src))
                    {
                        if (move)
                            File.Move(src, dest);
                        else
                            File.Copy(src, dest, false);
                    }
                    else
                        continue;
                    ok++;
                }
                catch (Exception ex)
                {
                    status = ex.Message;
                }
            }
            if (move && ok > 0)
            {
                try { Clipboard.Clear(); }
                catch { }
            }
            if (ok == 0 && status == null)
                status = "Nothing pasted.";
            else if (ok > 0 && status == null)
                status = move ? ("Moved " + ok + " item(s)") : ("Copied " + ok + " item(s)");
            return ok > 0;
        }
        catch (Exception ex)
        {
            status = ex.Message;
            return false;
        }
    }

    private static bool IsCut(IDataObject data)
    {
        try
        {
            if (!data.GetDataPresent(DropEffectFormat))
                return false;
            object raw = data.GetData(DropEffectFormat);
            MemoryStream ms = raw as MemoryStream;
            if (ms == null)
                return false;
            byte[] buf = ms.ToArray();
            if (buf.Length < 4)
                return false;
            int effect = BitConverter.ToInt32(buf, 0);
            return (effect & DropEffectMove) != 0;
        }
        catch
        {
            return false;
        }
    }

    private static string UniqueDest(string folder, string name, bool isDir)
    {
        string dest = Path.Combine(folder, name);
        if (!File.Exists(dest) && !Directory.Exists(dest))
            return dest;
        string baseName = Path.GetFileNameWithoutExtension(name);
        string ext = isDir ? "" : Path.GetExtension(name);
        if (isDir)
            baseName = name;
        for (int n = 2; n < 1000; n++)
        {
            string candidate = Path.Combine(folder, baseName + " (" + n + ")" + ext);
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
                return candidate;
        }
        return Path.Combine(folder, baseName + " (" + Guid.NewGuid().ToString("N").Substring(0, 6) + ")" + ext);
    }

    private static void CopyDirectory(string src, string dest)
    {
        Directory.CreateDirectory(dest);
        string[] files = Directory.GetFiles(src);
        for (int i = 0; i < files.Length; i++)
        {
            string name = Path.GetFileName(files[i]);
            File.Copy(files[i], Path.Combine(dest, name), false);
        }
        string[] dirs = Directory.GetDirectories(src);
        for (int i = 0; i < dirs.Length; i++)
        {
            string name = Path.GetFileName(dirs[i]);
            CopyDirectory(dirs[i], Path.Combine(dest, name));
        }
    }
}
