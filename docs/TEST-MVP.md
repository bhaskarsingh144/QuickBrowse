# Manual test matrix — MVP

Run `scripts\test.ps1`, then `scripts\setup-standalone.bat` (or `bin\QuickBrowse.exe` after `build.ps1`).

Pins: see [`TEST-PINS.md`](TEST-PINS.md). Recent: see [`TEST-RECENT.md`](TEST-RECENT.md). Context menus: see [`TEST-CONTEXT.md`](TEST-CONTEXT.md).

## Smoke

- [ ] Single instance: second launch exits quietly
- [ ] Rail floating lower-left (~96 DIP wide, center ~mid/lower work area)
- [ ] **Every rail label fully visible** (This PC, Desktop, Downloads, …) — no clip under next chip
- [ ] **Pins** + **Recent** chips at top of rail; flyouts beside rail
- [ ] Tooltip on place hover
- [ ] Click Downloads → detached pane opens with gap; lists folder
- [ ] Single-click folder enters; single-click file opens with shell
- [ ] Right-click item → shell menu (Open with / Properties / type verbs); empty area → background menu
- [ ] Back / Up / crumbs / Ctrl+L·F4 edit path / New folder · Refresh · Clipboard (Copy/Cut/Paste) / Del / Explorer / Pin / view / ×; F5 · Ctrl+Shift+N · Ctrl+C/X/V; Esc / re-tap; Enter / Alt+Up / Delete key
- [ ] View dropdown: Tiles / Content / Details; preference survives restart
- [ ] Access-denied / empty folder show status text
- [ ] Tray → Show / Exit
- [ ] Idle ~30s without activity closes pane (+ flyout if open)

## Shell / coexistence

- [ ] Maximize app → rail (+ pane if open) parks fully off-screen
- [ ] Hover left edge → reveals **above** maximized app (no Win+D needed)
- [ ] Open This PC pane → JumpDock/QuickView do **not** flicker (see [`TEST-SHELL.md`](TEST-SHELL.md))
- [ ] QuickView (right) + JumpDock (top) + QuickBrowse (left) together
- [ ] Text remains sharp (no fuzzy ClearType)

## Touch (Phase 5 follow-up)

- [ ] Tap places without missing
- [ ] Scroll long Downloads without accidental opens
- [ ] Reveal while maximized browser is up
