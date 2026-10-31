using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Forms;

/// <summary>
/// Surfaces crashes / unclean exits to the user and can relaunch QuickBrowse.
/// Logging stays in CrashLog; titles/bodies are built from the event kind + exception.
/// </summary>
internal static class CrashNotify
{
    private const string RunningFlagName = "session.running";
    private const string FatalAckName = "last-fatal.ack";

    public static bool DetectUncleanPriorSession()
    {
        try
        {
            return File.Exists(RunningFlagPath());
        }
        catch
        {
            return false;
        }
    }

    public static void MarkSessionRunning()
    {
        try
        {
            CrashLog.EnsureReady();
            File.WriteAllText(RunningFlagPath(), DateTime.UtcNow.ToString("o"), Encoding.UTF8);
        }
        catch
        {
        }
    }

    public static void MarkSessionEnded()
    {
        try
        {
            string path = RunningFlagPath();
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
        }
    }

    public static bool HasUnackedFatal()
    {
        try
        {
            string fatal = CrashLog.LastFatalPath();
            if (!File.Exists(fatal))
                return false;
            string ack = FatalAckPath();
            if (!File.Exists(ack))
                return true;
            string fatalStamp = File.GetLastWriteTimeUtc(fatal).Ticks.ToString();
            string ackText = File.ReadAllText(ack).Trim();
            return !string.Equals(fatalStamp, ackText, StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    public static void AcknowledgeFatal()
    {
        try
        {
            CrashLog.EnsureReady();
            string fatal = CrashLog.LastFatalPath();
            if (!File.Exists(fatal))
            {
                try { File.Delete(FatalAckPath()); } catch { }
                return;
            }
            string stamp = File.GetLastWriteTimeUtc(fatal).Ticks.ToString();
            File.WriteAllText(FatalAckPath(), stamp, Encoding.UTF8);
        }
        catch
        {
        }
    }

    public static string ShortExceptionName(Exception ex)
    {
        if (ex == null)
            return "Unknown error";
        string name = ex.GetType().Name;
        if (name.EndsWith("Exception", StringComparison.Ordinal) && name.Length > "Exception".Length)
            name = name.Substring(0, name.Length - "Exception".Length);
        if (string.IsNullOrEmpty(name))
            return "Error";
        return name;
    }

    public static string BuildFatalTitle(Exception ex)
    {
        return "QuickBrowse — " + ShortExceptionName(ex);
    }

    public static string FormatExceptionDetail(Exception ex)
    {
        string detail = ex != null
            ? ex.GetType().Name + ": " + (ex.Message ?? "")
            : "Unknown error";
        if (detail.Length > 280)
            detail = detail.Substring(0, 279) + "…";
        return detail;
    }

    public static string BuildFatalBody(Exception ex, string logsDir, bool offerContinue)
    {
        string detail = FormatExceptionDetail(ex);
        string actions = offerContinue
            ? "Yes = restart QuickBrowse"
                + Environment.NewLine + "No = keep this session (may be unstable)"
                + Environment.NewLine + "Cancel = exit"
            : "Yes = restart QuickBrowse"
                + Environment.NewLine + "No = exit";

        return "QuickBrowse hit a problem"
            + (offerContinue ? "." : " and needs to stop.")
            + Environment.NewLine + Environment.NewLine
            + detail
            + Environment.NewLine + Environment.NewLine
            + "A report was saved under:"
            + Environment.NewLine + (logsDir ?? "")
            + Environment.NewLine + Environment.NewLine
            + actions;
    }

    public static string BuildStartupTitle(bool unackedFatal, bool uncleanPrior)
    {
        if (unackedFatal)
            return "QuickBrowse — previous crash";
        if (uncleanPrior)
            return "QuickBrowse — closed unexpectedly";
        return "QuickBrowse";
    }

    public static string BuildStartupBody(bool unackedFatal, bool uncleanPrior, string logsDir)
    {
        if (unackedFatal)
        {
            string summary = SummarizeLastFatal(280);
            return "QuickBrowse is starting again after a crash."
                + Environment.NewLine + Environment.NewLine
                + (string.IsNullOrEmpty(summary) ? "" : summary + Environment.NewLine + Environment.NewLine)
                + "Report folder:"
                + Environment.NewLine + (logsDir ?? "")
                + Environment.NewLine + Environment.NewLine
                + "Open the logs folder now?";
        }

        return "QuickBrowse closed unexpectedly last time (no crash report — often a force-quit or kill)."
            + Environment.NewLine + Environment.NewLine
            + "Session log:"
            + Environment.NewLine + (logsDir ?? "")
            + Environment.NewLine + Environment.NewLine
            + "Open the logs folder now?";
    }

    public static string SummarizeLastFatal(int maxChars)
    {
        if (maxChars < 40)
            maxChars = 40;
        try
        {
            string path = CrashLog.LastFatalPath();
            if (!File.Exists(path))
                return "";
            string body = File.ReadAllText(path);
            if (string.IsNullOrEmpty(body))
                return "";
            string type = ExtractLine(body, "Type    : ");
            string msg = ExtractLine(body, "Message : ");
            string time = ExtractLine(body, "Time    : ");
            StringBuilder sb = new StringBuilder();
            if (!string.IsNullOrEmpty(time))
                sb.AppendLine("When: " + time);
            if (!string.IsNullOrEmpty(type))
                sb.AppendLine(type);
            if (!string.IsNullOrEmpty(msg))
                sb.AppendLine(msg);
            string text = sb.ToString().Trim();
            if (text.Length > maxChars)
                text = text.Substring(0, maxChars - 1) + "…";
            return text;
        }
        catch
        {
            return "";
        }
    }

    public static string RunningFlagPath()
    {
        return Path.Combine(CrashLog.RootDir(), RunningFlagName);
    }

    public static string FatalAckPath()
    {
        return Path.Combine(CrashLog.RootDir(), FatalAckName);
    }

    public static void ShowStartupIfNeeded(bool uncleanPrior, bool unackedFatal)
    {
        if (!uncleanPrior && !unackedFatal)
            return;
        try
        {
            string logs = CrashLog.RootDir();
            string title = BuildStartupTitle(unackedFatal, uncleanPrior);
            string body = BuildStartupBody(unackedFatal, uncleanPrior, logs);
            DialogResult r = MessageBox.Show(
                body,
                title,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (r == DialogResult.Yes)
                TryOpenLogsFolder(logs);
            if (unackedFatal)
                AcknowledgeFatal();
        }
        catch
        {
            try { if (unackedFatal) AcknowledgeFatal(); } catch { }
        }
    }

    /// <summary>
    /// Immediate alert after a fatal is logged. Offers restart (default) or exit.
    /// Clears the running flag before relaunch so the new session is not flagged unclean.
    /// </summary>
    public static void ShowFatalNow(Exception ex, bool offerContinue)
    {
        try
        {
            string logs = CrashLog.RootDir();
            string title = BuildFatalTitle(ex);
            string body = BuildFatalBody(ex, logs, offerContinue);
            if (offerContinue)
            {
                DialogResult r = MessageBox.Show(
                    body,
                    title,
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Error,
                    MessageBoxDefaultButton.Button1);
                AcknowledgeFatal();
                if (r == DialogResult.Yes)
                    RestartApplication();
                else if (r == DialogResult.Cancel)
                    ExitApplication();
                return;
            }

            DialogResult choice = MessageBox.Show(
                body,
                title,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Error,
                MessageBoxDefaultButton.Button1);
            AcknowledgeFatal();
            if (choice == DialogResult.Yes)
                RestartApplication();
            else
                ExitApplication();
        }
        catch
        {
            try { AcknowledgeFatal(); } catch { }
            try { RestartApplication(); } catch { ExitApplication(); }
        }
    }

    public static void RestartApplication()
    {
        try
        {
            // Intentional relaunch — clear running flag so the new session is not "unclean".
            MarkSessionEnded();
            string exe = Application.ExecutablePath;
            if (!string.IsNullOrEmpty(exe) && File.Exists(exe))
            {
                // Delay start so this process can release the single-instance mutex first.
                string quoted = "\"" + exe.Replace("\"", "") + "\"";
                string args = "/c timeout /t 1 /nobreak >nul & start \"\" " + quoted;
                Process.Start(new ProcessStartInfo("cmd.exe", args)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WorkingDirectory = Path.GetDirectoryName(exe) ?? ""
                });
            }
        }
        catch
        {
        }
        finally
        {
            ExitApplication();
        }
    }

    public static void ExitApplication()
    {
        try { MarkSessionEnded(); } catch { }
        try { Application.Exit(); } catch { }
        try { Environment.Exit(1); } catch { }
    }

    private static void TryOpenLogsFolder(string logs)
    {
        try
        {
            if (string.IsNullOrEmpty(logs))
                return;
            Directory.CreateDirectory(logs);
            Process.Start("explorer.exe", logs);
        }
        catch
        {
        }
    }

    private static string ExtractLine(string body, string prefix)
    {
        if (string.IsNullOrEmpty(body) || string.IsNullOrEmpty(prefix))
            return "";
        using (StringReader sr = new StringReader(body))
        {
            string line;
            while ((line = sr.ReadLine()) != null)
            {
                if (line.StartsWith(prefix, StringComparison.Ordinal))
                    return line.Substring(prefix.Length).Trim();
            }
        }
        return "";
    }
}
