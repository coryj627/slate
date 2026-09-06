// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace SlateWindows.Tests;

/// <summary>
/// ONE compilation over the shell's own sources for the censuses that must
/// BIND rather than read spellings (W6-2 PR B's post-implementation passes,
/// IPB-4, IPB-8, IPB-9): the shell's trees, the SDK's implicit global
/// usings, and the metadata the test process has loaded — the framework,
/// WPF, the binding — everything but the shell's own assembly, whose types
/// the sources declare. The XAML-generated partials are absent, so a few
/// members stay unbound; a census asks for what it needs and says when
/// binding yields nothing. Built once per test process.
/// </summary>
internal static class ShellCompilation
{
    private static readonly Lazy<(IReadOnlyList<(string Relative, CSharpSource Source)> Sources, CSharpCompilation Compilation)> Built = new(Build);

    internal static IReadOnlyList<(string Relative, CSharpSource Source)> Sources => Built.Value.Sources;

    internal static CSharpCompilation Compilation => Built.Value.Compilation;

    internal static SemanticModel ModelFor(CSharpSource source) =>
        Compilation.GetSemanticModel(source.Root.SyntaxTree);

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
        var references = new List<MetadataReference>();
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.IsDynamic || string.IsNullOrEmpty(assembly.Location) || !File.Exists(assembly.Location))
            {
                continue;
            }
            if (string.Equals(assembly.GetName().Name, "SlateWindows", StringComparison.Ordinal))
            {
                continue;
            }
            references.Add(MetadataReference.CreateFromFile(assembly.Location));
        }
        var compilation = CSharpCompilation.Create(
            "shell-census",
            sources.Select(s => s.Source.Root.SyntaxTree).Append(globalUsings),
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable,
                allowUnsafe: true));
        return (sources, compilation);
    }
}
