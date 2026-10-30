using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

/// <summary>
/// Builds address-bar crumb segments: This PC › Drive › … › folder.
/// </summary>
internal sealed class PathCrumbSeg
{
    public string Label;
    public bool IsThisPc;
    /// <summary>Null when <see cref="IsThisPc"/>; otherwise a filesystem path for that segment.</summary>
    public string TargetPath;
    public bool IsEllipsis;
}

internal static class PathCrumbs
{
    public static List<PathCrumbSeg> Build(bool thisPc, string path)
    {
        List<PathCrumbSeg> list = new List<PathCrumbSeg>();
        list.Add(new PathCrumbSeg { Label = "This PC", IsThisPc = true, TargetPath = null });
        if (thisPc || string.IsNullOrEmpty(path))
            return list;

        string full;
        try { full = Path.GetFullPath(path); }
        catch { return list; }

        string root;
        try { root = Path.GetPathRoot(full); }
        catch { return list; }
        if (string.IsNullOrEmpty(root))
            return list;

        list.Add(new PathCrumbSeg
        {
            Label = DriveLabel(root),
            IsThisPc = false,
            TargetPath = EnsureDirSep(root)
        });

        string rest = full;
        if (rest.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            rest = rest.Substring(root.Length);
        rest = rest.Trim(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.IsNullOrEmpty(rest))
            return list;

        string accum = EnsureDirSep(root);
        string[] parts = rest.Split(new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            accum = Path.Combine(accum, parts[i]);
            list.Add(new PathCrumbSeg
            {
                Label = parts[i],
                IsThisPc = false,
                TargetPath = accum
            });
        }
        return list;
    }

    /// <summary>
    /// Keep first + last segments when the run is too wide; insert an ellipsis crumb.
    /// </summary>
    public static List<PathCrumbSeg> Fit(List<PathCrumbSeg> crumbs, Font font, int maxWidth, int sepWidth, int pad)
    {
        if (crumbs == null || crumbs.Count == 0)
            return new List<PathCrumbSeg>();
        if (Measure(crumbs, font, sepWidth, pad) <= maxWidth)
            return new List<PathCrumbSeg>(crumbs);

        PathCrumbSeg ellipsis = new PathCrumbSeg { Label = "…", IsEllipsis = true };
        if (crumbs.Count <= 2)
        {
            List<PathCrumbSeg> tiny = new List<PathCrumbSeg>();
            tiny.Add(crumbs[0]);
            if (crumbs.Count == 2)
                tiny.Add(crumbs[1]);
            return tiny;
        }

        List<PathCrumbSeg> fitted = new List<PathCrumbSeg>();
        fitted.Add(crumbs[0]);
        fitted.Add(ellipsis);
        fitted.Add(crumbs[crumbs.Count - 1]);

        // Grow from the end while space remains.
        int takeFromEnd = 1;
        while (takeFromEnd + 1 < crumbs.Count - 1)
        {
            List<PathCrumbSeg> trial = new List<PathCrumbSeg>();
            trial.Add(crumbs[0]);
            trial.Add(ellipsis);
            for (int i = crumbs.Count - (takeFromEnd + 1); i < crumbs.Count; i++)
                trial.Add(crumbs[i]);
            if (Measure(trial, font, sepWidth, pad) > maxWidth)
                break;
            fitted = trial;
            takeFromEnd++;
        }
        return fitted;
    }

    public static int Measure(IList<PathCrumbSeg> crumbs, Font font, int sepWidth, int pad)
    {
        if (crumbs == null || crumbs.Count == 0 || font == null)
            return 0;
        int w = 0;
        for (int i = 0; i < crumbs.Count; i++)
        {
            if (i > 0)
                w += sepWidth;
            w += TextWidth(font, crumbs[i].Label ?? "") + pad;
        }
        return w;
    }

    public static int TextWidth(Font font, string text)
    {
        if (font == null || string.IsNullOrEmpty(text))
            return 0;
        return TextRenderer.MeasureText(text, font, new Size(int.MaxValue, int.MaxValue),
            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;
    }

    private static string DriveLabel(string root)
    {
        string letter = root.TrimEnd('\\', '/');
        try
        {
            DriveInfo drive = new DriveInfo(root);
            if (drive.IsReady && !string.IsNullOrEmpty(drive.VolumeLabel))
                return drive.VolumeLabel + " (" + letter + ")";
        }
        catch
        {
        }
        return letter;
    }

    private static string EnsureDirSep(string root)
    {
        if (string.IsNullOrEmpty(root))
            return root;
        char sep = Path.DirectorySeparatorChar;
        if (root[root.Length - 1] == sep || root[root.Length - 1] == Path.AltDirectorySeparatorChar)
            return root;
        return root + sep;
    }
}
