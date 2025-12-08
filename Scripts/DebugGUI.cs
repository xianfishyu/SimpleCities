using Godot;
using System;
using ImGuiNET;
using static Godot.GD;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;


public partial class DebugGUI : Node
{
    private static Dictionary<string, Action> DebugGUIRender = [];

    public override void _Ready()
    {
        DebugGUIInitializer.InitializeDebugRenders();
    }

    public override void _Process(double delta)
    {
        ImGui.Begin("Debug Window");
        foreach (var render in DebugGUIRender)
        {
            if (ImGui.CollapsingHeader(render.Key))
            {
                render.Value.Invoke();
            }
        }
        ImGui.End();
    }

    public static void RegisterDebugRender(string name, Action renderFunc)
    {
        if (!DebugGUIRender.ContainsKey(name))
            DebugGUIRender.TryAdd(name, renderFunc);
        else
            DebugGUIRender[name] += renderFunc;
    }
}

public class DebugGUIAttribute(string name) : Attribute
{
    public string Name { get; set; } = name;
}

public static class DebugGUIInitializer
{
    public static void InitializeDebugRenders()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var methods = assembly.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Where(m => m.GetCustomAttribute<DebugGUIAttribute>() != null);

        foreach (var method in methods)
        {
            var attribute = method.GetCustomAttribute<DebugGUIAttribute>();
            var action = (Action)Delegate.CreateDelegate(typeof(Action), method);
            DebugGUI.RegisterDebugRender(attribute.Name, action);
        }
    }
}

public static class DebugInfo
{
    [DebugGUI("RenderInfo")]
    public static void RenderInfo() => ImGui.Text($"FPS: {Engine.GetFramesPerSecond()}");

    [DebugGUI("MemoryInfo")]
    public static void MemoryInfo() => ImGui.Text($"Memory Usage: {OS.GetStaticMemoryUsage() / (1024 * 1024)} MB");
}