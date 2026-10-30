using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;

/// <summary>
/// Local crash / error logging under %LOCALAPPDATA%\QuickBrowse\logs.
/// Fatals get a dedicated file + last-fatal marker; soft errors append to app.log.
/// Handlers stay light: create dir if needed, write, flush — never throw.
/// </summary>
internal static class CrashLog
{
    private const long LogDirCapBytes = 5L * 1024 * 1024; // 5 MB
    private const long AppLogMaxBytes = 512L * 1024; // 512 KB then roll
    private static readonly object Gate = new object();
    private static string _testDir;
    private static int _writing;

    /// <summary>Test hook — pass null to restore default AppData path.</summary>
    public static void SetStorageDirForTests(string directory)
    {
        _testDir = directory;
    }

    public static string RootDir()
    {
        if (!string.IsNullOrEmpty(_testDir))
            return _testDir;
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "QuickBrowse",
            "logs");
    }

    public static string AppLogPath()
    {
        return Path.Combine(RootDir(), "app.log");
    }

    public static string LastFatalPath()
    {
        return Path.Combine(RootDir(), "last-fatal.log");
    }

    public static void EnsureReady()
    {
        try
        {
            Directory.CreateDirectory(RootDir());
        }
        catch
        {
        }
    }

    /// <summary>Unhandled / process-ending failure. Writes crash_*.log + last-fatal + app line.</summary>
    public static string WriteFatal(Exception ex, string context)
    {
        if (Interlocked.Exchange(ref _writing, 1) == 1)
            return null;
        try
        {
            EnsureReady();
            string body = BuildReport("FATAL", ex, context);
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string type = ex != null ? SafeFileToken(ex.GetType().Name) : "unknown";
            string crashPath = Path.Combine(RootDir(), "crash_" + stamp + "_" + type + ".log");
            AtomicWrite(crashPath, body);
            AtomicWrite(LastFatalPath(), body);
            AppendAppLine("FATAL", context, ex);
            TrimLogDir();
            return crashPath;
        }
        catch
        {
            return null;
        }
        finally
        {
            Interlocked.Exchange(ref _writing, 0);
        }
    }

    /// <summary>Recoverable failure — appends to app.log only.</summary>
    public static void WriteError(Exception ex, string context)
    {
        try
        {
            EnsureReady();
            AppendAppLine("ERROR", context, ex);
            RollAppLogIfNeeded();
        }
        catch
        {
        }
    }

    public static void WriteInfo(string message)
    {
        try
        {
            EnsureReady();
            AppendRaw("INFO", message ?? "");
            RollAppLogIfNeeded();
        }
        catch
        {
        }
    }

    private static void AppendAppLine(string level, string context, Exception ex)
    {
        string msg = string.IsNullOrEmpty(context) ? "" : context;
        if (ex != null)
        {
            if (msg.Length > 0)
                msg += " | ";
            msg += ex.GetType().FullName + ": " + ex.Message;
            if (!string.IsNullOrEmpty(ex.StackTrace))
                msg += Environment.NewLine + ex.StackTrace;
        }
        AppendRaw(level, msg);
    }

    private static void AppendRaw(string level, string message)
    {
        lock (Gate)
        {
            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                + " [" + level + "] "
                + (message ?? "")
                + Environment.NewLine;
            File.AppendAllText(AppLogPath(), line, Encoding.UTF8);
        }
    }

    private static string BuildReport(string level, Exception ex, string context)
    {
        StringBuilder sb = new StringBuilder(2048);
        Version ver = null;
        try { ver = Assembly.GetExecutingAssembly().GetName().Version; } catch { }

        sb.AppendLine("QuickBrowse " + level + " report");
        sb.AppendLine("Time    : " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"));
        sb.AppendLine("UTC     : " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "Z");
        sb.AppendLine("Version : " + (ver != null ? ver.ToString() : "unknown"));
        sb.AppendLine("OS      : " + Environment.OSVersion);
        sb.AppendLine("CLR     : " + Environment.Version);
        sb.AppendLine("64-bit  : " + Environment.Is64BitProcess);
        sb.AppendLine("PID     : " + Process.GetCurrentProcess().Id);
        sb.AppendLine("Thread  : " + Thread.CurrentThread.ManagedThreadId);
        sb.AppendLine("Context : " + (context ?? ""));
        sb.AppendLine();

        Exception inner = ex;
        int depth = 0;
        if (inner == null)
            sb.AppendLine("(no exception object)");
        while (inner != null && depth < 6)
        {
            if (depth > 0)
            {
                sb.AppendLine();
                sb.AppendLine("=== Inner Exception ===");
            }
            sb.AppendLine("Type    : " + inner.GetType().FullName);
            sb.AppendLine("Message : " + inner.Message);
            sb.AppendLine("Stack:");
            sb.AppendLine(string.IsNullOrEmpty(inner.StackTrace) ? "(none)" : inner.StackTrace);
            inner = inner.InnerException;
            depth++;
        }
        return sb.ToString();
    }

    private static void AtomicWrite(string path, string body)
    {
        string tmp = path + ".tmp";
        File.WriteAllText(tmp, body ?? "", Encoding.UTF8);
        if (File.Exists(path))
        {
            try { File.Delete(path); } catch { }
        }
        File.Move(tmp, path);
    }

    private static void RollAppLogIfNeeded()
    {
        try
        {
            lock (Gate)
            {
                string path = AppLogPath();
                if (!File.Exists(path))
                    return;
                FileInfo fi = new FileInfo(path);
                if (fi.Length < AppLogMaxBytes)
                    return;
                string bak = Path.Combine(RootDir(), "app.prev.log");
                if (File.Exists(bak))
                {
                    try { File.Delete(bak); } catch { }
                }
                File.Move(path, bak);
            }
        }
        catch
        {
        }
    }

    private static void TrimLogDir()
    {
        try
        {
            DirectoryInfo dir = new DirectoryInfo(RootDir());
            if (!dir.Exists)
                return;
            FileInfo[] files = dir.GetFiles("crash_*.log");
            long total = 0;
            for (int i = 0; i < files.Length; i++)
                total += files[i].Length;
            if (total <= LogDirCapBytes)
                return;
            Array.Sort(files, delegate(FileInfo a, FileInfo b)
            {
                return a.LastWriteTimeUtc.CompareTo(b.LastWriteTimeUtc);
            });
            long half = LogDirCapBytes / 2;
            for (int i = 0; i < files.Length && total > half; i++)
            {
                try
                {
                    total -= files[i].Length;
                    files[i].Delete();
                }
                catch
                {
                }
            }
        }
        catch
        {
        }
    }

    private static string SafeFileToken(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "unknown";
        char[] bad = Path.GetInvalidFileNameChars();
        StringBuilder sb = new StringBuilder(name.Length);
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            bool ok = true;
            for (int j = 0; j < bad.Length; j++)
            {
                if (c == bad[j]) { ok = false; break; }
            }
            sb.Append(ok ? c : '_');
        }
        return sb.ToString();
    }
}
