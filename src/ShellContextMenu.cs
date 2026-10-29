using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

/// <summary>
/// Hosts the Windows Shell IContextMenu for a path (item or folder background).
/// Third-party hosts get the classic/full menu (Win11 "Show more options" verbs).
/// </summary>
internal static class ShellContextMenu
{
    // Custom commands live below the shell band.
    public const uint CustomPinId = 1;
    public const uint CustomUnpinId = 2;
    public const uint ShellIdCmdFirst = 100;
    public const uint ShellIdCmdLast = 0x7FFF;

    private const string ThisPcParsingName = "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}";

    private const uint CMF_NORMAL = 0x00000000;
    private const uint CMF_EXPLORE = 0x00000004;
    private const uint CMF_EXTENDEDVERBS = 0x00000100;
    private const uint CMF_CANRENAME = 0x00000010;

    private const uint TPM_RIGHTBUTTON = 0x0002;
    private const uint TPM_RETURNCMD = 0x0100;

    private const uint MF_STRING = 0x00000000;
    private const uint MF_SEPARATOR = 0x00000800;
    private const uint MF_BYPOSITION = 0x00000400;

    private const int SW_SHOWNORMAL = 1;
    private const uint CMIC_MASK_UNICODE = 0x00004000;
    private const uint CMIC_MASK_PTINVOKE = 0x20000000;

    private const uint FO_DELETE = 0x0003;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_SILENT = 0x0004;

    private const int WM_INITMENUPOPUP = 0x0117;
    private const int WM_DRAWITEM = 0x002B;
    private const int WM_MEASUREITEM = 0x002C;
    private const int WM_MENUCHAR = 0x0120;

    private static readonly Guid IidIShellFolder = new Guid("000214E6-0000-0000-C000-000000000046");
    private static readonly Guid IidIContextMenu = new Guid("000214E4-0000-0000-C000-000000000046");
    private static readonly Guid IidIContextMenu2 = new Guid("000214F4-0000-0000-C000-000000000046");
    private static readonly Guid IidIContextMenu3 = new Guid("BCFCE0A0-EC17-11D0-8D10-00A0C90F2719");

    private static IContextMenu2 _activeCm2;
    private static IContextMenu3 _activeCm3;

    public sealed class ShowResult
    {
        public bool Shown;
        public bool Invoked;
        public bool NeedsRefresh;
        public bool PinRequested;
        public bool UnpinRequested;
    }

    /// <summary>Pin menu caption for a selected entry; null when Pin/Unpin should not appear.</summary>
    public static string PinCommandLabel(bool isDirectory, bool isPinned)
    {
        if (!isDirectory)
            return null;
        return isPinned ? "Unpin folder" : "Pin folder";
    }

    public static bool CustomIdsBelowShellBand()
    {
        return CustomPinId < ShellIdCmdFirst
            && CustomUnpinId < ShellIdCmdFirst
            && CustomPinId != CustomUnpinId;
    }

    /// <summary>Forward owner-draw / cascade messages while TrackPopupMenuEx is active.</summary>
    public static bool TryHandleMenuMessage(ref Message m)
    {
        if (m.Msg != WM_INITMENUPOPUP && m.Msg != WM_DRAWITEM && m.Msg != WM_MEASUREITEM && m.Msg != WM_MENUCHAR)
            return false;

        if (_activeCm3 != null)
        {
            IntPtr result;
            int hr = _activeCm3.HandleMenuMsg2((uint)m.Msg, m.WParam, m.LParam, out result);
            if (hr >= 0)
            {
                m.Result = result;
                return true;
            }
        }
        else if (_activeCm2 != null)
        {
            int hr = _activeCm2.HandleMenuMsg((uint)m.Msg, m.WParam, m.LParam);
            if (hr >= 0)
            {
                m.Result = IntPtr.Zero;
                return true;
            }
        }
        return false;
    }

