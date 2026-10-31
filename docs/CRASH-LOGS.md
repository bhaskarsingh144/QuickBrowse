# Crash and error logs

QuickBrowse keeps local diagnostics under:

`%LOCALAPPDATA%\QuickBrowse\logs\`

| File | Purpose |
| --- | --- |
| `app.log` | Rolling session + recoverable errors (`[INFO]` / `[ERROR]` / `[FATAL]` lines). Rolls to `app.prev.log` around 512 KB. |
| `last-fatal.log` | Always the most recent unhandled/fatal report (overwritten). |
| `crash_yyyyMMdd_HHmmss_Type.log` | One file per fatal; oldest trimmed when the folder exceeds ~5 MB. |
| `session.running` | Present while a session is active; left behind after a kill / hard exit. |
| `last-fatal.ack` | Stamp of the last fatal the user already dismissed. |

## User notifications

Logging alone is easy to miss. QuickBrowse also shows a **MessageBox** with a dynamic title/body:

1. **On crash (unhandled exception)** — title like `QuickBrowse — Argument` (from the exception type); body includes the message and log folder. Default action is **restart** (delayed so the single-instance mutex clears). UI-thread faults also offer **continue** or **exit**.
2. **On next startup** — if the previous run left `session.running` (force-quit / kill) or an unacked `last-fatal.log`, a warning titled `QuickBrowse — previous crash` or `QuickBrowse — closed unexpectedly` can open the logs folder.

Clean exits and intentional restarts remove `session.running` and write `Session end` to `app.log`. Native / process-corrupting faults may still bypass managed logging and only leave the running flag.

## What is captured

- UI-thread unhandled exceptions (`Application.ThreadException`)
- Non-UI unhandled exceptions (`AppDomain.UnhandledException`)
- Top-level `Application.Run` failures
- Soft failures that call `Program.LogError` (open file, context menu, shell sync, …)

Each fatal report includes local/UTC time, app version, OS, CLR, PID/thread, context string, and exception chain + stacks.

Handlers stay best-effort and must not throw; native process-corrupting faults may still bypass managed logging.

## Debugging

1. Reproduce the issue.
2. Open `%LOCALAPPDATA%\QuickBrowse\logs\`.
3. Read `last-fatal.log` first, then the matching `crash_*.log` and recent `app.log` lines.
4. Attach those files when filing a bug (see CONTRIBUTING.md).
