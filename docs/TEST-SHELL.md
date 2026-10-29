# Manual test matrix — Shell park / reveal / coexistence

Run `scripts\test.ps1` then `scripts\build.ps1` / launch `bin\QuickBrowse.exe`.
Also rebuild **QuickView** and **JumpDock** after companion edits (`QuickBrowse` in `IsCompanion`).

Appearance/disappearance is the **QuickView** Decide → Place loop (no EdgeSensor, no QB-only park hacks).

## Left reveal (priority)

- [ ] Fullscreen / maximized app → hover **left** edge → rail appears (**no Win+D**)
- [ ] Desktop / no filling app → rail stays visible (Rest), same as QuickView on the right
- [ ] Leave left edge under fullscreen → parks off-screen; hover left again still works
- [ ] Win+D → see rail on empty desktop (Rest); return to fullscreen → parks; left hover still works
- [ ] After parking once, left hover keeps working without Win+D

## Pane vs flyout

- [ ] Pins / Recent / This PC → open and use; leave left edge under fullscreen → parks; re-hover works
- [ ] Hover top/right after using left → JumpDock / QuickView still reveal
- [ ] Intentionally leave sibling flicker for a later pass if it returns

## Intentional sibling edges

- [ ] Hover **top** → JumpDock still reveals
- [ ] Hover **right** → QuickView still reveals

## Notes

- **Do not regress left hover:** see LOCK on `ShellHost`.
- **Hover-only show** while any workspace window exists; Rest without hover only on empty desktop. Opening QB pane / OSK / focus changes must not pop JD/QV.
