using System.Drawing;

/// <summary>
/// Matches QuickView dark chrome with a slightly softer Win11 flyout feel.
/// </summary>
internal static class UiTheme
{
    public static readonly Color Bg = Color.FromArgb(255, 0x20, 0x22, 0x27);
    public static readonly Color Surface = Color.FromArgb(255, 0x2B, 0x2E, 0x35);
    public static readonly Color Raised = Color.FromArgb(255, 0x34, 0x38, 0x40);
    public static readonly Color Border = Color.FromArgb(255, 0x45, 0x4A, 0x54);
    public static readonly Color BorderSoft = Color.FromArgb(255, 0x55, 0x5B, 0x66);
    public static readonly Color Text = Color.FromArgb(255, 0xE8, 0xEB, 0xF0);
    /// <summary>Secondary body text — brighter than classic muted so labels stay readable on dark chrome.</summary>
    public static readonly Color TextSecondary = Color.FromArgb(255, 0xC8, 0xCD, 0xD6);
    public static readonly Color Muted = Color.FromArgb(255, 0x9A, 0xA1, 0xAC);
    public static readonly Color Accent = Color.FromArgb(255, 0x60, 0xCD, 0xFF); // Win11 accent-ish
    public static readonly Color AccentSoft = Color.FromArgb(40, 0x60, 0xCD, 0xFF);
    public static readonly Color Hover = Color.FromArgb(255, 0x3A, 0x3F, 0x48);
    public static readonly Color Press = Color.FromArgb(255, 0x46, 0x4C, 0x56);
    public static readonly Color Chip = Color.FromArgb(255, 0x32, 0x36, 0x3E);
}
