namespace XRayForge.Core.Models;

public sealed class MapIcon
{
    public string X { get; set; } = "";
    public string Y { get; set; } = "";
}

public sealed class Character
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Community { get; set; } = "";
    public string Rank { get; set; } = "";
    public string Reputation { get; set; } = "";
    public string Class { get; set; } = "";
    public string TerrainSect { get; set; } = "";
    public string SndConfig { get; set; } = "";
    public MapIcon? MapIcon { get; set; }
    public string Bio { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Visual { get; set; } = "";
    public string Supplies { get; set; } = "";
    public List<string> StartDialog { get; set; } = new();
    public List<string> ActorDialog { get; set; } = new();
    public string Source { get; set; } = "gamedata";
}