    public static ShowResult ShowForItem(IntPtr hwnd, string path, Point screenPt, bool isDirectory, bool isPinned, bool extendedVerbs)
    {
        ShowResult result = new ShowResult();
        if (hwnd == IntPtr.Zero || string.IsNullOrEmpty(path))
            return result;

        IntPtr absPidl = IntPtr.Zero;
        IntPtr folderPtr = IntPtr.Zero;
        IntPtr childPidl = IntPtr.Zero;
        IntPtr menuPtr = IntPtr.Zero;
        IShellFolder folder = null;
        IContextMenu context = null;

        try
        {
            absPidl = CreateAbsolutePidl(path);
            if (absPidl == IntPtr.Zero)
                return result;

            Guid iidFolder = IidIShellFolder;
            int hr = SHBindToParent(absPidl, ref iidFolder, out folderPtr, out childPidl);
            if (hr < 0 || folderPtr == IntPtr.Zero || childPidl == IntPtr.Zero)
                return result;

            folder = (IShellFolder)Marshal.GetTypedObjectForIUnknown(folderPtr, typeof(IShellFolder));
            folderPtr = IntPtr.Zero; // ownership transferred to RCW

            IntPtr[] kids = new IntPtr[] { childPidl };
            Guid iid = IidIContextMenu;
            hr = folder.GetUIObjectOf(hwnd, 1, kids, ref iid, IntPtr.Zero, out menuPtr);
            if (hr < 0 || menuPtr == IntPtr.Zero)
                return result;

            context = (IContextMenu)Marshal.GetTypedObjectForIUnknown(menuPtr, typeof(IContextMenu));
            menuPtr = IntPtr.Zero;

            string pinLabel = PinCommandLabel(isDirectory, isPinned);
            result = TrackAndInvoke(hwnd, context, screenPt, extendedVerbs, pinLabel, isPinned, ParentDirectory(path));
        }
        catch
        {
            result = new ShowResult();
        }
        finally
        {
            ClearActiveHandlers();
            if (context != null)
            {
                try { Marshal.ReleaseComObject(context); }
                catch { }
            }
            if (menuPtr != IntPtr.Zero)
                Marshal.Release(menuPtr);
            if (folder != null)
            {
                try { Marshal.ReleaseComObject(folder); }
                catch { }
            }
            if (folderPtr != IntPtr.Zero)
                Marshal.Release(folderPtr);
            // childPidl is an interior pointer into absPidl — do not free separately.
            if (absPidl != IntPtr.Zero)
                ILFree(absPidl);
        }
        return result;
    }

    public static ShowResult ShowForBackground(IntPtr hwnd, string folderPathOrNullForThisPc, Point screenPt, bool extendedVerbs)
    {
        ShowResult result = new ShowResult();
        if (hwnd == IntPtr.Zero)
            return result;

        string target = string.IsNullOrEmpty(folderPathOrNullForThisPc) ? ThisPcParsingName : folderPathOrNullForThisPc;
        IntPtr absPidl = IntPtr.Zero;
        IntPtr parentPtr = IntPtr.Zero;
        IntPtr childPidl = IntPtr.Zero;
        IntPtr folderPtr = IntPtr.Zero;
        IntPtr menuPtr = IntPtr.Zero;
        IShellFolder parent = null;
        IShellFolder folder = null;
        IContextMenu context = null;

        try
        {
            absPidl = CreateAbsolutePidl(target);
            if (absPidl == IntPtr.Zero)
                return result;

            Guid iidParent = IidIShellFolder;
            int hr = SHBindToParent(absPidl, ref iidParent, out parentPtr, out childPidl);
            if (hr < 0 || parentPtr == IntPtr.Zero || childPidl == IntPtr.Zero)
                return result;

            parent = (IShellFolder)Marshal.GetTypedObjectForIUnknown(parentPtr, typeof(IShellFolder));
            parentPtr = IntPtr.Zero;

            Guid iidFolder = IidIShellFolder;
            hr = parent.BindToObject(childPidl, IntPtr.Zero, ref iidFolder, out folderPtr);
            if (hr < 0 || folderPtr == IntPtr.Zero)
                return result;

            folder = (IShellFolder)Marshal.GetTypedObjectForIUnknown(folderPtr, typeof(IShellFolder));
            folderPtr = IntPtr.Zero;

            Guid iidMenu = IidIContextMenu;
            hr = folder.CreateViewObject(hwnd, ref iidMenu, out menuPtr);
            if (hr < 0 || menuPtr == IntPtr.Zero)
                return result;

            context = (IContextMenu)Marshal.GetTypedObjectForIUnknown(menuPtr, typeof(IContextMenu));
            menuPtr = IntPtr.Zero;

            string workDir = string.IsNullOrEmpty(folderPathOrNullForThisPc) ? null : folderPathOrNullForThisPc;
            result = TrackAndInvoke(hwnd, context, screenPt, extendedVerbs, null, false, workDir);
        }
        catch
        {
            result = new ShowResult();
        }
        finally
        {
            ClearActiveHandlers();
            if (context != null)
                Marshal.ReleaseComObject(context);
            if (menuPtr != IntPtr.Zero)
                Marshal.Release(menuPtr);
            if (folder != null)
                Marshal.ReleaseComObject(folder);
            if (folderPtr != IntPtr.Zero)
                Marshal.Release(folderPtr);
            if (parent != null)
                Marshal.ReleaseComObject(parent);
            if (parentPtr != IntPtr.Zero)
                Marshal.Release(parentPtr);
            if (absPidl != IntPtr.Zero)
                ILFree(absPidl);
        }
        return result;
    }

