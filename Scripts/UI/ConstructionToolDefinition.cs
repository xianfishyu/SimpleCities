using Godot;

[GlobalClass]
/// <summary>
/// 单个建造工具的资源定义。ConstructionDock 用它生成按钮，ToolContextPanel 用它显示说明。
/// </summary>
public partial class ConstructionToolDefinition : Resource
{
    /// <summary>用于资源校验和去重的稳定工具 ID。</summary>
    [Export] public string Id { get; set; } = string.Empty;
    /// <summary>显示在建造栏和工具上下文面板中的名称。</summary>
    [Export] public string DisplayName { get; set; } = string.Empty;
    [Export] public string ShortcutHint { get; set; } = string.Empty;
    [Export] public ToolType ToolType { get; set; }
    [Export] public Texture2D? Icon { get; set; }
    [Export] public int SortOrder { get; set; }
    [Export(PropertyHint.MultilineText)] public string Description { get; set; } = string.Empty;

    public bool TryValidate(out string error) => TryValidate(Id, DisplayName, out error);

    public static bool TryValidate(string id, string displayName, out string error)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            error = "Tool ID cannot be empty.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            error = $"Tool '{id}' display name cannot be empty.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
