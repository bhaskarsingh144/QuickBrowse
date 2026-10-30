using System;
using System.IO;

/// <summary>Copy a file or folder into a destination directory with unique naming.</summary>
internal static class FileCopy
{
    public static string CopyInto(string destFolder, string srcPath, out string error)
    {
        error = null;
        if (string.IsNullOrEmpty(destFolder) || !Directory.Exists(destFolder))
        {
            error = "Destination folder not found.";
            return null;
        }
        if (string.IsNullOrEmpty(srcPath))
        {
            error = "Nothing to copy.";
            return null;
        }
        try
        {
            string name = Path.GetFileName(srcPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrEmpty(name))
            {
                error = "Invalid path.";
                return null;
            }
            bool isDir = Directory.Exists(srcPath);
            if (!isDir && !File.Exists(srcPath))
            {
                error = "Item not found.";
                return null;
            }
            string dest = UniqueDest(destFolder, name, isDir);
            if (isDir)
                CopyDirectory(srcPath, dest);
            else
                File.Copy(srcPath, dest, false);
            return dest;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return null;
        }
    }

    public static string UniqueDest(string folder, string name, bool isDir)
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

    public static void CopyDirectory(string src, string dest)
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
