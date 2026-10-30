using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

internal sealed class BrowseForm : Form
{
    private sealed class NavState
    {
        public bool ThisPc;
        public string Path;
        public string Title;
    }

    private const int LvmFirst = 0x1000;
    private const int LvmSetIconSpacing = LvmFirst + 53;

    private readonly ShellHost _session;
    private readonly Panel _chrome;
    private readonly Panel _content;
    private readonly Panel _pathHost;
    private readonly Panel _filterHost;
    private readonly ChromeToolButton _backBtn;
    private readonly ChromeToolButton _upBtn;
    private readonly TextBox _pathBox;
    private readonly ChromeToolButton _pathCopyBtn;
    private readonly TextBox _filterBox;
    private readonly ChromeToolButton _filterClearBtn;
    private readonly Label _filterGlyph;
    private readonly ChromeToolButton _newFolderBtn;
    private readonly ChromeToolButton _refreshBtn;
    private readonly ChromeToolButton _clipboardBtn;
    private readonly ChromeToolButton _deleteBtn;
    private readonly ChromeToolButton _explorerBtn;
    private readonly ChromeToolButton _pinBtn;
    private readonly ChromeToolButton _viewBtn;
    private readonly ChromeToolButton _closeBtn;
    private readonly Label _status;
    private readonly ListView _list;
    private readonly ImageList _smallImages;
    private readonly ImageList _largeImages;
    private readonly BrowseContextMenu _ctxMenu;
    private readonly ToolTip _tips;
    private readonly List<NavState> _backStack = new List<NavState>();
    private readonly List<Image> _glyphOwned = new List<Image>();
    private readonly List<DirEntry> _entries = new List<DirEntry>();

    private float _dpi = 96f;
    private float _fit = 1f;
    private string _currentPath;
    private bool _thisPc;
    private int _loadGen;
    private Font _uiFont;
    private Font _tileFont;
    private Font _metaFont;
    private BrowseViewMode _viewMode = BrowseViewMode.Tiles;
    private bool _showHidden;
    private bool _suppressPathEnter;
    private bool _pathEditing;
    private bool _suppressFilter;
    private string _filterText = "";
    private Image _filterGlyphImage;
    private string _statusSticky;
    private int _statusStickyGen;
    private long _menuClosedTick;
    private int _sortColumn;
    private bool _sortAsc = true;
    private int _hotIndex = -1;
    private int _crumbHot = -1;
    private ViewMenuForm _viewMenu;
    private ClipboardMenuForm _clipboardMenu;
    private readonly List<PathCrumbSeg> _crumbSource = new List<PathCrumbSeg>();
    private readonly List<PathCrumbSeg> _crumbs = new List<PathCrumbSeg>();
    private readonly List<Rectangle> _crumbHits = new List<Rectangle>();

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    private const int EmSetCueBanner = 0x1501;

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

    public BrowseForm(ShellHost session)
    {
        _session = session;
        _viewMode = Prefs.LoadViewMode();
        _showHidden = Prefs.LoadShowHidden();
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = false;
        BackColor = UiTheme.Bg;
        ForeColor = UiTheme.Text;
        Opacity = ChromeVisual.PanelOpacity;
        DoubleBuffered = true;
        Text = "QuickBrowse Pane";
        AutoScaleMode = AutoScaleMode.None;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

        _tips = new ToolTip();
        _tips.AutoPopDelay = 12000;
        _tips.InitialDelay = 450;
        _tips.ReshowDelay = 200;
        _tips.ShowAlways = true;

        _chrome = new Panel();
        _chrome.Dock = DockStyle.Top;
        _chrome.BackColor = UiTheme.Surface;
        _chrome.Paint += PaintChrome;

        _backBtn = MakeIconTool();
        _backBtn.Click += delegate { _session.NoteActivity(); NavigateBack(); };
        _tips.SetToolTip(_backBtn, "Back");

        _upBtn = MakeIconTool();
        _upBtn.Click += delegate { _session.NoteActivity(); NavigateUp(); };
        _tips.SetToolTip(_upBtn, "Up");

        _pathHost = new Panel();
        _pathHost.BackColor = UiTheme.Raised;
        _pathHost.Paint += PaintPathHost;
        _pathHost.MouseMove += OnPathHostMouseMove;
        _pathHost.MouseLeave += OnPathHostMouseLeave;
        _pathHost.MouseClick += OnPathHostMouseClick;
        _pathHost.Cursor = Cursors.IBeam;

        _pathBox = new TextBox();
        _pathBox.BorderStyle = BorderStyle.None;
        _pathBox.BackColor = UiTheme.Raised;
        _pathBox.ForeColor = UiTheme.Text;
        _pathBox.TabStop = true;
        _pathBox.Visible = false;
        _pathBox.KeyDown += OnPathKeyDown;
        _pathBox.Enter += delegate { _session.NoteActivity(); _pathHost.Invalidate(); };
        _pathBox.Leave += OnPathBoxLeave;
        _pathBox.MouseDown += delegate { _session.NoteActivity(); };
        _pathBox.GotFocus += delegate
        {
            BeginInvoke(new Action(delegate
            {
                if (!_pathBox.IsDisposed && _pathEditing)
                    _pathBox.SelectAll();
            }));
        };

        _pathCopyBtn = MakeIconTool();
        _pathCopyBtn.Click += delegate { _session.NoteActivity(); CopyFolderPath(); };
        _tips.SetToolTip(_pathCopyBtn, "Copy path");

        _pathHost.Controls.Add(_pathCopyBtn);
        _pathHost.Controls.Add(_pathBox);

        _filterHost = new Panel();
        _filterHost.BackColor = UiTheme.Raised;
        _filterHost.Paint += PaintFilterHost;

        _filterGlyph = new Label();
        _filterGlyph.BackColor = Color.Transparent;
        _filterGlyph.AutoSize = false;
        _filterGlyph.Paint += PaintFilterGlyph;

        _filterBox = new TextBox();
        _filterBox.BorderStyle = BorderStyle.None;
        _filterBox.BackColor = UiTheme.Raised;
        _filterBox.ForeColor = UiTheme.Text;
        _filterBox.TabStop = true;
        _filterBox.TextChanged += OnFilterTextChanged;
        _filterBox.KeyDown += OnFilterKeyDown;
        _filterBox.Enter += delegate { _session.NoteActivity(); _filterHost.Invalidate(); };
        _filterBox.Leave += delegate { _filterHost.Invalidate(); };
        _filterBox.MouseDown += delegate { _session.NoteActivity(); };
        _filterBox.HandleCreated += delegate { ApplyFilterCue(); };

        _filterClearBtn = MakeIconTool();
        _filterClearBtn.Click += delegate { ClearFilter(); };
        _tips.SetToolTip(_filterClearBtn, "Clear filter");

        _filterHost.Controls.Add(_filterClearBtn);
        _filterHost.Controls.Add(_filterBox);
        _filterHost.Controls.Add(_filterGlyph);

        _newFolderBtn = MakeIconTool();
        _newFolderBtn.Click += delegate { _session.NoteActivity(); CreateNewFolder(); };
        _tips.SetToolTip(_newFolderBtn, "New folder");

        _refreshBtn = MakeIconTool();
        _refreshBtn.Click += delegate { _session.NoteActivity(); ReloadCurrentListing(); };
        _tips.SetToolTip(_refreshBtn, "Refresh");

        _clipboardBtn = MakeIconTool();
        _clipboardBtn.ShowChevron = true;
        _clipboardBtn.Click += delegate { ToggleClipboardMenu(); };
        _tips.SetToolTip(_clipboardBtn, "Clipboard");

        _deleteBtn = MakeIconTool();
        _deleteBtn.Click += delegate { _session.NoteActivity(); DeleteSelected(); };
        _tips.SetToolTip(_deleteBtn, "Delete");

        _explorerBtn = MakeIconTool();
        _explorerBtn.Click += delegate { _session.NoteActivity(); OpenInExplorer(); };
        _tips.SetToolTip(_explorerBtn, "Open in Explorer");

        _pinBtn = MakeIconTool();
        _pinBtn.Click += delegate { TogglePinCurrent(); };
        _tips.SetToolTip(_pinBtn, "Pin folder");

        _viewBtn = MakeIconTool();
        _viewBtn.ShowChevron = true;
        _viewBtn.Click += delegate { ToggleViewMenu(); };
        _tips.SetToolTip(_viewBtn, "View");

        _closeBtn = MakeIconTool();
        _closeBtn.Click += delegate { _session.ClosePane(); };
        _tips.SetToolTip(_closeBtn, "Close");

        _status = new Label();
        _status.Dock = DockStyle.Bottom;
        _status.ForeColor = UiTheme.TextSecondary;
        _status.BackColor = UiTheme.Surface;
        _status.Padding = new Padding(12, 5, 12, 5);
        _status.Text = "";
        _status.TextAlign = ContentAlignment.MiddleLeft;

        _smallImages = new ImageList();
        _smallImages.ColorDepth = ColorDepth.Depth32Bit;
        _largeImages = new ImageList();
        _largeImages.ColorDepth = ColorDepth.Depth32Bit;

        _content = new Panel();
        _content.Dock = DockStyle.Fill;
        _content.BackColor = UiTheme.Bg;
        _content.Padding = new Padding(10, 8, 10, 4);

        _list = new ListView();
        _list.Dock = DockStyle.Fill;
        _list.FullRowSelect = true;
        _list.MultiSelect = false;
        _list.HeaderStyle = ColumnHeaderStyle.None;
        _list.BorderStyle = BorderStyle.None;
        _list.BackColor = UiTheme.Bg;
        _list.ForeColor = UiTheme.Text;
        _list.SmallImageList = _smallImages;
        _list.LargeImageList = _largeImages;
        _list.HideSelection = false;
        _list.ShowItemToolTips = true;
        _list.Alignment = ListViewAlignment.SnapToGrid;
        _list.OwnerDraw = true;
        _list.DrawItem += OnListDrawItem;
        _list.DrawSubItem += OnListDrawSubItem;
        _list.DrawColumnHeader += OnListDrawColumnHeader;
        _list.ColumnClick += OnColumnClick;
        _list.MouseClick += OnListMouseClick;
        _list.MouseMove += OnListMouseMove;
        _list.MouseLeave += OnListMouseLeave;
        _list.KeyDown += OnListKeyDown;
        _list.MouseUp += OnListMouseUp;
        _list.SelectedIndexChanged += delegate { SyncActionButtons(); };
        EnsureColumns();
        ApplyViewMode();

        _ctxMenu = new BrowseContextMenu();
        _ctxMenu.OpenRequested += delegate { ActivateSelected(); };
        _ctxMenu.PinToggleRequested += delegate { PinSelectedFolder(); };
        _ctxMenu.CutRequested += delegate { ClipboardCopySelected(true); };
        _ctxMenu.CopyRequested += delegate { ClipboardCopySelected(false); };
        _ctxMenu.CopyPathRequested += delegate { CopySelectedPath(); };
        _ctxMenu.RenameRequested += delegate { RenameSelected(); };
        _ctxMenu.DeleteRequested += delegate { DeleteSelected(); };
        _ctxMenu.ExplorerRequested += delegate { ShowSelectedInExplorer(); };
        _ctxMenu.PropertiesRequested += delegate { ShowSelectedProperties(); };
        _ctxMenu.NewFolderRequested += delegate { CreateNewFolder(); };
        _ctxMenu.PasteRequested += delegate { ClipboardPaste(); };
        _ctxMenu.RefreshRequested += delegate { ReloadCurrentListing(); };
        _ctxMenu.BackgroundExplorerRequested += delegate { OpenInExplorer(); };
        _ctxMenu.BackgroundPropertiesRequested += delegate { ShowFolderProperties(); };

        _chrome.Controls.Add(_closeBtn);
        _chrome.Controls.Add(_viewBtn);
        _chrome.Controls.Add(_pinBtn);
        _chrome.Controls.Add(_explorerBtn);
        _chrome.Controls.Add(_deleteBtn);
        _chrome.Controls.Add(_clipboardBtn);
        _chrome.Controls.Add(_refreshBtn);
        _chrome.Controls.Add(_newFolderBtn);
        _chrome.Controls.Add(_filterHost);
        _chrome.Controls.Add(_pathHost);
        _chrome.Controls.Add(_upBtn);
        _chrome.Controls.Add(_backBtn);

        _content.Controls.Add(_list);

        Controls.Add(_content);
        Controls.Add(_status);
        Controls.Add(_chrome);

        _chrome.MouseMove += delegate { _session.NoteActivity(); };
        SyncNavButtons();
        SyncActionButtons();
        SyncViewButtons();
    }

