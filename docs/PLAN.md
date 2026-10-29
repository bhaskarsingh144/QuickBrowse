# Plan — QuickBrowse

Living product plan. **OPEN-QUESTIONS defaults locked (2026-10-29)** — see [`OPEN-QUESTIONS.md`](OPEN-QUESTIONS.md).

## Vision

A left-edge **places rail** that feels like part of the desktop chrome (family: QuickView / JumpDock), plus a **connected browse pane** for everyday file work — faster path to the folders you open constantly, without Explorer’s window management tax.

## Experience sketch (v0)

```text
┌────┬──────────────────────────────┐
│ ■  │  Downloads            [×]    │  ← browse pane (attached)
│ ↓  │  ─────────────────────────   │
│ 🖿 │  invoice.pdf                 │
│ 📄 │  photo.jpg                   │
│ 🖼 │  notes/                      │
│    │  ...                         │
│    │                              │
└────┴──────────────────────────────┘
  ↑
 rail (always left; parks with pane)
```

1. Rest: only the rail (or rail mostly parked off-screen until left-edge hover/touch).
2. Tap/click **Downloads** → pane slides/fades attached to rail, listing that folder.
3. Enter subfolders in the pane (no fancy transit animation).
4. Open file → normal Windows association; pane stays open until closed (× / re-tap / Esc).
5. Fullscreen game/app → rail + pane park; peek left edge to restore.

## Phases

### Phase 0 — Decide (done)

- OPEN-QUESTIONS answered with MVP defaults.
- Name QuickBrowse; WinForms 4.5; 64 DIP icon rail; single-tap open; one attached pane; same-head park.

### Phase 1 — Shell skeleton

- WinForms app, mutex, DPI, left-edge park/reveal (`ShellEdge.Left`).
- Empty rail chrome that parks correctly with QuickView/JumpDock running.
- Manual test matrix: maximize, fullscreen, multi-monitor, virtual desktop.

### Phase 2 — Places rail

- Resolve Known Folders (Downloads, Desktop, …) + This PC.
- Icons from shell; hover/press states; touch-sized hits.
- Selecting a place raises/updates the browse pane.

### Phase 3 — Browse pane (MVP)

- Attached window; same park/reveal head as rail.
- Enumerate directory (files + folders); sort folders first then name.
- Navigate into folders; Up / breadcrumb minimal.
- Open file via shell; “Open in Explorer” escape hatch.
- Empty / access-denied states.

### Phase 4 — Feel

- Reveal/hide motion (short, opacity/slide).
- Typography, spacing, dark/light decision from OPEN-QUESTIONS.
- No folder zoom animations.

### Phase 5 — Touch pass

- Single-tap navigation policy locked.
- Scroll physics, long-press menu, edge reveal.
- Validate on the real laptop touchscreen.

### Phase 6 — Power features (only if ranked in OPEN-QUESTIONS)

- Pins, recents, details view, multi-select, drag-drop, search filter, tabs.

## MVP cut (initial release)

**In:** Phase 1–3 + light Phase 4 (dark theme, short opacity on show/hide).

**Out of first release:** pins, recents, hotkey, drag-drop, multi-select, rename/new/delete, FileSystemWatcher, full touch validation pass (Phase 5), acrylic.

## Success criteria (v0)

- Reach Downloads in ≤2 interactions from a parked rail.
- Pane never “orphans” (always tied to rail head).
- Looks intentional next to QuickView/JumpDock, not like a random WinForms dialog.
- Touch usable without precision mouse aiming.
- Zero telemetry; standalone build script like siblings.

## Risks

| Risk | Mitigation |
| --- | --- |
| Left edge fights Win11 tablet gestures | Configurable reveal; optional disable swipe |
| Shell IContextMenu / drag-drop complexity | IContextMenu hosted for browse pane; drag-drop still deferred |
| Large folders UI jank | Virtualized list early if .NET 4.5 ListView pain → reconsider stack |
| Three edge apps overcrowding | Document reserved edges; allow disable auto-start |
| “Better than Explorer” scope creep | Non-goals list; rank must-haves |

## Out of scope until asked

- Shipping to GitHub / branch protection (copy QuickView process later).
- Shared NuGet for ShellChrome.
- Replacing Explorer as shell namespace.
