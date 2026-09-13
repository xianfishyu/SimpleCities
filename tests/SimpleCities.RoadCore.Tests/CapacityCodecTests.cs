namespace SimpleCities.RoadCore.Tests;

public sealed class CapacityCodecTests
{
    [Fact]
    public void LegalPayloadBeyondFormerOneMiBLimitLoadsThroughNonSeekableStream()
    {
        using var encoded = new MemoryStream();
        RoadCodec.Write(encoded, new RoadNetwork(new MapDefinition(25)).Snapshot);
        using var source = new PaddedStream(encoded.ToArray(), 1024 * 1024 + 1);
        var restored = new RoadNetwork();
        Assert.True(restored.TryCommit(restored.PlanLoad(RoadCodec.Read(source))));
        Assert.Equal(25, restored.Snapshot.Map.CellSizeMetres);
        Assert.Empty(restored.Snapshot.Edges);
        Assert.Equal(1024 * 1024 + 1, source.BytesRead);
    }

    [Theory]
    [InlineData(33554432, true)]
    [InlineData(33554433, false)]
    public void ThirtyTwoMiBBoundaryIsEnforcedWithoutReadingBeyondTheSentinel(int length, bool accepted)
    {
        using var encoded = new MemoryStream();
        RoadCodec.Write(encoded, new RoadNetwork(new MapDefinition(50)).Snapshot);
        using var source = new PaddedStream(encoded.ToArray(), length);
        var network = new RoadNetwork();
        RoadSnapshot before = network.Snapshot;
        if (accepted)
        {
            Assert.True(network.TryCommit(network.PlanLoad(RoadCodec.Read(source))));
            Assert.Equal(50, network.Snapshot.Map.CellSizeMetres);
        }
        else
        {
            Assert.Throws<InvalidDataException>(() => network.PlanLoad(RoadCodec.Read(source)));
            Assert.Same(before, network.Snapshot);
        }
        Assert.Equal(length, source.BytesRead);
    }

    private sealed class PaddedStream(byte[] prefix, int length) : Stream
    {
        public int BytesRead { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = Math.Min(count, length - BytesRead);
            buffer.AsSpan(offset, read).Fill((byte)' ');
            if (BytesRead < prefix.Length)
                prefix.AsSpan(BytesRead, Math.Min(read, prefix.Length - BytesRead)).CopyTo(buffer.AsSpan(offset));
            BytesRead += read;
            return read;
        }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
