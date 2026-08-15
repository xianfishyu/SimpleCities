namespace SimpleCities.Road.V3;

/// <summary>
/// 一次性不可变快照：保存 root 引用与当时 token。
/// 后续 facade 编辑构造新 root，旧快照不受影响。
/// </summary>
public sealed record RoadGraphV3Snapshot(
    RoadGraphV3Revision Revision,
    GraphStateToken Token);
