using Godot;

/// <summary>
/// 当前城市结束后的最小入口页。它不保存状态，只允许重新进入测试地图或退出到桌面。
/// </summary>
public partial class MainMenu : Control
{
    private const string MapScenePath = "res://Scenes/MapTest.tscn";

    private Button _startButton = null!;
    private Button _quitButton = null!;

    public override void _Ready()
    {
        _startButton = GetNode<Button>("Center/MainPanel/Content/StartButton");
        _quitButton = GetNode<Button>("Center/MainPanel/Content/QuitButton");
        _startButton.Pressed += StartGame;
        _quitButton.Pressed += QuitToDesktop;
        _startButton.GrabFocus();
    }

    public override void _ExitTree()
    {
        if (_startButton != null)
            _startButton.Pressed -= StartGame;
        if (_quitButton != null)
            _quitButton.Pressed -= QuitToDesktop;
    }

    private void StartGame()
    {
        Error result = GetTree().ChangeSceneToFile(MapScenePath);
        if (result != Error.Ok)
            GD.PushError($"MainMenu: failed to start MapTest ({result}).");
    }

    private void QuitToDesktop() => GetTree().Quit();
}
