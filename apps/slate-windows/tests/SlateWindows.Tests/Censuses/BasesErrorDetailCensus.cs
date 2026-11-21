// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

// #1697: a Bases failure is spoken in core's words. A VaultException's
// Message, and its ToString(), are the binding's field rendering
// ("@message=..."), not the error's text; every sentence the Bases sources
// build from a caught failure goes through FailureDetail.Of (#1890; Bases
// calls it through BasePhrase.ErrorDetail, which forwards), which is core's
// VaultErrorDetail for a vault error and the exception's own message
// otherwise. The helper is identified by its symbol,
// SlateWindows.FailureDetail.Of(System.Exception), never by its name or
// its class's name.
//
// The census BINDS (ShellCompilation) rather than reading spellings. Codex
// rounds 1 to 5 each found a syntax shape a scrape could not type: a stored
// read, a null-conditional read, an alias, a property pattern, ToString and
// interpolation, a lambda parameter whose type is inferred, an untyped
// pattern variable. With types, two rules cover every shape:
//
// - No Bases member outside FailureDetail.Of reads Exception.Message
//   (member access, conditional access, property or extended pattern).
//   ShellFailureDetailCensus applies this rule, through MessageReads, to
//   every shell source.
// - An expression whose type is an exception only flows on as an exception
//   (a local, return or parameter of an exception type; never a field or
//   property, where code the census does not read could reach it), into core's
//   renderer, FailureDetail.Of (trusted by its symbol; no other member of its
//   file is), a censused Bases method that takes an exception (whose own
//   body this census checks), a TaskCompletionSource, an exception's
//   constructor, a throw, or a type, pattern or null test. Anything else
//   (ToString, interpolation, concatenation, string.Format, an object slot)
//   is named. ShellFailureDetailCensus runs this rule's text sinks alone
//   (TextSinks) over every shell source.
//
// It fails closed on binding: the Bases sources must bind without errors, so
// an unbound type can never hide a read.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "bases-error-detail")]
public sealed class BasesErrorDetailCensus
{
    /// <summary>The Bases sources: the workspace's Bases half and the Bases
    /// folder, as the shell compilation holds them.</summary>
    private static (string Relative, SyntaxTree Tree)[] BasesTrees() =>
    [
        .. ShellCompilation.Sources
            .Where(source => source.Relative == "WorkspaceViewModel.Bases.cs"
                || source.Relative.StartsWith("Bases/", StringComparison.Ordinal))
            .Select(source => (source.Relative, source.Source.Root.SyntaxTree)),
    ];

