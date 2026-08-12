namespace PortfolioCalc.App.Tests;

/// <summary>Wraps a stream to behave like Blazor's InputFile.OpenReadStream() over the
/// browser/WebView file bridge: forward-only (CanSeek = false) and, critically,
/// synchronous Read() throws "Synchronous reads are not supported." — only ReadAsync
/// works. File.OpenRead(), which the other import tests use, is fully seekable and
/// supports synchronous reads, so it can't catch a bug that only shows up against this
/// kind of stream.</summary>
public sealed class NonSeekableStream(Stream inner) : Stream
{
    public override bool CanRead => inner.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => inner.Position;
        set => throw new NotSupportedException();
    }

    public override void Flush() => inner.Flush();

    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException("Synchronous reads are not supported.");

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        inner.ReadAsync(buffer, offset, count, cancellationToken);

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        inner.ReadAsync(buffer, cancellationToken);

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            inner.Dispose();
        base.Dispose(disposing);
    }
}
