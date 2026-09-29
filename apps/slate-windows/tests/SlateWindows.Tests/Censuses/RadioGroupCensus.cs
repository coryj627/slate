// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// W7-7 PR 4 (#1247, contract R-5; the owner's S2): every radio button in
// the shell is in a Windows radio group — its Panel carries
// RadioGroupArrows (an arrow checks the radio it reaches, before it takes
// the keys), DirectionalNavigation=Cycle (no other arrow walks out) and
// TabNavigation=Once (one Tab stop, the checked radio). WPF's own radios
// move focus alone: the Bibliography and History segments did, so an
// arrow announced a segment the leaf had not switched to and the next one
// left the leaf (the completeness sweep's G2). The census reads every
// radio the shell's XAML declares, and every radio its code builds — by
// binding each `new RadioButton` to the class that creates it, which is
// constructed and walked, or, where its radios are built later from a
// model, by the panel field its code adds them to.

using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "radio-groups")]
public sealed class RadioGroupCensus
{
    private const string Arrows = "RadioGroupArrows.IsEnabled";

    [Fact]
    public void EveryRadioTheXamlDeclaresIsInAnArrowGroup()
    {
        var offenders = new List<string>();
        int radios = 0;
        foreach (string path in Directory.EnumerateFiles(SourceText.ShellSourceRoot(), "*.xaml", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)))
        {
            XDocument document = XDocument.Load(path, LoadOptions.SetLineInfo);
            foreach (XElement radio in document.Descendants().Where(element => element.Name.LocalName == "RadioButton"))
            {
                radios++;
                XElement? panel = radio.Parent;
                string? missing = panel is null ? "no parent panel" : MissingOnPanel(
                    (string?)panel.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == Arrows),
                    (string?)panel.Attribute("KeyboardNavigation.DirectionalNavigation"),
                    (string?)panel.Attribute("KeyboardNavigation.TabNavigation"));
                if (missing is not null)
                {
                    offenders.Add($"{Path.GetFileName(path)}:{((System.Xml.IXmlLineInfo)radio).LineNumber} "
                        + $"<RadioButton {(string?)radio.Attributes().FirstOrDefault(a => a.Name.LocalName == "Name")}>: {missing}");
                }
            }
        }

        Assert.True(radios >= 6, $"only {radios} XAML radios were found; the scrape is broken.");
        Assert.True(
            offenders.Count == 0,
            "Radios outside a Windows radio group (R-5, S2):\n  " + string.Join("\n  ", offenders));
    }

    [Fact]
    public void EveryRadioTheCodeBuildsIsInAnArrowGroup()
    {
        CSharpCompilation compilation = ShellCompilation.Compilation;
        INamedTypeSymbol radioButton = compilation.GetTypeByMetadataName("System.Windows.Controls.RadioButton")
            ?? throw new Xunit.Sdk.XunitException("RadioButton did not bind.");
        var creators = new HashSet<string>(StringComparer.Ordinal);
        var addedToFields = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach ((string relative, CSharpSource source) in ShellCompilation.Sources)
        {
            SemanticModel model = ShellCompilation.ModelFor(source);
            foreach (SyntaxNode node in source.Root.DescendantNodes())
            {
                if (node is BaseObjectCreationExpressionSyntax creation
                    && model.GetTypeInfo(creation).Type is { } created
                    && SymbolEqualityComparer.Default.Equals(created, radioButton))
                {
                    creators.Add(Declaring(model, creation));
                }
                else if (node is InvocationExpressionSyntax invocation
                    && model.GetOperation(invocation) is IInvocationOperation { TargetMethod.Name: "Add" } add
                    && add.Arguments.Length == 1
                    && Unconverted(add.Arguments[0].Value).Type is { } argument
                    && SymbolEqualityComparer.Default.Equals(argument, radioButton)
                    && add.Instance is IPropertyReferenceOperation { Property.Name: "Children", Instance: IFieldReferenceOperation field })
                {
                    string type = Declaring(model, invocation);
                    if (!addedToFields.TryGetValue(type, out HashSet<string>? fields))
                    {
                        fields = new HashSet<string>(StringComparer.Ordinal);
                        addedToFields[type] = fields;
                    }

                    fields.Add(field.Field.Name);
                }
            }
        }

        Assert.True(creators.Count >= 3, $"only {creators.Count} classes build radios; the scrape is broken.");
        var offenders = new List<string>();
        RunSta(() =>
        {
            foreach (string creator in creators.OrderBy(name => name, StringComparer.Ordinal))
            {
                Type type = typeof(MainWindow).Assembly.GetType(creator)
                    ?? throw new Xunit.Sdk.XunitException($"{creator} did not load.");
                object instance = Activator.CreateInstance(type, nonPublic: true)
                    ?? throw new Xunit.Sdk.XunitException($"{creator} could not be constructed.");
                RadioButton[] built = [.. Descendants((DependencyObject)instance).OfType<RadioButton>()];
                foreach (RadioButton radio in built)
                {
                    if (radio.Parent is not Panel panel)
                    {
                        offenders.Add($"{creator}: a radio outside a panel");
                    }
                    else if (MissingOnPanel(panel) is { } missing)
                    {
                        offenders.Add($"{creator}: {AutomationName(radio)}: {missing}");
                    }
                }

                // Radios built later, from a model: the panel their code
                // adds them to, as constructed.
                foreach (string fieldName in addedToFields.GetValueOrDefault(creator) ?? [])
                {
                    FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                        ?? throw new Xunit.Sdk.XunitException($"{creator}.{fieldName} did not bind.");
                    if (field.GetValue(instance) is not Panel panel)
                    {
                        offenders.Add($"{creator}.{fieldName}: radios are added to something that is not a panel");
                    }
                    else if (MissingOnPanel(panel) is { } missing)
                    {
                        offenders.Add($"{creator}.{fieldName}: {missing}");
                    }
                }

                if (built.Length == 0 && addedToFields.GetValueOrDefault(creator) is not { Count: > 0 })
                {
                    offenders.Add($"{creator}: builds radios the census can neither see nor trace to a panel field");
                }
            }
        });

        Assert.True(
            offenders.Count == 0,
            "Code-built radios outside a Windows radio group (R-5, S2):\n  " + string.Join("\n  ", offenders));
    }

    private static string? MissingOnPanel(Panel panel) => MissingOnPanel(
        RadioGroupArrows.GetIsEnabled(panel) ? "True" : null,
        KeyboardNavigation.GetDirectionalNavigation(panel).ToString(),
        KeyboardNavigation.GetTabNavigation(panel).ToString());

    private static string? MissingOnPanel(string? arrows, string? directional, string? tab)
    {
        var missing = new List<string>();
        if (arrows != "True")
        {
            missing.Add("RadioGroupArrows.IsEnabled");
        }

        if (directional != "Cycle")
        {
            missing.Add("DirectionalNavigation=Cycle");
        }

        if (tab != "Once")
        {
            missing.Add("TabNavigation=Once");
        }

        return missing.Count == 0 ? null : "its panel lacks " + string.Join(", ", missing);
    }

    private static IOperation Unconverted(IOperation operation) =>
        operation is IConversionOperation { IsImplicit: true } conversion ? conversion.Operand : operation;

    private static string Declaring(SemanticModel model, SyntaxNode node) =>
        model.GetEnclosingSymbol(node.SpanStart)?.ContainingType is { } type
            ? type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty, StringComparison.Ordinal)
            : throw new Xunit.Sdk.XunitException($"no containing type at {node.GetLocation().GetLineSpan()}");

    private static string AutomationName(RadioButton radio) =>
        System.Windows.Automation.AutomationProperties.GetAutomationId(radio) is { Length: > 0 } id ? id : radio.Content?.ToString() ?? "a radio";

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        foreach (object child in LogicalTreeHelper.GetChildren(root))
        {
            if (child is DependencyObject element)
            {
                yield return element;
                foreach (DependencyObject nested in Descendants(element))
                {
                    yield return nested;
                }
            }
        }
    }

    private static void RunSta(Action body) =>
        StaThread.RunPumped(body, TimeSpan.FromSeconds(60), "the radio census timed out.");
}
