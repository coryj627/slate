// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Reflection;
using System.Security.Cryptography;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace SlateWindows.Tests;

/// <summary>Build-recorded test inputs for evidence binding. Source changes
/// after a build fail explicitly instead of silently examining another build's
/// symbols, generated files, or preprocessor branches.</summary>
internal static class TestEvidenceCompilation
{
    private static readonly Lazy<TestEvidence[]> Built = new(Build);

    internal static IReadOnlyList<TestEvidence> Projects => Built.Value;

    private static TestEvidence[] Build()
    {
        string local = Path.Combine(AppContext.BaseDirectory, "evidence-compilation", "SlateWindows.Tests");
        string configuration = ReadOne(local, "configuration.txt");
        Assert.Equal(typeof(TestEvidenceCompilation).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration,
            configuration);
        string framework = ReadOne(local, "framework.txt");
        string accessibility = Path.Combine(SourceText.RepoRoot(), "apps", "slate-windows", "tests",
            "SlateWindows.AccessibilityTests", "bin", configuration, framework,
            "evidence-compilation", "SlateWindows.AccessibilityTests");
        return [Load(local), Load(accessibility)];
    }

    internal static TestEvidence Load(string directory)
    {
        string project = ReadOne(directory, "project-directory.txt");
        string[] currentSources = Directory.EnumerateFiles(project, "*.cs", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(project, path).Replace('\\', '/').Split('/').Any(segment => segment is "obj" or "bin"))
            .Order(StringComparer.Ordinal).ToArray();
        Assert.True(ReadLines(directory, "source-tree.txt").Order(StringComparer.Ordinal).SequenceEqual(currentSources),
            Rebuild(directory, "source files were added or removed"));
        string[] fingerprints = ReadLines(directory, "source-hashes.txt");
        Assert.NotEmpty(fingerprints);
        var recorded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string fingerprint in fingerprints)
        {
            string[] parts = fingerprint.Split('|');
            Assert.True(parts.Length == 2 && File.Exists(parts[0]), Rebuild(directory, $"missing input {parts[0]}"));
            using var file = File.OpenRead(parts[0]);
            Assert.True(string.Equals(parts[1], Convert.ToHexString(SHA256.HashData(file)), StringComparison.OrdinalIgnoreCase),
                Rebuild(directory, $"changed input {parts[0]}"));
            recorded.Add(parts[0]);
        }
        Assert.True(LanguageVersionFacts.TryParse(ReadOne(directory, "language.txt"), out LanguageVersion version),
            Rebuild(directory, "unrecognized compiler language version"));
        var options = new CSharpParseOptions(version, preprocessorSymbols: ReadLines(directory, "defines.txt"));
        string[] paths = ReadLines(directory, "sources.txt");
        Assert.NotEmpty(paths);
        Assert.True(paths.All(recorded.Contains), Rebuild(directory, "sources lack fingerprints"));
        var trees = paths.Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), options, path)).ToArray();
        string[] referencePaths = ReadLines(directory, "references.txt");
        Assert.True(referencePaths.All(recorded.Contains), Rebuild(directory, "references lack fingerprints"));
        var references = referencePaths
            .Select(path => MetadataReference.CreateFromFile(path)).ToArray();
        var compilation = CSharpCompilation.Create(ReadOne(directory, "assembly.txt"), trees, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable, allowUnsafe: true));
        return new TestEvidence(compilation);
    }

    private static string ReadOne(string directory, string name) => Assert.Single(ReadLines(directory, name));

    private static string[] ReadLines(string directory, string name)
    {
        string path = Path.Combine(directory, name);
        Assert.True(File.Exists(path), Rebuild(directory, $"missing {name}"));
        return File.ReadAllLines(path).Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
    }

    private static string Rebuild(string directory, string reason) =>
        $"Evidence compilation inputs are missing or stale ({reason}). Build SlateWindows.Tests.csproj "
        + $"in the active configuration to regenerate both test manifests. Manifest: {directory}";
}
