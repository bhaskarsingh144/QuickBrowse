# Open questions — confirm before implementation

**Status (2026-10-29):** Defaults locked. Geometry below matches the clean-rewrite freeze.

---

## 1. Product identity

- [x] **Name:** Keep `QuickBrowse`.
- [x] **Job statement:** “Left-edge places rail + attached browse pane for everyday folders.”
- [x] **Scope vs Explorer:** v0 is a **fast places browser**, not a full Explorer clone — OK.
- [x] **Who wins when both exist:** Both may open the same folder. **“Open in Explorer”** is a first-class action (toolbar + context menu).

---

## 2. Rail (resting left strip)

- [x] **Resting width:** **~96 DIP** with icon + short label (floating card, not full-height strip).
- [x] **What appears at rest:** Icon + short label (Downloads, Docs, …); tooltips for full names. Row height is **measure-driven** so labels never clip under the next chip.
- [x] **Default places (order):**

  1. This PC
  2. Desktop
  3. Downloads
  4. Documents
  5. Pictures
  6. Music
  7. Videos
  8. User profile folder

  *Out of v0:* Home / Quick access, Recycle Bin.

- [x] **This PC:** drives listed **inside the browse pane** only (rail entry opens This PC root).
- [x] **Pinned places:** shipping (Pins chip + flyout).
- [x] **Recent items:** shipping (Recent chip; Windows Recent Items — files/folders, not apps).
- [x] **JumpDock collision:** Left rail is ours. Top clear under JumpDock; WorkingArea layout.

---

## 3. Connected browse pane (“attached window”)

- [x] **Geometry:** floating **lower-left rail** (vertical center ~**0.52** of work height, ~10 DIP left inset); browse pane is a **detached submenu** with ~16 DIP gap (not glued/same height as rail).
- [x] **Pane size:** ~**1092×860 DIP** (~30% wider than early 840; independent; can extend above/below the rail).
- [x] **One pane or many:** **single** browse pane that swaps path.
- [x] **Lifecycle:** selecting another place **replaces** pane content; re-tapping the active place **closes** the pane.
- [x] **Close behavior:** × control, re-tap place, **Esc**.
- [x] **Park:** QuickView Decide → Place (off-screen `-32000`, PointerInEdge pull, ZOrder every shown tick for the chrome unit).
- [x] **Focus:** opening a pane must **not** steal keyboard focus (`ShowWithoutActivation`).
- [x] **Z-order:** topmost while revealing over fullscreen (`view == Reveal`).

---

## 4. Browsing model (inside the pane)

- [x] **View modes:** dropdown — **Tiles** (default) / **Content** / **Details**; pref persisted. Legacy icons/list map to Tiles. Tile cells ~134×124 DIP / icons ~68 DIP.
- [x] **Navigation:** **Back** + **Up**, editable path bar (Enter to go), **Copy** path, **Delete** selected (confirm).
- [x] **Enter folder / open file:** **single click/tap** opens.
- [x] **Open file:** default verb via `ShellExecute` / `Process.Start`.
- [x] **Context menu v0:** **minimal** — Open, Copy path, Delete, Show in Explorer.
- [x] **Drag-drop v0:** **none**.
- [x] **Multi-select:** **later**.
- [x] **Search:** **later**.
- [x] **Hidden / system files:** View menu **Show hidden items** (persisted); System / protected OS files stay hidden.
- [x] **Network / OneDrive / libraries:** no special UI; Known Folder redirects OK.
- [x] **Permissions / UAC paths:** fail soft with message in the pane.

---

## 5. Animation & aesthetics

- [x] **Allowed motion:** short opacity ease on pane show — OK.
- [x] **Forbidden:** zooming into folders, 3D flips, per-item fly animations — confirmed.
- [x] **Theme:** QuickView dark language; rounded cards; **Opacity = 1** (sharp ClearType).
- [x] **Fonts:** `pt = basePt × Fit × TextFit` (QuickView model).
- [x] **Acrylic / blur:** **flat solid** for .NET 4.5 reliability.
- [x] **Icons:** shell icons via `SHGetFileInfo` / stock This PC — OK.

---

## 6. Touch / pen / laptop

- [x] **Hit targets:** min ~52 DIP rail rows (measure-driven, touch-friendly).
- [x] **Open folder/file:** **single tap** opens.
- [x] **Edge reveal:** hover/sensor strip on left (wider than right-edge siblings for Win11 snap).

---

## 7. Shell behavior (park / reveal / desktops)

- [x] Match QuickView: park when maximized/fullscreen; reveal on edge hover; pin all virtual desktops; hide from Task View.
- [x] **Auto-start** with login like QuickView.
- [x] **Single instance** mutex `Local\QuickBrowse`.
- [x] **Coexistence:** companions QuickView / JumpDock / QuickBrowse.

---

## 8. Engineering

- [x] **Stack:** WinForms + **.NET Framework 4.5**.
- [x] **ShellChrome Left** in this repo only.
- [x] **Tests:** PlacesCatalog, DirectoryModel, DisplayScale, ChromeLayout / RailCellMetrics.
- [x] **Git remote / OSS:** local until usable; MIT in tree.

---

## 9. “Better than Explorer” — v0 must-haves

- [x] Faster jump to Downloads / Desktop / places
- [x] Cleaner visual density
- [x] Always available without hunting taskbar
- [x] Touch-friendly

---

## 10. Non-goals (v0)

- Replace Explorer as default host; full ribbon; cloud sync UI; archive-as-folders; embedded terminal; shell namespace installers; global hotkey; FileSystemWatcher; pins/recents/DnD/multi-select.

---

## Extra defaults

| Topic | Decision |
| --- | --- |
| Quit | Tray → Show / Exit |
| Idle reset | ~30s no activity → close pane + clear place |
| Keyboard | Esc close; Enter open; Alt+Up up |
| Layout | Rail center ~0.52; pane ~1092×860; gap ~16 DIP |
