# Manual test matrix — Pins / More + regression

Run `scripts\test.ps1` then `scripts\build.ps1` / launch `bin\QuickBrowse.exe` before this checklist.

## Automated gate

- [ ] `scripts\test.ps1` — all suites green (incl. PinStore, PinSession, Prefs)

## Pins / More

- [ ] **Pins is the top chip** on the rail (above Recent / This PC); flyout opens aligned to that top
- [ ] Pins opens flyout beside rail; empty state readable
- [ ] Flyout rows feel compact (not rail-tall); little empty space to the right of titles
- [ ] **With Explorer open, tap Pins → pane closes**, flyout visible (not behind Explorer)
- [ ] Pins again closes flyout only (pane stays closed)
- [ ] Open Downloads → Pin toolbar → status “Pinned”; reopen Pins → pin listed
- [ ] Restart app → pin still in Pins
- [ ] Tap pin → opens that folder; flyout closes; Pins chip selected
- [ ] Right-click pin in flyout → Unpin; list updates
- [ ] Toolbar Unpin when viewing pinned folder
- [ ] Context menu on folder row → Pin folder; on file → Pin disabled / N/A
- [ ] Pinning same path twice does not duplicate
- [ ] Missing pin path → soft status, pane not broken
- [ ] System place click closes flyout; Pins alone never navigates
- [ ] Idle ~30s closes pane **and** flyout
- [ ] Maximize / fullscreen park → **no left hairline**; hover left edge still reveals; flyout hidden while parked
- [ ] Coexistence with QuickView / JumpDock — no intermittent QV/JD flicker while QB is parked over fullscreen
- [ ] Recent chip sibling — see [`TEST-RECENT.md`](TEST-RECENT.md)

## Regression (shipped)

- [ ] All system places open; labels unclipped
- [ ] Views Icons / Tiles / Content / List / Details; pref survives restart
- [ ] Path Enter / Copy / Back / Up / Delete / Explorer / Close / Esc
- [ ] This PC = computer glyph (not folder)
- [ ] Tray Show / Exit; single instance
