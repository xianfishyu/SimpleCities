/// <summary>
/// 持久化接口 — 各需要存档的子系统实现此接口，
/// 向 SaveManager 注册后自动参与保存/加载流程。
/// </summary>
public interface ISaveable
{
    /// <summary>存档文件名（不含扩展名），如 "road_network"</summary>
    string SaveFileName { get; }

    /// <summary>捕获当前运行时状态，返回纯数据 DTO</summary>
    object CaptureState();

    /// <summary>从 JSON 字符串恢复运行时状态（各实现自行反序列化为对应 DTO）</summary>
    void RestoreState(string json);
}

/// <summary>
/// 可在不修改运行时状态的前提下准备恢复数据的持久化系统。
/// SaveManager 会先准备整槽，再统一提交。
/// </summary>
public interface IPreparedSaveable : ISaveable
{
    object PrepareRestoreState(string json);
    void RestorePreparedState(object preparedState);
}
