using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

/// <summary>
/// Managed Win11-like browse context menus — no shell IContextMenu.
/// Scaled icon action row, duplicate list verbs, Open in / archives / power extras.
/// </summary>
internal sealed class BrowseContextMenu : IDisposable
{
    private readonly ContextMenuStrip _itemMenu;
    private readonly ContextMenuStrip _bgMenu;
    private readonly ImageList _images;
    private readonly List<Image> _owned = new List<Image>();
    private readonly ToolTip _iconTips = new ToolTip();
    private readonly ToolStripControlHost _iconHost;
    private readonly Panel _iconRow;
    private readonly Button _iconCut;
    private readonly Button _iconCopy;
    private readonly Button _iconPaste;
    private readonly Button _iconRename;
    private readonly Button _iconDelete;

    private readonly ToolStripMenuItem _listCut;
    private readonly ToolStripMenuItem _listCopy;
    private readonly ToolStripMenuItem _listPaste;
    private readonly ToolStripMenuItem _listRename;
    private readonly ToolStripMenuItem _listDelete;

    private readonly ToolStripMenuItem _openItem;
    private readonly ToolStripMenuItem _openWithItem;
    private readonly ToolStripMenuItem _openInItem;
    private readonly ToolStripMenuItem _pinItem;
    private readonly ToolStripMenuItem _sendToItem;
    private readonly ToolStripMenuItem _compressItem;
    private readonly ToolStripMenuItem _extractItem;
    private readonly ToolStripMenuItem _copyPathItem;
    private readonly ToolStripMenuItem _copyNameItem;
    private readonly ToolStripMenuItem _copyQuotedItem;
    private readonly ToolStripMenuItem _copyHashItem;
    private readonly ToolStripMenuItem _duplicateItem;
    private readonly ToolStripMenuItem _shortcutItem;
    private readonly ToolStripMenuItem _runAsAdminItem;
    private readonly ToolStripMenuItem _terminalItem;
    private readonly ToolStripMenuItem _explorerItem;
    private readonly ToolStripMenuItem _propsItem;

    private readonly ToolStripMenuItem _bgNewFolder;
    private readonly ToolStripMenuItem _bgNewText;
    private readonly ToolStripMenuItem _bgPaste;
    private readonly ToolStripMenuItem _bgTerminal;
    private readonly ToolStripMenuItem _bgRefresh;
    private readonly ToolStripMenuItem _bgExplorer;
    private readonly ToolStripMenuItem _bgProps;

    private float _dpi = 96f;
    private float _fit = 1f;
    private int _glyphPx = 22;
    private int _iconBtnPx = 40;
    private Font _menuFont;

    public event Action OpenRequested;
    public event Action OpenWithRequested;
    public event Action<string> OpenInRequested;
    public event Action PinToggleRequested;
    public event Action CutRequested;
    public event Action CopyRequested;
    public event Action PasteRequested;
    public event Action CopyPathRequested;
    public event Action CopyNameRequested;
    public event Action CopyPathQuotedRequested;
    public event Action CopyHashRequested;
    public event Action RenameRequested;
    public event Action DeleteRequested;
    public event Action DuplicateRequested;
    public event Action CreateShortcutRequested;
    public event Action RunAsAdminRequested;
    public event Action ExplorerRequested;
    public event Action PropertiesRequested;
    public event Action NewFolderRequested;
    public event Action NewTextDocumentRequested;
    public event Action RefreshRequested;
    public event Action BackgroundExplorerRequested;
    public event Action BackgroundPropertiesRequested;
    public event Action<string> CompressWithRequested;
    public event Action<string, string> ExtractWithRequested;
    public event Action<string> TerminalRequested;
    public event Action<string> SendToRequested;

