using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

internal sealed class PinEntry
{
    public string Path;
    public string Title;
    /// <summary>True for folders (pins are always folders; recent may be files).</summary>
    public bool IsDirectory = true;
}

/// <summary>
/// Persisted user pins under a directory (default: %LocalAppData%\QuickBrowse\pins.json).
/// Pure data — no UI. Directory injectable for tests.
/// </summary>
internal sealed class PinStore
{
    private readonly string _dir;
    private readonly string _file;
    private readonly List<PinEntry> _pins = new List<PinEntry>();
    private readonly object _gate = new object();

    public PinStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "QuickBrowse"))
    {
    }

    public PinStore(string directory)
    {
        _dir = directory ?? "";
        _file = Path.Combine(_dir, "pins.json");
        Reload();
    }

    public IList<PinEntry> List()
    {
        lock (_gate)
        {
            List<PinEntry> copy = new List<PinEntry>(_pins.Count);
            for (int i = 0; i < _pins.Count; i++)
            {
                PinEntry p = _pins[i];
                copy.Add(new PinEntry { Path = p.Path, Title = p.Title });
            }
            return copy;
        }
    }

    public bool Contains(string path)
    {
        string key = Normalize(path);
        if (key == null)
            return false;
        lock (_gate)
        {
            for (int i = 0; i < _pins.Count; i++)
            {
                if (string.Equals(Normalize(_pins[i].Path), key, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        return false;
    }

    public bool Add(string path)
    {
        string key = Normalize(path);
        if (key == null)
            return false;
        lock (_gate)
        {
            for (int i = 0; i < _pins.Count; i++)
            {
                if (string.Equals(Normalize(_pins[i].Path), key, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            _pins.Add(new PinEntry { Path = key, Title = TitleFromPath(key) });
            SaveUnlocked();
            return true;
        }
    }

    public bool Remove(string path)
    {
        string key = Normalize(path);
        if (key == null)
            return false;
        lock (_gate)
        {
            for (int i = 0; i < _pins.Count; i++)
            {
                if (string.Equals(Normalize(_pins[i].Path), key, StringComparison.OrdinalIgnoreCase))
                {
                    _pins.RemoveAt(i);
                    SaveUnlocked();
                    return true;
                }
            }
        }
        return false;
    }

    public void Reload()
    {
        lock (_gate)
        {
            _pins.Clear();
            try
            {
                if (string.IsNullOrEmpty(_file) || !File.Exists(_file))
                    return;
                string text = File.ReadAllText(_file, Encoding.UTF8);
                ParseInto(_pins, text);
            }
            catch
            {
                _pins.Clear();
            }
        }
    }

    public static string TitleFromPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "";
        try
        {
            string trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            // Drive root: "C:" after trim of "C:\"
            if (trimmed.Length == 2 && trimmed[1] == ':')
                return trimmed + "\\";
            string name = Path.GetFileName(trimmed);
            if (!string.IsNullOrEmpty(name))
                return name;
            return trimmed;
        }
        catch
        {
            return path;
        }
    }

    public static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;
        try
        {
            string full = Path.GetFullPath(path.Trim());
            // Keep trailing slash only for drive roots.
            string root = Path.GetPathRoot(full);
            if (!string.IsNullOrEmpty(root)
                && full.TrimEnd('\\', '/').Equals(root.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
                return root.EndsWith("\\") || root.EndsWith("/") ? root : root + "\\";
            return full.TrimEnd('\\', '/');
        }
        catch
        {
            return null;
        }
    }

    private void SaveUnlocked()
    {
        try
        {
            if (string.IsNullOrEmpty(_dir))
                return;
            Directory.CreateDirectory(_dir);
            StringBuilder sb = new StringBuilder();
            sb.Append('[');
            for (int i = 0; i < _pins.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append("{\"path\":");
                sb.Append(JsonString(_pins[i].Path));
                sb.Append(",\"title\":");
                sb.Append(JsonString(_pins[i].Title));
                sb.Append('}');
            }
            sb.Append(']');
            File.WriteAllText(_file, sb.ToString(), Encoding.UTF8);
        }
        catch
        {
        }
    }

    private static string JsonString(string s)
    {
        if (s == null) s = "";
        StringBuilder sb = new StringBuilder();
        sb.Append('"');
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '\\' || c == '"')
            {
                sb.Append('\\');
                sb.Append(c);
            }
            else if (c == '\n')
                sb.Append("\\n");
            else if (c == '\r')
                sb.Append("\\r");
            else if (c == '\t')
                sb.Append("\\t");
            else
                sb.Append(c);
        }
        sb.Append('"');
        return sb.ToString();
    }

    private static void ParseInto(List<PinEntry> dest, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;
        text = text.Trim();
        if (text.Length < 2 || text[0] != '[')
            throw new InvalidDataException("pins.json");
        int i = 1;
        while (i < text.Length)
        {
            SkipWs(text, ref i);
            if (i >= text.Length || text[i] == ']')
                break;
            if (text[i] == ',')
            {
                i++;
                continue;
            }
            if (text[i] != '{')
                throw new InvalidDataException("pins.json object");
            i++;
            string path = null;
            string title = null;
            while (i < text.Length && text[i] != '}')
            {
                SkipWs(text, ref i);
                if (i < text.Length && text[i] == ',')
                {
                    i++;
                    continue;
                }
                string key = ReadJsonString(text, ref i);
                SkipWs(text, ref i);
                if (i >= text.Length || text[i] != ':')
                    throw new InvalidDataException("pins.json colon");
                i++;
                SkipWs(text, ref i);
                string val = ReadJsonString(text, ref i);
                if (string.Equals(key, "path", StringComparison.OrdinalIgnoreCase))
                    path = val;
                else if (string.Equals(key, "title", StringComparison.OrdinalIgnoreCase))
                    title = val;
                SkipWs(text, ref i);
            }
            if (i < text.Length && text[i] == '}')
                i++;
            string norm = Normalize(path);
            if (norm == null)
                continue;
            if (string.IsNullOrEmpty(title))
                title = TitleFromPath(norm);
            bool dup = false;
            for (int k = 0; k < dest.Count; k++)
            {
                if (string.Equals(Normalize(dest[k].Path), norm, StringComparison.OrdinalIgnoreCase))
                {
                    dup = true;
                    break;
                }
            }
            if (!dup)
                dest.Add(new PinEntry { Path = norm, Title = title });
        }
    }

    private static void SkipWs(string text, ref int i)
    {
        while (i < text.Length && char.IsWhiteSpace(text[i]))
            i++;
    }

    private static string ReadJsonString(string text, ref int i)
    {
        SkipWs(text, ref i);
        if (i >= text.Length || text[i] != '"')
            throw new InvalidDataException("pins.json string");
        i++;
        StringBuilder sb = new StringBuilder();
        while (i < text.Length)
        {
            char c = text[i++];
            if (c == '"')
                return sb.ToString();
            if (c == '\\' && i < text.Length)
            {
                char n = text[i++];
                if (n == 'n') sb.Append('\n');
                else if (n == 'r') sb.Append('\r');
                else if (n == 't') sb.Append('\t');
                else sb.Append(n);
            }
            else
                sb.Append(c);
        }
        throw new InvalidDataException("pins.json unclosed string");
    }
}
