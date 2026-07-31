using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// 统一管理玩家可配置的键盘动作，并将绑定持久化到 user://input_bindings.cfg。
/// </summary>
public partial class InputBindingManager : Node
{
    public sealed record BindingDefinition(
        string ActionName,
        string DisplayName,
        string GroupName,
        Key DefaultKey,
        ToolType? Tool = null);

    public const string CameraMoveUpAction = "KeyBoard_MoveUp";
    public const string CameraMoveLeftAction = "KeyBoard_MoveLeft";
    public const string CameraMoveDownAction = "KeyBoard_MoveDown";
    public const string CameraMoveRightAction = "KeyBoard_MoveRight";
    public const string ToolSelectAction = "tool_select";
    public const string ToolRoadAction = "tool_road";
    public const string ToolRemoveAction = "tool_remove";
    public const string PauseMenuAction = "pause_menu";

    private const string ConfigPath = "user://input_bindings.cfg";
    private const string ConfigSection = "bindings";

    private static readonly BindingDefinition[] BindingCatalog =
    [
        new(CameraMoveUpAction, "向上移动", "镜头", Key.W),
        new(CameraMoveLeftAction, "向左移动", "镜头", Key.A),
        new(CameraMoveDownAction, "向下移动", "镜头", Key.S),
        new(CameraMoveRightAction, "向右移动", "镜头", Key.D),
        new(ToolSelectAction, "选择工具", "工具", Key.Q, ToolType.Select),
        new(ToolRoadAction, "铺路工具", "工具", Key.R, ToolType.Road),
        new(ToolRemoveAction, "拆路工具", "工具", Key.E, ToolType.RoadRemove),
        new(PauseMenuAction, "暂停菜单", "系统", Key.Escape),
    ];

    private static readonly IReadOnlyDictionary<string, BindingDefinition> DefinitionsByAction =
        BindingCatalog.ToDictionary(definition => definition.ActionName, StringComparer.Ordinal);

    public static InputBindingManager Instance { get; private set; } = null!;
    public static IReadOnlyList<BindingDefinition> Definitions => BindingCatalog;