    public BrowseContextMenu()
    {
        ChromeMenuRenderer renderer = new ChromeMenuRenderer();
        _images = new ImageList();
        _images.ColorDepth = ColorDepth.Depth32Bit;
        _glyphPx = ChromeLayout.CtxGlyphDip;
        _iconBtnPx = ChromeLayout.CtxIconBtnDip;
        _images.ImageSize = new Size(_glyphPx, _glyphPx);
        SeedImages(_glyphPx);

        _itemMenu = MakeMenu(renderer);
        _itemMenu.ImageList = _images;
        _itemMenu.Opening += OnMenuOpening;
        _itemMenu.Opened += OnMenuOpened;

        _iconRow = new Panel();
        _iconRow.BackColor = UiTheme.Surface;
        _iconCut = MakeIconButton("Cut", delegate { CloseMenus(); Fire(CutRequested); });
        _iconCopy = MakeIconButton("Copy", delegate { CloseMenus(); Fire(CopyRequested); });
        _iconPaste = MakeIconButton("Paste", delegate { CloseMenus(); Fire(PasteRequested); });
        _iconRename = MakeIconButton("Rename", delegate { CloseMenus(); Fire(RenameRequested); });
        _iconDelete = MakeIconButton("Delete", delegate { CloseMenus(); Fire(DeleteRequested); });
        ApplyIconGlyphs();
        LayoutIconRow();
        _iconHost = new ToolStripControlHost(_iconRow);
        _iconHost.AutoSize = false;
        _iconHost.Size = new Size(_iconRow.Width, _iconRow.Height);
        _itemMenu.Items.Add(_iconHost);
        _itemMenu.Items.Add(new ToolStripSeparator());

        _listCut = Add(_itemMenu, "Cut", "cut", delegate { Fire(CutRequested); });
        _listCopy = Add(_itemMenu, "Copy", "copy", delegate { Fire(CopyRequested); });
        _listPaste = Add(_itemMenu, "Paste", "paste", delegate { Fire(PasteRequested); });
        _listRename = Add(_itemMenu, "Rename", "rename", delegate { Fire(RenameRequested); });
        _listDelete = Add(_itemMenu, "Delete", "delete", delegate { Fire(DeleteRequested); });
        _itemMenu.Items.Add(new ToolStripSeparator());

        _openItem = Add(_itemMenu, "Open", "open", delegate { Fire(OpenRequested); });
        _openWithItem = Add(_itemMenu, "Open with", "openwith", null);
        _openInItem = Add(_itemMenu, "Open in", "open", null);
        _pinItem = Add(_itemMenu, "Pin folder", "pin", delegate { Fire(PinToggleRequested); });
        _itemMenu.Items.Add(new ToolStripSeparator());

        _sendToItem = Add(_itemMenu, "Send to", "sendto", null);
        _compressItem = Add(_itemMenu, "Compress", "compress", null);
        _extractItem = Add(_itemMenu, "Extract", "extract", null);
        _itemMenu.Items.Add(new ToolStripSeparator());

        _copyPathItem = Add(_itemMenu, "Copy path", "copy", delegate { Fire(CopyPathRequested); });
        _copyNameItem = Add(_itemMenu, "Copy name", "copy", delegate { Fire(CopyNameRequested); });
        _copyQuotedItem = Add(_itemMenu, "Copy as path", "copy", delegate { Fire(CopyPathQuotedRequested); });
        _copyHashItem = Add(_itemMenu, "Copy SHA-256", "copy", delegate { Fire(CopyHashRequested); });
        _duplicateItem = Add(_itemMenu, "Duplicate", "copy", delegate { Fire(DuplicateRequested); });
        _shortcutItem = Add(_itemMenu, "Create shortcut", "sendto", delegate { Fire(CreateShortcutRequested); });
        _runAsAdminItem = Add(_itemMenu, "Run as administrator", "open", delegate { Fire(RunAsAdminRequested); });
        _terminalItem = Add(_itemMenu, "Open in Terminal", "explorer", null);
        _explorerItem = Add(_itemMenu, "Show in Explorer", "explorer", delegate { Fire(ExplorerRequested); });
        _propsItem = Add(_itemMenu, "Properties", null, delegate { Fire(PropertiesRequested); });

        StyleDrop(_openWithItem);
        StyleDrop(_openInItem);
        StyleDrop(_sendToItem);
        StyleDrop(_compressItem);
        StyleDrop(_extractItem);
        StyleDrop(_terminalItem);
        RebuildSendTo();
        RebuildCompress();
        RebuildTerminal(_terminalItem);

        _bgMenu = MakeMenu(renderer);
        _bgMenu.ImageList = _images;
        _bgMenu.Opening += OnMenuOpening;
        _bgMenu.Opened += OnMenuOpened;
        _bgNewFolder = Add(_bgMenu, "New folder", "newfolder", delegate { Fire(NewFolderRequested); });
        _bgNewText = Add(_bgMenu, "New Text Document", "newfolder", delegate { Fire(NewTextDocumentRequested); });
        _bgPaste = Add(_bgMenu, "Paste", "paste", delegate { Fire(PasteRequested); });
        _bgTerminal = Add(_bgMenu, "Open in Terminal", "explorer", null);
        StyleDrop(_bgTerminal);
        RebuildTerminal(_bgTerminal);
        _bgMenu.Items.Add(new ToolStripSeparator());
        _bgRefresh = Add(_bgMenu, "Refresh", "refresh", delegate { Fire(RefreshRequested); });
        _bgExplorer = Add(_bgMenu, "Open in Explorer", "explorer", delegate { Fire(BackgroundExplorerRequested); });
        _bgProps = Add(_bgMenu, "Properties", null, delegate { Fire(BackgroundPropertiesRequested); });
    }

