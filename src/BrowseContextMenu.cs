using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

/// <summary>
/// Managed Win11-like browse context menus — no shell IContextMenu.
/// Rounded dark chrome, horizontal icon action row, curated Send to / Open with / zip.
/// </summary>
internal sealed class BrowseContextMenu : IDisposable
{
    private const int GlyphSize = 16;
    private const int IconBtn = 28;

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

    private readonly ToolStripMenuItem _openItem;
    private readonly ToolStripMenuItem _openWithItem;
    private readonly ToolStripMenuItem _pinItem;
    private readonly ToolStripMenuItem _sendToItem;
    private readonly ToolStripMenuItem _compressItem;
    private readonly ToolStripMenuItem _extractItem;
    private readonly ToolStripMenuItem _copyPathItem;
    private readonly ToolStripMenuItem _explorerItem;
    private readonly ToolStripMenuItem _propsItem;
    private readonly ToolStripMenuItem _bgNewFolder;
    private readonly ToolStripMenuItem _bgPaste;
    private readonly ToolStripMenuItem _bgRefresh;
    private readonly ToolStripMenuItem _bgExplorer;
    private readonly ToolStripMenuItem _bgProps;

    public event Action OpenRequested;
    public event Action OpenWithRequested;
    public event Action PinToggleRequested;
    public event Action CutRequested;
    public event Action CopyRequested;
    public event Action PasteRequested;
    public event Action CopyPathRequested;
    public event Action RenameRequested;
    public event Action DeleteRequested;
    public event Action ExplorerRequested;
    public event Action PropertiesRequested;
    public event Action NewFolderRequested;
    public event Action RefreshRequested;
    public event Action BackgroundExplorerRequested;
    public event Action BackgroundPropertiesRequested;
    public event Action CompressRequested;
    public event Action ExtractRequested;
    public event Action<string> SendToRequested;

