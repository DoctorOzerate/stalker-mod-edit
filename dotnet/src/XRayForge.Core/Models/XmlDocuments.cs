namespace XRayForge.Core.Models;

public sealed class CommunityRelation
{
    public string Community { get; set; } = "";
    public string Value { get; set; } = "";
}

public sealed class Community
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "";
    public List<CommunityRelation> Relations { get; set; } = new();
}

/// <summary>character_desc_general.xml: сообщества и их отношения (#9).</summary>
public sealed class CharacterDescGeneral
{
    public List<Community> Communities { get; set; } = new();
}

/// <summary>info_portions.xml: одна info-порция (#60, #61).</summary>
public sealed class InfoPortion
{
    public string Id { get; set; } = "";
    public List<string> Tasks { get; set; } = new();
    public List<string> Dialogs { get; set; } = new();
    public List<string> Disables { get; set; } = new();
    public List<string> Articles { get; set; } = new();
}

/// <summary>encyclopedia_*.xml: статья (#63).</summary>
public sealed class EncyclopediaArticle
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Group { get; set; } = "";
    public string Ltx { get; set; } = "";
    public string Text { get; set; } = "";
}

/// <summary>npc_profile.xml: профиль персонажа (#17).</summary>
public sealed class NpcProfileCharacter
{
    public string Id { get; set; } = "";
    public string Class { get; set; } = "";
    public string SpecificCharacter { get; set; } = "";
}

/// <summary>Дескриптор текстуры в атласе ui_*.xml (#18).</summary>
public sealed class TextureEntry
{
    public string Id { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}

public sealed class TextureDescriptor
{
    public string File { get; set; } = "";
    public List<TextureEntry> Textures { get; set; } = new();
}