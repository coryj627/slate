// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Automation.Peers;

namespace SlateWindows.Reading;

/// <summary>
/// A peer that can answer the registered MathML custom UIA property.
/// </summary>
internal interface IMathMlUiaSource
{
    /// <summary>The complete <c>&lt;math&gt;…&lt;/math&gt;</c> markup
    /// for this element, or empty when none.</summary>
    string MathMl { get; }
}

/// <summary>
/// The MathML-over-UIA convention layer (W3-2, gap G23; owner call
/// 2026-07-27 accepting the reflection dependency).
///
/// Registers the custom UIA property Microsoft Word ships and
/// Chromium's `kUiaMathMlSupport` deliberately reuses — GUID
/// <c>{FA170AB3-3229-4E7C-827F-DD05EE0481D9}</c>, programmatic name
/// "MathML", string — and bridges it into WPF. WPF's
/// <c>ElementProxy</c> forwards ANY property id to
/// <see cref="AutomationPeer"/>, which resolves it against a PRIVATE
/// static table of built-in getters; the one missing link is a table
/// entry for the registered id, injected here by reflection. The
/// injection is guarded end to end: any failure disables the
/// convention layer and logs — never crashes — and
/// <see cref="IsActive"/> lets a startup smoke test fail LOUDLY in CI
/// if a dotnet/wpf change ever breaks the shape (the condition the
/// owner call attached to accepting the dependency).
///
/// No AT consumes this from third-party apps today (NVDA's consumer
/// is gated to Word's window class; JAWS documents none) — the
/// consumers are Narrator's visibly-generalizing pipeline and the
/// W-E7 Slate NVDA appModule, which reads this property to unlock
/// NVDA's built-in math speech, braille, and interaction.
/// </summary>
internal static class MathMlUiaProperty
{
    private static readonly Guid PropertyGuid =
        new("FA170AB3-3229-4E7C-827F-DD05EE0481D9");

    private static int _propertyId;
    private static string? _inactiveReason = "not initialized";

    /// <summary>True when the property is registered AND the WPF
    /// bridge is installed; the startup smoke test asserts this.</summary>
    public static bool IsActive => _inactiveReason is null;

    /// <summary>Why the convention layer is off, for the smoke test's
    /// failure message and the host log.</summary>
    public static string? InactiveReason => _inactiveReason;

    /// <summary>The dynamic property id UIA assigned this session
    /// (process-local; the GUID is the cross-process key).</summary>
    internal static int PropertyIdForTests => _propertyId;

    /// <summary>
    /// Idempotent; call once before the first reading surface exists.
    /// Never throws.
    /// </summary>
    public static void Initialize()
    {
        if (_inactiveReason is null)
        {
            return;
        }
        try
        {
            _propertyId = RegisterWithUia();
            InstallPeerBridge(_propertyId);
            _inactiveReason = null;
        }
        catch (Exception exception) when (
            exception is COMException
                or InvalidOperationException
                or MissingMemberException
                or ArgumentException
                or TypeLoadException
                or MemberAccessException)
        {
            _inactiveReason =
                $"{exception.GetType().Name}: {exception.Message}";
            HostLog.Write(
                HostDiagnosticEvent.MathMlUiaPropertyUnavailable, exception);
        }
    }

    private static int RegisterWithUia()
    {
        var registrar = (IUIAutomationRegistrar)new CUIAutomationRegistrar();
        nint name = Marshal.StringToCoTaskMemUni("MathML");
        try
        {
            var info = new UiaPropertyInfo
            {
                Guid = PropertyGuid,
                ProgrammaticName = name,
                Type = 3, // UIAutomationType_String
            };
            registrar.RegisterProperty(ref info, out int id);
            return id;
        }
        finally
        {
            Marshal.FreeCoTaskMem(name);
        }
    }

    /// <summary>
    /// The reflection bridge: AutomationPeer resolves property ids
    /// through a private static Hashtable of
    /// <c>GetProperty(AutomationPeer)</c> delegates. One entry maps
    /// the registered id to a getter that asks the peer for MathML.
    /// Every assumption is checked; a miss throws into the guarded
    /// caller and turns the layer off.
    /// </summary>
    private static void InstallPeerBridge(int propertyId)
    {
        FieldInfo field = typeof(AutomationPeer).GetField(
            "s_propertyInfo", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMemberException(
                "AutomationPeer.s_propertyInfo is gone — dotnet/wpf changed shape.");
        if (field.GetValue(null) is not Hashtable table)
        {
            throw new InvalidOperationException(
                "AutomationPeer.s_propertyInfo is not a Hashtable — dotnet/wpf changed shape.");
        }
        Type delegateType = typeof(AutomationPeer).GetNestedType(
            "GetProperty", BindingFlags.NonPublic)
            ?? throw new MissingMemberException(
                "AutomationPeer.GetProperty delegate is gone — dotnet/wpf changed shape.");
        MethodInfo getter = typeof(MathMlUiaProperty).GetMethod(
            nameof(GetMathMlFromPeer), BindingFlags.NonPublic | BindingFlags.Static)!;
        Delegate bridge = Delegate.CreateDelegate(delegateType, getter);
        // Hashtable is thread-safe for one writer; this runs once on
        // the UI thread before any peer exists.
        table[propertyId] = bridge;
    }

    private static object? GetMathMlFromPeer(AutomationPeer peer) =>
        peer is IMathMlUiaSource source && source.MathMl.Length > 0
            ? source.MathMl
            : null;

    [ComImport]
    [Guid("6E29FABF-9977-42D1-8D0E-CA7E61AD87E6")]
    private class CUIAutomationRegistrar
    {
    }

    [ComImport]
    [Guid("8609C4EC-4A1A-4D88-A357-5A66E060E1CF")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IUIAutomationRegistrar
    {
        void RegisterProperty(ref UiaPropertyInfo property, out int propertyId);
        // Remaining registrar methods are not used; the vtable order
        // above matches uiautomationcore.h (RegisterProperty first).
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct UiaPropertyInfo
    {
        public Guid Guid;
        public nint ProgrammaticName;
        public int Type;
    }
}
