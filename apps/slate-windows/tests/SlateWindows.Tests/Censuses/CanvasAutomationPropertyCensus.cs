// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// W6-1 PR A (#745), round 8: an AutomationProperties value set on an
// element WPF never peers is INERT — it reaches no assistive technology
// and no UIA client, silently.
//
// This is the fifth instance of the properties-that-don't-reach-AT class
// in this PR's history, and the second in the canvas surfaces alone: the
// outline's Invoke sat on a container peer no client reads (round 7), and
// the surface switcher's AutomationId and "Canvas view" name sat on a
// bare StackPanel that produced no peer at all (round 8). Both were
// invisible to every gate because the journey that would have caught them
// had never run past its fixture lookup.
//
// The point of this census is that the sixth instance fails at build
// time instead. It is deliberately FAIL-CLOSED: a target whose type it
// cannot resolve is a failure, not a skip, because a blind spot here is
// indistinguishable from the bug.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "canvas-automation-properties")]
public sealed class CanvasAutomationPropertyCensus
{
    /// <summary>
    /// Element types that DO create an automation peer, so a property set
    /// on one actually reaches a client.
    /// </summary>
    /// <remarks>
    /// An allow-list rather than a deny-list of known-peerless panels, so
    /// a new element type is a conscious decision instead of a silent
    /// pass. WPF's rule is not "panels are peerless" — it is that
    /// <c>FrameworkElement.OnCreateAutomationPeer</c> returns null unless
    /// a type overrides it, which most controls do and no layout panel
    /// does.
    /// </remarks>
    private static readonly HashSet<string> PeeredTypes = new(StringComparer.Ordinal)
    {
        // WPF controls with peers of their own.
        "TreeView", "TreeViewItem", "TextBlock", "TextBox", "ListBox",
        "ListBoxItem", "RadioButton", "CheckBox", "Button", "UserControl",
        "ComboBox", "TabControl", "TabItem", "Slider", "ProgressBar",
        // This PR's peered subclasses.
        "CanvasOutlineTree", "CanvasOutlineItem", "CanvasSurfaceView",
        // The shared peered containers (AutomationLandmark.cs) — the
        // whole reason that file exists.
        "AutomationNamedGroupPanel", "AutomationLandmarkGrid",
        "AutomationLandmarkBorder", "AutomationNamedRowBorder",
        "AutomationVisibilityListBox", "AutomationPresentationTextBlock",
        "AutomationPresentationItemsControl",
    };

    /// <summary>
    /// Every <c>AutomationProperties.SetX(target, …)</c> in the canvas
    /// surfaces targets an element type that has an automation peer.
    /// </summary>
    /// <remarks>
    /// Mutation-verified: setting a Name or AutomationId on a bare
    /// <c>StackPanel</c> fails this census, naming the file, the line and
    /// the type.
    /// </remarks>
    [Fact]
    public void NoAutomationPropertySitsOnAnUnpeeredElement()
    {
        var offenders = new List<string>();
        var checkedSites = 0;

        foreach (string file in CanvasSources())
        {
            string label = Path.GetFileName(file);
            CSharpSource source = CSharpSource.Load("Canvas", label);
            Dictionary<string, string> fields = FieldTypes(source.Root);

            foreach (InvocationExpressionSyntax invocation in source.Root
                .DescendantNodes()
                .OfType<InvocationExpressionSyntax>())
            {
                if (invocation.Expression is not MemberAccessExpressionSyntax access
                    || access.Expression is not IdentifierNameSyntax { Identifier.ValueText: "AutomationProperties" }
                    || !access.Name.Identifier.ValueText.StartsWith("Set", StringComparison.Ordinal)
                    || invocation.ArgumentList.Arguments.Count == 0)
                {
                    continue;
                }

                checkedSites++;
                ExpressionSyntax target = invocation.ArgumentList.Arguments[0].Expression;
                string setter = access.Name.Identifier.ValueText;
                int line = invocation.GetLocation()
                    .GetLineSpan().StartLinePosition.Line + 1;

                string? type = ResolveType(target, fields, source.Root);
                if (type is null)
                {
                    offenders.Add(
                        $"{label}:{line}: {setter} on `{target}` — the census cannot "
                        + "resolve this target's type, so it cannot tell whether the "
                        + "property reaches a client. Declare the target with an "
                        + "explicit type, or add it to the census.");
                }
                else if (!PeeredTypes.Contains(type))
                {
                    offenders.Add(
                        $"{label}:{line}: {setter} on `{target}` of type {type}, "
                        + "which creates NO automation peer — the value is inert.");
                }
            }

            // The Style-setter form: `new Setter(AutomationProperties.XProperty, …)`
            // applies to whatever the enclosing `new Style(typeof(T))` targets.
            foreach (ObjectCreationExpressionSyntax creation in source.Root
                .DescendantNodes()
                .OfType<ObjectCreationExpressionSyntax>())
            {
                if (creation.Type.ToString() != "Setter"
                    || creation.ArgumentList is not { Arguments.Count: > 0 }
                    || creation.ArgumentList.Arguments[0].Expression
                        is not MemberAccessExpressionSyntax
                        {
                            Expression: IdentifierNameSyntax { Identifier.ValueText: "AutomationProperties" },
                        } property)
                {
                    continue;
                }

                checkedSites++;
                int line = creation.GetLocation()
                    .GetLineSpan().StartLinePosition.Line + 1;
                string? styleTarget = EnclosingStyleTargetType(creation);
                if (styleTarget is null)
                {
                    offenders.Add(
                        $"{label}:{line}: {property.Name.Identifier.ValueText} in a Setter "
                        + "whose Style TargetType the census cannot resolve.");
                }
                else if (!PeeredTypes.Contains(styleTarget))
                {
                    offenders.Add(
                        $"{label}:{line}: {property.Name.Identifier.ValueText} applied via a "
                        + $"Style targeting {styleTarget}, which creates NO automation peer.");
                }
            }
        }

        // The census's own premise: a refactor that moved every
        // AutomationProperties call out of Canvas/ would otherwise leave
        // this scanning nothing and passing.
        Assert.True(
            checkedSites >= 15,
            $"only {checkedSites} AutomationProperties sites were found in the canvas "
            + "sources; the canvas surfaces set far more than that, so the scan is "
            + "reading less than the truth.");

        Assert.True(
            offenders.Count == 0,
            "An AutomationProperties value set on an element WPF never peers is "
            + "INERT — it reaches no screen reader and no UIA client, and nothing "
            + "fails. Peer the element (see AutomationLandmark.cs, e.g. "
            + "AutomationNamedGroupPanel) or delete the property:\n  "
            + string.Join("\n  ", offenders));
    }