    public void Dispose()
    {
        if (_itemMenu != null) _itemMenu.Dispose();
        if (_bgMenu != null) _bgMenu.Dispose();
        if (_images != null) _images.Dispose();
        if (_iconTips != null) _iconTips.Dispose();
        if (_menuFont != null) { try { _menuFont.Dispose(); } catch { } }
        for (int i = 0; i < _owned.Count; i++)
        {
            try { _owned[i].Dispose(); } catch { }
        }
        _owned.Clear();
    }

    public void ApplyFont(Font font)
    {
        if (font == null)
            return;
        _itemMenu.Font = font;
        _bgMenu.Font = font;
    }

    /// <summary>DPI/Fit-aware metrics: larger icon row, list glyphs, menu font.</summary>
    public void ApplyMetrics(float dpi, float fit, float textFit)
    {
        if (dpi < 96f) dpi = 96f;
        if (fit < 0.62f) fit = 0.62f;
        if (textFit < 0.8f) textFit = 0.8f;
        _dpi = dpi;
        _fit = fit;

        int glyph = ChromeLayout.CtxGlyphPx(dpi, fit);
        int btn = ChromeLayout.CtxIconBtnPx(dpi, fit);
        int pad = DisplayScale.Px(ChromeLayout.CtxMenuPadDip, dpi, fit);
        int rowMin = ChromeLayout.CtxRowMinPx(dpi, fit);

        bool rebuildGlyphs = glyph != _glyphPx;
        _glyphPx = glyph;
        _iconBtnPx = btn;

        if (rebuildGlyphs)
        {
            ClearOwnedImages();
            _images.Images.Clear();
            _images.ImageSize = new Size(glyph, glyph);
            SeedImages(glyph);
            ApplyIconGlyphs();
        }

        LayoutIconRow();
        _iconHost.Size = new Size(_iconRow.Width, _iconRow.Height);

        _itemMenu.ImageScalingSize = new Size(glyph, glyph);
        _bgMenu.ImageScalingSize = new Size(glyph, glyph);
        _itemMenu.Padding = new Padding(pad, pad, pad, pad);
        _bgMenu.Padding = new Padding(pad, pad, pad, pad);

        float em = ChromeLayout.FontEm(fit, textFit);
        Font menuFont = ChromeFonts.CreateUi(ChromeLayout.CtxMenuFontPt, em);
        if (_menuFont != null)
        {
            try { _menuFont.Dispose(); } catch { }
        }
        _menuFont = menuFont;
        ApplyFont(_menuFont);

        int vPad = DisplayScale.Px(ChromeLayout.CtxRowPadVDip, dpi, fit);
        if (vPad < 1) vPad = 1;
        // Keep rows compact: horizontal room is fine; vertical pad should not dominate the glyph.
        int maxV = Math.Max(1, (rowMin - glyph) / 4);
        if (vPad > maxV) vPad = maxV;
        ApplyItemPadding(_itemMenu.Items, pad, vPad);
        ApplyItemPadding(_bgMenu.Items, pad, vPad);
    }

