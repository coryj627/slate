// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

// #1697: a Bases failure is spoken in core's words. A VaultException's
// Message is the binding's field rendering ("@message=..."), not the error's
// text; every sentence the Bases sources build from a caught failure goes
// through BasePhrase.ErrorDetail, which is core's VaultErrorDetail for a
// vault error and the exception's own message otherwise.
//
// The census is fail-closed (codex round 2): a source scrape cannot tell an
// exception's type through an alias or a conditional access, so every
// Message read in the Bases sources, plain or null-conditional, is named
// unless its receiver is a local or parameter the same member declares with
// one of the model types below, whose Message is already a composed
// sentence. BasePhrase.ErrorDetail, the one place a non-vault exception's
// message is read on purpose, is exempt.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "bases-error-detail")]
public sealed class BasesErrorDetailCensus
{
    /// <summary>The types whose <c>Message</c> is not an exception's: core's
    /// expression validation result, and a dashboard section whose message is
    /// the sentence the dashboard already composed.</summary>
    private static readonly HashSet<string> MessageModels =
    [
        "BaseExpressionValidation",
        "DashboardSectionViewModel",
    ];

    /// <summary>The Bases sources: the workspace's Bases half and every file
    /// in the Bases folder.</summary>
    private static IEnumerable<string> BasesSources()
    {
        string root = SourceText.ShellSourceRoot();
        yield return Path.Combine(root, "WorkspaceViewModel.Bases.cs");
        foreach (string file in Directory.EnumerateFiles(Path.Combine(root, "Bases"), "*.cs").Order(StringComparer.Ordinal))
        {
            yield return file;
        }
    }

    [Fact]
    public void NoBasesAnnouncementReadsAnExceptionsMessage()
    {
        string[] failures = [.. BasesSources()
            .SelectMany(path => AnnouncementMessageReads(CSharpSource.LoadPath(path).Root, Path.GetFileName(path)))];
        Assert.True(failures.Length == 0, string.Join("\n", failures));
    }

    /// <summary>A caught exception's Message never leaves the catch as text
    /// either (a local, a return, a property the sentence reads later), by any
    /// name or through a conditional access (codex rounds 1 and 2).</summary>
    [Fact]
    public void NoBasesMessageIsReadExceptFromAModel()
    {
        string[] failures = [.. BasesSources()
            .SelectMany(path => UnmodelledMessageReads(CSharpSource.LoadPath(path).Root, Path.GetFileName(path)))];
        Assert.True(failures.Length == 0, string.Join("\n", failures));
    }

    /// <summary>Codex round 4: an exception turned into text another way
    /// (<c>ToString()</c>, interpolation, <c>+</c>, <c>string.Format</c>) is
    /// the same field dump. In the Bases sources an exception, or any local
    /// it is copied into, may only reach the helper, an
    /// <see cref="ExceptionConsumers"/> method, a throw, a pattern or null
    /// test, or another exception local.</summary>
    [Fact]
    public void NoBasesExceptionBecomesTextOutsideTheHelper()
    {
        (string File, SyntaxNode Root)[] sources = [.. BasesSources()
            .Select(path => (Path.GetFileName(path), CSharpSource.LoadPath(path).Root))];
        HashSet<string> censused = [.. sources.SelectMany(source => CensusedConsumers(source.Root))];
        string[] failures = [.. sources.SelectMany(source => ExceptionTextUses(source.Root, source.File, censused))];
        Assert.True(failures.Length == 0, string.Join("\n", failures));
    }

    /// <summary>Methods the census reads that take an exception parameter
    /// (BasePhrase.OpenFailed, ExecuteFailed, Message): handing them an
    /// exception is safe because this same census checks what their body
    /// does with it.</summary>
    internal static IEnumerable<string> CensusedConsumers(SyntaxNode root) =>
        root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Where(method => method.ParameterList.Parameters.Any(parameter => parameter.Type is { } type && IsException(type)))
            .SelectMany(method => method.Parent is BaseTypeDeclarationSyntax owner
                ? new[] { method.Identifier.ValueText, $"{owner.Identifier.ValueText}.{method.Identifier.ValueText}" }
                : [method.Identifier.ValueText]);

