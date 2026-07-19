// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Hand-written safe wrapper over the csbindgen-generated raw externs
// (generated/NativeMethods.g.cs). Under the uniffi candidate every line
// of this file is generated; here it is application code the Windows
// host would own — string marshalling, handle lifetime (SafeHandle +
// per-call guards), callback trampolines with GCHandle contexts,
// exception fencing inside [UnmanagedCallersOnly] bodies, and the
// listener-context free discipline (delayed to dodge the
// unregister/in-flight-dispatch race).

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using SlateShim;

namespace SlateShimProbe;

// ---------------------------------------------------------------------
// Errors
// ---------------------------------------------------------------------

internal static class Codes
{
    public const int Ok = 0;
    public const int Io = 1;
    public const int InvalidPath = 3;
    public const int Cancelled = 5;
    public const int InvalidUtf8 = 6;
    public const int WriteConflict = 12;
    public const int CommandUnknownId = 100;
    public const int CommandActionFailed = 101;
    public const int Panic = -2;
}

internal class ShimException : Exception
{
    public int Code { get; }

    public ShimException(int code, string message) : base(message) => Code = code;
}

internal sealed class ShimWriteConflictException : ShimException
{
    public string CurrentContentHash { get; }
    public long CurrentMtimeMs { get; }

    public ShimWriteConflictException(string message, string currentHash, long mtimeMs)
        : base(Codes.WriteConflict, message)
    {
        CurrentContentHash = currentHash;
        CurrentMtimeMs = mtimeMs;
    }
}

// ---------------------------------------------------------------------
// Marshalling helpers
// ---------------------------------------------------------------------

internal static unsafe class Ffi
{
    /// <summary>Pinned UTF-8 bytes for an in-param string.</summary>
    internal readonly struct Utf8 : IDisposable
    {
        private readonly GCHandle _pin;
        public readonly nuint Len;

        public Utf8(string s)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(s);
            _pin = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            Len = (nuint)bytes.Length;
        }

        public byte* Ptr => Len == 0 ? null : (byte*)_pin.AddrOfPinnedObject();

        public void Dispose() => _pin.Free();
    }

    /// <summary>Copy a Rust-owned buffer into a string and free it.</summary>
    public static string TakeString(SlateBuf buf)
    {
        if (buf.ptr == null || buf.len == 0)
        {
            return string.Empty;
        }
        string s = Encoding.UTF8.GetString(buf.ptr, checked((int)buf.len));
        NativeMethods.slate_buf_free(buf);
        return s;
    }

    /// <summary>Throw the mapped exception when a call reported failure.</summary>
    public static void ThrowIfError(int code, ref SlateError err)
    {
        if (code == Codes.Ok)
        {
            return;
        }
        string message = TakeString(err.message);
        err.message = default;
        throw new ShimException(code, message);
    }
}

// ---------------------------------------------------------------------
// SafeHandle base + per-call guard
// ---------------------------------------------------------------------

internal abstract unsafe class NativeHandle : SafeHandle
{
    protected NativeHandle(IntPtr ptr) : base(IntPtr.Zero, ownsHandle: true) => SetHandle(ptr);

    public override bool IsInvalid => handle == IntPtr.Zero;

    /// <summary>
    /// Ref-counted access for the duration of one native call: keeps the
    /// handle alive against a concurrent Dispose (releases run when the
    /// last guard drops). Throws ObjectDisposedException after Dispose —
    /// the use-after-dispose behavior uniffi's counter gives generated.
    /// </summary>
    public Guard Use() => new(this);

    internal readonly struct Guard : IDisposable
    {
        private readonly NativeHandle _h;
        private readonly bool _added;

        public Guard(NativeHandle h)
        {
            _h = h;
            bool added = false;
            h.DangerousAddRef(ref added);
            _added = added;
        }

        public void* Ptr => (void*)_h.handle;

        public void Dispose()
        {
            if (_added)
            {
                _h.DangerousRelease();
            }
        }
    }
}

internal sealed unsafe class VaultHandle : NativeHandle
{
    public VaultHandle(IntPtr ptr) : base(ptr) { }

    protected override bool ReleaseHandle()
    {
        NativeMethods.slate_vault_close((ShimVaultSession*)handle);
        return true;
    }
}

internal sealed unsafe class TokenHandle : NativeHandle
{
    public TokenHandle(IntPtr ptr) : base(ptr) { }

    protected override bool ReleaseHandle()
    {
        NativeMethods.slate_cancel_free((SlateShim.ShimCancelToken*)handle);
        return true;
    }
}