    public void ShowForItem(Control host, Point clientPt, DirEntry entry, bool isPinned, bool canPaste)
    {
        if (entry == null || host == null)
            return;
        List<DirEntry> one = new List<DirEntry>(1);
        one.Add(entry);
        ShowForItems(host, clientPt, one, isPinned, canPaste);
    }

    public void ShowForItems(Control host, Point clientPt, IList<DirEntry> entries, bool isPinned, bool canPaste)
    {
        if (host == null || entries == null || entries.Count == 0)
            return;

        BrowseMenuCapabilities caps = BrowseSelectionRules.Compute(entries);
        bool single = caps.Count == 1;
        DirEntry first = entries[0];

        SetClipboardEnabled(caps.CanCutCopyDelete, canPaste);
        _listRename.Enabled = caps.CanRename;
        _listDelete.Enabled = caps.CanCutCopyDelete;
        _iconRename.Enabled = caps.CanRename;
        _iconDelete.Enabled = caps.CanCutCopyDelete;

        // Open is single-target only: opening many items at once is dangerous.
        _openItem.Enabled = single && caps.CanOpen;

        RebuildOpenWith(single ? first : null);
        RebuildOpenIn(single ? first : null);
        RebuildSendTo();
        RebuildCompress();
        RebuildExtract(single ? first : null);
        RebuildTerminal(_terminalItem);

        _openWithItem.Visible = caps.CanOpenWith;
        _openWithItem.Enabled = caps.CanOpenWith;
        if (!caps.CanOpenIn)
        {
            _openInItem.Visible = false;
            _openInItem.Enabled = false;
        }

        _pinItem.Visible = caps.CanPin;
        _pinItem.Enabled = caps.CanPin;
        _pinItem.Text = isPinned ? "Unpin folder" : "Pin folder";

        _sendToItem.Visible = caps.CanSendTo;
        _sendToItem.Enabled = caps.CanSendTo && _sendToItem.DropDownItems.Count > 0;
        _compressItem.Visible = caps.CanCompress;
        _compressItem.Enabled = caps.CanCompress && _compressItem.DropDownItems.Count > 0;
        _extractItem.Visible = caps.CanExtract;
        _extractItem.Enabled = caps.CanExtract && _extractItem.DropDownItems.Count > 0;

        _copyPathItem.Enabled = caps.CanCopyPath;
        _copyNameItem.Visible = single;
        _copyNameItem.Enabled = single && caps.CanCopyPath;
        _copyQuotedItem.Visible = single;
        _copyQuotedItem.Enabled = single && caps.CanCopyPath;
        _copyHashItem.Visible = caps.CanHash;
        _copyHashItem.Enabled = caps.CanHash;
        _duplicateItem.Enabled = caps.CanDuplicate;
        _shortcutItem.Enabled = caps.CanShortcut;
        _runAsAdminItem.Visible = caps.CanRunAsAdmin
            && single && BrowseSelectionRules.IsRunnablePath(first.FullPath);
        _runAsAdminItem.Enabled = _runAsAdminItem.Visible;
        _terminalItem.Visible = caps.CanTerminal;
        _terminalItem.Enabled = caps.CanTerminal;
        _explorerItem.Visible = single;
        _explorerItem.Enabled = single && caps.CanExplorer;
        _propsItem.Visible = single;
        _propsItem.Enabled = single && caps.CanProperties;

        _itemMenu.Show(host, clientPt);
    }

    public void ShowForBackground(Control host, Point clientPt, bool thisPc, string folderPath, bool canPaste)
    {
        bool inFolder = !thisPc && !string.IsNullOrEmpty(folderPath);
        _bgNewFolder.Enabled = inFolder;
        _bgNewText.Enabled = inFolder;
        _bgPaste.Enabled = inFolder && canPaste;
        _bgTerminal.Enabled = inFolder;
        RebuildTerminal(_bgTerminal);
        _bgRefresh.Enabled = true;
        _bgExplorer.Enabled = true;
        _bgProps.Enabled = inFolder;
        _bgMenu.Show(host, clientPt);
    }