    /// <summary>Send path to Recycle Bin (FO_DELETE + FOF_ALLOWUNDO). Caller may confirm first.</summary>
    public static bool SendToRecycleBin(IntPtr hwnd, string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;
        try
        {
            SHFILEOPSTRUCT op = new SHFILEOPSTRUCT();
            op.hwnd = hwnd;
            op.wFunc = FO_DELETE;
            op.pFrom = path + "\0\0";
            op.pTo = null;
            op.fFlags = (short)(FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT);
            op.fAnyOperationsAborted = false;
            op.hNameMappings = IntPtr.Zero;
            op.lpszProgressTitle = null;
            int rc = SHFileOperation(ref op);
            return rc == 0 && !op.fAnyOperationsAborted;
        }
        catch
        {
            return false;
        }
    }

    private static ShowResult TrackAndInvoke(
        IntPtr hwnd,
        IContextMenu context,
        Point screenPt,
        bool extendedVerbs,
        string pinLabel,
        bool isPinned,
        string workDir)
    {
        ShowResult result = new ShowResult();
        IntPtr hmenu = CreatePopupMenu();
        if (hmenu == IntPtr.Zero)
            return result;

        try
        {
            uint insertAt = 0;
            if (!string.IsNullOrEmpty(pinLabel))
            {
                uint id = isPinned ? CustomUnpinId : CustomPinId;
                InsertMenu(hmenu, insertAt, MF_BYPOSITION | MF_STRING, new IntPtr(id), pinLabel);
                insertAt++;
                InsertMenu(hmenu, insertAt, MF_BYPOSITION | MF_SEPARATOR, IntPtr.Zero, null);
                insertAt++;
            }

            uint flags = CMF_NORMAL | CMF_EXPLORE | CMF_CANRENAME;
            if (extendedVerbs)
                flags |= CMF_EXTENDEDVERBS;

            int hr = context.QueryContextMenu(hmenu, insertAt, ShellIdCmdFirst, ShellIdCmdLast, flags);
            if (hr < 0)
                return result;

            result.Shown = true;

            IntPtr unk = IntPtr.Zero;
            IntPtr cm2Ptr = IntPtr.Zero;
            IntPtr cm3Ptr = IntPtr.Zero;
            try
            {
                unk = Marshal.GetIUnknownForObject(context);
                Guid g2 = IidIContextMenu2;
                Guid g3 = IidIContextMenu3;
                if (Marshal.QueryInterface(unk, ref g3, out cm3Ptr) == 0 && cm3Ptr != IntPtr.Zero)
                    _activeCm3 = (IContextMenu3)Marshal.GetTypedObjectForIUnknown(cm3Ptr, typeof(IContextMenu3));
                else if (Marshal.QueryInterface(unk, ref g2, out cm2Ptr) == 0 && cm2Ptr != IntPtr.Zero)
                    _activeCm2 = (IContextMenu2)Marshal.GetTypedObjectForIUnknown(cm2Ptr, typeof(IContextMenu2));

                uint cmd = TrackPopupMenuEx(
                    hmenu,
                    TPM_RETURNCMD | TPM_RIGHTBUTTON,
                    screenPt.X,
                    screenPt.Y,
                    hwnd,
                    IntPtr.Zero);

                if (cmd == 0)
                    return result;

                if (cmd == CustomPinId)
                {
                    result.Invoked = true;
                    result.PinRequested = true;
                    return result;
                }
                if (cmd == CustomUnpinId)
                {
                    result.Invoked = true;
                    result.UnpinRequested = true;
                    return result;
                }

                if (cmd < ShellIdCmdFirst || cmd > ShellIdCmdLast)
                    return result;

                uint verbIndex = cmd - ShellIdCmdFirst;
                InvokeShellCommand(context, hwnd, verbIndex, workDir, screenPt);
                result.Invoked = true;
                result.NeedsRefresh = true;
            }
            finally
            {
                ClearActiveHandlers();
                if (cm3Ptr != IntPtr.Zero)
                    Marshal.Release(cm3Ptr);
                if (cm2Ptr != IntPtr.Zero)
                    Marshal.Release(cm2Ptr);
                if (unk != IntPtr.Zero)
                    Marshal.Release(unk);
            }
        }
        finally
        {
            DestroyMenu(hmenu);
        }
        return result;
    }

