// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

// #1697: a Bases failure is spoken in core's words. A VaultException's
// Message, and its ToString(), are the binding's field rendering
// ("@message=..."), not the error's text; every sentence the Bases sources
// build from a caught failure goes through BasePhrase.ErrorDetail, which is
// core's VaultErrorDetail for a vault error and the exception's own message
// otherwise.
//
// The census BINDS (ShellCompilation) rather than reading spellings. Codex
// rounds 1 to 5 each found a syntax shape a scrape could not type: a stored
// read, a null-conditional read, an alias, a property pattern, ToString and
// interpolation, a lambda parameter whose type is inferred, an untyped
// pattern variable. With types, two rules cover every shape:
//
// - No Bases member outside BasePhrase.ErrorDetail reads Exception.Message
//   (member access, conditional access, property or extended pattern).
// - An expression whose type is an exception only flows on as an exception
//   (a local, field, return or parameter of an exception type), into core's
//   renderer, a censused Bases method that takes an exception (whose own
//   body this census checks), a TaskCompletionSource, an exception's
//   constructor, a throw, or a type, pattern or null test. Anything else
//   (ToString, interpolation, concatenation, string.Format, an object slot)
//   is named.
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
    [InlineData("List<Exception> failures = [thrown]; detail = string.Join(\", \", failures.Select(failure => BasePhrase.ErrorDetail(failure)));", "")]
    [InlineData("List<Exception> failures = [thrown]; detail = string.Join(\", \", failures.Select(BasePhrase.ErrorDetail));", "")]
    [InlineData("List<Exception> failures = [thrown]; detail = BasePhrase.ErrorDetail(failures.First());", "")]
    [InlineData("List<Exception> failures = [thrown]; detail = failures.Where(failure => failure is VaultException).Count().ToString();", "")]
    [InlineData("if (thrown is { HResult: var code }) { detail = code.ToString(); }", "")]
    [InlineData("List<Exception> failures = [thrown]; foreach (var failure in failures) { detail = BasePhrase.ErrorDetail(failure); }", "")]
    [InlineData("List<Exception> failures = new() { thrown }; detail = failures.Count.ToString();", "")]
    [InlineData("detail = BasePhrase.ErrorDetail(thrown);", "")]
    [InlineData("if (thrown is VaultException vault) { detail = SlateUniffiMethods.VaultErrorDetail(vault); }", "")]
    [InlineData("var error = work.Exception?.GetBaseException(); if (error is not null) { detail = BasePhrase.ErrorDetail(error); }", "")]
    [InlineData("if (thrown == null) { return \"\"; } _last = thrown; throw thrown;", "")]
    [InlineData("if (thrown is VaultException vault) { detail = Describe(vault); }", "")]
    [InlineData("Callback((text, failure) => { if (failure is not null) { detail = BasePhrase.ErrorDetail(failure); } });", "")]
    [InlineData("detail = new Note(\"x\").Message;", "")]
    [InlineData("var completion = new TaskCompletionSource(); completion.TrySetException(thrown);", "")]
    [InlineData("throw new InvalidOperationException(\"wrapped\", thrown);", "")]
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

                    private static void Callback(Action<string?, VaultException?> done) => done(null, null);

                    private static string Describe(VaultException failure) => BasePhrase.ErrorDetail(failure);

                    private static string Render(object value) => value.ToString() ?? "";

                    private string M(Exception thrown, Task work)
                    {
                        string detail = "";
                        {{body}}
                        return detail;
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

    /// <summary>BasePhrase.ErrorDetail's own read is exempt; the same read in
    /// an ErrorDetail outside BasePhrase is not.</summary>
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

                internal static class Elsewhere
                {
                    internal static string ErrorDetail(Exception failure) =>
                        failure is VaultException vault ? SlateUniffiMethods.VaultErrorDetail(vault) : failure.Message;
                }
            }
            """);
        Assert.Contains(failures, failure => failure.Contains("line 14", StringComparison.Ordinal));
        Assert.DoesNotContain(failures, failure => failure.Contains("line 8", StringComparison.Ordinal));
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
    /// cleanly or the case proves nothing.</summary>
    private static string[] PlantedViolations(string source)
    {
        var options = (CSharpParseOptions)ShellCompilation.Compilation.SyntaxTrees.First().Options;
        SyntaxTree tree = CSharpSyntaxTree.ParseText(source, options, path: "Planted.cs");
        CSharpCompilation compilation = ShellCompilation.Compilation.AddSyntaxTrees(tree);
        SemanticModel model = compilation.GetSemanticModel(tree);
        Diagnostic[] errors = [.. model.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)];
        Assert.True(errors.Length == 0, "premise: the planted source binds. " + string.Join("\n", errors));
        HashSet<SyntaxTree> censused = [.. BasesTrees().Select(bases => bases.Tree), tree];
        return [.. Violations(model, "Planted.cs", censused)];
    }

    /// <summary>Both rules over one bound tree.</summary>
    internal static IEnumerable<string> Violations(SemanticModel model, string file, IReadOnlySet<SyntaxTree> censused)
    {
        INamedTypeSymbol exception = model.Compilation.GetTypeByMetadataName("System.Exception")
            ?? throw new InvalidOperationException("System.Exception does not bind.");
        foreach (SyntaxNode node in model.SyntaxTree.GetRoot().DescendantNodes())
        {
            if (InTheHelper(node))
            {
                continue;
            }
            if (node is SimpleNameSyntax { Identifier.ValueText: "Message" } name
                && model.GetSymbolInfo(name).Symbol is IPropertySymbol property
                && DerivesFrom(property.ContainingType, exception))
            {
                yield return $"{file} line {Line(node)}: `{MessageContext(name)}` reads an exception's Message; "
                    + "use BasePhrase.ErrorDetail.";
            }
            if (node is ExpressionSyntax expression
                && IsExceptionValue(model, expression, exception)
                && !CarriedOn(model, expression, exception)
                && !AllowedUse(model, expression, exception, censused))
            {
                yield return $"{file} line {Line(node)}: {expression} reaches text through `{Snippet(expression)}`; "
                    + "use BasePhrase.ErrorDetail.";
            }
        }
    }

    private static bool InTheHelper(SyntaxNode node) =>
        node.FirstAncestorOrSelf<MethodDeclarationSyntax>() is
        {
            Identifier.ValueText: "ErrorDetail",
            Parent: ClassDeclarationSyntax { Identifier.ValueText: "BasePhrase" },
        };

    /// <summary>An expression whose value is an exception, or a collection
    /// of them; a type name (the <c>VaultException</c> in a catch or pattern)
    /// is not a value.</summary>
    private static bool IsExceptionValue(SemanticModel model, ExpressionSyntax expression, INamedTypeSymbol exception) =>
        model.GetTypeInfo(expression).Type is { } type
        && CarriesExceptions(type, exception)
        && model.GetSymbolInfo(expression).Symbol is not ITypeSymbol;

    /// <summary>An exception, or an array or sequence of them (codex round 6:
    /// <c>string.Join</c> and a typed foreach erase a collection's element
    /// type the way a capture erases a value's).</summary>
    private static bool CarriesExceptions(ITypeSymbol type, INamedTypeSymbol exception) => type switch
    {
        _ when DerivesFrom(type, exception) => true,
        IArrayTypeSymbol array => CarriesExceptions(array.ElementType, exception),
        INamedTypeSymbol named => SequenceElement(named) is { } element && CarriesExceptions(element, exception),
        _ => false,
    };

    private static ITypeSymbol? SequenceElement(INamedTypeSymbol type) =>
        (type.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T
            ? type
            : type.AllInterfaces.FirstOrDefault(candidate =>
                candidate.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T))
        ?.TypeArguments[0];

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
    private static bool CarriedOn(SemanticModel model, ExpressionSyntax expression, INamedTypeSymbol exception) =>
        expression.Parent is ExpressionSyntax outer && IsExceptionValue(model, outer, exception);

    /// <summary>The uses that never make an exception text.</summary>
    private static bool AllowedUse(
        SemanticModel model, ExpressionSyntax expression, INamedTypeSymbol exception, IReadOnlySet<SyntaxTree> censused)
    {
        bool staysAnException = model.GetTypeInfo(expression).ConvertedType is { } converted
            && CarriesExceptions(converted, exception);
        bool isACollection = model.GetTypeInfo(expression).Type is { } type && !DerivesFrom(type, exception);
        return expression.Parent switch
        {
            ArgumentSyntax argument => AllowedArgument(model, argument, exception, censused),
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
            EqualsValueClauseSyntax or ReturnStatementSyntax or ArrowExpressionClauseSyntax or LambdaExpressionSyntax
                or AssignmentExpressionSyntax => staysAnException,
            ExpressionElementSyntax { Parent: CollectionExpressionSyntax collection } =>
                model.GetTypeInfo(collection).ConvertedType is { } target && CarriesExceptions(target, exception),
            InitializerExpressionSyntax { Parent: BaseObjectCreationExpressionSyntax creation } =>
                model.GetTypeInfo(creation).Type is { } created && CarriesExceptions(created, exception),
            MemberAccessExpressionSyntax access when access.Expression == expression =>
                AllowedMember(model, access.Name, exception, isACollection, censused),
            ConditionalAccessExpressionSyntax conditional when conditional.Expression == expression
                && conditional.WhenNotNull.DescendantNodesAndSelf().OfType<MemberBindingExpressionSyntax>().FirstOrDefault() is { } binding =>
                AllowedMember(model, binding.Name, exception, isACollection, censused),
            _ => false,
        };
    }

    /// <summary>A member whose value holds no text: <c>HResult</c>,
    /// <c>InnerException</c>, <c>GetBaseException()</c>, a collection's
    /// <c>Count</c>, <c>First()</c> or <c>Where(…)</c> (which the census then
    /// follows), and an exception's <c>GetType</c>. On a collection, a call
    /// whose arguments are all lambdas (<c>Select(f => …)</c>) also passes:
    /// each element reaches text only inside the lambda, whose body the
    /// census reads. <c>Cast&lt;object&gt;()</c>, <c>OfType&lt;object&gt;()</c>
    /// and a method group erase the type unread, so they are named (codex
    /// round 7).</summary>
    private static bool AllowedMember(
        SemanticModel model, SimpleNameSyntax name, INamedTypeSymbol exception, bool onACollection, IReadOnlySet<SyntaxTree> censused) =>
        model.GetSymbolInfo(name).Symbol switch
        {
            IMethodSymbol { Name: "GetType" } when !onACollection => true,
            IMethodSymbol method => HoldsNoText(method.ReturnType, exception)
                || (onACollection && KeepsItsElements(method, exception)
                    && name.Parent?.Parent is InvocationExpressionSyntax { ArgumentList.Arguments: { Count: > 0 } arguments }
                    && arguments.All(argument => argument.Expression is AnonymousFunctionExpressionSyntax
                        || (model.GetSymbolInfo(argument.Expression).Symbol is IMethodSymbol group
                            && group.Parameters.Any(parameter => CarriesExceptions(parameter.Type, exception))
                            && IsConsumer(group, exception, censused)))),
            IPropertySymbol property => HoldsNoText(property.Type, exception),
            _ => false,
        };

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
        SemanticModel model, ArgumentSyntax argument, INamedTypeSymbol exception, IReadOnlySet<SyntaxTree> censused)
    {
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
        return parameter is not null && CarriesExceptions(parameter.Type, exception) && IsConsumer(method, exception, censused);
    }

    /// <summary>A method an exception may be handed to: core's renderer, a
    /// TaskCompletionSource, an exception's constructor, or a method the
    /// census reads (BasePhrase.ErrorDetail among them).</summary>
    private static bool IsConsumer(IMethodSymbol method, INamedTypeSymbol exception, IReadOnlySet<SyntaxTree> censused) =>
        (method.ReducedFrom ?? method) switch
        {
            { Name: "VaultErrorDetail", ContainingType.Name: "SlateUniffiMethods" } => true,
            { Name: "SetException" or "TrySetException", ContainingType.Name: "TaskCompletionSource" } => true,
            { MethodKind: MethodKind.Constructor } => DerivesFrom(method.ContainingType, exception),
            var declared => declared.DeclaringSyntaxReferences.Any(reference => censused.Contains(reference.SyntaxTree)),
        };

    private static bool DerivesFrom(ITypeSymbol type, INamedTypeSymbol exception)
    {
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
