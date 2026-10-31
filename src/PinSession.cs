using System;
using System.IO;

internal enum PinSessionActive
{
    None,
    Place,
    Pin
}

internal enum FlyoutKind
{
    None,
    Pins,
    Recent
}

internal enum RecentActivateKind
{
    None,
    Folder,
    File
}

/// <summary>
/// Pure click / idle / park state for Pins flyout, Recent panel, and browse pane.
/// ShellHost applies the resulting flags to chrome.
/// </summary>
internal sealed class PinSession
{
    private readonly PinStore _store;

    public PinSession(PinStore store)
    {
        _store = store ?? new PinStore();
        LastActivityUtc = DateTime.UtcNow;
    }

    public PinStore Store { get { return _store; } }

    public FlyoutKind FlyoutKind { get; private set; }
    /// <summary>Pins slim flyout only — Recent uses the pane-slot panel.</summary>
    public bool FlyoutOpen { get { return FlyoutKind == FlyoutKind.Pins; } }
    public bool PinsFlyoutOpen { get { return FlyoutKind == FlyoutKind.Pins; } }
    public bool RecentPanelOpen { get { return FlyoutKind == FlyoutKind.Recent; } }
    /// <summary>Compat alias for RecentPanelOpen (tests / older call sites).</summary>
    public bool RecentFlyoutOpen { get { return RecentPanelOpen; } }
    public bool PaneOpen { get; private set; }
    public PinSessionActive ActiveKind { get; private set; }
    public int ActivePlaceIndex { get; private set; }
    public string ActivePlaceId { get; private set; }
    public string ActivePath { get; private set; }
    public bool ActiveThisPc { get; private set; }
    public string LastError { get; private set; }
    public DateTime LastActivityUtc { get; private set; }
    public RecentActivateKind LastRecentActivate { get; private set; }
    public string LastRecentPath { get; private set; }

    public bool IsCurrentPinned
    {
        get
        {
            if (ActiveThisPc || string.IsNullOrEmpty(ActivePath))
                return false;
            return _store.Contains(ActivePath);
        }
    }

    public bool CanPinCurrent
    {
        get
        {
            if (ActiveThisPc || string.IsNullOrEmpty(ActivePath))
                return false;
            try
            {
                return Directory.Exists(ActivePath);
            }
            catch
            {
                return false;
            }
        }
    }

    public void NoteActivity()
    {
        LastActivityUtc = DateTime.UtcNow;
    }

    public void TogglePins()
    {
        NoteActivity();
        LastError = null;
        if (FlyoutKind == FlyoutKind.Pins)
        {
            FlyoutKind = FlyoutKind.None;
            return;
        }
        ClearActive();
        FlyoutKind = FlyoutKind.Pins;
    }

    public void ToggleRecent()
    {
        NoteActivity();
        LastError = null;
        if (FlyoutKind == FlyoutKind.Recent)
        {
            FlyoutKind = FlyoutKind.None;
            return;
        }
        ClearActive();
        FlyoutKind = FlyoutKind.Recent;
    }

    /// <summary>Compat alias for TogglePins.</summary>
    public void ToggleMore()
    {
        TogglePins();
    }

    public void CloseFlyout()
    {
        FlyoutKind = FlyoutKind.None;
    }

    public void CloseRecent()
    {
        NoteActivity();
        if (FlyoutKind == FlyoutKind.Recent)
            FlyoutKind = FlyoutKind.None;
    }

    public bool OpenPin(string path)
    {
        NoteActivity();
        LastError = null;
        LastRecentActivate = RecentActivateKind.None;
        LastRecentPath = null;
        string norm = PinStore.Normalize(path);
        if (norm == null)
        {
            LastError = "Invalid path.";
            return false;
        }
        try
        {
            if (!Directory.Exists(norm))
            {
                LastError = "Folder not found.";
                FlyoutKind = FlyoutKind.None;
                return false;
            }
        }
        catch
        {
            LastError = "Folder not found.";
            FlyoutKind = FlyoutKind.None;
            return false;
        }

        FlyoutKind = FlyoutKind.None;
        PaneOpen = true;
        ActiveKind = PinSessionActive.Pin;
        ActivePlaceIndex = -1;
        ActivePlaceId = null;
        ActiveThisPc = false;
        ActivePath = norm;
        return true;
    }

