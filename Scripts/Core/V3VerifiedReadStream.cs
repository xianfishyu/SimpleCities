using System;
using System.IO;
using System.Security.Cryptography;

internal sealed class V3VerifiedReadStream : Stream
{
    private readonly FileStream _source;
    private readonly IStorageOperationLease? _operationLease;
    private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private bool _finished;
    private bool _disposed;

    internal V3VerifiedReadStream(
        FileStream source,
        IStorageOperationLease? operationLease = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.CanRead || !source.CanSeek)
            throw new ArgumentException("Verified payload source must be a readable file handle.", nameof(source));
        if (source.Position != 0)
            throw new ArgumentException("Verified payload source must start at byte zero.", nameof(source));
        _source = source;
        _operationLease = operationLease;
        InitialLength = source.Length;
    }

    internal long InitialLength { get; }
    internal long BytesRead { get; private set; }
    internal bool ReachedEof { get; private set; }

    public override bool CanRead => !_disposed;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => BytesRead;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _operationLease?.ThrowIfCancellationRequested();
        int read = _source.Read(buffer, offset, count);
        Record(buffer.AsSpan(offset, read), read);
        return read;
    }

    public override int Read(Span<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _operationLease?.ThrowIfCancellationRequested();
        int read = _source.Read(buffer);
        Record(buffer[..read], read);
        return read;
    }

    internal void VerifyComplete(V3ManifestFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_finished)
            throw new InvalidOperationException("Payload verification has already finished.");
        _finished = true;

        if (!ReachedEof)
        {
            Span<byte> probe = stackalloc byte[1];
            if (Read(probe) != 0)
                throw new InvalidDataException($"Payload '{file.Name}' was not consumed to EOF.");
        }
        if (InitialLength != file.EncodedLength ||
            _source.Length != InitialLength ||
            BytesRead != InitialLength)
        {
            throw new InvalidDataException($"Payload '{file.Name}' length does not match its declared and consumed lengths.");
        }
        string actualHash = Convert.ToHexString(_hash.GetHashAndReset()).ToLowerInvariant();
        if (!string.Equals(actualHash, file.Sha256, StringComparison.Ordinal))
            throw new InvalidDataException($"Payload '{file.Name}' SHA-256 does not match manifest.");
    }

    private void Record(ReadOnlySpan<byte> bytes, int read)
    {
        if (read == 0)
        {
            ReachedEof = true;
            return;
        }
        if (ReachedEof)
            throw new IOException("Payload source returned bytes after EOF.");
        BytesRead = checked(BytesRead + read);
        _hash.AppendData(bytes);
    }

    protected override void Dispose(bool disposing)
    {
        if (_disposed)
            return;
        _disposed = true;
        if (disposing)
            _hash.Dispose();
        base.Dispose(disposing);
    }

    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
