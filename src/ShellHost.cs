using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

/// <summary>
/// Owns rail + Pins flyout + Recent panel + browse pane.
/// Click semantics live in PinSession (unit-tested).
///
/// LOCK — left appearance (do not regress):
/// 1) Decide: hover-only show while any workspace window exists; Rest only on empty
///    desktop. fills = enum maximized | EdgeCovered (never bury Rest behind fullscreen).
/// 2) Place: rail ZOrder every shown tick; pane/flyout every tick only while Reveal,
///    else on show/open/topmost transitions only (avoids JD/QV flicker).
/// 3) No EdgeSensor — parked pull is PointerInEdge geometry only.
/// 4) Start Place(false,false); tick drives Rest/Reveal/Park.
/// </summary>
internal sealed class ShellHost : ApplicationContext
{
    private const int IdleResetMs = 30000;
    private const int Offscreen = -32000;
    private const int RecentRefreshMs = 5000;

    private readonly PinStore _pins;
    private readonly PinSession _session;
    private readonly RailForm _rail;
    private readonly BrowseForm _pane;
    private readonly PinsFlyoutForm _flyout;
    private readonly RecentPanelForm _recent;
    private readonly System.Windows.Forms.Timer _tick;
    private readonly NotifyIcon _tray;
    private readonly ContextMenuStrip _trayMenu;

    private bool _syncing;
    private bool _shown;
    private int _recentRefreshTick;
    private bool _placedShow;
    private bool _placedTopmost;
    private bool _placedPaneZ;
    private bool _placedFlyZ;
    private bool _placedRecentZ;

    public ShellHost()
    {
        _pins = new PinStore();
        _session = new PinSession(_pins);
        _rail = new RailForm(this);
        _pane = new BrowseForm(this);
        _flyout = new PinsFlyoutForm(this);
        _recent = new RecentPanelForm(this);

        _trayMenu = new ContextMenuStrip();
        _trayMenu.Items.Add("Show", null, delegate { NoteActivity(); Place(true, true); });
        _trayMenu.Items.Add("Exit", null, delegate { ExitThread(); });

        _tray = new NotifyIcon();
        _tray.Text = "QuickBrowse";
        _tray.Icon = SystemIcons.Application;
        _tray.Visible = true;
        _tray.ContextMenuStrip = _trayMenu;
        _tray.DoubleClick += delegate
        {
            NoteActivity();
            Place(true, true);
        };

        _tick = new System.Windows.Forms.Timer();
        _tick.Interval = 16;
        _tick.Tick += OnTick;
        _tick.Start();

        _rail.Opacity = ChromeVisual.PanelOpacity;
        _pane.Opacity = 0.0;
        _flyout.Opacity = 0.0;
        _recent.Opacity = 0.0;
        _rail.Show();
        _pane.Show();
        _flyout.Show();
        _recent.Show();
        NoteActivity();
        ApplySessionToChrome();
        // Same as QuickView OnHandleCreated: start parked; first tick Rest/Reveal as Decide says.
        Place(false, false);
        LayoutChrome();
    }

    public RailForm Rail { get { return _rail; } }
    public PinStore Pins { get { return _pins; } }
    public PinSession Session { get { return _session; } }

    public void NoteActivity()
    {
        _session.NoteActivity();
    }

    public void OnPinsClicked()
    {
        _session.TogglePins();
        ApplySessionToChrome();
        SetPaneVisible(_session.PaneOpen);
        Place(_shown, true);
    }

    public void OnMoreClicked()
    {
        OnPinsClicked();
    }

    public void OnRecentClicked()
    {
        _session.ToggleRecent();
        if (_session.RecentPanelOpen)
            RefreshRecentPanel(true);
        ApplySessionToChrome();
        SetPaneVisible(_session.PaneOpen);
        Place(_shown, true);
    }

    public void CloseRecentPanel()
    {
        _session.CloseRecent();
        ApplySessionToChrome();
        Place(_shown, true);
    }

