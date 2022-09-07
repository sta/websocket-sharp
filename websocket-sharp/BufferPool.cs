using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace WebSocketSharp;

public static class BufferPool
{
    private static readonly ConcurrentDictionary<int, ConcurrentBag<byte[]>> _pool = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] Rent(int size)
    {
        if (_pool.TryGetValue(size, out var bag))
        {
            return bag.TryTake(out var result) ? result : new byte[size];
        }

        _pool.TryAdd(size, new ConcurrentBag<byte[]>());
        return new byte[size];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Return(byte[] bytes)
    {
        Array.Clear(bytes);
        _pool[bytes.Length].Add(bytes);
    }
}