    /// <summary>Fail closed: a Bases source that does not bind could hide an
    /// exception's type from the rules below.</summary>
    [Fact]
    public void TheBasesSourcesBindWithoutErrors()
    {
        (string Relative, SyntaxTree Tree)[] trees = BasesTrees();
        string[] found = [.. trees.Select(source => source.Relative)];
        foreach (string required in (string[])["WorkspaceViewModel.Bases.cs", "Bases/BaseDocumentViewModel.cs",
            "Bases/BaseQueryBuilderViewModel.cs", "Bases/DashboardViewModel.cs"])
        {
            Assert.True(found.Contains(required), $"premise: {required} is among the Bases sources ({string.Join(", ", found)}).");
        }
        string[] errors = [.. trees.SelectMany(source => ShellCompilation.Compilation.GetSemanticModel(source.Tree)
            .GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => $"{source.Relative}: {diagnostic}"))];
        Assert.True(errors.Length == 0, string.Join("\n", errors.Take(20)));
    }

    [Fact]
    public void NoBasesFailureBecomesTextOutsideCoresDetail()
    {
        (string Relative, SyntaxTree Tree)[] trees = BasesTrees();
        HashSet<SyntaxTree> censused = [.. trees.Select(source => source.Tree)];
        string[] failures = [.. trees.SelectMany(source =>
            Violations(ShellCompilation.Compilation.GetSemanticModel(source.Tree), Path.GetFileName(source.Relative), censused))];
        Assert.True(failures.Length == 0, string.Join("\n", failures));
    }

    /// <summary>The census's teeth, one planted member per shape codex rounds
    /// 1 to 5 found, bound against the shell: each is named; the helper,
    /// core's renderer, a censused consumer, a test, a throw, a wrap, a
    /// TaskCompletionSource and a model's own Message are clean.</summary>
    [Theory]
    [InlineData("detail = thrown.Message;", "thrown.Message")]
    [InlineData("detail = thrown?.Message ?? \"\";", "thrown?.Message")]
    [InlineData("var error = thrown; detail = error.Message;", "error.Message")]
    [InlineData("if (thrown is { Message: var text }) { detail = text; }", "Message: var text")]
    [InlineData("detail = thrown switch { { Message: var text } => text, _ => \"\" };", "Message: var text")]
    [InlineData("if (thrown is { InnerException.Message: var text }) { detail = text; }", "InnerException.Message: var text")]
    [InlineData("_announce(new A11yEvent.BasesViewExportFailed(thrown.ToString()));", "thrown.ToString")]
    [InlineData("var text = $\"{thrown}\"; detail = text;", "{thrown}")]
    [InlineData("detail = \"Failed: \" + thrown;", "\"Failed: \" + thrown")]
    [InlineData("detail = string.Format(\"{0}\", thrown);", "string.Format")]
    [InlineData("detail = $\"{work.Exception}\";", "{work.Exception}")]
    [InlineData("detail = $\"{_last}\";", "{_last}")]
    [InlineData("detail = $\"{(thrown as VaultException)}\";", "{(thrown as VaultException)}")]
    [InlineData("detail = Render(thrown);", "Render(thrown)")]
    [InlineData("Callback((text, failure) => { detail = failure?.ToString() ?? \"\"; });", "failure?.ToString()")]
    [InlineData("Callback((text, failure) => { if (failure is not null) { _announce(new A11yEvent.BasesCellEditFailed(failure.Message)); } });", "failure.Message")]
    [InlineData("if (thrown is { } error) { detail = $\"{error}\"; }", "{error}")]
    [InlineData("detail = thrown.StackTrace ?? \"\";", "thrown.StackTrace")]
    [InlineData("object boxed = thrown; detail = boxed.ToString() ?? \"\";", "= thrown")]
    [InlineData("if (thrown is object boxed) { detail = $\"{boxed}\"; }", "thrown is object boxed")]
    [InlineData("detail = thrown switch { object boxed => $\"{boxed}\", _ => \"\" };", "object boxed =>")]
    [InlineData("switch (thrown) { case object boxed: detail = $\"{boxed}\"; break; }", "case object boxed")]
    [InlineData("List<Exception> failures = [thrown]; detail = string.Join(\", \", failures);", "string.Join")]
    [InlineData("List<Exception> failures = [thrown]; foreach (object boxed in failures) { detail = $\"{boxed}\"; }", "foreach (object boxed")]
    [InlineData("List<Exception> failures = [thrown]; detail = string.Join(\", \", failures.Select(failure => failure.ToString()));", "failure.ToString")]
    [InlineData("List<object> boxes = [thrown]; detail = $\"{boxes[0]}\";", "[thrown]")]
    [InlineData("List<Exception> failures = [thrown]; detail = string.Join(\", \", failures.Cast<object>());", "failures.Cast<object>")]
    [InlineData("List<Exception> failures = [thrown]; detail = string.Join(\", \", failures.OfType<object>());", "failures.OfType<object>")]
    [InlineData("List<Exception> failures = [thrown]; detail = string.Join(\", \", failures.Select(failure => (object)failure));", "(object)failure")]
    [InlineData("List<Exception> failures = [thrown]; detail = string.Join(\", \", failures.Select(Convert.ToString));", "failures.Select")]
    [InlineData("List<Exception> failures = [thrown]; detail = string.Join(\", \", failures.Select<object, string>(failure => failure.ToString() ?? \"\"));", "failures.Select<object, string>")]
    [InlineData("List<Exception> failures = [thrown]; failures.ForEach(Report);", "failures.ForEach")]
    [InlineData("List<Exception> failures = [thrown]; detail = failures.Count<object>(item => item.ToString() == \"\").ToString();", "failures.Count<object>")]
    [InlineData("List<Exception> failures = [thrown]; detail = failures.Any<object>(item => item.ToString() == \"\") ? \"x\" : \"\";", "failures.Any<object>")]
    [InlineData("detail = thrown.Describe();", "thrown.Describe")]
    [InlineData("thrown.Shout();", "thrown.Shout")]
    [InlineData("List<Exception> failures = [thrown]; detail = failures.ToString() ?? \"\";", "failures.ToString")]
    [InlineData("List<Exception> failures = [thrown]; failures.ForEach(failure => _announce(new A11yEvent.BasesCellEditFailed(BasePhrase.ErrorDetail(failure))));", "")]
    [InlineData("List<Exception> failures = [thrown]; detail = failures.Any(failure => failure is VaultException) ? \"x\" : \"\";", "")]
    [InlineData("detail = thrown.HResult.ToString();", "")]
    [InlineData("_announce(new A11yEvent.BasesViewExportFailed(Format(thrown)));", "Format(thrown)")]
    [InlineData("thrown?.Tap(Report);", "thrown?.Tap(Report)")]
    [InlineData("_last = thrown?.Tap(Report);", "thrown?.Tap(Report)")]
    [InlineData("thrown.Tap(Report);", "thrown.Tap")]
    [InlineData("Exception[] failures = [thrown]; detail = BasePhrase.ErrorDetail(failures[0]);", "")]
    [InlineData("detail = BasePhrase.ErrorDetail(thrown?.InnerException ?? thrown);", "")]
    [InlineData("work.Exception!.Handle(Handled);", "Handle")]
    [InlineData("work.Exception?.Handle(Handled);", "Handle")]
    [InlineData("work.Exception!.Handle(failure => { _announce(new A11yEvent.BasesViewExportFailed(BasePhrase.ErrorDetail(failure))); return true; });", "")]
    [InlineData("List<Exception> failures = [thrown]; System.Collections.IEnumerator cursor = failures.GetEnumerator(); while (cursor.MoveNext()) { detail += $\"{cursor.Current}\"; }", "failures.GetEnumerator()")]
    [InlineData("List<Exception> failures = [thrown]; var cursor = failures.GetEnumerator(); while (cursor.MoveNext()) { detail += $\"{cursor.Current}\"; }", "{cursor.Current}")]
    [InlineData("List<Exception> failures = [thrown]; var byCode = failures.ToDictionary(failure => failure.HResult); detail = string.Join(\", \", byCode);", "string.Join")]
    [InlineData("List<Exception> failures = [thrown]; var byCode = failures.ToDictionary(failure => failure.HResult); detail = byCode.First().ToString();", "byCode.First().ToString")]
    [InlineData("List<Exception> failures = [thrown]; var cursor = failures.GetEnumerator(); while (cursor.MoveNext()) { detail = BasePhrase.ErrorDetail(cursor.Current); }", "")]
    [InlineData("List<Exception> failures = [thrown]; var byCode = failures.ToDictionary(failure => failure.HResult); detail = BasePhrase.ErrorDetail(byCode[0]);", "")]
    [InlineData("detail = thrown.Spell();", "thrown.Spell")]
    [InlineData("List<Exception> failures = [thrown]; detail = string.Join(\", \", failures.Select(Format));", "failures.Select")]
    [InlineData("List<Exception> failures = [thrown]; detail = string.Join(\", \", failures.Select(failure => BasePhrase.ErrorDetail(failure)));", "")]
    [InlineData("List<Exception> failures = [thrown]; detail = string.Join(\", \", failures.Select(BasePhrase.ErrorDetail));", "")]
    [InlineData("List<Exception> failures = [thrown]; detail = BasePhrase.ErrorDetail(failures.First());", "")]
    [InlineData("List<Exception> failures = [thrown]; detail = failures.Where(failure => failure is VaultException).Count().ToString();", "")]
    [InlineData("if (thrown is { HResult: var code }) { detail = code.ToString(); }", "")]
    [InlineData("List<Exception> failures = [thrown]; foreach (var failure in failures) { detail = BasePhrase.ErrorDetail(failure); }", "")]
    [InlineData("List<Exception> failures = new() { thrown }; detail = failures.Count.ToString();", "")]
    [InlineData("detail = BasePhrase.ErrorDetail(thrown);", "")]
    [InlineData("detail = FailureDetail.Of(thrown);", "")]
    [InlineData("List<Exception> failures = [thrown]; detail = string.Join(\", \", failures.Select(FailureDetail.Of));", "")]
    [InlineData("if (thrown is VaultException vault) { detail = SlateUniffiMethods.VaultErrorDetail(vault); }", "")]
    [InlineData("var error = work.Exception?.GetBaseException(); if (error is not null) { detail = BasePhrase.ErrorDetail(error); }", "")]
    [InlineData("if (thrown == null) { return \"\"; } Exception? kept = thrown; kept ??= thrown; _ = kept; throw kept;", "")]
    [InlineData("_last = thrown;", "_last = thrown")]
    [InlineData("_last ??= thrown;", "_last ??= thrown")]
    [InlineData("var info = new FailureInfo(); info.Error = thrown; _announce(new A11yEvent.BasesViewExportFailed($\"{info}\"));", "info.Error = thrown")]
    [InlineData("var info = new FailureInfo { Error = thrown }; detail = $\"{info}\";", "Error = thrown")]
    [InlineData("var info = new FailureInfo() with { Error = thrown }; detail = $\"{info}\";", "Error = thrown")]
    [InlineData("var info = new { Error = thrown }; detail = $\"{info}\";", "Error = thrown")]
    [InlineData("if (thrown is VaultException vault) { detail = Describe(vault); }", "")]
    [InlineData("Callback((text, failure) => { if (failure is not null) { detail = BasePhrase.ErrorDetail(failure); } });", "")]
    [InlineData("detail = new Note(\"x\").Message;", "")]
    [InlineData("var completion = new TaskCompletionSource(); completion.TrySetException(thrown);", "")]
    [InlineData("throw new InvalidOperationException(\"wrapped\", thrown);", "new InvalidOperationException(\"wrapped\", thrown)")]
    [InlineData("detail = BasePhrase.ErrorDetail(new AggregateException(thrown));", "new AggregateException(thrown)")]
    [InlineData("throw new InvalidOperationException(\"no failure attached\");", "")]
    [InlineData("detail = thrown.GetType().Name;", "")]
    public void TheCensusNamesEveryWayAFailureBecomesText(string body, string named)
    {
        string[] failures = PlantedViolations($$"""
            namespace SlateWindows.Bases.Planted
            {
                using uniffi.slate_uniffi;

                internal sealed class Planted
                {
                    private Exception? _last;
                    private readonly Action<A11yEvent> _announce = _ => { };

                    private sealed record Note(string Message);

                    private sealed record FailureInfo
                    {
                        public Exception? Error { get; set; }
                    }

                    private static void Callback(Action<string?, VaultException?> done) => done(null, null);

                    private static string Describe(VaultException failure) => BasePhrase.ErrorDetail(failure);

                    private static string Render(object value) => value.ToString() ?? "";

                    private void Report(object value) => _announce(new A11yEvent.BasesViewExportFailed(value.ToString() ?? ""));

                    private bool Handled(object value)
                    {
                        Report(value);
                        return true;
                    }

                    private static string Format<T>(T failure) => failure?.ToString() ?? "";
                    private string M(Exception thrown, Task work)
                    {
                        string detail = "";
                        {{body}}
                        return detail;
                    }
                }

                internal static class PlantedText
                {
                    internal static string Describe(this object value) => value.ToString() ?? "";

                    internal static void Shout(this object value) => Console.WriteLine(value);

                    internal static string Spell<T>(this T value) => value?.ToString() ?? "";

                    internal static T Tap<T>(this T value, Action<T> callback)
                    {
                        callback(value);
                        return value;
                    }
                }
            }
            """);
        if (named.Length == 0)
        {
            Assert.Empty(failures);
        }
        else
        {
            Assert.Contains(failures, failure => failure.Contains(named, StringComparison.Ordinal));
        }
    }

    /// <summary>#1890: the helper is FailureDetail.Of(Exception), bound by
    /// its symbol. Its own read is exempt. The same read in a method named
    /// BasePhrase.ErrorDetail, in a class named FailureDetail in another
    /// namespace, or in an overload of Of beside the real one, is not.</summary>
    [Fact]
    public void OnlyTheHelperReadsAnExceptionsMessage()
    {
        string[] failures = PlantedViolations("""
            namespace SlateWindows.Bases.Planted
            {
                using uniffi.slate_uniffi;

                internal static class BasePhrase
                {
                    internal static string ErrorDetail(Exception failure) =>
                        failure is VaultException vault ? SlateUniffiMethods.VaultErrorDetail(vault) : failure.Message;
                }

                internal static class FailureDetail
                {
                    internal static string Of(Exception failure) =>
                        failure is VaultException vault ? SlateUniffiMethods.VaultErrorDetail(vault) : failure.Message;
                }
            }

            namespace SlateWindows
            {
                internal static partial class FailureDetail
                {
                    internal static string Of(Exception failure, string lead) => lead + failure.Message;
                }
            }
            """,
            failureDetail: PartialHelper);
        Assert.Contains(failures, failure => failure.StartsWith("Planted.cs line 8: `failure.Message`", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.StartsWith("Planted.cs line 14: `failure.Message`", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.StartsWith("Planted.cs line 22: `failure.Message`", StringComparison.Ordinal));
        Assert.DoesNotContain(failures, failure => failure.StartsWith("FailureDetail.cs", StringComparison.Ordinal));
    }

    /// <summary>#1890: FailureDetail.Of is trusted by its symbol, and no other
    /// member of its file is: a stringifying member planted beside it is
    /// named where a Bases source hands it a failure.</summary>
    [Fact]
    public void AMemberBesideTheHelperIsNeverTrusted()
    {
        string[] failures = PlantedViolations("""
            namespace SlateWindows.Bases.Planted
            {
                internal static class Planted
                {
                    internal static string Spoken(Exception thrown) => FailureDetail.Of(thrown);

                    internal static string Described(Exception thrown) => FailureDetail.Describe(thrown);
                }
            }
            """,
            failureDetail: PartialHelper.Replace(
                "    internal static string Of(",
                "    internal static string Describe(Exception failure) => failure.ToString();\n\n    internal static string Of(",
                StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.StartsWith("Planted.cs", StringComparison.Ordinal)
            && failure.Contains("FailureDetail.Describe(thrown)", StringComparison.Ordinal));
        Assert.DoesNotContain(failures, failure => failure.Contains("FailureDetail.Of(thrown)", StringComparison.Ordinal));
    }

    /// <summary>The real <c>FailureDetail.cs</c>, read from the shell's
    /// sources and declared partial, so a planted source can add an overload
    /// to the same class: the text a case binds in place of the real file. It
    /// is the real file's text with one word added, so a change to the real
    /// helper reaches every case that binds it.</summary>
    internal static string PartialHelper
    {
        get
        {
            const string declaration = "internal static class FailureDetail";
            string real = FailureDetailTree().GetText().ToString();
            int at = real.IndexOf(declaration, StringComparison.Ordinal);
            Assert.True(
                at >= 0 && real.IndexOf(declaration, at + 1, StringComparison.Ordinal) < 0,
                $"premise: FailureDetail.cs declares `{declaration}` once, so the planted cases can bind it partial.");
            return real.Replace(declaration, "internal static partial class FailureDetail", StringComparison.Ordinal);
        }
    }

    /// <summary>Codex round 10: a generic helper is never a trusted consumer,
    /// and a body over a type parameter constrained to an exception is held
    /// to the rules like one over the exception itself.</summary>
    [Fact]
    public void AGenericHelperIsNeverTrustedAndAConstrainedBodyIsChecked()
    {
        string[] failures = PlantedViolations("""
            namespace SlateWindows.Bases.Planted
            {
                using uniffi.slate_uniffi;

                internal static class Planted
                {
                    internal static string Unconstrained(VaultException thrown) => Format(thrown);

                    private static string Format<T>(T failure) => failure?.ToString() ?? "";

                    internal static string Constrained<T>(T failure) where T : Exception => failure.ToString();
                }
            }
            """);
        Assert.Contains(failures, failure => failure.Contains("Format(thrown)", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("failure.ToString", StringComparison.Ordinal));
    }

    /// <summary>Codex round 15: an exception written through a <c>ref</c> or
    /// <c>out</c> parameter or a <c>ref</c> local lands in its caller's
    /// storage, a record field here; the call site, the helper's write and
    /// the ref local are each named.</summary>
    [Fact]
    public void AnExceptionWrittenThroughARefIsNamed()
    {
        string[] failures = PlantedViolations("""
            namespace SlateWindows.Bases.Planted
            {
                using uniffi.slate_uniffi;

                internal sealed class Planted
                {
                    private sealed record FailureSlot
                    {
                        public Exception? Error;
                    }

                    private static void Remember(ref Exception? target, Exception failure) => target = failure;

                    private static void Capture(out Exception? target, Exception failure) => target = failure;

                    internal string ByRef(Exception thrown)
                    {
                        var slot = new FailureSlot();
                        Remember(ref slot.Error, thrown);
                        return $"{slot}";
                    }

                    internal string ByOut(Exception thrown)
                    {
                        var slot = new FailureSlot();
                        Capture(out slot.Error, thrown);
                        return $"{slot}";
                    }

                    internal string ByRefLocal(Exception thrown)
                    {
                        var slot = new FailureSlot();
                        ref Exception? cell = ref slot.Error;
                        cell = thrown;
                        return $"{slot}";
                    }
                }
            }
            """);
        Assert.Contains(failures, failure => failure.Contains("Remember(ref slot.Error, thrown)", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("Capture(out slot.Error, thrown)", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("target = failure", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("cell = thrown", StringComparison.Ordinal));
    }

    /// <summary>Codex round 16: an interface, abstract or virtual method
    /// declared in Bases is not a trusted consumer, since an implementation
    /// outside Bases can read the Message; a sealed override is.</summary>
    [Fact]
    public void AnOverridableConsumerIsNeverTrusted()
    {
        string[] failures = PlantedViolations("""
            namespace SlateWindows.Bases.Planted
            {
                internal interface IErrorFormatter
                {
                    string Format(Exception failure);
                }

                internal abstract class ErrorShaper
                {
                    public abstract string Shape(Exception failure);
                }

                internal class Phrasing
                {
                    public virtual string Say(Exception failure) => BasePhrase.ErrorDetail(failure);
                }

                internal sealed class SafePhrasing : Phrasing
                {
                    public override string Say(Exception failure) => BasePhrase.ErrorDetail(failure);
                }

                internal static class Planted
                {
                    internal static string ByInterface(IErrorFormatter formatter, Exception thrown) => formatter.Format(thrown);

                    internal static string ByAbstract(ErrorShaper shaper, Exception thrown) => shaper.Shape(thrown);

                    internal static string ByVirtual(Phrasing phrasing, Exception thrown) => phrasing.Say(thrown);

                    internal static string BySealed(SafePhrasing safe, Exception thrown) => safe.Say(thrown);
                }
            }
            """,
            outsideBases: """
            namespace SlateWindows.Elsewhere
            {
                using SlateWindows.Bases.Planted;

                internal sealed class LeakyFormatter : IErrorFormatter
                {
                    public string Format(Exception failure) => failure.Message;
                }

                internal sealed class LeakyShaper : ErrorShaper
                {
                    public override string Shape(Exception failure) => failure.ToString();
                }

                internal sealed class LeakyPhrasing : Phrasing
                {
                    public override string Say(Exception failure) => failure.Message;
                }
            }
            """);
        Assert.Contains(failures, failure => failure.Contains("formatter.Format(thrown)", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("shaper.Shape(thrown)", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("phrasing.Say(thrown)", StringComparison.Ordinal));
        Assert.DoesNotContain(failures, failure => failure.Contains("safe.Say(thrown)", StringComparison.Ordinal));
    }

    /// <summary>Codex round 17: a partial method declared in Bases is trusted
    /// only when its implementation, the body that runs, is in Bases too.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void APartialConsumerIsTrustedOnlyByItsImplementation(bool implementedInBases)
    {
        const string implementation = """
                internal static partial class PartialPhrase
                {
                    internal static partial string Detail(Exception failure) => IMPLEMENTATION;
                }
            """;
        string inBases = implementation.Replace("IMPLEMENTATION", "BasePhrase.ErrorDetail(failure)", StringComparison.Ordinal);
        string elsewhere = implementation.Replace("IMPLEMENTATION", "failure.Message", StringComparison.Ordinal);
        string[] failures = PlantedViolations(
            $$"""
            namespace SlateWindows.Bases.Planted
            {
                internal static partial class PartialPhrase
                {
                    internal static partial string Detail(Exception failure);
                }

            {{(implementedInBases ? inBases : "")}}

                internal static class Planted
                {
                    internal static string Speak(Exception thrown) => PartialPhrase.Detail(thrown);
                }
            }
            """,
            outsideBases: implementedInBases ? null : $$"""
            namespace SlateWindows.Bases.Planted
            {
            {{elsewhere}}
            }
            """);
        if (implementedInBases)
        {
            Assert.Empty(failures);
        }
        else
        {
            Assert.Contains(failures, failure => failure.Contains("PartialPhrase.Detail(thrown)", StringComparison.Ordinal));
        }
    }

    /// <summary>Codex round 18: an exception's constructor is trusted with a
    /// failure only when its body is in Bases, where the census reads it.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AnExceptionConstructorIsTrustedOnlyByACensusedBody(bool declaredInBases)
    {
        const string reporter = """
                internal sealed class FailureReporter : Exception
                {
                    public FailureReporter(Exception failure, Action<string> announce)
                    {
                        announce(SPOKEN);
                    }
                }
            """;
        string[] failures = PlantedViolations(
            $$"""
            namespace SlateWindows.Bases.Planted
            {
            {{(declaredInBases ? reporter.Replace("SPOKEN", "BasePhrase.ErrorDetail(failure)", StringComparison.Ordinal) : "")}}

                internal static class Planted
                {
                    internal static Exception Speak(Exception thrown, Action<string> announce) => new FailureReporter(thrown, announce);
                }
            }
            """,
            outsideBases: declaredInBases ? null : $$"""
            namespace SlateWindows.Bases.Planted
            {
            {{reporter.Replace("SPOKEN", "failure.Message", StringComparison.Ordinal)}}
            }
            """);
        if (declaredInBases)
        {
            Assert.Empty(failures);
        }
        else
        {
            Assert.Contains(failures, failure => failure.Contains("new FailureReporter(thrown, announce)", StringComparison.Ordinal));
        }
    }

    /// <summary>Codex round 19: a delegate that returns a failure carries it.
    /// Handed to a helper outside Bases, as a lambda, a delegate local or a
    /// method group, it is named; LINQ may take one.</summary>
    [Fact]
    public void AFailureReturningDelegateIsFollowed()
    {
        string[] failures = PlantedViolations("""
            namespace SlateWindows.Bases.Planted
            {
                internal static class Planted
                {
                    internal static string ByLambda(Exception thrown) =>
                        SlateWindows.Elsewhere.SharedPhrase.Detail(() => thrown);

                    internal static string ByLocal(Exception thrown)
                    {
                        Func<Exception> get = () => thrown;
                        return SlateWindows.Elsewhere.SharedPhrase.Detail(get);
                    }

                    internal static string ByGroup(Exception thrown)
                    {
                        Exception Get() => thrown;
                        return SlateWindows.Elsewhere.SharedPhrase.Detail(Get);
                    }

                    internal static string ByInvoking(Exception thrown)
                    {
                        Func<Exception> get = () => thrown;
                        string direct = BasePhrase.ErrorDetail(get());
                        return get?.Invoke() is { } fault ? direct + BasePhrase.ErrorDetail(fault) : direct;
                    }

                    internal static string ByLinq(Exception thrown)
                    {
                        List<Exception> failures = [thrown];
                        return string.Join(", ", failures.Select(failure => failure.InnerException ?? failure)
                            .Select(BasePhrase.ErrorDetail));
                    }
                }
            }
            """,
            outsideBases: """
            namespace SlateWindows.Elsewhere
            {
                internal static class SharedPhrase
                {
                    internal static string Detail(Func<Exception> get) => get().Message;
                }
            }
            """);
        Assert.Contains(failures, failure => failure.Contains("Detail(() => thrown)", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("Detail(get)", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("Detail(Get)", StringComparison.Ordinal));
        Assert.DoesNotContain(failures, failure => failure.Contains("failures.Select", StringComparison.Ordinal));
        Assert.DoesNotContain(failures, failure => failure.Contains("get()", StringComparison.Ordinal)
            || failure.Contains("get?.Invoke()", StringComparison.Ordinal));
    }

    /// <summary>A censused consumer's own body is held to the rule.</summary>
    [Fact]
    public void ACensusedConsumersBodyIsCheckedToo()
    {
        string[] failures = PlantedViolations("""
            namespace SlateWindows.Bases.Planted
            {
                using uniffi.slate_uniffi;

                internal static class Planted
                {
                    internal static string M(VaultException thrown) => Describe(thrown);

                    private static string Describe(VaultException failure) => $"Open failed: {failure}";
                }
            }
            """);
        Assert.Contains(failures, failure => failure.Contains("{failure}", StringComparison.Ordinal));
        Assert.DoesNotContain(failures, failure => failure.Contains("Describe(thrown)", StringComparison.Ordinal));
    }

    /// <summary>Binds <paramref name="source"/> as one more Bases tree beside
    /// the shell's, and returns its violations; the planted source must bind
    /// cleanly or the case proves nothing. With <paramref name="failureDetail"/>,
    /// that text stands in for <c>FailureDetail.cs</c> (never censused, as
    /// the real file is not), and its own violations are returned too.</summary>
    private static string[] PlantedViolations(string source, string? outsideBases = null, string? failureDetail = null)
    {
        var options = (CSharpParseOptions)ShellCompilation.Compilation.SyntaxTrees.First().Options;
        SyntaxTree tree = CSharpSyntaxTree.ParseText(source, options, path: "Planted.cs");
        CSharpCompilation compilation = ShellCompilation.Compilation.AddSyntaxTrees(tree);
        if (outsideBases is not null)
        {
            // Bound beside the planted tree, never censused: code elsewhere
            // in the shell.
            compilation = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(outsideBases, options, path: "Elsewhere.cs"));
        }
        SyntaxTree? helper = null;
        if (failureDetail is not null)
        {
            helper = CSharpSyntaxTree.ParseText(failureDetail, options, path: "FailureDetail.cs");
            compilation = compilation.ReplaceSyntaxTree(FailureDetailTree(), helper);
        }
        SemanticModel model = compilation.GetSemanticModel(tree);
        Diagnostic[] errors = [.. model.GetDiagnostics()
            .Concat(helper is null ? [] : compilation.GetSemanticModel(helper).GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)];
        Assert.True(errors.Length == 0, "premise: the planted source binds. " + string.Join("\n", errors));
        HashSet<SyntaxTree> censused = [.. BasesTrees().Select(bases => bases.Tree), tree];
        return
        [
            .. Violations(model, "Planted.cs", censused),
            .. helper is null ? [] : Violations(compilation.GetSemanticModel(helper), "FailureDetail.cs", censused),
        ];
    }

    /// <summary>The shell's <c>FailureDetail.cs</c>, as the shell compilation
    /// holds it.</summary>
    internal static SyntaxTree FailureDetailTree() =>
        ShellCompilation.Sources.Single(source => source.Relative == "FailureDetail.cs").Source.Root.SyntaxTree;

    /// <summary>Both rules over one bound tree.</summary>
    internal static IEnumerable<string> Violations(SemanticModel model, string file, IReadOnlySet<SyntaxTree> censused)
    {
        INamedTypeSymbol exception = model.Compilation.GetTypeByMetadataName("System.Exception")
            ?? throw new InvalidOperationException("System.Exception does not bind.");
        var trust = new Trust(censused, TheHelper(model.Compilation));
        Func<SyntaxNode, bool> inTheHelper = InTheHelper(model, trust.Helper);
        foreach (MessageRead read in MessageReads(model, file, inTheHelper))
        {
            yield return read.ToString();
        }
        foreach (SyntaxNode node in model.SyntaxTree.GetRoot().DescendantNodes())
        {
            if (node is ExpressionSyntax expression
                && !inTheHelper(node)
                && IsExceptionValue(model, expression, exception)
                && !CarriedOn(model, expression, exception)
                && !AllowedUse(model, expression, exception, trust))
            {
                yield return $"{file} line {Line(node)}: {expression} reaches text through `{Snippet(expression)}`; "
                    + "use FailureDetail.Of.";
            }
        }
    }

    /// <summary>Rule 2's text sinks alone, which ShellFailureDetailCensus
    /// runs over every shell source (#1890, plan 47 CB-15c): an exception, or
    /// a value that carries one, made text directly. An interpolation hole, a
    /// string concatenation (<c>+</c> or <c>+=</c>), a <c>ToString()</c> call
    /// on it, or an argument to the framework's text builders
    /// (<see cref="TextBuilders"/>). The other uses rule 2 names on the Bases
    /// sources (a field, an object slot, a generic helper, a delegate) are not
    /// sinks here. A stored exception whose Message is later read still meets
    /// rule 1; but a holder of one that is itself made text (a record's
    /// generated ToString), an erasure to object that is later made text, and
    /// a serializer are beyond this list (ShellFailureDetailCensus's header
    /// names them).</summary>
    internal static IEnumerable<string> TextSinks(SemanticModel model, string file, Func<SyntaxNode, bool> isHelper)
    {
        INamedTypeSymbol exception = model.Compilation.GetTypeByMetadataName("System.Exception")
            ?? throw new InvalidOperationException("System.Exception does not bind.");
        foreach (SyntaxNode node in model.SyntaxTree.GetRoot().DescendantNodes())
        {
            // The cheap test on the parent's shape first, then the types: the
            // census walks every expression of the shell.
            if (node is ExpressionSyntax expression
                && IsTextSinkShape(expression)
                && !isHelper(node)
                && IsExceptionValue(model, expression, exception)
                && !CarriedOn(model, expression, exception)
                && IsTextSink(model, expression))
            {
                yield return $"{file} line {Line(node)}: {expression} becomes text through `{Snippet(expression)}`; "
                    + "use FailureDetail.Of.";
            }
        }
    }

    /// <summary>The framework types whose methods make their arguments text:
    /// a string's Format, Concat and Join, a builder's Append and Insert, a
    /// writer's Write, Convert.ToString, and the interpolation and debug
    /// writers.</summary>
    private static readonly HashSet<string> TextBuilders =
    [
        "System.String",
        "System.Text.StringBuilder",
        "System.IO.TextWriter",
        "System.IO.StringWriter",
        "System.Console",
        "System.Convert",
        "System.FormattableString",
        "System.Runtime.CompilerServices.DefaultInterpolatedStringHandler",
        "System.Diagnostics.Debug",
        "System.Diagnostics.Trace",
    ];

    /// <summary>Whether the parent has a sink's shape at all, by syntax
    /// alone: an interpolation hole, a <c>+</c> or <c>+=</c>, a member access
    /// or conditional access on the value, or an argument.</summary>
    private static bool IsTextSinkShape(ExpressionSyntax expression) =>
        expression.Parent is InterpolationSyntax
            or BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AddExpression }
            or AssignmentExpressionSyntax { RawKind: (int)SyntaxKind.AddAssignmentExpression }
            or MemberAccessExpressionSyntax
            or ConditionalAccessExpressionSyntax
            or ArgumentSyntax;

    /// <summary>Whether the parent makes <paramref name="expression"/>
    /// text: see <see cref="TextSinks"/>.</summary>
    private static bool IsTextSink(SemanticModel model, ExpressionSyntax expression) =>
        expression.Parent switch
        {
            InterpolationSyntax hole => hole.Expression == expression,
            BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AddExpression } sum =>
                model.GetTypeInfo(sum).Type?.SpecialType == SpecialType.System_String,
            AssignmentExpressionSyntax { RawKind: (int)SyntaxKind.AddAssignmentExpression } append =>
                append.Right == expression && model.GetTypeInfo(append.Left).Type?.SpecialType == SpecialType.System_String,
            MemberAccessExpressionSyntax access when access.Expression == expression =>
                access.Name.Identifier.ValueText == "ToString",
            ConditionalAccessExpressionSyntax conditional when conditional.Expression == expression =>
                conditional.WhenNotNull.DescendantNodesAndSelf().OfType<MemberBindingExpressionSyntax>().FirstOrDefault()
                    is { Name.Identifier.ValueText: "ToString" },
            ArgumentSyntax { Parent.Parent: { } call } =>
                model.GetSymbolInfo(call).Symbol is IMethodSymbol method
                && TextBuilders.Contains(
                    method.ContainingType.ContainingNamespace.ToDisplayString() + "." + method.ContainingType.MetadataName),
            _ => false,
        };

    /// <summary>One read of an exception's <c>Message</c>: where it is, and
    /// the access, pattern, arm or case it is part of.</summary>
    internal sealed record MessageRead(string File, int Line, string Context, SimpleNameSyntax Name)
    {
        public override string ToString() =>
            $"{File} line {Line}: `{Context}` reads an exception's Message; use FailureDetail.Of.";
    }

    /// <summary>Rule 1, which this census and ShellFailureDetailCensus share:
    /// every read of <c>Message</c> on a type deriving from
    /// <c>System.Exception</c> (member access, conditional access, property
    /// or extended pattern) that <paramref name="isHelper"/> does not exempt.</summary>
    internal static IEnumerable<MessageRead> MessageReads(SemanticModel model, string file, Func<SyntaxNode, bool> isHelper)
    {
        INamedTypeSymbol exception = model.Compilation.GetTypeByMetadataName("System.Exception")
            ?? throw new InvalidOperationException("System.Exception does not bind.");
        foreach (SimpleNameSyntax name in model.SyntaxTree.GetRoot().DescendantNodes().OfType<SimpleNameSyntax>())
        {
            if (name.Identifier.ValueText == "Message"
                && model.GetSymbolInfo(name).Symbol is IPropertySymbol property
                && DerivesFrom(property.ContainingType, exception)
                && !isHelper(name))
            {
                yield return new MessageRead(file, Line(name), MessageContext(name), name);
            }
        }
    }

    /// <summary>The one reader of an exception's Message the censuses allow:
    /// <c>SlateWindows.FailureDetail.Of(System.Exception)</c> as the
    /// compilation binds it, the one static, non-generic method of that name
    /// taking a single <c>Exception</c> by value. Fail closed: without it,
    /// nothing is exempt and the census says why.</summary>
    internal static IMethodSymbol TheHelper(Compilation compilation)
    {
        INamedTypeSymbol exception = compilation.GetTypeByMetadataName("System.Exception")
            ?? throw new InvalidOperationException("System.Exception does not bind.");
        INamedTypeSymbol type = compilation.GetTypeByMetadataName("SlateWindows.FailureDetail")
            ?? throw new InvalidOperationException("SlateWindows.FailureDetail does not bind, so the census has no helper to exempt.");
        IMethodSymbol[] helpers = [.. type.GetMembers("Of").OfType<IMethodSymbol>().Where(method => method.IsStatic
            && method.TypeParameters.IsEmpty
            && method.Parameters.Length == 1
            && method.Parameters[0].RefKind == RefKind.None
            && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, exception))];
        return helpers.Length == 1
            ? helpers[0]
            : throw new InvalidOperationException($"SlateWindows.FailureDetail declares {helpers.Length} methods Of(Exception); the census needs exactly one.");
    }

    /// <summary>Whether a node sits in the helper's body, by the symbol its
    /// method declaration binds to: no name, class name or namespace makes a
    /// method the helper.</summary>
    internal static Func<SyntaxNode, bool> InTheHelper(SemanticModel model, IMethodSymbol helper)
    {
        var verdicts = new Dictionary<SyntaxNode, bool>();
        return node =>
        {
            if (node.FirstAncestorOrSelf<MethodDeclarationSyntax>() is not { } method)
            {
                return false;
            }
            if (!verdicts.TryGetValue(method, out bool verdict))
            {
                verdict = SymbolEqualityComparer.Default.Equals(model.GetDeclaredSymbol(method), helper);
                verdicts[method] = verdict;
            }
            return verdict;
        };
    }

    /// <summary>What rule 2 may trust: the censused sources, whose bodies it
    /// reads, and the helper, by its symbol.</summary>
    private sealed record Trust(IReadOnlySet<SyntaxTree> Censused, IMethodSymbol Helper);

    /// <summary>An expression whose value is an exception, or a collection
    /// of them; a type name (the <c>VaultException</c> in a catch or pattern)
    /// is not a value.</summary>
    private static bool IsExceptionValue(SemanticModel model, ExpressionSyntax expression, INamedTypeSymbol exception)
    {
        TypeInfo info = model.GetTypeInfo(expression);
        // A lambda or method group has no type of its own; the delegate it
        // becomes is what carries a failure on.
        ITypeSymbol? type = info.Type
            ?? (expression is AnonymousFunctionExpressionSyntax || model.GetSymbolInfo(expression).Symbol is IMethodSymbol
                ? info.ConvertedType
                : null);
        return type is not null
            && CarriesExceptions(type, exception)
            && model.GetSymbolInfo(expression).Symbol is not ITypeSymbol;
    }

    /// <summary>An exception, or an array or sequence of them (codex round 6:
    /// <c>string.Join</c> and a typed foreach erase a collection's element
    /// type the way a capture erases a value's).</summary>
    /// <remarks>Codex round 11: any type built over an exception carries it,
    /// not just a sequence: an enumerator (<c>List&lt;Exception&gt;.Enumerator</c>,
    /// through its containing type), a <c>KeyValuePair</c>, a dictionary, a
    /// grouping, a tuple. Widening one to a type that does not carry (the
    /// non-generic <c>IEnumerator</c>, <c>object</c>) is then named like any
    /// other erasure.
    /// <para>Each type is examined once per question: <c>string</c> is
    /// <c>IEquatable&lt;string&gt;</c>, so a walk without a visited set
    /// revisits the same types without end.</para></remarks>
    private static bool CarriesExceptions(ITypeSymbol type, INamedTypeSymbol exception) =>
        CarriesExceptions(type, exception, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default));

    private static bool CarriesExceptions(ITypeSymbol type, INamedTypeSymbol exception, HashSet<ITypeSymbol> visited)
    {
        if (!visited.Add(type))
        {
            return false;
        }
        if (DerivesFrom(type, exception))
        {
            return true;
        }
        return type switch
        {
            // A delegate's type arguments are its signature, not values it
            // holds: an Action<VaultException?> callback carries no failure.
            // One that RETURNS a failure does (codex round 19: a helper
            // outside Bases handed () => failure calls it and reads the
            // Message).
            INamedTypeSymbol { TypeKind: TypeKind.Delegate, DelegateInvokeMethod: { } invoke } =>
                CarriesExceptions(invoke.ReturnType, exception, visited),
            INamedTypeSymbol { TypeKind: TypeKind.Delegate } => false,
            IArrayTypeSymbol array => CarriesExceptions(array.ElementType, exception, visited),
            INamedTypeSymbol named => named.TypeArguments.Any(argument => CarriesExceptions(argument, exception, visited))
                || (named.ContainingType is { } outer && CarriesExceptions(outer, exception, visited))
                || named.AllInterfaces.Any(contract =>
                    contract.TypeArguments.Any(argument => CarriesExceptions(argument, exception, visited))),
            _ => false,
        };
    }

    /// <summary>A type a captured value can't speak the dump through: an
    /// exception or a collection of them (the census follows it), a value
    /// type, or <c>System.Type</c>.</summary>
    private static bool HoldsNoText(ITypeSymbol type, INamedTypeSymbol exception) =>
        CarriesExceptions(type, exception) || type.IsValueType || type.ToDisplayString() == "System.Type";

    /// <summary>Codex round 6: a pattern on an exception may capture
    /// (<c>is object boxed</c>, a switch arm, a case label); every variable
    /// it declares must keep a type that holds no text.</summary>
    private static bool CapturesHoldNoText(SemanticModel model, PatternSyntax pattern, INamedTypeSymbol exception) =>
        pattern.DescendantNodesAndSelf().OfType<SingleVariableDesignationSyntax>().All(designation =>
            model.GetDeclaredSymbol(designation) is ILocalSymbol local && HoldsNoText(local.Type, exception));

    /// <summary>Whether the parent expression is itself an exception, so the
    /// value is judged where that larger expression goes: parentheses,
    /// <c>!</c>, <c>??</c>, <c>?:</c>, a cast or <c>as</c>, an assignment into
    /// an exception slot, <c>.InnerException</c>, a conditional access that
    /// yields one.</summary>
    /// <remarks>Codex round 13: only pure value flow carries a value on. A
    /// member call or indexer whose result is still an exception
    /// (<c>thrown?.Tap(Report)</c>) hands the receiver to code first, so its
    /// receiver is judged by the member rules, never skipped.</remarks>
    private static bool CarriedOn(SemanticModel model, ExpressionSyntax expression, INamedTypeSymbol exception) =>
        expression.Parent switch
        {
            MemberAccessExpressionSyntax access => access.Name == expression,
            MemberBindingExpressionSyntax binding => binding.Name == expression,
            ParenthesizedExpressionSyntax or CastExpressionSyntax
                or PostfixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.SuppressNullableWarningExpression }
                or BinaryExpressionSyntax { RawKind: (int)SyntaxKind.CoalesceExpression or (int)SyntaxKind.AsExpression } =>
                IsExceptionValue(model, (ExpressionSyntax)expression.Parent, exception),
            ConditionalExpressionSyntax choice when choice.Condition != expression => IsExceptionValue(model, choice, exception),
            ConditionalAccessExpressionSyntax conditional when conditional.WhenNotNull == expression =>
                IsExceptionValue(model, conditional, exception),
            _ => false,
        };

    /// <summary>The uses that never make an exception text.</summary>
    private static bool AllowedUse(
        SemanticModel model, ExpressionSyntax expression, INamedTypeSymbol exception, Trust trust)
    {
        bool staysAnException = model.GetTypeInfo(expression).ConvertedType is { } converted
            && CarriesExceptions(converted, exception);
        bool isACollection = model.GetTypeInfo(expression).Type is { } type && !DerivesFrom(type, exception);
        return expression.Parent switch
        {
            ArgumentSyntax argument => AllowedArgument(model, argument, exception, trust),
            // get(): a failure-returning delegate invoked directly.
            InvocationExpressionSyntax invocation when invocation.Expression == expression =>
                model.GetSymbolInfo(invocation).Symbol is IMethodSymbol { MethodKind: MethodKind.DelegateInvoke },
            ThrowStatementSyntax or ThrowExpressionSyntax or ExpressionStatementSyntax => true,
            IsPatternExpressionSyntax test => test.Expression == expression
                && CapturesHoldNoText(model, test.Pattern, exception),
            SwitchExpressionSyntax switchExpression => switchExpression.GoverningExpression == expression
                && switchExpression.Arms.All(arm => CapturesHoldNoText(model, arm.Pattern, exception)),
            SwitchStatementSyntax switchStatement => switchStatement.Expression == expression
                && switchStatement.Sections.SelectMany(section => section.Labels).OfType<CasePatternSwitchLabelSyntax>()
                    .All(label => CapturesHoldNoText(model, label.Pattern, exception)),
            ForEachStatementSyntax loop => loop.Expression == expression
                && model.GetDeclaredSymbol(loop) is { } variable && HoldsNoText(variable.Type, exception),
            BinaryExpressionSyntax binary => binary.Kind() is SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression
                or SyntaxKind.IsExpression,
            // Codex round 14: an exception is kept only in a local or a
            // parameter. Stored in a field or property (an assignment, an
            // object or `with` initializer, a field initializer), it is
            // reachable by code the census does not read: a record's
            // generated ToString, a holder's own ToString, anything handed
            // the holder.
            AssignmentExpressionSyntax assignment => assignment.Left == expression
                || (staysAnException && IsLocalStorage(model, assignment.Left)),
            EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator } =>
                staysAnException && model.GetDeclaredSymbol(declarator) is ILocalSymbol,
            EqualsValueClauseSyntax => false,
            ReturnStatementSyntax or ArrowExpressionClauseSyntax or LambdaExpressionSyntax => staysAnException,
            ExpressionElementSyntax { Parent: CollectionExpressionSyntax collection } =>
                model.GetTypeInfo(collection).ConvertedType is { } target && CarriesExceptions(target, exception),
            InitializerExpressionSyntax { Parent: BaseObjectCreationExpressionSyntax creation } =>
                model.GetTypeInfo(creation).Type is { } created && CarriesExceptions(created, exception),
            MemberAccessExpressionSyntax access when access.Expression == expression =>
                AllowedMember(model, access.Name, exception, isACollection, trust),
            ElementAccessExpressionSyntax element when element.Expression == expression =>
                model.GetSymbolInfo(element).Symbol switch
                {
                    IPropertySymbol indexer => (indexer.ContainingNamespace?.ToDisplayString() is "System.Collections.Generic"
                        or "System.Collections.Immutable" or "System.Collections.ObjectModel")
                        && HoldsNoText(indexer.Type, exception),
                    null => model.GetTypeInfo(expression).Type is IArrayTypeSymbol,
                    _ => false,
                },
            ConditionalAccessExpressionSyntax conditional when conditional.Expression == expression
                && conditional.WhenNotNull.DescendantNodesAndSelf().OfType<MemberBindingExpressionSyntax>().FirstOrDefault() is { } binding =>
                AllowedMember(model, binding.Name, exception, isACollection, trust),
            _ => false,
        };
    }

    /// <summary>A local, a parameter or a discard: storage only this member
    /// can reach. Codex round 15: a <c>ref</c> or <c>out</c> parameter or
    /// <c>ref</c> local aliases its caller's storage (a record's field
    /// through <c>Remember(ref info.Error, …)</c>), so it is not local.</summary>
    private static bool IsLocalStorage(SemanticModel model, ExpressionSyntax target) =>
        target is IdentifierNameSyntax { Identifier.ValueText: "_" }
        || model.GetSymbolInfo(target).Symbol is ILocalSymbol { RefKind: RefKind.None }
            or IParameterSymbol { RefKind: RefKind.None } or IDiscardSymbol;

    /// <summary>A member whose value holds no text: <c>HResult</c>,
    /// <c>InnerException</c>, <c>GetBaseException()</c>, a collection's
    /// <c>Count</c>, <c>First()</c> or <c>Where(…)</c> (which the census then
    /// follows), and an exception's <c>GetType</c>. On a collection, a call
    /// whose arguments are all lambdas (<c>Select(f => …)</c>) also passes:
    /// each element reaches text only inside the lambda, whose body the
    /// census reads. <c>Cast&lt;object&gt;()</c>, <c>OfType&lt;object&gt;()</c>
    /// and a method group erase the type unread, so they are named (codex
    /// round 7).</summary>
    /// <remarks>Codex round 9: the receiver and callback checks stand on
    /// their own, never behind the return type: <c>failures.ForEach(Report)</c>
    /// returns void and <c>Count&lt;object&gt;(…)</c> an int, yet each hands
    /// the elements to code the census does not read. On an exception, only
    /// the exception's own members pass (an extension on <c>object</c> widens
    /// the receiver), or a censused method that still binds an exception.</remarks>
    private static bool AllowedMember(
        SemanticModel model, SimpleNameSyntax name, INamedTypeSymbol exception, bool onACollection, Trust trust)
    {
        ISymbol? symbol = model.GetSymbolInfo(name).Symbol;
        if (!onACollection)
        {
            return symbol switch
            {
                IMethodSymbol method when KeepsItsElements(method, exception) && IsConsumer(method, exception, trust)
                    && CallbacksAreRead(model, name, exception, trust) => true,
                IMethodSymbol { Name: "GetType", MethodKind: MethodKind.Ordinary } => true,
                // Codex round 12: an exception's own method with a callback
                // (AggregateException.Handle) hands the failures to it, so
                // the callback is read here too.
                IMethodSymbol { MethodKind: MethodKind.Ordinary } method => DerivesFrom(method.ContainingType, exception)
                    && HoldsNoText(method.ReturnType, exception)
                    && CallbacksAreRead(model, name, exception, trust),
                IPropertySymbol property => DerivesFrom(property.ContainingType, exception) && HoldsNoText(property.Type, exception),
                _ => false,
            };
        }
        return symbol switch
        {
            IPropertySymbol property => HoldsNoText(property.Type, exception),
            // Invoking a failure-returning delegate yields the failure,
            // typed, and the census follows it from there.
            IMethodSymbol { MethodKind: MethodKind.DelegateInvoke } => CallbacksAreRead(model, name, exception, trust),
            IMethodSymbol method => KeepsItsElements(method, exception)
                && (IsConsumer(method, exception, trust)
                    || (IsCollectionLibrary(method) && (HoldsNoText(method.ReturnType, exception) || HasACallback(model, name))))
                && CallbacksAreRead(model, name, exception, trust),
            _ => false,
        };
    }

    /// <summary>Whether the call takes a delegate. A library call with none
    /// (<c>KeyValuePair.ToString()</c>, which formats the exception inside
    /// it) makes its result from the elements itself, so that result must
    /// hold no text; text may only come out of a callback the census reads
    /// (codex round 11).</summary>
    private static bool HasACallback(SemanticModel model, SimpleNameSyntax name) =>
        name.Parent?.Parent is InvocationExpressionSyntax invocation
        && invocation.ArgumentList.Arguments.Any(argument =>
            model.GetTypeInfo(argument.Expression).ConvertedType is { TypeKind: TypeKind.Delegate });

    /// <summary>LINQ and the collection types never make an element text
    /// themselves; whatever reaches text does it in a callback.</summary>
    private static bool IsCollectionLibrary(IMethodSymbol method) =>
        (method.ReducedFrom ?? method).ContainingNamespace?.ToDisplayString() is "System.Linq" or "System.Collections.Generic"
            or "System.Collections.Immutable" or "System.Collections.ObjectModel";

    /// <summary>Every delegate argument of the call is a lambda (whose body
    /// the census reads) or a method group naming a trusted consumer that
    /// takes exceptions. A method group or delegate variable the census
    /// cannot read is not.</summary>
    private static bool CallbacksAreRead(
        SemanticModel model, SimpleNameSyntax name, INamedTypeSymbol exception, Trust trust) =>
        name.Parent?.Parent is InvocationExpressionSyntax invocation
        && invocation.ArgumentList.Arguments.All(argument =>
            model.GetTypeInfo(argument.Expression).ConvertedType is not { TypeKind: TypeKind.Delegate }
            || argument.Expression is AnonymousFunctionExpressionSyntax
            || (model.GetSymbolInfo(argument.Expression).Symbol is IMethodSymbol group
                && group.Parameters.Any(parameter => CarriesExceptions(parameter.Type, exception))
                && IsConsumer(group, exception, trust)));

    /// <summary>Codex round 8: a call whose receiver is converted before
    /// the call (<c>failures.Select&lt;object, string&gt;(…)</c> widens
    /// the elements by covariance) hands its lambda <c>object</c>s the
    /// census can't follow. The receiver it binds (an extension's
    /// <c>this</c> parameter) must still carry exceptions.</summary>
    private static bool KeepsItsElements(IMethodSymbol method, INamedTypeSymbol exception) =>
        method.ReceiverType is { } receiver && CarriesExceptions(receiver, exception);

    /// <summary>An exception argument is fine where its parameter is an
    /// exception and the callee is core's renderer, a TaskCompletionSource,
    /// an exception's constructor, or a method the census reads.</summary>
    private static bool AllowedArgument(
        SemanticModel model, ArgumentSyntax argument, INamedTypeSymbol exception, Trust trust)
    {
        // A ref or out argument hands the callee storage to write: it must be
        // a plain local, or the callee could park the exception in a field.
        if (!argument.RefKindKeyword.IsKind(SyntaxKind.None) && !argument.RefKindKeyword.IsKind(SyntaxKind.InKeyword)
            && !IsLocalStorage(model, argument.Expression))
        {
            return false;
        }
        if (argument.Parent?.Parent is not { } call || model.GetSymbolInfo(call).Symbol is not IMethodSymbol method)
        {
            return false;
        }
        IParameterSymbol? parameter = argument.NameColon is { } nameColon
            ? method.Parameters.FirstOrDefault(candidate => candidate.Name == nameColon.Name.Identifier.ValueText)
            : argument.Parent is ArgumentListSyntax list && list.Arguments.IndexOf(argument) is var index
                && index < method.Parameters.Length
                ? method.Parameters[index]
                : null;
        if (parameter is null || !CarriesExceptions(parameter.Type, exception))
        {
            return false;
        }
        // LINQ and the collection types call a failure-returning callback
        // and hand its result on in a typed value the census follows
        // (Select(f => f.InnerException)).
        if (parameter.Type.TypeKind == TypeKind.Delegate && IsCollectionLibrary(method))
        {
            return true;
        }
        return IsConsumer(method, exception, trust);
    }

    /// <summary>A method an exception may be handed to: the helper,
    /// <c>FailureDetail.Of</c>, by its symbol; core's renderer; a
    /// TaskCompletionSource; an exception's constructor; or a method the
    /// census reads (BasePhrase.ErrorDetail among them). The helper's file is
    /// not censused, so no other member a change puts there is trusted.</summary>
    private static bool IsConsumer(IMethodSymbol method, INamedTypeSymbol exception, Trust trust) =>
        (method.ReducedFrom ?? method) switch
        {
            var helper when SymbolEqualityComparer.Default.Equals(helper.OriginalDefinition, trust.Helper) => true,
            { Name: "VaultErrorDetail", ContainingType.Name: "SlateUniffiMethods" } => true,
            { Name: "SetException" or "TrySetException", ContainingType.Name: "TaskCompletionSource" } => true,
            // Codex round 18: a constructor runs a body like any other call
            // (a custom exception's constructor can announce the Message),
            // and a framework wrapper's Message can embed its inner one
            // (AggregateException's does). A constructor is trusted only on
            // the same terms as a method: a censused body.
            { MethodKind: MethodKind.Constructor } => DerivesFrom(method.ContainingType, exception)
                && HasACensusedBody(method, trust.Censused)
                && method.ContainingType is not { IsGenericType: true },
            // Codex round 10: a generic method is checked against its
            // declared T, not the call's Exception, so its body can stringify
            // unseen (Render<T>(T failure) => failure.ToString()). Only a
            // non-generic censused method, whose body sees the types its
            // callers pass, is trusted.
            var declared => HasACensusedBody(declared, trust.Censused)
                && declared.OriginalDefinition.TypeParameters.IsEmpty
                && declared.ContainingType is not { IsGenericType: true }
                && DispatchesOnlyToThisBody(declared),
        };

    /// <summary>Codex round 17: the body a call runs is what is trusted. For
    /// a partial method that is its implementation part, which may sit in
    /// another file; every declaration of it must be in the censused sources
    /// and carry a body (a bodiless declaration is never trusted).</summary>
    private static bool HasACensusedBody(IMethodSymbol method, IReadOnlySet<SyntaxTree> censused)
    {
        IMethodSymbol implementation = method.PartialImplementationPart ?? method;
        if (implementation.IsPartialDefinition)
        {
            return false;
        }
        SyntaxReference[] declarations = [.. implementation.DeclaringSyntaxReferences];
        return declarations.Length > 0
            && declarations.All(reference => censused.Contains(reference.SyntaxTree))
            && declarations.All(reference => reference.GetSyntax() switch
            {
                BaseMethodDeclarationSyntax body => body.Body is not null || body.ExpressionBody is not null,
                LocalFunctionStatementSyntax local => local.Body is not null || local.ExpressionBody is not null,
                _ => false,
            });
    }

    /// <summary>Codex round 16: trusting a declaration means trusting the
    /// body the census reads, so the call must reach no other: not an
    /// interface or abstract member (its implementation can live outside
    /// Bases), not virtual, and an override only when sealed or in a sealed
    /// type.</summary>
    private static bool DispatchesOnlyToThisBody(IMethodSymbol method) =>
        method.ContainingType.TypeKind != TypeKind.Interface
        && !method.IsAbstract
        && !method.IsVirtual
        && !method.IsExtern
        && (!method.IsOverride || method.IsSealed || method.ContainingType.IsSealed);

    private static bool DerivesFrom(ITypeSymbol type, INamedTypeSymbol exception)
    {
        if (type is ITypeParameterSymbol parameter)
        {
            return parameter.ConstraintTypes.Any(constraint => DerivesFrom(constraint, exception));
        }
        for (ITypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, exception))
            {
                return true;
            }
        }
        return false;
    }

    private static int Line(SyntaxNode node) => node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

    /// <summary>Where a Message read sits: the access, or the pattern, arm or
    /// case it is part of.</summary>
    private static string MessageContext(SimpleNameSyntax name)
    {
        SyntaxNode context = name.Parent switch
        {
            MemberAccessExpressionSyntax access when access.Parent is not ExpressionColonSyntax => access,
            MemberBindingExpressionSyntax binding => binding.FirstAncestorOrSelf<ConditionalAccessExpressionSyntax>() ?? (SyntaxNode)binding,
            _ => name.FirstAncestorOrSelf<SyntaxNode>(candidate => candidate is IsPatternExpressionSyntax
                or SwitchExpressionArmSyntax or CasePatternSwitchLabelSyntax) ?? name.Parent!,
        };
        return Shorten(context);
    }

    /// <summary>The construct that consumes the exception: the call an
    /// argument goes to, otherwise the parent.</summary>
    private static string Snippet(ExpressionSyntax expression) => Shorten(expression.Parent switch
    {
        ArgumentSyntax { Parent.Parent: { } call } => call,
        ExpressionElementSyntax { Parent: { } collection } => collection,
        var parent => parent!,
    });

    private static string Shorten(SyntaxNode node)
    {
        string text = node.WithoutTrivia().NormalizeWhitespace().ToString();
        return text.Length <= 100 ? text : text[..100] + "…";
    }
}
