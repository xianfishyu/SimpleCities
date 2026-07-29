using System;
using System.Collections.Generic;
using Godot;
using Godot.Collections;

[GlobalClass]
public partial class ConstructionCategoryDefinition : Resource
{
    [Export] public string Id { get; set; } = string.Empty;
    [Export] public string DisplayName { get; set; } = string.Empty;
    [Export] public int SortOrder { get; set; }
    [Export(PropertyHint.MultilineText)] public string Description { get; set; } = string.Empty;
    [Export] public Array<ConstructionToolDefinition>? Tools { get; set; } = [];

    public bool TryValidate(out string error)
    {
        if (Tools == null)
        {
            error = $"Category '{Id}' tools array cannot be null.";
            return false;
        }

        var toolIds = new List<string>(Tools.Count);
        foreach (ConstructionToolDefinition? tool in Tools)
        {
            if (tool is null)
            {
                error = $"Category '{Id}' contains an empty tool reference.";
                return false;
            }

            if (!tool.TryValidate(out error)) return false;
            toolIds.Add(tool.Id);
        }

        return TryValidate(Id, DisplayName, toolIds, out error);
    }

    public Godot.Collections.Dictionary GetValidationResult()
    {
        bool valid = TryValidate(out string error);
        return new Godot.Collections.Dictionary
        {
            ["valid"] = valid,
            ["error"] = error,
        };
    }

    public static bool TryValidate(
        string id,
        string displayName,
        IReadOnlyCollection<string>? toolIds,
        out string error)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            error = "Category ID cannot be empty.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            error = $"Category '{id}' display name cannot be empty.";
            return false;
        }

        if (toolIds == null)
        {
            error = $"Category '{id}' tools array cannot be null.";
            return false;
        }

        var uniqueToolIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (string toolId in toolIds)
        {
            if (!uniqueToolIds.Add(toolId))
            {
                error = $"Category '{id}' contains duplicate tool ID '{toolId}'.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }
}
