using System;
using System.Collections.Specialized;
using System.IO;
using System.Text;
using System.Windows.Forms;

/// <summary>
/// Shared file drag/drop + paste helpers (CF_HDROP, Preferred DropEffect, copy/move into a folder).
/// </summary>
internal static class ShellDragDrop
{
    /// <summary>Shell key-state bits from DragEventArgs.KeyState.</summary>
    public const int KeyLButton = 1;
    public const int KeyRButton = 2;
    public const int KeyShift = 4;
    public const int KeyCtrl = 8;

    private const string DropEffectFormat = "Preferred DropEffect";
    private const int DropEffectCopy = 1;
    private const int DropEffectMove = 2;

    /// <summary>CF_HDROP data object with Preferred DropEffect; null when no valid paths.</summary>
    public static DataObject BuildFileDropDataObject(string[] paths, bool preferMove)
    {
        if (paths == null || paths.Length == 0)
            return null;
        StringCollection list = new StringCollection();
        for (int i = 0; i < paths.Length; i++)
        {
            if (!string.IsNullOrEmpty(paths[i]))
                list.Add(paths[i]);
        }
        if (list.Count == 0)
            return null;

        DataObject data = new DataObject();
        data.SetFileDropList(list);
        MemoryStream effect = new MemoryStream(BitConverter.GetBytes(preferMove ? DropEffectMove : DropEffectCopy));
        data.SetData(DropEffectFormat, true, effect);
        return data;
    }

    /// <summary>True when the data object's Preferred DropEffect includes Move (cut).</summary>
    public static bool IsPreferredMove(IDataObject data)
    {
        if (data == null)
            return false;
        try
        {
            if (!data.GetDataPresent(DropEffectFormat))
                return false;
            MemoryStream ms = data.GetData(DropEffectFormat) as MemoryStream;
            if (ms == null)
                return false;
            byte[] buf = ms.ToArray();
            if (buf.Length < 4)
                return false;
            return (BitConverter.ToInt32(buf, 0) & DropEffectMove) != 0;
        }
        catch
        {
            return false;
        }
    }

    public static DragDropEffects ResolveDropEffect(DragEventArgs e)
    {
        if (e == null)
            return DragDropEffects.None;
        return ResolveDropEffect(e.AllowedEffect, e.KeyState, false);
    }

    public static DragDropEffects ResolveDropEffect(DragDropEffects allowed, int keyState)
    {
        return ResolveDropEffect(allowed, keyState, false);
    }

    /// <summary>
    /// Ctrl = copy, Shift = move, Ctrl+Shift = link (when allowed, else copy).
    /// With no modifier: copy, or move when defaultMove (e.g. same volume).
    /// Result is always a subset of <paramref name="allowed"/>; falls back copy -> move -> none.
    /// </summary>
    public static DragDropEffects ResolveDropEffect(DragDropEffects allowed, int keyState, bool defaultMove)
    {
        bool ctrl = (keyState & KeyCtrl) != 0;
        bool shift = (keyState & KeyShift) != 0;

        DragDropEffects want;
        if (ctrl && shift)
            want = DragDropEffects.Link;
        else if (ctrl)
            want = DragDropEffects.Copy;
        else if (shift)
            want = DragDropEffects.Move;
        else
            want = defaultMove ? DragDropEffects.Move : DragDropEffects.Copy;

        if ((allowed & want) != 0)
            return want;
        if ((allowed & DragDropEffects.Copy) != 0)
            return DragDropEffects.Copy;
        if ((allowed & DragDropEffects.Move) != 0)
            return DragDropEffects.Move;
        return DragDropEffects.None;
    }

    /// <summary>
    /// Null-separated list for SHFILEOPSTRUCT.pFrom: "a\0b\0\0". Null when no valid paths.
    /// </summary>
    public static string JoinPathsForShFileOp(string[] paths)
    {
        if (paths == null || paths.Length == 0)
            return null;
        StringBuilder sb = new StringBuilder();
        int n = 0;
        for (int i = 0; i < paths.Length; i++)
        {
            if (string.IsNullOrEmpty(paths[i]))
                continue;
            sb.Append(paths[i]);
            sb.Append('\0');
            n++;
        }
        if (n == 0)
            return null;
        sb.Append('\0');
        return sb.ToString();
    }

    /// <summary>
    /// Copy or move files/folders into destFolder (unique names on conflict).
    /// Shared by clipboard paste and drag/drop. Returns true when at least one item succeeded.
    /// </summary>
    public static bool BringPathsInto(string destFolder, string[] sources, bool move, out string status)
    {
        status = null;
        if (string.IsNullOrEmpty(destFolder) || !Directory.Exists(destFolder))
        {
            status = "Cannot paste here.";
            return false;
        }
        if (sources == null || sources.Length == 0)
        {
            status = "No files.";
            return false;
        }

        int ok = 0;
        for (int i = 0; i < sources.Length; i++)
        {
            string src = sources[i];
            if (string.IsNullOrEmpty(src))
                continue;
            string trimmed = src.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string name = Path.GetFileName(trimmed);
            if (string.IsNullOrEmpty(name))
                continue;
            bool isDir = Directory.Exists(src);
            if (!isDir && !File.Exists(src))
                continue;

            try
            {
                if (isDir && IsSameOrInside(destFolder, trimmed))
                {
                    status = "Cannot " + (move ? "move" : "copy") + " a folder into itself.";
                    continue;
                }
                if (move && IsSameFolder(Path.GetDirectoryName(trimmed), destFolder))
                    continue; // already there

                string dest = FileCopy.UniqueDest(destFolder, name, isDir);
                if (isDir)
                {
                    if (move)
                        Directory.Move(src, dest);
                    else
                        FileCopy.CopyDirectory(src, dest);
                }
                else
                {
                    if (move)
                        File.Move(src, dest);
                    else
                        File.Copy(src, dest, false);
                }
                ok++;
            }
            catch (Exception ex)
            {
                status = ex.Message;
            }
        }

        if (ok == 0 && status == null)
            status = "Nothing pasted.";
        else if (ok > 0 && status == null)
            status = move ? ("Moved " + ok + " item(s)") : ("Copied " + ok + " item(s)");
        return ok > 0;
    }

    private static bool IsSameFolder(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            return false;
        return string.Equals(NormalizeDir(a), NormalizeDir(b), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>True when candidate equals root or is nested under it.</summary>
    private static bool IsSameOrInside(string candidate, string root)
    {
        string c = NormalizeDir(candidate);
        string r = NormalizeDir(root);
        if (string.Equals(c, r, StringComparison.OrdinalIgnoreCase))
            return true;
        return c.StartsWith(r + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeDir(string path)
    {
        try { path = Path.GetFullPath(path); }
        catch { }
        return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
