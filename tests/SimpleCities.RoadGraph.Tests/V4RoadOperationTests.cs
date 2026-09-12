public sealed class V4RoadOperationTests
{
    [Fact]
    public void AcceptedCancellationRejectsLateCommitAndKeepsSingleWriterUntilCleanup()
    {
        var operation = new V4RoadOperation();
        Assert.True(operation.TryBegin());
        Assert.True(operation.TryCancel());
        Assert.Equal("Cancelling", operation.Phase);
        Assert.False(operation.TryBegin());
        bool published = false;
        Assert.False(operation.TryCommit(() => published = true));
        Assert.False(published);
        Assert.True(operation.IsBusy);
        operation.Finish();
        Assert.False(operation.IsBusy);
        Assert.True(operation.TryBegin());
    }

    [Fact]
    public void CommitWinningBeforeCancellationRemainsBusyUntilFirstDrawAndCannotBeUndoneByEsc()
    {
        var operation = new V4RoadOperation();
        Assert.True(operation.TryBegin());
        Assert.False(operation.RecordFirstDraw());
        Assert.Equal(-1, operation.DrawnElapsedMilliseconds);
        Assert.True(operation.TryCommit(() => true));
        Assert.False(operation.TryCancel());
        Assert.False(operation.TryBegin());
        Assert.Equal("Committed", operation.Phase);
        Assert.True(operation.RecordFirstDraw());
        Assert.Equal("Drawn", operation.Phase);
        Assert.False(operation.IsBusy);
        Assert.True(operation.DrawnElapsedMilliseconds >= 0);
        double firstDraw = operation.DrawnElapsedMilliseconds;
        Assert.False(operation.RecordFirstDraw());
        Assert.Equal(firstDraw, operation.DrawnElapsedMilliseconds);
        Assert.False(operation.TryCancel());
    }

    [Fact]
    public void RejectedCommitDoesNotClaimTheRoadWasPublishedOrDrawn()
    {
        var operation = new V4RoadOperation();
        Assert.True(operation.TryBegin());
        Assert.False(operation.TryCommit(() => false));
        Assert.True(operation.CanPublish);
        Assert.False(operation.RecordFirstDraw());
        Assert.True(operation.TryCancel());
        operation.Finish();
        Assert.Equal("Cancelled", operation.Phase);
        Assert.Equal(-1, operation.DrawnElapsedMilliseconds);
    }
}
