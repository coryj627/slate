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
        foreach ((string relative, CSharpSource source) in ShellCompilation.Sources)
        {
            foreach (string expression in CSharpExpressions(source.Root, ShellCompilation.ModelFor(source)))
            {
                sites.Add(new(relative, expression));
            }
        }
        foreach (string path in Directory.EnumerateFiles(SourceText.ShellSourceRoot(), "*.xaml", SearchOption.AllDirectories)
            .Where(p => !Path.GetRelativePath(SourceText.ShellSourceRoot(), p).Split(Path.DirectorySeparatorChar).Any(s => s is "obj" or "bin")))
        {
            string relative = Path.GetRelativePath(SourceText.ShellSourceRoot(), path).Replace('\\', '/');
            XDocument document = XDocument.Load(path);
            foreach (XAttribute attribute in document.Descendants().Attributes()
                .Where(a => IsIdProperty(a.Name.LocalName)))
            {
                sites.Add(new(relative, attribute.Value));
            }
            foreach (XElement setter in document.Descendants().Where(e => e.Name.LocalName == "Setter"
                && IsIdProperty((string?)e.Attribute("Property") ?? "")))
            {
                sites.Add(new(relative, (string?)setter.Attribute("Value") ?? setter.ToString(SaveOptions.DisableFormatting)));
            }
        }
        return [.. sites.OrderBy(s => s.Source, StringComparer.Ordinal).ThenBy(s => s.Expression, StringComparer.Ordinal)];
    }

    internal static IEnumerable<string> CSharpExpressions(SyntaxNode root, SemanticModel model)
    {
        if (root.ContainsDiagnostics)
        {
            throw new ArgumentException("Automation ID inventory requires syntactically valid source.", nameof(root));
        }
        // The setter's helper parameters are tracked at EVERY call site too.
        // This catches a newly added Notice(..., "NewId") as well as a new
        // direct setter, including suffixes such as id + "Value".
        var helperParameters = new Dictionary<ISymbol, (string Name, int Index)[]>(SymbolEqualityComparer.Default);
        foreach (MethodDeclarationSyntax method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            if (!method.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(IsSetter)
                && !method.DescendantNodes().OfType<AssignmentExpressionSyntax>().Any(a => IsIdProperty(MemberName(a.Left)))) { continue; }
            (string Name, int Index)[] parameters = [.. method.ParameterList.Parameters
                .Select((parameter, index) => (Name: parameter.Identifier.ValueText, Index: index))
                .Where(parameter => parameter.Name is "automationId" or "id" or "idRoot")];
            if (parameters.Length == 0) { continue; }
            IMethodSymbol symbol = model.GetDeclaredSymbol(method)
                ?? throw new InvalidOperationException($"Cannot bind automation ID helper {method.Identifier.ValueText} at {method.GetLocation()}");
            helperParameters.Add(symbol, parameters);
        }

        foreach (InvocationExpressionSyntax call in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (IsSetter(call) && call.ArgumentList.Arguments.Count == 2)
            {
                ArgumentSyntax? value = Argument(call.ArgumentList.Arguments, "value", 1);
                if (value is not null) { yield return Normalize(value.Expression); }
            }
            if (model.GetSymbolInfo(call).Symbol is IMethodSymbol method
                && helperParameters.TryGetValue(method.OriginalDefinition, out var parameters))
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
            .Where(v => IsIdProperty(v.Identifier.ValueText) && v.Initializer is not null))
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
