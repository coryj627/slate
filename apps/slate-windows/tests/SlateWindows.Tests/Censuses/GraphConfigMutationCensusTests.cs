// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace SlateWindows.Tests.Censuses;

public sealed class GraphConfigMutationCensusTests
{
    private const string StoreSource = """
        using System;
        using System.IO;
        namespace SlateWindows.Graph
        {
            internal sealed class GraphConfigStore
            {
                internal const string FileName = "graph.json";
                private readonly string _path;
                public GraphConfigStore(string root) { _path = Path.Combine(root, ".slate", FileName); }
                public string FilePath => _path;
                public void Write()
                {
                    string temporary = _path + ".tmp";
                    File.WriteAllText(temporary, "{}");
                    File.Move(temporary, _path, true);
                    SlateWindows.Cleanup.Remove(temporary);
                }
            }
        }
        namespace SlateWindows
        {
            internal static class Cleanup
            {
                internal static void Remove(string path) { File.Delete(path); }
            }
        }
        """;

    private static SyntaxTree Parse(string source, string path) =>
        CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview), path);

    private static GraphConfigMutationCensus.Report Inspect(string source, string storeSource = StoreSource)
    {
        SyntaxTree[] trees = [Parse(storeSource, "Graph/GraphConfigStore.cs"), Parse(source, "OutsideGraph/Writer.cs")];
        CSharpCompilation compilation = CSharpCompilation.Create("shell-census", trees,
            ShellCompilation.Compilation.References, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        Diagnostic[] errors = [.. compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)];
        Assert.True(errors.Length == 0, string.Join("\n", errors.Select(error => error.ToString())));
        return GraphConfigMutationCensus.Inspect(compilation, trees);
    }

    private static string Writer(string body, string members = "", string usings = "") => $$"""
        using System;
        using System.IO;
        using SlateWindows.Graph;
        {{usings}}
        namespace SlateWindows;
        internal static class OtherWriter
        {
            internal static void Run(GraphConfigStore store)
            {
                {{body}}
            }
            {{members}}
        }
        """;

    [Fact]
    public void RealWholeShellRejectsAnOutsideGraphWriterWithoutAnyNewFileNameLiteral()
    {
        SyntaxTree mutant = Parse(Writer("System.IO.File.AppendAllText(store.FilePath, \"planted writer\");"), "OutsideGraph/PlantedWriter.cs");
        CSharpCompilation compilation = ShellCompilation.Compilation.AddSyntaxTrees(mutant);
        GraphConfigMutationCensus.Report report = GraphConfigMutationCensus.Inspect(compilation,
            ShellCompilation.Sources.Select(file => file.Source.Root.SyntaxTree).Append(mutant));
        Assert.Contains(report.Violations, violation => violation.Contains("OutsideGraph/PlantedWriter.cs:Run", StringComparison.Ordinal)
            && violation.Contains("File.AppendAllText", StringComparison.Ordinal));
        Assert.DoesNotContain(report.Violations, violation => violation.Contains("graph.json is named outside", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("File.WriteAllBytes(store.FilePath, []);")]
    [InlineData("global::System.IO.File.AppendAllLines(store.FilePath, new[] { \"text\" });")]
    [InlineData("IO.File.WriteAllTextAsync(store.FilePath, \"text\");", "using IO = System.IO;")]
    [InlineData("Disk.Delete(store.FilePath);", "using Disk = System.IO.File;")]
    [InlineData("AppendAllText(store.FilePath, \"text\");", "using static System.IO.File;")]
    [InlineData("File.Copy(\"unrelated.txt\", store.FilePath, true);")]
    [InlineData("File.Move(store.FilePath, \"unrelated.txt\");")]
    [InlineData("File.Replace(\"new.txt\", \"old.txt\", store.FilePath);")]
    [InlineData("using var writer = new global::System.IO.StreamWriter(store.FilePath);")]
    [InlineData("using var stream = new FileStream(store.FilePath, FileMode.Open);")]
    [InlineData("using var stream = File.OpenWrite(store.FilePath);")]
    [InlineData("using var handle = File.OpenHandle(store.FilePath, access: FileAccess.Write);")]
    [InlineData("new FileInfo(store.FilePath).Delete();")]
    [InlineData("using var writer = new FileInfo(store.FilePath).AppendText();")]
    [InlineData("new FileInfo(\"unrelated.txt\").CopyTo(store.FilePath);")]
    [InlineData("new FileInfo(store.FilePath).IsReadOnly = true;")]
    [InlineData("using var stream = new FileStream(store.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.DeleteOnClose);")]
    [InlineData("using var handle = File.OpenHandle(store.FilePath); RandomAccess.SetLength(handle, 0);")]
    public void QualifiedAliasedAndAlternateMutationApisCannotEscape(string body, string usings = "")
    {
        GraphConfigMutationCensus.Report report = Inspect(Writer(body, usings: usings));
        Assert.Contains(report.Violations, violation => violation.Contains("outside GraphConfigStore.Write's call context", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("string path = store.FilePath; string copy = path; File.WriteAllText(copy, \"text\");", "")]
    [InlineData("Write(Identity(store.FilePath));", "private static string Identity(string path) => path; private static void Write(string path) => File.Delete(path);")]
    [InlineData("Write(Find(store));", "private static string Find(GraphConfigStore store) => store.FilePath; private static void Write(string path) => File.Delete(path);")]
    [InlineData("string path = string.Empty; if (DateTime.Now.Ticks > 0) path = store.FilePath; File.Delete(path);", "")]
    [InlineData("string path = Path.Combine(\"root\", \".slate\", GraphConfigStore.FileName); File.Delete(path);", "")]
    [InlineData("string path = $\"{store.FilePath}.replacement.tmp\"; Cleanup.Remove(path);", "")]
    [InlineData("void Remove(string path) { File.Delete(path); } Remove(store.FilePath);", "")]
    [InlineData("var info = new FileInfo(store.FilePath); FileInfo alias = info; alias.Create();", "")]
    [InlineData("var box = new Box(store.FilePath); box.Remove();", "private sealed class Box { private readonly string _path; internal Box(string path) { _path = path; } internal void Remove() => File.Delete(_path); }")]
    [InlineData("Find(store, out string path); File.Delete(path);", "private static void Find(GraphConfigStore store, out string path) => path = store.FilePath;")]
    [InlineData("string path = string.Empty; Find(store, ref path); File.Delete(path);", "private static void Find(GraphConfigStore store, ref string path) => path = store.FilePath;")]
    [InlineData("string[] paths = [store.FilePath]; File.Delete(paths[0]);", "")]
    [InlineData("var paths = (store.FilePath, Other: \"other.txt\"); File.Delete(paths.Item1);", "")]
    [InlineData("foreach (string path in new[] { store.FilePath }) File.Delete(path);", "")]
    [InlineData("WriteAsync(store);", "private static async System.Threading.Tasks.Task WriteAsync(GraphConfigStore store) { string path = await System.Threading.Tasks.Task.FromResult(store.FilePath); File.Delete(path); }")]
    [InlineData("File.Delete(Name);", "private static string Name { get; } = GraphConfigStore.FileName;")]
    [InlineData("store.FilePath.Remove();", "private static void Remove(this string path) => File.Delete(path);")]
    [InlineData("Action<string> erase = File.Delete; erase(store.FilePath);", "")]
    [InlineData("var box = new Box(); box.Set(store.FilePath); box.Remove();", "private sealed class Box { private string _path = \"other.txt\"; internal void Set(string path) { _path = path; } internal void Remove() => File.Delete(_path); }")]
    [InlineData("string[] paths = new string[1]; paths[0] = store.FilePath; File.Delete(paths[0]);", "")]
    [InlineData("var paths = new System.Collections.Generic.List<string>(); paths.Add(store.FilePath); File.Delete(paths[0]);", "")]
    [InlineData("var box = new PathBox(store.FilePath); File.Delete(box.Path);", "private readonly record struct PathBox(string Path);")]
    [InlineData("var paths = new string[1]; var alias = paths; alias[0] = store.FilePath; File.Delete(paths[0]);", "")]
    [InlineData("var box = new Box(); var alias = box; alias.Set(store.FilePath); box.Remove();", "private sealed class Box { private string _path = \"other.txt\"; internal void Set(string path) { _path = path; } internal void Remove() => File.Delete(_path); }")]
    [InlineData("Action remove = Remove; remove();", "private static GraphConfigStore _store = new(\"root\"); private static void Remove() => File.Delete(_store.FilePath);")]
    [InlineData("Func<string> path = Find; File.Delete(path());", "private static GraphConfigStore _store = new(\"root\"); private static string Find() => _store.FilePath;")]
    [InlineData("WriteAsync(store);", "private static async System.Threading.Tasks.Task WriteAsync(GraphConfigStore store) { string path = await System.Threading.Tasks.Task.Run(() => store.FilePath); File.Delete(path); }")]
    [InlineData("var box = new Box(); Set(box, store.FilePath); File.Delete(box.Path);", "private sealed class Box { internal string Path = \"other.txt\"; } private static void Set(Box box, string path) => box.Path = path;")]
    [InlineData("Set(store.FilePath); File.Delete(_path);", "private static string _path = \"other.txt\"; private static void Set(string path) => _path = path;")]
    [InlineData("File.Delete(Make(store.FilePath).Path);", "private sealed class Box { public string Path { get; set; } = \"other.txt\"; } private static Box Make(string path) => new Box { Path = path };")]
    [InlineData("FileInfo? info = new FileInfo(store.FilePath); info?.Delete();", "")]
    [InlineData("var paths = new System.Collections.Generic.List<string> { store.FilePath }; File.Delete(paths[0]);", "")]
    public void PathProvenanceSurvivesLocalsHelpersAndInstanceWrappers(string body, string members)
    {
        Assert.NotEmpty(Inspect(Writer(body, members)).Violations);
    }

    [Fact]
    public void SharedCleanupDoesNotGrantAnotherCallerTheWritersAuthority()
    {
        Assert.Empty(Inspect(Writer("store.Write();")).Violations);
        GraphConfigMutationCensus.Report report = Inspect(Writer("Cleanup.Remove(store.FilePath + \".tmp\");"));
        Assert.Contains(report.Violations, violation => violation.Contains("Cleanup", StringComparison.Ordinal)
            || violation.Contains("File.Delete", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("File.WriteAllText(\"unrelated.txt\", \"text\");")]
    [InlineData("string text = File.ReadAllText(store.FilePath); File.WriteAllText(\"backup.txt\", text);")]
    [InlineData("File.Copy(store.FilePath, \"backup.txt\");")]
    [InlineData("new FileInfo(store.FilePath).CopyTo(\"backup.txt\");")]
    [InlineData("using var stream = File.OpenRead(store.FilePath); stream.ReadByte();")]
    [InlineData("using var reader = new StreamReader(store.FilePath); reader.ReadToEnd();")]
    [InlineData("using var stream = new FileStream(store.FilePath, FileMode.Open, FileAccess.Read);")]
    [InlineData("using var stream = File.Open(store.FilePath, FileMode.Open, FileAccess.Read);")]
    [InlineData("using var handle = File.OpenHandle(store.FilePath);")]
    [InlineData("Directory.Exists(store.FilePath);")]
    [InlineData("string directory = Path.GetDirectoryName(store.FilePath)!; File.WriteAllText(Path.Combine(directory, \"notes.txt\"), \"text\");")]
    public void UnrelatedWritesAndReadOnlyGraphAccessRemainAllowed(string body)
    {
        GraphConfigMutationCensus.Report report = Inspect(Writer(body));
        Assert.True(report.Violations.Count == 0, string.Join("\n", report.Violations));
    }

    [Theory]
    [InlineData("Contains")]
    [InlineData("IndexOf")]
    public void ReadOnlyListQueriesDoNotStoreTheirSearchPath(string query)
    {
        string body = $$"""
            var paths = new System.Collections.Generic.List<string> { "other.txt" };
            paths.{{query}}(store.FilePath);
            File.Delete(paths[0]);
            """;
        GraphConfigMutationCensus.Report allowed = Inspect(Writer(body));
        Assert.True(allowed.Violations.Count == 0, string.Join("\n", allowed.Violations));
        foreach (string mutationCall in new[] { "paths.Add(store.FilePath);", "paths.Insert(1, store.FilePath);" })
        {
            string mutation = body.Replace($"paths.{query}(store.FilePath);", mutationCall, StringComparison.Ordinal)
                .Replace("paths[0]", "paths[1]", StringComparison.Ordinal);
            Assert.Contains(Inspect(Writer(mutation)).Violations,
                violation => violation.Contains("outside GraphConfigStore.Write's call context", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void ASourceTypeCannotBorrowTheFrameworkListQueryException()
    {
        const string sourceType = """
            namespace System.Collections.Generic
            {
                internal sealed class List<T>
                {
                    internal extern bool Contains(T item);
                    internal string Path { get; } = "other.txt";
                }
            }
            """;
        string source = Writer("var paths = new System.Collections.Generic.List<string>(); paths.Contains(store.FilePath); File.Delete(paths.Path);");
        Assert.NotEmpty(Inspect(source, StoreSource + sourceType).Violations);
    }

    [Fact]
    public void OtherDirectoryEffectsStillRequireClassificationWhenTheyReceiveAGraphPath()
    {
        Assert.Contains(Inspect(Writer("Directory.Delete(store.FilePath);")).Violations,
            violation => violation.Contains("unclassified graph-path API System.IO.Directory.Delete", StringComparison.Ordinal));
        Assert.Empty(Inspect(Writer("Directory.Delete(\"other-directory\");")).Violations);
    }

    [Theory]
    [InlineData("private sealed class Writer : StreamWriter { internal Writer(string path) : base(path) { } }")]
    [InlineData("private sealed class Writer(string path) : StreamWriter(path);")]
    [InlineData("private sealed class Writer : StreamWriter { internal Writer(string path) : this(path, true) { } private Writer(string path, bool unused) : base(path) { } }")]
    public void WritingConstructorInitializersCannotHideAStreamOpen(string members)
    {
        Assert.NotEmpty(Inspect(Writer("using var writer = new Writer(store.FilePath);", members)).Violations);
    }

    [Theory]
    [InlineData("private sealed class Reader : StreamReader { internal Reader(string path) : base(path) { } }")]
    [InlineData("private sealed class Reader(string path) : StreamReader(path);")]
    public void ReadOnlyConstructorInitializersRemainAllowed(string members)
    {
        GraphConfigMutationCensus.Report report = Inspect(Writer("using var reader = new Reader(store.FilePath);", members));
        Assert.True(report.Violations.Count == 0, string.Join("\n", report.Violations));
    }

    [Fact]
    public void IntrinsicGraphMembersDoNotTaintUnrelatedPathsOnTheirOwner()
    {
        const string members = """
            private sealed class Box
            {
                private readonly string _graph;
                private readonly string _other = "other.txt";
                internal Box(GraphConfigStore store)
                {
                    _graph = store.FilePath;
                    File.Delete(_other);
                }
            }
            """;
        GraphConfigMutationCensus.Report allowed = Inspect(Writer("new Box(store);", members));
        Assert.True(allowed.Violations.Count == 0, string.Join("\n", allowed.Violations));
        string mutation = members.Replace("File.Delete(_other)", "File.Delete(_graph)", StringComparison.Ordinal);
        Assert.NotEmpty(Inspect(Writer("new Box(store);", mutation)).Violations);
    }

    [Fact]
    public void APrivateFieldBasedCleanupInheritsOnlyItsActualCallersAuthority()
    {
        string helper = StoreSource.Replace("SlateWindows.Cleanup.Remove(temporary);", "RemoveTemporary();", StringComparison.Ordinal)
            .Replace("public void Write()", "private void RemoveTemporary() => File.Delete(_path + \".tmp\"); public void Write()", StringComparison.Ordinal);
        GraphConfigMutationCensus.Report allowed = Inspect(Writer(""), helper);
        Assert.True(allowed.Violations.Count == 0, string.Join("\n", allowed.Violations));
        string shared = helper.Replace("public void Write()", "public void Wrong() => RemoveTemporary(); public void Write()", StringComparison.Ordinal);
        Assert.NotEmpty(Inspect(Writer(""), shared).Violations);
    }

    [Fact]
    public void TheOneFileNameMustStillBeExactlyGraphJson()
    {
        string altered = StoreSource.Replace("FileName = \"graph.json\"", "FileName = \"graph.json.backup\"", StringComparison.Ordinal);
        Assert.NotEmpty(Inspect(Writer(""), altered).Violations);
    }

    [Theory]
    [InlineData("string name = \"graph.json\";")]
    [InlineData("string name = \"C:/vault/.slate/graph.json\";")]
    [InlineData("string name = \"graph\" + \".json\";")]
    public void SecondFileNameLiteralsAreRejectedEvenWithoutAMutation(string body)
    {
        Assert.Contains(Inspect(Writer(body)).Violations,
            violation => violation.Contains("graph.json is named outside", StringComparison.Ordinal));
    }
}