    private void SetClipboardEnabled(bool canEdit, bool canPaste)
    {
        _iconCut.Enabled = canEdit;
        _iconCopy.Enabled = canEdit;
        _iconPaste.Enabled = canPaste;
        _listCut.Enabled = canEdit;
        _listCopy.Enabled = canEdit;
        _listPaste.Enabled = canPaste;
    }

    private void RebuildOpenWith(DirEntry entry)
    {
        _openWithItem.DropDownItems.Clear();
        bool isFile = entry != null && !entry.IsDirectory && !entry.IsDrive
            && !string.IsNullOrEmpty(entry.FullPath);
        _openWithItem.Visible = isFile;
        _openWithItem.Enabled = isFile;
        if (!isFile)
            return;

        IList<OpenInApp> handlers = OpenInCatalog.HandlersForExtension(entry.FullPath);
        for (int i = 0; i < handlers.Count; i++)
        {
            OpenInApp app = handlers[i];
            string id = app.Id;
            ToolStripMenuItem item = MakeDropItem(app.Title, delegate
            {
                FireOpenIn(id);
            });
            _openWithItem.DropDownItems.Add(item);
        }
        if (_openWithItem.DropDownItems.Count > 0)
            _openWithItem.DropDownItems.Add(new ToolStripSeparator());
        _openWithItem.DropDownItems.Add(MakeDropItem("Choose another app…", delegate
        {
            Fire(OpenWithRequested);
        }));
    }

    private void RebuildOpenIn(DirEntry entry)
    {
        _openInItem.DropDownItems.Clear();
        bool hasPath = entry != null && !string.IsNullOrEmpty(entry.FullPath) && !entry.IsDrive;
        _openInItem.Visible = hasPath;
        if (!hasPath)
        {
            _openInItem.Enabled = false;
            return;
        }

        IList<OpenInApp> apps = OpenInCatalog.DetectedApps();
        for (int i = 0; i < apps.Count; i++)
        {
            OpenInApp app = apps[i];
            string id = app.Id;
            _openInItem.DropDownItems.Add(MakeDropItem(app.Title, delegate { FireOpenIn(id); }));
        }
        _openInItem.Enabled = _openInItem.DropDownItems.Count > 0;
        _openInItem.Visible = _openInItem.DropDownItems.Count > 0;
    }

    private void RebuildSendTo()
    {
        _sendToItem.DropDownItems.Clear();
        IList<SendToDest> dests = SendToCatalog.Destinations();
        for (int i = 0; i < dests.Count; i++)
        {
            SendToDest d = dests[i];
            string id = d.Id;
            _sendToItem.DropDownItems.Add(MakeDropItem(d.Title, delegate { FireSend(id); }));
        }
    }

    private void RebuildCompress()
    {
        _compressItem.DropDownItems.Clear();
        IList<ArchiveProvider> providers = ArchiveCatalog.DetectedProviders();
        for (int i = 0; i < providers.Count; i++)
        {
            ArchiveProvider p = providers[i];
            string kind = p.Kind.ToString();
            string title = p.Kind == ArchiveProviderKind.ZipManaged
                ? "ZIP (QuickBrowse)"
                : ("Add to archive (" + p.Title + ")");
            _compressItem.DropDownItems.Add(MakeDropItem(title, delegate { FireCompress(kind); }));
        }
    }

    private void RebuildExtract(DirEntry entry)
    {
        _extractItem.DropDownItems.Clear();
        if (entry == null || string.IsNullOrEmpty(entry.FullPath) || !ArchiveCatalog.IsArchivePath(entry.FullPath))
            return;

        bool isZip = ZipOps.IsZipPath(entry.FullPath);
        IList<ArchiveProvider> providers = ArchiveCatalog.DetectedProviders();
        for (int i = 0; i < providers.Count; i++)
        {
            ArchiveProvider p = providers[i];
            if (p.Kind == ArchiveProviderKind.ZipManaged)
            {
                if (!isZip)
                    continue;
                _extractItem.DropDownItems.Add(MakeDropItem("Extract all (ZIP)", delegate
                {
                    FireExtract(ArchiveProviderKind.ZipManaged.ToString(), "folder");
                }));
                continue;
            }
            string kind = p.Kind.ToString();
            string title = p.Title;
            _extractItem.DropDownItems.Add(MakeDropItem("Extract here (" + title + ")", delegate
            {
                FireExtract(kind, "here");
            }));
            _extractItem.DropDownItems.Add(MakeDropItem("Extract to folder (" + title + ")", delegate
            {
                FireExtract(kind, "folder");
            }));
        }
    }

