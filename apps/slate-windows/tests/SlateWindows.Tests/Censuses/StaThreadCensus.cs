// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SlateWindows.Tests.Censuses;

/// <summary>
/// The unit project's hosted facts cannot leak WPF window classes: every
/// STA thread a fact starts goes through <see cref="StaThread"/>, whose
/// teardown leaves nothing registered, and the dispatcher registry the
/// assembly-wide <see cref="LeakedDispatcherGuardAttribute"/> reads is
/// where it looks.
/// </summary>
/// <remarks>
/// A per-class copy of the old STA runner — thread, body, Join, nothing
/// else — leaked two to six window classes per fact into the desktop heap.
/// On CI's non-interactive 768 KB heap the suite outgrew the budget, and
/// every hosted fact after the exhaustion point failed with a Win32Exception
/// out of <c>CreateWindowEx</c> (or hung the run). A fifty-first copy would
/// bring that back one pull request later, so the census counts them.
/// </remarks>
[Trait("census", "sta-thread")]
public sealed class StaThreadCensus
{
    /// <summary>Facts that own a live <c>Dispatcher.Run</c> loop on a thread
    /// of their own — the dispatcher's lifecycle IS what they test — and
    /// shut it down themselves (the leak guard checks that they do).</summary>
    private static readonly (string File, string Member)[] OwnTheirDispatcherLifecycle =
    [
        ("ConnectionsLeafTests.Ghost.cs", "ACreateCompletionAbortedByTheDispatchersShutdownWithdrawsItsPromise"),
        ("PanelWorkSchedulerTests.cs", "AnEnqueuedApplyAbortedByTheDispatchersShutdownWithdrawsItsPromise"),
        ("PanelWorkSchedulerTests.cs", "AForeignDispatcherContextPostsThroughTheContextNotTheConstructingThread"),
        ("PanelWorkSchedulerTests.cs", "AShutDownDispatcherAbortsThePostAndTheTrackedTaskStillCompletes"),
        ("W1HardeningTests.cs", "OffThreadDisposalRunsOnTheCapturedWpfDispatcher"),
    ];

    private const string Runner = "Support/StaThread.cs";

    [Fact]
    public void EveryStaThreadGoesThroughTheSharedRunner()
    {
        List<(string File, string Member, int Line)> sites = ApartmentStateSites();

        Assert.True(
            sites.Count(site => site.File == Runner) == 1,
            $"{Runner} must set the apartment state exactly once; found {sites.Count(site => site.File == Runner)}.");

        string[] adHoc = [.. sites
            .Where(site => site.File != Runner && !OwnTheirDispatcherLifecycle.Contains((site.File, site.Member)))
            .Select(site => $"{site.File}:{site.Line} ({site.Member})")];
        Assert.True(
            adHoc.Length == 0,
            "These start an STA thread of their own. Forward the helper to StaThread.Run (or StaThread.RunPumped / "
            + "StaThread.Run<T>) so its WPF resources are torn down before the thread ends — an ad-hoc runner leaks "
            + "window classes into CI's 768 KB desktop heap: " + string.Join(", ", adHoc));

        string[] stale = [.. OwnTheirDispatcherLifecycle
            .Where(entry => !sites.Any(site => (site.File, site.Member) == entry))
            .Select(entry => $"{entry.File} ({entry.Member})")];
        Assert.True(stale.Length == 0, "Allow-list entries that no longer start an STA thread: " + string.Join(", ", stale));
    }

    [Fact]
    public void TheLeakGuardReadsTheDispatcherRegistryWpfKeeps()
    {
        Assert.True(
            LeakedDispatcherGuardAttribute.Readable,
            "WPF's private Dispatcher._dispatchers/_globalLock moved: the assembly-wide leak guard is disarmed until it is taught the new shape.");
        bool seen = StaThread.Run(() => LeakedDispatcherGuardAttribute.Snapshot()
            .Contains(System.Windows.Threading.Dispatcher.CurrentDispatcher));
        Assert.True(seen, "the registry read does not see a live dispatcher; the leak guard would pass everything.");
    }

