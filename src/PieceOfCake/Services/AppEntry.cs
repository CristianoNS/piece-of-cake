namespace PieceOfCake.Services;

/// <summary>A program shown as a launcher tile (with its own icon) in the panel.</summary>
public sealed class AppEntry
{
    public string Name { get; set; } = "";

    /// <summary>
    /// What to open: a file path (exe, shortcut, folder), a web address, or <c>shell:AppsFolder\AppId</c>
    /// for any app from the Start menu (including Store apps). Use <c>Get-StartApps</c> in PowerShell to see the ids.
    /// </summary>
    public string Target { get; set; } = "";
}
