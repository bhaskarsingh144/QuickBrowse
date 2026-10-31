using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

/// <summary>
/// Surfaces crashes / unclean exits to the user (MessageBox). Logging stays in CrashLog;
/// this layer only decides when to alert and acknowledge.
/// </summary>
internal static class CrashNotify
{
    private const string RunningFlagName = "session.running";
    private const string FatalAckName = "last-fatal.ack";

    /// <summary>True when a previous run left the session.running flag (kill / hard crash).</summary>
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

    /// <summary>First lines of last-fatal for dialog body (best-effort).</summary>
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

    /// <summary>Call once after deciding unclean/fatal flags, before or after Session start log.</summary>
    public static void ShowStartupIfNeeded(bool uncleanPrior, bool unackedFatal)
    {
        if (!uncleanPrior && !unackedFatal)
            return;
        try
        {
            string logs = CrashLog.RootDir();
            string title = "QuickBrowse";
            string body;
            if (unackedFatal)
            {
                string summary = SummarizeLastFatal(280);
                body = "QuickBrowse crashed previously."
                    + Environment.NewLine + Environment.NewLine
                    + (string.IsNullOrEmpty(summary) ? "" : summary + Environment.NewLine + Environment.NewLine)
                    + "A report was saved under:"
                    + Environment.NewLine + logs
                    + Environment.NewLine + Environment.NewLine
                    + "Open the logs folder now?";
            }
            else
            {
                body = "QuickBrowse closed unexpectedly last time (no crash report was written — often a force-quit or kill)."
                    + Environment.NewLine + Environment.NewLine
                    + "Session log:"
                    + Environment.NewLine + logs
                    + Environment.NewLine + Environment.NewLine
                    + "Open the logs folder now?";
            }

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

    /// <summary>Immediate alert when an unhandled exception is logged.</summary>
    public static void ShowFatalNow(Exception ex)
    {
        try
        {
            string logs = CrashLog.RootDir();
            string detail = ex != null
                ? ex.GetType().Name + ": " + ex.Message
                : "Unknown error";
            if (detail.Length > 240)
                detail = detail.Substring(0, 239) + "…";
            string body = "QuickBrowse hit an error and may stop working."
                + Environment.NewLine + Environment.NewLine
                + detail
                + Environment.NewLine + Environment.NewLine
                + "A report was saved under:"
                + Environment.NewLine + logs
                + Environment.NewLine + Environment.NewLine
                + "Open the logs folder now?";
            DialogResult r = MessageBox.Show(
                body,
                "QuickBrowse — crash",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Error,
                MessageBoxDefaultButton.Button2);
            if (r == DialogResult.Yes)
                TryOpenLogsFolder(logs);
            AcknowledgeFatal();
        }
        catch
        {
            try { AcknowledgeFatal(); } catch { }
        }
    }

    private static void TryOpenLogsFolder(string logs)
    {
        try
        {
            if (string.IsNullOrEmpty(logs))
                return;
            Directory.CreateDirectory(logs);
            System.Diagnostics.Process.Start("explorer.exe", logs);
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
