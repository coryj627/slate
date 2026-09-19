// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SlateWindows.Tests;

/// <summary>Automation-id construction sites, including symbolic bindings and
/// helper inputs. Expressions are deliberately retained: an interpolated id is
/// a family, not a claim that a placeholder is a runtime automation id.</summary>
internal static class AutomationIdInventory
{
    internal sealed record Site(string Source, string Expression);

    internal static Site[] Read()
    {
        var sites = new HashSet<Site>();
        IReadOnlyDictionary<ISymbol, (string Name, int Index)[]> helpers = HelperParameters(
            ShellCompilation.Sources.Select(s => ((SyntaxNode)s.Source.Root, ShellCompilation.ModelFor(s.Source))));
        foreach ((string relative, CSharpSource source) in ShellCompilation.Sources)
        {
            foreach (string expression in CSharpExpressions(source.Root, ShellCompilation.ModelFor(source), helpers))
            {
                sites.Add(new(relative, expression));
            }
        }
        foreach (string path in Directory.EnumerateFiles(SourceText.ShellSourceRoot(), "*.xaml", SearchOption.AllDirectories)
            .Where(p => !Path.GetRelativePath(SourceText.ShellSourceRoot(), p).Split(Path.DirectorySeparatorChar).Any(s => s is "obj" or "bin")))
        {
            string relative = Path.GetRelativePath(SourceText.ShellSourceRoot(), path).Replace('\\', '/');
            foreach (string expression in XamlExpressions(XDocument.Load(path)))
            {
                sites.Add(new(relative, expression));
            }
        }
        return [.. sites.OrderBy(s => s.Source, StringComparer.Ordinal).ThenBy(s => s.Expression, StringComparer.Ordinal)];
    }

    /// <summary>The explicit ids (attributes and style setters) and, on an
    /// element that declares none of the id properties, its <c>x:Name</c> or
    /// <c>Name</c>: WPF's FrameworkElementAutomationPeer answers the element's
    /// Name as its AutomationId when none is set, so a named element is a
    /// runtime id (the shell journeys locate <c>MainMenu</c> that way).</summary>
    internal static IEnumerable<string> XamlExpressions(XDocument document)
    {
        foreach (XElement element in document.Descendants())
        {
            XAttribute[] ids = [.. element.Attributes().Where(a => IsIdProperty(a.Name.LocalName))];
            foreach (XAttribute id in ids) { yield return id.Value; }
            if (ids.Length == 0 && element.Attributes().FirstOrDefault(a => a.Name.LocalName == "Name") is { } name)
            {
                yield return name.Value;
            }
            if (element.Name.LocalName == "Setter" && IsIdProperty((string?)element.Attribute("Property") ?? ""))
            {
                yield return (string?)element.Attribute("Value") ?? element.ToString(SaveOptions.DisableFormatting);
            }
        }
    }

    internal static IEnumerable<string> CSharpExpressions(SyntaxNode root, SemanticModel model) =>
        CSharpExpressions(root, model, HelperParameters([(root, model)]));

    /// <summary>The setter's helper parameters, tracked at EVERY call site too:
    /// a newly added Notice(..., "NewId") as well as a new direct setter,
    /// including suffixes such as id + "Value". Built over the whole
    /// compilation so a helper called from another file is seen, and closed
    /// over helpers of helpers: a method whose own id-named parameter flows
    /// into a helper's id parameter is a helper as well.</summary>
    internal static IReadOnlyDictionary<ISymbol, (string Name, int Index)[]> HelperParameters(
        IEnumerable<(SyntaxNode Root, SemanticModel Model)> sources)
    {
        var helpers = new Dictionary<ISymbol, (string Name, int Index)[]>(SymbolEqualityComparer.Default);
        var candidates = new List<(MethodDeclarationSyntax Method, SemanticModel Model)>();
        foreach ((SyntaxNode root, SemanticModel model) in sources)
        {
            RequireParsed(root);
            foreach (MethodDeclarationSyntax method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                (string Name, int Index)[] parameters = [.. method.ParameterList.Parameters
                    .Select((parameter, index) => (Name: parameter.Identifier.ValueText, Index: index))
                    .Where(parameter => IsIdParameter(parameter.Name))];
                if (parameters.Length == 0) { continue; }
                if (method.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(IsSetter)
                    || method.DescendantNodes().OfType<AssignmentExpressionSyntax>().Any(a => IsIdProperty(MemberName(a.Left))))
                {
                    helpers.Add(Declared(method, model), parameters);
                }
                else { candidates.Add((method, model)); }
            }
        }
        for (bool grew = true; grew;)
        {
            grew = false;
            foreach ((MethodDeclarationSyntax method, SemanticModel model) in candidates)
            {
                IMethodSymbol symbol = Declared(method, model);
                if (helpers.ContainsKey(symbol)) { continue; }
                var flowing = new HashSet<(string Name, int Index)>();
                foreach (InvocationExpressionSyntax call in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    if (model.GetSymbolInfo(call).Symbol is not IMethodSymbol target
                        || !helpers.TryGetValue(target.OriginalDefinition, out var targetParameters)) { continue; }
                    foreach ((string name, int index) in targetParameters)
                    {
                        if (Argument(call.ArgumentList.Arguments, name, index)?.Expression is IdentifierNameSyntax identifier
                            && model.GetSymbolInfo(identifier).Symbol is IParameterSymbol parameter
                            && IsIdParameter(parameter.Name)
                            && SymbolEqualityComparer.Default.Equals(parameter.ContainingSymbol, symbol))
                        {
                            flowing.Add((parameter.Name, parameter.Ordinal));
                        }
                    }
                }
                if (flowing.Count > 0)
                {
                    helpers.Add(symbol, [.. flowing]);
                    grew = true;
                }
            }
        }
        return helpers;
    }

