// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

// #1890 (plan 47 CB-15a to CB-15c, CD9): a failure sentence anywhere in the
// shell says core's words. A uniffi exception's Message is the binding's
// field rendering ("@message=...", "@path=..., @reason=..."), not the error's
// text, so the shell reads an exception's Message in one place only:
// FailureDetail.Of(Exception), which is core's VaultErrorDetail for a vault
// error, the typed fields for the binding's other error kinds, each inner
// exception's detail for an aggregate, and the exception's own message
// otherwise. The helper is identified by its symbol, never by a name.
//
// Two rules run over every shell source, and no read stands as an exception:
// - Rule 1 of BasesErrorDetailCensus (MessageReads, one implementation for
//   both): no read of Message on a type deriving from System.Exception
//   (member access, conditional access, property or extended pattern)
//   outside FailureDetail.Of.
// - Rule 2's text sinks (BasesErrorDetailCensus.TextSinks): no exception, nor
//   a value that carries one, is made text directly: an interpolation hole, a
//   string concatenation, ToString(), or an argument to the framework's text
//   builders (string.Format, Concat and Join, StringBuilder, a TextWriter,
//   Convert.ToString and the like).
// The census BINDS the shell (ShellCompilation) and fails closed on binding.
//
// How the shell got here. CB-15a's trial (main 26d353ef, 2026-11-18) found 96
// reads in 32 files, held as dated rows under a baseline and ceilings while
// the chunks routed them. CB-15a routed 23 (the Files sidebar and the tag
// edit) on 2026-11-19; the census alone holds four of them: the three
// settings-store reads, which take only ordinary I/O text, and the
// synchronous tree refresh, a path only tests take. CB-15b routed 26 (Canvas/,
// Graph/ and Search/) on 2026-11-19; the census alone holds five:
// GraphConfigStore.Read, the two non-vault catches of
// ConnectionsLeafViewModel.FetchUnder and the one of
// GraphDocumentViewModel.FetchUnder take only an ordinary exception, and
// CanvasDocumentViewModel.CanvasRemoveFromGroup's catch needs a race the
// canvas lease's lock forbids. CB-15c routed the last 47 (the panels, the
// workspace partials, the reading view, the vault lifecycle, the palette's
// command wrapper, the editor's embed preview and find in note), and the rows
// went with them. The census alone holds 20 of CB-15c's: the four wraps that
// hand an ordinary fault to core's renderer (WorkspaceViewModel's task toggle
// and WorkspaceTabViewModel.WriteProperty), the five broad History catches,
// the bulk rename's turn, TaskIndexRepairCoordinator.TryRepairNow's discarded
// out value, the review's direct toggle's two, the embed preview's non-vault
// catch, find in note's pattern error, the MathML property's inactive
// reason, and the vault lifecycle's four (an empty path, the open's
// non-vault faults, the recents file's I/O).
//
// Rule 2 in full stays on the Bases sources (the lead's ruling on CB-15c's
// Call 1). Its trial over the whole shell at main 47bcf77e had 76 hits of its
// own in 29 files: 25 typed-field reads of a uniffi error, 14 stored
// exceptions, 8 captures or rethrows, 10 wrappers, 16 delegate or completion
// hand-offs, 2 ThrowIfNull and 1 stack trace into the UIA diagnostic log. None
// was a dump or an exception reaching text, and none was a text sink, which
// is why the sinks alone run here with no exception.
//
// What this census cannot see:
// - Code under an inactive #if: the shell is parsed with no preprocessor
//   symbols (CSharpSource).
// - A XAML binding path ({Binding Exception.Message}): XAML is not bound here.
// - An exception erased to object, or handed to a generic helper, an
//   interface or a delegate, and made text there: rule 2 in full names these
//   on the Bases sources only.
// - A new uniffi error kind whose Message is a field dump: it reaches
//   FailureDetail.Of's last arm and looks to rule 1 like any other exception.
//   FailureDetailTests pins the kinds the binding declares today.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "shell-failure-detail")]
public sealed class ShellFailureDetailCensus
{
    /// <summary>One read, named by its file, line and the member and type
    /// that hold it.</summary>
    internal sealed record Read(string File, string Type, string Member, int Line, string Context)
    {
        public override string ToString() =>
            $"{File} line {Line} ({Type}.{Member}): `{Context}` reads an exception's Message; use FailureDetail.Of.";
    }

