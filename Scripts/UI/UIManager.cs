using Godot;
using System.Collections.Generic;

/// <summary>
/// UI 面板生命周期管理器 — 每个 GameHUD 拥有自己的实例，负责该 HUD 内真实受管面板的注册 / 显示 / 隐藏 / 模态栈。
/// GameHUD 只向自己的 manager 注册需要外部可见性管理的组件，例如 ContextPanel、DebugPanel、SystemControls。
/// ConstructionDock 始终可见，不通过 UIManager 管理。
/// </summary>
public partial class UIManager : Node
{
    private readonly Dictionary<string, Control> _panels = new();
    private readonly Stack<string> _modalStack = new();

    /// <summary>是否有模态面板正在阻塞游戏输入。</summary>
    public bool IsModalActive => _modalStack.Count > 0;

    public override void _ExitTree()
    {
        _panels.Clear();
        _modalStack.Clear();
    }

    // ── 注册 ──────────────────────────────────────────────

    /// <summary>
    /// 注册一个 UI 面板到管理器。
    /// 面板在 _Ready 中调用此方法注册自己。
    /// </summary>
    public void Register(string name, Control panel)
    {
        _panels[name] = panel;
    }

    /// <summary>注销面板。</summary>
    public void Unregister(string name)
    {
        _panels.Remove(name);
    }

    // ── 可见性控制 ────────────────────────────────────────

    /// <summary>显示指定面板。</summary>
    public void Show(string name)
    {
        if (_panels.TryGetValue(name, out var panel))
            panel.Visible = true;
    }

    /// <summary>隐藏指定面板。</summary>
    public void Hide(string name)
    {
        if (_panels.TryGetValue(name, out var panel))
            panel.Visible = false;
    }

    /// <summary>切换面板可见性。</summary>
    public void Toggle(string name)
    {
        if (_panels.TryGetValue(name, out var panel))
            panel.Visible = !panel.Visible;
    }

    /// <summary>查询面板是否可见。</summary>
    public bool IsVisible(string name)
    {
        return _panels.TryGetValue(name, out var panel) && panel.Visible;
    }

    /// <summary>隐藏所有已注册面板。</summary>
    public void HideAll()
    {
        foreach (var (_, panel) in _panels)
            panel.Visible = false;
    }

    // ── 模态面板 ──────────────────────────────────────────

    /// <summary>
    /// 推入一个模态面板——显示该面板并阻塞游戏输入（例如设置弹窗、离线报告）。
    /// </summary>
    public void PushModal(string name)
    {
        if (_panels.TryGetValue(name, out var panel))
        {
            panel.Visible = true;
            _modalStack.Push(name);
        }
    }

    /// <summary>关闭最顶层的模态面板。</summary>
    public void PopModal()
    {
        if (_modalStack.Count == 0) return;
        var name = _modalStack.Pop();
        if (_panels.TryGetValue(name, out var panel))
            panel.Visible = false;
    }

    // ── 查询 ──────────────────────────────────────────────

    /// <summary>获取已注册面板（用于外部直接操作）。</summary>
    public T? GetPanel<T>(string name) where T : Control
    {
        return _panels.TryGetValue(name, out var panel) ? panel as T : null;
    }

    public Control? GetPanel(string name)
    {
        return _panels.TryGetValue(name, out var panel) ? panel : null;
    }
}
