// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace SlateWindows.Tests;

/// <summary>
/// ONE compilation over the shell's own sources for the censuses that must
/// BIND rather than read spellings (W6-2 PR B's post-implementation passes,
/// IPB-4, IPB-8, IPB-9): the shell's trees, the SDK's implicit global
/// usings, and the test project's recorded compile references — the
/// framework, WPF, the packages, the binding — everything but the shell's
/// own assembly, whose types the sources declare. The references come from
/// the build (#1600), so every census binds the same types whatever ran
/// before it. The XAML-generated partials are added when a build left them
/// under obj/; without them a few members stay unbound, so a census asks
/// for what it needs and says when binding yields nothing. Built once per
/// test process.
/// </summary>
internal static class ShellCompilation
{
    private static readonly Lazy<(IReadOnlyList<(string Relative, CSharpSource Source)> Sources, CSharpCompilation Compilation)> Built = new(Build);

    internal static IReadOnlyList<(string Relative, CSharpSource Source)> Sources => Built.Value.Sources;

    internal static CSharpCompilation Compilation => Built.Value.Compilation;

    internal static SemanticModel ModelFor(CSharpSource source) =>
        Compilation.GetSemanticModel(source.Root.SyntaxTree);

    /// <summary>The recorded references minus the shell's own assembly, whose
    /// types the sources declare. Exactly one <c>SlateWindows.dll</c> must be
    /// among them: a list without it is not the test project's.</summary>
    internal static string[] SelectReferences(IReadOnlyList<string> recorded)
    {
        string[] shell = recorded.Where(IsShellAssembly).ToArray();
        Assert.True(shell.Length == 1,
            "The shell compilation expects exactly one SlateWindows.dll among the test project's recorded "
            + $"references (evidence-compilation/SlateWindows.Tests/references.txt), and found {shell.Length}. "
            + "Build SlateWindows.Tests.csproj in the active configuration to regenerate the list.");
        return recorded.Where(path => !IsShellAssembly(path)).ToArray();
    }

    private static bool IsShellAssembly(string path) =>
        string.Equals(Path.GetFileName(path), "SlateWindows.dll", StringComparison.OrdinalIgnoreCase);

    private static (IReadOnlyList<(string Relative, CSharpSource Source)>, CSharpCompilation) Build()
    {
        string shellRoot = SourceText.ShellSourceRoot();
        var sources = new List<(string Relative, CSharpSource Source)>();
        foreach (string file in Directory.EnumerateFiles(shellRoot, "*.cs", SearchOption.AllDirectories)
            .OrderBy(f => f, StringComparer.Ordinal))
        {
            string relative = Path.GetRelativePath(shellRoot, file).Replace('\\', '/');
            if (relative.StartsWith("obj/", StringComparison.Ordinal) || relative.Contains("/obj/", StringComparison.Ordinal))
            {
                continue;
            }
            sources.Add((relative, CSharpSource.LoadPath(file)));
        }
        // The SDK's implicit usings live in a generated file under obj/;
        // parsed with the sources' own options so the trees agree on a
        // language version.
        var parseOptions = (CSharpParseOptions)sources[0].Source.Root.SyntaxTree.Options;
        SyntaxTree globalUsings = CSharpSyntaxTree.ParseText(
            "global using System;\nglobal using System.Collections.Generic;\nglobal using System.IO;\n"
            + "global using System.Linq;\nglobal using System.Net.Http;\nglobal using System.Threading;\n"
            + "global using System.Threading.Tasks;\n",
            parseOptions,
            path: "GlobalUsings.census.cs");
        // The XAML-generated partials — the x:Name fields, InitializeComponent —
        // live under obj/ after a build; the newest copy of each is added so
        // a call through an x:Name field BINDS instead of failing closed
        // (codex post-implementation pass 3, IPB-17).
        var generated = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string objRoot = Path.Combine(shellRoot, "obj");
        if (Directory.Exists(objRoot))
        {
            foreach (string file in Directory.EnumerateFiles(objRoot, "*.g.cs", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(file);
                if (!generated.TryGetValue(name, out string? existing)
                    || File.GetLastWriteTimeUtc(file) > File.GetLastWriteTimeUtc(existing))
                {
                    generated[name] = file;
                }
            }
        }
        IEnumerable<SyntaxTree> generatedTrees = generated.Values
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), parseOptions, path: path));
        // #1600: the test project's compile references as its build recorded
        // them, never the assemblies this process happens to have loaded.
        // Which census builds the compilation first depends on the test
        // order, and xunit v3 orders differently by runner pool: built from
        // the loaded assemblies, an early first build bound no WPF or
        // System.ObjectModel types for every census after it.
        MetadataReference[] references = SelectReferences(TestEvidenceCompilation.RecordedReferences())
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToArray();
        var compilation = CSharpCompilation.Create(
            "shell-census",
            sources.Select(s => s.Source.Root.SyntaxTree).Append(globalUsings).Concat(generatedTrees),
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable,
                allowUnsafe: true));
        return (sources, compilation);
    }
}
