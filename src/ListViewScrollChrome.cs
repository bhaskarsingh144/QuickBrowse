using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

/// <summary>
/// Hides the native ListView vertical scrollbar and drives a ChromeScrollBar instead.
/// </summary>
internal sealed class ListViewScrollChrome : IDisposable, IMessageFilter
{
    private const int WM_MOUSEWHEEL = 0x020A;
    private const int SB_VERT = 1;
    private const int SIF_RANGE = 0x0001;
    private const int SIF_PAGE = 0x0002;
    private const int SIF_POS = 0x0004;
    private const int SIF_TRACKPOS = 0x0010;
    private const int SIF_ALL = SIF_RANGE | SIF_PAGE | SIF_POS | SIF_TRACKPOS;
    private const int WM_VSCROLL = 0x0115;
    private const int SB_THUMBPOSITION = 4;
    private const int LVM_FIRST = 0x1000;
    private const int LVM_SCROLL = LVM_FIRST + 20;

    private readonly ListView _list;
    private readonly ChromeScrollBar _bar;
    private readonly Timer _poll;
    private bool _syncing;
    private int _lastPos = -1;
    private int _lastMax = -1;
    private int _lastPage = -1;

    public ListViewScrollChrome(ListView list, ChromeScrollBar bar)
    {
        _list = list;
        _bar = bar;
        _bar.ValueChanged += OnBarValueChanged;
        _bar.MouseWheel += OnMouseWheel;
        _list.MouseWheel += OnMouseWheel;
        _list.HandleCreated += OnListHandle;
        _list.Resize += delegate { SyncFromList(); };
        _list.SelectedIndexChanged += delegate { SyncFromList(); };
        _poll = new Timer();
        _poll.Interval = 120;
        _poll.Tick += delegate { SyncFromList(); };
        _poll.Start();
        Application.AddMessageFilter(this);
        if (_list.IsHandleCreated)
            OnListHandle(_list, EventArgs.Empty);
    }

    public void Dispose()
    {
        Application.RemoveMessageFilter(this);
        _poll.Stop();
        _poll.Dispose();
        _bar.ValueChanged -= OnBarValueChanged;
        _bar.MouseWheel -= OnMouseWheel;
        _list.MouseWheel -= OnMouseWheel;
    }

    public bool PreFilterMessage(ref Message m)
    {
        if (m.Msg != WM_MOUSEWHEEL || _list == null || _list.IsDisposed || !_list.IsHandleCreated)
            return false;
        if (!_list.Visible)
            return false;
        Point screen = Control.MousePosition;
        bool overList = _list.RectangleToScreen(_list.ClientRectangle).Contains(screen);
        bool overBar = _bar != null && _bar.Visible
            && _bar.RectangleToScreen(_bar.ClientRectangle).Contains(screen);
        if (!overList && !overBar)
            return false;
        int delta = (short)((m.WParam.ToInt64() >> 16) & 0xffff);
        ApplyWheelDelta(delta);
        return true; // eat — avoid native + our handler double-scrolling
    }

    public void SyncFromList()
    {
        if (_list == null || _list.IsDisposed || !_list.IsHandleCreated)
            return;
        try
        {
            ShowScrollBar(_list.Handle, SB_VERT, false);

            SCROLLINFO si = new SCROLLINFO();
            si.cbSize = Marshal.SizeOf(typeof(SCROLLINFO));
            si.fMask = SIF_ALL;
            if (!GetScrollInfo(_list.Handle, SB_VERT, ref si))
            {
                _bar.Visible = false;
                return;
            }

            int max = si.nMax;
            int min = si.nMin;
            int page = (int)si.nPage;
            int pos = si.nPos;
            int range = max - min + 1;
            bool need = range > page && page > 0;
            _bar.Visible = need;
            if (!need)
                return;

            if (pos == _lastPos && max == _lastMax && page == _lastPage)
                return;
            _lastPos = pos;
            _lastMax = max;
            _lastPage = page;

            _syncing = true;
            try
            {
                // Map native inclusive max to ChromeScrollBar's exclusive-style Maximum.
                _bar.SetMetrics(min, max + 1, Math.Max(1, page), pos);
            }
            finally
            {
                _syncing = false;
            }
        }
        catch
        {
        }
    }

    private void OnListHandle(object sender, EventArgs e)
    {
        ShowScrollBar(_list.Handle, SB_VERT, false);
        SyncFromList();
    }

    private void OnMouseWheel(object sender, MouseEventArgs e)
    {
        ApplyWheelDelta(e.Delta);
    }

    /// <summary>Scroll from a wheel delta (e.g. form WndProc when focus is elsewhere).</summary>
    public void ApplyWheelDelta(int delta)
    {
        if (_list == null || _list.IsDisposed || !_list.IsHandleCreated)
            return;
        // Hiding the native SB_VERT often breaks ListView's built-in wheel scroll.
        // Drive our bar (and thus LVM_SCROLL) from the wheel delta instead.
        SyncFromList();
        if (!_bar.Visible)
            return;
        int lines = SystemInformation.MouseWheelScrollLines;
        if (lines <= 0)
            lines = 3;
        // Scroll units match SB_VERT / LVM_SCROLL (pixels in icon/tile views).
        int linePx = Math.Max(24, Math.Min(64, _bar.LargeChange / 8));
        int notches = delta / 120;
        if (notches == 0)
            notches = delta > 0 ? 1 : -1;
        _bar.Value = _bar.Value - (notches * lines * linePx);
    }

    private void OnBarValueChanged(object sender, EventArgs e)
    {
        if (_syncing || _list == null || !_list.IsHandleCreated)
            return;
        try
        {
            SCROLLINFO cur = new SCROLLINFO();
            cur.cbSize = Marshal.SizeOf(typeof(SCROLLINFO));
            cur.fMask = SIF_POS | SIF_PAGE | SIF_RANGE;
            GetScrollInfo(_list.Handle, SB_VERT, ref cur);
            int delta = _bar.Value - cur.nPos;
            if (delta == 0)
                return;

            // ListView icon/tile modes: LVM_SCROLL is in pixels (matches SB_VERT for those views).
            SendMessage(_list.Handle, LVM_SCROLL, IntPtr.Zero, (IntPtr)delta);
            _lastPos = _bar.Value;
            ShowScrollBar(_list.Handle, SB_VERT, false);
        }
        catch
        {
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SCROLLINFO
    {
        public int cbSize;
        public int fMask;
        public int nMin;
        public int nMax;
        public uint nPage;
        public int nPos;
        public int nTrackPos;
    }

    [DllImport("user32.dll")]
    private static extern bool ShowScrollBar(IntPtr hWnd, int wBar, bool bShow);

    [DllImport("user32.dll")]
    private static extern bool GetScrollInfo(IntPtr hWnd, int fnBar, ref SCROLLINFO lpsi);

    [DllImport("user32.dll")]
    private static extern int SetScrollInfo(IntPtr hWnd, int fnBar, ref SCROLLINFO lpsi, bool redraw);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
}
