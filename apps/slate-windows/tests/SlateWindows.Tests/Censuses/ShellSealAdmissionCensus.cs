// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// #1275 (codex round 6, owner decision): while the palette is sealed — open
// or closed — only Escape acts, and ONE check says so: MainWindow.SealTakes.
// The window is the root of every input route in the shell's tree, so its
// preview handlers run before any element's, and a key, text or pointer
// press the check marks handled reaches no later handler, no input binding,
// no access key and no menu mode. The census keeps that true as the shell
// grows: the seal is read nowhere but the check (and the reviewed waits
// that admit no input, SealReadersAllowed); every input handler the
// window declares, in XAML or in code, is the check and nothing else (the
// key route calls it first and returns); and nothing in the shell listens
// past a handled input or ahead of the routed events, where the check could
// not reach it.
//
// A past-handled class handler on a BUBBLING mouse event is unreachable
// under the seal: WPF's MouseDevice raises no bubbling twin for a preview the
// admission handled (dotnet/wpf MouseDevice.cs, the PostProcessInput arms
// that raise MouseDown/MouseUp/MouseWheel only for an unhandled preview), so
// only preview-side class handlers need the seal's own (MainWindow.Seal.cs).
// WB-15 (#1313, #1307) adds the departure observer's call graph, the one
// HWND hook, the window's input overrides and the owned-window guard.

using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "shell-seal-admission")]
public sealed class ShellSealAdmissionCensus
{
    private const string Admission = "SealTakes";
    private const string KeyRoute = "Window_PreviewKeyDown";

    /// <summary>The window's preview input routes the admission must hold at
    /// least: keys down and up (the Apps key opens a context menu on its
    /// release), text that arrives without a key, pointer press and
    /// release, the wheel (a graph pans and zooms on it — codex round 7).</summary>
    private static readonly string[] RequiredGates =
        ["PreviewKeyDown", "PreviewKeyUp", "PreviewTextInput", "PreviewMouseDown", "PreviewMouseUp", "PreviewMouseWheel"];

    /// <summary>The double-click events WPF's <c>Control</c> raises FRESH from
    /// a press it saw past handled; the seal's class handler takes both, and
    /// only it may register for them.</summary>
    private static readonly string[] DoubleClickEvents =
        ["Control.PreviewMouseDoubleClickEvent", "Control.MouseDoubleClickEvent"];

    /// <summary>The shell's reads of a press's click count, each reviewed:
    /// none can run for a press the admission took.</summary>
    private static readonly Dictionary<(string File, string Method), string> ClickCountAllowed = new()
    {
        [("GraphDiagramView.cs", "OnMouseLeftButtonDown")] =
            "an override WPF calls only for an unhandled press",
        [("MainWindow.Templates.cs", "TemplateNameCreate_PreviewMouseLeftButtonDown")] =
            "an instance handler XAML attaches, which a handled press never reaches",
    };

    /// <summary>The listeners allowed past a handled input, each with why it
    /// cannot act under the seal.</summary>
    private static readonly Dictionary<(string File, string RoutedEvent), string> PastHandledAllowed = new()
    {
        [("MainWindow.xaml.cs", "AccessKeyManager.AccessKeyPressedEvent")] =
            "the seal's own access-key gate: it must see a candidate a menu item already answered",
        [("ShellSeal.cs", "AccessKeyManager.AccessKeyPressedEvent")] =
            "the owned window's access-key gate: the shell's own reason, in a modeless window the shell admitted (WB-15)",
        [("SlateTextEditor.cs", "TextCompositionManager.PreviewTextInputStartEvent")] =
            "IME composition tracking only (IsComposing); a taken key never reaches the IME",
        [("SlateTextEditor.cs", "TextCompositionManager.TextInputEvent")] =
            "IME composition tracking only (IsComposing); a taken key never reaches the IME",
    };

    /// <summary>Class handlers for a BUBBLING input event, each reviewed. One
    /// runs ahead of its element's own handlers, never ahead of the window's
    /// preview route, which the admission holds. Under real input a handled
    /// preview has no bubbling twin at all (WPF's MouseDevice raises none);
    /// the rule that such a handler is registered without
    /// <c>handledEventsToo</c> (the census checks) keeps it out of every
    /// other raise of the twin as well — a synthetic one carries the
    /// preview's handled state.</summary>
    private static readonly Dictionary<(string File, string RoutedEvent), string> BubblingClassHandlersAllowed = new()
    {
        [("SelectorFocus.cs", "Mouse.MouseDownEvent")] =
            "W7-7 PR 4's click rule (R-5, OD-11c): a press on a populated list's empty area lands a row; "
            + "held by CommandPaletteTests.AClickOnAListsEmptyAreaMovesNoKeysUnderTheSeal",
    };

    /// <summary>Class handlers for routed events that carry no key, text or
    /// pointer — a keyboard FOCUS change — each with why it cannot act on
    /// input the admission took (file-scoped: the same registration anywhere
    /// else is an offender). A focus change is not an input the admission
    /// takes: under the seal no key or press reaches the shell to move focus,
    /// so what these see is a change the shell itself made — a running
    /// command's rebuild taking the focused element away — and the guard lands
    /// the keys, as it must then too.</summary>
    private static readonly Dictionary<(string File, string RoutedEvent), string> ClassHandlerAllowed = new()
    {
        [("RegionFocusGuard.cs", "Keyboard.GotKeyboardFocusEvent")] =
            "W7-7 PR 4b's guard (R-5, S3): records the scopes the focused element is in",
        [("RegionFocusGuard.cs", "Keyboard.PreviewGotKeyboardFocusEvent")] =
            "W7-7 PR 4b's guard: lands keys stranded by an element that went away, inside WPF's re-evaluation",
        [("RegionFocusGuard.cs", "Keyboard.PreviewLostKeyboardFocusEvent")] =
            "W7-7 PR 4b's guard: tells a direct focus request from WPF's re-evaluation",
        [("EditorLandingSlot.cs", "Keyboard.GotKeyboardFocusEvent")] = FocusDepartureObserver,
        [("EditorLandingSlot.cs", "Keyboard.LostKeyboardFocusEvent")] = FocusDepartureObserver,
    };

    /// <summary>The shell's reads of the seal outside the admission, each
    /// reviewed: a wait that admits no input — it runs nothing while the seal
    /// holds, and nothing it runs is a key, text or pointer press.</summary>
    private static readonly Dictionary<(string File, string Member), string> SealReadersAllowed = new()
    {
        [("MainWindow.xaml.cs", "ExternalActivationMustWait")] =
            "WB-9 (#1296, AR-48): the single-instance handoff is no input route; it waits while the seal "
            + "holds and runs from SealReleased once it lifts — held by "
            + "MainWindowHandoffTests.AHandoffWhileAPaletteCommandIsSealedWaitsForTheSeal",
    };

    /// <summary>W7-7 PR 8's one departure observer (contract 40 OD-12).</summary>
    private const string FocusDepartureObserver =
        "a focus-change notification, raised by a focus move — never by a key, text or pointer reaching a "
        + "handler; the observer only lets go of the window's held editor landing or notes its entry: it runs "
        + "no command, moves no focus and speaks nothing";

    private sealed record Unit(string Name, CompilationUnitSyntax Root);

    [Fact]
    public void TheSealIsReadOnlyByTheAdmission()
    {
        List<Unit> shell = ShellUnits().ToList();
        Assert.True(shell.Count > 50, "the census found almost no shell source — the discovery is broken");
        Assert.True(
            shell.Any(unit => unit.Name == "MainWindow.Seal.cs"),
            "MainWindow.Seal.cs is gone — the admission's home moved without the census");

        List<string> offenders = SealReaderOffenders(shell);
        Assert.True(
            offenders.Count == 0,
            "the palette's seal is read outside the shell's one admission check (a second check is a route "
            + "the admission does not govern, and a branch that forgets it bypasses the seal):\n  "
            + string.Join("\n  ", offenders));
        foreach ((string file, string member) in SealReadersAllowed.Keys)
        {
            Unit unit = Assert.Single(shell, candidate => candidate.Name == file);
            Assert.True(
                unit.Root.DescendantNodes().OfType<PropertyDeclarationSyntax>()
                    .Any(p => p.Identifier.ValueText == member && p.DescendantNodes().Any(IsSealReference)),
                $"{file}: {member} is allowed to read the seal but no longer does");
        }

        // The check itself: unsealed it takes nothing; sealed it takes the
        // input — after T9's Escape — and says so.
        MethodDeclarationSyntax check = CSharpSource.Load("MainWindow.Seal.cs").Method(Admission);
        SyntaxList<StatementSyntax> statements = check.Body!.Statements;
        Assert.Equal("if(!_viewModel.Palette.IsSealed){returnfalse;}", CSharpSource.Normalize(statements[0]));
        Assert.Equal(
            ["input.Handled=true;", "returntrue;"],
            statements.Skip(statements.Count - 2).Select(statement => CSharpSource.Normalize(statement)).ToArray());
    }

    [Fact]
    public void TheKeyRouteCallsTheAdmissionFirst()
    {
        MethodDeclarationSyntax route = CSharpSource.Load("MainWindow.xaml.cs").Method(KeyRoute);
        Assert.Equal(
            "if(SealTakes(e)){return;}",
            CSharpSource.Normalize(Assert.IsAssignableFrom<StatementSyntax>(route.Body?.Statements.FirstOrDefault())));
    }

