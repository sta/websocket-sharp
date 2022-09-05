using System;
using System.Buffers;

namespace WebSocketSharp;

internal struct GrowableMemoryOwner<T> : IDisposable
{
    private IMemoryOwner<T> _buffer;

    public static GrowableMemoryOwner<T> Rent(int initialCountHint) =>
        new GrowableMemoryOwner<T>(initialCountHint);

    private GrowableMemoryOwner(int initialCountHint)
    {
        _buffer = MemoryPool<T>.Shared.Rent(initialCountHint);
    }

    public Memory<T> Memory => _buffer.Memory;

    public Memory<T> Grow()
    {
        var oldBuffer = _buffer;
        // Allocate buffer twice as big
        _buffer = MemoryPool<T>.Shared.Rent(oldBuffer.Memory.Length * 2);
        try
        {
            // Copy content
            oldBuffer.Memory.CopyTo(_buffer.Memory);
        }
        finally
        {
            oldBuffer.Dispose();
        }

        return Memory;
    }

    public void Dispose() => _buffer.Dispose();
}