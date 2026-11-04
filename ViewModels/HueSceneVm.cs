namespace PieceOfCake.ViewModels;

public sealed class HueSceneVm
{
    public HueSceneVm(string id, string name)
    {
        Id = id;
        Name = name;
    }

    public string Id { get; }

    public string Name { get; }
}