    /// <summary>The teardown, measured where the leak lives: the window
    /// classes this process has registered. Five facts that show a window,
    /// orphan an HwndSource and close neither leave no class behind — the
    /// old runner left six per fact.</summary>
    [Fact]
    public void FactsThatLeaveTheirWindowsOpenLeaveNoWindowClassBehind()
    {
        const int facts = 5;
        int before = RegisteredWrapperClasses();
        for (int fact = 0; fact < facts; fact++)
        {
            StaThread.Run(() =>
            {
                var window = new Window
                {
                    Content = new TextBox { Text = "left open" },
                    Left = -32000,
                    Top = -32000,
                    Width = 200,
                    Height = 100,
                    ShowInTaskbar = false,
                    ShowActivated = false,
                    WindowStyle = WindowStyle.None,
                };
                window.Show();
                window.UpdateLayout();
                _ = new HwndSource(0, 0, 0, 0, 0, "orphaned source", IntPtr.Zero);
            });
        }
        int grown = RegisteredWrapperClasses() - before;
        Assert.True(
            grown < facts,
            $"{facts} torn-down facts left {grown} WPF window classes registered; the teardown should leave none.");
    }

    private static List<(string File, string Member, int Line)> ApartmentStateSites()
    {
        string root = Path.Combine(SourceText.RepoRoot(), "apps", "slate-windows", "tests", "SlateWindows.Tests");
        Assert.True(Directory.Exists(root), $"the unit project is missing: {root}");
        var sites = new List<(string File, string Member, int Line)>();
        foreach (string file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(file => !Path.GetRelativePath(root, file).Split(Path.DirectorySeparatorChar)
                .Any(segment => segment is "bin" or "obj"))
            .OrderBy(file => file, StringComparer.Ordinal))
        {
            CSharpSource source = CSharpSource.LoadPath(file);
            string relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            foreach (InvocationExpressionSyntax call in source.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                string? name = call.Expression switch
                {
                    MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
                    IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                    _ => null,
                };
                if (name is not ("SetApartmentState" or "TrySetApartmentState"))
                {
                    continue;
                }
                SyntaxNode? owner = call.Ancestors().FirstOrDefault(node => node is BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax or PropertyDeclarationSyntax);
                string member = owner switch
                {
                    MethodDeclarationSyntax method => method.Identifier.ValueText,
                    LocalFunctionStatementSyntax local => local.Identifier.ValueText,
                    ConstructorDeclarationSyntax constructor => constructor.Identifier.ValueText,
                    PropertyDeclarationSyntax property => property.Identifier.ValueText,
                    _ => "<member>",
                };
                sites.Add((relative, member, call.GetLocation().GetLineSpan().StartLinePosition.Line + 1));
            }
        }
        return sites;
    }

    /// <summary>The WPF window classes (<c>HwndWrapper[…]</c>) registered
    /// by THIS process. Every class name lives in the session's atom table,
    /// which other processes share — a concurrent test host's classes carry
    /// the same prefix — so each name counts only when
    /// <c>GetClassInfoEx</c> finds it registered against this process's
    /// module, the instance WPF registers them under.</summary>
    private static int RegisteredWrapperClasses()
    {
        IntPtr module = GetModuleHandleW(IntPtr.Zero);
        int count = 0;
        var name = new StringBuilder(512);
        for (uint atom = 0xC000; atom <= 0xFFFF; atom++)
        {
            name.Clear();
            if (GetClipboardFormatNameW(atom, name, name.Capacity) > 0
                && name.ToString().StartsWith("HwndWrapper[", StringComparison.Ordinal))
            {
                var info = new WindowClassInfo { Size = Marshal.SizeOf<WindowClassInfo>() };
                if (GetClassInfoExW(module, name.ToString(), ref info) != 0)
                {
                    count++;
                }
            }
        }
        return count;
    }

    /// <summary>WNDCLASSEXW with its string members as raw pointers: the
    /// system writes them, and a marshalled string field would free memory
    /// it never allocated.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct WindowClassInfo
    {
        public int Size;
        public int Style;
        public IntPtr WindowProcedure;
        public int ClassExtra;
        public int WindowExtra;
        public IntPtr Instance;
        public IntPtr Icon;
        public IntPtr Cursor;
        public IntPtr Background;
        public IntPtr MenuName;
        public IntPtr ClassName;
        public IntPtr SmallIcon;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClipboardFormatNameW(uint format, StringBuilder name, int maxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassInfoExW(IntPtr instance, string className, ref WindowClassInfo info);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandleW(IntPtr moduleName);
}
