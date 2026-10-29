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
    private readonly ChromeToolButton _backBtn;
    private readonly ChromeToolButton _upBtn;
    private readonly TextBox _pathBox;
    private readonly ChromeToolButton _pathCopyBtn;
    private readonly ChromeToolButton _deleteBtn;
    private readonly ChromeToolButton _explorerBtn;
    private readonly ChromeToolButton _pinBtn;
    private readonly ChromeToolButton _iconsBtn;
    private readonly ChromeToolButton _tilesBtn;
    private readonly ChromeToolButton _contentBtn;
    private readonly ChromeToolButton _listBtn;
    private readonly ChromeToolButton _detailsBtn;
    private readonly ChromeToolButton _closeBtn;
    private readonly Label _status;
    private readonly ListView _list;
    private readonly ImageList _smallImages;
    private readonly ImageList _largeImages;
    private readonly ContextMenuStrip _menu;
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
    private BrowseViewMode _viewMode = BrowseViewMode.Icons;
    private bool _suppressPathEnter;
    private int _sortColumn;
    private bool _sortAsc = true;

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    public BrowseForm(ShellHost session)
    {
        _session = session;
        _viewMode = Prefs.LoadViewMode();
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

        _pathBox = new TextBox();
        _pathBox.BorderStyle = BorderStyle.None;
        _pathBox.BackColor = UiTheme.Raised;
        _pathBox.ForeColor = UiTheme.Text;
        _pathBox.TabStop = true;
        _pathBox.KeyDown += OnPathKeyDown;
        _pathBox.Enter += delegate { _session.NoteActivity(); _pathHost.Invalidate(); };
        _pathBox.Leave += delegate { _pathHost.Invalidate(); };
        _pathBox.MouseDown += delegate { _session.NoteActivity(); };
        _pathBox.GotFocus += delegate
        {
            BeginInvoke(new Action(delegate
            {
                if (!_pathBox.IsDisposed)
                    _pathBox.SelectAll();
            }));
        };

        _pathCopyBtn = MakeIconTool();
        _pathCopyBtn.Click += delegate { _session.NoteActivity(); CopyFolderPath(); };
        _tips.SetToolTip(_pathCopyBtn, "Copy path");

        _pathHost.Controls.Add(_pathCopyBtn);
        _pathHost.Controls.Add(_pathBox);

        _deleteBtn = MakeIconTool();
        _deleteBtn.Click += delegate { _session.NoteActivity(); DeleteSelected(); };
        _tips.SetToolTip(_deleteBtn, "Delete");

        _explorerBtn = MakeIconTool();
        _explorerBtn.Click += delegate { _session.NoteActivity(); OpenInExplorer(); };
        _tips.SetToolTip(_explorerBtn, "Open in Explorer");

        _pinBtn = MakeIconTool();
        _pinBtn.Click += delegate { TogglePinCurrent(); };
        _tips.SetToolTip(_pinBtn, "Pin folder");

        _iconsBtn = MakeIconTool();
        _iconsBtn.Click += delegate { SetViewMode(BrowseViewMode.Icons); };
        _tips.SetToolTip(_iconsBtn, "Icons");

        _tilesBtn = MakeIconTool();
        _tilesBtn.Click += delegate { SetViewMode(BrowseViewMode.Tiles); };
        _tips.SetToolTip(_tilesBtn, "Tiles");

        _contentBtn = MakeIconTool();
        _contentBtn.Click += delegate { SetViewMode(BrowseViewMode.Content); };
        _tips.SetToolTip(_contentBtn, "Content");

        _listBtn = MakeIconTool();
        _listBtn.Click += delegate { SetViewMode(BrowseViewMode.List); };
        _tips.SetToolTip(_listBtn, "List");

        _detailsBtn = MakeIconTool();
        _detailsBtn.Click += delegate { SetViewMode(BrowseViewMode.Details); };
        _tips.SetToolTip(_detailsBtn, "Details");

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
        _list.MouseMove += delegate { _session.NoteActivity(); };
        _list.KeyDown += OnListKeyDown;
        _list.MouseUp += OnListMouseUp;
        _list.SelectedIndexChanged += delegate { SyncActionButtons(); };
        EnsureColumns();
        ApplyViewMode();

        _menu = new ContextMenuStrip();
        _menu.Items.Add("Open", null, delegate { ActivateSelected(); });
        _menu.Items.Add("Pin folder", null, delegate { PinSelectedFolder(); });
        _menu.Items.Add("Copy path", null, delegate { CopySelectedPath(); });
        _menu.Items.Add("Delete", null, delegate { DeleteSelected(); });
        _menu.Items.Add("Show in Explorer", null, delegate { ShowSelectedInExplorer(); });

        _chrome.Controls.Add(_closeBtn);
        _chrome.Controls.Add(_detailsBtn);
        _chrome.Controls.Add(_listBtn);
        _chrome.Controls.Add(_contentBtn);
        _chrome.Controls.Add(_tilesBtn);
        _chrome.Controls.Add(_iconsBtn);
        _chrome.Controls.Add(_pinBtn);
        _chrome.Controls.Add(_explorerBtn);
        _chrome.Controls.Add(_deleteBtn);
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
        if (_uiFont != null) { _uiFont.Dispose(); _uiFont = null; }
        if (_tileFont != null) { _tileFont.Dispose(); _tileFont = null; }
        if (_metaFont != null) { _metaFont.Dispose(); _metaFont = null; }
        DisposeGlyphs();
        _smallImages.Dispose();
        _largeImages.Dispose();
        _menu.Dispose();
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
        _menu.Font = _uiFont;

        ApplyGlyphs();
        LayoutChromeButtons(chromeH);
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

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        _session.NoteActivity();
        if (keyData == Keys.Escape)
        {
            _session.ClosePane();
            return true;
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
        if (keyData == Keys.Enter && !_pathBox.Focused)
        {
            ActivateSelected();
            return true;
        }
        if (keyData == Keys.Delete)
        {
            DeleteSelected();
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

    private void PaintPathHost(object sender, PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        Rectangle r = new Rectangle(0, 0, _pathHost.Width - 1, _pathHost.Height - 1);
        int radius = Math.Max(4, _pathHost.Height / 2);
        using (var path = ChromeVisual.Round(r, radius))
        using (SolidBrush brush = new SolidBrush(UiTheme.Raised))
            g.FillPath(brush, path);
        using (var path = ChromeVisual.Round(r, radius))
        using (Pen pen = new Pen(_pathBox.Focused ? UiTheme.Accent : UiTheme.BorderSoft))
            g.DrawPath(pen, path);
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
        if (ShellContextMenu.TryHandleMenuMessage(ref m))
            return;
        base.WndProc(ref m);
    }

    private void LayoutChromeButtons(int chromeH)
    {
        // Row 1: Nav | Path | Close — Row 2: Organize | View (balanced sections).
        int edge = Px(12);
        int gap = Px(8);
        int group = Px(16);
        int btnH = Px(ChromeLayout.PaneChromeBtnDip);
        int btnW = Px(ChromeLayout.PaneChromeBtnWidthDip);
        int viewW = Px(ChromeLayout.PaneViewBtnWidthDip);
        int pathH = Px(ChromeLayout.PanePathHeightDip);
        int rowGap = Px(10);
        int topPad = Math.Max(Px(8), (chromeH - (btnH * 2 + rowGap)) / 2);
        int y1 = topPad;
        int y2 = y1 + btnH + rowGap;
        // Path sits on the nav row, half-height, vertically centered with nav chips.
        int pathY = y1 + (btnH - pathH) / 2;

        _backBtn.SetBounds(edge, y1, btnW, btnH);
        _upBtn.SetBounds(_backBtn.Right + gap, y1, btnW, btnH);
        _closeBtn.SetBounds(Width - edge - btnW, y1, btnW, btnH);

        int pathLeft = _upBtn.Right + group;
        int pathRight = _closeBtn.Left - group;
        int pathW = Math.Max(Px(200), pathRight - pathLeft);
        _pathHost.SetBounds(pathLeft, pathY, pathW, pathH);

        int inset = Px(6);
        int copySize = Math.Max(Px(18), pathH - Px(4));
        int copyY = Math.Max(1, (pathH - copySize) / 2);
        _pathCopyBtn.SetBounds(pathW - inset - copySize, copyY, copySize, copySize);

        int pathBoxLeft = inset;
        int pathBoxRight = _pathCopyBtn.Left - Px(6);
        int pathBoxH = Math.Min(_pathBox.PreferredHeight, Math.Max(Px(14), pathH - Px(4)));
        int pathBoxY = Math.Max(1, (pathH - pathBoxH) / 2);
        _pathBox.SetBounds(pathBoxLeft, pathBoxY, Math.Max(40, pathBoxRight - pathBoxLeft), pathBoxH);

        // Organize cluster (left) + view segment (right), same baseline.
        _deleteBtn.SetBounds(edge, y2, btnW, btnH);
        _explorerBtn.SetBounds(_deleteBtn.Right + gap, y2, btnW, btnH);
        _pinBtn.SetBounds(_explorerBtn.Right + gap, y2, btnW, btnH);

        int viewGap = Px(4);
        int viewTotal = viewW * 5 + viewGap * 4;
        int viewLeft = Width - edge - viewTotal;
        _iconsBtn.SetBounds(viewLeft, y2, viewW, btnH);
        _tilesBtn.SetBounds(_iconsBtn.Right + viewGap, y2, viewW, btnH);
        _contentBtn.SetBounds(_tilesBtn.Right + viewGap, y2, viewW, btnH);
        _listBtn.SetBounds(_contentBtn.Right + viewGap, y2, viewW, btnH);
        _detailsBtn.SetBounds(_listBtn.Right + viewGap, y2, viewW, btnH);

        _pathHost.Invalidate();
    }

    private static ChromeToolButton MakeIconTool()
    {
        return new ChromeToolButton();
    }

    private void ApplyGlyphs()
    {
        DisposeGlyphs();
        int s = Math.Max(20, Px(ChromeLayout.PaneChromeIconDip));
        int pathS = Math.Max(14, Px(14));
        AssignGlyph(_backBtn, ChromeIcons.Back(s));
        AssignGlyph(_upBtn, ChromeIcons.Up(s));
        AssignGlyph(_pathCopyBtn, ChromeIcons.Copy(pathS));
        AssignGlyph(_deleteBtn, ChromeIcons.Delete(s));
        AssignGlyph(_explorerBtn, ChromeIcons.Explorer(s));
        AssignGlyph(_pinBtn, ChromeIcons.Pin(s));
        AssignGlyph(_iconsBtn, ChromeIcons.IconsView(s));
        AssignGlyph(_tilesBtn, ChromeIcons.TilesView(s));
        AssignGlyph(_contentBtn, ChromeIcons.ContentView(s));
        AssignGlyph(_listBtn, ChromeIcons.ListView(s));
        AssignGlyph(_detailsBtn, ChromeIcons.DetailsView(s));
        AssignGlyph(_closeBtn, ChromeIcons.Close(s));
    }

    private void AssignGlyph(ChromeToolButton btn, Image glyph)
    {
        _glyphOwned.Add(glyph);
        btn.Glyph = glyph;
    }

    private void DisposeGlyphs()
    {
        for (int i = 0; i < _glyphOwned.Count; i++)
        {
            try { _glyphOwned[i].Dispose(); }
            catch { }
        }
        _glyphOwned.Clear();
        _backBtn.Glyph = null;
        _upBtn.Glyph = null;
        _pathCopyBtn.Glyph = null;
        _deleteBtn.Glyph = null;
        _explorerBtn.Glyph = null;
        _pinBtn.Glyph = null;
        _iconsBtn.Glyph = null;
        _tilesBtn.Glyph = null;
        _contentBtn.Glyph = null;
        _listBtn.Glyph = null;
        _detailsBtn.Glyph = null;
        _closeBtn.Glyph = null;
    }

    private bool UsesContentIcon()
    {
        return _viewMode == BrowseViewMode.Content;
    }

    private bool UsesLargeGrid()
    {
        return _viewMode == BrowseViewMode.Icons || _viewMode == BrowseViewMode.Tiles;
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
        SyncViewButtons();
        RebuildItemsFromCache();
    }

    private void ApplyViewMode()
    {
        EnsureColumns();
        switch (_viewMode)
        {
            case BrowseViewMode.Icons:
                _list.View = View.LargeIcon;
                _list.HeaderStyle = ColumnHeaderStyle.None;
                ApplyTileMetrics();
                break;
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
            case BrowseViewMode.List:
                _list.View = View.List;
                _list.HeaderStyle = ColumnHeaderStyle.None;
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
        _iconsBtn.Selected = _viewMode == BrowseViewMode.Icons;
        _tilesBtn.Selected = _viewMode == BrowseViewMode.Tiles;
        _contentBtn.Selected = _viewMode == BrowseViewMode.Content;
        _listBtn.Selected = _viewMode == BrowseViewMode.List;
        _detailsBtn.Selected = _viewMode == BrowseViewMode.Details;
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
        _deleteBtn.Enabled = canDelete;
        _pathCopyBtn.Enabled = true;
        SyncPinChrome();
        SyncPinMenuItem();
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
        _status.Text = text ?? "";
    }

    public void GetCurrentLocation(out string path, out bool thisPc)
    {
        path = _currentPath;
        thisPc = _thisPc;
    }

    private void SyncPinMenuItem()
    {
        DirEntry entry = SelectedEntry();
        bool folder = entry != null && entry.IsDirectory && !string.IsNullOrEmpty(entry.FullPath);
        if (_menu.Items.Count > 1)
            _menu.Items[1].Enabled = folder;
        if (folder && _menu.Items.Count > 1)
        {
            bool pinned = _session.IsPathPinned(entry.FullPath);
            _menu.Items[1].Text = pinned ? "Unpin folder" : "Pin folder";
        }
        else if (_menu.Items.Count > 1)
            _menu.Items[1].Text = "Pin folder";
    }

    private void TogglePinCurrent()
    {
        _session.NoteActivity();
        if (_thisPc || string.IsNullOrEmpty(_currentPath))
            return;
        if (_session.IsPathPinned(_currentPath))
        {
            if (_session.UnpinCurrentFolder())
                _status.Text = "Unpinned";
        }
        else
        {
            if (_session.PinCurrentFolder())
                _status.Text = "Pinned";
            else
                _status.Text = "Could not pin";
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
            _status.Text = "Unpinned";
        }
        else
        {
            if (_session.PinFolderPath(entry.FullPath))
                _status.Text = "Pinned " + entry.Name;
            else
                _status.Text = "Could not pin";
        }
        SyncPinChrome();
        SyncPinMenuItem();
    }

    private void GoToState(NavState next, bool pushCurrent, bool notifyHost)
    {
        if (next == null)
            return;
        if (pushCurrent)
            PushCurrent();
        _thisPc = next.ThisPc;
        _currentPath = next.Path;
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
        _pathHost.Invalidate();
    }

    private void OnPathKeyDown(object sender, KeyEventArgs e)
    {
        _session.NoteActivity();
        if (e.KeyCode != Keys.Enter || _suppressPathEnter)
            return;
        e.Handled = true;
        e.SuppressKeyPress = true;
        NavigateTypedPath();
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
        _status.Text = "Loading…";
        _list.BeginUpdate();
        _list.Items.Clear();
        _list.EndUpdate();
        _smallImages.Images.Clear();
        _largeImages.Images.Clear();
        _entries.Clear();
        SyncActionButtons();

        ThreadPool.QueueUserWorkItem(delegate
        {
            DirectoryListing listing = thisPc ? DirectoryModel.ListThisPc() : DirectoryModel.ListPath(path);
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
            _status.Text = "Nothing to show.";
            SyncNavButtons();
            SyncActionButtons();
            return;
        }
        if (!string.IsNullOrEmpty(listing.Error))
        {
            RebuildItemsFromCache();
            _status.Text = listing.Error;
            SyncNavButtons();
            SyncActionButtons();
            return;
        }
        IList<DirEntry> entries = listing.Entries ?? new List<DirEntry>();
        for (int i = 0; i < entries.Count; i++)
            _entries.Add(entries[i]);
        SortCachedEntries();
        RebuildItemsFromCache();
        _status.Text = _entries.Count == 0 ? "Empty folder" : (_entries.Count + " items");
        SyncNavButtons();
        SyncActionButtons();
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

    private void RebuildItemsFromCache()
    {
        _list.BeginUpdate();
        _list.Items.Clear();
        _smallImages.Images.Clear();
        _largeImages.Images.Clear();

        bool useLarge = UsesLargeGrid() || _viewMode == BrowseViewMode.Content || _viewMode == BrowseViewMode.Tiles;
        Size smallSz = _smallImages.ImageSize;
        Size largeSz = _largeImages.ImageSize;

        for (int i = 0; i < _entries.Count; i++)
        {
            DirEntry entry = _entries[i];
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
        if (_list.View == View.List)
        {
            DrawListRowItem(e);
            return;
        }
        if (_viewMode == BrowseViewMode.Content)
        {
            DrawContentItem(e);
            return;
        }
        if (_viewMode == BrowseViewMode.Tiles)
        {
            DrawTilesItem(e);
            return;
        }
        DrawIconsItem(e);
    }

    private void DrawListRowItem(DrawListViewItemEventArgs e)
    {
        Graphics g = e.Graphics;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        bool selected = e.Item.Selected;
        using (SolidBrush bg = new SolidBrush(selected ? UiTheme.Raised : UiTheme.Bg))
            g.FillRectangle(bg, e.Bounds);

        Image img = null;
        if (!string.IsNullOrEmpty(e.Item.ImageKey) && _smallImages.Images.ContainsKey(e.Item.ImageKey))
            img = _smallImages.Images[e.Item.ImageKey];
        else if (e.Item.ImageIndex >= 0 && e.Item.ImageIndex < _smallImages.Images.Count)
            img = _smallImages.Images[e.Item.ImageIndex];

        int x = e.Bounds.X + Px(4);
        if (img != null)
        {
            int iy = e.Bounds.Y + (e.Bounds.Height - img.Height) / 2;
            g.DrawImage(img, x, iy, img.Width, img.Height);
            x += img.Width + Px(6);
        }

        Font font = _uiFont ?? Font;
        Rectangle textRect = new Rectangle(x, e.Bounds.Y, Math.Max(8, e.Bounds.Right - x - Px(4)), e.Bounds.Height);
        Color ink = selected ? UiTheme.Accent : UiTheme.Text;
        TextRenderer.DrawText(g, e.Item.Text ?? "", font, textRect, ink,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        e.DrawDefault = false;
    }

    private void DrawIconsItem(DrawListViewItemEventArgs e)
    {
        Graphics g = e.Graphics;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        bool selected = e.Item.Selected;
        bool hot = (e.State & ListViewItemStates.Hot) != 0;
        if (selected || hot)
            ChromeVisual.FillRound(g, e.Bounds, Px(ChromeVisual.TileCornerDip), selected ? UiTheme.Raised : UiTheme.Hover);
        else
        {
            using (SolidBrush bg = new SolidBrush(UiTheme.Bg))
                g.FillRectangle(bg, e.Bounds);
        }

        Image img = ResolveLargeImage(e.Item);
        Font font = _tileFont ?? _uiFont ?? Font;
        int labelH = font != null ? (int)Math.Ceiling(font.GetHeight(g) * 2.15) + 4 : Px(32);
        int pad = Px(6);
        int iconAreaBottom = e.Bounds.Bottom - labelH - pad;
        if (iconAreaBottom < e.Bounds.Top + pad)
            iconAreaBottom = e.Bounds.Top + pad;

        if (img != null)
        {
            int iw = img.Width;
            int ih = img.Height;
            int maxW = Math.Max(8, e.Bounds.Width - pad * 2);
            int maxH = Math.Max(8, iconAreaBottom - e.Bounds.Top - pad);
            float scale = Math.Min(1f, Math.Min((float)maxW / iw, (float)maxH / ih));
            int dw = Math.Max(1, (int)Math.Round(iw * scale));
            int dh = Math.Max(1, (int)Math.Round(ih * scale));
            int ix = e.Bounds.X + (e.Bounds.Width - dw) / 2;
            int iy = e.Bounds.Y + pad + Math.Max(0, (maxH - dh) / 2);
            g.DrawImage(img, new Rectangle(ix, iy, dw, dh));
        }

        Rectangle labelRect = new Rectangle(
            e.Bounds.X + pad,
            e.Bounds.Bottom - labelH - pad / 2,
            Math.Max(8, e.Bounds.Width - pad * 2),
            labelH);
        Color ink = selected ? UiTheme.Accent : UiTheme.Text;
        using (SolidBrush brush = new SolidBrush(ink))
        using (StringFormat fmt = new StringFormat())
        {
            fmt.Alignment = StringAlignment.Center;
            fmt.LineAlignment = StringAlignment.Near;
            fmt.Trimming = StringTrimming.EllipsisCharacter;
            g.DrawString(e.Item.Text ?? "", font, brush, labelRect, fmt);
        }
        e.DrawDefault = false;
    }

    private void DrawTilesItem(DrawListViewItemEventArgs e)
    {
        Graphics g = e.Graphics;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        bool selected = e.Item.Selected;
        bool hot = (e.State & ListViewItemStates.Hot) != 0;
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
        bool hot = (e.State & ListViewItemStates.Hot) != 0;
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
        if (!string.IsNullOrEmpty(item.ImageKey) && _largeImages.Images.ContainsKey(item.ImageKey))
            return _largeImages.Images[item.ImageKey];
        if (item.ImageIndex >= 0 && item.ImageIndex < _largeImages.Images.Count)
            return _largeImages.Images[item.ImageIndex];
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
        using (SolidBrush bg = new SolidBrush(selected ? UiTheme.Raised : UiTheme.Bg))
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
        ListViewItem item = _list.GetItemAt(e.X, e.Y);
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

        Point screen = _list.PointToScreen(e.Location);
        bool extended = (Control.ModifierKeys & Keys.Shift) == Keys.Shift;
        ListViewItem item = _list.GetItemAt(e.X, e.Y);

        if (item != null)
        {
            item.Selected = true;
            SyncActionButtons();
            DirEntry entry = item.Tag as DirEntry;
            if (entry == null || string.IsNullOrEmpty(entry.FullPath))
                return;
            bool pinned = entry.IsDirectory && _session.IsPathPinned(entry.FullPath);
            ShellContextMenu.ShowResult shell = ShellContextMenu.ShowForItem(
                Handle,
                entry.FullPath,
                screen,
                entry.IsDirectory,
                pinned,
                extended);
            if (!shell.Shown)
            {
                SyncPinMenuItem();
                _menu.Show(_list, e.Location);
                return;
            }
            ApplyShellMenuResult(shell);
            return;
        }

        // Empty area — folder / This PC background menu.
        string folder = _thisPc ? null : _currentPath;
        ShellContextMenu.ShowResult bg = ShellContextMenu.ShowForBackground(Handle, folder, screen, extended);
        if (bg.Shown)
            ApplyShellMenuResult(bg);
    }

    private void ApplyShellMenuResult(ShellContextMenu.ShowResult result)
    {
        if (result == null)
            return;
        if (result.PinRequested)
        {
            DirEntry entry = SelectedEntry();
            if (entry != null && entry.IsDirectory && !string.IsNullOrEmpty(entry.FullPath))
            {
                if (_session.PinFolderPath(entry.FullPath))
                    _status.Text = "Pinned " + entry.Name;
                else
                    _status.Text = "Could not pin";
                SyncPinChrome();
            }
            return;
        }
        if (result.UnpinRequested)
        {
            DirEntry entry = SelectedEntry();
            if (entry != null && !string.IsNullOrEmpty(entry.FullPath))
            {
                _session.OnUnpinPath(entry.FullPath);
                _status.Text = "Unpinned";
                SyncPinChrome();
            }
            return;
        }
        if (result.NeedsRefresh)
            ReloadCurrentListing();
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
        if (entry.IsDirectory)
        {
            NavigateTo(entry.FullPath, entry.Name);
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo(entry.FullPath) { UseShellExecute = true });
            _session.NotifyRecentAccess(entry.FullPath);
        }
        catch (Exception ex)
        {
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

        if (ShellContextMenu.SendToRecycleBin(Handle, entry.FullPath))
        {
            _status.Text = "Moved to Recycle Bin: " + entry.Name;
            ReloadCurrentListing();
        }
        else
            _status.Text = "Could not delete";
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
}
