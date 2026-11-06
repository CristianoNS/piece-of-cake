using System.Collections.Generic;

namespace PieceOfCake.Services;

public sealed class AppSettings
{
    public string BridgeIp { get; set; } = "";
    public string BridgeCertThumbprint { get; set; } = "";
    public string ProtectedAppKey { get; set; } = "";
    public List<AppEntry> Apps { get; set; } = new();
    public List<ScriptEntry> Scripts { get; set; } = new();
}
