# Touch behavior (draft)

Touch is a first-class design constraint, not a polish pass. Confirm items in [`OPEN-QUESTIONS.md`](OPEN-QUESTIONS.md) §6.

## Principles

1. **Fat targets** — places and list rows ≥ ~40–48 DIP tall; gaps that forgive imprecise taps.
2. **One clear tap language** — avoid “sometimes single, sometimes double” without UI cues.
3. **Scroll is sacred** — vertical pan on the file list must not be stolen by accidental navigation.
4. **Edge reveal** — left-edge affordance must not fight Win11 gesture nav more than the user tolerates.
5. **Mouse still excellent** — touch sizing should not make the rail look childish on desktop.

## Proposed interaction map (needs confirmation)

| Intent | Mouse | Touch |
| --- | --- | --- |
| Reveal parked rail | Hover left strip | Swipe/tap left edge strip |
| Open place (Downloads, …) | Click rail icon | Tap rail icon |
| Enter folder in pane | **TBD: double-click vs single** | Prefer **single tap** to enter |
| Open file | Double-click or Enter | Tap file → open (or tap selects, button opens — TBD) |
| Scroll list | Wheel / drag scrollbar | Finger pan |
| Up one level | Button / Alt+Up | Large Up / breadcrumb tap |
| Close pane | × / Esc / re-tap place | × / swipe pane away? (maybe later) |
| Context actions | Right-click | Long-press |

## Conflict: single-tap enter vs select

Explorer classic: double-click opens. Touch-friendly UIs: single tap opens.

**Options:**

1. **Single tap opens everything** (folder enter / file open) — best for touch; mouse users adapt; multi-select harder.
2. **Single tap selects; Open button / double-tap opens** — safer; more chrome.
3. **Setting:** “Click items as follows” like Explorer — flexible; more work.

**Ask user which default.**

## Edge gestures

- Implement a **24 DIP** (scaled) hover/touch strip like QuickView’s right edge, mirrored left.
- Optional: treat contact starting within strip + move right as “reveal”.
- Document how to disable if it fights tablet back-gesture (Win11).

## Feedback

- Pressed state on rail icons (scale or highlight) — short, ≤100–150ms feel.
- No folder-flip animation when changing path; optional 80–120ms pane opacity only.

## Pen

Treat as mouse unless we hear otherwise; long-press still for context.

## Validation checklist (manual later)

- [ ] Tap every default place with a finger without missing
- [ ] Scroll a long Downloads list without opening random files
- [ ] Reveal while a maximized browser is up
- [ ] JumpDock top + QuickBrowse left + QuickView right all running
- [ ] Rotate/tablet mode if you use it
