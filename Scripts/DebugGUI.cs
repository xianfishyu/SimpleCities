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

    /// <summary>
    /// 内部方法：仅供 DebugGUIInitializer 使用来注册调试渲染函数
    /// </summary>
    internal static void InternalRegisterDebugRender(string name, Action renderFunc, bool opening = false)
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
    /// <summary>
    /// 初始化并注册所有带有 DebugGUIAttribute 特性的静态方法
    /// 并在编译时验证所有使用该特性的方法都是 public static
    /// </summary>
    public static void InitializeDebugRenders()
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        List<MethodInfo> allAttributedMethods = [.. assemblies.SelectMany(assembly => assembly.GetTypes())
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
            .Where(m => m.GetCustomAttribute<DebugGUIAttribute>() != null)];

        // 验证所有使用 DebugGUIAttribute 的方法都是 public static
        var invalidMethods = allAttributedMethods
            .Where(m => !m.IsPublic || !m.IsStatic)
            .ToList();

        if (invalidMethods.Count > 0)
        {
            var errorMsg = "Error: DebugGUI methods must be public static!\n";
            foreach (var method in invalidMethods)
            {
                var attr = method.GetCustomAttribute<DebugGUIAttribute>();
                string methodModifiers = $"{(method.IsPublic ? "public" : "private")} {(method.IsStatic ? "static" : "instance")}";
                errorMsg += $"  - {method.DeclaringType?.FullName}.{method.Name}() is '{methodModifiers}' but should be 'public static'\n";
            }
            throw new InvalidOperationException(errorMsg);
        }

        // 注册所有有效的方法
        foreach (var method in allAttributedMethods.Where(m => m.IsPublic && m.IsStatic))
        {
            var attribute = method.GetCustomAttribute<DebugGUIAttribute>();
            var action = (Action)Delegate.CreateDelegate(typeof(Action), method);
            DebugGUI.InternalRegisterDebugRender(attribute.Name, action, attribute.Opening);
        }
    }
}

public static partial class DebugInfo
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
        ImGui.Text($"Date: {GameTimeManager.CurrentDateString}");
        ImGui.Text($"Time: {GameTimeManager.CurrentTimeString}");

        ImGui.Separator();

        float timeScale = GameTimeManager.TimeScale;
        ImGui.SetNextItemWidth(150);
        if (ImGui.DragFloat("Time Scale", ref timeScale, 1f, 10f, 2f))
        {
            GameTimeManager.TimeScale = timeScale;
        }


        if (ImGui.Button(GameTimeManager.IsPaused ? "Resume" : "Pause"))
            GameTimeManager.IsPaused = !GameTimeManager.IsPaused;

        ImGui.SameLine();
        if (ImGui.Button("x1"))
            GameTimeManager.TimeScale = 1f;
        ImGui.SameLine();
        if (ImGui.Button("x10"))
            GameTimeManager.TimeScale = 10f;

        ImGui.SameLine();
        if (ImGui.Button("x100"))
            GameTimeManager.TimeScale = 100f;
        ImGui.SameLine();
        if (ImGui.Button("x1000"))
            GameTimeManager.TimeScale = 1000f;

        ImGui.Separator();
        if (ImGui.Button("Reset Time"))
            GameTimeManager.ResetGameTime();
        ImGui.SameLine();
        if (ImGui.Button("Time Set to Now"))
            GameTimeManager.SetGameTimeToNow();
    }

    private static int setYear = 2025;
    private static int setMonth = 8;
    private static int setDay = 10;
    private static int setHour = 11;
    private static int setMinute = 45;
    private static int setSecond = 14;

    [DebugGUI("TimeInfo", Opening = false)]
    public static void SetTimeGUI()
    {
        if (ImGui.CollapsingHeader("TimeSet"))
        {


            ImGui.Text("Set Game Time:");

            // 日期设置
            ImGui.BeginGroup();
            {
                ImGui.Text("Date:");
                ImGui.SetNextItemWidth(100);
                ImGui.InputInt("Year", ref setYear);
                setYear = Math.Clamp(setYear, 1900, 2100);

                ImGui.SetNextItemWidth(100);
                ImGui.InputInt("Month", ref setMonth);
                setMonth = Math.Clamp(setMonth, 1, 12);

                ImGui.SetNextItemWidth(100);
                ImGui.InputInt("Day", ref setDay);
                int maxDay = DateTime.DaysInMonth(setYear, setMonth);
                setDay = Math.Clamp(setDay, 1, maxDay);
            }
            ImGui.EndGroup();

            ImGui.SameLine(200);

            // 时间设置
            ImGui.BeginGroup();
            {
                ImGui.Text("Time:");
                ImGui.SetNextItemWidth(100);
                ImGui.InputInt("Hour", ref setHour);
                setHour = Math.Clamp(setHour, 0, 23);

                ImGui.SetNextItemWidth(100);
                ImGui.InputInt("Minute", ref setMinute);
                setMinute = Math.Clamp(setMinute, 0, 59);

                ImGui.SetNextItemWidth(100);
                ImGui.InputInt("Second", ref setSecond);
                setSecond = Math.Clamp(setSecond, 0, 59);
            }
            ImGui.EndGroup();

            ImGui.Spacing();
            ImGui.Separator();

            // 预览设置的时间
            try
            {
                var previewTime = new DateTime(setYear, setMonth, setDay, setHour, setMinute, setSecond);
                ImGui.Text($"Preview: {previewTime:yyyy-MM-dd HH:mm:ss}");
            }
            catch
            {
                ImGui.TextColored(new System.Numerics.Vector4(1, 0, 0, 1), "Invalid date/time!");
            }

            ImGui.Spacing();

            // 应用按钮
            if (ImGui.Button("Apply Time"))
            {
                try
                {
                    var newTime = new DateTime(setYear, setMonth, setDay, setHour, setMinute, setSecond);
                    GameTimeManager.SetGameTime(newTime);
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"Failed to set time: {ex.Message}");
                }
            }
        }
    }

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