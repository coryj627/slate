// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SlateWindows.Tests;

/// <summary>
/// #1600: the one compilation every binding census shares binds the same
/// types whatever ran before it. It used to take its references from
/// <c>AppDomain.CurrentDomain.GetAssemblies()</c> when the first census built
/// it, so a census that ran before anything loaded WPF or
/// <c>System.ObjectModel</c> fixed a compilation where those types never
/// bound. xunit v3's test order (#1555) made that the first census on a
/// Namespace runner, and four censuses failed on #1599. The references now
/// come from the list the test project's build records
/// (<c>evidence-compilation/SlateWindows.Tests/references.txt</c>), which no
/// test order, filter or runner pool can change.
/// </summary>
public class ShellCompilationTests
{
    [Fact]
    public void ItsReferencesAreTheBuildsRecordedReferencesNotTheAssembliesTheProcessHasLoaded()
    {
        string[] expected = TestEvidenceCompilation.RecordedReferences()
            .Where(path => !string.Equals(Path.GetFileName(path), "SlateWindows.dll", StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFullPath)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        string[] actual = ShellCompilation.Compilation.References
            .Select(reference => Path.GetFullPath(Assert.IsAssignableFrom<PortableExecutableReference>(reference).FilePath!))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.NotEmpty(expected);
        Assert.Equal(expected, actual, StringComparer.OrdinalIgnoreCase);
    }

    [Theory]
    // WPF's element tree, its input and controls, and the collection the
    // shell's item hosts hold.
    [InlineData("System.Windows.UIElement")]
    [InlineData("System.Windows.Input.Keyboard")]
    [InlineData("System.Windows.Controls.ItemsControl")]
    [InlineData("System.Collections.ObjectModel.ObservableCollection`1")]
    // The packages and the project the shell compiles against.
    [InlineData("ICSharpCode.AvalonEdit.TextEditor")]
    [InlineData("WpfMath.Parsers.WpfTeXFormulaParser")]
    [InlineData("uniffi.slate_uniffi.A11yEvent")]
    public void EveryLibraryTheShellCompilesAgainstBinds(string metadataName) =>
        Assert.NotEmpty(ShellCompilation.Compilation.GetTypesByMetadataName(metadataName));

    [Theory]
    // The two hosts ItemContainerNameCensus read as "(an unreadable element
    // type)" on #1599's run: ObservableCollection<T> did not bind, so its
    // IEnumerable<T> was out of reach.
    [InlineData("Canvas/CanvasOutlineView.cs", "SlateWindows.Canvas.CanvasOutlineRowViewModel")]
    [InlineData("Graph/ConnectionsLeafView.cs", "SlateWindows.Graph.ConnectionsRowViewModel")]
    public void TheItemHostsRootsReadAsTheirRowType(string relative, string row)
    {
        CSharpSource source = ShellCompilation.Sources.Single(entry => entry.Relative == relative).Source;
        VariableDeclaratorSyntax roots = source.Root.DescendantNodes().OfType<VariableDeclaratorSyntax>()
            .Single(variable => variable.Identifier.ValueText == "_roots");
        var field = Assert.IsAssignableFrom<IFieldSymbol>(ShellCompilation.ModelFor(source).GetDeclaredSymbol(roots));
        var type = Assert.IsAssignableFrom<INamedTypeSymbol>(field.Type);

        Assert.NotEqual(TypeKind.Error, type.TypeKind);
        INamedTypeSymbol enumerable = Assert.Single(type.AllInterfaces,
            candidate => candidate.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>");
        Assert.Equal(row, enumerable.TypeArguments[0].ToDisplayString());
    }

    /// <summary>Plan 47 CB-15a (#1890): the compilation binds with the shell's
    /// own generated global usings and no others. A census tree of its own
    /// once added <c>System.IO</c> and <c>System.Net.Http</c>, which the app
    /// does not have, and made <c>Path</c> ambiguous with WPF's
    /// <c>System.Windows.Shapes.Path</c> in three shell sources. Every tree
    /// is a shell source or a file the build generated in the configuration
    /// these tests were built in (as ShellPromptOwnerCensus reads them), and
    /// none is one of the <c>_wpftmp</c> copies the markup compile leaves
    /// behind: a stale copy, or another configuration's newer build, would
    /// bind names the shell no longer has.</summary>
    [Fact]
    public void ItsGlobalUsingsAreExactlyTheShellsGeneratedOnes()
    {
        string separator = Path.DirectorySeparatorChar.ToString();
        string shellRoot = Path.GetFullPath(SourceText.ShellSourceRoot()) + separator;
        string configuration = AppContext.BaseDirectory.Contains($"{separator}Debug{separator}", StringComparison.OrdinalIgnoreCase)
            ? "Debug"
            : "Release";
        string objRoot = Path.Combine(shellRoot, "obj");
        string ownObj = Path.Combine(objRoot, configuration) + separator;
        string generated = Directory.EnumerateFiles(ownObj, "SlateWindows.GlobalUsings.g.cs", SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .First();
        string[] expected = GlobalUsings(CSharpSyntaxTree.ParseText(File.ReadAllText(generated)));
        string[] actual = [.. ShellCompilation.Compilation.SyntaxTrees
            .SelectMany(GlobalUsings)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

        Assert.NotEmpty(expected);
        Assert.True(
            expected.SequenceEqual(actual, StringComparer.Ordinal),
            $"the shell's generated usings, {generated}, are [{string.Join(", ", expected)}]; the compilation binds "
            + $"[{string.Join(", ", actual)}]");
        string[] strays = [.. ShellCompilation.Compilation.SyntaxTrees
            .Select(tree => Path.GetFullPath(tree.FilePath))
            .Where(path => !path.StartsWith(shellRoot, StringComparison.OrdinalIgnoreCase)
                || (path.StartsWith(objRoot + separator, StringComparison.OrdinalIgnoreCase)
                    && (!path.StartsWith(ownObj, StringComparison.OrdinalIgnoreCase)
                        || Path.GetFileName(path).Contains("_wpftmp", StringComparison.OrdinalIgnoreCase))))];
        Assert.True(
            strays.Length == 0,
            $"the compilation binds {strays.Length} tree(s) that are neither a shell source nor a file the {configuration} "
            + $"build generated (the usings file it should follow is {generated}):\n" + string.Join("\n", strays));
    }

    private static string[] GlobalUsings(SyntaxTree tree) =>
    [
        .. ((CompilationUnitSyntax)tree.GetRoot()).Usings
            .Where(directive => directive.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword))
            .Select(directive => directive.NamespaceOrType.ToString())
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal),
    ];

    [Fact]
    public void TheShellsOwnAssemblyIsLeftOutAndMustBeRecordedExactlyOnce()
    {
        Assert.Equal(
            ["C:/packs/PresentationFramework.dll", "C:/repo/SlateUniffi.dll"],
            ShellCompilation.SelectReferences(
                ["C:/packs/PresentationFramework.dll", "C:/repo/refint/SlateWindows.dll", "C:/repo/SlateUniffi.dll"]));
        // A list without the shell is not the test project's list.
        Assert.ThrowsAny<Exception>(() => ShellCompilation.SelectReferences(["C:/packs/PresentationFramework.dll"]));
        Assert.ThrowsAny<Exception>(() => ShellCompilation.SelectReferences(
            ["C:/one/SlateWindows.dll", "C:/two/slatewindows.dll", "C:/packs/PresentationFramework.dll"]));
    }

    [Fact]
    public void AMissingReferenceListFailsWithTheRebuildInstruction()
    {
        string empty = Directory.CreateTempSubdirectory("shell-compilation-").FullName;
        try
        {
            Exception failure = Assert.ThrowsAny<Exception>(() => TestEvidenceCompilation.RecordedReferences(empty));
            Assert.Contains("missing references.txt", failure.Message, StringComparison.Ordinal);
            Assert.Contains("Build SlateWindows.Tests.csproj", failure.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(empty, recursive: true);
        }
    }
}