internal sealed unsafe class DocHandle : NativeHandle
{
    public DocHandle(IntPtr ptr) : base(ptr) { }

    protected override bool ReleaseHandle()
    {
        NativeMethods.slate_doc_free((SlateShim.ShimDocBuffer*)handle);
        return true;
    }
}

internal sealed unsafe class RegistryHandle : NativeHandle
{
    public RegistryHandle(IntPtr ptr) : base(ptr) { }

    protected override bool ReleaseHandle()
    {
        NativeMethods.slate_registry_free((ShimCommandRegistry*)handle);
        return true;
    }
}

// ---------------------------------------------------------------------
// Delayed GCHandle free: an unregistered listener's context handle may
// still be touched by an in-flight dispatch on a Rust thread, so frees
// go through a grace queue instead of happening inline. uniffi's
// generated handle map owns this race internally.
// ---------------------------------------------------------------------

internal static class ContextReaper
{
    private static readonly ConcurrentQueue<(GCHandle Handle, long DueTicks)> Queue = new();
    public static TimeSpan Grace = TimeSpan.FromMilliseconds(500);

    public static void Retire(GCHandle handle) =>
        Queue.Enqueue((handle, Stopwatch.GetTimestamp() + (long)(Grace.TotalSeconds * Stopwatch.Frequency)));

    public static void Sweep()
    {
        long now = Stopwatch.GetTimestamp();
        int n = Queue.Count;
        for (int i = 0; i < n; i++)
        {
            if (!Queue.TryDequeue(out var item))
            {
                break;
            }
            if (item.DueTicks <= now)
            {
                item.Handle.Free();
            }
            else
            {
                Queue.Enqueue(item);
            }
        }
    }
}

// ---------------------------------------------------------------------
// Recorders (mirror the uniffi probe's semantics)
// ---------------------------------------------------------------------

internal sealed class ShimScanRecorder
{
    private readonly object _lock = new();
    private readonly List<(uint Tag, string? Text, ulong A, ulong B)> _events = new();
    public readonly HashSet<int> ThreadIds = new();
    public Action<uint>? OnEvent;

    public void Add(uint tag, string? text, ulong a, ulong b)
    {
        lock (_lock)
        {
            _events.Add((tag, text, a, b));
            ThreadIds.Add(Environment.CurrentManagedThreadId);
        }
        OnEvent?.Invoke(tag);
    }

    public List<(uint Tag, string? Text, ulong A, ulong B)> Snapshot()
    {
        lock (_lock) return new(_events);
    }
}

internal sealed class ShimEventsRecorder
{
    private readonly object _lock = new();
    public readonly List<(uint Code, string Path, string Message)> Errors = new();
    public readonly List<(uint Kind, string Path, string? Previous)> FileChanges = new();
    public readonly List<(uint Phase, ulong FilesSeen)> IndexPhases = new();
    public readonly HashSet<int> ThreadIds = new();

    public void AddError(uint code, string path, string message)
    {
        lock (_lock)
        {
            Errors.Add((code, path, message));
            ThreadIds.Add(Environment.CurrentManagedThreadId);
        }
    }

    public void AddFileChange(uint kind, string path, string? previous)
    {
        lock (_lock)
        {
            FileChanges.Add((kind, path, previous));
            ThreadIds.Add(Environment.CurrentManagedThreadId);
        }
    }

    public void AddIndexPhase(uint phase, ulong filesSeen)
    {
        lock (_lock)
        {
            IndexPhases.Add((phase, filesSeen));
            ThreadIds.Add(Environment.CurrentManagedThreadId);
        }
    }

    public int TotalCount
    {
        get { lock (_lock) return Errors.Count + FileChanges.Count + IndexPhases.Count; }
    }

    public T Locked<T>(Func<ShimEventsRecorder, T> read)
    {
        lock (_lock) return read(this);
    }
}

// ---------------------------------------------------------------------
// [UnmanagedCallersOnly] trampolines. Every body is exception-fenced:
// a C# exception unwinding into a Rust frame is UB, and nothing
// generated stands between us and that mistake (uniffi emits this
// guard in its foreign-callback vtable shims).
// ---------------------------------------------------------------------

internal static unsafe class Trampolines
{
    public static readonly ConcurrentQueue<string> Faults = new();

