namespace XRayForge.Core.Lua;

public sealed class LuaToken
{
    public string Text { get; set; } = "";
    public string Cls { get; set; } = LuaTokenClasses.Plain;
}

public sealed class LuaSnippet
{
    public string Label { get; set; } = "";
    public string Text { get; set; } = "";
}

/// <summary>Скрипт для проверки script_binding: имя файла и его текст.</summary>
public sealed class LuaScriptFile
{
    public string Name { get; set; } = "";
    public string Text { get; set; } = "";
}

public sealed class LuaBlock
{
    public int Start { get; set; }
    public int End { get; set; }
}

public sealed class LuaBracketCount
{
    public int Open { get; set; }
    public int Close { get; set; }
}

public sealed class LuaCallEdge
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
}

public sealed class LuaCallGraph
{
    public List<string> Functions { get; set; } = new();
    public List<LuaCallEdge> Edges { get; set; } = new();
}

public sealed class LuaDuplicateFunction
{
    public string Name { get; set; } = "";
    public int Count { get; set; }
}

/// <summary>Диагностика Lua (luaCheck / luaUndeclaredGlobals / validateScriptBindings).</summary>
public sealed class LuaIssue
{
    public string Severity { get; set; } = "";
    public string Code { get; set; } = "";
    public int? Line { get; set; }
    public string? Binding { get; set; }
    public string? Key { get; set; }
    public string? Message { get; set; }
}