    [Fact]
    public void EveryInputHandlerTheWindowDeclaresIsTheAdmission()
    {
        XElement window = XDocument.Load(
            Path.Combine(SourceText.ShellSourceRoot(), "MainWindow.xaml"), LoadOptions.SetLineInfo).Root!;
        List<Unit> sources = WindowUnits().ToList();
        Assert.True(sources.Count > 5, "the census found almost no MainWindow partials — the discovery is broken");

        (List<string> offenders, HashSet<string> gated) = WindowRouteOffenders(window, sources);
        Assert.True(
            offenders.Count == 0,
            "window-level input routes that do not go through the seal's admission:\n  "
            + string.Join("\n  ", offenders));
        Assert.True(
            RequiredGates.All(gated.Contains),
            "the window no longer gates " + string.Join(", ", RequiredGates.Where(gate => !gated.Contains(gate)))
            + " through the admission — that input reaches the shell under the seal");

        // The access-key gate listens past handled: a menu item's own answer
        // marks the event handled before it reaches the window.
        InvocationExpressionSyntax accessKeys = Assert.Single(
            Registrations(sources),
            invocation => invocation.ArgumentList.Arguments.Count > 0
                && CSharpSource.Normalize(invocation.ArgumentList.Arguments[0].Expression)
                    == "AccessKeyManager.AccessKeyPressedEvent");
        Assert.True(
            PastHandled(accessKeys),
            "the access-key gate no longer listens past handled — a menu item's answer hides its candidate from the seal");
    }

    /// <summary>
    /// Codex round 7: a press the admission took still reaches
    /// <c>Control</c>'s own double-click detection, which listens past
    /// handled and raises a fresh, unhandled double-click on the control.
    /// The seal's gate is one class handler per double-click event, on
    /// <c>Control</c>, past handled, applying the owning shell's admission —
    /// and nothing else in the shell acts on a double-click ahead of it.
    /// </summary>
    [Fact]
    public void TheDoubleClickWpfRebuildsGoesThroughTheAdmission()
    {
        CSharpSource seal = CSharpSource.Load("MainWindow.Seal.cs");
        ConstructorDeclarationSyntax registration = seal.Root.DescendantNodes()
            .OfType<ConstructorDeclarationSyntax>()
            .Single(constructor => constructor.Modifiers.Any(SyntaxKind.StaticKeyword));
        string[] registered = registration.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(invocation => CSharpSource.Normalize(invocation.Expression) == "EventManager.RegisterClassHandler")
            .Select(invocation => string.Join(",", invocation.ArgumentList.Arguments.Select(argument => CSharpSource.Normalize(argument))))
            .ToArray();
        Assert.Equal(
            DoubleClickEvents.Select(routed =>
                $"typeof(Control),{routed},newMouseButtonEventHandler(OnDoubleClickUnderTheSeal),handledEventsToo:true"),
            registered);
        Assert.Equal(
            "if(senderisDependencyObjectelement&&ShellSeal.ShellOf(element)isMainWindowshell){_=shell.SealTakes(e);}",
            CSharpSource.Normalize(Assert.Single(seal.Method("OnDoubleClickUnderTheSeal").Body!.Statements)));

        List<string> offenders = DoubleClickOffenders(ShellUnits());
        Assert.True(
            offenders.Count == 0,
            "double-click routes the seal's class handler does not govern:\n  " + string.Join("\n  ", offenders));
    }

    [Fact]
    public void NothingInTheShellListensPastAHandledInputOrAheadOfTheRoutes()
    {
        List<string> offenders = PastHandledOffenders(ShellUnits());
        Assert.True(
            offenders.Count == 0,
            "listeners that act on input the seal's admission took, or see it before the window does:\n  "
            + string.Join("\n  ", offenders));
    }

