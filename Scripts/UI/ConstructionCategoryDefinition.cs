using System;
using System.Collections.Generic;
using Godot;
using Godot.Collections;

[GlobalClass]
/// <summary>
/// 建造分类的资源定义。底边栏根据该资源提供的分类名称和工具列表渲染可用建造项。
/// </summary>
public partial class ConstructionCategoryDefinition : Resource
{
    /// <summary>用于保存和代码识别的稳定分类 ID。</summary>
    [Export] public string Id { get; set; } = string.Empty;
    /// <summary>显示在 UI 中的分类名称。</summary>
    [Export] public string DisplayName { get; set; } = string.Empty;
    [Export] public int SortOrder { get; set; }
    [Export(PropertyHint.MultilineText)] public string Description { get; set; } = string.Empty;
    [Export] public Array<ConstructionToolDefinition>? Tools { get; set; } = [];

    /// <summary>验证资源在运行时动态建栏时所需的最小数据是否完整。</summary>
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
