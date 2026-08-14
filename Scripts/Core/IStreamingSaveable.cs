using System.IO;

/// <summary>由保存参与者捕获的不可变状态。</summary>
public interface ISaveSnapshot;

/// <summary>已完整解析并验证、可以提交到活动会话的状态。</summary>
public interface IPreparedSaveState;

/// <summary>
/// V3 保存参与者。捕获只取得不可变快照；编码和准备加载直接使用字节流。
/// </summary>
public interface ISaveFileParticipant
{
    string SaveFileName { get; }
}

public interface IStreamingSaveParticipant : ISaveFileParticipant
{
    ISaveSnapshot CaptureSnapshot();
    void WriteSnapshot(Stream destination, ISaveSnapshot snapshot);
}

public interface IStreamingLoadReader
{
    IPreparedSaveState PrepareLoad(Stream source);
}

public interface IStreamingLoadTarget : ISaveFileParticipant
{
    IStreamingLoadReader CaptureLoadReader();
    void CommitPreparedLoad(IPreparedSaveState preparedState);
}

public interface IStreamingSaveable : IStreamingSaveParticipant, IStreamingLoadTarget;