    private static void InvokeShellCommand(IContextMenu context, IntPtr hwnd, uint verbIndex, string workDir, Point screenPt)
    {
        IntPtr dirPtr = IntPtr.Zero;
        try
        {
            if (!string.IsNullOrEmpty(workDir))
                dirPtr = Marshal.StringToHGlobalUni(workDir);

            CMINVOKECOMMANDINFOEX info = new CMINVOKECOMMANDINFOEX();
            info.cbSize = Marshal.SizeOf(typeof(CMINVOKECOMMANDINFOEX));
            info.fMask = CMIC_MASK_UNICODE | CMIC_MASK_PTINVOKE;
            info.hwnd = hwnd;
            info.lpVerb = new IntPtr(verbIndex);
            info.lpParameters = IntPtr.Zero;
            info.lpDirectory = IntPtr.Zero;
            info.nShow = SW_SHOWNORMAL;
            info.dwHotKey = 0;
            info.hIcon = IntPtr.Zero;
            info.lpTitle = IntPtr.Zero;
            info.lpVerbW = new IntPtr(verbIndex);
            info.lpParametersW = IntPtr.Zero;
            info.lpDirectoryW = dirPtr;
            info.lpTitleW = IntPtr.Zero;
            info.ptInvoke = new POINT(screenPt.X, screenPt.Y);
            context.InvokeCommand(ref info);
        }
        finally
        {
            if (dirPtr != IntPtr.Zero)
                Marshal.FreeHGlobal(dirPtr);
        }
    }

    private static void ClearActiveHandlers()
    {
        if (_activeCm3 != null)
        {
            try { Marshal.ReleaseComObject(_activeCm3); }
            catch { }
            _activeCm3 = null;
        }
        if (_activeCm2 != null)
        {
            try { Marshal.ReleaseComObject(_activeCm2); }
            catch { }
            _activeCm2 = null;
        }
    }

    private static IntPtr CreateAbsolutePidl(string path)
    {
        if (string.IsNullOrEmpty(path))
            return IntPtr.Zero;
        IntPtr pidl = ILCreateFromPathW(path);
        if (pidl != IntPtr.Zero)
            return pidl;

        IntPtr parsed = IntPtr.Zero;
        uint attrs = 0;
        int hr = SHParseDisplayName(path, IntPtr.Zero, out parsed, 0, ref attrs);
        if (hr >= 0 && parsed != IntPtr.Zero)
            return parsed;
        return IntPtr.Zero;
    }

