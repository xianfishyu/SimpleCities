using Godot;
using System;
using ImGuiNET;
using static Godot.GD;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;


public partial class DebugGUI : Node
{
    public static DebugGUI Instance { get; private set; }
    public static bool IsVisible = true;
    private static Dictionary<string, (Action action, bool opening)> DebugGUIRender = [];

    public override void _Ready()
    {
        if (Instance != null)
        {
            QueueFree();
            return;
        }
        Instance = this;

        DebugGUIInitializer.InitializeDebugRenders();
    }

    public override void _Process(double delta)
    {
        if (IsVisible)
        {
            ImGui.Begin("Debug Window");
            foreach (var render in DebugGUIRender)
            {
                ImGui.SetNextItemOpen(render.Value.opening, ImGuiCond.FirstUseEver);
                if (ImGui.CollapsingHeader(render.Key))
                {
                    render.Value.action.Invoke();
                }
            }
            ImGui.End();
        }
        else
            return;
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey eventKey && eventKey.Pressed && !eventKey.Echo)
        {
            if (eventKey.Keycode == Key.Tab)
            {
                IsVisible = !IsVisible;
            }
        }
    }


    public static void RegisterDebugRender(string name, Action renderFunc, bool opening = false)
    {
        if (!DebugGUIRender.ContainsKey(name))
            DebugGUIRender.TryAdd(name, (renderFunc, opening));
        else
            DebugGUIRender[name] = (DebugGUIRender[name].action + renderFunc, DebugGUIRender[name].opening);
    }
}

public class DebugGUIAttribute(string name, bool opening = false) : Attribute
{
    public string Name { get; set; } = name;
    public bool Opening { get; set; } = opening;
}

public static class DebugGUIInitializer
{
    //初始化并注册所有带有DebugGUIAttribute特性的静态方法
    public static void InitializeDebugRenders()
    {
        //获取当前程序集中的所有方法
        var assembly = Assembly.GetExecutingAssembly();
        var methods = assembly.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Where(m => m.GetCustomAttribute<DebugGUIAttribute>() != null);

        //注册所有带有DebugGUIAttribute特性的静态方法
        foreach (var method in methods)
        {
            //获取属性和方法委托
            var attribute = method.GetCustomAttribute<DebugGUIAttribute>();
            var action = (Action)Delegate.CreateDelegate(typeof(Action), method);

            //注册到DebugGUI
            DebugGUI.RegisterDebugRender(attribute.Name, action, attribute.Opening);
        }
    }
}

public static class DebugInfo
{
    [DebugGUI("GeneralInfo")]
    public static void GeneralInfo()
    {
        ImGui.Text($"FPS: {Engine.GetFramesPerSecond()}");
        ImGui.Text($"Memory Usage: {OS.GetStaticMemoryUsage() / (1024 * 1024)} MB");
    }

    [DebugGUI("TimeInfo", Opening = true)]
    public static void GameTimeDebug()
    {
        ImGui.Text($"Date: {GameTime.GetFormattedDate()}");
        ImGui.Text($"Time: {GameTime.GetFormattedTime()}");

        ImGui.Separator();

        float timeScale = GameTime.GetTimeScale();
        if (ImGui.SliderFloat("Time Scale", ref timeScale, 0.1f, 10000f, "%.2f", ImGuiSliderFlags.Logarithmic))
        {
            GameTime.Instance.SetTimeScale(timeScale);
        }


        if (ImGui.Button(GameTime.IsPaused ? "Resume" : "Pause"))
            GameTime.TogglePause();

        ImGui.SameLine();
        if (ImGui.Button("x1"))
            GameTime.Instance.SetTimeScale(1f);

        ImGui.SameLine();
        if (ImGui.Button("x10"))
            GameTime.Instance.SetTimeScale(10f);

        ImGui.SameLine();
        if (ImGui.Button("x100"))
            GameTime.Instance.SetTimeScale(100f);

        ImGui.SameLine();
        if (ImGui.Button("x1000"))
            GameTime.Instance.SetTimeScale(1000f);

        ImGui.Separator();
        if (ImGui.Button("Reset Time"))
        {
            GameTime.Instance.ResetTime();
        }
    }
}