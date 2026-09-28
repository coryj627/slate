// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// #1275 (codex round 6, owner decision): while the palette is sealed — open
// or closed — only Escape acts, and ONE check says so: MainWindow.SealTakes.
// The window is the root of every input route in the shell's tree, so its
// preview handlers run before any element's, and a key, text or pointer
// press the check marks handled reaches no later handler, no input binding,
// no access key and no menu mode. The census keeps that true as the shell
// grows: the seal is read nowhere but the check; every input handler the
// window declares, in XAML or in code, is the check and nothing else (the
// key route calls it first and returns); and nothing in the shell listens
// past a handled input or ahead of the routed events, where the check could
// not reach it.

using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
        [("SlateTextEditor.cs", "TextCompositionManager.PreviewTextInputStartEvent")] =
            "IME composition tracking only (IsComposing); a taken key never reaches the IME",
        [("SlateTextEditor.cs", "TextCompositionManager.TextInputEvent")] =
            "IME composition tracking only (IsComposing); a taken key never reaches the IME",
    };

    /// <summary>Class handlers for routed events that carry no key, text or
    /// pointer, each with why it cannot act on input the admission took
    /// (file-scoped: the same registration anywhere else is an offender).</summary>
    private static readonly Dictionary<(string File, string RoutedEvent), string> ClassHandlerAllowed = new()
    {
        [("EditorLandingSlot.cs", "Keyboard.GotKeyboardFocusEvent")] = FocusDepartureObserver,
        [("EditorLandingSlot.cs", "Keyboard.LostKeyboardFocusEvent")] = FocusDepartureObserver,
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
            "if(senderisDependencyObjectelement&&GetWindow(element)isMainWindowshell){_=shell.SealTakes(e);}",
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
    public void EachDetectorCatchesItsBypass(string bypass, string source)
    {
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
            _ => PastHandledOffenders([new Unit("Other.cs", Parse(source))]),
        };
        Assert.True(found.Count > 0, $"the census missed a planted {bypass} bypass");

        // And the planted handlers alone are clean, so the finding above is
        // the bypass, not the scaffold.
        Assert.Empty(SealReaderOffenders([handlers]));
    }

    private static List<string> SealReaderOffenders(IEnumerable<Unit> shell)
    {
        var offenders = new List<string>();
        foreach (Unit unit in shell.Where(unit => unit.Name != "CommandPaletteViewModel.cs"))
        {
            foreach (SyntaxNode read in unit.Root.DescendantNodes().Where(IsSealReference))
            {
                string? method = read.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText;
                if (method != Admission || unit.Name != "MainWindow.Seal.cs")
                {
                    offenders.Add($"{unit.Name}:{Line(read)} reads the seal in {method ?? "a non-method member"}");
                }
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
                    if (!IsKnownNonInput(routed) && !theDoubleClickGate && !ClassHandlerAllowed.ContainsKey((unit.Name, routed)))
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
        }

        return offenders;
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
    /// input.</summary>
    private static bool IsKnownNonInput(string routed) =>
        routed is "FrameworkElement.LoadedEvent" or "TextBoxBase.TextChangedEvent";

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