    private static readonly Lazy<IMethodSymbol> Helper = new(() => BasesErrorDetailCensus.TheHelper(ShellCompilation.Compilation));

    /// <summary>Fail closed: a shell source that does not bind could hide an
    /// exception's type from the rules.</summary>
    [Fact]
    public void TheShellSourcesBindWithoutErrors()
    {
        string[] errors = [.. ShellCompilation.Sources.SelectMany(source => ShellCompilation.ModelFor(source.Source)
            .GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => $"{source.Relative}: {diagnostic}"))];
        Assert.True(errors.Length == 0, $"{errors.Length} error(s):\n" + string.Join("\n", errors.Take(20)));
    }

    [Fact]
    public void NoShellFailureBecomesTextOutsideFailureDetail()
    {
        Read[] reads = [.. ShellCompilation.Sources.SelectMany(source =>
            Reads(ShellCompilation.ModelFor(source.Source), source.Relative, Helper.Value))];
        Assert.True(
            reads.Length == 0,
            "A shell sentence reads an exception's Message, which for a uniffi error is the binding's field dump. "
            + "Say FailureDetail.Of(exception) instead:\n" + string.Join("\n", reads.Select(read => read.ToString())));
    }

    [Fact]
    public void NoShellExceptionIsMadeTextDirectly()
    {
        string[] sinks = [.. ShellCompilation.Sources.SelectMany(source =>
        {
            SemanticModel model = ShellCompilation.ModelFor(source.Source);
            return BasesErrorDetailCensus.TextSinks(
                model, source.Relative, BasesErrorDetailCensus.InTheHelper(model, Helper.Value));
        })];
        Assert.True(
            sinks.Length == 0,
            "A shell exception is made text directly (interpolation, concatenation, ToString or a text builder), "
            + "which for a uniffi error is the binding's field dump. Say FailureDetail.Of(exception) instead:\n"
            + string.Join("\n", sinks));
    }

    /// <summary>The rule's shapes, planted beside the shell and run through
    /// MessageReads alone: each is named, and a typed field, a record's own
    /// Message and the helper's call are not.</summary>
    [Theory]
    [InlineData("detail = thrown.Message;", "thrown.Message")]
    [InlineData("detail = thrown?.Message ?? \"\";", "thrown?.Message")]
    [InlineData("if (thrown is { Message: var text }) { detail = text; }", "Message: var text")]
    [InlineData("if (thrown is { InnerException.Message: var text }) { detail = text; }", "InnerException.Message: var text")]
    [InlineData("detail = thrown.GetBaseException().Message;", "thrown.GetBaseException().Message")]
    [InlineData("try { work.Wait(); } catch (VaultException.Io io) { detail = io.Message; }", "io.Message")]
    [InlineData("try { work.Wait(); } catch (VaultException.Io io) { detail = io.message; }", "")]
    [InlineData("detail = new Note(\"x\").Message;", "")]
    [InlineData("detail = FailureDetail.Of(thrown);", "")]
    public void TheRuleNamesEveryReadOfAnExceptionsMessage(string body, string named)
    {
        (SemanticModel model, SemanticModel? _) = Planted(PlantedMember(body));
        Read[] reads = [.. Reads(model, "Planted.cs", BasesErrorDetailCensus.TheHelper(model.Compilation))];
        if (named.Length == 0)
        {
            Assert.Empty(reads);
        }
        else
        {
            Read read = Assert.Single(reads);
            Assert.Contains(named, read.Context, StringComparison.Ordinal);
            Assert.Equal(("SlateWindows.Planted.Planted", "M"), (read.Type, read.Member));
        }
    }

    /// <summary>The text sinks, planted beside the shell and run through
    /// TextSinks alone: each way of making an exception text directly is
    /// named; storing it, passing it on, throwing it, testing it, a typed
    /// field, its type's name, and the helper's detail inside a sentence are
    /// not.</summary>
    [Theory]
    [InlineData("detail = $\"Failed: {thrown}\";", "{thrown}")]
    [InlineData("detail = $\"Failed: {thrown,10:G}\";", "{thrown")]
    [InlineData("detail = \"Failed: \" + thrown;", "\"Failed: \" + thrown")]
    [InlineData("detail = thrown + \" failed\";", "thrown + \" failed\"")]
    [InlineData("detail += thrown;", "detail += thrown")]
    [InlineData("detail = thrown.ToString();", "thrown.ToString")]
    [InlineData("detail = thrown?.ToString() ?? \"\";", "thrown?.ToString()")]
    [InlineData("detail = $\"{work.Exception}\";", "{work.Exception}")]
    [InlineData("detail = $\"{thrown.InnerException}\";", "{thrown.InnerException}")]
    [InlineData("detail = $\"{(thrown as VaultException)}\";", "{(thrown as VaultException)}")]
    [InlineData("detail = string.Format(\"{0}\", thrown);", "string.Format")]
    [InlineData("detail = string.Concat(\"Failed: \", thrown);", "string.Concat")]
    [InlineData("List<Exception> failures = [thrown]; detail = string.Join(\", \", failures);", "string.Join")]
    [InlineData("var builder = new System.Text.StringBuilder(); builder.Append(thrown); detail = builder.ToString();", "builder.Append")]
    [InlineData("detail = Convert.ToString(thrown) ?? \"\";", "Convert.ToString")]
    [InlineData("var writer = new System.IO.StringWriter(); writer.Write(thrown); detail = writer.ToString();", "writer.Write")]
    [InlineData("detail = $\"Failed: {FailureDetail.Of(thrown)}\";", "")]
    [InlineData("detail = \"Failed: \" + FailureDetail.Of(thrown);", "")]
    [InlineData("detail = $\"{thrown.GetType().Name}\";", "")]
    [InlineData("detail = thrown.HResult.ToString();", "")]
    [InlineData("try { work.Wait(); } catch (VaultException.Io io) { detail = $\"Failed: {io.message}\"; }", "")]
    [InlineData("var kept = thrown; _last = kept; Keep(thrown);", "")]
    [InlineData("if (thrown is VaultException) { throw thrown; }", "")]
    [InlineData("detail = thrown == _last ? \"same\" : \"\";", "")]
    public void TheSinksNameEveryWayAnExceptionBecomesTextDirectly(string body, string named)
    {
        (SemanticModel model, SemanticModel? _) = Planted(PlantedMember(body));
        string[] sinks = [.. BasesErrorDetailCensus.TextSinks(
            model, "Planted.cs", BasesErrorDetailCensus.InTheHelper(model, BasesErrorDetailCensus.TheHelper(model.Compilation)))];
        if (named.Length == 0)
        {
            Assert.Empty(sinks);
        }
        else
        {
            string sink = Assert.Single(sinks);
            Assert.Contains(named, sink, StringComparison.Ordinal);
        }
    }

    /// <summary>The real helper's read is exempt, by its symbol. A class named
    /// FailureDetail in another namespace with a method Of, and an overload of
    /// Of in the real class, are each named.</summary>
    [Fact]
    public void OnlyTheRealHelpersReadIsExempt()
    {
        (SemanticModel model, SemanticModel? helperModel) = Planted(
            """
            namespace SlateWindows.Planted
            {
                internal static class FailureDetail
                {
                    internal static string Of(Exception failure) => failure.Message;
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
            failureDetail: BasesErrorDetailCensus.PartialHelper);
        IMethodSymbol helper = BasesErrorDetailCensus.TheHelper(model.Compilation);
        Read[] reads = [.. Reads(model, "Planted.cs", helper), .. Reads(helperModel!, "FailureDetail.cs", helper)];
        (string File, string Type, string Member, int Line)[] named =
            [("Planted.cs", "SlateWindows.Planted.FailureDetail", "Of", 5), ("Planted.cs", "SlateWindows.FailureDetail", "Of", 13)];
        Assert.Equal(named, reads.Select(read => (read.File, read.Type, read.Member, read.Line)).OrderBy(read => read.Line));
    }

    /// <summary>A planted member holding <paramref name="body"/>, with an
    /// exception, a task and a field to work on.</summary>
    private static string PlantedMember(string body) => $$"""
        namespace SlateWindows.Planted
        {
            using uniffi.slate_uniffi;

            internal sealed class Planted
            {
                private sealed record Note(string Message);

                private Exception? _last;

                private static void Keep(Exception failure) => _ = failure;

                private string M(Exception thrown, Task work)
                {
                    string detail = "";
                    {{body}}
                    return detail;
                }
            }
        }
        """;

    /// <summary>Binds <paramref name="source"/> beside the shell (as
    /// BasesErrorDetailCensus.PlantedViolations does); it must bind cleanly or
    /// the case proves nothing. With <paramref name="failureDetail"/>, that
    /// text stands in for <c>FailureDetail.cs</c>, and its model is returned
    /// too.</summary>
    private static (SemanticModel Planted, SemanticModel? Helper) Planted(string source, string? failureDetail = null)
    {
        var options = (CSharpParseOptions)ShellCompilation.Compilation.SyntaxTrees.First().Options;
        SyntaxTree tree = CSharpSyntaxTree.ParseText(source, options, path: "Planted.cs");
        CSharpCompilation compilation = ShellCompilation.Compilation.AddSyntaxTrees(tree);
        SyntaxTree? helperTree = null;
        if (failureDetail is not null)
        {
            helperTree = CSharpSyntaxTree.ParseText(failureDetail, options, path: "FailureDetail.cs");
            compilation = compilation.ReplaceSyntaxTree(BasesErrorDetailCensus.FailureDetailTree(), helperTree);
        }
        SemanticModel model = compilation.GetSemanticModel(tree);
        SemanticModel? helperModel = helperTree is null ? null : compilation.GetSemanticModel(helperTree);
        Diagnostic[] errors = [.. model.GetDiagnostics()
            .Concat(helperModel?.GetDiagnostics() ?? [])
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)];
        Assert.True(errors.Length == 0, "premise: the planted source binds. " + string.Join("\n", errors));
        return (model, helperModel);
    }

    /// <summary>Rule 1 over one bound tree, each read named by the member and
    /// type that hold it.</summary>
    internal static IEnumerable<Read> Reads(SemanticModel model, string file, IMethodSymbol helper) =>
        BasesErrorDetailCensus.MessageReads(model, file, BasesErrorDetailCensus.InTheHelper(model, helper))
            .Select(read =>
            {
                (string type, string member) = Holder(model, read.Name);
                return new Read(file, type, member, read.Line, read.Context);
            });

    /// <summary>The declaration that holds a read's operation tree, keyed as
    /// SaveChainAdmissionCensus keys one (HoldsAnOperationTree and MemberOf),
    /// by its declared symbol: the member is the symbol's name (a method's,
    /// an explicit interface implementation's qualified name included; a
    /// finalizer's Finalize; an operator's or conversion's metadata name), a
    /// constructor, static or not, is its type's name, and an accessor, an
    /// expression-bodied property or indexer and a property initializer are
    /// their property, event or indexer (this[]). A field initializer is its
    /// field, and a primary constructor's base arguments are the type's name.
    /// A lambda or local function counts to the member around it. The type is
    /// the one that declares the member, by its full name (TypeKey).</summary>
    internal static (string Type, string Member) Holder(SemanticModel model, SyntaxNode node)
    {
        for (SyntaxNode? current = node.Parent; current is not null; current = current.Parent)
        {
            ISymbol? symbol = current switch
            {
                BaseMethodDeclarationSyntax or AccessorDeclarationSyntax => model.GetDeclaredSymbol(current),
                ArrowExpressionClauseSyntax { Parent: BasePropertyDeclarationSyntax property } => model.GetDeclaredSymbol(property),
                VariableDeclaratorSyntax { Parent.Parent: BaseFieldDeclarationSyntax } => model.GetDeclaredSymbol(current),
                BasePropertyDeclarationSyntax or BaseTypeDeclarationSyntax => model.GetDeclaredSymbol(current),
                _ => null,
            };
            if (symbol is null)
            {
                continue;
            }
            INamedTypeSymbol? type = symbol as INamedTypeSymbol ?? symbol.ContainingType;
            string member = symbol switch
            {
                IMethodSymbol { MethodKind: MethodKind.Constructor or MethodKind.StaticConstructor } constructor =>
                    constructor.ContainingType.Name,
                IMethodSymbol { AssociatedSymbol: { } owner } => owner.Name,
                _ => symbol.Name,
            };
            return (type is null ? "(no type)" : TypeKey(type), member);
        }
        return ("(no type)", "(no member)");
    }

    /// <summary>A type's full name as metadata spells it: its namespace, each
    /// type it is nested in (joined by +) and its arity (Pair`2), so a type
    /// of the same name in another namespace or another type, or with
    /// another number of type parameters, reads apart.</summary>
    internal static string TypeKey(INamedTypeSymbol type)
    {
        string name = type.MetadataName;
        for (INamedTypeSymbol? outer = type.ContainingType; outer is not null; outer = outer.ContainingType)
        {
            name = outer.MetadataName + "+" + name;
        }
        return type.ContainingNamespace is { IsGlobalNamespace: false } space
            ? space.ToDisplayString() + "." + name
            : name;
    }
}
