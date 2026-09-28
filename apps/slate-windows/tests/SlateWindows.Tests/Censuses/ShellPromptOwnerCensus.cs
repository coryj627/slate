// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// #1275 (contract 28 I11, #1298): a message box never enters WPF's
// thread-modal state; the palette's seal sees it only because it disables
// its OWNER, the shell. CI's Windows session is non-interactive, so no fact
// there can raise a native box, and the rule that makes each box the
// shell's is pinned here — on Roslyn's semantic model of the shell's source,
// not on text:
//   * no message box in the shell is ownerless;
//   * every owned box, raised directly or through the close prompt's display
//     seam, is owned by `this` typed MainWindow — or, inside a named
//     owner-taking helper, by that helper's own (non-nullable) owner
//     parameter;
//   * every call of every such helper hands it the shell (`this`,
//     MainWindow).

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "shell-prompt-owner")]
public sealed class ShellPromptOwnerCensus
{
    /// <summary>The close prompt's display seam, which stands where its
    /// message box does (MainWindow.ShowUnsavedClosePrompt).</summary>
    private const string DisplaySeam = "ShowUnsavedClosePrompt";

    /// <summary>The helpers that raise a box for an owner they are handed,
    /// by containing type and name. Each owner parameter is non-nullable
    /// and every call hands it the shell.</summary>
    private static readonly (string Type, string Method)[] OwnerTakingHelpers =
    [
        ("WorkspaceViewModel", "ShowHistoryAlert"),
        ("WorkspaceViewModel", "AskBasesExportScope"),
    ];

    private static readonly CSharpParseOptions Options = new(LanguageVersion.Preview);

