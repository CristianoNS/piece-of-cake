using System.Collections.Generic;

namespace PieceOfCake.Services;

public sealed record HueRoomInfo(
    string Id,
    string Name,
    string GroupedLightId,
    bool IsOn,
    double Brightness,
    IReadOnlyList<HueSceneInfo> Scenes);
