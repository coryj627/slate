// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Automation.Provider;

namespace SlateWindows;

/// <summary>
/// Exports an already dispatcher-wrapped WPF range through UIA's Link VARIANT.
/// See contract A-6: the controlling IUnknown needs the range ABI and free-threaded
/// marshaling. Text operations still belong to WPF and AvalonEdit.
/// </summary>
internal static unsafe class UiaLinkRangeExport
{
    private struct State
    {
        public void** Vtable;
        public int References;
        public void* Inner;
        public void* Marshaler;
    }

    // Native VARIANT is 24 bytes on x64/ARM64, 16 on x86. The scalar overlay
    // occupies 16 bytes; the BRECORD pair starts after the eight-byte header.
    [StructLayout(LayoutKind.Sequential)]
    private struct RecordValue { public nint Record; public nint RecordInfo; }
    [StructLayout(LayoutKind.Explicit)]
    private struct Variant
    {
        [FieldOffset(0)] public long Header;
        [FieldOffset(8)] public long Payload;
        [FieldOffset(8)] public RecordValue Record;
    }

    private static readonly Guid UnknownId = new("00000000-0000-0000-C000-000000000046");
    private static readonly Guid RangeId = typeof(ITextRangeProvider).GUID;
    private static readonly Guid MarshalId = new("00000003-0000-0000-C000-000000000046");
    private static readonly Guid ExportId = new("6db58f7e-a350-47c5-b286-1b9af192a612");
    private static readonly void** Table = CreateVtable();
    private static int _liveExports;
    internal static int LiveExportsForCensus => Volatile.Read(ref _liveExports);

    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern int CoCreateFreeThreadedMarshaler(nint outer, out nint marshaler);

    internal static object Create(ITextRangeProvider dispatcherWrappedRange)
    {
        var state = (State*)NativeMemory.AllocZeroed((nuint)sizeof(State));
        state->Vtable = Table;
        state->References = 1;
        Interlocked.Increment(ref _liveExports);
        try
        {
            state->Inner = (void*)Marshal.GetComInterfaceForObject(dispatcherWrappedRange, typeof(ITextRangeProvider));
            Marshal.ThrowExceptionForHR(CoCreateFreeThreadedMarshaler((nint)state, out nint marshaler));
            state->Marshaler = (void*)marshaler;
            // The RCW owns its own reference. Neither the peer nor a range cache
            // retains the export; releasing the last COM reference frees it.
            return Marshal.GetObjectForIUnknown((nint)state);
        }
        finally { ReleaseCore(state); }
    }

    internal static ITextRangeProvider Unwrap(ITextRangeProvider range)
    {
        if (!Marshal.IsComObject(range)) { return range; }
        nint unknown = Marshal.GetIUnknownForObject(range);
        nint export = 0;
        try
        {
            Guid id = ExportId;
            if (Marshal.QueryInterface(unknown, in id, out export) != 0) { return range; }
            return (ITextRangeProvider)Marshal.GetObjectForIUnknown((nint)((State*)export)->Inner);
        }
        finally
        {
            if (export != 0) { Marshal.Release(export); }
            Marshal.Release(unknown);
        }
    }

    private static void** InnerVtable(State* state) => *(void***)state->Inner;

    private static void* UnwrapOperand(void* range)
    {
        if (range == null) { return null; }
        Guid id = ExportId;
        void* export = null;
        int result = ((delegate* unmanaged[Stdcall]<void*, Guid*, void**, int>)(*(void***)range)[0])(range, &id, &export);
        if (result != 0) { return range; }
        // The caller retains the operand throughout this call; its inner
        // reference remains alive after releasing our temporary identity query.
        void* inner = ((State*)export)->Inner;
        Marshal.Release((nint)export);
        return inner;
    }