    internal static IEnumerable<string> CSharpExpressions(SyntaxNode root, SemanticModel model,
        IReadOnlyDictionary<ISymbol, (string Name, int Index)[]> helpers)
    {
        RequireParsed(root);
        var helperNames = helpers.Keys.Select(helper => helper.Name).ToHashSet(StringComparer.Ordinal);
        foreach (InvocationExpressionSyntax call in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (IsSetter(call) && call.ArgumentList.Arguments.Count == 2)
            {
                ArgumentSyntax? value = Argument(call.ArgumentList.Arguments, "value", 1);
                if (value is not null) { yield return Normalize(value.Expression); }
            }
            if (model.GetSymbolInfo(call).Symbol is not IMethodSymbol method)
            {
                // A helper's call that does not bind is not a skipped input:
                // the census says when binding yields nothing (ShellCompilation).
                if (helperNames.Contains(MemberName(call.Expression)))
                {
                    throw new InvalidOperationException(
                        $"Cannot bind automation ID helper call {call} at {call.GetLocation().GetLineSpan()}");
                }
                continue;
            }
            if (helpers.TryGetValue(method.OriginalDefinition, out var parameters))
            {
                foreach ((string name, int index) in parameters)
                {
                    ArgumentSyntax? argument = Argument(call.ArgumentList.Arguments, name, index);
                    if (argument is not null) { yield return Normalize(argument.Expression); }
                }
            }
        }
        foreach (AssignmentExpressionSyntax assignment in root.DescendantNodes().OfType<AssignmentExpressionSyntax>()
            .Where(a => IsIdProperty(MemberName(a.Left))))
        {
            yield return Normalize(assignment.Right);
        }
        foreach (ObjectCreationExpressionSyntax creation in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
        {
            if (creation.Type.ToString().Split('.').Last() == "Setter"
                && creation.ArgumentList?.Arguments is { Count: 2 } arguments
                && Argument(arguments, "property", 0)?.Expression is { } property
                && MemberName(property) == "AutomationIdProperty"
                && Argument(arguments, "value", 1)?.Expression is { } value)
            {
                yield return Normalize(value);
            }
        }
        foreach (VariableDeclaratorSyntax variable in root.DescendantNodes().OfType<VariableDeclaratorSyntax>()
            .Where(v => (IsIdProperty(v.Identifier.ValueText) || IsIdField(v.Identifier.ValueText)) && v.Initializer is not null))
        {
            yield return Normalize(variable.Initializer!.Value);
        }
        foreach (MethodDeclarationSyntax method in root.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Where(m => m.Identifier.ValueText == "GetAutomationIdCore"))
        {
            if (method.ExpressionBody is { } body) { yield return Normalize(body.Expression); }
            foreach (ReturnStatementSyntax statement in method.DescendantNodes().OfType<ReturnStatementSyntax>())
            {
                if (statement.Expression is { } expression) { yield return Normalize(expression); }
            }
        }
    }

    private static bool IsIdProperty(string name) => name is "AutomationProperties.AutomationId"
        or "AutomationId" or "GridAutomationId" or "AutomationIdRoot" or "AutomationIdPrefix";

    /// <summary>A property's backing field, <c>_automationIdRoot</c>: its
    /// initializer is the default the property never assigns.</summary>
    private static bool IsIdField(string name) =>
        name.Length > 1 && name[0] == '_' && IsIdProperty(char.ToUpperInvariant(name[1]) + name[2..]);

    private static bool IsIdParameter(string name) => name is "automationId" or "id" or "idRoot";

    private static IMethodSymbol Declared(MethodDeclarationSyntax method, SemanticModel model) =>
        model.GetDeclaredSymbol(method)
        ?? throw new InvalidOperationException($"Cannot bind automation ID helper {method.Identifier.ValueText} at {method.GetLocation()}");

    private static void RequireParsed(SyntaxNode root)
    {
        // Parse errors leave a partial tree; a parser warning (#warning) does not.
        if (root.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
        {
            throw new ArgumentException("Automation ID inventory requires syntactically valid source.", nameof(root));
        }
    }

    private static bool IsSetter(InvocationExpressionSyntax call) => MemberName(call.Expression) == "SetAutomationId";

    private static ArgumentSyntax? Argument(SeparatedSyntaxList<ArgumentSyntax> arguments, string name, int index) =>
        arguments.FirstOrDefault(a => a.NameColon?.Name.Identifier.ValueText == name)
        ?? (arguments.ElementAtOrDefault(index) is { NameColon: null } positional ? positional : null);

    private static string MemberName(ExpressionSyntax expression) => expression switch
    {
        MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
        IdentifierNameSyntax name => name.Identifier.ValueText,
        _ => "",
    };

    private static string Normalize(ExpressionSyntax expression) => expression.WithoutTrivia().NormalizeWhitespace().ToFullString();
}