    private void RebuildTerminal(ToolStripMenuItem parent)
    {
        parent.DropDownItems.Clear();
        parent.DropDownItems.Add(MakeDropItem("Windows Terminal", delegate { FireTerminal("wt"); }));
        parent.DropDownItems.Add(MakeDropItem("PowerShell", delegate { FireTerminal("powershell"); }));
        parent.DropDownItems.Add(MakeDropItem("Command Prompt", delegate { FireTerminal("cmd"); }));
    }

    private Button MakeIconButton(string tip, EventHandler onClick)
    {
        Button btn = new Button();
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = UiTheme.Hover;
        btn.FlatAppearance.MouseDownBackColor = UiTheme.Press;
        btn.BackColor = UiTheme.Surface;
        btn.Size = new Size(_iconBtnPx, _iconBtnPx);
        btn.Cursor = Cursors.Hand;
        _iconTips.SetToolTip(btn, tip);
        btn.Click += onClick;
        _iconRow.Controls.Add(btn);
        return btn;
    }

    private void ApplyIconGlyphs()
    {
        SetBtnGlyph(_iconCut, ChromeIcons.Cut(_glyphPx));
        SetBtnGlyph(_iconCopy, ChromeIcons.Copy(_glyphPx));
        SetBtnGlyph(_iconPaste, ChromeIcons.Paste(_glyphPx));
        SetBtnGlyph(_iconRename, ChromeIcons.Rename(_glyphPx));
        SetBtnGlyph(_iconDelete, ChromeIcons.Delete(_glyphPx));
    }

    private void SetBtnGlyph(Button btn, Image glyph)
    {
        _owned.Add(glyph);
        btn.Image = glyph;
        btn.Size = new Size(_iconBtnPx, _iconBtnPx);
    }

    private void LayoutIconRow()
    {
        int pad = DisplayScale.Px(ChromeLayout.CtxIconRowPadDip, _dpi, _fit);
        int x = pad;
        int y = pad / 2;
        if (y < 3) y = 3;
        _iconCut.Location = new Point(x, y); x += _iconBtnPx;
        _iconCopy.Location = new Point(x, y); x += _iconBtnPx;
        _iconPaste.Location = new Point(x, y); x += _iconBtnPx;
        _iconRename.Location = new Point(x, y); x += _iconBtnPx;
        _iconDelete.Location = new Point(x, y);
        _iconRow.Height = _iconBtnPx + pad;
        _iconRow.Width = _iconBtnPx * 5 + pad * 2;
    }

    private void ClearOwnedImages()
    {
        for (int i = 0; i < _owned.Count; i++)
        {
            try { _owned[i].Dispose(); } catch { }
        }
        _owned.Clear();
        _iconCut.Image = null;
        _iconCopy.Image = null;
        _iconPaste.Image = null;
        _iconRename.Image = null;
        _iconDelete.Image = null;
    }

    private void CloseMenus()
    {
        try { _itemMenu.Close(); } catch { }
        try { _bgMenu.Close(); } catch { }
    }

    private void SeedImages(int size)
    {
        AddImg("cut", ChromeIcons.Cut(size));
        AddImg("copy", ChromeIcons.Copy(size));
        AddImg("paste", ChromeIcons.Paste(size));
        AddImg("rename", ChromeIcons.Rename(size));
        AddImg("delete", ChromeIcons.Delete(size));
        AddImg("open", ChromeIcons.Explorer(size));
        AddImg("openwith", ChromeIcons.OpenWith(size));
        AddImg("pin", ChromeIcons.Pin(size));
        AddImg("sendto", ChromeIcons.SendTo(size));
        AddImg("compress", ChromeIcons.Compress(size));
        AddImg("extract", ChromeIcons.Extract(size));
        AddImg("explorer", ChromeIcons.Explorer(size));
        AddImg("newfolder", ChromeIcons.NewFolder(size));
        AddImg("refresh", ChromeIcons.Refresh(size));
    }

