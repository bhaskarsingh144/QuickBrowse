using System;
using System.Drawing;
using System.Windows.Forms;

/// <summary>
/// Managed browse-pane context menus (item + background) — no shell IContextMenu.
/// Dark chrome styling; verbs map to in-app actions.
/// </summary>
internal sealed class BrowseContextMenu : IDisposable
{
    private readonly ContextMenuStrip _itemMenu;
    private readonly ContextMenuStrip _bgMenu;
    private readonly ToolStripMenuItem _openItem;
    private readonly ToolStripMenuItem _pinItem;
    private readonly ToolStripMenuItem _cutItem;
    private readonly ToolStripMenuItem _copyItem;
    private readonly ToolStripMenuItem _copyPathItem;
    private readonly ToolStripMenuItem _renameItem;
    private readonly ToolStripMenuItem _deleteItem;
    private readonly ToolStripMenuItem _explorerItem;
    private readonly ToolStripMenuItem _propsItem;
    private readonly ToolStripMenuItem _bgNewFolder;
    private readonly ToolStripMenuItem _bgPaste;
    private readonly ToolStripMenuItem _bgRefresh;
    private readonly ToolStripMenuItem _bgExplorer;
    private readonly ToolStripMenuItem _bgProps;

    private DirEntry _entry;
    private bool _thisPc;
    private string _folderPath;

    public event Action OpenRequested;
    public event Action PinToggleRequested;
    public event Action CutRequested;
    public event Action CopyRequested;
    public event Action CopyPathRequested;
    public event Action RenameRequested;
    public event Action DeleteRequested;
    public event Action ExplorerRequested;
    public event Action PropertiesRequested;
    public event Action NewFolderRequested;
    public event Action PasteRequested;
    public event Action RefreshRequested;
    public event Action BackgroundExplorerRequested;
    public event Action BackgroundPropertiesRequested;

    public BrowseContextMenu()
    {
        ChromeMenuRenderer renderer = new ChromeMenuRenderer();

        _itemMenu = MakeMenu(renderer);
        _openItem = Add(_itemMenu, "Open", delegate { Fire(OpenRequested); });
        _pinItem = Add(_itemMenu, "Pin folder", delegate { Fire(PinToggleRequested); });
        _itemMenu.Items.Add(new ToolStripSeparator());
        _cutItem = Add(_itemMenu, "Cut", delegate { Fire(CutRequested); });
        _copyItem = Add(_itemMenu, "Copy", delegate { Fire(CopyRequested); });
        _copyPathItem = Add(_itemMenu, "Copy path", delegate { Fire(CopyPathRequested); });
        _itemMenu.Items.Add(new ToolStripSeparator());
        _renameItem = Add(_itemMenu, "Rename", delegate { Fire(RenameRequested); });
        _deleteItem = Add(_itemMenu, "Delete", delegate { Fire(DeleteRequested); });
        _itemMenu.Items.Add(new ToolStripSeparator());
        _explorerItem = Add(_itemMenu, "Show in Explorer", delegate { Fire(ExplorerRequested); });
        _propsItem = Add(_itemMenu, "Properties", delegate { Fire(PropertiesRequested); });

        _bgMenu = MakeMenu(renderer);
        _bgNewFolder = Add(_bgMenu, "New folder", delegate { Fire(NewFolderRequested); });
        _bgPaste = Add(_bgMenu, "Paste", delegate { Fire(PasteRequested); });
        _bgMenu.Items.Add(new ToolStripSeparator());
        _bgRefresh = Add(_bgMenu, "Refresh", delegate { Fire(RefreshRequested); });
        _bgExplorer = Add(_bgMenu, "Open in Explorer", delegate { Fire(BackgroundExplorerRequested); });
        _bgProps = Add(_bgMenu, "Properties", delegate { Fire(BackgroundPropertiesRequested); });
    }

    public void Dispose()
    {
        if (_itemMenu != null) _itemMenu.Dispose();
        if (_bgMenu != null) _bgMenu.Dispose();
    }

    public void ApplyFont(Font font)
    {
        if (font == null)
            return;
        _itemMenu.Font = font;
        _bgMenu.Font = font;
    }

    public void ShowForItem(Control host, Point clientPt, DirEntry entry, bool isPinned)
    {
        _entry = entry;
        if (entry == null || host == null)
            return;

        bool isDrive = entry.IsDrive;
        bool isDir = entry.IsDirectory;
        bool hasPath = !string.IsNullOrEmpty(entry.FullPath);

        _openItem.Enabled = hasPath;
        _pinItem.Visible = isDir && !isDrive;
        _pinItem.Enabled = isDir && !isDrive && hasPath;
        _pinItem.Text = isPinned ? "Unpin folder" : "Pin folder";
        _cutItem.Enabled = hasPath && !isDrive;
        _copyItem.Enabled = hasPath && !isDrive;
        _copyPathItem.Enabled = hasPath;
        _renameItem.Enabled = hasPath && !isDrive;
        _deleteItem.Enabled = hasPath && !isDrive;
        _explorerItem.Enabled = hasPath;
        _propsItem.Enabled = hasPath;

        _itemMenu.Show(host, clientPt);
    }

    public void ShowForBackground(Control host, Point clientPt, bool thisPc, string folderPath, bool canPaste)
    {
        _thisPc = thisPc;
        _folderPath = folderPath;
        bool inFolder = !thisPc && !string.IsNullOrEmpty(folderPath);

        _bgNewFolder.Enabled = inFolder;
        _bgPaste.Enabled = inFolder && canPaste;
        _bgRefresh.Enabled = true;
        _bgExplorer.Enabled = true;
        _bgProps.Enabled = inFolder;

        _bgMenu.Show(host, clientPt);
    }

    private static ContextMenuStrip MakeMenu(ToolStripRenderer renderer)
    {
        ContextMenuStrip menu = new ContextMenuStrip();
        menu.Renderer = renderer;
        menu.BackColor = UiTheme.Surface;
        menu.ForeColor = UiTheme.Text;
        menu.ShowImageMargin = false;
        menu.ShowCheckMargin = false;
        return menu;
    }

    private static ToolStripMenuItem Add(ContextMenuStrip menu, string text, EventHandler onClick)
    {
        ToolStripMenuItem item = new ToolStripMenuItem(text, null, onClick);
        item.BackColor = UiTheme.Surface;
        item.ForeColor = UiTheme.Text;
        menu.Items.Add(item);
        return item;
    }

    private static void Fire(Action handler)
    {
        if (handler != null)
            handler();
    }
}
