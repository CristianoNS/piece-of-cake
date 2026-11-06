using System.Collections.Generic;

namespace PieceOfCake.Services;

/// <summary>One tile that starts several programs at once (for example Chrome, Discord and Claude).</summary>
public sealed class AppGroupEntry
{
    public string Name { get; set; } = "";

    public List<AppEntry> Apps { get; set; } = new();
}
