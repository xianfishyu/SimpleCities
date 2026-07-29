using Godot;

[GlobalClass]
public partial class ConstructionToolDefinition : Resource
{
    [Export] public string Id { get; set; } = string.Empty;
    [Export] public string DisplayName { get; set; } = string.Empty;
    [Export] public string ShortcutHint { get; set; } = string.Empty;
    [Export] public ToolType ToolType { get; set; }
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
