// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Security.Cryptography;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace SlateWindows.Tests;

public sealed class TestEvidenceTests
{
    // All mutants reuse metadata; only their small source tree is compiled.
    private static readonly MetadataReference[] References =
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
        .Append(typeof(FactAttribute).Assembly.Location).Distinct(StringComparer.OrdinalIgnoreCase)
        .Select(path => MetadataReference.CreateFromFile(path)).ToArray();

    private static TestEvidence Evidence(string members, string otherTypes = "", string[]? symbols = null, string globalTypes = "")
    {
        string source = """
            using System;
            using System.Collections.Generic;
            using System.Diagnostics;
            using System.Threading.Tasks;
            using Xunit;
            namespace SlateWindows.AccessibilityTests {
            public class ShellAccessibilityTests {
                private static Process process;
                internal static void AssertAxeClean(Process process, string surface) { }
            """ + "\n" + members + "\n}\n" + otherTypes + "\n}\n" + globalTypes;
        SyntaxTree tree = CSharpSyntaxTree.ParseText(source,
            new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: symbols ?? []), "/tests/Journeys.cs");
        CSharpCompilation compilation = CSharpCompilation.Create("evidence-mutant", [tree], References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        Assert.Empty(compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        return new(compilation);
    }

    [Theory]
    [InlineData("// public void Journey() { }", "")]
    [InlineData("private const string Text = \"public void Journey() { }\";", "")]
    [InlineData("public void Journey() { }", "")]
    [InlineData("[Fact] private void Journey() { }", "")]
    [InlineData("[Fact(Skip = \"not run\")] public void Journey() { }", "")]
    [InlineData("[Fact] public void Journey(int missing) { }", "")]
    [InlineData("[Theory] public void Journey(int missing) { }", "")]
    [InlineData("[Theory, InlineData()] public void Journey(int missing) { }", "")]
    [InlineData("[Theory, InlineData(\"wrong type\")] public void Journey(int value) { }", "")]
    [InlineData("[Theory, MemberData(\"Missing\")] public void Journey(int value) { }", "")]
    [InlineData("[Theory, MemberData(nameof(Rows))] public void Journey(int value) { } public IEnumerable<object[]> Rows => new[] { new object[] { 1 } };", "")]
    [InlineData("[LookalikeFact] public void Journey() { }", "class LookalikeFactAttribute : Attribute { }")]
    [InlineData("[CustomFact] public void Journey() { }", "class CustomFactAttribute : FactAttribute { public CustomFactAttribute() { Skip = \"not run\"; } }")]
    [InlineData("private ShellAccessibilityTests() { } [Fact] public void Journey() { }", "")]
    [InlineData("private ShellAccessibilityTests() { } [Fact] public static void Journey() { }", "")]
    [InlineData("public ShellAccessibilityTests() { } public ShellAccessibilityTests(int value) { } [Fact] public void Journey() { }", "")]
    [InlineData("", "public abstract class AbstractTests { [Fact] public void Journey() { } }")]
    public void NonExecutableNamesDoNotCertifyMethodsClassesOrFiles(string members, string otherTypes)
    {
        TestEvidence evidence = Evidence(members, otherTypes);
        Assert.False(evidence.HasTestEvidence("Journey"));
        Assert.False(evidence.HasTestEvidence("ShellAccessibilityTests"));
        Assert.False(evidence.HasTestEvidence("Journeys"));
    }

    [Fact]
    public void BuildSymbolsChooseTheActualPreprocessorBranch()
    {
        const string source = """
            #if ACTIVE_JOURNEY
            [Fact] public void Journey() { AssertAxeClean(process, "graph-table"); }
            #else
            public void Journey() { AssertAxeClean(process, "graph-table"); }
            #endif
            """;
        TestEvidence inactive = Evidence(source);
        Assert.False(inactive.HasTestEvidence("Journey"));
        Assert.False(inactive.HasAxeLabel("graph-table"));
        TestEvidence active = Evidence(source, symbols: ["ACTIVE_JOURNEY"]);
        Assert.True(active.HasTestEvidence("Journey"));
        Assert.True(active.HasAxeLabel("graph-table"));
    }

    [Theory]
    [InlineData("[Fact] public void Journey() { /* AssertAxeClean(process, \"graph-table\"); */ }")]
    [InlineData("[Fact] public void Journey() { string text = \"\"\"AssertAxeClean(process, \"graph-table\")\"\"\"; }")]
    [InlineData("[Fact] public void Journey() { } private void Unused() { AssertAxeClean(process, \"graph-table\"); }")]
    [InlineData("[Fact] public void Journey() { if (false) AssertAxeClean(process, \"graph-table\"); }")]
    [InlineData("[Fact] public void Journey() { while (false) AssertAxeClean(null, \"graph-table\"); }")]
    [InlineData("[Fact] public void Journey() { return; AssertAxeClean(null, \"graph-table\"); }")]
    [InlineData("[Fact] public void Journey() { void Unused() => AssertAxeClean(null, \"graph-table\"); }")]
    [InlineData("[Fact] public void Journey() { Action unused = () => AssertAxeClean(null, \"graph-table\"); }")]
    [InlineData("[Fact] public void Journey() { Store(() => AssertAxeClean(null, \"graph-table\")); } private void Store(Action unused) { }")]
    [InlineData("[Fact] public void Journey() { Scan(0); } private void Scan(int unused) { } private void Scan() { AssertAxeClean(null, \"graph-table\"); }")]
    [InlineData("[Fact] public void Journey() { int process = 0; AssertAxeClean(process, \"graph-table\"); } private void AssertAxeClean(int process, string surface) { }")]
    [InlineData("[Fact] public void Journey() { Scan(false); } private void Scan(bool enabled) { if (enabled) AssertAxeClean(null, \"graph-table\"); }")]
    [InlineData("[Fact] public void Journey() { Scan(false); } private void Scan(bool enabled) { if (!enabled) return; AssertAxeClean(null, \"graph-table\"); }")]
    [InlineData("[Fact] public void Journey() { Scan(); } [Conditional(\"DISABLED_SCAN\")] private void Scan() { AssertAxeClean(null, \"graph-table\"); }")]
    [InlineData("[Fact] public void Journey() { Scan(\"graph-table\"); } private void Scan(string label) { label = \"other\"; AssertAxeClean(null, label); }")]
    [InlineData("[Fact] public void Journey() { Run(() => AssertAxeClean(null, \"graph-table\")); } private void Run(Action callback) { callback = () => { }; callback(); }")]
    public void UnreachableOrWronglyBoundAxeTextCannotCertifyALabel(string members)
    {
        Assert.False(Evidence(members).HasAxeLabel("graph-table"));
    }

    [Theory]
    [InlineData("[Fact] public void Journey() { AssertAxeClean(null, \"graph-table\"); }")]
    [InlineData("[global::Xunit.Fact] public Task Journey() { AssertAxeClean(null, \"graph-table\"); return Task.CompletedTask; }")]
    [InlineData("[Theory, InlineData(1)] public void Journey(int value) { AssertAxeClean(null, \"graph-table\"); }")]
    [InlineData("[Fact] public void Journey() { Scan(); } private void Scan() { const string label = \"graph-\" + \"table\"; AssertAxeClean(null, label); }")]
    [InlineData("[Fact] public void Journey() { Scan(\"graph-table\"); } private void Scan(string label) { AssertAxeClean(surface: label, process: null); }")]
    [InlineData("[Fact] public void Journey() { void Scan() => AssertAxeClean(null, \"graph-table\"); Scan(); }")]
    [InlineData("[Fact] public void Journey() { Run(() => AssertAxeClean(null, \"graph-table\")); } private void Run(Action callback) { callback(); }")]
    [InlineData("[Fact] public void Journey() { Run(Scan); } private void Run(Action callback) { callback(); } private void Scan() { AssertAxeClean(null, \"graph-table\"); }")]
    public void BoundFactsTheoriesAndCalledHelpersCertifyTheirActualLabel(string members)
    {
        TestEvidence evidence = Evidence(members);
        Assert.True(evidence.HasTestEvidence("Journey"));
        Assert.True(evidence.HasTestEvidence("ShellAccessibilityTests"));
        Assert.True(evidence.HasTestEvidence("Journeys"));
        Assert.True(evidence.HasAxeLabel("graph-table"));
        Assert.False(evidence.HasAxeLabel("graph-connections"));
    }

    [Fact]
    public void AnOverrideCannotCertifyTheBaseImplementationsUnusedScan()
    {
        TestEvidence evidence = Evidence("[Fact] public void Journey() { Base value = new Derived(); value.Scan(); }", """
            public class Base { public virtual void Scan() { ShellAccessibilityTests.AssertAxeClean(null, "graph-table"); } }
            public class Derived : Base { public override void Scan() { } }
            """);
        Assert.False(evidence.HasAxeLabel("graph-table"));
    }

    [Fact]
    public void AnExtensionHelperUsesItsBoundParameterIdentity()
    {
        TestEvidence evidence = Evidence("[Fact] public void Journey() { this.Scan(\"graph-table\"); }", """
            public static class Helpers {
                public static void Scan(this ShellAccessibilityTests tests, string label) {
                    ShellAccessibilityTests.AssertAxeClean(null, label);
                }
            }
            """);
        Assert.True(evidence.HasAxeLabel("graph-table"));
    }

    [Fact]
    public void StandardDataProvidersMustResolveToRunnableProviderShapes()
    {
        TestEvidence member = Evidence("""
            [Theory, MemberData(nameof(Rows))] public void Journey(int value) { AssertAxeClean(null, "graph-table"); }
            public static IEnumerable<object[]> Rows => new[] { new object[] { 1 } };
            """);
        Assert.True(member.HasTestEvidence("Journey"));
        Assert.True(member.HasAxeLabel("graph-table"));
        TestEvidence dataClass = Evidence("[Theory, ClassData(typeof(Rows))] public void Journey(int value) { }",
            "public class Rows : List<object[]> { public Rows() { Add(new object[] { 1 }); } }");
        Assert.True(dataClass.HasTestEvidence("Journey"));
    }

    [Theory]
    [InlineData("[Theory, MemberData(nameof(Rows), \"wrong\")] public void Journey(int value) { } public static IEnumerable<object[]> Rows(int count) => new[] { new object[] { count } };")]
    [InlineData("[Theory, MemberData(nameof(Rows), 1)] public void Journey(int value) { } public static IEnumerable<object[]> Rows(long count) => new[] { new object[] { count } };")]
    [InlineData("[Theory, MemberData(nameof(Rows))] public void Journey(int value) { } public static IEnumerable<object[]> Rows(int count = 1) => new[] { new object[] { count } };")]
    [InlineData("[Theory, MemberData(nameof(Rows))] public void Journey(int value) { } public static IEnumerable<int> Rows => new[] { 1 };")]
    [InlineData("[Theory, MemberData(nameof(Rows))] public void Journey(int value) { } public static IEnumerable<int[]> Rows => new[] { new[] { 1 } };")]
    [InlineData("[Theory, ClassData(typeof(Rows))] public void Journey(int value) { } public class Rows : List<int> { }")]
    [InlineData("[Theory, ClassData(typeof(Rows))] public void Journey(int value) { } public class Rows : System.Collections.ArrayList { }")]
    public void IncompatibleProviderArgumentsAndKnownInvalidRowsCannotCertifyTests(string members)
    {
        TestEvidence evidence = Evidence(members);
        Assert.False(evidence.HasTestEvidence("Journey"));
        Assert.False(evidence.HasTestEvidence("Journeys"));
    }

    [Theory]
    [InlineData("[Theory, MemberData(nameof(Rows), 1)] public void Journey(int value) { } public static IEnumerable<object[]> Rows(int count) => new[] { new object[] { count } };")]
    [InlineData("[Theory, MemberData(nameof(Rows), 1)] public void Journey(int value) { } public static IEnumerable<object[]> Rows(object count) => new[] { new[] { count } };")]
    [InlineData("[Theory, MemberData(nameof(Rows), \"row\")] public void Journey(string value) { } public static IEnumerable<object[]> Rows(object value) => new[] { new[] { value } };")]
    [InlineData("[Theory, MemberData(nameof(Rows))] public void Journey(string value) { } public static IEnumerable<string[]> Rows => new[] { new[] { \"row\" } };")]
    [InlineData("[Theory, MemberData(nameof(Rows))] public void Journey(int value) { } public static System.Collections.IEnumerable Rows => new[] { new object[] { 1 } };")]
    [InlineData("[Theory, MemberData(nameof(Rows))] public void Journey(int value) { } public static IEnumerable<object> Rows => new[] { new object[] { 1 } };")]
    [InlineData("[Theory, MemberData(nameof(Rows))] public void Journey(int value) { } public static TheoryData<int> Rows => new() { 1 };")]
    public void AssignableProviderArgumentsAndPotentialRuntimeRowsRemainEvidence(string members)
    {
        Assert.True(Evidence(members).HasTestEvidence("Journey"));
    }

    [Theory]
    [InlineData("[Fact] public void Journey() { }", "public sealed class FactAttribute : Attribute { }")]
    [InlineData("[Theory, InlineData(1)] public void Journey(int value) { }", "public sealed class TheoryAttribute : Attribute { }")]
    [InlineData("[Theory, InlineData(1)] public void Journey(int value) { }", "public sealed class InlineDataAttribute : Attribute { public InlineDataAttribute(params object[] data) { } }")]
    [InlineData("[Theory, MemberData(nameof(Rows))] public void Journey(int value) { } public static IEnumerable<object[]> Rows => new[] { new object[] { 1 } };", "public sealed class MemberDataAttribute : Attribute { public MemberDataAttribute(string name, params object[] data) { } }")]
    [InlineData("[Theory, ClassData(typeof(Rows))] public void Journey(int value) { } public class Rows : List<object[]> { }", "public sealed class ClassDataAttribute : Attribute { public ClassDataAttribute(Type type) { } }")]
    public void SourceShadowingDoesNotReplaceRealXunitAttributes(string members, string declaration)
    {
        Assert.False(Evidence(members, globalTypes: "namespace Xunit { " + declaration + " }").HasTestEvidence("Journey"));
    }

    [Fact]
    public void StaticTestClassesAndStaticMethodsInViableClassesRemainEvidence()
    {
        Assert.True(Evidence("[Fact] public static void Journey() { }").HasTestEvidence("Journey"));
        Assert.True(Evidence("", "public static class StaticTests { [Fact] public static void Journey() { } }").HasTestEvidence("Journey"));
    }

    [Theory]
    [InlineData("enabled == true")]
    [InlineData("true == enabled")]
    [InlineData("enabled != false")]
    [InlineData("!(enabled == false)")]
    [InlineData("enabled && true")]
    [InlineData("enabled || false")]
    [InlineData("enabled ^ false")]
    public void BoundBooleanGuardsExcludeFalsePathsAndKeepTruePaths(string guard)
    {
        foreach (bool enabled in new[] { false, true })
        {
            TestEvidence evidence = Evidence($$"""
                [Fact] public void Journey() { Scan({{enabled.ToString().ToLowerInvariant()}}); }
                private void Scan(bool enabled) { if ({{guard}}) AssertAxeClean(null, "graph-table"); }
                """);
            Assert.Equal(enabled, evidence.HasAxeLabel("graph-table"));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CallbackInvocationBindsLambdaAndMethodGroupParameters(bool enabled)
    {
        string actual = enabled.ToString().ToLowerInvariant();
        TestEvidence lambda = Evidence($$"""
            [Fact] public void Journey() { Run(enabled => { if (enabled) AssertAxeClean(null, "graph-table"); }); }
            private void Run(Action<bool> callback) { callback({{actual}}); }
            """);
        TestEvidence methodGroup = Evidence($$"""
            [Fact] public void Journey() { Run(Scan); }
            private void Run(Action<bool, string> callback) { callback({{actual}}, "graph-table"); }
            private void Scan(bool enabled, string label) { if (enabled == true) AssertAxeClean(null, label); }
            """);
        Assert.Equal(enabled, lambda.HasAxeLabel("graph-table"));
        Assert.Equal(enabled, methodGroup.HasAxeLabel("graph-table"));
    }

    [Fact]
    public void CompilerConditionalCallsUseTheBuildsSymbols()
    {
        const string source = """
            [Fact] public void Journey() { Scan(); }
            [Conditional("ACTIVE_SCAN")] private void Scan() { AssertAxeClean(null, "graph-table"); }
            """;
        Assert.False(Evidence(source).HasAxeLabel("graph-table"));
        Assert.True(Evidence(source, symbols: ["ACTIVE_SCAN"]).HasAxeLabel("graph-table"));
    }

    [Theory]
    [InlineData("thread.Start();", true)]
    [InlineData("", false)]
    [InlineData("if (false) thread.Start();", false)]
    [InlineData("return; thread.Start();", false)]
    [InlineData("thread = new System.Threading.Thread(() => { }); thread.Start();", false)]
    [InlineData("var alias = thread; alias.Start();", false)]
    [InlineData("var other = new System.Threading.Thread(() => { }); other.Start();", false)]
    public void OnlyTheStartedUnreassignedThreadCertifiesItsCallback(string route, bool expected)
    {
        string members = "[Fact] public void Journey() { var thread = new System.Threading.Thread(() => Scan()); "
            + route + " } private void Scan() { AssertAxeClean(null, \"graph-table\"); }";
        Assert.Equal(expected, Evidence(members).HasAxeLabel("graph-table"));
    }

    [Theory]
    [InlineData("Assert.NotNull(peer.GetPattern(System.Windows.Automation.Peers.PatternInterface.Text));", true)]
    [InlineData("Assert.Null(peer.GetPattern(System.Windows.Automation.Peers.PatternInterface.Text));", false)]
    [InlineData("if (false) Assert.NotNull(peer.GetPattern(System.Windows.Automation.Peers.PatternInterface.Text));", false)]
    [InlineData("var ignored = System.Windows.Automation.Peers.PatternInterface.Text;", false)]
    public void PatternClaimsNeedAPositiveExecutableWitness(string assertion, bool expected)
    {
        string members = "[Fact] public void Journey() { System.Windows.Automation.Peers.AutomationPeer peer = null; " + assertion + " }";
        Assert.Equal(expected, Evidence(members).HasPatternEvidence("Journey", "Text"));
        Assert.False(Evidence(members).HasPatternEvidence("Absent", "Text"));
    }

    [Fact]
    public void FixturesMustBeRealFilesWithTheExactStem()
    {
        using var directory = FixtureVault.Create(0, "evidence-fixtures");
        Directory.CreateDirectory(Path.Combine(directory.Root, "directory-only"));
        File.WriteAllText(Path.Combine(directory.Root, "graph.json"), "{}");
        Assert.True(TestEvidence.HasFixture(directory.Root, "graph"));
        Assert.False(TestEvidence.HasFixture(directory.Root, "directory-only"));
        Assert.False(TestEvidence.HasFixture(directory.Root, "gr*"));
        Assert.False(TestEvidence.HasFixture(directory.Root, "missing"));
    }

    [Fact]
    public void MissingOrChangedBuildInputsRequestARebuild()
    {
        using var directory = FixtureVault.Create(0, "evidence-manifest");
        Assert.Contains("Build SlateWindows.Tests.csproj", Assert.ThrowsAny<Xunit.Sdk.XunitException>(
            () => TestEvidenceCompilation.Load(directory.Root)).Message);
        string source = Path.Combine(directory.Root, "Test.cs");
        File.WriteAllText(source, "class Original { }");
        File.WriteAllText(Path.Combine(directory.Root, "project-directory.txt"), directory.Root);
        File.WriteAllText(Path.Combine(directory.Root, "source-tree.txt"), source);
        File.WriteAllText(Path.Combine(directory.Root, "source-hashes.txt"),
            source + "|" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source))));
        File.WriteAllText(source, "class Changed { }");
        Assert.Contains("changed input", Assert.ThrowsAny<Xunit.Sdk.XunitException>(
            () => TestEvidenceCompilation.Load(directory.Root)).Message);
        File.WriteAllText(Path.Combine(directory.Root, "New.cs"), "class Added { }");
        Assert.Contains("source files were added or removed", Assert.ThrowsAny<Xunit.Sdk.XunitException>(
            () => TestEvidenceCompilation.Load(directory.Root)).Message);
    }
}