    public BrowseContextMenu()
    {
        ChromeMenuRenderer renderer = new ChromeMenuRenderer();
        _images = new ImageList();
        _images.ColorDepth = ColorDepth.Depth32Bit;
        _images.ImageSize = new Size(GlyphSize, GlyphSize);
        SeedImages();

        _itemMenu = MakeMenu(renderer);
        _itemMenu.ImageList = _images;
        _itemMenu.Opening += OnMenuOpening;
        _itemMenu.Opened += OnMenuOpened;

        _iconRow = new Panel();
        _iconRow.BackColor = UiTheme.Surface;
        _iconRow.Height = IconBtn + 6;
        _iconRow.Width = IconBtn * 5 + 12;
        _iconCut = MakeIconButton(ChromeIcons.Cut(GlyphSize), "Cut", delegate { CloseMenus(); Fire(CutRequested); });
        _iconCopy = MakeIconButton(ChromeIcons.Copy(GlyphSize), "Copy", delegate { CloseMenus(); Fire(CopyRequested); });
        _iconPaste = MakeIconButton(ChromeIcons.Paste(GlyphSize), "Paste", delegate { CloseMenus(); Fire(PasteRequested); });
        _iconRename = MakeIconButton(ChromeIcons.Rename(GlyphSize), "Rename", delegate { CloseMenus(); Fire(RenameRequested); });
        _iconDelete = MakeIconButton(ChromeIcons.Delete(GlyphSize), "Delete", delegate { CloseMenus(); Fire(DeleteRequested); });
        LayoutIconRow();
        _iconHost = new ToolStripControlHost(_iconRow);
        _iconHost.AutoSize = false;
        _iconHost.Size = new Size(_iconRow.Width, _iconRow.Height);
        _itemMenu.Items.Add(_iconHost);
        _itemMenu.Items.Add(new ToolStripSeparator());

        _openItem = Add(_itemMenu, "Open", "open", delegate { Fire(OpenRequested); });
        _openWithItem = Add(_itemMenu, "Open with…", "openwith", delegate { Fire(OpenWithRequested); });
        _pinItem = Add(_itemMenu, "Pin folder", "pin", delegate { Fire(PinToggleRequested); });
        _itemMenu.Items.Add(new ToolStripSeparator());

        _sendToItem = Add(_itemMenu, "Send to", "sendto", null);
        RebuildSendTo();
        _compressItem = Add(_itemMenu, "Compress to ZIP", "compress", delegate { Fire(CompressRequested); });
        _extractItem = Add(_itemMenu, "Extract all…", "extract", delegate { Fire(ExtractRequested); });
        _itemMenu.Items.Add(new ToolStripSeparator());

        _copyPathItem = Add(_itemMenu, "Copy path", "copy", delegate { Fire(CopyPathRequested); });
        _explorerItem = Add(_itemMenu, "Show in Explorer", "explorer", delegate { Fire(ExplorerRequested); });
        _propsItem = Add(_itemMenu, "Properties", null, delegate { Fire(PropertiesRequested); });

        _bgMenu = MakeMenu(renderer);
        _bgMenu.ImageList = _images;
        _bgMenu.Opening += OnMenuOpening;
        _bgMenu.Opened += OnMenuOpened;
        _bgNewFolder = Add(_bgMenu, "New folder", "newfolder", delegate { Fire(NewFolderRequested); });
        _bgPaste = Add(_bgMenu, "Paste", "paste", delegate { Fire(PasteRequested); });
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

    public void ShowForItem(Control host, Point clientPt, DirEntry entry, bool isPinned, bool canPaste)
    {
        if (entry == null || host == null)
            return;

        bool isDrive = entry.IsDrive;
        bool isDir = entry.IsDirectory;
        bool hasPath = !string.IsNullOrEmpty(entry.FullPath);
        bool isFile = hasPath && !isDir && !isDrive;
        bool isZip = isFile && ZipOps.IsZipPath(entry.FullPath);

        _iconCut.Enabled = hasPath && !isDrive;
        _iconCopy.Enabled = hasPath && !isDrive;
        _iconPaste.Enabled = canPaste;
        _iconRename.Enabled = hasPath && !isDrive;
        _iconDelete.Enabled = hasPath && !isDrive;

        _openItem.Enabled = hasPath;
        _openWithItem.Visible = isFile;
        _openWithItem.Enabled = isFile;
        _pinItem.Visible = isDir && !isDrive;
        _pinItem.Enabled = isDir && !isDrive && hasPath;
        _pinItem.Text = isPinned ? "Unpin folder" : "Pin folder";

        _sendToItem.Visible = hasPath && !isDrive;
        _sendToItem.Enabled = hasPath && !isDrive && _sendToItem.DropDownItems.Count > 0;
        _compressItem.Visible = hasPath && !isDrive && !isZip;
        _compressItem.Enabled = hasPath && !isDrive && !isZip;
        _extractItem.Visible = isZip;
        _extractItem.Enabled = isZip;

        _copyPathItem.Enabled = hasPath;
        _explorerItem.Enabled = hasPath;
        _propsItem.Enabled = hasPath;

        _itemMenu.Show(host, clientPt);
    }

    public void ShowForBackground(Control host, Point clientPt, bool thisPc, string folderPath, bool canPaste)
    {
        bool inFolder = !thisPc && !string.IsNullOrEmpty(folderPath);
        _bgNewFolder.Enabled = inFolder;
        _bgPaste.Enabled = inFolder && canPaste;
        _bgRefresh.Enabled = true;
        _bgExplorer.Enabled = true;
        _bgProps.Enabled = inFolder;
        _bgMenu.Show(host, clientPt);
    }

    private void RebuildSendTo()
    {
        _sendToItem.DropDownItems.Clear();
        IList<SendToDest> dests = SendToCatalog.Destinations();
        for (int i = 0; i < dests.Count; i++)
        {
            SendToDest d = dests[i];
            string id = d.Id;
            ToolStripMenuItem item = new ToolStripMenuItem(d.Title);
            item.BackColor = UiTheme.Surface;
            item.ForeColor = UiTheme.Text;
            item.Click += delegate { FireSend(id); };
            _sendToItem.DropDownItems.Add(item);
        }
        ToolStripDropDownMenu drop = _sendToItem.DropDown as ToolStripDropDownMenu;
        if (drop != null)
        {
            drop.Renderer = new ChromeMenuRenderer();
            drop.BackColor = UiTheme.Surface;
            drop.ForeColor = UiTheme.Text;
            drop.ShowImageMargin = false;
            drop.Opening += OnMenuOpening;
            drop.Opened += OnMenuOpened;
        }
    }

    private Button MakeIconButton(Image glyph, string tip, EventHandler onClick)
    {
        _owned.Add(glyph);
        Button btn = new Button();
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = UiTheme.Hover;
        btn.FlatAppearance.MouseDownBackColor = UiTheme.Press;
        btn.BackColor = UiTheme.Surface;
        btn.Size = new Size(IconBtn, IconBtn);
        btn.Image = glyph;
        btn.Cursor = Cursors.Hand;
        _iconTips.SetToolTip(btn, tip);
        btn.Click += onClick;
        _iconRow.Controls.Add(btn);
        return btn;
    }

    private void LayoutIconRow()
    {
        int x = 4;
        int y = 3;
        _iconCut.Location = new Point(x, y); x += IconBtn;
        _iconCopy.Location = new Point(x, y); x += IconBtn;
        _iconPaste.Location = new Point(x, y); x += IconBtn;
        _iconRename.Location = new Point(x, y); x += IconBtn;
        _iconDelete.Location = new Point(x, y);
    }

    private void CloseMenus()
    {
        try { _itemMenu.Close(); } catch { }
        try { _bgMenu.Close(); } catch { }
    }

    private void SeedImages()
    {
        AddImg("copy", ChromeIcons.Copy(GlyphSize));
        AddImg("paste", ChromeIcons.Paste(GlyphSize));
        AddImg("open", ChromeIcons.Explorer(GlyphSize));
        AddImg("openwith", ChromeIcons.OpenWith(GlyphSize));
        AddImg("pin", ChromeIcons.Pin(GlyphSize));
        AddImg("sendto", ChromeIcons.SendTo(GlyphSize));
        AddImg("compress", ChromeIcons.Compress(GlyphSize));
        AddImg("extract", ChromeIcons.Extract(GlyphSize));
        AddImg("explorer", ChromeIcons.Explorer(GlyphSize));
        AddImg("newfolder", ChromeIcons.NewFolder(GlyphSize));
        AddImg("refresh", ChromeIcons.Refresh(GlyphSize));
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
        menu.Padding = new Padding(4, 4, 4, 4);
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
        menu.Items.Add(item);
        return item;
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
}