    /// <summary>The detectors catch each bypass they exist for — a census
    /// that cannot fail proves nothing.</summary>
    [Theory]
    [InlineData("xaml-attribute", "<Window xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" PreviewKeyDown=\"Window_PreviewKeyDown\" KeyUp=\"Bypass\" />")]
    [InlineData("xaml-late-check", "<Window xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" PreviewKeyDown=\"Window_PreviewKeyDown\" PreviewMouseDown=\"Late\" />")]
    [InlineData("code-subscription", "class MainWindow { void M() { PreviewKeyDown += Bypass; } }")]
    [InlineData("code-add-handler", "class MainWindow { void M() { AddHandler(Keyboard.KeyDownEvent, new KeyEventHandler(Bypass)); } }")]
    [InlineData("code-lambda", "class MainWindow { void M() { AddHandler(Mouse.MouseUpEvent, new MouseButtonEventHandler((s, e) => { })); } }")]
    [InlineData("second-seal-read", "class MainWindow { bool M() => _viewModel.Palette.IsSealed; }")]
    [InlineData("past-handled", "class Other { void M(UIElement u) { u.AddHandler(Keyboard.KeyDownEvent, new KeyEventHandler(X), true); } }")]
    [InlineData("class-handler", "class Other { static void M() { EventManager.RegisterClassHandler(typeof(Window), Keyboard.PreviewKeyDownEvent, new KeyEventHandler(X)); } }")]
    [InlineData("focus-class-handler-elsewhere", "class Other { static void M() { EventManager.RegisterClassHandler(typeof(UIElement), Keyboard.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(X), true); } }")]
    [InlineData("input-manager", "class Other { void M() { InputManager.Current.PreProcessInput += X; } }")]
    [InlineData("double-click-class-handler", "class Other { static Other() { EventManager.RegisterClassHandler(typeof(ListBox), Control.MouseDoubleClickEvent, new MouseButtonEventHandler(X), true); } }")]
    [InlineData("double-click-override", "class Other : ListBox { protected override void OnMouseDoubleClick(MouseButtonEventArgs e) => Run(); }")]
    [InlineData("click-count-read", "class Other { void Pressed(object s, MouseButtonEventArgs e) { if (e.ClickCount == 2) Run(); } }")]
    [InlineData("reviewed-bubble-past-handled", "class SelectorFocus { static void M() { EventManager.RegisterClassHandler(typeof(ListBox), Mouse.MouseDownEvent, new MouseButtonEventHandler(X), handledEventsToo: true); } }")]
    [InlineData("focus-allowance-other-input", "class RegionFocusGuard { static void M() { EventManager.RegisterClassHandler(typeof(Window), Keyboard.PreviewKeyDownEvent, new KeyEventHandler(X), true); } }")]
    [InlineData("transition-side-effect", "        Keyboard.ClearFocus();\n        if (Held is not { } held)")]
    [InlineData("transition-delegate-side-effect", "                    withdraw: () => { Keyboard.ClearFocus(); return surface.CancelFocusLanding(held); },")]
    [InlineData("transition-focus-api", "        held.Detach();\n        FocusManager.SetFocusedElement(held.Target()!, null);")]
    [InlineData("hwnd-hook-reads-keys", "        if (message == WmEnable)\n        {\n            Recompute();\n        }\n\n        if (message == 0x0100)\n        {\n            Recompute();\n        }")]
    [InlineData("hwnd-hook-second", "class Other { void M(HwndSource source) { source.AddHook(Hook); } }")]
    [InlineData("input-override-key", "partial class MainWindow { protected override void OnPreviewKeyDown(KeyEventArgs e) => Run(); }")]
    [InlineData("input-override-pointer", "partial class MainWindow { protected override void OnPreviewMouseDown(MouseButtonEventArgs e) => Run(); }")]
    [InlineData("owned-window-route-missing", "        owned.PreviewMouseWheel += Take;\n")]
    [InlineData("owned-window-unadmitted", "internal static class Planted { internal static void Open(Window owner) { var window = new Window { Owner = owner }; window.Show(); } }")]
    [InlineData("owned-window-admitted-late", "internal static class Planted { internal static void Open(Window owner) { var window = new Window { Owner = owner }; window.PreviewKeyDown += (_, _) => { }; ShellSeal.Admit(window); window.Show(); } }")]
    [InlineData("owned-window-conditional-show", "internal static class Planted { internal static void Open(Window owner) { Window? window = new Window { Owner = owner }; window?.Show(); } }")]
    [InlineData("owned-window-shown-elsewhere", "internal static class Planted { static Window Build(Window owner) { var window = new Window { Owner = owner }; ShellSeal.Admit(window); return window; } internal static void Open(Window owner) => Build(owner).Show(); }")]
    [InlineData("owned-window-show-method-group", "internal static class Planted { internal static void Open(Window owner) { var window = new Window { Owner = owner }; window.Dispatcher.BeginInvoke(new System.Action(window.Show)); } }")]
    [InlineData("modal-dialog-admitted", "internal static class Planted { internal static void Ask(Window owner) { var dialog = new Window(); dialog.Owner = owner; ShellSeal.Admit(dialog); _ = dialog.ShowDialog(); } }")]
    [InlineData("shell-of-follows-owner", "    internal static MainWindow? ShellOf(DependencyObject element)\n    {\n        for (Window? window = Window.GetWindow(element); window is not null; window = window.Owner)\n        {\n            if (window is MainWindow shell)\n            {\n                return shell;\n            }\n        }\n\n        return null;\n    }\n\n    internal static MainWindow? Unused(DependencyObject element) => Window.GetWindow(element) switch")]
    public void EachDetectorCatchesItsBypass(string bypass, string source)
    {
        // The WB-15 rows plant into a real file — through the compilation
        // where the detector binds — and the real files are clean.
        List<string>? planted = bypass switch
        {
            "transition-side-effect" => TransitionCallGraphOffenders(
                PlantInto(FocusObserverFile, "        if (Held is not { } held)", source)),
            "transition-delegate-side-effect" => TransitionCallGraphOffenders(
                PlantInto("MainWindow.xaml.cs", "                    withdraw: () => surface.CancelFocusLanding(held),", source)),
            "transition-focus-api" => TransitionCallGraphOffenders(
                PlantInto(FocusObserverFile, "        held.Detach();", source)),
            "hwnd-hook-reads-keys" => HwndHookOffenders(ShellUnits().Select(unit => unit.Name == MonitorFile
                ? new Unit(unit.Name, Parse(PlantedText(MonitorFile, "        if (message == WmEnable)\n        {\n            Recompute();\n        }", source)))
                : unit)),
            "hwnd-hook-second" => HwndHookOffenders(ShellUnits().Append(new Unit("Other.cs", Parse(source)))),
            "input-override-key" or "input-override-pointer" =>
                InputOverrideOffenders(WindowUnits().Append(new Unit("MainWindow.Planted.cs", Parse(source)))),
            "owned-window-route-missing" => OwnedWindowOffenders(PlantInto(OwnedWindowGuardFile, source, string.Empty)),
            "owned-window-unadmitted" or "owned-window-admitted-late" or "modal-dialog-admitted"
                or "owned-window-conditional-show" or "owned-window-shown-elsewhere" or "owned-window-show-method-group" =>
                OwnedWindowOffenders(ShellCompilation.Compilation.AddSyntaxTrees(
                    CSharpSyntaxTree.ParseText(
                        "using System.Windows;\nnamespace SlateWindows;\n" + source,
                        (CSharpParseOptions)ShellCompilation.Compilation.SyntaxTrees.First().Options,
                        path: "Planted.cs"))),
            "shell-of-follows-owner" => OwnedWindowOffenders(PlantInto(
                OwnedWindowGuardFile,
                "    internal static MainWindow? ShellOf(DependencyObject element) => Window.GetWindow(element) switch",
                source)),
            _ => null,
        };
        if (planted is not null)
        {
            Assert.True(planted.Count > 0, $"the census missed a planted {bypass} bypass");
            return;
        }


        const string Handlers = """
            partial class MainWindow
            {
                private bool SealTakes(RoutedEventArgs input) => _viewModel.Palette.IsSealed;
                private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
                {
                    if (SealTakes(e))
                    {
                        return;
                    }
                }
                private void Bypass(object sender, KeyEventArgs e) { }
                private void Late(object sender, MouseButtonEventArgs e)
                {
                    Route();
                    _ = SealTakes(e);
                }
            }
            """;
        var handlers = new Unit("MainWindow.Seal.cs", Parse(Handlers));
        List<string> found = bypass switch
        {
            "xaml-attribute" or "xaml-late-check" =>
                WindowRouteOffenders(XElement.Parse(source), [handlers]).Offenders,
            "code-subscription" or "code-add-handler" or "code-lambda" =>
                WindowRouteOffenders(
                    XElement.Parse("<Window xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" />"),
                    [handlers, new Unit("MainWindow.Extra.cs", Parse(source))]).Offenders,
            "second-seal-read" => SealReaderOffenders([handlers, new Unit("MainWindow.Extra.cs", Parse(source))]),
            "double-click-override" or "click-count-read" => DoubleClickOffenders([new Unit("Other.cs", Parse(source))]),
            "reviewed-bubble-past-handled" => PastHandledOffenders([new Unit("SelectorFocus.cs", Parse(source))]),
            "focus-allowance-other-input" => PastHandledOffenders([new Unit("RegionFocusGuard.cs", Parse(source))]),
            _ => PastHandledOffenders([new Unit("Other.cs", Parse(source))]),
        };
        Assert.True(found.Count > 0, $"the census missed a planted {bypass} bypass");

        // And the planted handlers alone are clean, so the finding above is
        // the bypass, not the scaffold.
        Assert.Empty(SealReaderOffenders([handlers]));
    }

    /// <summary>
    /// The focus guard is admitted only in its exact shape (PR 4b codex round
    /// 2, F7): <see cref="ClassHandlerAllowed"/> names its events, and
    /// <see cref="FocusGuardShapeOffenders"/> holds its file to exactly the
    /// intended registrations and recording callbacks. The real file is clean,
    /// and each change planted in that same file is an offender.
    /// </summary>
    [Theory]
    [InlineData("a wrong callback",
        "new KeyboardFocusChangedEventHandler(Asked)",
        "new KeyboardFocusChangedEventHandler(Recorded)")]
    [InlineData("a wrong owner",
        "typeof(UIElement), Keyboard.PreviewLostKeyboardFocusEvent",
        "typeof(FrameworkElement), Keyboard.PreviewLostKeyboardFocusEvent")]
    [InlineData("a duplicate registration",
        "new KeyboardFocusChangedEventHandler(Guarded));",
        "new KeyboardFocusChangedEventHandler(Guarded));\n"
        + "        EventManager.RegisterClassHandler(\n"
        + "            typeof(Window), Keyboard.PreviewGotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(Guarded));")]
    [InlineData("the landing past handled",
        "new KeyboardFocusChangedEventHandler(Guarded));",
        "new KeyboardFocusChangedEventHandler(Guarded), handledEventsToo: true);")]
    [InlineData("the record not past handled",
        "new KeyboardFocusChangedEventHandler(Recorded), handledEventsToo: true);",
        "new KeyboardFocusChangedEventHandler(Recorded), handledEventsToo: false);")]
    [InlineData("a registration outside Register()",
        "    internal static UIElement[] ScopesOf(DependencyObject element)",
        "    internal static void Again() => EventManager.RegisterClassHandler(\n"
        + "        typeof(Window), Keyboard.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(Recorded), handledEventsToo: true);\n\n"
        + "    internal static UIElement[] ScopesOf(DependencyObject element)")]
    [InlineData("a lambda handler added",
        "new KeyboardFocusChangedEventHandler(Asked), handledEventsToo: true);",
        "new KeyboardFocusChangedEventHandler(Asked), handledEventsToo: true);\n"
        + "        EventManager.RegisterClassHandler(\n"
        + "            typeof(Window), Keyboard.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler((_, e) => e.Handled = true), handledEventsToo: true);")]
    [InlineData("a side effect in the record",
        "ScopesAtFocus.AddOrUpdate(input, ScopesOf(focused));",
        "ScopesAtFocus.AddOrUpdate(input, ScopesOf(focused));\n            Keyboard.ClearFocus();")]
    [InlineData("a side effect in the request's note",
        "_directRequest = (new WeakReference<IInputElement>(old), new WeakReference<IInputElement>(requested));",
        "_directRequest = (new WeakReference<IInputElement>(old), new WeakReference<IInputElement>(requested));\n            e.Handled = true;")]
    public void TheFocusGuardIsAdmittedOnlyInItsExactShape(string bypass, string from, string to)
    {
        string real = File.ReadAllText(Path.Combine(SourceText.ShellSourceRoot(), FocusGuardFile));
        Assert.Empty(FocusGuardShapeOffenders(new Unit(FocusGuardFile, Parse(real))));
        Assert.Empty(PastHandledOffenders([new Unit(FocusGuardFile, Parse(real))]));
        Assert.True(
            real.Split(from).Length == 2,
            $"the planted {bypass} no longer finds its one anchor in {FocusGuardFile} — update the row");

        List<string> found = PastHandledOffenders([new Unit(FocusGuardFile, Parse(real.Replace(from, to, StringComparison.Ordinal)))]);
        Assert.True(found.Count > 0, $"the census admitted {bypass} in the focus guard's own file");
    }

    /// <summary>
    /// The departure observer is admitted only in its exact shape (codex on
    /// PR 8's merge with #1298): <see cref="ClassHandlerAllowed"/> names its
    /// events, and <see cref="FocusObserverShapeOffenders"/> holds its file to
    /// exactly the intended registrations. The real file is clean — two
    /// registrations in one loop over three owners, six at run time — and
    /// each change planted in that same file is an offender.
    /// </summary>
    [Theory]
    [InlineData("a wrong callback",
        "Keyboard.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(OnGotKeyboardFocus)",
        "Keyboard.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(OnLostKeyboardFocus)")]
    [InlineData("a wrong owner", "typeof(ContentElement)", "typeof(Control)")]
    [InlineData("an owner more", "typeof(UIElement3D) }", "typeof(UIElement3D), typeof(Window) }")]
    [InlineData("a duplicate registration",
        "new KeyboardFocusChangedEventHandler(OnGotKeyboardFocus), handledEventsToo: true);",
        "new KeyboardFocusChangedEventHandler(OnGotKeyboardFocus), handledEventsToo: true);\n"
        + "            EventManager.RegisterClassHandler(\n"
        + "                owner, Keyboard.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(OnGotKeyboardFocus), handledEventsToo: true);")]
    [InlineData("not past handled",
        "new KeyboardFocusChangedEventHandler(OnLostKeyboardFocus), handledEventsToo: true);",
        "new KeyboardFocusChangedEventHandler(OnLostKeyboardFocus), handledEventsToo: false);")]
    [InlineData("a side-effecting handler added",
        "new KeyboardFocusChangedEventHandler(OnLostKeyboardFocus), handledEventsToo: true);",
        "new KeyboardFocusChangedEventHandler(OnLostKeyboardFocus), handledEventsToo: true);\n"
        + "            EventManager.RegisterClassHandler(\n"
        + "                owner, Keyboard.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler((_, e) => e.Handled = true), handledEventsToo: true);")]
    [InlineData("a registration outside the loop",
        "    public void Dispose()",
        "    internal static void Again() => EventManager.RegisterClassHandler(\n"
        + "        typeof(UIElement), Keyboard.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(OnGotKeyboardFocus), handledEventsToo: true);\n\n"
        + "    public void Dispose()")]
    [InlineData("a side-effecting handler substituted",
        "Transition(e.OldFocus, e.NewFocus);",
        "Transition(e.OldFocus, e.NewFocus);\n                Keyboard.ClearFocus();")]
    public void TheFocusObserverIsAdmittedOnlyInItsExactShape(string bypass, string from, string to)
    {
        string real = File.ReadAllText(Path.Combine(SourceText.ShellSourceRoot(), FocusObserverFile));
        Assert.Empty(FocusObserverShapeOffenders(new Unit(FocusObserverFile, Parse(real))));
        Assert.Empty(PastHandledOffenders([new Unit(FocusObserverFile, Parse(real))]));
        Assert.True(
            real.Split(from).Length == 2,
            $"the planted {bypass} no longer finds its one anchor in {FocusObserverFile} — update the row");

        List<string> found = PastHandledOffenders([new Unit(FocusObserverFile, Parse(real.Replace(from, to, StringComparison.Ordinal)))]);
        Assert.True(found.Count > 0, $"the census admitted {bypass} in the departure observer's own file");
    }

    /// <summary>
    /// WB-15 (#1313): OD-12's premise is that a focus transition "only
    /// withdraws or notes the held landing". The callbacks are pinned whole,
    /// so a side effect would hide in what they call: the observer's
    /// <c>Transition</c> and each slot's <c>OnTransition</c> are pinned too,
    /// <c>OnTransition</c> calls only its reviewed callees, and nothing two
    /// calls deep from it moves the keys, runs a command or speaks.
    /// </summary>
    [Fact]
    public void TheFocusObserversTransitionCallGraphHasNoSideEffects()
    {
        string real = File.ReadAllText(Path.Combine(SourceText.ShellSourceRoot(), FocusObserverFile));
        Assert.Empty(FocusObserverShapeOffenders(new Unit(FocusObserverFile, Parse(real))));
        List<string> offenders = TransitionCallGraphOffenders(ShellCompilation.Compilation);
        Assert.True(
            offenders.Count == 0,
            "the departure observer's transition does more than withdraw or note the held landing (OD-12):\n  "
            + string.Join("\n  ", offenders));
    }

    /// <summary>WB-15 (#1313): a raw HWND hook sees every window message
    /// ahead of WPF's input routes. The shell has one — the modal-loop
    /// monitor's, which reads WM_ENABLE and nothing else.</summary>
    [Fact]
    public void TheOnlyHwndHookIsTheModalLoopMonitorsAndItReadsWmEnableOnly()
    {
        List<string> offenders = HwndHookOffenders(ShellUnits());
        Assert.True(offenders.Count == 0, "HWND hooks outside the modal-loop monitor's reviewed one:\n  " + string.Join("\n  ", offenders));
    }

    /// <summary>WB-15 (#1313): an override of a routed input virtual on the
    /// window — <c>OnPreviewKeyDown</c> and its kin — runs as the window's
    /// class handling, ahead of the window's own preview handlers and the
    /// admission. The window's routes are instance handlers by
    /// design.</summary>
    [Fact]
    public void MainWindowOverridesNoRoutedInputVirtual()
    {
        List<string> offenders = InputOverrideOffenders(WindowUnits());
        Assert.True(offenders.Count == 0, "MainWindow overrides a routed input virtual:\n  " + string.Join("\n  ", offenders));
    }

    /// <summary>
    /// WB-15 (#1307 item 3, WD9): a modeless window the shell owns admits
    /// every route the shell's own window admits, through the same check —
    /// <c>ShellSeal.Admit</c> subscribes exactly <see cref="RequiredGates"/>
    /// and the access-key gate, first on the window; every modeless window
    /// the shell shows (its own window aside) is admitted before its
    /// <c>Show</c>, in the same method — a <c>Show</c> the census cannot
    /// trace to an admitted local or field is an offender; no modal dialog
    /// is admitted; and <c>ShellSeal.ShellOf</c> resolves only the shell's
    /// window and admitted windows (one <c>Owner</c> hop, no walk), so an
    /// unadmitted modal's double-clicks stay its own.
    /// </summary>
    [Fact]
    public void TheOwnedWindowGuardHoldsEveryRouteTheShellsWindowHolds()
    {
        List<string> offenders = OwnedWindowOffenders(ShellCompilation.Compilation);
        Assert.True(
            offenders.Count == 0,
            "a window the shell owns is not held by the seal as the shell's own window is:\n  " + string.Join("\n  ", offenders));
    }

    private static List<string> SealReaderOffenders(IEnumerable<Unit> shell)
    {
        var offenders = new List<string>();
        foreach (Unit unit in shell.Where(unit => unit.Name != "CommandPaletteViewModel.cs"))
        {
            foreach (SyntaxNode read in unit.Root.DescendantNodes().Where(IsSealReference))
            {
                string? method = read.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText;
                if (method == Admission && unit.Name == "MainWindow.Seal.cs")
                {
                    continue;
                }

                string? property = read.Ancestors().OfType<PropertyDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText;
                if (method is null && property is not null && SealReadersAllowed.ContainsKey((unit.Name, property)))
                {
                    continue;
                }

                offenders.Add($"{unit.Name}:{Line(read)} reads the seal in {method ?? property ?? "a non-method member"}");
            }
        }

        return offenders;
    }

    private static bool IsSealReference(SyntaxNode node) => node switch
    {
        MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText == "IsSealed",
        MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText == "IsSealed",
        _ => false,
    };

    /// <summary>Every input handler the window declares — attributes on the
    /// XAML root, subscriptions and registrations on the window in code —
    /// must be the admission: the key route calls it first and returns, and
    /// every other handler is nothing but the call.</summary>
    private static (List<string> Offenders, HashSet<string> Gated) WindowRouteOffenders(
        XElement window, IReadOnlyList<Unit> sources)
    {
        var offenders = new List<string>();
        var gated = new HashSet<string>(StringComparer.Ordinal);

        foreach (XAttribute attribute in window.Attributes().Where(attribute => !attribute.IsNamespaceDeclaration
            && attribute.Name.Namespace == XNamespace.None))
        {
            string name = attribute.Name.LocalName;
            if (!IsInputEvent(name))
            {
                continue;
            }

            string where = $"MainWindow.xaml <Window {name}=\"{attribute.Value}\">";
            if (CheckHandler(attribute.Value, sources, where, offenders))
            {
                gated.Add(name);
            }
        }

        foreach (Unit unit in sources)
        {
            foreach (AssignmentExpressionSyntax subscription in unit.Root.DescendantNodes()
                .OfType<AssignmentExpressionSyntax>()
                .Where(assignment => assignment.IsKind(SyntaxKind.AddAssignmentExpression)))
            {
                string? target = subscription.Left switch
                {
                    IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                    MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } access => access.Name.Identifier.ValueText,
                    _ => null,
                };
                if (target is not null && IsInputEvent(target))
                {
                    CheckHandlerExpression(subscription.Right, sources, $"{unit.Name}:{Line(subscription)} {target} +=", offenders);
                }
            }
        }

        foreach (InvocationExpressionSyntax registration in Registrations(sources))
        {
            SeparatedSyntaxList<ArgumentSyntax> arguments = registration.ArgumentList.Arguments;
            // AddHandler(event, handler, …) and X.AddYHandler(this, handler)
            // both carry the handler second.
            ExpressionSyntax? handler = arguments.Count > 1 ? arguments[1].Expression : null;
            string where = $"{Line(registration)} {CSharpSource.Normalize(registration.Expression)}";
            if (handler is null)
            {
                offenders.Add($"{where}: no handler argument the census can read");
                continue;
            }

            CheckHandlerExpression(handler, sources, where, offenders);
        }

        return (offenders, gated);
    }

