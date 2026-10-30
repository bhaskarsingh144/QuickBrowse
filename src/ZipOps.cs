using System;
using System.IO;
using System.IO.Compression;

/// <summary>Compress / extract ZIP without shell IContextMenu.</summary>
internal static class ZipOps
{
    public static bool IsZipPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;
        return string.Equals(Path.GetExtension(path), ".zip", StringComparison.OrdinalIgnoreCase);
    }

    public static string Compress(string srcPath, out string error)
    {
        error = null;
        if (string.IsNullOrEmpty(srcPath))
        {
            error = "Nothing to compress.";
            return null;
        }
        try
        {
            bool isDir = Directory.Exists(srcPath);
            if (!isDir && !File.Exists(srcPath))
            {
                error = "Item not found.";
                return null;
            }
            string parent = Path.GetDirectoryName(srcPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrEmpty(parent))
            {
                error = "Cannot compress here.";
                return null;
            }
            string baseName = Path.GetFileNameWithoutExtension(
                srcPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (isDir)
                baseName = Path.GetFileName(srcPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrEmpty(baseName))
                baseName = "Archive";
            string zipPath = FileCopy.UniqueDest(parent, baseName + ".zip", false);
            if (isDir)
                ZipFile.CreateFromDirectory(srcPath, zipPath, CompressionLevel.Optimal, false);
            else
            {
                using (ZipArchive archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                {
                    archive.CreateEntryFromFile(srcPath, Path.GetFileName(srcPath), CompressionLevel.Optimal);
                }
            }
            return zipPath;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return null;
        }
    }

    public static string ExtractAll(string zipPath, out string error)
    {
        error = null;
        if (!IsZipPath(zipPath) || !File.Exists(zipPath))
        {
            error = "Not a zip file.";
            return null;
        }
        try
        {
            string parent = Path.GetDirectoryName(zipPath);
            if (string.IsNullOrEmpty(parent))
            {
                error = "Cannot extract here.";
                return null;
            }
            string folderName = Path.GetFileNameWithoutExtension(zipPath);
            if (string.IsNullOrEmpty(folderName))
                folderName = "Extracted";
            string dest = FileCopy.UniqueDest(parent, folderName, true);
            ZipFile.ExtractToDirectory(zipPath, dest);
            return dest;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return null;
        }
    }
}
