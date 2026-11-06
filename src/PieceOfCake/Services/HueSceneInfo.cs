namespace PieceOfCake.Services;

/// <param name="Id">Bridge id of the scene.</param>
/// <param name="Name">Name shown in the Hue app.</param>
/// <param name="IsActive">True while the bridge reports the scene as the one currently applied to the room.</param>
public sealed record HueSceneInfo(string Id, string Name, bool IsActive = false);