    public void OnRecentItemClicked(string path)
    {
        bool ok = _session.OpenRecentPath(path);
        ApplySessionToChrome();
        if (!ok)
        {
            if (!string.IsNullOrEmpty(_session.LastError))
                _pane.ShowStatus(_session.LastError);
            Place(_shown, true);
            return;
        }

        if (_session.LastRecentActivate == RecentActivateKind.Folder)
        {
            RecentSource.NotifyAccess(_session.LastRecentPath);
            _pane.NavigateTo(_session.ActivePath, PinStore.TitleFromPath(_session.ActivePath));
            SetPaneVisible(true);
        }
        else if (_session.LastRecentActivate == RecentActivateKind.File)
        {
            string launch = _session.LastRecentPath;
            RecentSource.NotifyAccess(launch);
            try
            {
                Process.Start(new ProcessStartInfo(launch) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                _pane.ShowStatus("Could not open: " + ex.Message);
            }
            SetPaneVisible(_session.PaneOpen);
        }
        Place(_shown, true);
    }

    public void NotifyRecentAccess(string path)
    {
        RecentSource.NotifyAccess(path);
    }

    public void OnPlaceClicked(int index, PlaceEntry place)
    {
        if (place == null)
            return;
        string path = PlacesCatalog.ResolvePath(place);
        _session.OpenPlace(index, place.Id, path);
        ApplySessionToChrome();
        if (_session.PaneOpen)
        {
            if (_session.ActiveThisPc)
                _pane.ShowPlace(place);
            else
                _pane.ShowPlace(place);
            SetPaneVisible(true);
        }
        else
        {
            SetPaneVisible(false);
        }
        Place(_shown, true);
    }

    public void OnPinClicked(string path)
    {
        bool ok = _session.OpenPin(path);
        ApplySessionToChrome();
        if (ok)
        {
            _pane.NavigateTo(_session.ActivePath, PinStore.TitleFromPath(_session.ActivePath));
            SetPaneVisible(true);
        }
        else if (!string.IsNullOrEmpty(_session.LastError))
        {
            _pane.ShowStatus(_session.LastError);
        }
        Place(_shown, true);
    }

    public void OnUnpinPath(string path)
    {
        _session.UnpinPath(path);
        ApplySessionToChrome();
        _pane.SyncPinChrome();
        Place(_shown, true);
    }

    public bool PinCurrentFolder()
    {
        // Sync session path from pane before pin.
        SyncPathFromPane();
        bool ok = _session.PinCurrent();
        ApplySessionToChrome();
        _pane.SyncPinChrome();
        return ok;
    }

    public bool UnpinCurrentFolder()
    {
        SyncPathFromPane();
        bool ok = _session.UnpinCurrent();
        ApplySessionToChrome();
        _pane.SyncPinChrome();
        return ok;
    }

    public bool PinFolderPath(string path)
    {
        bool ok = _session.PinPath(path);
        ApplySessionToChrome();
        _pane.SyncPinChrome();
        return ok;
    }

    public bool IsPathPinned(string path)
    {
        return _pins.Contains(path);
    }

    public void ClosePane()
    {
        _session.ClosePane();
        ApplySessionToChrome();
        SetPaneVisible(false);
        Place(_shown, true);
    }

    public void OnDisplayChanged()
    {
        _rail.Remeasure();
        _pane.Remeasure();
        _flyout.Remeasure();
        _recent.Remeasure();
        LayoutChrome();
    }

    public void NotifyPaneNavigated(string path, bool thisPc)
    {
        NoteActivity();
        if (thisPc)
        {
            if (_session.ActiveKind != PinSessionActive.Place || !_session.ActiveThisPc)
                _session.OpenThisPc(_session.ActivePlaceIndex >= 0 ? _session.ActivePlaceIndex : 0);
        }
        else if (!string.IsNullOrEmpty(path))
        {
            // In-pane navigation: track path for pin toolbar; drop place chip highlight.
            _session.OpenPath(path);
            RecentSource.NotifyAccess(path);
            _rail.SetActivePlace(-1);
            if (_pins.Contains(path))
                _rail.SetPinsSelected(true);
            else
            {
                _rail.SetPinsSelected(false);
                _rail.SetRecentSelected(false);
            }
        }
        ApplySessionToChrome();
    }

    protected override void ExitThreadCore()
    {
        _tick.Stop();
        _tray.Visible = false;
        _tray.Dispose();
        _trayMenu.Dispose();
        IconCache.Clear();
        if (_recent != null && !_recent.IsDisposed)
            _recent.Close();
        if (_flyout != null && !_flyout.IsDisposed)
            _flyout.Close();
        if (_pane != null && !_pane.IsDisposed)
            _pane.Close();
        if (_rail != null && !_rail.IsDisposed)
            _rail.Close();
        base.ExitThreadCore();
    }

    private void SyncPathFromPane()
    {
        string path;
        bool thisPc;
        _pane.GetCurrentLocation(out path, out thisPc);
        if (thisPc)
            _session.OpenThisPc(_session.ActivePlaceIndex >= 0 ? _session.ActivePlaceIndex : 0);
        else if (!string.IsNullOrEmpty(path))
            _session.OpenPath(path);
    }

    private void ApplySessionToChrome()
    {
        if (_session.ActiveKind == PinSessionActive.Place)
        {
            _rail.SetActivePlace(_session.ActivePlaceIndex);
        }
        else if (_session.ActiveKind == PinSessionActive.Pin)
        {
            _rail.SetPinsSelected(true);
        }
        else
        {
            _rail.SetActivePlace(-1);
            _rail.SetPinsSelected(_session.PinsFlyoutOpen);
            _rail.SetRecentSelected(_session.RecentPanelOpen);
        }

        string activePath = _session.ActiveKind == PinSessionActive.Pin ? _session.ActivePath : null;
        if (_session.RecentPanelOpen)
            RefreshRecentPanel(false);
        else
        {
            _flyout.SetEntries(_pins.List(), activePath);
            _flyout.Remeasure();
        }
        _pane.SyncPinChrome();
    }

    private void RefreshRecentPanel(bool force)
    {
        int now = Environment.TickCount;
        if (!force && (now - _recentRefreshTick) < RecentRefreshMs && _recentRefreshTick != 0)
            return;
        _recentRefreshTick = now;
        IList<RecentEntry> list = RecentSource.Load(RecentSource.DefaultCap);
        string active = _session.ActiveKind == PinSessionActive.Pin ? _session.ActivePath : null;
        _recent.SetEntries(list, active);
        _recent.Remeasure();
    }

    private void ResetToDefault()
    {
        if (!_session.PaneOpen && !_session.FlyoutOpen && !_session.RecentPanelOpen
            && _session.ActiveKind == PinSessionActive.None)
            return;
        _session.IdleReset();
        ApplySessionToChrome();
        SetPaneVisible(false);
        LayoutChrome();
    }

    private void OnTick(object sender, EventArgs e)
    {
        if (_syncing)
            return;
        _syncing = true;
        try
        {
            if ((DateTime.UtcNow - _session.LastActivityUtc).TotalMilliseconds >= IdleResetMs)
                ResetToDefault();

            float dpi = _rail.Dpi > 0 ? _rail.Dpi : 96f;
            float fit = _rail.Fit > 0 ? _rail.Fit : 1f;
            ShellChrome.SetDipScale(dpi, fit);

            // QuickView PanelForm.MouseTick — bounds / edge bridge / popups only; pull while
            // parked is PointerInEdge inside Decide (no EdgeSensor window).
            Point pt = Control.MousePosition;
            Rectangle screen = Screen.PrimaryScreen.Bounds;
            bool inside = _shown && _rail.Bounds.Contains(pt);
            bool onPane = _shown && _session.PaneOpen && _pane.Opacity > 0.05
                && _pane.Left > -10000 && _pane.Bounds.Contains(pt);
            bool onFlyout = _shown && _session.FlyoutOpen && _flyout.Opacity > 0.05
                && _flyout.Left > -10000 && _flyout.Bounds.Contains(pt);
            bool onRecent = _shown && _session.RecentPanelOpen && _recent.Opacity > 0.05
                && _recent.Left > -10000 && _recent.Bounds.Contains(pt);
            bool edgeBridge = _shown
                && pt.X >= screen.Left && pt.X < _rail.Left
                && pt.Y >= _rail.Top && pt.Y <= _rail.Bottom;
            bool pointerOnUs = inside || onPane || onFlyout || onRecent || edgeBridge;
            bool pressing = (Control.MouseButtons & MouseButtons.Left) == MouseButtons.Left && pointerOnUs;

            ShellView view = ShellChrome.Decide(ShellEdge.Left, pointerOnUs, pressing, false);
            if (view == ShellView.Park)
            {
                if (_session.FlyoutOpen || _session.RecentPanelOpen)
                    _session.OnPark();
                Place(false, false);
            }
            else
                Place(true, view == ShellView.Reveal);

            if (_shown && _session.RecentPanelOpen)
                RefreshRecentPanel(false);

            if (_rail.IsHandleCreated)
                ShellChrome.PinSoon(_rail.Handle);
        }
        catch (Exception ex)
        {
            Program.LogError(ex, "shell-sync");
        }
        finally
        {
            _syncing = false;
        }
    }

    /// <summary>
    /// QuickView Place / JumpDock PlaceBar: move only when show/position changes;
    /// reassert ZOrder every shown tick for the whole chrome unit.
    /// </summary>
    private void Place(bool show, bool topmost)
    {
        float dpi = _rail.Dpi > 0 ? _rail.Dpi : 96f;
        float fit = _rail.Fit > 0 ? _rail.Fit : 1f;
        ShellChrome.SetDipScale(dpi, fit);

        int railW = _rail.Width > 0 ? _rail.Width : DisplayScale.Px(ChromeLayout.RailWidthDip, dpi, fit);
        RailCellMetrics cell = _rail.CellMetrics;
        if (cell.RowHeight <= 0)
            cell = ChromeLayout.MeasureRailCell(dpi, fit, null);
        int railH = ChromeLayout.RailHeightPx(cell, _rail.RowCount, dpi, fit);
        Rectangle railHome = ChromeLayout.RailHome(dpi, fit, railW, railH);
        Rectangle paneHome = ChromeLayout.PaneHome(dpi, fit, railHome);
        Rectangle screen = Screen.PrimaryScreen.Bounds;

        int railLeft = show ? railHome.Left : Offscreen;
        int railTop = railHome.Top;

        if (show && _rail.IsHandleCreated)
            ShellChrome.RestoreIfIconic(_rail.Handle);
        if (show && _session.PaneOpen && _pane.IsHandleCreated)
            ShellChrome.RestoreIfIconic(_pane.Handle);
        if (show && _session.FlyoutOpen && _flyout.IsHandleCreated)
            ShellChrome.RestoreIfIconic(_flyout.Handle);
        if (show && _session.RecentPanelOpen && _recent.IsHandleCreated)
            ShellChrome.RestoreIfIconic(_recent.Handle);

        bool railMoved = _shown != show
            || _rail.Left != railLeft
            || _rail.Top != railTop
            || _rail.Width != railHome.Width
            || _rail.Height != railHome.Height;
        if (railMoved)
        {
            _shown = show;
            _rail.Opacity = ChromeVisual.PanelOpacity;
            _rail.Bounds = new Rectangle(railLeft, railTop, railHome.Width, railHome.Height);
        }
        else
            _shown = show;

        bool paneOpen = _session.PaneOpen;
        if (paneOpen && show)
        {
            if (_pane.Bounds != paneHome)
                _pane.Bounds = paneHome;
            if (!_pane.Visible)
                _pane.Visible = true;
            if (_pane.Opacity < ChromeVisual.PanelOpacity - 0.01)
                _pane.Opacity = ChromeVisual.PanelOpacity;
        }
        else if (paneOpen && !show)
        {
            Rectangle parkedPane = new Rectangle(Offscreen, paneHome.Top, paneHome.Width, paneHome.Height);
            if (_pane.Bounds != parkedPane)
                _pane.Bounds = parkedPane;
        }
        else
        {
            if (_pane.Left > -10000)
                _pane.Bounds = new Rectangle(Offscreen, Offscreen, paneHome.Width, paneHome.Height);
            if (_pane.Opacity > 0.01)
                _pane.Opacity = 0.0;
        }

        bool recentOpen = _session.RecentPanelOpen;
        if (recentOpen && show)
        {
            if (_recent.Bounds != paneHome)
                _recent.Bounds = paneHome;
            if (!_recent.Visible)
                _recent.Visible = true;
            if (_recent.Opacity < ChromeVisual.PanelOpacity - 0.01)
                _recent.Opacity = ChromeVisual.PanelOpacity;
        }
        else if (recentOpen && !show)
        {
            Rectangle parkedRecent = new Rectangle(Offscreen, paneHome.Top, paneHome.Width, paneHome.Height);
            if (_recent.Bounds != parkedRecent)
                _recent.Bounds = parkedRecent;
        }
        else
        {
            if (_recent.Left > -10000)
                _recent.Bounds = new Rectangle(Offscreen, Offscreen, paneHome.Width, paneHome.Height);
            if (_recent.Opacity > 0.01)
                _recent.Opacity = 0.0;
        }

        bool fly = show && _session.FlyoutOpen;
        if (fly)
        {
            int gap = DisplayScale.Px(ChromeLayout.PinsFlyoutGapDip, dpi, fit);
            int edgeClear = DisplayScale.Px(ChromeLayout.EdgeClearDip, dpi, fit);
            int topClear = DisplayScale.Px(ChromeLayout.TopClearDip, dpi, fit);
            int fw = _flyout.Width > 0 ? _flyout.Width : DisplayScale.Px(ChromeLayout.PinsFlyoutWidthDip, dpi, fit);
            int fh = _flyout.DesiredHeight();
            int fx = railLeft + railHome.Width + gap;
            int fy = Math.Max(railTop, screen.Top + topClear);
            if (fy + fh > screen.Bottom - edgeClear)
                fy = Math.Max(screen.Top + topClear, screen.Bottom - fh - edgeClear);
            if (fx + fw > screen.Right - edgeClear)
                fx = Math.Max(railLeft + railHome.Width + gap, screen.Right - fw - edgeClear);
            Rectangle wantFly = new Rectangle(fx, fy, fw, fh);
            if (_flyout.Bounds != wantFly)
                _flyout.Bounds = wantFly;
            if (_flyout.Opacity < ChromeVisual.PanelOpacity - 0.01)
                _flyout.Opacity = ChromeVisual.PanelOpacity;
            if (!_flyout.Visible)
                _flyout.Visible = true;
        }
        else
        {
            if (_flyout.Left > -10000)
                _flyout.Bounds = new Rectangle(Offscreen, Offscreen, _flyout.Width, _flyout.Height);
            if (_flyout.Opacity > 0.01)
                _flyout.Opacity = 0.0;
        }

        // Rail = QuickView's single panel: reassert every shown tick.
        // Pane/flyout are large — every-tick NOTOPMOST fights JD/QV. Assert them every
        // tick only while Reveal (topmost); on Rest only when show/open/topmost changes.
        if (show && _rail.IsHandleCreated)
            ShellChrome.ZOrder(_rail.Handle, topmost);

        bool wantPaneZ = show && paneOpen;
        bool wantFlyZ = fly;
        bool wantRecentZ = show && recentOpen;
        bool auxChanged = show != _placedShow
            || topmost != _placedTopmost
            || wantPaneZ != _placedPaneZ
            || wantFlyZ != _placedFlyZ
            || wantRecentZ != _placedRecentZ;
        _placedShow = show;
        _placedTopmost = topmost;
        _placedPaneZ = wantPaneZ;
        _placedFlyZ = wantFlyZ;
        _placedRecentZ = wantRecentZ;

        if (wantPaneZ && _pane.IsHandleCreated && (topmost || auxChanged))
            ShellChrome.ZOrder(_pane.Handle, topmost);
        if (wantFlyZ && _flyout.IsHandleCreated && (topmost || auxChanged))
            ShellChrome.ZOrder(_flyout.Handle, topmost);
        if (wantRecentZ && _recent.IsHandleCreated && (topmost || auxChanged))
            ShellChrome.ZOrder(_recent.Handle, topmost);
    }

    private void LayoutChrome()
    {
        _rail.Remeasure();
        _pane.Remeasure();
        _flyout.Remeasure();
        _recent.Remeasure();
        Place(_shown, true);
    }

    private void SetPaneVisible(bool show)
    {
        if (show)
        {
            _pane.Opacity = ChromeVisual.PanelOpacity;
            if (!_pane.Visible)
                _pane.Visible = true;
        }
        else
        {
            _pane.Opacity = 0.0;
        }
        _rail.Opacity = ChromeVisual.PanelOpacity;
    }
}