    // IUnknown followed by the 18 SDK ITextRangeProvider slots, in ABI order.
    private static void** CreateVtable()
    {
        var table = (void**)RuntimeHelpers.AllocateTypeAssociatedMemory(typeof(UiaLinkRangeExport), 21 * sizeof(void*));
        table[0] = (delegate* unmanaged[Stdcall]<State*, Guid*, void**, int>)&Query;
        table[1] = (delegate* unmanaged[Stdcall]<State*, uint>)&AddRef;
        table[2] = (delegate* unmanaged[Stdcall]<State*, uint>)&Release;
        table[3] = (delegate* unmanaged[Stdcall]<State*, void**, int>)&Clone;
        table[4] = (delegate* unmanaged[Stdcall]<State*, void*, int*, int>)&Compare;
        table[5] = (delegate* unmanaged[Stdcall]<State*, int, void*, int, int*, int>)&CompareEndpoints;
        table[6] = (delegate* unmanaged[Stdcall]<State*, int, int>)&Expand;
        table[7] = (delegate* unmanaged[Stdcall]<State*, int, Variant, int, void**, int>)&FindAttribute;
        table[8] = (delegate* unmanaged[Stdcall]<State*, void*, int, int, void**, int>)&FindText;
        table[9] = (delegate* unmanaged[Stdcall]<State*, int, Variant*, int>)&GetAttribute;
        table[10] = (delegate* unmanaged[Stdcall]<State*, void**, int>)&GetBounds;
        table[11] = (delegate* unmanaged[Stdcall]<State*, void**, int>)&GetEnclosing;
        table[12] = (delegate* unmanaged[Stdcall]<State*, int, void**, int>)&GetText;
        table[13] = (delegate* unmanaged[Stdcall]<State*, int, int, int*, int>)&Move;
        table[14] = (delegate* unmanaged[Stdcall]<State*, int, int, int, int*, int>)&MoveByUnit;
        table[15] = (delegate* unmanaged[Stdcall]<State*, int, void*, int, int>)&MoveByRange;
        table[16] = (delegate* unmanaged[Stdcall]<State*, int>)&Select;
        table[17] = (delegate* unmanaged[Stdcall]<State*, int>)&AddSelection;
        table[18] = (delegate* unmanaged[Stdcall]<State*, int>)&RemoveSelection;
        table[19] = (delegate* unmanaged[Stdcall]<State*, int, int>)&Scroll;
        table[20] = (delegate* unmanaged[Stdcall]<State*, void**, int>)&GetChildren;
        return table;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int Query(State* state, Guid* id, void** result)
    {
        if (result == null) { return unchecked((int)0x80004003); }
        *result = null;
        if (id == null) { return unchecked((int)0x80004003); }
        if (*id == MarshalId && state->Marshaler != null)
        {
            return ((delegate* unmanaged[Stdcall]<void*, Guid*, void**, int>)(*(void***)state->Marshaler)[0])(state->Marshaler, id, result);
        }
        if (*id != UnknownId && *id != RangeId && *id != ExportId) { return unchecked((int)0x80004002); }
        *result = state;
        Interlocked.Increment(ref state->References);
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint AddRef(State* state) => (uint)Interlocked.Increment(ref state->References);
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint Release(State* state) => ReleaseCore(state);
    private static uint ReleaseCore(State* state)
    {
        int remaining = Interlocked.Decrement(ref state->References);
        if (remaining == 0)
        {
            if (state->Marshaler != null) { Marshal.Release((nint)state->Marshaler); }
            if (state->Inner != null) { Marshal.Release((nint)state->Inner); }
            NativeMemory.Free(state);
            Interlocked.Decrement(ref _liveExports);
        }
        return (uint)remaining;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int Clone(State* s, void** value) => ((delegate* unmanaged[Stdcall]<void*, void**, int>)InnerVtable(s)[3])(s->Inner, value);
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int Compare(State* s, void* range, int* value) => ((delegate* unmanaged[Stdcall]<void*, void*, int*, int>)InnerVtable(s)[4])(s->Inner, UnwrapOperand(range), value);
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int CompareEndpoints(State* s, int endpoint, void* range, int target, int* value) => ((delegate* unmanaged[Stdcall]<void*, int, void*, int, int*, int>)InnerVtable(s)[5])(s->Inner, endpoint, UnwrapOperand(range), target, value);
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int Expand(State* s, int unit) => ((delegate* unmanaged[Stdcall]<void*, int, int>)InnerVtable(s)[6])(s->Inner, unit);
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int FindAttribute(State* s, int attribute, Variant value, int backward, void** result) => ((delegate* unmanaged[Stdcall]<void*, int, Variant, int, void**, int>)InnerVtable(s)[7])(s->Inner, attribute, value, backward, result);
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int FindText(State* s, void* text, int backward, int ignoreCase, void** result) => ((delegate* unmanaged[Stdcall]<void*, void*, int, int, void**, int>)InnerVtable(s)[8])(s->Inner, text, backward, ignoreCase, result);
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int GetAttribute(State* s, int attribute, Variant* result) => ((delegate* unmanaged[Stdcall]<void*, int, Variant*, int>)InnerVtable(s)[9])(s->Inner, attribute, result);
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int GetBounds(State* s, void** result) => ((delegate* unmanaged[Stdcall]<void*, void**, int>)InnerVtable(s)[10])(s->Inner, result);
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int GetEnclosing(State* s, void** result) => ((delegate* unmanaged[Stdcall]<void*, void**, int>)InnerVtable(s)[11])(s->Inner, result);
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int GetText(State* s, int maxLength, void** result) => ((delegate* unmanaged[Stdcall]<void*, int, void**, int>)InnerVtable(s)[12])(s->Inner, maxLength, result);
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int Move(State* s, int unit, int count, int* result) => ((delegate* unmanaged[Stdcall]<void*, int, int, int*, int>)InnerVtable(s)[13])(s->Inner, unit, count, result);
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int MoveByUnit(State* s, int endpoint, int unit, int count, int* result) => ((delegate* unmanaged[Stdcall]<void*, int, int, int, int*, int>)InnerVtable(s)[14])(s->Inner, endpoint, unit, count, result);
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int MoveByRange(State* s, int endpoint, void* range, int target) => ((delegate* unmanaged[Stdcall]<void*, int, void*, int, int>)InnerVtable(s)[15])(s->Inner, endpoint, UnwrapOperand(range), target);
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int Select(State* s) => ((delegate* unmanaged[Stdcall]<void*, int>)InnerVtable(s)[16])(s->Inner);
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int AddSelection(State* s) => ((delegate* unmanaged[Stdcall]<void*, int>)InnerVtable(s)[17])(s->Inner);
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int RemoveSelection(State* s) => ((delegate* unmanaged[Stdcall]<void*, int>)InnerVtable(s)[18])(s->Inner);
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int Scroll(State* s, int alignToTop) => ((delegate* unmanaged[Stdcall]<void*, int, int>)InnerVtable(s)[19])(s->Inner, alignToTop);
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int GetChildren(State* s, void** result) => ((delegate* unmanaged[Stdcall]<void*, void**, int>)InnerVtable(s)[20])(s->Inner, result);
}
