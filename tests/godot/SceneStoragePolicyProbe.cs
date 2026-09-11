using Godot;
using System;
using System.IO;
using System.Linq;

/// <summary>跨保存根删除授权回归的测试装配，仅编译进Debug。</summary>
public partial class SceneStoragePolicyProbe : RefCounted
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), $"simple-cities-root-auth-{Guid.NewGuid():N}");
    private SaveSlotSummary? _captured;
    public string SlotID => "manual-shared";
    public string RootA => Path.Combine(_root, "a");
    public string RootB => Path.Combine(_root, "b");

    public void CreateMatchingSlots(RoadSystem roads)
    {
        new SaveSlotStore(RootA).Save(SlotID, "Root authorization fixture", [roads.Graph]);
        string source = Path.Combine(RootA, SlotID);
        string destination = Path.Combine(RootB, SlotID);
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
    }

    public bool BindRoot(SaveManager manager, RoadSystem roads, ToolManager tools, RoadRenderer renderer, string root)
    {
        manager.UnregisterSceneLoad(tools);
        return manager.RegisterSceneLoad(new SceneLoadParticipants(
            roads.Graph, tools, renderer, new SceneStoragePolicy(root, [roads.Graph.SaveFileName])));
    }

    public string CaptureAndAuthorize(SaveManager manager)
    {
        _captured = manager.ListSlots().Single(slot => slot.SlotID == SlotID);
        return manager.ArmDeletion(_captured);
    }

    public string RearmCapturedSummary(SaveManager manager) =>
        manager.ArmDeletion(_captured ?? throw new InvalidOperationException("No slot summary was captured."));

    public bool BothSlotsExist() => Directory.Exists(Path.Combine(RootA, SlotID)) &&
        Directory.Exists(Path.Combine(RootB, SlotID));

    public void Cleanup(SaveManager manager, RoadSystem roads, ToolManager tools, RoadRenderer renderer)
    {
        manager.UnregisterSceneLoad(tools);
        if (!manager.RegisterSceneLoad(new SceneLoadParticipants(roads.Graph, tools, renderer, V3RoadStorage.Policy)))
            throw new InvalidOperationException("Could not restore the default scene storage.");
        string directory = Path.GetFullPath(_root);
        string prefix = Path.Combine(Path.GetFullPath(Path.GetTempPath()), "simple-cities-root-auth-");
        if (!directory.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Unexpected temporary storage root.");
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }
}