    private void AddImg(string key, Image img)
    {
        _owned.Add(img);
        _images.Images.Add(key, img);
    }

    private static ContextMenuStrip MakeMenu(ToolStripRenderer renderer)
    {
        ContextMenuStrip menu = new ContextMenuStrip();
        menu.Renderer = renderer;
        menu.BackColor = UiTheme.Surface;
        menu.ForeColor = UiTheme.Text;
        menu.ShowImageMargin = true;
        menu.ShowCheckMargin = false;
        menu.Padding = new Padding(6, 6, 6, 6);
        return menu;
    }

    private ToolStripMenuItem Add(ContextMenuStrip menu, string text, string imageKey, EventHandler onClick)
    {
        ToolStripMenuItem item = new ToolStripMenuItem(text, null, onClick);
        if (!string.IsNullOrEmpty(imageKey))
            item.ImageKey = imageKey;
        item.BackColor = UiTheme.Surface;
        item.ForeColor = UiTheme.Text;
        item.ImageScaling = ToolStripItemImageScaling.None;
        item.ImageAlign = ContentAlignment.MiddleLeft;
        item.TextAlign = ContentAlignment.MiddleLeft;
        menu.Items.Add(item);
        return item;
    }

    private void StyleDrop(ToolStripMenuItem parent)
    {
        ToolStripDropDownMenu drop = parent.DropDown as ToolStripDropDownMenu;
        if (drop == null)
            return;
        drop.Renderer = new ChromeMenuRenderer();
        drop.BackColor = UiTheme.Surface;
        drop.ForeColor = UiTheme.Text;
        drop.ShowImageMargin = false;
        drop.Opening += OnMenuOpening;
        drop.Opened += OnMenuOpened;
    }

    private ToolStripMenuItem MakeDropItem(string text, EventHandler onClick)
    {
        ToolStripMenuItem item = new ToolStripMenuItem(text, null, onClick);
        item.BackColor = UiTheme.Surface;
        item.ForeColor = UiTheme.Text;
        return item;
    }

    private static void ApplyItemPadding(ToolStripItemCollection items, int hPad, int vPad)
    {
        Padding p = new Padding(hPad, vPad, hPad, vPad);
        for (int i = 0; i < items.Count; i++)
        {
            ToolStripMenuItem mi = items[i] as ToolStripMenuItem;
            if (mi == null)
                continue;
            mi.Padding = p;
            mi.ImageAlign = ContentAlignment.MiddleLeft;
            mi.TextAlign = ContentAlignment.MiddleLeft;
            if (mi.HasDropDownItems)
                ApplyItemPadding(mi.DropDownItems, hPad, vPad);
        }
    }

    private void OnMenuOpening(object sender, System.ComponentModel.CancelEventArgs e)
    {
        ToolStripDropDown drop = sender as ToolStripDropDown;
        if (drop == null)
            return;
        drop.Region = null;
    }

    private void OnMenuOpened(object sender, EventArgs e)
    {
        ToolStripDropDown drop = sender as ToolStripDropDown;
        if (drop == null || drop.Width < 8 || drop.Height < 8)
            return;
        int radius = Math.Max(8, Math.Min(14, drop.Height / 8));
        ChromeVisual.ApplyRoundedRegion(drop, radius);
    }

    private static void Fire(Action handler)
    {
        if (handler != null)
            handler();
    }

    private void FireSend(string id)
    {
        if (SendToRequested != null)
            SendToRequested(id);
    }

    private void FireOpenIn(string id)
    {
        if (OpenInRequested != null)
            OpenInRequested(id);
    }

    private void FireCompress(string kind)
    {
        if (CompressWithRequested != null)
            CompressWithRequested(kind);
    }

    private void FireExtract(string kind, string mode)
    {
        if (ExtractWithRequested != null)
            ExtractWithRequested(kind, mode);
    }

    private void FireTerminal(string kind)
    {
        if (TerminalRequested != null)
            TerminalRequested(kind);
    }
}