    public event Action<string>? BindingChanged;

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
        InitializeBindings();
    }

    public override void _ExitTree()
    {
        if (ReferenceEquals(Instance, this))
            Instance = null!;
    }

    /// <summary>返回动作当前绑定的物理按键；动作不存在时返回 Key.None。</summary>
    public Key GetBoundKey(string actionName)
    {
        if (!DefinitionsByAction.ContainsKey(actionName) || !InputMap.HasAction(actionName))
            return Key.None;

        foreach (InputEvent inputEvent in InputMap.ActionGetEvents(actionName))
        {
            if (inputEvent is InputEventKey keyEvent)
                return NormalizeKey(keyEvent);
        }

        return Key.None;
    }

    public string GetBindingText(string actionName)
    {
        Key key = GetBoundKey(actionName);
        return key == Key.None ? "未绑定" : OS.GetKeycodeString(key);
    }

    public string GetDisplayName(string actionName)
    {
        return DefinitionsByAction.TryGetValue(actionName, out BindingDefinition? definition)
            ? definition.DisplayName
            : actionName;
    }

    /// <summary>用一次真实键盘事件匹配当前动作，供 HUD 和暂停菜单统一消费输入。</summary>
    public bool EventMatchesAction(InputEvent inputEvent, string actionName)
    {
        if (inputEvent is InputEventAction actionEvent)
            return actionEvent.Pressed && actionEvent.Action == actionName;

        if (inputEvent is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
            return false;
        if (keyEvent.CtrlPressed || keyEvent.AltPressed || keyEvent.ShiftPressed || keyEvent.MetaPressed)
            return false;

        return NormalizeKey(keyEvent) == GetBoundKey(actionName);
    }

    public bool TryGetToolForEvent(InputEvent inputEvent, out ToolType tool)
    {
        foreach (BindingDefinition definition in BindingCatalog)
        {
            if (definition.Tool is not ToolType candidate || !EventMatchesAction(inputEvent, definition.ActionName))
                continue;

            tool = candidate;
            return true;
        }

        tool = default;
        return false;
    }

    public static bool TryGetToolAction(ToolType tool, out string actionName)
    {
        foreach (BindingDefinition definition in BindingCatalog)
        {
            if (definition.Tool != tool)
                continue;

            actionName = definition.ActionName;
            return true;
        }

        actionName = string.Empty;
        return false;
    }

    public bool TryRebind(string actionName, Key key, out string error)
    {
        if (!DefinitionsByAction.TryGetValue(actionName, out BindingDefinition? definition))
        {
            error = $"未知输入动作：{actionName}";
            return false;
        }

        if (!IsBindableKey(key))
        {
            error = "该按键不能用于绑定";
            return false;
        }

        foreach (BindingDefinition candidate in BindingCatalog)
        {
            if (candidate.ActionName == actionName || GetBoundKey(candidate.ActionName) != key)
                continue;

            error = $"{OS.GetKeycodeString(key)} 已绑定到“{candidate.DisplayName}”";
            return false;
        }

        Key previousKey = GetBoundKey(actionName);
        if (previousKey == key)
        {
            error = string.Empty;
            return true;
        }

        ApplyBinding(definition, key);
        if (!SaveBindings(out error))
        {
            ApplyBinding(definition, previousKey);
            return false;
        }

        BindingChanged?.Invoke(actionName);
        return true;
    }

    public bool ResetToDefaults(out string error)
    {
        var previousKeys = BindingCatalog.ToDictionary(
            definition => definition.ActionName,
            definition => GetBoundKey(definition.ActionName),
            StringComparer.Ordinal);

        foreach (BindingDefinition definition in BindingCatalog)
            ApplyBinding(definition, definition.DefaultKey);

        if (!SaveBindings(out error))
        {
            foreach (BindingDefinition definition in BindingCatalog)
                ApplyBinding(definition, previousKeys[definition.ActionName]);
            return false;
        }

        foreach (BindingDefinition definition in BindingCatalog)
            BindingChanged?.Invoke(definition.ActionName);
        return true;
    }

    public static Key NormalizeKey(InputEventKey keyEvent)
    {
        return keyEvent.PhysicalKeycode != Key.None ? keyEvent.PhysicalKeycode : keyEvent.Keycode;
    }

    public static bool IsBindableKey(Key key)
    {
        return Enum.IsDefined(key) && key is not (Key.None or Key.Shift or Key.Ctrl or Key.Alt or Key.Meta);
    }

    private void InitializeBindings()
    {
        foreach (BindingDefinition definition in BindingCatalog)
        {
            if (!InputMap.HasAction(definition.ActionName))
                InputMap.AddAction(definition.ActionName);
            ApplyBinding(definition, definition.DefaultKey);
        }

        LoadBindings();
    }

    private static void ApplyBinding(BindingDefinition definition, Key key)
    {
        InputMap.ActionEraseEvents(definition.ActionName);
        if (key == Key.None)
            return;

        InputMap.ActionAddEvent(definition.ActionName, new InputEventKey
        {
            PhysicalKeycode = key,
        });
    }

    private void LoadBindings()
    {
        var config = new ConfigFile();
        Error loadResult = config.Load(ConfigPath);
        if (loadResult == Error.FileNotFound)
            return;
        if (loadResult != Error.Ok)
        {
            GD.PushWarning($"InputBindingManager: failed to load bindings ({loadResult}); defaults remain active.");
            return;
        }

        var candidateKeys = new Dictionary<string, Key>(StringComparer.Ordinal);
        foreach (BindingDefinition definition in BindingCatalog)
        {
            Key key = definition.DefaultKey;
            if (config.HasSectionKey(ConfigSection, definition.ActionName))
                key = (Key)config.GetValue(ConfigSection, definition.ActionName).AsInt64();
            candidateKeys[definition.ActionName] = key;
        }

        bool invalid = candidateKeys.Values.Any(key => !IsBindableKey(key));
        bool duplicated = candidateKeys.Values.Distinct().Count() != candidateKeys.Count;
        if (invalid || duplicated)
        {
            GD.PushWarning("InputBindingManager: saved bindings are invalid or duplicated; defaults remain active.");
            return;
        }

        foreach (BindingDefinition definition in BindingCatalog)
            ApplyBinding(definition, candidateKeys[definition.ActionName]);
    }

    private bool SaveBindings(out string error)
    {
        var config = new ConfigFile();
        foreach (BindingDefinition definition in BindingCatalog)
            config.SetValue(ConfigSection, definition.ActionName, (long)GetBoundKey(definition.ActionName));

        Error saveResult = config.Save(ConfigPath);
        if (saveResult == Error.Ok)
        {
            error = string.Empty;
            return true;
        }

        error = $"按键设置保存失败（{saveResult}）";
        GD.PushWarning($"InputBindingManager: failed to save bindings ({saveResult}).");
        return false;
    }
}
