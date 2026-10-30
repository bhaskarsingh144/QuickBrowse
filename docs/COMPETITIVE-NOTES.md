# Competitive notes — “better than Explorer”

Not a teardown of every Explorer feature. Focus: where a **left-edge companion** can win without cloning the shell.

## Where stock Explorer hurts (hypotheses — confirm which match you)

1. **Window management** — Explorer is another window to place, snap, lose behind others.
2. **Cold start to a known place** — taskbar → Explorer → click Downloads again.
3. **Visual noise** — ribbon, nav pane, home recommendations, ads/suggestions on Home.
4. **Touch** — small rows, double-click bias, awkward on laptop screens.
5. **Always-available chrome** — nothing edge-docked for *files* the way QuickView is for metrics.

## Where Explorer still wins (do not fight early)

- Full shell namespace (Control Panel, network neighborhood quirks)
- Shell `IContextMenu` / third-party shell verbs (QB uses managed dark menus; Open in Explorer for the rest)
- Preview handlers, complex multi-select + drag to arbitrary targets
- Search indexed query UI
- Default file manager replacement / set-as-default politics

**Shipped:** managed rounded Win11-like menus with icon row; Open / Open with; Send to (Desktop/Documents/Downloads); Compress/Extract ZIP; Cut/Copy/Paste/Rename/Delete→Recycle; Properties; header Cut/Copy/Paste/Delete chips. See [`TEST-CONTEXT.md`](TEST-CONTEXT.md).

**Escape hatch:** “Show in Explorer” / “Open in Explorer” for full shell verbs.

## What “better” can mean for QuickBrowse v0

| Lever | How |
| --- | --- |
| **Latency to place** | Rail always one edge-gesture away |
| **Calm UI** | Places + list; no Home feed |
| **Attached pane** | No orphan windows; parks with chrome |
| **Touch-first targets** | Designed hit sizes |
| **Aesthetics** | Cohesive with QuickView/JumpDock family |

## Inspiration (study, don’t clone)

- macOS Finder sidebar + column ideas (without copying Apple UI)
- AltTab / PowerToys Run (summon speed) — different job, same “always near” energy
- Directory Opus / XYplorer — power features are a **later** temptation; resist until pins/recents proven

## Anti-goals reminder

If a feature doesn’t make “Downloads in two taps” better, it waits.