    /// <summary>How a call names its target for the censused set: bare or
    /// through <c>this</c> by its name, through a type by
    /// <c>Type.Method</c>; any other receiver matches nothing, so a
    /// same-named method elsewhere is not taken for a censused one.</summary>
    private static string ConsumerKey(ExpressionSyntax target) => target switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } access => access.Name.Identifier.ValueText,
        MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax owner } access =>
            $"{owner.Identifier.ValueText}.{access.Name.Identifier.ValueText}",
        _ => "",
    };

    /// <summary>The exception rule's teeth: each round-4 form, through a
    /// local, an alias, a field, a task's exception or a cast, is named;
    /// the helper, a throw, a test and a copy are clean.</summary>
    [Theory]
    [InlineData("_announce(new A11yEvent.BasesViewExportFailed(thrown.ToString()));", "thrown.ToString")]
    [InlineData("var text = $\"{thrown}\"; detail = text;", "{thrown}")]
    [InlineData("detail = \"Failed: \" + thrown;", "\"Failed: \" + thrown")]
    [InlineData("detail = string.Format(\"{0}\", thrown);", "string.Format")]
    [InlineData("var error = thrown; detail = error?.ToString() ?? \"\";", "error?.ToString()")]
    [InlineData("detail = $\"{work.Exception}\";", "{work.Exception}")]
    [InlineData("detail = $\"{_last}\";", "{_last}")]
    [InlineData("if (thrown is VaultException vault) { detail = vault + \"\"; }", "vault + \"\"")]
    [InlineData("detail = $\"{(thrown as VaultException)}\";", "{(thrown as VaultException)}")]
    [InlineData("detail = BasePhrase.ErrorDetail(thrown);", "")]
    [InlineData("if (thrown is VaultException vault) { detail = SlateUniffiMethods.VaultErrorDetail(vault); }", "")]
    [InlineData("var error = work.Exception?.GetBaseException(); if (error is not null) { detail = BasePhrase.ErrorDetail(error); }", "")]
    [InlineData("if (thrown == null) { return \"\"; } _last = thrown; throw thrown;", "")]
    [InlineData("detail = Describe(thrown);", "")]
    [InlineData("detail = C.Describe(thrown);", "")]
    [InlineData("detail = this.Describe(thrown);", "")]
    [InlineData("detail = Elsewhere.Describe(thrown);", "Elsewhere.Describe(thrown)")]
    [InlineData("detail = Render(thrown);", "Render(thrown)")]
    public void TheCensusNamesAnExceptionTurnedIntoText(string body, string named)
    {
        SyntaxNode root = CSharpSyntaxTree.ParseText(
            "class C { Exception? _last; string M(Exception thrown, Task work) { string detail = \"\"; " + body
            + " return detail; } string Describe(VaultException failure) => BasePhrase.ErrorDetail(failure);"
            + " string Render(object value) => value.ToString(); }").GetRoot();
        AssertNamed([.. ExceptionTextUses(root, "planted.cs", new HashSet<string>(CensusedConsumers(root)))], named);
    }

    /// <summary>A censused consumer's own body is held to the rule: an
    /// exception parameter it stringifies is named.</summary>
    [Fact]
    public void ACensusedConsumersBodyIsCheckedToo()
    {
        SyntaxNode root = CSharpSyntaxTree.ParseText(
            "class C { string M(Exception thrown) => Describe(thrown);"
            + " string Describe(VaultException failure) => $\"Open failed: {failure}\"; }").GetRoot();
        AssertNamed([.. ExceptionTextUses(root, "planted.cs", new HashSet<string>(CensusedConsumers(root)))], "{failure}");
    }

    /// <summary>Methods an exception may be handed to: core's renderers, and
    /// the consumers that never speak it.</summary>
    private static readonly HashSet<string> ExceptionConsumers =
    [
        "ErrorDetail",
        "VaultErrorDetail",
        "SetException",
        "TrySetException",
    ];

    /// <summary>Every use of an exception value outside the allowed shapes,
    /// per member; <c>ErrorDetail</c> is exempt.</summary>
    internal static IEnumerable<string> ExceptionTextUses(SyntaxNode root, string file, IReadOnlySet<string> censused)
    {
        HashSet<string> fields = [.. root.DescendantNodes().SelectMany(node => node switch
        {
            FieldDeclarationSyntax field when IsException(field.Declaration.Type) =>
                field.Declaration.Variables.Select(variable => variable.Identifier.ValueText),
            PropertyDeclarationSyntax property when IsException(property.Type) => [property.Identifier.ValueText],
            _ => [],
        })];
        foreach (MemberDeclarationSyntax member in root.DescendantNodes().OfType<MemberDeclarationSyntax>()
            .Where(candidate => candidate is BaseMethodDeclarationSyntax or BasePropertyDeclarationSyntax))
        {
            if (member is MethodDeclarationSyntax { Identifier.ValueText: "ErrorDetail" })
            {
                continue;
            }
            HashSet<string> names = ExceptionLocals(member, fields);
            foreach (ExpressionSyntax expression in member.DescendantNodes().OfType<ExpressionSyntax>())
            {
                if (!IsExceptional(expression, names)
                    || (expression.Parent is ExpressionSyntax outer && IsExceptional(outer, names) && Wraps(outer, expression))
                    || AllowedUse(expression, censused))
                {
                    continue;
                }
                yield return $"{file} line {expression.GetLocation().GetLineSpan().StartLinePosition.Line + 1}: "
                    + $"{expression} reaches text through `{Snippet(expression)}`; use BasePhrase.ErrorDetail.";
            }
        }
    }

    /// <summary>The exception locals a member declares: catch variables,
    /// typed patterns, typed parameters, locals and foreach variables, the
    /// file's exception fields, and every local an exception is copied into
    /// (to a fixed point).</summary>
    private static HashSet<string> ExceptionLocals(MemberDeclarationSyntax member, HashSet<string> fields)
    {
        HashSet<string> names = [.. fields];
        foreach (SyntaxNode node in member.DescendantNodesAndSelf())
        {
            switch (node)
            {
                case CatchDeclarationSyntax { Identifier.ValueText: { Length: > 0 } name }:
                    names.Add(name);
                    break;
                case DeclarationPatternSyntax { Designation: SingleVariableDesignationSyntax designation } pattern
                    when IsException(pattern.Type):
                    names.Add(designation.Identifier.ValueText);
                    break;
                case RecursivePatternSyntax { Type: { } type, Designation: SingleVariableDesignationSyntax designation }
                    when IsException(type):
                    names.Add(designation.Identifier.ValueText);
                    break;
                case ParameterSyntax { Type: { } type } parameter when IsException(type):
                    names.Add(parameter.Identifier.ValueText);
                    break;
                case VariableDeclarationSyntax declaration when IsException(declaration.Type):
                    names.UnionWith(declaration.Variables.Select(variable => variable.Identifier.ValueText));
                    break;
                case ForEachStatementSyntax loop when IsException(loop.Type):
                    names.Add(loop.Identifier.ValueText);
                    break;
            }
        }
        bool grew = true;
        while (grew)
        {
            grew = false;
            foreach (SyntaxNode node in member.DescendantNodes())
            {
                string? alias = node switch
                {
                    VariableDeclaratorSyntax { Initializer.Value: var value } declarator
                        when IsExceptional(value, names) => declarator.Identifier.ValueText,
                    AssignmentExpressionSyntax { Left: IdentifierNameSyntax left } assignment
                        when IsExceptional(assignment.Right, names) => left.Identifier.ValueText,
                    _ => null,
                };
                grew |= alias is not null && names.Add(alias);
            }
        }
        return names;
    }

    /// <summary>Whether <paramref name="expression"/> evaluates to an
    /// exception: a tracked name, a task's or exception's <c>Exception</c> /
    /// <c>InnerException</c>, <c>GetBaseException()</c>, an exception cast or
    /// <c>as</c>, and parentheses, <c>!</c>, <c>??</c>, <c>?:</c> and
    /// conditional access over those.</summary>
    private static bool IsExceptional(ExpressionSyntax expression, HashSet<string> names) => expression switch
    {
        IdentifierNameSyntax identifier =>
            names.Contains(identifier.Identifier.ValueText)
            && !(identifier.Parent is MemberAccessExpressionSyntax access && access.Name == identifier)
            && identifier.Parent is not MemberBindingExpressionSyntax and not NameColonSyntax,
        MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Exception" or "InnerException" } => true,
        MemberBindingExpressionSyntax { Name.Identifier.ValueText: "Exception" or "InnerException" } => true,
        InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "GetBaseException" } } => true,
        InvocationExpressionSyntax { Expression: MemberBindingExpressionSyntax { Name.Identifier.ValueText: "GetBaseException" } } => true,
        ConditionalAccessExpressionSyntax conditional => IsExceptional(conditional.WhenNotNull, names),
        ParenthesizedExpressionSyntax parenthesized => IsExceptional(parenthesized.Expression, names),
        PostfixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.SuppressNullableWarningExpression } suppressed =>
            IsExceptional(suppressed.Operand, names),
        CastExpressionSyntax cast => IsException(cast.Type),
        BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AsExpression, Right: TypeSyntax type } => IsException(type),
        BinaryExpressionSyntax { RawKind: (int)SyntaxKind.CoalesceExpression } coalesce =>
            IsExceptional(coalesce.Left, names) || IsExceptional(coalesce.Right, names),
        ConditionalExpressionSyntax choice => IsExceptional(choice.WhenTrue, names) || IsExceptional(choice.WhenFalse, names),
        _ => false,
    };

    /// <summary>Whether <paramref name="outer"/> carries
    /// <paramref name="inner"/>'s value on as the exception it evaluates to,
    /// rather than consuming it.</summary>
    private static bool Wraps(ExpressionSyntax outer, ExpressionSyntax inner) => outer switch
    {
        MemberAccessExpressionSyntax access => access.Expression == inner,
        ConditionalAccessExpressionSyntax conditional => conditional.Expression == inner || conditional.WhenNotNull == inner,
        InvocationExpressionSyntax invocation => invocation.Expression == inner,
        BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AsExpression } cast => cast.Left == inner,
        CastExpressionSyntax => true,
        _ => true,
    };

    /// <summary>The uses that never make an exception text.</summary>
    private static bool AllowedUse(ExpressionSyntax expression, IReadOnlySet<string> censused) => expression.Parent switch
    {
        ArgumentSyntax { Parent.Parent: InvocationExpressionSyntax invocation } =>
            ExceptionConsumers.Contains(InvokedName(invocation.Expression))
            || censused.Contains(ConsumerKey(invocation.Expression)),
        ArgumentSyntax { Parent.Parent: ObjectCreationExpressionSyntax creation } => IsException(creation.Type),
        ThrowStatementSyntax or ThrowExpressionSyntax => true,
        IsPatternExpressionSyntax test => test.Expression == expression,
        SwitchExpressionSyntax switchExpression => switchExpression.GoverningExpression == expression,
        SwitchStatementSyntax switchStatement => switchStatement.Expression == expression,
        EqualsValueClauseSyntax => true,
        AssignmentExpressionSyntax { Left: IdentifierNameSyntax } => true,
        AssignmentExpressionSyntax assignment => assignment.Left == expression,
        BinaryExpressionSyntax binary => binary.Kind() is SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression
            or SyntaxKind.IsExpression,
        // GetType names the type; GetBaseException, Exception and
        // InnerException hand on an exception the census follows.
        MemberAccessExpressionSyntax { Name.Identifier.ValueText: "GetType" or "GetBaseException" or "Exception" or "InnerException" } => true,
        _ => false,
    };

    private static bool IsException(TypeSyntax type) =>
        CSharpSource.Normalize(type).TrimEnd('?').EndsWith("Exception", StringComparison.Ordinal);

    private static string InvokedName(ExpressionSyntax target) => target switch
    {
        MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
        MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText,
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        GenericNameSyntax generic => generic.Identifier.ValueText,
        _ => "",
    };

    /// <summary>The construct that consumes the exception, for the
    /// census's message: the call an argument goes to, otherwise the
    /// parent.</summary>
    private static string Snippet(ExpressionSyntax expression)
    {
        SyntaxNode context = expression.Parent is ArgumentSyntax { Parent.Parent: { } call } ? call : expression.Parent!;
        string text = context.WithoutTrivia().NormalizeWhitespace().ToString();
        return text.Length <= 80 ? text : text[..80] + "…";
    }

    /// <summary>The fail-closed rule's teeth: a caught, patterned, aliased,
    /// null-conditional or task exception's Message is named, as is an
    /// exception that borrows a model's name; a model's own Message and the
    /// helper are clean.</summary>
    [Theory]
    [InlineData("try { Work(); } catch (VaultException failure) { detail = failure.Message; }", "failure.Message")]
    [InlineData("try { Work(); } catch (VaultException failure) { return failure.Message; }", "failure.Message")]
    [InlineData("if (thrown is VaultException vault) { detail = vault.Message; }", "vault.Message")]
    [InlineData("try { Work(); } catch (VaultException failure) { _announce(new A11yEvent.BasesViewExportFailed(failure?.Message ?? \"Unknown failure\")); }", "failure?.Message")]
    [InlineData("var error = thrown; detail = error.Message;", "error.Message")]
    [InlineData("detail = work.Exception?.Message ?? \"\";", "work.Exception?.Message")]
    [InlineData("var validation = thrown; detail = validation.Message;", "validation.Message")]
    [InlineData("try { Work(); } catch (VaultException failure) { detail = BasePhrase.ErrorDetail(failure); }", "")]
    [InlineData("BaseExpressionValidation validation = Validate(); detail = validation.Message ?? \"Expression invalid\";", "")]
    [InlineData("var section = new DashboardSectionViewModel(status); section.Message = BasePhrase.ErrorDetail(thrown);", "")]
    [InlineData("foreach (DashboardSectionViewModel section in Sections) { detail = section.Message; }", "")]
    [InlineData("if (thrown is { Message: var text }) { _announce(new A11yEvent.BasesViewExportFailed(text)); }", "thrown is { Message: … }")]
    [InlineData("if (thrown is VaultException { Message: var text }) { detail = text; }", "thrown is { Message: … }")]
    [InlineData("if (thrown is not null and { Message: var text }) { detail = text; }", "thrown is { Message: … }")]
    [InlineData("detail = thrown switch { { Message: var text } => text, _ => \"\" };", "thrown is { Message: … }")]
    [InlineData("switch (thrown) { case { Message: var text }: detail = text; break; }", "thrown is { Message: … }")]
    [InlineData("if (thrown is { InnerException.Message: var text }) { detail = text; }", "thrown is { InnerException.Message: … }")]
    [InlineData("if (thrown is { InnerException: { Message: var text } }) { detail = text; }", "a nested pattern is { Message: … }")]
    [InlineData("foreach (DashboardSectionViewModel section in Sections) { if (section is { Message: { Length: > 0 } text }) { detail = text; } }", "")]
    [InlineData("if (Current is DashboardSectionViewModel { Message: var text }) { detail = text; }", "")]
    public void TheCensusNamesEveryMessageReadOutsideAModel(string body, string named)
    {
        SyntaxNode root = CSharpSyntaxTree.ParseText(
            "class C { string M(Exception thrown, Task work) { string detail = \"\"; " + body + " return detail; } }").GetRoot();
        AssertNamed([.. UnmodelledMessageReads(root, "planted.cs")], named);
    }

    /// <summary>The helper's own read is exempt; the same read in any other
    /// member is not.</summary>
    [Theory]
    [InlineData("ErrorDetail", "")]
    [InlineData("Describe", "failure.Message")]
    public void OnlyTheHelperReadsAnExceptionsMessage(string member, string named)
    {
        SyntaxNode root = CSharpSyntaxTree.ParseText(
            "static class BasePhrase { internal static string " + member + "(Exception failure) =>"
            + " failure is VaultException vault ? SlateUniffiMethods.VaultErrorDetail(vault) : failure.Message; }").GetRoot();
        AssertNamed([.. UnmodelledMessageReads(root, "planted.cs")], named);
    }

    /// <summary>The announcement rule's teeth: a Message read in the
    /// constructor's argument, bare, interpolated, named or null-conditional,
    /// is named; core's rendering through the helper is clean.</summary>
    [Theory]
    [InlineData("_announce(new A11yEvent.BasesCellEditFailed(failure.Message));", "failure.Message")]
    [InlineData("_announce(new A11yEvent.BasesViewExportFailed($\"Export failed: {failure.Message}\"));", "failure.Message")]
    [InlineData("_announce(new A11yEvent.BasesSavedQueryCreateFailed(Detail: failure.Message));", "failure.Message")]
    [InlineData("_announce(new A11yEvent.BasesViewExportFailed(failure?.Message ?? \"Unknown failure\"));", "failure?.Message")]
    [InlineData("_announce(new A11yEvent.BasesCellEditFailed(BasePhrase.ErrorDetail(failure)));", "")]
    [InlineData("_announce(new A11yEvent.BasesCellEditFailed(SlateUniffiMethods.VaultErrorDetail(failure)));", "")]
    public void TheCensusNamesAMessageRead(string body, string named)
    {
        SyntaxNode root = CSharpSyntaxTree.ParseText(
            "class C { void M(VaultException failure) { " + body + " } }").GetRoot();
        AssertNamed([.. AnnouncementMessageReads(root, "planted.cs")], named);
    }

    private static void AssertNamed(string[] failures, string named)
    {
        if (named.Length == 0)
        {
            Assert.Empty(failures);
        }
        else
        {
            Assert.Contains(failures, failure => failure.Contains(named, StringComparison.Ordinal));
        }
    }

    /// <summary>A Message read inside an A11yEvent construction's
    /// arguments.</summary>
    internal static IEnumerable<string> AnnouncementMessageReads(SyntaxNode root, string file) =>
        root.DescendantNodes()
            .OfType<ObjectCreationExpressionSyntax>()
            .Where(creation => CSharpSource.Normalize(creation.Type).StartsWith("A11yEvent.", StringComparison.Ordinal))
            .SelectMany(creation => (creation.ArgumentList?.Arguments ?? default)
                .SelectMany(argument => MessageReads(argument))
                .Select(read =>
                    $"{file} line {creation.GetLocation().GetLineSpan().StartLinePosition.Line + 1}: "
                    + $"{CSharpSource.Normalize(creation.Type)} reads {read.Text}; use BasePhrase.ErrorDetail."));

    /// <summary>Every Message read outside <c>ErrorDetail</c> whose receiver
    /// is not a local, foreach variable or parameter the enclosing member
    /// declares with a <see cref="MessageModels"/> type.</summary>
    internal static IEnumerable<string> UnmodelledMessageReads(SyntaxNode root, string file)
    {
        foreach ((SyntaxNode node, ExpressionSyntax? receiver, string text, TypeSyntax? patternType) in MessageReads(root))
        {
            MemberDeclarationSyntax? member = node.FirstAncestorOrSelf<MemberDeclarationSyntax>(
                candidate => candidate is BaseMethodDeclarationSyntax or BasePropertyDeclarationSyntax or FieldDeclarationSyntax);
            if (member is MethodDeclarationSyntax { Identifier.ValueText: "ErrorDetail" })
            {
                continue;
            }
            if (patternType is not null && IsModel(patternType))
            {
                continue;
            }
            if (receiver is IdentifierNameSyntax { Identifier.ValueText: var name }
                && member is not null
                && DeclaresAModel(member, name))
            {
                continue;
            }
            yield return $"{file} line {node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}: "
                + $"{text} is not read from a known model; use BasePhrase.ErrorDetail, "
                + "or add the receiver's type to MessageModels with its reason.";
        }
    }

    /// <summary>Every <c>x.Message</c>, <c>x?.Message</c> and property
    /// pattern on <c>Message</c> (<c>x is { Message: var m }</c>, an extended
    /// <c>{ Inner.Message: … }</c>, a switch arm; codex round 3) under
    /// <paramref name="scope"/>, with its receiver when one is known and the
    /// pattern's type when it names one.</summary>
    private static IEnumerable<(SyntaxNode Node, ExpressionSyntax? Receiver, string Text, TypeSyntax? PatternType)> MessageReads(
        SyntaxNode scope)
    {
        foreach (SyntaxNode node in scope.DescendantNodesAndSelf())
        {
            switch (node)
            {
                case MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Message" } access
                    when access.Parent is not SubpatternSyntax and not ExpressionColonSyntax:
                    yield return (access, access.Expression, $"{access.Expression}.Message", null);
                    break;
                case MemberBindingExpressionSyntax { Name.Identifier.ValueText: "Message" } binding
                    when binding.FirstAncestorOrSelf<ConditionalAccessExpressionSyntax>() is { } conditional:
                    yield return (binding, conditional.Expression, $"{conditional.Expression}?.Message", null);
                    break;
                case SubpatternSyntax { ExpressionColon.Expression: var colon } subpattern when NamesMessage(colon):
                    var recursive = subpattern.Parent?.Parent as RecursivePatternSyntax;
                    ExpressionSyntax? governing = PatternReceiver(recursive);
                    bool extended = colon is MemberAccessExpressionSyntax;
                    yield return (
                        subpattern,
                        extended ? null : governing,
                        $"{governing?.ToString() ?? "a nested pattern"} is {{ {colon}: … }}",
                        extended ? null : recursive?.Type);
                    break;
            }
        }
    }

    private static bool NamesMessage(ExpressionSyntax colon) => colon switch
    {
        IdentifierNameSyntax { Identifier.ValueText: "Message" } => true,
        MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Message" } => true,
        _ => false,
    };

    /// <summary>The expression a recursive pattern tests: the left of an
    /// <c>is</c>, a switch expression's or statement's governing expression,
    /// through <c>and</c>/<c>or</c>/<c>not</c> and parentheses. Null for a
    /// nested pattern, whose receiver is a member the census can't type.</summary>
    private static ExpressionSyntax? PatternReceiver(RecursivePatternSyntax? pattern)
    {
        SyntaxNode? node = pattern;
        while (node?.Parent is BinaryPatternSyntax or UnaryPatternSyntax or ParenthesizedPatternSyntax)
        {
            node = node.Parent;
        }
        return node?.Parent switch
        {
            IsPatternExpressionSyntax test => test.Expression,
            SwitchExpressionArmSyntax { Parent: SwitchExpressionSyntax switchExpression } => switchExpression.GoverningExpression,
            CasePatternSwitchLabelSyntax { Parent.Parent: SwitchStatementSyntax switchStatement } => switchStatement.Expression,
            _ => null,
        };
    }

    /// <summary>Whether <paramref name="member"/> declares <paramref name="name"/>
    /// with a model type: a typed local, <c>var x = new Model(...)</c>, a
    /// typed foreach variable or a typed parameter. Any other declaration of
    /// the name in the member (an alias of an exception) disqualifies it.</summary>
    private static bool DeclaresAModel(MemberDeclarationSyntax member, string name)
    {
        bool modelled = false;
        foreach (SyntaxNode node in member.DescendantNodesAndSelf())
        {
            bool? declared = node switch
            {
                VariableDeclaratorSyntax declarator when declarator.Identifier.ValueText == name
                    && declarator.Parent is VariableDeclarationSyntax declaration =>
                    IsModel(declaration.Type)
                    || (declaration.Type.IsVar && declarator.Initializer?.Value is ObjectCreationExpressionSyntax creation
                        && IsModel(creation.Type)),
                ForEachStatementSyntax loop when loop.Identifier.ValueText == name => IsModel(loop.Type),
                ParameterSyntax { Type: { } type } parameter when parameter.Identifier.ValueText == name => IsModel(type),
                SingleVariableDesignationSyntax designation when designation.Identifier.ValueText == name => false,
                CatchDeclarationSyntax catchDeclaration when catchDeclaration.Identifier.ValueText == name => false,
                _ => null,
            };
            if (declared == false)
            {
                return false;
            }
            modelled |= declared == true;
        }
        return modelled;
    }

    private static bool IsModel(TypeSyntax type) =>
        MessageModels.Contains(CSharpSource.Normalize(type).TrimEnd('?'));
}