    /// <summary>Handler registrations on the window itself: a bare or
    /// <c>this.</c> <c>AddHandler</c>, and a static
    /// <c>X.AddSomethingHandler(this, handler)</c>.</summary>
    private static IEnumerable<InvocationExpressionSyntax> Registrations(IEnumerable<Unit> sources) =>
        sources.SelectMany(unit => unit.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            .Where(invocation => invocation.Expression switch
            {
                IdentifierNameSyntax { Identifier.ValueText: "AddHandler" } => true,
                MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name.Identifier.ValueText: "AddHandler" } => true,
                MemberAccessExpressionSyntax access =>
                    access.Name.Identifier.ValueText.StartsWith("Add", StringComparison.Ordinal)
                    && access.Name.Identifier.ValueText.EndsWith("Handler", StringComparison.Ordinal)
                    && invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression is ThisExpressionSyntax,
                _ => false,
            });

    private static void CheckHandlerExpression(
        ExpressionSyntax handler, IReadOnlyList<Unit> sources, string where, List<string> offenders)
    {
        ExpressionSyntax method = handler is ObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: 1 } creation
            ? creation.ArgumentList.Arguments[0].Expression
            : handler;
        if (method is not IdentifierNameSyntax identifier)
        {
            offenders.Add($"{where}: the handler is not a named method, so the admission cannot be checked");
            return;
        }

