// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;

namespace SlateWindows;

internal sealed class FileSizeLimitExceededException : IOException
{
    public FileSizeLimitExceededException(long observedBytes, long maximumBytes)
        : base($"File data exceeds the {maximumBytes}-byte safety limit.")
    {
        ObservedBytes = observedBytes;
        MaximumBytes = maximumBytes;
    }

    public long ObservedBytes { get; }
    public long MaximumBytes { get; }
}

internal static class SafeFile
{
    private const int ReadChunkBytes = 64 * 1024;

    public static byte[] ReadAllBytesBounded(
        string path,
        long maximumBytes,
        FileShare fileShare = FileShare.Read,
        CancellationToken cancellationToken = default)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            fileShare,
            ReadChunkBytes,
            FileOptions.SequentialScan);
        return ReadAllBytesBounded(stream, maximumBytes, cancellationToken);
    }

    internal static byte[] ReadAllBytesBounded(
        Stream stream,
        long maximumBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead || !stream.CanSeek)
        {
            throw new NotSupportedException("Bounded reads require a readable, seekable stream.");
        }

        if (maximumBytes < 0 || maximumBytes > Array.MaxLength)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        }

        cancellationToken.ThrowIfCancellationRequested();
        long remaining = stream.Length - stream.Position;
        if (remaining < 0)
        {
            throw new IOException("The stream position is beyond its reported length.");
        }

        if (remaining > maximumBytes)
        {
            throw new FileSizeLimitExceededException(remaining, maximumBytes);
        }

        byte[] output = GC.AllocateUninitializedArray<byte>((int)remaining);
        int offset = 0;
        int maximum = (int)maximumBytes;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (offset == output.Length)
            {
                int next = stream.ReadByte();
                if (next < 0)
                {
                    return output;
                }

                if (offset >= maximum)
                {
                    throw new FileSizeLimitExceededException((long)offset + 1, maximumBytes);
                }

                int expandedLength = Math.Min(
                    maximum,
                    Math.Max(offset + 1, (int)Math.Min((long)maximum, Math.Max(ReadChunkBytes, (long)offset * 2))));
                Array.Resize(ref output, expandedLength);
                output[offset++] = (byte)next;
                continue;
            }

            int read = stream.Read(
                output.AsSpan(offset, Math.Min(ReadChunkBytes, output.Length - offset)));
            if (read == 0)
            {
                Array.Resize(ref output, offset);
                return output;
            }

            offset += read;
        }
    }

    public static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}