    public float Dpi { get { return _dpi; } }
    public float Fit { get { return _fit; } }

    protected override bool ShowWithoutActivation
    {
        get { return true; }
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            ShellChrome.Prepare(cp);
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ChromeVisual.PreferDarkMode(Handle);
        Remeasure();
        ShellChrome.Pin(Handle);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        CloseViewMenu();
        CloseClipboardMenu();
        if (_uiFont != null) { _uiFont.Dispose(); _uiFont = null; }
        if (_tileFont != null) { _tileFont.Dispose(); _tileFont = null; }
        if (_metaFont != null) { _metaFont.Dispose(); _metaFont = null; }
        DisposeGlyphs();
        _smallImages.Dispose();
        _largeImages.Dispose();
        if (_ctxMenu != null) _ctxMenu.Dispose();
        _tips.Dispose();
        base.OnFormClosed(e);
    }

    public bool ContainsScreenPoint(Point pt)
    {
        return Visible && Opacity > 0.05 && Bounds.Contains(pt);
    }

    public void Remeasure()
    {
        if (IsHandleCreated)
        {
            try { _dpi = GetDpiForWindow(Handle); }
            catch { _dpi = 96f; }
        }
        if (_dpi < 96f)
            _dpi = 96f;
        DisplayProfile profile = DisplayScale.Measure(_dpi);
        _dpi = profile.Dpi;
        _fit = profile.Fit;
        float textFit = profile.TextFit < 0.8f ? 0.8f : profile.TextFit;

        int chromeH = Px(ChromeLayout.PaneChromeHeightDip);
        _chrome.Height = chromeH;
        _status.Height = Px(30);
        _content.Padding = new Padding(Px(12), Px(10), Px(12), Px(6));

        if (_uiFont != null) _uiFont.Dispose();
        if (_tileFont != null) _tileFont.Dispose();
        if (_metaFont != null) _metaFont.Dispose();
        float em = ChromeLayout.FontEm(_fit, textFit);
        _uiFont = ChromeFonts.CreateUi(ChromeLayout.UiPt, em);
        _tileFont = ChromeFonts.CreateLabel(ChromeLayout.TitlePt, em);
        _metaFont = ChromeFonts.CreateUi(ChromeLayout.MetricMinPt + 0.5f, em);
        Font = _uiFont;
        _status.Font = _uiFont;
        _list.Font = _tileFont;
        _pathBox.Font = _uiFont;
        _filterBox.Font = _uiFont;
        _ctxMenu.ApplyFont(_uiFont);

        ApplyGlyphs();
        RebuildCrumbs();
        LayoutChromeButtons(chromeH);
        ApplyFilterCue();
        _smallImages.ImageSize = new Size(Px(22), Px(22));
        int tileIcon = Px(UsesContentIcon() ? ChromeLayout.ContentIconDip : ChromeLayout.TileIconDip);
        _largeImages.ImageSize = new Size(tileIcon, tileIcon);
        ApplyTileMetrics();
        LayoutDetailColumns();
        SyncViewButtons();
        ChromeVisual.ApplyRoundedRegion(this, Px(ChromeVisual.CornerDip));
        Invalidate();
        _chrome.Invalidate();
        _pathHost.Invalidate();
    }

    public int Px(int dip)
    {
        return DisplayScale.Px(dip, _dpi, _fit);
    }

    public void ShowPlace(PlaceEntry place)
    {
        if (place == null)
            return;
        _backStack.Clear();
        if (place.Kind == PlaceKind.ThisPc)
        {
            GoToState(new NavState { ThisPc = true, Path = null, Title = "This PC" }, false, false);
            return;
        }
        string path = PlacesCatalog.ResolvePath(place);
        GoToState(new NavState { ThisPc = false, Path = path, Title = place.Title }, false, false);
    }

    public void NavigateTo(string path, string title)
    {
        GoToState(new NavState
        {
            ThisPc = false,
            Path = path,
            Title = string.IsNullOrEmpty(title) ? (path ?? "") : title
        }, true, true);
    }

