// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Xml.Linq;
using Microsoft.CodeAnalysis;
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
            foreach (string expression in CSharpExpressions(source.Root))
            {
                sites.Add(new(relative, expression));
            }
        }
        foreach (string path in Directory.EnumerateFiles(SourceText.ShellSourceRoot(), "*.xaml", SearchOption.AllDirectories)
            .Where(p => !Path.GetRelativePath(SourceText.ShellSourceRoot(), p).Split(Path.DirectorySeparatorChar).Any(s => s is "obj" or "bin")))
        {
            string relative = Path.GetRelativePath(SourceText.ShellSourceRoot(), path).Replace('\\', '/');
            foreach (XAttribute attribute in XDocument.Load(path).Descendants().Attributes()
                .Where(a => IsIdProperty(a.Name.LocalName)))
            {
                sites.Add(new(relative, attribute.Value));
            }
        }
        return [.. sites.OrderBy(s => s.Source, StringComparer.Ordinal).ThenBy(s => s.Expression, StringComparer.Ordinal)];
    }

    internal static IEnumerable<string> CSharpExpressions(SyntaxNode root)
    {
        // The setter's helper parameters are tracked at EVERY call site too.
        // This catches a newly added Notice(..., "NewId") as well as a new
        // direct setter, including suffixes such as id + "Value".
        var helperParameters = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .SelectMany(m => m.ParameterList.Parameters.Select((p, i) => (Method: m, Parameter: p, Index: i)))
            .Where(p => p.Parameter.Identifier.ValueText is "automationId" or "id" or "idRoot")
            .Where(p => p.Method.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(IsSetter)
                || p.Method.DescendantNodes().OfType<AssignmentExpressionSyntax>().Any(a => IsIdProperty(MemberName(a.Left))))
            .GroupBy(p => p.Method.Identifier.ValueText)
            .ToDictionary(g => g.Key, g => g.Select(p => (p.Parameter.Identifier.ValueText, p.Index)).Distinct().ToArray());

        foreach (InvocationExpressionSyntax call in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (IsSetter(call) && call.ArgumentList.Arguments.Count == 2)
            {
                yield return Normalize(call.ArgumentList.Arguments[1].Expression);
            }
            if (helperParameters.TryGetValue(MemberName(call.Expression), out var parameters))
            {
                foreach ((string name, int index) in parameters)
                {
                    ArgumentSyntax? argument = call.ArgumentList.Arguments.FirstOrDefault(a => a.NameColon?.Name.Identifier.ValueText == name)
                        ?? call.ArgumentList.Arguments.ElementAtOrDefault(index);
                    if (argument is not null) { yield return Normalize(argument.Expression); }
                }
            }
        }
        foreach (AssignmentExpressionSyntax assignment in root.DescendantNodes().OfType<AssignmentExpressionSyntax>()
            .Where(a => IsIdProperty(MemberName(a.Left))))
        {
            yield return Normalize(assignment.Right);
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

    private static string MemberName(ExpressionSyntax expression) => expression switch
    {
        MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
        IdentifierNameSyntax name => name.Identifier.ValueText,
        _ => "",
    };

    private static string Normalize(ExpressionSyntax expression) => expression.WithoutTrivia().NormalizeWhitespace().ToFullString();
}
