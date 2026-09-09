namespace GitlabMCP.GraphQL;

/// <summary>
///     Throws once more than <paramref name="maxBytes" /> have been read, mid-stream. A poisoned or
///     pathological GraphQL response must not be allowed to fill the model's context window silently
///     (mcp-untrusted-content §6). Wrapping the stream, rather than checking <c>Content-Length</c> up front,
///     also catches a response that lied about its length or was chunked without one.
/// </summary>
internal sealed class BoundedReadStream(Stream inner, long maxBytes) : Stream
{
    private long _read;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var n = await inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        _read += n;
        if (_read > maxBytes)
            throw new GitLabGraphQlServerException(
                $"gitlab_graphql response exceeded {maxBytes} bytes; refusing to buffer further.");
        return n;
    }

    public override void Flush()
    {
        inner.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException("Async only.");
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) inner.Dispose();
        base.Dispose(disposing);
    }
}