    private static T? Target<T>(void* ctx) where T : class =>
        GCHandle.FromIntPtr((IntPtr)ctx).Target as T;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static void ScanProgress(void* ctx, uint tag, byte* str, nuint len, ulong a, ulong b)
    {
        try
        {
            string? text = str == null ? null : Encoding.UTF8.GetString(str, checked((int)len));
            Target<ShimScanRecorder>(ctx)?.Add(tag, text, a, b);
        }
        catch (Exception ex)
        {
            Faults.Enqueue($"scan-progress: {ex.GetType().Name}");
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static void VaultError(void* ctx, uint code, byte* path, nuint pathLen, byte* msg, nuint msgLen)
    {
        try
        {
            Target<ShimEventsRecorder>(ctx)?.AddError(
                code,
                Encoding.UTF8.GetString(path, checked((int)pathLen)),
                Encoding.UTF8.GetString(msg, checked((int)msgLen)));
        }
        catch (Exception ex)
        {
            Faults.Enqueue($"vault-error: {ex.GetType().Name}");
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static void FileChange(void* ctx, uint kind, byte* path, nuint pathLen, byte* prev, nuint prevLen)
    {
        try
        {
            Target<ShimEventsRecorder>(ctx)?.AddFileChange(
                kind,
                Encoding.UTF8.GetString(path, checked((int)pathLen)),
                prev == null ? null : Encoding.UTF8.GetString(prev, checked((int)prevLen)));
        }
        catch (Exception ex)
        {
            Faults.Enqueue($"file-change: {ex.GetType().Name}");
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static void IndexPhase(void* ctx, uint phase, ulong filesSeen)
    {
        try
        {
            Target<ShimEventsRecorder>(ctx)?.AddIndexPhase(phase, filesSeen);
        }
        catch (Exception ex)
        {
            Faults.Enqueue($"index-phase: {ex.GetType().Name}");
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static int CommandInvoke(void* ctx, byte* msgOut, nuint msgCap, nuint* msgLen)
    {
        try
        {
            var action = Target<ShimActionBox>(ctx);
            if (action == null)
            {
                return 1;
            }
            var (ok, message) = action.Body();
            if (ok)
            {
                return 0;
            }
            byte[] bytes = Encoding.UTF8.GetBytes(message ?? "action failed");
            int n = Math.Min(bytes.Length, checked((int)msgCap));
            new ReadOnlySpan<byte>(bytes, 0, n).CopyTo(new Span<byte>(msgOut, n));
            *msgLen = (nuint)n;
            return 1;
        }
        catch (Exception ex)
        {
            // The typed escape hatch: an untyped exception cannot cross;
            // it degrades to a generic failure message.
            Faults.Enqueue($"command-invoke: {ex.GetType().Name}");
            byte[] bytes = Encoding.UTF8.GetBytes($"unhandled {ex.GetType().Name}");
            int n = Math.Min(bytes.Length, checked((int)msgCap));
            new ReadOnlySpan<byte>(bytes, 0, n).CopyTo(new Span<byte>(msgOut, n));
            *msgLen = (nuint)n;
            return 1;
        }
    }

    public static List<string> TakeFaults()
    {
        var list = new List<string>();
        while (Faults.TryDequeue(out var f))
        {
            list.Add(f);
        }
        return list;
    }
}

/// <summary>Holder for a registered command action's managed body.</summary>
internal sealed class ShimActionBox
{
    public readonly Func<(bool Ok, string? Message)> Body;
    public int InvocationCount;

    public ShimActionBox(Func<(bool Ok, string? Message)> body)
    {
        Func<(bool, string?)> counted = () =>
        {
            Interlocked.Increment(ref InvocationCount);
            return body();
        };
        Body = counted;
    }
}

// ---------------------------------------------------------------------
// Object wrappers
// ---------------------------------------------------------------------

internal sealed unsafe class ShimCancelToken : IDisposable
{
    internal readonly TokenHandle Handle;

    public ShimCancelToken() => Handle = new TokenHandle((IntPtr)NativeMethods.slate_cancel_new());

    public void Cancel()
    {
        using var g = Handle.Use();
        NativeMethods.slate_cancel_cancel((SlateShim.ShimCancelToken*)g.Ptr);
    }

    public bool IsCancelled()
    {
        using var g = Handle.Use();
        return NativeMethods.slate_cancel_is_cancelled((SlateShim.ShimCancelToken*)g.Ptr) != 0;
    }

    public void Dispose() => Handle.Dispose();
}

internal sealed class EventSubscription : IDisposable
{
    private readonly ShimVault _vault;
    private readonly ulong _token;
    private GCHandle _ctx;
    private bool _disposed;

    internal EventSubscription(ShimVault vault, ulong token, GCHandle ctx)
    {
        _vault = vault;
        _token = token;
        _ctx = ctx;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _vault.UnregisterEvents(_token);
        ContextReaper.Retire(_ctx);
        ContextReaper.Sweep();
    }
}

internal sealed unsafe class ShimVault : IDisposable
{
    private readonly VaultHandle _handle;

    private ShimVault(VaultHandle handle) => _handle = handle;

    public static ShimVault Open(string root)
    {
        using var rootArg = new Ffi.Utf8(root);
        ShimVaultSession* session = null;
        SlateError err = default;
        int code = NativeMethods.slate_vault_open(rootArg.Ptr, rootArg.Len, &session, &err);
        Ffi.ThrowIfError(code, ref err);
        return new ShimVault(new VaultHandle((IntPtr)session));
    }

    public (ulong Seen, ulong Indexed) ScanWithProgress(ShimCancelToken token, ShimScanRecorder recorder)
    {
        var ctx = GCHandle.Alloc(recorder);
        try
        {
            using var g = _handle.Use();
            using var t = token.Handle.Use();
            ulong seen = 0, indexed = 0;
            SlateError err = default;
            int code = NativeMethods.slate_vault_scan_with_progress(
                (ShimVaultSession*)g.Ptr,
                (SlateShim.ShimCancelToken*)t.Ptr,
                &Trampolines.ScanProgress,
                (void*)GCHandle.ToIntPtr(ctx),
                &seen,
                &indexed,
                &err);
            Ffi.ThrowIfError(code, ref err);
            return (seen, indexed);
        }
        finally
        {
            // The scan call is synchronous — no dispatch survives return.
            ctx.Free();
        }
    }

    public EventSubscription RegisterEvents(ShimEventsRecorder recorder)
    {
        var ctx = GCHandle.Alloc(recorder);
        using var g = _handle.Use();
        ulong token = NativeMethods.slate_vault_register_events(
            (ShimVaultSession*)g.Ptr,
            &Trampolines.VaultError,
            &Trampolines.FileChange,
            &Trampolines.IndexPhase,
            (void*)GCHandle.ToIntPtr(ctx));
        return new EventSubscription(this, token, ctx);
    }

    internal void UnregisterEvents(ulong token)
    {
        using var g = _handle.Use();
        NativeMethods.slate_vault_unregister_events((ShimVaultSession*)g.Ptr, token);
    }

    public (string NewHash, long NewMtimeMs) SaveText(string path, string contents, string? expectedHash)
    {
        using var pathArg = new Ffi.Utf8(path);
        using var contentsArg = new Ffi.Utf8(contents);
        using var hashArg = new Ffi.Utf8(expectedHash ?? string.Empty);
        using var g = _handle.Use();
        SlateBuf newHash = default, wcHash = default;
        long newMtime = 0, wcMtime = 0;
        SlateError err = default;
        int code = NativeMethods.slate_vault_save_text(
            (ShimVaultSession*)g.Ptr,
            pathArg.Ptr, pathArg.Len,
            contentsArg.Ptr, contentsArg.Len,
            hashArg.Ptr, hashArg.Len,
            &newHash, &newMtime, &wcHash, &wcMtime, &err);
        if (code == Codes.WriteConflict)
        {
            string message = Ffi.TakeString(err.message);
            throw new ShimWriteConflictException(message, Ffi.TakeString(wcHash), wcMtime);
        }
        Ffi.ThrowIfError(code, ref err);
        return (Ffi.TakeString(newHash), newMtime);
    }

    public string ReadText(string path)
    {
        using var pathArg = new Ffi.Utf8(path);
        using var g = _handle.Use();
        SlateBuf text = default;
        SlateError err = default;
        int code = NativeMethods.slate_vault_read_text(
            (ShimVaultSession*)g.Ptr, pathArg.Ptr, pathArg.Len, &text, &err);
        Ffi.ThrowIfError(code, ref err);
        return Ffi.TakeString(text);
    }

    public void Dispose() => _handle.Dispose();
}

internal sealed unsafe class ShimDocBuffer : IDisposable
{
    private readonly DocHandle _handle;

    public ShimDocBuffer(string text)
    {
        using var textArg = new Ffi.Utf8(text);
        var ptr = NativeMethods.slate_doc_new(textArg.Ptr, textArg.Len);
        if (ptr == null)
        {
            throw new ShimException(Codes.Panic, "slate_doc_new returned null");
        }
        _handle = new DocHandle((IntPtr)ptr);
    }

    public void ApplyEdit(uint startUtf16, uint oldLenUtf16, string newText)
    {
        using var textArg = new Ffi.Utf8(newText);
        using var g = _handle.Use();
        int code = NativeMethods.slate_doc_apply_edit(
            (SlateShim.ShimDocBuffer*)g.Ptr, startUtf16, oldLenUtf16, textArg.Ptr, textArg.Len);
        if (code != Codes.Ok)
        {
            throw new ShimException(code, "apply_edit failed");
        }
    }

    public void Reset(string text)
    {
        using var textArg = new Ffi.Utf8(text);
        using var g = _handle.Use();
        int code = NativeMethods.slate_doc_reset((SlateShim.ShimDocBuffer*)g.Ptr, textArg.Ptr, textArg.Len);
        if (code != Codes.Ok)
        {
            throw new ShimException(code, "reset failed");
        }
    }

    public uint LenUtf16()
    {
        using var g = _handle.Use();
        return NativeMethods.slate_doc_len_utf16((SlateShim.ShimDocBuffer*)g.Ptr);
    }

    public uint ByteToUtf16(uint byteOffset)
    {
        using var g = _handle.Use();
        return NativeMethods.slate_doc_byte_to_utf16((SlateShim.ShimDocBuffer*)g.Ptr, byteOffset);
    }

    public (uint AppliedStart, uint AppliedEnd, (uint Start, uint End, uint Kind, uint Arg)[] Spans)
        Highlight(uint dirtyStartUtf16, uint dirtyEndUtf16)
    {
        using var g = _handle.Use();
        uint appliedStart = 0, appliedEnd = 0;
        SlateSpan* spans = null;
        nuint count = 0;
        int code = NativeMethods.slate_doc_highlight(
            (SlateShim.ShimDocBuffer*)g.Ptr, dirtyStartUtf16, dirtyEndUtf16,
            &appliedStart, &appliedEnd, &spans, &count);
        if (code != Codes.Ok)
        {
            throw new ShimException(code, "highlight failed");
        }
        var result = new (uint, uint, uint, uint)[checked((int)count)];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = (spans[i].start_byte, spans[i].end_byte, spans[i].kind, spans[i].arg);
        }
        NativeMethods.slate_spans_free(spans, count);
        return (appliedStart, appliedEnd, result);
    }

    public void Dispose() => _handle.Dispose();
}

internal sealed unsafe class ShimRegistry : IDisposable
{
    private readonly RegistryHandle _handle;
    private readonly Dictionary<string, GCHandle> _actions = new();
    private readonly object _lock = new();

    public ShimRegistry() => _handle = new RegistryHandle((IntPtr)NativeMethods.slate_registry_new());

    /// <summary>Returns true when the registration replaced a live id.</summary>
    public bool Register(string id, string label, ShimActionBox action)
    {
        var ctx = GCHandle.Alloc(action);
        using var idArg = new Ffi.Utf8(id);
        using var labelArg = new Ffi.Utf8(label);
        using var g = _handle.Use();
        int result = NativeMethods.slate_registry_register(
            (ShimCommandRegistry*)g.Ptr,
            idArg.Ptr, idArg.Len,
            labelArg.Ptr, labelArg.Len,
            0 /* File */,
            &Trampolines.CommandInvoke,
            (void*)GCHandle.ToIntPtr(ctx));
        if (result < 0)
        {
            ctx.Free();
            throw new ShimException(result, "register failed");
        }
        lock (_lock)
        {
            // Replace semantics: the previous action's context handle is
            // orphaned Rust-side the moment register returns — retire it.
            if (_actions.TryGetValue(id, out var old))
            {
                ContextReaper.Retire(old);
            }
            _actions[id] = ctx;
        }
        return result == 1;
    }

    public bool Unregister(string id)
    {
        using var idArg = new Ffi.Utf8(id);
        bool removed;
        using (var g = _handle.Use())
        {
            removed = NativeMethods.slate_registry_unregister(
                (ShimCommandRegistry*)g.Ptr, idArg.Ptr, idArg.Len) == 1;
        }
        lock (_lock)
        {
            if (removed && _actions.Remove(id, out var ctx))
            {
                ContextReaper.Retire(ctx);
            }
        }
        return removed;
    }

    public int Count()
    {
        using var g = _handle.Use();
        return checked((int)NativeMethods.slate_registry_count((ShimCommandRegistry*)g.Ptr));
    }

    public void Invoke(string id)
    {
        using var idArg = new Ffi.Utf8(id);
        using var g = _handle.Use();
        SlateError err = default;
        int code = NativeMethods.slate_registry_invoke(
            (ShimCommandRegistry*)g.Ptr, idArg.Ptr, idArg.Len, &err);
        Ffi.ThrowIfError(code, ref err);
    }

    public void Dispose()
    {
        _handle.Dispose();
        lock (_lock)
        {
            foreach (var ctx in _actions.Values)
            {
                ContextReaper.Retire(ctx);
            }
            _actions.Clear();
        }
    }
}
