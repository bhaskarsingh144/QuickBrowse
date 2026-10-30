using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

internal static class Program
{
    private const string ShortcutName = "QuickBrowse.lnk";

    [DllImport("user32.dll")]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

    [STAThread]
    private static void Main()
    {
        try
        {
            SetProcessDpiAwarenessContext(new IntPtr(-4));
        }
        catch
        {
        }
        bool created;
        using (Mutex mutex = new Mutex(true, @"Local\QuickBrowse", out created))
        {
            if (!created)
                return;
            EnsureStartup();
            CrashLog.EnsureReady();
            CrashLog.WriteInfo("Session start");
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += OnThreadException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandled;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                Application.Run(new ShellHost());
            }
            catch (Exception ex)
            {
                LogCrash(ex);
                throw;
            }
            finally
            {
                CrashLog.WriteInfo("Session end");
            }
        }
    }

    private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
    {
        // UI-thread unhandled: record a fatal marker; WinForms may keep running.
        LogCrash(e.Exception);
    }

    private static void OnUnhandled(object sender, UnhandledExceptionEventArgs e)
    {
        LogCrash(e.ExceptionObject as Exception);
    }

    /// <summary>Unhandled / fatal — writes under %LOCALAPPDATA%\QuickBrowse\logs.</summary>
    internal static void LogCrash(Exception ex)
    {
        CrashLog.WriteFatal(ex, "unhandled");
    }

    /// <summary>Recoverable error — appends to app.log only.</summary>
    internal static void LogError(Exception ex, string context)
    {
        CrashLog.WriteError(ex, context);
    }

    private static void EnsureStartup()
    {
        try
        {
            string exe = Application.ExecutablePath;
            if (string.IsNullOrEmpty(exe))
                return;
            string startupDir = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            string shortcut = Path.Combine(startupDir, ShortcutName);
            string prefDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuickBrowse");
            string pref = Path.Combine(prefDir, "startup.txt");
            string choice = ReadChoice(pref);
            bool exists = File.Exists(shortcut);
            if (choice == "off")
                return;
            if (choice == "on")
            {
                if (exists)
                    return;
                Directory.CreateDirectory(prefDir);
                File.WriteAllText(pref, "off");
                return;
            }
            if (!exists)
                CreateShortcut(shortcut, exe);
            if (File.Exists(shortcut))
            {
                Directory.CreateDirectory(prefDir);
                File.WriteAllText(pref, "on");
            }
        }
        catch
        {
        }
    }

    private static string ReadChoice(string pref)
    {
        try
        {
            if (!File.Exists(pref))
                return "";
            string text = File.ReadAllText(pref).Trim().ToLowerInvariant();
            if (text == "on" || text == "off")
                return text;
        }
        catch
        {
        }
        return "";
    }

    private static void CreateShortcut(string lnk, string exe)
    {
        Type shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType == null)
            return;
        object shell = Activator.CreateInstance(shellType);
        object shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnk });
        Type shortcutType = shortcut.GetType();
        string dir = Path.GetDirectoryName(exe);
        shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { exe });
        shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { dir ?? "" });
        shortcutType.InvokeMember("WindowStyle", BindingFlags.SetProperty, null, shortcut, new object[] { 1 });
        shortcutType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { "QuickBrowse" });
        shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
    }
}
