using Godot;
using System;
using ImGuiNET;
using static Godot.GD;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Numerics;


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

    [DebugGUI("TimeInfo", Opening = false)]
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

public static class DebugBackground
{
    [DebugGUI("Background", Opening = false)]
    public static void BackgroundPanel()
    {
        var bg = Background.Instance;
        if (bg == null)
        {
            ImGui.Text("Background node not found in scene.");
            return;
        }

        bool showBg = bg.ShowBackground;
        if (ImGui.Checkbox("Show Background", ref showBg))
            bg.ShowBackground = showBg;

        ImGui.Separator();

        bool showGrid = bg.ShowGrid;
        if (ImGui.Checkbox("Show Grid", ref showGrid))
            bg.ShowGrid = showGrid;

        bool showMain = bg.ShowMainGrid;
        if (ImGui.Checkbox("Show Main Grid", ref showMain))
            bg.ShowMainGrid = showMain;

        bool showMinor = bg.ShowMinorGrid;
        if (ImGui.Checkbox("Show Minor Grid", ref showMinor))
            bg.ShowMinorGrid = showMinor;

        bool showDot = bg.ShowDotGrid;
        if (ImGui.Checkbox("Show Dot Grid", ref showDot))
            bg.ShowDotGrid = showDot;

        ImGui.Text("Major");
        ImGui.SameLine();
        float majorGridSize = bg.MajorGridSize;
        ImGui.SetNextItemWidth(80);
        if (ImGui.DragFloat("##MajorGridSize", ref majorGridSize, 1f, 1f, 1000f))
            bg.MajorGridSize = majorGridSize;
        ImGui.SameLine();
        ImGui.Text("Size");
        ImGui.SameLine();
        float majorLineWidth = bg.MainLineWidth;
        ImGui.SetNextItemWidth(80);
        if (ImGui.DragFloat("##MajorLineWidth", ref majorLineWidth, 0.1f, 0.1f, 10f))
            bg.MainLineWidth = majorLineWidth;
        ImGui.SameLine();
        ImGui.Text("Width");

        ImGui.Text("Minor");
        ImGui.SameLine();
        float minorGridSize = bg.MinorGridSize;
        ImGui.SetNextItemWidth(80);
        if (ImGui.DragFloat("##MinorGridSize", ref minorGridSize, 1f, 1f, 1000f))
            bg.MinorGridSize = minorGridSize;
        ImGui.SameLine();
        ImGui.Text("Size");
        ImGui.SameLine();
        float minorLineWidth = bg.LineWidth;
        ImGui.SetNextItemWidth(80);
        if (ImGui.DragFloat("##MinorLineWidth", ref minorLineWidth, 0.1f, 0.1f, 10f))
            bg.LineWidth = minorLineWidth;
        ImGui.SameLine();
        ImGui.Text("Width");

        ImGui.Text("Dot");
        ImGui.SameLine();
        float dotGridSize = bg.DotGridSize;
        ImGui.SetNextItemWidth(80);
        if (ImGui.DragFloat("##DotGridSize", ref dotGridSize, 1f, 1f, 1000f))
            bg.DotGridSize = dotGridSize;
        ImGui.SameLine();
        ImGui.Text("Size");
        ImGui.SameLine();
        float dotRadius = bg.DotRadius;
        ImGui.SetNextItemWidth(80);
        if (ImGui.DragFloat("##DotRadius", ref dotRadius, 0.1f, 0.1f, 10f))
            bg.DotRadius = dotRadius;
        ImGui.SameLine();
        ImGui.Text("Radius");
    }
}