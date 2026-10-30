# Crash and error logs

QuickBrowse keeps local diagnostics under:

`%LOCALAPPDATA%\QuickBrowse\logs\`

| File | Purpose |
| --- | --- |
| `app.log` | Rolling session + recoverable errors (`[INFO]` / `[ERROR]` / `[FATAL]` lines). Rolls to `app.prev.log` around 512 KB. |
| `last-fatal.log` | Always the most recent unhandled/fatal report (overwritten). |
| `crash_yyyyMMdd_HHmmss_Type.log` | One file per fatal; oldest trimmed when the folder exceeds ~5 MB. |

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