    private static string ParentDirectory(string path)
    {
        try
        {
            if (string.IsNullOrEmpty(path))
                return null;
            string parent = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(parent))
            {
                // Drive root like C:\
                if (path.Length >= 2 && path[1] == ':')
                    return path.Substring(0, 2) + "\\";
                return null;
            }
            return parent;
        }
        catch
        {
            return null;
        }
    }

    #region Native

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        public string pFrom;
        public string pTo;
        public short fFlags;
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string lpszProgressTitle;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
        public POINT(int x, int y) { X = x; Y = y; }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CMINVOKECOMMANDINFOEX
    {
        public int cbSize;
        public uint fMask;
        public IntPtr hwnd;
        public IntPtr lpVerb;
        public IntPtr lpParameters;
        public IntPtr lpDirectory;
        public int nShow;
        public int dwHotKey;
        public IntPtr hIcon;
        public IntPtr lpTitle;
        public IntPtr lpVerbW;
        public IntPtr lpParametersW;
        public IntPtr lpDirectoryW;
        public IntPtr lpTitleW;
        public POINT ptInvoke;
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214E6-0000-0000-C000-000000000046")]
    private interface IShellFolder
    {
        void ParseDisplayName(IntPtr hwnd, IntPtr pbc, [MarshalAs(UnmanagedType.LPWStr)] string pszDisplayName, ref uint pchEaten, out IntPtr ppidl, ref uint pdwAttributes);

        [PreserveSig]
        int EnumObjects(IntPtr hwnd, int grfFlags, out IntPtr ppenumIDList);

        [PreserveSig]
        int BindToObject(IntPtr pidl, IntPtr pbc, ref Guid riid, out IntPtr ppv);

        [PreserveSig]
        int BindToStorage(IntPtr pidl, IntPtr pbc, ref Guid riid, out IntPtr ppv);

        [PreserveSig]
        int CompareIDs(IntPtr lParam, IntPtr pidl1, IntPtr pidl2);

        [PreserveSig]
        int CreateViewObject(IntPtr hwndOwner, ref Guid riid, out IntPtr ppv);

        [PreserveSig]
        int GetAttributesOf(uint cidl, IntPtr[] apidl, ref uint rgfInOut);

        [PreserveSig]
        int GetUIObjectOf(IntPtr hwndOwner, uint cidl, IntPtr[] apidl, ref Guid riid, IntPtr rgfReserved, out IntPtr ppv);

        [PreserveSig]
        int GetDisplayNameOf(IntPtr pidl, uint uFlags, IntPtr pName);

        [PreserveSig]
        int SetNameOf(IntPtr hwnd, IntPtr pidl, [MarshalAs(UnmanagedType.LPWStr)] string pszName, uint uFlags, out IntPtr ppidlOut);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214E4-0000-0000-C000-000000000046")]
    private interface IContextMenu
    {
        [PreserveSig]
        int QueryContextMenu(IntPtr hmenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);

        [PreserveSig]
        int InvokeCommand(ref CMINVOKECOMMANDINFOEX pici);

        [PreserveSig]
        int GetCommandString(UIntPtr idCmd, uint uFlags, IntPtr pReserved, StringBuilder pszName, uint cchMax);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F4-0000-0000-C000-000000000046")]
    private interface IContextMenu2
    {
        [PreserveSig]
        int QueryContextMenu(IntPtr hmenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);

        [PreserveSig]
        int InvokeCommand(ref CMINVOKECOMMANDINFOEX pici);

        [PreserveSig]
        int GetCommandString(UIntPtr idCmd, uint uFlags, IntPtr pReserved, StringBuilder pszName, uint cchMax);

        [PreserveSig]
        int HandleMenuMsg(uint uMsg, IntPtr wParam, IntPtr lParam);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("BCFCE0A0-EC17-11D0-8D10-00A0C90F2719")]
    private interface IContextMenu3
    {
        [PreserveSig]
        int QueryContextMenu(IntPtr hmenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);

        [PreserveSig]
        int InvokeCommand(ref CMINVOKECOMMANDINFOEX pici);

        [PreserveSig]
        int GetCommandString(UIntPtr idCmd, uint uFlags, IntPtr pReserved, StringBuilder pszName, uint cchMax);

        [PreserveSig]
        int HandleMenuMsg(uint uMsg, IntPtr wParam, IntPtr lParam);

        [PreserveSig]
        int HandleMenuMsg2(uint uMsg, IntPtr wParam, IntPtr lParam, out IntPtr plResult);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr ILCreateFromPathW(string pszPath);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHParseDisplayName(string name, IntPtr bindingContext, out IntPtr pidl, uint sfgaoIn, ref uint psfgaoOut);

    [DllImport("shell32.dll")]
    private static extern int SHBindToParent(IntPtr pidl, ref Guid riid, out IntPtr ppv, out IntPtr ppidlLast);

    [DllImport("shell32.dll")]
    private static extern void ILFree(IntPtr pidl);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool InsertMenu(IntPtr hMenu, uint uPosition, uint uFlags, IntPtr uIDNewItem, string lpNewItem);

    [DllImport("user32.dll")]
    private static extern uint TrackPopupMenuEx(IntPtr hmenu, uint fuFlags, int x, int y, IntPtr hwnd, IntPtr lptpm);

    #endregion
}
