using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

/// <summary>
/// Hides the native ListView vertical scrollbar and drives a ChromeScrollBar instead.
/// </summary>
internal sealed class ListViewScrollChrome : IDisposable
{
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
        _list.HandleCreated += OnListHandle;
        _list.Resize += delegate { SyncFromList(); };
        _list.SelectedIndexChanged += delegate { SyncFromList(); };
        _poll = new Timer();
        _poll.Interval = 120;
        _poll.Tick += delegate { SyncFromList(); };
        _poll.Start();
        if (_list.IsHandleCreated)
            OnListHandle(_list, EventArgs.Empty);
    }

    public void Dispose()
    {
        _poll.Stop();
        _poll.Dispose();
        _bar.ValueChanged -= OnBarValueChanged;
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
