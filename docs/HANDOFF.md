# Handoff — open this in a new agent

**Repo:** `Documents/GitHub/QuickBrowse`  
**Do not modify:** `../QuickView` or `../JumpDock` unless the user explicitly asks.  
**OPEN-QUESTIONS defaults are locked** (2026-10-29). Implement against those decisions; do not re-open scope without asking.

## What the user asked for

1. New project **next to** QuickView (sibling folder) — left-edge, not right.
2. Aesthetically pleasing **file explorer companion**, better than stock Explorer for *their* workflow — not a full Explorer rewrite on day one.
3. Operates **on top of / beside** other apps like QuickView/JumpDock (edge dock, park on fullscreen, reveal from edge).
4. Resting UI shows **standard places** well (This PC, Downloads, …).
5. Selecting a place opens a **browse window connected to the left rail**; same “head” — disappears / parks with the rail.
6. Decent animation allowed for chrome show/hide; **not** animated folder-traversal gimmicks.
7. Plan **touch** early.
8. Document doubts + confirmations first; heavy plan; then a later agent implements.

## Suggested first actions for a follow-on agent

1. Read `OPEN-QUESTIONS.md` (defaults locked) + `TEST-MVP.md`.
2. Run `scripts\build.ps1` / `scripts\test.ps1`; exercise park/reveal with QuickView + JumpDock.
3. Polish Phase 4/5 only after smoke checklist passes (motion, touch validation).
4. Do not edit `../QuickView` or `../JumpDock` unless asked.

## Name

Working name: **QuickBrowse**. Rename is cheap if the user prefers QuickFiles / SideBrowse / FileRail / etc. (see OPEN-QUESTIONS).

## Temperature / unrelated

Ignore QuickView temperature research. Out of scope here.