        CheckHandler(identifier.Identifier.ValueText, sources, where, offenders);
    }

    private static bool CheckHandler(string name, IReadOnlyList<Unit> sources, string where, List<string> offenders)
    {
        MethodDeclarationSyntax[] declarations = sources
            .SelectMany(unit => unit.Root.DescendantNodes().OfType<MethodDeclarationSyntax>())
            .Where(method => method.Identifier.ValueText == name)
            .ToArray();
        if (declarations.Length != 1)
        {
            offenders.Add($"{where}: {declarations.Length} methods named {name} — the census reads exactly one");
            return false;
        }

        MethodDeclarationSyntax handler = declarations[0];
        string args = handler.ParameterList.Parameters.LastOrDefault()?.Identifier.ValueText ?? "e";
        string call = $"{Admission}({args})";
        bool isTheAdmission = handler.ExpressionBody is { } body
            ? CSharpSource.Normalize(body.Expression) == $"_={call}"
            : handler.Body is { Statements: [IfStatementSyntax only] } && CSharpSource.Normalize(only.Condition) == call;
        bool callsItFirst = name == KeyRoute
            && handler.Body?.Statements.FirstOrDefault() is { } first
            && CSharpSource.Normalize(first) == $"if({call}){{return;}}";
        if (!isTheAdmission && !callsItFirst)
        {
            offenders.Add($"{where}: {name} is not the seal's admission (it must be `_ = {call}`, one "
                + $"`if ({call})`, or — the key route — begin `if ({call}) {{ return; }}`)");
            return false;
        }

        return true;
    }

    /// <summary>Whether a window event (a CLR event on <c>Window</c>, or an
    /// attached <c>Owner.Event</c>) carries input.</summary>
    private static bool IsInputEvent(string name)
    {
        Type? args;
        int dot = name.LastIndexOf('.');
        if (dot < 0)
        {
            args = typeof(Window).GetEvent(name, BindingFlags.Public | BindingFlags.Instance)?
                .EventHandlerType?.GetMethod("Invoke")?.GetParameters().LastOrDefault()?.ParameterType;
        }
        else
        {
            string owner = name[..dot];
            string routed = name[(dot + 1)..];
            args = EventManager.GetRoutedEvents()
                .FirstOrDefault(candidate => candidate.OwnerType.Name == owner && candidate.Name == routed)?
                .HandlerType.GetMethod("Invoke")?.GetParameters().LastOrDefault()?.ParameterType;
        }

        return args is not null
            && (typeof(InputEventArgs).IsAssignableFrom(args) || args == typeof(AccessKeyPressedEventArgs));
    }

    /// <summary>What sees input the admission took, or sees it first: a
    /// listener registered past handled (outside the allowed list), a class
    /// handler for an input event, an input-manager hook.</summary>
    private static List<string> PastHandledOffenders(IEnumerable<Unit> shell)
    {
        var offenders = new List<string>();
        foreach (Unit unit in shell)
        {
            foreach (InvocationExpressionSyntax invocation in unit.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                string callee = invocation.Expression switch
                {
                    IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                    MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
                    _ => string.Empty,
                };
                SeparatedSyntaxList<ArgumentSyntax> arguments = invocation.ArgumentList.Arguments;
                if (callee == "AddHandler" && PastHandled(invocation))
                {
                    string routed = CSharpSource.Normalize(arguments[0].Expression);
                    if (!PastHandledAllowed.ContainsKey((unit.Name, routed)))
                    {
                        offenders.Add($"{unit.Name}:{Line(invocation)} listens to {routed} past handled");
                    }
                }
                else if (callee == "RegisterClassHandler" && arguments.Count >= 2)
                {
                    string routed = CSharpSource.Normalize(arguments[1].Expression);
                    bool theDoubleClickGate = unit.Name == "MainWindow.Seal.cs" && DoubleClickEvents.Contains(routed);
                    bool aReviewedBubble = BubblingClassHandlersAllowed.ContainsKey((unit.Name, routed))
                        && !routed.Contains(".Preview", StringComparison.Ordinal)
                        && arguments.Count == 3;
                    if (!IsKnownNonInput(routed) && !theDoubleClickGate && !aReviewedBubble
                        && !ClassHandlerAllowed.ContainsKey((unit.Name, routed)))
                    {
                        offenders.Add($"{unit.Name}:{Line(invocation)} registers a class handler for {routed} "
                            + "(a class handler runs before the window's own; name it here only if it carries no input)");
                    }
                }
            }

            foreach (SyntaxNode hook in unit.Root.DescendantNodes().Where(node => node is IdentifierNameSyntax
            {
                Identifier.ValueText: "InputManager" or "ThreadPreprocessMessage" or "ThreadFilterMessage",
            }))
            {
                offenders.Add($"{unit.Name}:{Line(hook)} hooks {CSharpSource.Normalize(hook)} — input seen ahead of the window's routes");
            }

            offenders.AddRange(FocusGuardShapeOffenders(unit));
            offenders.AddRange(FocusObserverShapeOffenders(unit));
        }

        return offenders;
    }

    /// <summary>W7-7 PR 4b's focus guard (contract 40 R-5 (h)) — with
    /// <see cref="FocusObserverFile"/>, one of the two files whose focus class
    /// handlers <see cref="ClassHandlerAllowed"/> names.</summary>
    private const string FocusGuardFile = "RegionFocusGuard.cs";

    /// <summary>The guard's registrations, exactly, as the last statements of
    /// its <c>Register()</c>: the scopes recorded past handled, the landing
    /// on the window's preview of a focus change, and a direct request told
    /// from WPF's re-evaluation past handled.</summary>
    private static readonly string[] FocusGuardRegistrations =
    [
        "EventManager.RegisterClassHandler(typeof(Window),Keyboard.GotKeyboardFocusEvent,newKeyboardFocusChangedEventHandler(Recorded),handledEventsToo:true);",
        "EventManager.RegisterClassHandler(typeof(Window),Keyboard.PreviewGotKeyboardFocusEvent,newKeyboardFocusChangedEventHandler(Guarded));",
        "EventManager.RegisterClassHandler(typeof(UIElement),Keyboard.PreviewLostKeyboardFocusEvent,newKeyboardFocusChangedEventHandler(Asked),handledEventsToo:true);",
    ];

    /// <summary>The guard's two past-handled callbacks, whole: each only
    /// records. <c>Guarded</c> is not pinned — it lands through the landings
    /// the shell registers, and pinning its text would bound nothing (AR-53's
    /// stance: a handler's body is code review's).</summary>
    private static readonly Dictionary<string, string> FocusGuardCallbacks = new()
    {
        ["Recorded"] = "{if(e.NewFocusisDependencyObjectfocusedandIInputElementinput&&!ReferenceEquals(sender,e.NewFocus)){"
            + "ScopesAtFocus.AddOrUpdate(input,ScopesOf(focused));"
            + "if(((focusedasFrameworkElement)?.DataContext??(focusedasFrameworkContentElement)?.DataContext)is{}context)"
            + "{ContextsAtFocus.AddOrUpdate(input,context);}else{ContextsAtFocus.Remove(input);}"
            + "if(focusedisListBoxItemorTreeViewItem&&ItemsControl.ItemsControlFromItemContainer(focused)is{}rows){"
            + "for(ItemsControllevel=rows;levelisTreeViewItemrow&&ItemsControl.ItemsControlFromItemContainer(row)is{}up;level=up){rows=up;}"
            + "RowsAtFocus.AddOrUpdate(input,rows);}}}",
        ["Asked"] = "{if(ReferenceEquals(sender,e.OriginalSource)&&e.OldFocusis{}old&&e.NewFocusis{}requested){"
            + "_directRequest=(newWeakReference<IInputElement>(old),newWeakReference<IInputElement>(requested));}}",
    };

    /// <summary>
    /// The focus guard in its exact shape (PR 4b codex round 2, F7; PR 8's
    /// precedent for its departure observer): <see cref="FocusGuardFile"/>
    /// registers exactly <see cref="FocusGuardRegistrations"/> — no other
    /// class handler, and those as the last statements of <c>Register()</c> —
    /// and its past-handled callbacks are exactly
    /// <see cref="FocusGuardCallbacks"/>. A wrong owner or callback, a
    /// registration more or elsewhere, a flipped <c>handledEventsToo</c>, or a
    /// recording callback that does more is an offender, whatever
    /// <see cref="ClassHandlerAllowed"/> names. Any other file: nothing.
    /// </summary>
    private static List<string> FocusGuardShapeOffenders(Unit unit)
    {
        var offenders = new List<string>();
        if (unit.Name != FocusGuardFile)
        {
            return offenders;
        }

        int registrations = unit.Root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Count(invocation => invocation.Expression switch
            {
                IdentifierNameSyntax identifier => identifier.Identifier.ValueText == "RegisterClassHandler",
                MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText == "RegisterClassHandler",
                _ => false,
            });
        MethodDeclarationSyntax[] register = unit.Root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Where(method => method.Identifier.ValueText == "Register")
            .ToArray();
        string[] registered = register is [{ Body: { } body }]
            ? body.Statements.TakeLast(FocusGuardRegistrations.Length).Select(statement => CSharpSource.Normalize(statement)).ToArray()
            : [];
        if (!registered.SequenceEqual(FocusGuardRegistrations) || registrations != FocusGuardRegistrations.Length)
        {
            offenders.Add($"{unit.Name}: the focus guard's class handlers are not exactly its intended registrations — "
                + $"{registrations} registration(s) in the file, and Register() ends [{string.Join(" ", registered)}]");
        }

        foreach ((string name, string expected) in FocusGuardCallbacks)
        {
            MethodDeclarationSyntax[] declared = unit.Root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Where(method => method.Identifier.ValueText == name)
                .ToArray();
            string actual = declared is [{ Body: { } callback }] ? CSharpSource.Normalize(callback) : "(not one method)";
            if (declared is not [{ } only] || !only.Modifiers.Any(SyntaxKind.StaticKeyword) || actual != expected)
            {
                offenders.Add($"{unit.Name}: the focus guard's callback {name} is not exactly its intended handler, "
                    + $"which only records — it is {actual}");
            }
        }

        return offenders;
    }

    /// <summary>W7-7 PR 8's departure observer (contract 40 OD-12) — with
    /// <see cref="FocusGuardFile"/>, one of the two files whose focus class
    /// handlers <see cref="ClassHandlerAllowed"/> names.</summary>
    private const string FocusObserverFile = "EditorLandingSlot.cs";

    /// <summary>The observer's owners: every input element kind, so a popup's
    /// and a context menu's focus is seen.</summary>
    private const string FocusObserverOwners = "new[]{typeof(UIElement),typeof(ContentElement),typeof(UIElement3D)}";

    /// <summary>The observer's registrations, the whole of its static
    /// constructor's one loop over <see cref="FocusObserverOwners"/> — two
    /// statements, six registrations at run time — each past handled, each to
    /// its own callback.</summary>
    private static readonly string[] FocusObserverRegistrations =
    [
        "EventManager.RegisterClassHandler(owner,Keyboard.GotKeyboardFocusEvent,newKeyboardFocusChangedEventHandler(OnGotKeyboardFocus),handledEventsToo:true);",
        "EventManager.RegisterClassHandler(owner,Keyboard.LostKeyboardFocusEvent,newKeyboardFocusChangedEventHandler(OnLostKeyboardFocus),handledEventsToo:true);",
    ];

    /// <summary>The observer's callbacks, whole: each hands the transition to
    /// the window slots and does nothing else.</summary>
    private static readonly Dictionary<string, string> FocusObserverCallbacks = new()
    {
        ["OnGotKeyboardFocus"] = "{if(ReferenceEquals(sender,e.NewFocus)){Transition(e.OldFocus,e.NewFocus);}}",
        ["OnLostKeyboardFocus"] = "{if(e.NewFocusisnull&&ReferenceEquals(sender,e.OldFocus)){Transition(e.OldFocus,null);}}",
    };

    /// <summary>
    /// The departure observer in its exact shape (codex on PR 8's merge with
    /// #1298): <see cref="FocusObserverFile"/> registers exactly
    /// <see cref="FocusObserverRegistrations"/> — no other class handler, and
    /// those only as the whole body of ONE loop over exactly
    /// <see cref="FocusObserverOwners"/> in its static constructor — and its
    /// callbacks are exactly <see cref="FocusObserverCallbacks"/>. A wrong
    /// owner or callback, a registration more, one not past handled, or a
    /// handler that does more is an offender, whatever
    /// <see cref="ClassHandlerAllowed"/> names. Any other file: nothing.
    /// </summary>
    private static List<string> FocusObserverShapeOffenders(Unit unit)
    {
        var offenders = new List<string>();
        if (unit.Name != FocusObserverFile)
        {
            return offenders;
        }

        int registrations = unit.Root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Count(invocation => invocation.Expression switch
            {
                IdentifierNameSyntax identifier => identifier.Identifier.ValueText == "RegisterClassHandler",
                MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText == "RegisterClassHandler",
                _ => false,
            });
        ForEachStatementSyntax[] loops = unit.Root.DescendantNodes()
            .OfType<ConstructorDeclarationSyntax>()
            .Where(constructor => constructor.Modifiers.Any(SyntaxKind.StaticKeyword))
            .SelectMany(constructor => constructor.DescendantNodes().OfType<ForEachStatementSyntax>())
            .Where(loop => loop.Identifier.ValueText == "owner"
                && CSharpSource.Normalize(loop.Expression) == FocusObserverOwners)
            .ToArray();
        string[] registered = loops is [{ Statement: BlockSyntax body }]
            ? body.Statements.Select(statement => CSharpSource.Normalize(statement)).ToArray()
            : [];
        if (!registered.SequenceEqual(FocusObserverRegistrations) || registrations != FocusObserverRegistrations.Length)
        {
            offenders.Add($"{unit.Name}: the departure observer's class handlers are not exactly its intended "
                + $"registrations — {registrations} registration(s) in the file, and the static constructor's loop "
                + $"over {FocusObserverOwners} registers [{string.Join(" ", registered)}]");
        }

        foreach ((string name, string expected) in FocusObserverCallbacks)
        {
            MethodDeclarationSyntax[] declared = unit.Root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Where(method => method.Identifier.ValueText == name)
                .ToArray();
            if (declared is not [{ Body: { } callback } only]
                || !only.Modifiers.Any(SyntaxKind.StaticKeyword)
                || CSharpSource.Normalize(callback) != expected)
            {
                offenders.Add($"{unit.Name}: the departure observer's callback {name} is not exactly its intended "
                    + "handler, which only hands the transition to the window slots");
            }
        }

        foreach ((string name, string expected) in FocusObserverTransition)
        {
            MethodDeclarationSyntax[] declared = unit.Root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Where(method => method.Identifier.ValueText == name)
                .ToArray();
            if (declared is not [{ Body: { } transition }] || CSharpSource.Normalize(transition) != expected)
            {
                offenders.Add($"{unit.Name}: the departure observer's {name} is not exactly its intended transition, "
                    + "which only withdraws or notes the held landing");
            }
        }

        return offenders;
    }

    /// <summary>The slots' half of the transition, whole: the observer hands
    /// it to each slot, and a slot withdraws or notes its held landing
    /// (OD-12). Instance methods, so pinned apart from the callbacks.</summary>
    private static readonly Dictionary<string, string> FocusObserverTransition = new()
    {
        ["Transition"] = "{if(t_slotsisnot{Count:>0}slots){return;}"
            + "foreach(EditorLandingSlotslotinslots.ToArray()){slot.OnTransition(from,to);}}",
        ["OnTransition"] = "{if(Heldisnot{}held){return;}"
            + "if(t_parking>0||(fromisnull?_restoringActivation:!CanHoldKeys(from))){return;}"
            + "if(held.Target()is{}target&&IsWithin(toasDependencyObject,target)&&(!held.Entered||IsTerminalSeatInto(target)))"
            + "{held.Entered=true;return;}_=Withdraw();}",
    };

    /// <summary>What <c>OnTransition</c> may call: the held landing's
    /// withdrawal and target, and the reads that decide between them.</summary>
    private static readonly string[] TransitionCallees = ["Withdraw", "CanHoldKeys", "IsWithin", "IsTerminalSeatInto", "Target"];

    /// <summary>
    /// <c>OnTransition</c>'s call graph, two calls deep from it, bound
    /// through the shell's compilation: it calls only
    /// <see cref="TransitionCallees"/>, and no method on the way touches
    /// <c>Keyboard</c>, executes an <c>ICommand</c>, reads an
    /// <c>_announce</c> funnel or builds an <c>A11yEvent</c>.
    /// </summary>
    private static List<string> TransitionCallGraphOffenders(CSharpCompilation compilation)
    {
        var offenders = new List<string>();
        SyntaxTree tree = compilation.SyntaxTrees.Single(candidate => Path.GetFileName(candidate.FilePath) == FocusObserverFile);
        SemanticModel model = compilation.GetSemanticModel(tree);
        MethodDeclarationSyntax root = tree.GetRoot().DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Single(method => method.Identifier.ValueText == "OnTransition");
        foreach (InvocationExpressionSyntax call in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (model.GetSymbolInfo(call).Symbol is not IMethodSymbol callee || !TransitionCallees.Contains(callee.Name))
            {
                offenders.Add($"OnTransition calls {CSharpSource.Normalize(call.Expression)}, which is none of its reviewed "
                    + $"callees ({string.Join(", ", TransitionCallees)})");
            }
        }

        WalkTransition(compilation, root, model, 0, "OnTransition", new HashSet<ISymbol>(SymbolEqualityComparer.Default), offenders);
        return offenders;
    }

    /// <summary>How deep the transition walk reads: <c>OnTransition</c> is
    /// 0; the held landing's delegates count as the body of the method that
    /// invokes them.</summary>
    private const int TransitionWalkDepth = 3;

    /// <summary>The held landing's delegate-backed members, by the
    /// constructor parameter whose delegate each invokes (WB-15 red team:
    /// <c>HeldEditorLanding.Withdraw()</c> is <c>_withdraw()</c>, so the walk
    /// follows the lambdas and method groups every
    /// <c>new HeldEditorLanding(…)</c> hands it).</summary>
    private static readonly Dictionary<string, (string Parameter, int Position)> HeldLandingDelegates = new()
    {
        ["Target"] = ("target", 0),
        ["Withdraw"] = ("withdraw", 2),
    };

    private static void WalkTransition(
        CSharpCompilation compilation, SyntaxNode body, SemanticModel model, int depth, string path, HashSet<ISymbol> visited, List<string> offenders)
    {
        foreach (SyntaxNode node in body.DescendantNodes())
        {
            string? effect = TransitionEffect(node, model);
            if (effect is not null)
            {
                offenders.Add($"{path} reaches {effect} — a focus transition only withdraws or notes the held landing");
            }

            if (depth < TransitionWalkDepth
                && node is InvocationExpressionSyntax invocation
                && model.GetSymbolInfo(invocation).Symbol is IMethodSymbol callee
                && visited.Add(callee.OriginalDefinition))
            {
                foreach (SyntaxReference reference in callee.OriginalDefinition.DeclaringSyntaxReferences)
                {
                    if (reference.GetSyntax() is MethodDeclarationSyntax or LocalFunctionStatementSyntax)
                    {
                        WalkTransition(
                            compilation, reference.GetSyntax(), compilation.GetSemanticModel(reference.SyntaxTree),
                            depth + 1, $"{path} → {callee.Name}", visited, offenders);
                    }
                }

                if (callee.ContainingType.Name == "HeldEditorLanding"
                    && HeldLandingDelegates.TryGetValue(callee.Name, out (string Parameter, int Position) handed))
                {
                    WalkHeldLandingDelegates(compilation, handed, depth + 1, $"{path} → {callee.Name}", visited, offenders);
                }
            }
        }
    }

    /// <summary>Every delegate handed to a <c>HeldEditorLanding</c> for
    /// <paramref name="handed"/>: a lambda's body, or a method group's
    /// declaration, walked as the delegate-backed member's own body.</summary>
    private static void WalkHeldLandingDelegates(
        CSharpCompilation compilation, (string Parameter, int Position) handed, int depth, string path, HashSet<ISymbol> visited, List<string> offenders)
    {
        foreach (SyntaxTree tree in compilation.SyntaxTrees.Where(tree => !tree.FilePath.EndsWith(".g.cs", StringComparison.Ordinal)))
        {
            SemanticModel model = compilation.GetSemanticModel(tree);
            foreach (BaseObjectCreationExpressionSyntax creation in tree.GetRoot().DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>())
            {
                if (model.GetTypeInfo(creation).Type?.Name != "HeldEditorLanding" || creation.ArgumentList is not { } arguments)
                {
                    continue;
                }

                ArgumentSyntax? argument = arguments.Arguments.FirstOrDefault(candidate => candidate.NameColon?.Name.Identifier.ValueText == handed.Parameter)
                    ?? (arguments.Arguments.Count > handed.Position && arguments.Arguments[handed.Position].NameColon is null
                        ? arguments.Arguments[handed.Position]
                        : null);
                if (argument is null)
                {
                    offenders.Add($"{Path.GetFileName(tree.FilePath)}:{Line(creation)} builds a HeldEditorLanding whose {handed.Parameter} the census cannot find");
                    continue;
                }

                string where = $"{path} ({Path.GetFileName(tree.FilePath)}:{Line(argument)})";
                if (argument.Expression is AnonymousFunctionExpressionSyntax lambda)
                {
                    WalkTransition(compilation, lambda, model, depth, where, visited, offenders);
                }
                else if (model.GetSymbolInfo(argument.Expression).Symbol is IMethodSymbol group)
                {
                    foreach (SyntaxReference reference in group.OriginalDefinition.DeclaringSyntaxReferences)
                    {
                        if (reference.GetSyntax() is MethodDeclarationSyntax declaration)
                        {
                            WalkTransition(
                                compilation, declaration, compilation.GetSemanticModel(reference.SyntaxTree),
                                depth, $"{where} → {group.Name}", visited, offenders);
                        }
                    }
                }
                else
                {
                    offenders.Add($"{where}: the {handed.Parameter} handed to a HeldEditorLanding is neither a lambda nor a method group");
                }
            }
        }
    }

    /// <summary>What a node does that a focus transition may not: move the
    /// keys (any <c>Keyboard</c> member, bound — <c>using static</c>
    /// included — or <c>Focus</c>, <c>MoveFocus</c>,
    /// <c>FocusManager.SetFocusedElement</c>), run a command, or
    /// speak.</summary>
    private static string? TransitionEffect(SyntaxNode node, SemanticModel model)
    {
        switch (node)
        {
            case MemberAccessExpressionSyntax access when access.Expression is IdentifierNameSyntax { Identifier.ValueText: "Keyboard" }:
                return $"Keyboard.{access.Name.Identifier.ValueText}";
            case IdentifierNameSyntax { Identifier.ValueText: "_announce" }:
                return "the _announce funnel";
            case BaseObjectCreationExpressionSyntax creation when IsA11yEvent(model.GetTypeInfo(creation).Type):
                return "an A11yEvent";
            case InvocationExpressionSyntax call when model.GetSymbolInfo(call).Symbol is IMethodSymbol method:
                if (method.ContainingType.Name is "Keyboard" or "FocusManager")
                {
                    return $"{method.ContainingType.Name}.{method.Name}";
                }

                if (method.Name is "Focus" or "MoveFocus")
                {
                    return $"{method.ContainingType.Name}.{method.Name}";
                }

                if (method.Name == "Execute"
                    && (method.ContainingType.Name == "ICommand" || method.ContainingType.AllInterfaces.Any(face => face.Name == "ICommand")))
                {
                    return "an ICommand's Execute";
                }

                return null;
            default:
                return null;
        }
    }

    private static bool IsA11yEvent(ITypeSymbol? type)
    {
        for (; type is not null; type = type.BaseType)
        {
            if (type.Name == "A11yEvent")
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The modal-loop monitor, whose one HWND hook reads WM_ENABLE
    /// to learn that a native modal loop disabled the shell.</summary>
    private const string MonitorFile = "ShellModalLoopMonitor.cs";

    private const string MonitorHookBody = "{if(message==WmEnable){Recompute();}returnIntPtr.Zero;}";

    /// <summary>Every <c>AddHook</c> in the shell — exactly the monitor's
    /// one, its <c>Hook</c> pinned whole.</summary>
    private static List<string> HwndHookOffenders(IEnumerable<Unit> shell)
    {
        var offenders = new List<string>();
        int hooks = 0;
        foreach (Unit unit in shell)
        {
            foreach (InvocationExpressionSyntax hook in unit.Root.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(invocation => invocation.Expression switch
                {
                    MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText == "AddHook",
                    MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText == "AddHook",
                    _ => false,
                }))
            {
                hooks++;
                if (unit.Name != MonitorFile
                    || hook.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText != "Attach"
                    || CSharpSource.Normalize(hook.ArgumentList) != "(Hook)")
                {
                    offenders.Add($"{unit.Name}:{Line(hook)} adds an HWND hook — it sees every message ahead of the window's routes");
                }
            }

            if (unit.Name == MonitorFile)
            {
                MethodDeclarationSyntax[] declared = unit.Root.DescendantNodes()
                    .OfType<MethodDeclarationSyntax>()
                    .Where(method => method.Identifier.ValueText == "Hook")
                    .ToArray();
                if (declared is not [{ Body: { } body }] || CSharpSource.Normalize(body) != MonitorHookBody)
                {
                    offenders.Add($"{unit.Name}: the modal-loop monitor's Hook is not exactly its WM_ENABLE read");
                }
            }
        }

        if (hooks != 1)
        {
            offenders.Add($"the shell adds {hooks} HWND hook(s); it adds exactly the modal-loop monitor's one");
        }

        return offenders;
    }

    /// <summary>Overrides on the window whose single parameter is a routed
    /// input's arguments.</summary>
    private static List<string> InputOverrideOffenders(IEnumerable<Unit> window)
    {
        Type[] inputArguments = typeof(InputEventArgs).Assembly.GetTypes()
            .Concat(typeof(AccessKeyPressedEventArgs).Assembly.GetTypes())
            .Where(type => typeof(InputEventArgs).IsAssignableFrom(type) || type == typeof(AccessKeyPressedEventArgs))
            .ToArray();
        var offenders = new List<string>();
        foreach (Unit unit in window)
        {
            foreach (MethodDeclarationSyntax method in unit.Root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Where(method => method.Modifiers.Any(SyntaxKind.OverrideKeyword)
                    && method.ParameterList.Parameters is [{ Type: { } parameter }]
                    && inputArguments.Any(type => type.Name == CSharpSource.Normalize(parameter).Split('.').Last())))
            {
                offenders.Add($"{unit.Name}:{Line(method)} overrides {method.Identifier.ValueText} — the window's class "
                    + "handling runs before its own preview handlers, so the admission cannot take that input first");
            }
        }

        return offenders;
    }

    /// <summary>The owned-window guard.</summary>
    private const string OwnedWindowGuardFile = "ShellSeal.cs";

    private static readonly Dictionary<string, string> OwnedWindowGuardBodies = new()
    {
        ["ShellOf"] = "Window.GetWindow(element)switch{MainWindowshell=>shell,"
            + "WindowwindowwhenAdmittedWindows.TryGetValue(window,out_)=>window.OwnerasMainWindow,_=>null,}",
        ["Take"] = "_=TakesFor((Window)sender,e)",
        ["TakeAccessKey"] = "{if(TakesFor((Window)sender,e)){e.Target=null;}}",
        ["TakesFor"] = "ShellOf(owned)?.SealTakes(input,inTheShellsWindow:false)??false",
    };

    /// <summary>
    /// The owned-window guard's shape, and every window the shell owns: see
    /// <see cref="TheOwnedWindowGuardHoldsEveryRouteTheShellsWindowHolds"/>.
    /// Bound through <paramref name="compilation"/>, so an <c>Owner</c>
    /// assignment is a <c>Window</c>'s, not another type's property of that
    /// name.
    /// </summary>
    private static List<string> OwnedWindowOffenders(CSharpCompilation compilation)
    {
        var offenders = new List<string>();
        SyntaxTree guard = compilation.SyntaxTrees.Single(tree => Path.GetFileName(tree.FilePath) == OwnedWindowGuardFile);
        var guardRoot = (CompilationUnitSyntax)guard.GetRoot();
        MethodDeclarationSyntax Method(string name) => guardRoot.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Single(method => method.Identifier.ValueText == name);

        foreach ((string name, string expected) in OwnedWindowGuardBodies)
        {
            MethodDeclarationSyntax[] declared = guardRoot.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Where(method => method.Identifier.ValueText == name)
                .ToArray();
            string actual = declared switch
            {
                [{ ExpressionBody: { } arrow }] => CSharpSource.Normalize(arrow.Expression),
                [{ Body: { } body }] => CSharpSource.Normalize(body),
                _ => "(not one method)",
            };
            if (actual != expected)
            {
                offenders.Add($"{OwnedWindowGuardFile}: {name} is not exactly its reviewed text — it is {actual}");
            }
        }

        MethodDeclarationSyntax admit = Method("Admit");
        string[] subscribed = admit.DescendantNodes()
            .OfType<AssignmentExpressionSyntax>()
            .Where(assignment => assignment.IsKind(SyntaxKind.AddAssignmentExpression))
            .Select(assignment => (CSharpSource.Normalize(assignment.Left), CSharpSource.Normalize(assignment.Right)))
            .Select(pair => pair.Item2 == "Take" && pair.Item1.StartsWith("owned.", StringComparison.Ordinal)
                ? pair.Item1["owned.".Length..]
                : $"{pair.Item1}+={pair.Item2}")
            .ToArray();
        if (!subscribed.OrderBy(route => route, StringComparer.Ordinal).SequenceEqual(RequiredGates.OrderBy(route => route, StringComparer.Ordinal)))
        {
            offenders.Add($"{OwnedWindowGuardFile}: Admit subscribes [{string.Join(", ", subscribed)}] — exactly "
                + $"[{string.Join(", ", RequiredGates)}], each to Take, the shell's own window's routes");
        }

        string[] accessKeys = admit.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(invocation => CSharpSource.Normalize(invocation.Expression) == "owned.AddHandler")
            .Select(invocation => CSharpSource.Normalize(invocation.ArgumentList))
            .ToArray();
        if (accessKeys is not ["(AccessKeyManager.AccessKeyPressedEvent,newAccessKeyPressedEventHandler(TakeAccessKey),true)"])
        {
            offenders.Add($"{OwnedWindowGuardFile}: Admit's access-key gate is not exactly one past-handled registration to TakeAccessKey");
        }

        foreach (InvocationExpressionSyntax write in guardRoot.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(invocation => invocation.Expression is MemberAccessExpressionSyntax
            {
                Expression: IdentifierNameSyntax { Identifier.ValueText: "AdmittedWindows" },
                Name.Identifier.ValueText: not "TryGetValue",
            }))
        {
            if (!ReferenceEquals(write.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault(), admit))
            {
                offenders.Add($"{OwnedWindowGuardFile}:{Line(write)} writes AdmittedWindows outside Admit");
            }
        }

        // Every window the shell shows: a modeless one (the shell's own
        // window aside) admitted first, in the method that shows it; a modal
        // one never. Show and ShowDialog are bound through IOperation, so a
        // conditional call and a method group are seen; a window the census
        // cannot trace to a local or field admitted beside its Show is an
        // offender, not a pass (WB-15 red team).
        foreach (SyntaxTree tree in compilation.SyntaxTrees.Where(tree => tree.FilePath.EndsWith(".cs", StringComparison.Ordinal)
            && !tree.FilePath.EndsWith(".g.cs", StringComparison.Ordinal)))
        {
            SemanticModel model = compilation.GetSemanticModel(tree);
            foreach (SyntaxNode node in tree.GetRoot().DescendantNodes().Where(node => node is InvocationExpressionSyntax
                || (node is MemberAccessExpressionSyntax && node.Parent is not InvocationExpressionSyntax)))
            {
                (IMethodSymbol? shown, IOperation? receiver) = model.GetOperation(node) switch
                {
                    IInvocationOperation call => (call.TargetMethod, call.Instance),
                    IMethodReferenceOperation reference => (reference.Method, reference.Instance),
                    _ => (null, null),
                };
                if (shown is not { Name: "Show" or "ShowDialog", Parameters.Length: 0 } || !IsWindow(shown.ContainingType))
                {
                    continue;
                }

                if (receiver is IConditionalAccessInstanceOperation)
                {
                    IOperation? up = receiver.Parent;
                    while (up is not null and not IConditionalAccessOperation)
                    {
                        up = up.Parent;
                    }

                    receiver = (up as IConditionalAccessOperation)?.Operation;
                }

                if (receiver?.Type is { } receiverType && IsMainWindow(receiverType))
                {
                    continue;
                }

                SyntaxNode? method = node.Ancestors().FirstOrDefault(ancestor => ancestor is BaseMethodDeclarationSyntax or AccessorDeclarationSyntax);
                ISymbol? window = receiver switch
                {
                    ILocalReferenceOperation local => local.Local,
                    IFieldReferenceOperation field => field.Field,
                    _ => null,
                };
                string where = $"{Path.GetFileName(tree.FilePath)}:{Line(node)} {shown.Name}";
                if (window is null || method is null)
                {
                    offenders.Add($"{where}: the census cannot trace the window shown here to a local or field, so it "
                        + "cannot see that the seal holds in it — show it from the method that builds and admits it");
                    continue;
                }

                InvocationExpressionSyntax[] admits = method.DescendantNodes()
                    .OfType<InvocationExpressionSyntax>()
                    .Where(invocation => model.GetSymbolInfo(invocation).Symbol is IMethodSymbol { Name: "Admit", ContainingType.Name: "ShellSeal" }
                        && invocation.ArgumentList.Arguments is [{ } argument]
                        && SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(argument.Expression).Symbol, window))
                    .ToArray();
                if (shown.Name == "ShowDialog")
                {
                    if (admits.Length > 0)
                    {
                        offenders.Add($"{where} {window.Name}: a modal dialog is admitted — it is the prompt the seal waits on");
                    }

                    continue;
                }

                if (admits is not [{ } admitted] || admitted.SpanStart > node.SpanStart)
                {
                    offenders.Add($"{where} {window.Name}: a modeless window is shown without ShellSeal.Admit before it, "
                        + "so the seal does not hold in it");
                    continue;
                }

                SyntaxNode? earlier = method.DescendantNodes()
                    .Where(candidate => candidate.SpanStart < admitted.SpanStart)
                    .FirstOrDefault(candidate =>
                        (candidate is AssignmentExpressionSyntax { Left: MemberAccessExpressionSyntax target } assignment
                            && assignment.IsKind(SyntaxKind.AddAssignmentExpression)
                            && SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(target.Expression).Symbol, window))
                        || (candidate is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "AddHandler" } added }
                            && SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(added.Expression).Symbol, window)));
                if (earlier is not null)
                {
                    offenders.Add($"{where} {window.Name}: a subscription on the window (line {Line(earlier)}) runs ahead of "
                        + "ShellSeal.Admit, so it acts before the seal's check");
                }
            }
        }

        return offenders;
    }

    private static bool IsMainWindow(ITypeSymbol? type)
    {
        for (; type is not null; type = type.BaseType)
        {
            if (type.Name == "MainWindow")
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsWindow(ITypeSymbol? type)
    {
        for (; type is not null; type = type.BaseType)
        {
            if (type.ToDisplayString() == "System.Windows.Window")
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The shell's compilation with <paramref name="file"/>'s
    /// tree replaced: its one <paramref name="anchor"/> becomes
    /// <paramref name="replacement"/>.</summary>
    private static CSharpCompilation PlantInto(string file, string anchor, string replacement)
    {
        CSharpCompilation compilation = ShellCompilation.Compilation;
        SyntaxTree tree = compilation.SyntaxTrees.Single(candidate => Path.GetFileName(candidate.FilePath) == file);
        return compilation.ReplaceSyntaxTree(
            tree,
            CSharpSyntaxTree.ParseText(PlantedText(file, anchor, replacement), (CSharpParseOptions)tree.Options, path: tree.FilePath));
    }

    private static string PlantedText(string file, string anchor, string replacement)
    {
        string real = File.ReadAllText(Path.Combine(SourceText.ShellSourceRoot(), file));
        Assert.True(real.Split(anchor).Length == 2, $"the planted row no longer finds its one anchor in {file} — update the row");
        return real.Replace(anchor, replacement, StringComparison.Ordinal);
    }

    /// <summary>What acts on a double-click ahead of the seal's class handler:
    /// an override of <c>Control</c>'s double-click virtuals (it runs before
    /// the event is raised), or a click-count read the census has not
    /// reviewed.</summary>
    private static List<string> DoubleClickOffenders(IEnumerable<Unit> shell)
    {
        var offenders = new List<string>();
        foreach (Unit unit in shell)
        {
            foreach (MethodDeclarationSyntax method in unit.Root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Where(method => method.Modifiers.Any(SyntaxKind.OverrideKeyword)
                    && method.Identifier.ValueText is "OnMouseDoubleClick" or "OnPreviewMouseDoubleClick"))
            {
                offenders.Add($"{unit.Name}:{Line(method)} overrides {method.Identifier.ValueText} — Control calls it for a "
                    + "double-click rebuilt from a press the admission took, before the gate sees the event");
            }

            foreach (MemberAccessExpressionSyntax read in unit.Root.DescendantNodes()
                .OfType<MemberAccessExpressionSyntax>()
                .Where(access => access.Name.Identifier.ValueText == "ClickCount"))
            {
                string method = read.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText
                    ?? "a non-method member";
                if (!ClickCountAllowed.ContainsKey((unit.Name, method)))
                {
                    offenders.Add($"{unit.Name}:{Line(read)} reads a click count in {method} — review whether a press "
                        + "the admission took can reach it, then name it in ClickCountAllowed");
                }
            }
        }

        return offenders;
    }

    /// <summary>Class handlers the shell registers today, none of which carry
    /// input. #1515's <c>RealizedRowHold</c> hears a virtualizing panel ask
    /// before it recycles a container, which only its own layout pass
    /// raises.</summary>
    private static bool IsKnownNonInput(string routed) =>
        routed is "FrameworkElement.LoadedEvent" or "TextBoxBase.TextChangedEvent"
            or "VirtualizingStackPanel.CleanUpVirtualizedItemEvent";

    private static bool PastHandled(InvocationExpressionSyntax invocation)
    {
        SeparatedSyntaxList<ArgumentSyntax> arguments = invocation.ArgumentList.Arguments;
        return arguments.Count >= 3
            && arguments.Any(argument => argument.Expression is LiteralExpressionSyntax literal
                && literal.IsKind(SyntaxKind.TrueLiteralExpression)
                && (argument.NameColon is null
                    ? arguments.IndexOf(argument) == 2
                    : argument.NameColon.Name.Identifier.ValueText == "handledEventsToo"));
    }

    private static IEnumerable<Unit> ShellUnits() =>
        Directory.EnumerateFiles(SourceText.ShellSourceRoot(), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => new Unit(Path.GetFileName(path), CSharpSource.LoadPath(path).Root));

    private static IEnumerable<Unit> WindowUnits() =>
        Directory.EnumerateFiles(SourceText.ShellSourceRoot(), "MainWindow*.cs", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => new Unit(Path.GetFileName(path), CSharpSource.LoadPath(path).Root));

    private static CompilationUnitSyntax Parse(string source) =>
        (CompilationUnitSyntax)CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview)).GetRoot();

    private static int Line(SyntaxNode node) => node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
}
