namespace SimpleCities.Core.V3;

/// <summary>
/// V3 根中 canonical direct child 的分类。
/// </summary>
public enum V3SlotOccupant
{
    Absent,
    CompleteV3,
    CorruptV3,
    Foreign,
    Unsafe,
}

public static class V3SlotClassifier
{
    public static V3SlotOccupant Classify(
        bool isDirectory,
        bool manifestDeclaresV3,
        bool manifestValid,
        bool payloadsValid)
    {
        if (!isDirectory)
            return V3SlotOccupant.Unsafe;
        if (!manifestDeclaresV3)
            return V3SlotOccupant.Foreign;
        if (!manifestValid || !payloadsValid)
            return V3SlotOccupant.CorruptV3;
        return V3SlotOccupant.CompleteV3;
    }
}