    private static readonly Lazy<ImmutableArray<MetadataReference>> References = new(() =>
    {
        string own = typeof(MainWindow).Assembly.Location;
        string tests = typeof(ShellPromptOwnerCensus).Assembly.Location;
        return
        [
            .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
                .Concat(Directory.EnumerateFiles(AppContext.BaseDirectory, "*.dll"))
                .Where(path => !string.Equals(path, own, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(path, tests, StringComparison.OrdinalIgnoreCase)
                    && IsManaged(path))
                .DistinctBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path)),
        ];
    });

    [Fact]
    public void EveryMessageBoxTheShellRaisesIsOwnedByTheShell()
    {
        CSharpCompilation shell = Compile(
            Directory.EnumerateFiles(SourceText.ShellSourceRoot(), "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                    && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .Concat(GeneratedShellSources())
                .Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), Options, path)));

        // The model must be whole: an invocation that failed to bind would
        // be skipped silently (a delegate seam with no Func in scope is "not
        // invocable", and escaped this census once).
        Diagnostic[] errors = [.. shell.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)];
        Assert.True(
            errors.Length == 0,
            $"the shell's semantic model has {errors.Length} error(s), so the census cannot see every call:\n  "
            + string.Join("\n  ", errors.Take(10).Select(error => error.ToString())));

        (List<string> offenders, int boxes, Dictionary<string, int> helperCalls) = Offenders(shell);
        Assert.True(boxes >= 10, $"the census found {boxes} message boxes — the semantic model is broken");
        Assert.True(
            offenders.Count == 0,
            "message boxes the shell raises without the shell as owner (a box disables only its owner, so the "
            + "palette's seal would not see it — contract 28 I11):\n  " + string.Join("\n  ", offenders));
        foreach ((string type, string method) in OwnerTakingHelpers)
        {
            Assert.True(
                helperCalls.GetValueOrDefault($"{type}.{method}") > 0,
                $"the shell no longer calls {type}.{method} — drop it from OwnerTakingHelpers");
        }
    }

    /// <summary>The detector on planted sources: an ownerless box; a helper
    /// that is not named, called with null; a named helper handed null, or
    /// a window that is not the shell; a box whose owner is merely NAMED
    /// "owner"; and the shipped shapes, which pass.</summary>
    [Theory]
    [InlineData("class Other { void M() { MessageBox.Show(\"text\"); } }", 1)]
    [InlineData("class Other { static void Warn(Window? owner) { if (owner is null) { MessageBox.Show(\"t\"); return; } MessageBox.Show(owner, \"t\"); } void M() { Warn(null); } }", 2)]
    [InlineData("class Other { void M() { WorkspaceViewModel.ShowHistoryAlert(null!, \"t\", \"m\"); } }", 1)]
    [InlineData("class Other { void M(Window owner) { MessageBox.Show(owner, \"t\"); } }", 1)]
    [InlineData("partial class MainWindow { void M() { WorkspaceViewModel.ShowHistoryAlert(this, \"t\", \"m\"); MessageBox.Show(this, \"t\"); } }", 0)]
    [InlineData("partial class MainWindow { void M() { WorkspaceViewModel.ShowHistoryAlert(new MainWindow(), \"t\", \"m\"); } }", 1)]
    public void TheDetectorRejectsEveryPromptTheShellDoesNotOwn(string planted, int expected) =>
        Assert.Equal(expected, Offenders(Planted(nullableOwner: false, planted)).Offenders.Count);

    /// <summary>A named helper whose owner could be null is itself rejected:
    /// its box would be ownerless the day a caller passed null.</summary>
    [Fact]
    public void ANamedHelperWhoseOwnerCanBeNullIsRejected() =>
        Assert.Single(
            Offenders(Planted(nullableOwner: true, "partial class MainWindow { void M() { WorkspaceViewModel.ShowHistoryAlert(this, \"t\", \"m\"); } }")).Offenders,
            offender => offender.Contains("nullable", StringComparison.Ordinal));

    private static CSharpCompilation Planted(bool nullableOwner, string planted)
    {
        string stubs = $$"""
            using System.Windows;
            partial class MainWindow : Window { }
            class WorkspaceViewModel
            {
                internal static void ShowHistoryAlert(Window{{(nullableOwner ? "?" : string.Empty)}} owner, string title, string message) =>
                    _ = MessageBox.Show(owner!, message, title);
            }
            """;
        CSharpCompilation compilation = Compile(
        [
            CSharpSyntaxTree.ParseText(stubs, Options, "Stubs.cs"),
            CSharpSyntaxTree.ParseText("using System.Windows;\n" + planted, Options, "Planted.cs"),
        ]);
        Assert.Empty(compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        return compilation;
    }

    /// <summary>The sources the build generates for the shell — its implicit
    /// global usings and its XAML partials (InitializeComponent, named
    /// elements) — from the build configuration these tests were built in,
    /// so the model sees what the compiler saw.</summary>
    private static IEnumerable<string> GeneratedShellSources()
    {
        string configuration = AppContext.BaseDirectory.Contains($"{Path.DirectorySeparatorChar}Debug{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
            ? "Debug"
            : "Release";
        string root = Path.Combine(SourceText.ShellSourceRoot(), "obj", configuration);
        string globals = Directory.EnumerateFiles(root, "SlateWindows.GlobalUsings.g.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains("wpftmp", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"no generated global usings under {root} — build the shell first");
        return Directory.EnumerateFiles(Path.GetDirectoryName(globals)!, "*.g.cs", SearchOption.AllDirectories);
    }

    private static CSharpCompilation Compile(IEnumerable<SyntaxTree> trees) =>
        CSharpCompilation.Create(
            "prompt-owner-census",
            trees,
            References.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

    private static (List<string> Offenders, int Boxes, Dictionary<string, int> HelperCalls) Offenders(CSharpCompilation compilation)
    {
        var offenders = new List<string>();
        var helperCalls = new Dictionary<string, int>(StringComparer.Ordinal);
        int boxes = 0;
        foreach (SyntaxTree tree in compilation.SyntaxTrees)
        {
            SemanticModel model = compilation.GetSemanticModel(tree);
            SyntaxNode root = tree.GetRoot();
            foreach (MethodDeclarationSyntax declaration in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                if (model.GetDeclaredSymbol(declaration) is IMethodSymbol declared
                    && NamedHelper(declared) is { } named
                    && declared.Parameters.FirstOrDefault() is { } owner
                    && (owner.Type.Name != "Window" || owner.NullableAnnotation == NullableAnnotation.Annotated))
                {
                    offenders.Add($"{Where(tree, declaration)} {named}'s owner is nullable or not a window");
                }
            }

            foreach (InvocationExpressionSyntax invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                string where = Where(tree, invocation);
                SymbolInfo info = model.GetSymbolInfo(invocation);
                ISymbol? callee = info.Symbol ?? info.CandidateSymbols.FirstOrDefault();
                ISymbol? target = model.GetSymbolInfo(invocation.Expression).Symbol;
                SeparatedSyntaxList<ArgumentSyntax> arguments = invocation.ArgumentList.Arguments;

                if (callee is IMethodSymbol { Name: "Show", ContainingType.Name: "MessageBox" } box
                    && box.ContainingNamespace.ToDisplayString() == "System.Windows")
                {
                    boxes++;
                    bool takesAnOwner = box.Parameters.Length > 0 && box.Parameters[0].Type.Name == "Window";
                    if (!takesAnOwner)
                    {
                        offenders.Add($"{where} MessageBox.Show without an owner");
                    }
                    else if (!IsTheShell(model, arguments[0].Expression)
                        && !IsTheHelpersOwner(model, invocation, arguments[0].Expression))
                    {
                        offenders.Add($"{where} MessageBox.Show's owner is not the shell");
                    }

                    continue;
                }

                if (target is IPropertySymbol { Name: DisplaySeam })
                {
                    boxes++;
                    if (arguments.Count == 0 || !IsTheShell(model, arguments[0].Expression))
                    {
                        offenders.Add($"{where} {DisplaySeam}'s owner is not the shell");
                    }

                    continue;
                }

                if (callee is IMethodSymbol method && NamedHelper(method) is { } helper)
                {
                    helperCalls[helper] = helperCalls.GetValueOrDefault(helper) + 1;
                    if (arguments.Count == 0 || !IsTheShell(model, arguments[0].Expression))
                    {
                        offenders.Add($"{where} {helper} is not handed the shell");
                    }
                }
            }
        }

        return (offenders, boxes, helperCalls);
    }

    private static string Where(SyntaxTree tree, SyntaxNode node) =>
        $"{Path.GetFileName(tree.FilePath)}:{node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}";

    /// <summary><c>this</c>, typed MainWindow.</summary>
    private static bool IsTheShell(SemanticModel model, ExpressionSyntax argument) =>
        argument is ThisExpressionSyntax && model.GetTypeInfo(argument).Type?.Name == "MainWindow";

    /// <summary>Inside a named owner-taking helper, that helper's own owner
    /// parameter (optionally null-forgiven).</summary>
    private static bool IsTheHelpersOwner(SemanticModel model, SyntaxNode invocation, ExpressionSyntax argument)
    {
        ExpressionSyntax owner = argument is PostfixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.SuppressNullableWarningExpression } forgiven
            ? forgiven.Operand
            : argument;
        return invocation.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault() is { } declaration
            && model.GetDeclaredSymbol(declaration) is IMethodSymbol helper
            && NamedHelper(helper) is not null
            && model.GetSymbolInfo(owner).Symbol is IParameterSymbol parameter
            && SymbolEqualityComparer.Default.Equals(parameter, helper.Parameters.FirstOrDefault());
    }

    private static string? NamedHelper(IMethodSymbol method) =>
        OwnerTakingHelpers.Any(helper => helper.Type == method.ContainingType?.Name && helper.Method == method.Name)
            ? $"{method.ContainingType!.Name}.{method.Name}"
            : null;

    /// <summary>The test output also holds native libraries (the Rust core),
    /// which carry no metadata to reference.</summary>
    private static bool IsManaged(string path)
    {
        try
        {
            _ = System.Reflection.AssemblyName.GetAssemblyName(path);
            return true;
        }
        catch (BadImageFormatException)
        {
            return false;
        }
    }
}