    private bool TextFieldFocused
    {
        get { return _pathEditing || _pathBox.Focused || _filterBox.Focused; }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        _session.NoteActivity();
        if (keyData == Keys.Escape)
        {
            if (_pathEditing)
            {
                CancelPathEdit();
                return true;
            }
            if (!string.IsNullOrEmpty(_filterText))
            {
                ClearFilter();
                return true;
            }
            if (_filterBox.Focused)
            {
                _list.Focus();
                return true;
            }
            _session.ClosePane();
            return true;
        }
        if (keyData == (Keys.Control | Keys.L) || keyData == Keys.F4)
        {
            BeginPathEdit();
            return true;
        }
        if (keyData == (Keys.Control | Keys.F))
        {
            FocusFilter();
            return true;
        }
        if (!TextFieldFocused)
        {
            if (keyData == Keys.F5)
            {
                ReloadCurrentListing();
                return true;
            }
            if (keyData == (Keys.Control | Keys.Shift | Keys.N))
            {
                CreateNewFolder();
                return true;
            }
            if (keyData == (Keys.Control | Keys.C))
            {
                ClipboardCopySelected(false);
                return true;
            }
            if (keyData == (Keys.Control | Keys.X))
            {
                ClipboardCopySelected(true);
                return true;
            }
            if (keyData == (Keys.Control | Keys.V))
            {
                ClipboardPaste();
                return true;
            }
            if (keyData == Keys.Enter)
            {
                ActivateSelected();
                return true;
            }
            if (keyData == Keys.Delete)
            {
                DeleteSelected();
                return true;
            }
        }
        if (keyData == (Keys.Alt | Keys.Up))
        {
            NavigateUp();
            return true;
        }
        if (keyData == (Keys.Alt | Keys.Left) || keyData == Keys.BrowserBack)
        {
            NavigateBack();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (_closeBtn != null && Width > 0)
            LayoutChromeButtons(_chrome.Height);
        LayoutDetailColumns();
        ChromeVisual.ApplyRoundedRegion(this, Px(ChromeVisual.CornerDip));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        ChromeVisual.DrawRoundBorder(e.Graphics, new Rectangle(0, 0, Width, Height), Px(ChromeVisual.CornerDip), UiTheme.BorderSoft);
    }

    private void PaintChrome(object sender, PaintEventArgs e)
    {
        using (Pen pen = new Pen(UiTheme.Border))
            e.Graphics.DrawLine(pen, 0, _chrome.Height - 1, _chrome.Width, _chrome.Height - 1);
    }

    private void PaintPillHost(Graphics g, Control host, bool accent)
    {
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        Rectangle r = new Rectangle(0, 0, host.Width - 1, host.Height - 1);
        int radius = Math.Max(4, host.Height / 2);
        using (var path = ChromeVisual.Round(r, radius))
        using (SolidBrush brush = new SolidBrush(UiTheme.Raised))
            g.FillPath(brush, path);
        using (var path = ChromeVisual.Round(r, radius))
        using (Pen pen = new Pen(accent ? UiTheme.Accent : UiTheme.BorderSoft))
            g.DrawPath(pen, path);
    }

    private void PaintFilterHost(object sender, PaintEventArgs e)
    {
        PaintPillHost(e.Graphics, _filterHost, _filterBox.Focused);
    }

    private void PaintFilterGlyph(object sender, PaintEventArgs e)
    {
        e.Graphics.Clear(UiTheme.Raised);
        if (_filterGlyphImage == null)
            return;
        try
        {
            int gw = _filterGlyphImage.Width;
            int gh = _filterGlyphImage.Height;
            int x = (_filterGlyph.Width - gw) / 2;
            int y = (_filterGlyph.Height - gh) / 2;
            e.Graphics.DrawImage(_filterGlyphImage, x, y, gw, gh);
        }
        catch (ArgumentException) { }
        catch (ObjectDisposedException) { }
    }

    private void ApplyFilterCue()
    {
        if (_filterBox == null || !_filterBox.IsHandleCreated)
            return;
        try { SendMessage(_filterBox.Handle, EmSetCueBanner, (IntPtr)1, "Filter"); }
        catch { }
    }

    private void PaintPathHost(object sender, PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        PaintPillHost(g, _pathHost, _pathEditing && _pathBox.Focused);

        if (_pathEditing)
            return;

        Font font = _uiFont ?? Font;
        int sepW = PathCrumbs.TextWidth(font, " › ");
        for (int i = 0; i < _crumbs.Count && i < _crumbHits.Count; i++)
        {
            PathCrumbSeg seg = _crumbs[i];
            Rectangle hit = _crumbHits[i];
            bool hot = i == _crumbHot && !seg.IsEllipsis;
            if (hot)
                ChromeVisual.FillRound(g, hit, Math.Max(3, hit.Height / 3), UiTheme.Hover);
            Color ink = hot ? UiTheme.Accent : (i == _crumbs.Count - 1 ? UiTheme.Text : UiTheme.TextSecondary);
            TextRenderer.DrawText(
                g,
                seg.Label ?? "",
                font,
                hit,
                ink,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            if (i < _crumbs.Count - 1)
            {
                Rectangle sep = new Rectangle(hit.Right, 0, sepW, _pathHost.Height);
                TextRenderer.DrawText(g, " › ", font, sep, UiTheme.Muted,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        _session.NoteActivity();
        base.OnMouseMove(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (ShellChrome.SwallowMinimize(ref m))
            return;
        base.WndProc(ref m);
    }

    private void LayoutChromeButtons(int chromeH)
    {
        // Row 1: Nav | Path | Filter | Close — Row 2: Organize | View.
        int edge = Px(12);
        int gap = Px(8);
        int group = Px(16);
        int btnH = Px(ChromeLayout.PaneChromeBtnDip);
        int btnW = Px(ChromeLayout.PaneChromeBtnWidthDip);
        int viewW = Px(ChromeLayout.PaneViewDropWidthDip);
        int pathH = Px(ChromeLayout.PanePathHeightDip);
        int filterIdeal = Px(ChromeLayout.PaneFilterSlotDip);
        int rowGap = Px(10);
        int topPad = Math.Max(Px(8), (chromeH - (btnH * 2 + rowGap)) / 2);
        int y1 = topPad;
        int y2 = y1 + btnH + rowGap;
        int pathY = y1 + (btnH - pathH) / 2;

        _backBtn.SetBounds(edge, y1, btnW, btnH);
        _upBtn.SetBounds(_backBtn.Right + gap, y1, btnW, btnH);
        _closeBtn.SetBounds(Width - edge - btnW, y1, btnW, btnH);

        int pathLeft = _upBtn.Right + group;
        int row1Right = _closeBtn.Left - group;
        int avail = Math.Max(Px(200), row1Right - pathLeft);
        int filterW = Math.Min(filterIdeal, Math.Max(Px(110), avail / 3));
        int pathW = Math.Max(Px(120), avail - gap - filterW);
        if (pathW + gap + filterW > avail)
        {
            filterW = Math.Max(Px(96), avail / 4);
            pathW = Math.Max(Px(100), avail - gap - filterW);
        }
        _pathHost.SetBounds(pathLeft, pathY, pathW, pathH);
        _filterHost.SetBounds(_pathHost.Right + gap, pathY, filterW, pathH);

        int inset = Px(8);
        int copySize = Math.Max(Px(22), pathH - Px(8));
        int copyY = Math.Max(1, (pathH - copySize) / 2);
        _pathCopyBtn.SetBounds(pathW - inset - copySize, copyY, copySize, copySize);

        int pathBoxLeft = inset;
        int pathBoxRight = _pathCopyBtn.Left - Px(6);
        int pathBoxH = Math.Min(_pathBox.PreferredHeight + Px(2), Math.Max(Px(16), pathH - Px(8)));
        int pathBoxY = Math.Max(1, (pathH - pathBoxH) / 2);
        if (_pathEditing)
        {
            _pathBox.Visible = true;
            _pathBox.SetBounds(pathBoxLeft, pathBoxY, Math.Max(40, pathBoxRight - pathBoxLeft), pathBoxH);
        }
        else
        {
            _pathBox.Visible = false;
            _pathBox.SetBounds(pathBoxLeft, pathBoxY, Math.Max(40, pathBoxRight - pathBoxLeft), pathBoxH);
            LayoutCrumbHits();
        }

        LayoutFilterInner(pathBoxH, pathBoxY, inset, copySize);

        // Organize cluster (left) + view segment (right).
        int clipW = Px(ChromeLayout.PaneViewDropWidthDip);
        int orgGap = gap;
        _newFolderBtn.SetBounds(edge, y2, btnW, btnH);
        _refreshBtn.SetBounds(_newFolderBtn.Right + orgGap, y2, btnW, btnH);
        _clipboardBtn.SetBounds(_refreshBtn.Right + orgGap, y2, clipW, btnH);
        _deleteBtn.SetBounds(_clipboardBtn.Right + group, y2, btnW, btnH);
        _explorerBtn.SetBounds(_deleteBtn.Right + orgGap, y2, btnW, btnH);
        _pinBtn.SetBounds(_explorerBtn.Right + orgGap, y2, btnW, btnH);

        int viewLeft = Width - edge - viewW;
        if (_pinBtn.Right + gap > viewLeft)
            viewLeft = Math.Min(Width - edge - viewW, _pinBtn.Right + gap);
        _viewBtn.SetBounds(viewLeft, y2, viewW, btnH);

        _pathHost.Invalidate();
        _filterHost.Invalidate();
        _filterGlyph.Invalidate();
    }

    private void LayoutFilterInner(int pathBoxH, int pathBoxY, int inset, int clearSize)
    {
        int filterW = _filterHost.Width;
        int pathH = _filterHost.Height;
        int clearY = Math.Max(1, (pathH - clearSize) / 2);
        // Always reserve clear slot so typing does not shrink the text box.
        _filterClearBtn.SetBounds(filterW - inset - clearSize, clearY, clearSize, clearSize);
        _filterClearBtn.Visible = !string.IsNullOrEmpty(_filterText);

        int searchSize = Math.Max(Px(14), pathH - Px(14));
        int searchY = Math.Max(1, (pathH - searchSize) / 2);
        _filterGlyph.SetBounds(inset, searchY, searchSize, searchSize);

        int filterBoxLeft = _filterGlyph.Right + Px(6);
        int filterBoxRight = _filterClearBtn.Left - Px(6);
        _filterBox.SetBounds(filterBoxLeft, pathBoxY, Math.Max(40, filterBoxRight - filterBoxLeft), pathBoxH);
    }

    private static ChromeToolButton MakeIconTool()
    {
        return new ChromeToolButton();
    }

    private void ApplyGlyphs()
    {
        DisposeGlyphs();
        int s = Math.Max(20, Px(ChromeLayout.PaneChromeIconDip));
        int pathS = Math.Max(16, Px(16));
        AssignGlyph(_backBtn, ChromeIcons.Back(s));
        AssignGlyph(_upBtn, ChromeIcons.Up(s));
        AssignGlyph(_pathCopyBtn, ChromeIcons.Copy(pathS));
        if (_filterGlyphImage != null)
        {
            try { _filterGlyphImage.Dispose(); }
            catch { }
        }
        _filterGlyphImage = ChromeIcons.Search(Math.Max(14, Px(14)));
        AssignGlyph(_filterClearBtn, ChromeIcons.Close(pathS));
        AssignGlyph(_newFolderBtn, ChromeIcons.NewFolder(s));
        AssignGlyph(_refreshBtn, ChromeIcons.Refresh(s));
        AssignGlyph(_clipboardBtn, ChromeIcons.Clipboard(s));
        AssignGlyph(_deleteBtn, ChromeIcons.Delete(s));
        AssignGlyph(_explorerBtn, ChromeIcons.Explorer(s));
        AssignGlyph(_pinBtn, ChromeIcons.Pin(s));
        AssignGlyph(_viewBtn, ViewModeGlyph(s));
        AssignGlyph(_closeBtn, ChromeIcons.Close(s));
    }

    private Image ViewModeGlyph(int size)
    {
        switch (_viewMode)
        {
            case BrowseViewMode.Content: return ChromeIcons.ContentView(size);
            case BrowseViewMode.Details: return ChromeIcons.DetailsView(size);
            default: return ChromeIcons.TilesView(size);
        }
    }

    private void AssignGlyph(ChromeToolButton btn, Image glyph)
    {
        _glyphOwned.Add(glyph);
        btn.Glyph = glyph;
    }

    private void DisposeGlyphs()
    {
        // Detach from buttons first so a queued paint cannot touch disposed bitmaps.
        _backBtn.Glyph = null;
        _upBtn.Glyph = null;
        _pathCopyBtn.Glyph = null;
        _filterClearBtn.Glyph = null;
        if (_filterGlyphImage != null)
        {
            try { _filterGlyphImage.Dispose(); }
            catch { }
            _filterGlyphImage = null;
        }
        _newFolderBtn.Glyph = null;
        _refreshBtn.Glyph = null;
        _clipboardBtn.Glyph = null;
        _deleteBtn.Glyph = null;
        _explorerBtn.Glyph = null;
        _pinBtn.Glyph = null;
        _viewBtn.Glyph = null;
        _closeBtn.Glyph = null;
        for (int i = 0; i < _glyphOwned.Count; i++)
        {
            try { _glyphOwned[i].Dispose(); }
            catch { }
        }
        _glyphOwned.Clear();
    }

    private bool UsesContentIcon()
    {
        return _viewMode == BrowseViewMode.Content;
    }

    private bool UsesLargeGrid()
    {
        return _viewMode == BrowseViewMode.Tiles;
    }

    private void ApplyTileMetrics()
    {
        if (_viewMode == BrowseViewMode.Content)
        {
            int cw = Px(ChromeLayout.ContentRowWidthDip);
            int ch = Px(ChromeLayout.ContentRowHeightDip);
            _list.TileSize = new Size(cw, ch);
            ApplyIconSpacing(cw, ch);
        }
        else if (UsesLargeGrid())
        {
            int cellW = Px(ChromeLayout.TileWidthDip);
            int cellH = Px(ChromeLayout.TileHeightDip);
            if (_viewMode == BrowseViewMode.Tiles)
                cellH = Math.Max(cellH, Px(156));
            _list.TileSize = new Size(cellW, cellH);
            ApplyIconSpacing(cellW, cellH);
        }
    }

    private void ApplyIconSpacing(int cellW, int cellH)
    {
        if (!_list.IsHandleCreated)
            return;
        int cx = Math.Max(cellW, Px(120));
        int cy = Math.Max(cellH, Px(120));
        IntPtr lParam = (IntPtr)((cy << 16) | (cx & 0xFFFF));
        SendMessage(_list.Handle, LvmSetIconSpacing, IntPtr.Zero, lParam);
    }

    private void EnsureColumns()
    {
        if (_list.Columns.Count >= 4)
            return;
        _list.Columns.Clear();
        _list.Columns.Add("Name", 280);
        _list.Columns.Add("Date modified", 140);
        _list.Columns.Add("Type", 120);
        _list.Columns.Add("Size", 90);
    }

    private void LayoutDetailColumns()
    {
        EnsureColumns();
        if (_viewMode != BrowseViewMode.Details)
            return;
        int w = Math.Max(200, _list.ClientSize.Width - Px(16));
        int dateW = Px(140);
        int typeW = Px(120);
        int sizeW = Px(90);
        int nameW = Math.Max(Px(160), w - dateW - typeW - sizeW);
        _list.Columns[0].Width = nameW;
        _list.Columns[1].Width = dateW;
        _list.Columns[2].Width = typeW;
        _list.Columns[3].Width = sizeW;
    }

    private void SetViewMode(BrowseViewMode mode)
    {
        _session.NoteActivity();
        if (_viewMode == mode)
        {
            SyncViewButtons();
            return;
        }
        _viewMode = mode;
        Prefs.SaveViewMode(_viewMode);
        int tileIcon = Px(UsesContentIcon() ? ChromeLayout.ContentIconDip : ChromeLayout.TileIconDip);
        _largeImages.ImageSize = new Size(tileIcon, tileIcon);
        ApplyViewMode();
        ApplyViewGlyphOnly();
        SyncViewButtons();
        RebuildItemsFromCache();
    }

    private void ApplyViewMode()
    {
        EnsureColumns();
        switch (_viewMode)
        {
            case BrowseViewMode.Tiles:
                _list.View = View.Tile;
                _list.HeaderStyle = ColumnHeaderStyle.None;
                ApplyTileMetrics();
                break;
            case BrowseViewMode.Content:
                _list.View = View.Tile;
                _list.HeaderStyle = ColumnHeaderStyle.None;
                ApplyTileMetrics();
                break;
            default:
                _list.View = View.Details;
                _list.HeaderStyle = ColumnHeaderStyle.Clickable;
                LayoutDetailColumns();
                break;
        }
    }

    private void SyncViewButtons()
    {
        bool menuOpen = _viewMenu != null && !_viewMenu.IsDisposed && _viewMenu.Visible;
        _viewBtn.Selected = menuOpen;
    }

    private void ApplyViewGlyphOnly()
    {
        Image old = _viewBtn.Glyph;
        int s = Math.Max(20, Px(ChromeLayout.PaneChromeIconDip));
        Image glyph = ViewModeGlyph(s);
        _viewBtn.Glyph = glyph;
        if (old != null)
        {
            _glyphOwned.Remove(old);
            try { old.Dispose(); }
            catch { }
        }
        _glyphOwned.Add(glyph);
        string tip = "View";
        switch (_viewMode)
        {
            case BrowseViewMode.Content: tip = "View: Content"; break;
            case BrowseViewMode.Details: tip = "View: Details"; break;
            default: tip = "View: Tiles"; break;
        }
        _tips.SetToolTip(_viewBtn, tip);
    }

    private bool MenuJustClosed()
    {
        return Environment.TickCount - _menuClosedTick < 220;
    }

    private void NoteMenuClosed()
    {
        _menuClosedTick = Environment.TickCount;
    }

    private void ToggleViewMenu()
    {
        _session.NoteActivity();
        if (_viewMenu != null && !_viewMenu.IsDisposed && _viewMenu.Visible)
        {
            _viewMenu.Close();
            return;
        }
        if (MenuJustClosed())
            return;
        CloseViewMenu();
        CloseClipboardMenu();
        ViewMenuForm menu = new ViewMenuForm(_viewMode, _showHidden, _dpi, _fit);
        _viewMenu = menu;
        menu.ModeChosen += delegate(BrowseViewMode mode) { SetViewMode(mode); };
        menu.ShowHiddenChanged += delegate(bool show)
        {
            if (_showHidden == show)
                return;
            _showHidden = show;
            Prefs.SaveShowHidden(_showHidden);
            ReloadCurrentListing();
        };
        menu.FormClosed += delegate
        {
            NoteMenuClosed();
            if (ReferenceEquals(_viewMenu, menu))
                _viewMenu = null;
            if (!_viewBtn.IsDisposed)
                _viewBtn.Selected = false;
        };
        menu.PlaceBelow(_viewBtn);
        menu.Show(this);
        if (menu.IsHandleCreated)
            ShellChrome.ZOrder(menu.Handle, true);
        _viewBtn.Selected = true;
    }

    private void CloseViewMenu()
    {
        if (_viewMenu == null || _viewMenu.IsDisposed)
        {
            _viewMenu = null;
            return;
        }
        _viewMenu.Close();
        _viewMenu = null;
    }

    private void ToggleClipboardMenu()
    {
        _session.NoteActivity();
        if (_clipboardMenu != null && !_clipboardMenu.IsDisposed && _clipboardMenu.Visible)
        {
            _clipboardMenu.Close();
            return;
        }
        if (MenuJustClosed())
            return;
        CloseClipboardMenu();
        CloseViewMenu();
        DirEntry entry = SelectedEntry();
        bool canCopyCut = entry != null && !entry.IsDrive && !string.IsNullOrEmpty(entry.FullPath);
        bool canPaste = !_thisPc && !string.IsNullOrEmpty(_currentPath) && Directory.Exists(_currentPath)
            && ShellClipboard.CanPaste();
        ClipboardMenuForm menu = new ClipboardMenuForm(canCopyCut, canPaste, _dpi, _fit);
        _clipboardMenu = menu;
        menu.ActionChosen += OnClipboardAction;
        menu.FormClosed += delegate
        {
            NoteMenuClosed();
            if (ReferenceEquals(_clipboardMenu, menu))
                _clipboardMenu = null;
            if (!_clipboardBtn.IsDisposed)
                _clipboardBtn.Selected = false;
        };
        menu.PlaceBelow(_clipboardBtn);
        menu.Show(this);
        if (menu.IsHandleCreated)
            ShellChrome.ZOrder(menu.Handle, true);
        _clipboardBtn.Selected = true;
    }

    private void CloseClipboardMenu()
    {
        if (_clipboardMenu == null || _clipboardMenu.IsDisposed)
        {
            _clipboardMenu = null;
            return;
        }
        _clipboardMenu.Close();
        _clipboardMenu = null;
    }

    private void OnClipboardAction(ClipboardMenuAction action)
    {
        switch (action)
        {
            case ClipboardMenuAction.Copy:
                ClipboardCopySelected(false);
                break;
            case ClipboardMenuAction.Cut:
                ClipboardCopySelected(true);
                break;
            case ClipboardMenuAction.Paste:
                ClipboardPaste();
                break;
        }
    }

    private void ClipboardCopySelected(bool cut)
    {
        _session.NoteActivity();
        DirEntry entry = SelectedEntry();
        if (entry == null || entry.IsDrive || string.IsNullOrEmpty(entry.FullPath))
        {
            SetActionStatus(cut ? "Nothing to cut" : "Nothing to copy");
            return;
        }
        if (ShellClipboard.SetFiles(new string[] { entry.FullPath }, cut))
            SetActionStatus(cut ? "Cut to clipboard" : "Copied to clipboard");
        else
            SetActionStatus("Clipboard failed");
    }

    private void ClipboardPaste()
    {
        _session.NoteActivity();
        if (_thisPc || string.IsNullOrEmpty(_currentPath) || !Directory.Exists(_currentPath))
        {
            SetActionStatus("Cannot paste here");
            return;
        }
        string status;
        if (ShellClipboard.PasteInto(_currentPath, out status))
        {
            SetActionStatus(status ?? "Pasted");
            ReloadCurrentListing();
        }
        else
            SetActionStatus(status ?? "Paste failed");
    }

    private void CreateNewFolder()
    {
        _session.NoteActivity();
        if (_thisPc || string.IsNullOrEmpty(_currentPath) || !Directory.Exists(_currentPath))
        {
            SetActionStatus("Cannot create a folder here");
            return;
        }
        try
        {
            string dest = UniqueNewFolderPath(_currentPath);
            Directory.CreateDirectory(dest);
            SetActionStatus("Created " + Path.GetFileName(dest));
            ReloadCurrentListing();
        }
        catch (Exception ex)
        {
            SetActionStatus(ex.Message);
        }
    }

    private static string UniqueNewFolderPath(string parent)
    {
        string dest = Path.Combine(parent, "New folder");
        if (!Directory.Exists(dest) && !File.Exists(dest))
            return dest;
        for (int n = 2; n < 1000; n++)
        {
            dest = Path.Combine(parent, "New folder (" + n + ")");
            if (!Directory.Exists(dest) && !File.Exists(dest))
                return dest;
        }
        return Path.Combine(parent, "New folder (" + Guid.NewGuid().ToString("N").Substring(0, 6) + ")");
    }

    private void SyncNavButtons()
    {
        _backBtn.Enabled = _backStack.Count > 0;
        _upBtn.Enabled = !_thisPc;
    }

    private void SyncActionButtons()
    {
        DirEntry entry = SelectedEntry();
        bool canDelete = entry != null && !entry.IsDrive && !string.IsNullOrEmpty(entry.FullPath);
        bool canNewFolder = !_thisPc && !string.IsNullOrEmpty(_currentPath) && Directory.Exists(_currentPath);
        _newFolderBtn.Enabled = canNewFolder;
        _tips.SetToolTip(_newFolderBtn, canNewFolder ? "New folder" : "New folder (not available on This PC)");
        _refreshBtn.Enabled = true;
        _clipboardBtn.Enabled = true;
        _tips.SetToolTip(_clipboardBtn, "Clipboard (Copy / Cut / Paste)");
        _tips.SetToolTip(_filterBox, "Filter names in this folder (Ctrl+F)");
        _tips.SetToolTip(_pathHost, "Click a crumb to go up, or click empty space to edit (Ctrl+L)");
        _deleteBtn.Enabled = canDelete;
        _pathCopyBtn.Enabled = true;
        SyncPinChrome();
    }

    public void SyncPinChrome()
    {
        bool canPin = !_thisPc && !string.IsNullOrEmpty(_currentPath) && Directory.Exists(_currentPath);
        bool pinned = canPin && _session.IsPathPinned(_currentPath);
        _pinBtn.Enabled = canPin;
        _pinBtn.Selected = pinned;
        _tips.SetToolTip(_pinBtn, pinned ? "Unpin folder" : "Pin folder");
    }

    public void ShowStatus(string text)
    {
        SetActionStatus(text);
    }

    private void SetActionStatus(string text)
    {
        _statusSticky = text ?? "";
        // Match the next listing load gen (LoadListingAsync increments first).
        _statusStickyGen = _loadGen + 1;
        _status.Text = _statusSticky;
    }

    private void SetListingStatus(string text)
    {
        _statusSticky = null;
        _statusStickyGen = -1;
        _status.Text = text ?? "";
    }

    public void GetCurrentLocation(out string path, out bool thisPc)
    {
        path = _currentPath;
        thisPc = _thisPc;
    }

    private void TogglePinCurrent()
    {
        _session.NoteActivity();
        if (_thisPc || string.IsNullOrEmpty(_currentPath))
            return;
        if (_session.IsPathPinned(_currentPath))
        {
            if (_session.UnpinCurrentFolder())
                SetActionStatus("Unpinned");
        }
        else
        {
            if (_session.PinCurrentFolder())
                SetActionStatus("Pinned");
            else
                SetActionStatus("Could not pin");
        }
        SyncPinChrome();
    }

    private void PinSelectedFolder()
    {
        _session.NoteActivity();
        DirEntry entry = SelectedEntry();
        if (entry == null || !entry.IsDirectory || string.IsNullOrEmpty(entry.FullPath))
            return;
        if (_session.IsPathPinned(entry.FullPath))
        {
            _session.OnUnpinPath(entry.FullPath);
            SetActionStatus("Unpinned");
        }
        else
        {
            if (_session.PinFolderPath(entry.FullPath))
                SetActionStatus("Pinned " + entry.Name);
            else
                SetActionStatus("Could not pin");
        }
        SyncPinChrome();
    }

    private void GoToState(NavState next, bool pushCurrent, bool notifyHost)
    {
        if (next == null)
            return;
        if (pushCurrent)
            PushCurrent();
        _thisPc = next.ThisPc;
        _currentPath = next.Path;
        ClearFilter(false);
        SetPathText(next.ThisPc ? "This PC" : (next.Path ?? ""));
        SyncNavButtons();
        SyncActionButtons();
        if (notifyHost)
            _session.NotifyPaneNavigated(_currentPath, _thisPc);
        else
            SyncPinChrome();
        LoadListingAsync(next.ThisPc, next.Path);
    }

    private void PushCurrent()
    {
        _backStack.Add(new NavState
        {
            ThisPc = _thisPc,
            Path = _currentPath,
            Title = _thisPc ? "This PC" : (_currentPath ?? "")
        });
        if (_backStack.Count > 40)
            _backStack.RemoveAt(0);
    }

    private void SetPathText(string text)
    {
        _suppressPathEnter = true;
        try
        {
            _pathBox.Text = text ?? "";
        }
        finally
        {
            _suppressPathEnter = false;
        }
        RebuildCrumbs();
        if (_pathEditing)
            EndPathEdit(false);
        _pathHost.Invalidate();
    }

    private void RebuildCrumbs()
    {
        _crumbSource.Clear();
        _crumbSource.AddRange(PathCrumbs.Build(_thisPc, _currentPath));
        LayoutCrumbHits();
    }

    private void LayoutCrumbHits()
    {
        _crumbHits.Clear();
        _crumbs.Clear();
        if (_pathEditing || _uiFont == null || _pathHost.Width < 8)
            return;
        int inset = Px(8);
        int segPad = Px(6);
        int copyReserve = _pathCopyBtn.Width + Px(14);
        int maxRight = _pathCopyBtn.Left - Px(6);
        int maxW = Math.Max(40, maxRight - inset);
        int sepW = PathCrumbs.TextWidth(_uiFont, " › ");
        _crumbs.AddRange(PathCrumbs.Fit(_crumbSource, _uiFont, maxW, sepW, segPad));
        int x = inset;
        int h = Math.Max(Px(18), _pathHost.Height - Px(8));
        int y = Math.Max(1, (_pathHost.Height - h) / 2);
        for (int i = 0; i < _crumbs.Count; i++)
        {
            int tw = PathCrumbs.TextWidth(_uiFont, _crumbs[i].Label ?? "");
            int hitW = Math.Max(8, tw + segPad);
            if (x + hitW > maxRight)
                hitW = Math.Max(8, maxRight - x);
            if (hitW <= 0)
                break;
            _crumbHits.Add(new Rectangle(x, y, hitW, h));
            x += hitW + sepW;
            if (x >= maxRight)
                break;
        }
        while (_crumbHits.Count < _crumbs.Count)
            _crumbs.RemoveAt(_crumbs.Count - 1);
    }

    private void BeginPathEdit()
    {
        if (_pathEditing)
        {
            if (!_pathBox.Focused)
                _pathBox.Focus();
            return;
        }
        _pathEditing = true;
        _crumbHot = -1;
        string text = _thisPc ? "This PC" : (_currentPath ?? "");
        _suppressPathEnter = true;
        try { _pathBox.Text = text; }
        finally { _suppressPathEnter = false; }
        LayoutChromeButtons(_chrome.Height);
        _pathBox.Visible = true;
        _pathBox.BringToFront();
        _pathCopyBtn.BringToFront();
        _pathBox.Focus();
        _pathBox.SelectAll();
        _pathHost.Invalidate();
    }

    private void CancelPathEdit()
    {
        EndPathEdit(true);
    }

    private void EndPathEdit(bool restore)
    {
        if (!_pathEditing)
            return;
        _pathEditing = false;
        if (restore)
        {
            _suppressPathEnter = true;
            try { _pathBox.Text = _thisPc ? "This PC" : (_currentPath ?? ""); }
            finally { _suppressPathEnter = false; }
        }
        if (_pathBox.Focused)
            _list.Focus();
        _pathBox.Visible = false;
        RebuildCrumbs();
        LayoutChromeButtons(_chrome.Height);
        _pathHost.Invalidate();
    }

    private void OnPathBoxLeave(object sender, EventArgs e)
    {
        _pathHost.Invalidate();
        if (!_pathEditing)
            return;
        // Defer so a crumb click / copy click can run first.
        BeginInvoke(new Action(delegate
        {
            if (IsDisposed || !_pathEditing)
                return;
            if (_pathCopyBtn.Focused || _pathBox.Focused)
                return;
            CancelPathEdit();
        }));
    }

    private void OnPathHostMouseMove(object sender, MouseEventArgs e)
    {
        _session.NoteActivity();
        if (_pathEditing)
            return;
        int next = -1;
        for (int i = 0; i < _crumbHits.Count; i++)
        {
            if (_crumbHits[i].Contains(e.Location))
            {
                next = i;
                break;
            }
        }
        if (next == _crumbHot)
            return;
        _crumbHot = next;
        _pathHost.Cursor = (next >= 0 && next < _crumbs.Count && !_crumbs[next].IsEllipsis)
            ? Cursors.Hand
            : Cursors.IBeam;
        _pathHost.Invalidate();
    }

    private void OnPathHostMouseLeave(object sender, EventArgs e)
    {
        if (_crumbHot < 0)
            return;
        _crumbHot = -1;
        _pathHost.Cursor = Cursors.IBeam;
        _pathHost.Invalidate();
    }

    private void OnPathHostMouseClick(object sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || _pathEditing)
            return;
        _session.NoteActivity();
        for (int i = 0; i < _crumbHits.Count && i < _crumbs.Count; i++)
        {
            if (!_crumbHits[i].Contains(e.Location))
                continue;
            PathCrumbSeg seg = _crumbs[i];
            if (seg.IsEllipsis)
                return;
            NavigateCrumb(seg);
            return;
        }
        BeginPathEdit();
    }

    private void NavigateCrumb(PathCrumbSeg seg)
    {
        if (seg == null)
            return;
        if (seg.IsThisPc)
        {
            if (_thisPc)
                return;
            GoToState(new NavState { ThisPc = true, Path = null, Title = "This PC" }, true, true);
            return;
        }
        if (string.IsNullOrEmpty(seg.TargetPath))
            return;
        string full;
        try { full = Path.GetFullPath(seg.TargetPath); }
        catch { return; }
        if (!Directory.Exists(full))
            return;
        if (!_thisPc && string.Equals(PinStore.Normalize(_currentPath), PinStore.Normalize(full), StringComparison.OrdinalIgnoreCase))
            return;
        string name = Path.GetFileName(full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        GoToState(new NavState
        {
            ThisPc = false,
            Path = full,
            Title = string.IsNullOrEmpty(name) ? full : name
        }, true, true);
    }

    private void OnPathKeyDown(object sender, KeyEventArgs e)
    {
        _session.NoteActivity();
        if (e.KeyCode == Keys.Escape)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            CancelPathEdit();
            return;
        }
        if (e.KeyCode != Keys.Enter || _suppressPathEnter)
            return;
        e.Handled = true;
        e.SuppressKeyPress = true;
        NavigateTypedPath();
        EndPathEdit(false);
    }

    private void NavigateTypedPath()
    {
        string typed = (_pathBox.Text ?? "").Trim().Trim('"');
        if (string.IsNullOrEmpty(typed))
            return;
        if (string.Equals(typed, "This PC", StringComparison.OrdinalIgnoreCase))
        {
            GoToState(new NavState { ThisPc = true, Path = null, Title = "This PC" }, true, true);
            return;
        }
        try
        {
            string full = Path.GetFullPath(typed);
            if (Directory.Exists(full))
            {
                string name = Path.GetFileName(full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                GoToState(new NavState
                {
                    ThisPc = false,
                    Path = full,
                    Title = string.IsNullOrEmpty(name) ? full : name
                }, true, true);
                return;
            }
            if (File.Exists(full))
            {
                Process.Start(new ProcessStartInfo(full) { UseShellExecute = true });
                _status.Text = "Opened file";
                return;
            }
            _status.Text = "Path not found.";
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
        }
    }

    private void NavigateBack()
    {
        if (_backStack.Count == 0)
            return;
        NavState prev = _backStack[_backStack.Count - 1];
        _backStack.RemoveAt(_backStack.Count - 1);
        GoToState(prev, false, true);
    }

    private void LoadListingAsync(bool thisPc, string path)
    {
        int gen = Interlocked.Increment(ref _loadGen);
        if (string.IsNullOrEmpty(_statusSticky) || _statusStickyGen != gen)
            _status.Text = "Loading…";
        ClearListAndImages();
        _entries.Clear();
        SyncActionButtons();

        bool showHidden = _showHidden;
        ThreadPool.QueueUserWorkItem(delegate
        {
            DirectoryListing listing = thisPc
                ? DirectoryModel.ListThisPc()
                : DirectoryModel.ListPath(path, showHidden);
            BeginInvoke(new Action(delegate
            {
                if (gen != _loadGen)
                    return;
                ApplyListing(listing);
            }));
        });
    }

    private void ApplyListing(DirectoryListing listing)
    {
        _entries.Clear();
        if (listing == null)
        {
            RebuildItemsFromCache();
            FinishListingStatus("Nothing to show.");
            SyncNavButtons();
            SyncActionButtons();
            return;
        }
        if (!string.IsNullOrEmpty(listing.Error))
        {
            RebuildItemsFromCache();
            FinishListingStatus(listing.Error);
            SyncNavButtons();
            SyncActionButtons();
            return;
        }
        IList<DirEntry> entries = listing.Entries ?? new List<DirEntry>();
        for (int i = 0; i < entries.Count; i++)
            _entries.Add(entries[i]);
        SortCachedEntries();
        RebuildItemsFromCache();
        FinishListingStatus(null);
        SyncNavButtons();
        SyncActionButtons();
    }

    private void FinishListingStatus(string fallback)
    {
        if (!string.IsNullOrEmpty(_statusSticky) && _statusStickyGen == _loadGen)
        {
            _status.Text = _statusSticky;
            return;
        }
        if (!string.IsNullOrEmpty(fallback))
        {
            SetListingStatus(fallback);
            return;
        }
        UpdateFilterStatus();
    }

    private void SortCachedEntries()
    {
        if (_entries.Count == 0)
            return;
        _entries.Sort(delegate(DirEntry a, DirEntry b)
        {
            return DirectoryModel.CompareByColumn(a, b, _sortColumn, _sortAsc);
        });
    }

    private void ClearListAndImages()
    {
        _hotIndex = -1;
        _list.BeginUpdate();
        // Detach imagelists before Clear so a paint cannot touch disposed bitmaps.
        _list.SmallImageList = null;
        _list.LargeImageList = null;
        _list.Items.Clear();
        _smallImages.Images.Clear();
        _largeImages.Images.Clear();
        _list.SmallImageList = _smallImages;
        _list.LargeImageList = _largeImages;
        _list.EndUpdate();
    }

    private void RebuildItemsFromCache()
    {
        ClearListAndImages();
        _list.BeginUpdate();

        bool useLarge = UsesLargeGrid() || _viewMode == BrowseViewMode.Content || _viewMode == BrowseViewMode.Tiles;
        Size smallSz = _smallImages.ImageSize;
        Size largeSz = _largeImages.ImageSize;

        string filter = _filterText ?? "";
        for (int i = 0; i < _entries.Count; i++)
        {
            DirEntry entry = _entries[i];
            if (!PassesFilter(entry, filter))
                continue;
            string key = i.ToString();
            Bitmap small = IconCache.FitCentered(
                IconCache.GetOwned(entry.FullPath, entry.IsDirectory, false),
                smallSz.Width,
                smallSz.Height);
            Bitmap large = IconCache.FitCentered(
                IconCache.GetOwned(entry.FullPath, entry.IsDirectory, true),
                largeSz.Width,
                largeSz.Height);
            if (small != null)
                _smallImages.Images.Add(key, small);
            if (large != null)
                _largeImages.Images.Add(key, large);

            ListViewItem item = new ListViewItem(entry.Name ?? "");
            item.Tag = entry;
            item.ToolTipText = BuildItemTip(entry);
            item.SubItems.Add(DirectoryModel.FormatDate(entry));
            item.SubItems.Add(entry.TypeName ?? "");
            item.SubItems.Add(DirectoryModel.FormatSize(entry));
            if (useLarge)
            {
                if (large != null)
                    item.ImageKey = key;
            }
            else if (small != null)
            {
                item.ImageKey = key;
            }
            _list.Items.Add(item);
        }

        _list.EndUpdate();
        ApplyTileMetrics();
        LayoutDetailColumns();
    }

    private static bool PassesFilter(DirEntry entry, string filter)
    {
        if (string.IsNullOrEmpty(filter))
            return true;
        if (entry == null || entry.Name == null)
            return false;
        return entry.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void OnFilterTextChanged(object sender, EventArgs e)
    {
        if (_suppressFilter)
            return;
        _session.NoteActivity();
        _filterText = _filterBox.Text ?? "";
        RebuildItemsFromCache();
        UpdateFilterStatus();
        int inset = Px(8);
        int clearSize = Math.Max(Px(22), _filterHost.Height - Px(8));
        int pathBoxH = Math.Min(_filterBox.PreferredHeight + Px(2), Math.Max(Px(16), _filterHost.Height - Px(8)));
        int pathBoxY = Math.Max(1, (_filterHost.Height - pathBoxH) / 2);
        LayoutFilterInner(pathBoxH, pathBoxY, inset, clearSize);
        _filterHost.Invalidate();
    }

    private void OnFilterKeyDown(object sender, KeyEventArgs e)
    {
        _session.NoteActivity();
        if (e.KeyCode == Keys.Escape)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            if (!string.IsNullOrEmpty(_filterText))
                ClearFilter();
            else
                _list.Focus();
        }
    }

    private void FocusFilter()
    {
        _session.NoteActivity();
        if (_pathEditing)
            CancelPathEdit();
        _filterBox.Focus();
        _filterBox.SelectAll();
        _filterHost.Invalidate();
    }

    private void ClearFilter()
    {
        ClearFilter(true);
    }

    private void ClearFilter(bool rebuild)
    {
        bool had = !string.IsNullOrEmpty(_filterText) || !string.IsNullOrEmpty(_filterBox.Text);
        _suppressFilter = true;
        try
        {
            _filterText = "";
            _filterBox.Text = "";
        }
        finally
        {
            _suppressFilter = false;
        }
        int inset = Px(8);
        int clearSize = Math.Max(Px(22), _filterHost.Height - Px(8));
        int pathBoxH = Math.Min(_filterBox.PreferredHeight + Px(2), Math.Max(Px(16), _filterHost.Height - Px(8)));
        int pathBoxY = Math.Max(1, (_filterHost.Height - pathBoxH) / 2);
        LayoutFilterInner(pathBoxH, pathBoxY, inset, clearSize);
        _filterHost.Invalidate();
        if (rebuild && had)
        {
            RebuildItemsFromCache();
            UpdateFilterStatus();
        }
    }

    private void UpdateFilterStatus()
    {
        if (!string.IsNullOrEmpty(_statusSticky) && _statusStickyGen == _loadGen
            && string.IsNullOrEmpty(_filterText))
        {
            _status.Text = _statusSticky;
            return;
        }
        int total = _entries.Count;
        if (total == 0)
        {
            SetListingStatus("Empty folder");
            return;
        }
        if (string.IsNullOrEmpty(_filterText))
        {
            SetListingStatus(total + " items");
            return;
        }
        int shown = 0;
        for (int i = 0; i < _entries.Count; i++)
        {
            if (PassesFilter(_entries[i], _filterText))
                shown++;
        }
        SetListingStatus(shown + " of " + total + " items");
    }

    /// <summary>
    /// Full cell hit-test — WinForms GetItemAt often misses padding around icon/label.
    /// </summary>
    private ListViewItem FindItemAtPoint(Point clientPt)
    {
        for (int i = 0; i < _list.Items.Count; i++)
        {
            ListViewItem item = _list.Items[i];
            if (item.Bounds.Contains(clientPt))
                return item;
        }
        return _list.GetItemAt(clientPt.X, clientPt.Y);
    }

    private bool IsHotItem(ListViewItem item)
    {
        return item != null && item.Index == _hotIndex;
    }

    private void SetHotIndex(int index)
    {
        if (_hotIndex == index)
            return;
        int old = _hotIndex;
        _hotIndex = index;
        InvalidateItemIndex(old);
        InvalidateItemIndex(_hotIndex);
    }

    private void InvalidateItemIndex(int index)
    {
        if (index < 0 || index >= _list.Items.Count)
            return;
        try
        {
            _list.Invalidate(_list.Items[index].Bounds);
        }
        catch
        {
        }
    }

    private void OnListMouseMove(object sender, MouseEventArgs e)
    {
        _session.NoteActivity();
        ListViewItem item = FindItemAtPoint(e.Location);
        SetHotIndex(item != null ? item.Index : -1);
    }

    private void OnListMouseLeave(object sender, EventArgs e)
    {
        SetHotIndex(-1);
    }

    private static string BuildItemTip(DirEntry entry)
    {
        if (entry == null)
            return "";
        StringBuilder sb = new StringBuilder();
        sb.AppendLine(entry.Name ?? "");
        if (!string.IsNullOrEmpty(entry.TypeName))
            sb.AppendLine(entry.TypeName);
        if (!string.IsNullOrEmpty(entry.FullPath))
            sb.AppendLine(entry.FullPath);
        try
        {
            if (entry.IsDrive)
            {
                DriveInfo drive = new DriveInfo(entry.FullPath);
                if (drive.IsReady)
                {
                    sb.AppendLine("Free: " + DirectoryModel.FormatBytes(drive.AvailableFreeSpace)
                        + " / " + DirectoryModel.FormatBytes(drive.TotalSize));
                }
            }
            else
            {
                if (entry.HasSize)
                    sb.AppendLine("Size: " + DirectoryModel.FormatBytes(entry.Length));
                if (entry.HasDate)
                    sb.Append("Modified: ").Append(entry.LastWriteTime.ToString("g"));
            }
        }
        catch
        {
        }
        return sb.ToString().TrimEnd();
    }

    private void OnColumnClick(object sender, ColumnClickEventArgs e)
    {
        _session.NoteActivity();
        if (_viewMode != BrowseViewMode.Details)
            return;
        if (_sortColumn == e.Column)
            _sortAsc = !_sortAsc;
        else
        {
            _sortColumn = e.Column;
            _sortAsc = true;
        }
        SortCachedEntries();
        RebuildItemsFromCache();
    }

    private void OnListDrawColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
    {
        e.DrawDefault = false;
        Graphics g = e.Graphics;
        using (SolidBrush brush = new SolidBrush(UiTheme.Surface))
            g.FillRectangle(brush, e.Bounds);
        using (Pen pen = new Pen(UiTheme.Border))
            g.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);

        Font font = _uiFont ?? Font;
        string title = e.Header != null ? e.Header.Text : "";
        if (_sortColumn == e.ColumnIndex)
            title = title + (_sortAsc ? "  ^" : "  v");
        TextRenderer.DrawText(
            g,
            title,
            font,
            new Rectangle(e.Bounds.X + Px(8), e.Bounds.Y, e.Bounds.Width - Px(12), e.Bounds.Height),
            UiTheme.TextSecondary,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
    }

    private void OnListDrawItem(object sender, DrawListViewItemEventArgs e)
    {
        if (_list.View == View.Details)
        {
            // Details paints through DrawSubItem.
            e.DrawDefault = false;
            return;
        }
        if (_viewMode == BrowseViewMode.Content)
        {
            DrawContentItem(e);
            return;
        }
        DrawTilesItem(e);
    }

    private void DrawTilesItem(DrawListViewItemEventArgs e)
    {
        Graphics g = e.Graphics;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        bool selected = e.Item.Selected;
        bool hot = !selected && (IsHotItem(e.Item) || (e.State & ListViewItemStates.Hot) != 0);
        if (selected || hot)
            ChromeVisual.FillRound(g, e.Bounds, Px(ChromeVisual.TileCornerDip), selected ? UiTheme.Raised : UiTheme.Hover);
        else
        {
            using (SolidBrush bg = new SolidBrush(UiTheme.Bg))
                g.FillRectangle(bg, e.Bounds);
        }

        DirEntry entry = e.Item.Tag as DirEntry;
        Image img = ResolveLargeImage(e.Item);
        int pad = Px(8);
        int icon = Px(ChromeLayout.TileIconDip);
        if (img != null)
        {
            int dw = Math.Min(icon, img.Width);
            int dh = Math.Min(icon, img.Height);
            int ix = e.Bounds.X + (e.Bounds.Width - dw) / 2;
            int iy = e.Bounds.Y + pad;
            g.DrawImage(img, new Rectangle(ix, iy, dw, dh));
        }

        Font titleFont = _tileFont ?? Font;
        Font metaFont = _metaFont ?? _uiFont ?? Font;
        int textTop = e.Bounds.Y + pad + icon + Px(4);
        Rectangle nameRect = new Rectangle(e.Bounds.X + pad, textTop, Math.Max(8, e.Bounds.Width - pad * 2), (int)Math.Ceiling(titleFont.GetHeight(g)) + 2);
        Color ink = selected ? UiTheme.Accent : UiTheme.Text;
        TextRenderer.DrawText(g, e.Item.Text ?? "", titleFont, nameRect, ink,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

        string type = entry != null ? (entry.TypeName ?? "") : "";
        string size = entry != null ? DirectoryModel.FormatSize(entry) : "";
        string meta = type;
        if (!string.IsNullOrEmpty(size))
            meta = string.IsNullOrEmpty(meta) ? size : (type + "\n" + size);
        Rectangle metaRect = new Rectangle(e.Bounds.X + pad, nameRect.Bottom + 2, Math.Max(8, e.Bounds.Width - pad * 2), e.Bounds.Bottom - nameRect.Bottom - pad);
        TextRenderer.DrawText(g, meta, metaFont, metaRect, UiTheme.TextSecondary,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        e.DrawDefault = false;
    }

    private void DrawContentItem(DrawListViewItemEventArgs e)
    {
        Graphics g = e.Graphics;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        bool selected = e.Item.Selected;
        bool hot = !selected && (IsHotItem(e.Item) || (e.State & ListViewItemStates.Hot) != 0);
        if (selected || hot)
            ChromeVisual.FillRound(g, e.Bounds, Px(ChromeVisual.TileCornerDip), selected ? UiTheme.Raised : UiTheme.Hover);
        else
        {
            using (SolidBrush bg = new SolidBrush(UiTheme.Bg))
                g.FillRectangle(bg, e.Bounds);
        }

        DirEntry entry = e.Item.Tag as DirEntry;
        Image img = ResolveLargeImage(e.Item);
        int pad = Px(10);
        int icon = Px(ChromeLayout.ContentIconDip);
        int ix = e.Bounds.X + pad;
        int iy = e.Bounds.Y + (e.Bounds.Height - icon) / 2;
        if (img != null)
            g.DrawImage(img, new Rectangle(ix, iy, icon, icon));

        int textLeft = ix + icon + Px(12);
        Font titleFont = _tileFont ?? Font;
        Font metaFont = _metaFont ?? _uiFont ?? Font;
        int titleH = (int)Math.Ceiling(titleFont.GetHeight(g)) + 2;
        int metaH = (int)Math.Ceiling(metaFont.GetHeight(g)) + 2;
        int blockH = titleH + metaH + 2;
        int textTop = e.Bounds.Y + Math.Max(pad / 2, (e.Bounds.Height - blockH) / 2);
        Rectangle nameRect = new Rectangle(textLeft, textTop, Math.Max(8, e.Bounds.Right - textLeft - pad), titleH);
        Color ink = selected ? UiTheme.Accent : UiTheme.Text;
        TextRenderer.DrawText(g, e.Item.Text ?? "", titleFont, nameRect, ink,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

        string meta = "";
        if (entry != null)
        {
            string date = DirectoryModel.FormatDate(entry);
            string size = DirectoryModel.FormatSize(entry);
            string type = entry.TypeName ?? "";
            meta = type;
            if (!string.IsNullOrEmpty(date))
                meta = string.IsNullOrEmpty(meta) ? date : (meta + "  ·  " + date);
            if (!string.IsNullOrEmpty(size))
                meta = string.IsNullOrEmpty(meta) ? size : (meta + "  ·  " + size);
        }
        Rectangle metaRect = new Rectangle(textLeft, nameRect.Bottom + 2, Math.Max(8, e.Bounds.Right - textLeft - pad), metaH);
        TextRenderer.DrawText(g, meta, metaFont, metaRect, UiTheme.TextSecondary,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        e.DrawDefault = false;
    }

    private Image ResolveLargeImage(ListViewItem item)
    {
        if (item == null)
            return null;
        try
        {
            if (!string.IsNullOrEmpty(item.ImageKey) && _largeImages.Images.ContainsKey(item.ImageKey))
                return _largeImages.Images[item.ImageKey];
            if (item.ImageIndex >= 0 && item.ImageIndex < _largeImages.Images.Count)
                return _largeImages.Images[item.ImageIndex];
        }
        catch (ArgumentException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        return null;
    }

    private void OnListDrawSubItem(object sender, DrawListViewSubItemEventArgs e)
    {
        if (_list.View == View.LargeIcon || _list.View == View.Tile)
        {
            e.DrawDefault = false;
            return;
        }

        Graphics g = e.Graphics;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        bool selected = e.Item.Selected;
        bool hot = !selected && IsHotItem(e.Item);
        using (SolidBrush bg = new SolidBrush(selected ? UiTheme.Raised : (hot ? UiTheme.Hover : UiTheme.Bg)))
            g.FillRectangle(bg, e.Bounds);

        Image img = null;
        if (!string.IsNullOrEmpty(e.Item.ImageKey) && _smallImages.Images.ContainsKey(e.Item.ImageKey))
            img = _smallImages.Images[e.Item.ImageKey];
        else if (e.Item.ImageIndex >= 0 && e.Item.ImageIndex < _smallImages.Images.Count)
            img = _smallImages.Images[e.Item.ImageIndex];

        int x = e.Bounds.X + Px(6);
        if (img != null && e.ColumnIndex == 0)
        {
            int iy = e.Bounds.Y + (e.Bounds.Height - img.Height) / 2;
            g.DrawImage(img, x, iy, img.Width, img.Height);
            x += img.Width + Px(8);
        }

        Font font = _uiFont ?? Font;
        Rectangle textRect = new Rectangle(x, e.Bounds.Y, Math.Max(8, e.Bounds.Right - x - Px(6)), e.Bounds.Height);
        Color ink = selected ? UiTheme.Accent : (e.ColumnIndex == 0 ? UiTheme.Text : UiTheme.TextSecondary);
        TextFormatFlags align = e.ColumnIndex == 3
            ? TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding
            : TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding;
        TextRenderer.DrawText(g, e.SubItem.Text ?? "", font, textRect, ink, align);
        e.DrawDefault = false;
    }

    private void OnListMouseClick(object sender, MouseEventArgs e)
    {
        _session.NoteActivity();
        if (e.Button != MouseButtons.Left)
            return;
        ListViewItem item = FindItemAtPoint(e.Location);
        if (item == null)
            return;
        _list.SelectedItems.Clear();
        item.Selected = true;
        ActivateEntry(item.Tag as DirEntry);
    }

    private void OnListMouseUp(object sender, MouseEventArgs e)
    {
        _session.NoteActivity();
        if (e.Button != MouseButtons.Right)
            return;

        try
        {
            ListViewItem item = FindItemAtPoint(e.Location);
            if (item != null)
            {
                item.Selected = true;
                SyncActionButtons();
                DirEntry entry = item.Tag as DirEntry;
                if (entry == null || string.IsNullOrEmpty(entry.FullPath))
                    return;
                bool pinned = entry.IsDirectory && _session.IsPathPinned(entry.FullPath);
                _ctxMenu.ShowForItem(_list, e.Location, entry, pinned);
                return;
            }

            bool canPaste = !_thisPc && !string.IsNullOrEmpty(_currentPath) && Directory.Exists(_currentPath)
                && ShellClipboard.CanPaste();
            _ctxMenu.ShowForBackground(_list, e.Location, _thisPc, _currentPath, canPaste);
        }
        catch (Exception ex)
        {
            Program.LogError(ex, "context-menu");
            SetActionStatus("Menu failed: " + ex.Message);
        }
    }

    private void ReloadCurrentListing()
    {
        if (_thisPc)
            LoadListingAsync(true, null);
        else if (!string.IsNullOrEmpty(_currentPath))
            LoadListingAsync(false, _currentPath);
    }

    private void OnListKeyDown(object sender, KeyEventArgs e)
    {
        _session.NoteActivity();
        if (e.KeyCode == Keys.Enter)
        {
            ActivateSelected();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Back && !_pathBox.Focused)
        {
            NavigateUp();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Delete)
        {
            DeleteSelected();
            e.Handled = true;
        }
    }

    private DirEntry SelectedEntry()
    {
        if (_list.SelectedItems.Count == 0)
            return null;
        return _list.SelectedItems[0].Tag as DirEntry;
    }

    private void ActivateSelected()
    {
        ActivateEntry(SelectedEntry());
    }

    private void ActivateEntry(DirEntry entry)
    {
        if (entry == null)
            return;
        try
        {
            if (entry.IsDirectory)
            {
                NavigateTo(entry.FullPath, entry.Name);
                return;
            }
            Process.Start(new ProcessStartInfo(entry.FullPath) { UseShellExecute = true });
            _session.NotifyRecentAccess(entry.FullPath);
        }
        catch (Exception ex)
        {
            Program.LogError(ex, "activate");
            _status.Text = "Could not open: " + ex.Message;
        }
    }

    private void NavigateUp()
    {
        if (_thisPc)
            return;
        string parent = DirectoryModel.ParentPath(_currentPath);
        if (string.IsNullOrEmpty(parent))
        {
            GoToState(new NavState { ThisPc = true, Path = null, Title = "This PC" }, true, true);
            return;
        }
        string name = Path.GetFileName(parent.TrimEnd('\\'));
        GoToState(new NavState
        {
            ThisPc = false,
            Path = parent,
            Title = string.IsNullOrEmpty(name) ? parent : name
        }, true, true);
    }

    private void OpenInExplorer()
    {
        try
        {
            if (_thisPc || string.IsNullOrEmpty(_currentPath))
                Process.Start("explorer.exe");
            else
                Process.Start("explorer.exe", _currentPath);
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
        }
    }

    private void CopyFolderPath()
    {
        string path = _thisPc ? "This PC" : (_currentPath ?? _pathBox.Text);
        if (string.IsNullOrEmpty(path))
            return;
        try
        {
            Clipboard.SetText(path);
            _status.Text = "Path copied";
        }
        catch
        {
            _status.Text = "Could not copy path.";
        }
    }

    private void CopySelectedPath()
    {
        DirEntry entry = SelectedEntry();
        if (entry == null || string.IsNullOrEmpty(entry.FullPath))
        {
            CopyFolderPath();
            return;
        }
        try
        {
            Clipboard.SetText(entry.FullPath);
            _status.Text = "Path copied";
        }
        catch
        {
        }
    }

    private void DeleteSelected()
    {
        DirEntry entry = SelectedEntry();
        if (entry == null || entry.IsDrive || string.IsNullOrEmpty(entry.FullPath))
            return;

        string kind = entry.IsDirectory ? "folder" : "file";
        DialogResult answer = MessageBox.Show(
            this,
            "Send this " + kind + " to the Recycle Bin?\n\n" + entry.FullPath,
            "QuickBrowse",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes)
            return;

        if (ShellFileOps.SendToRecycleBin(Handle, entry.FullPath))
        {
            SetActionStatus("Moved to Recycle Bin: " + entry.Name);
            ReloadCurrentListing();
        }
        else
            SetActionStatus("Could not delete");
    }

    private void ShowSelectedInExplorer()
    {
        DirEntry entry = SelectedEntry();
        if (entry == null)
        {
            OpenInExplorer();
            return;
        }
        try
        {
            Process.Start("explorer.exe", "/select,\"" + entry.FullPath + "\"");
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
        }
    }

    private void RenameSelected()
    {
        DirEntry entry = SelectedEntry();
        if (entry == null || string.IsNullOrEmpty(entry.FullPath))
            return;
        if (_thisPc && entry.IsDirectory && entry.FullPath.Length <= 3)
        {
            SetActionStatus("Drive letters cannot be renamed here");
            return;
        }

        string newName = NamePromptForm.Ask(this, "Rename", "New name:", entry.Name);
        if (string.IsNullOrEmpty(newName) || string.Equals(newName, entry.Name, StringComparison.Ordinal))
            return;

        string error;
        string dest = ShellFileOps.Rename(entry.FullPath, newName, out error);
        if (dest == null)
        {
            SetActionStatus("Rename failed: " + (error ?? "unknown error"));
            return;
        }
        SetActionStatus("Renamed to " + Path.GetFileName(dest));
        ReloadCurrentListing();
        SelectPathInList(dest);
    }

    private void SelectPathInList(string path)
    {
        if (string.IsNullOrEmpty(path))
            return;
        foreach (ListViewItem item in _list.Items)
        {
            DirEntry e = item.Tag as DirEntry;
            if (e == null || string.IsNullOrEmpty(e.FullPath))
                continue;
            if (!string.Equals(e.FullPath, path, StringComparison.OrdinalIgnoreCase))
                continue;
            item.Selected = true;
            item.Focused = true;
            item.EnsureVisible();
            return;
        }
    }

    private void ShowSelectedProperties()
    {
        DirEntry entry = SelectedEntry();
        if (entry == null || string.IsNullOrEmpty(entry.FullPath))
            return;
        if (!ShellFileOps.ShowProperties(Handle, entry.FullPath))
            SetActionStatus("Could not open Properties");
    }

    private void ShowFolderProperties()
    {
        if (_thisPc || string.IsNullOrEmpty(_currentPath))
            return;
        if (!ShellFileOps.ShowProperties(Handle, _currentPath))
            SetActionStatus("Could not open Properties");
    }
}