    /// <summary>Field name → declared type, for the whole file.</summary>
    private static Dictionary<string, string> FieldTypes(SyntaxNode root)
    {
        var types = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (FieldDeclarationSyntax field in root
            .DescendantNodes()
            .OfType<FieldDeclarationSyntax>())
        {
            string declared = StripNullable(field.Declaration.Type.ToString());
            foreach (VariableDeclaratorSyntax variable in field.Declaration.Variables)
            {
                types[variable.Identifier.ValueText] = declared;
            }
        }
        return types;
    }

    /// <summary>
    /// Locals and parameters of the member CONTAINING this site.
    /// </summary>
    /// <remarks>
    /// Scoped, not file-wide: <c>BannerText</c>'s local <c>TextBlock
    /// text</c> and <c>SetBanner</c>'s <c>string text</c> parameter share
    /// a name, and a file-wide map let the string shadow the TextBlock and
    /// report a false offender. A census that cries wolf gets suppressed,
    /// which would cost more than the bug it is guarding.
    /// </remarks>
    private static Dictionary<string, string> LocalTypes(SyntaxNode site)
    {
        var types = new Dictionary<string, string>(StringComparer.Ordinal);
        SyntaxNode? member = site.Ancestors()
            .FirstOrDefault(node =>
                node is MethodDeclarationSyntax
                    or ConstructorDeclarationSyntax
                    or PropertyDeclarationSyntax
                    or AccessorDeclarationSyntax);
        if (member is null)
        {
            return types;
        }

        foreach (ParameterSyntax parameter in member
            .DescendantNodes()
            .OfType<ParameterSyntax>())
        {
            if (parameter.Type is not null)
            {
                types[parameter.Identifier.ValueText] =
                    StripNullable(parameter.Type.ToString());
            }
        }

        // Locals win over parameters of the same name: a local
        // declaration shadows an outer parameter in C# scoping terms.
        foreach (LocalDeclarationStatementSyntax local in member
            .DescendantNodes()
            .OfType<LocalDeclarationStatementSyntax>())
        {
            string declared = StripNullable(local.Declaration.Type.ToString());
            foreach (VariableDeclaratorSyntax variable in local.Declaration.Variables)
            {
                // `var x = new T { … }` carries its type on the initializer.
                string? resolved = declared == "var"
                    ? (variable.Initializer?.Value as ObjectCreationExpressionSyntax)
                        ?.Type.ToString()
                    : declared;
                if (resolved is not null)
                {
                    types[variable.Identifier.ValueText] = StripNullable(resolved);
                }
            }
        }

        return types;
    }

    private static string? ResolveType(
        ExpressionSyntax target, Dictionary<string, string> fields, SyntaxNode root)
    {
        switch (target)
        {
            case ThisExpressionSyntax:
                // `this` in a view is the view's own type.
                return root.DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
                    .Select(declaration => declaration.Identifier.ValueText)
                    .FirstOrDefault(name => PeeredTypes.Contains(name));
            case IdentifierNameSyntax identifier:
                {
                    string name = identifier.Identifier.ValueText;
                    // Innermost scope first, then fields.
                    return LocalTypes(target).GetValueOrDefault(name)
                        ?? fields.GetValueOrDefault(name);
                }
            case ObjectCreationExpressionSyntax creation:
                return StripNullable(creation.Type.ToString());
            default:
                return null;
        }
    }

    private static string? EnclosingStyleTargetType(SyntaxNode setter)
    {
        // The Setter is added to a Style built in the same method.
        MethodDeclarationSyntax? method = setter.Ancestors()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault();
        ObjectCreationExpressionSyntax? style = method?.DescendantNodes()
            .OfType<ObjectCreationExpressionSyntax>()
            .FirstOrDefault(creation => creation.Type.ToString() == "Style");
        if (style?.ArgumentList is not { Arguments.Count: > 0 })
        {
            return null;
        }
        return style.ArgumentList.Arguments[0].Expression
            is TypeOfExpressionSyntax typeOf
            ? StripNullable(typeOf.Type.ToString())
            : null;
    }

    private static string StripNullable(string type) => type.TrimEnd('?');

    private static IEnumerable<string> CanvasSources()
    {
        string root = Path.Combine(SourceText.ShellSourceRoot(), "Canvas");
        Assert.True(Directory.Exists(root), $"the canvas source root is missing: {root}");
        string[] files = Directory
            .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .OrderBy(file => file, StringComparer.Ordinal)
            .ToArray();
        Assert.NotEmpty(files);
        return files;
    }
}
