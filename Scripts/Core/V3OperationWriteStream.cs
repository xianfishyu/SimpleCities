using System;
using System.IO;

internal sealed class V3OperationWriteStream : Stream
{
    private readonly Stream _destination;
    private readonly IStorageOperationLease _operationLease;

    internal V3OperationWriteStream(
        Stream destination,
        IStorageOperationLease operationLease)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(operationLease);
        if (!destination.CanWrite)
            throw new ArgumentException("Operation destination must be writable.", nameof(destination));
        _destination = destination;
        _operationLease = operationLease;
    }

    public override bool CanRead => false;
    public override bool CanSeek => _destination.CanSeek;
    public override bool CanWrite => true;
    public override long Length => _destination.Length;
    public override long Position
    {
        get => _destination.Position;
        set => _destination.Position = value;
    }

    public override void Flush()
    {
        _operationLease.ThrowIfCancellationRequested();
        _destination.Flush();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        _operationLease.ThrowIfCancellationRequested();
        _destination.Write(buffer, offset, count);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        _operationLease.ThrowIfCancellationRequested();
        _destination.Write(buffer);
    }

    public override void WriteByte(byte value)
    {
        _operationLease.ThrowIfCancellationRequested();
        _destination.WriteByte(value);
    }

    public override long Seek(long offset, SeekOrigin origin) => _destination.Seek(offset, origin);
    public override void SetLength(long value) => _destination.SetLength(value);
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        // The owner controls the underlying file handle lifetime.
        base.Dispose(disposing);
    }
}