    /// <summary>Open a recent item: folder → pane; file → close flyout and set LastRecent*.</summary>
    public bool OpenRecentPath(string path)
    {
        NoteActivity();
        LastError = null;
        LastRecentActivate = RecentActivateKind.None;
        LastRecentPath = null;
        string norm = PinStore.Normalize(path);
        if (norm == null)
        {
            LastError = "Invalid path.";
            return false;
        }

        try
        {
            if (Directory.Exists(norm))
            {
                FlyoutKind = FlyoutKind.None;
                PaneOpen = true;
                ActiveKind = PinSessionActive.Pin;
                ActivePlaceIndex = -1;
                ActivePlaceId = null;
                ActiveThisPc = false;
                ActivePath = norm;
                LastRecentActivate = RecentActivateKind.Folder;
                LastRecentPath = norm;
                return true;
            }
            if (File.Exists(norm))
            {
                FlyoutKind = FlyoutKind.None;
                LastRecentActivate = RecentActivateKind.File;
                LastRecentPath = norm;
                return true;
            }
        }
        catch
        {
        }

        LastError = "Item not found.";
        FlyoutKind = FlyoutKind.None;
        return false;
    }

    public void OpenPlace(int index, string placeId, string path)
    {
        NoteActivity();
        LastError = null;
        FlyoutKind = FlyoutKind.None;

        if (PaneOpen && ActiveKind == PinSessionActive.Place && ActivePlaceIndex == index)
        {
            ClearActive();
            return;
        }

        PaneOpen = true;
        ActiveKind = PinSessionActive.Place;
        ActivePlaceIndex = index;
        ActivePlaceId = placeId;
        ActiveThisPc = string.Equals(placeId, "thispc", StringComparison.OrdinalIgnoreCase);
        ActivePath = ActiveThisPc ? null : PinStore.Normalize(path);
    }

    public void OpenThisPc(int index)
    {
        OpenPlace(index, "thispc", null);
        ActiveThisPc = true;
        ActivePath = null;
    }

    public void OpenPath(string path)
    {
        NoteActivity();
        LastError = null;
        string norm = PinStore.Normalize(path);
        PaneOpen = true;
        ActiveKind = PinSessionActive.Pin;
        ActivePlaceIndex = -1;
        ActivePlaceId = null;
        ActiveThisPc = false;
        ActivePath = norm;
    }

    public void ClosePane()
    {
        NoteActivity();
        ClearActive();
    }

    public bool PinCurrent()
    {
        NoteActivity();
        LastError = null;
        if (!CanPinCurrent)
            return false;
        return _store.Add(ActivePath);
    }

    public bool UnpinCurrent()
    {
        NoteActivity();
        LastError = null;
        if (string.IsNullOrEmpty(ActivePath))
            return false;
        return _store.Remove(ActivePath);
    }

    public bool PinPath(string path)
    {
        NoteActivity();
        LastError = null;
        string norm = PinStore.Normalize(path);
        if (norm == null)
            return false;
        try
        {
            if (!Directory.Exists(norm))
                return false;
        }
        catch
        {
            return false;
        }
        return _store.Add(norm);
    }

    public bool UnpinPath(string path)
    {
        NoteActivity();
        LastError = null;
        return _store.Remove(path);
    }

    public void IdleReset()
    {
        FlyoutKind = FlyoutKind.None;
        ClearActive();
    }

    public void OnPark()
    {
        FlyoutKind = FlyoutKind.None;
    }

    private void ClearActive()
    {
        PaneOpen = false;
        ActiveKind = PinSessionActive.None;
        ActivePlaceIndex = -1;
        ActivePlaceId = null;
        ActivePath = null;
        ActiveThisPc = false;
    }
}
