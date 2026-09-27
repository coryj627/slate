// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// EmbedPreviewTitleCensus, the XAML half: the bindings that hand the
// popover's header, its image's name, its own name and each card root to
// WPF. The C# half proves what those properties hold; this half proves the
// XAML adds nothing on the way — a StringFormat, a converter, fallback text,
// a MultiBinding, static sibling text or a static name beside the binding
// would each put host words back in front of core's. The rule and its
// history are in EmbedPreviewTitleCensus.cs.
//
// XAML is read as XML (System.Xml.Linq); a binding's markup extension is
// read by a small tokenizer that honours nested braces and quotes.

using System.Xml;
using System.Xml.Linq;

namespace SlateWindows.Tests.Censuses;

public sealed partial class EmbedPreviewTitleCensus
{
    /// <summary>What marks a XAML value as a title sink: the coordinator
    /// properties that carry a card title, the popover's name or a card
    /// root, and a card's own Title read straight off an embeds-leaf row —
    /// named from the production members themselves.</summary>
    private static readonly string[] XamlSinkProperties =
    [
        nameof(EditorInteractionCoordinator.PopoverTitle),
        nameof(EditorInteractionCoordinator.PopoverAutomationName),
        nameof(EditorInteractionCoordinator.PopoverEmbedRoot),
        $"{nameof(global::SlateWindows.Panels.EmbedRowViewModel.Node)}.{nameof(EditorEmbedPreviewNode.Title)}",
    ];

    /// <summary>What a plain binding may say besides its path.</summary>
    private static readonly string[] PlainBindingKeys = ["Path", "Mode"];

    private static readonly string[] Inlines =
        ["Run", "Span", "Bold", "Italic", "Underline", "Hyperlink", "LineBreak", "InlineUIContainer"];

    private const string Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private const string Shell = "clr-namespace:SlateWindows";

    internal sealed record XamlSink(
        string Where, string File, string Namespace, string Element, string Attribute, string Value);

    /// <summary>The title sinks the shell's XAML binds, each exactly once:
    /// element (WPF's own, or the shell's popover host and renderer),
    /// attribute and path, spelled from the production members.</summary>
    private static (string Namespace, string Element, string Attribute, string Path)[] ExpectedXamlSinks()
    {
        string popover = nameof(WorkspaceTabViewModel.EditorInteractions);
        string header = $"{popover}.{nameof(EditorInteractionCoordinator.PopoverTitle)}";
        return
        [
            (Presentation, "TextBlock", "Text", header),
            (Presentation, "Image", "AutomationProperties.Name", header),
            (Shell, PopoverHost, "AutomationProperties.Name",
                $"{popover}.{nameof(EditorInteractionCoordinator.PopoverAutomationName)}"),
            (Shell, nameof(EditorEmbedPreviewView), nameof(EditorEmbedPreviewView.Root),
                $"{popover}.{nameof(EditorInteractionCoordinator.PopoverEmbedRoot)}"),
            (Shell, nameof(EditorEmbedPreviewView), nameof(EditorEmbedPreviewView.Root),
                nameof(global::SlateWindows.Panels.EmbedRowViewModel.Node)),
        ];
    }

    internal static IReadOnlyList<(string Relative, string Text)> XamlSources()
    {
        string root = SourceText.ShellSourceRoot();
        return Directory.EnumerateFiles(root, "*.xaml", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .Where(relative => !relative.Split('/').Any(part => part is "obj" or "bin"))
            .OrderBy(relative => relative, StringComparer.Ordinal)
            .Select(relative => (relative, File.ReadAllText(Path.Combine(root, relative))))
            .ToArray();
    }

    /// <summary>Every failure of the XAML half: each title sink binding's
    /// rules, and the pinned population.</summary>
    internal static IEnumerable<string> XamlFailures(IReadOnlyList<(string Relative, string Text)> files)
    {
        (List<XamlSink> sinks, List<string> failures) = XamlScan(files);
        return failures.Concat(XamlPopulationFailures(sinks));
    }

    internal static (List<XamlSink> Sinks, List<string> Failures) XamlScan(
        IReadOnlyList<(string Relative, string Text)> files)
    {
        var sinks = new List<XamlSink>();
        var failures = new List<string>();
        foreach ((string file, string text) in files)
        {
            XDocument document;
            try
            {
                document = XDocument.Parse(text, LoadOptions.SetLineInfo);
            }
            catch (XmlException error)
            {
                failures.Add($"{file}:{error.LineNumber}: the XAML does not parse ({error.Message}), so the "
                    + "census cannot read its bindings.");
                continue;
            }

            foreach (XElement element in document.Descendants())
            {
                string where = $"{file}:{((IXmlLineInfo)element).LineNumber}";
                string name = element.Name.LocalName;
                if (name is "Binding" or "MultiBinding" or "PriorityBinding"
                    && element.Attributes().Any(attribute => MentionsSink(attribute.Value)))
                {
                    string container = element.Parent?.Name.LocalName ?? string.Empty;
                    failures.Add(container is "MultiBinding" or "PriorityBinding"
                        ? $"{where}: a {container} composes a title sink with other text."
                        : $"{where}: an element-syntax {name} of a title sink; bind it as {{Binding …}} so the "
                            + "census reads all of it.");
                    continue;
                }

                bool view = name == nameof(EditorEmbedPreviewView);
                bool rooted = false;
                foreach (XAttribute attribute in element.Attributes())
                {
                    string property = attribute.Name.LocalName;
                    bool root = view && property == nameof(EditorEmbedPreviewView.Root);
                    if (!root && !MentionsSink(attribute.Value))
                    {
                        continue;
                    }
                    rooted |= root;
                    sinks.Add(new XamlSink(where, file, element.Name.NamespaceName, name, property, attribute.Value));
                    failures.AddRange(BindingFailures(where, element, property, attribute.Value));
                }
                if (view && !rooted)
                {
                    failures.Add($"{where}: an {nameof(EditorEmbedPreviewView)} without a Root binding the census "
                        + "can read.");
                }

                // The renderer's cards are Expanders built in C# and their
                // headers are strings; a style or template for Expander, or
                // a template for every string, could wrap each card title in
                // text. The type is read through the element's namespace map
                // (codex round 3: `{x:Type wpf:Expander}` under an alias).
                if (name is "Style" or "ControlTemplate"
                    && (IsType(element, "TargetType", Expander) || IsType(element, "Key", Expander)))
                {
                    foreach (XElement setter in element.Descendants().Where(child => child.Name.LocalName == "Setter"
                        && (string?)child.Attribute("Property") is { } set
                        && WrapsHeader(set)))
                    {
                        failures.Add($"{file}:{((IXmlLineInfo)setter).LineNumber}: an Expander style sets "
                            + $"{(string?)setter.Attribute("Property")}, which can wrap a card's title in text.");
                    }
                    if (name == "ControlTemplate")
                    {
                        failures.Add($"{where}: an Expander template, which can wrap a card's title in text.");
                    }
                }
                if (name is "DataTemplate" && (IsType(element, "DataType", SystemString) || IsType(element, "Key", SystemString)))
                {
                    failures.Add($"{where}: a DataTemplate for every string, which templates a card's header "
                        + "and can wrap its title in text.");
                }
            }
        }
        return (sinks, failures);
    }

    private static IEnumerable<string> BindingFailures(string where, XElement element, string property, string value)
    {
        string site = $"{where}: {element.Name.LocalName}.{property} = \"{value}\"";
        Markup? markup = Markup.Parse(value);
        if (markup is null)
        {
            yield return $"{site} — static text, not a binding of the title.";
            yield break;
        }
        if (markup.Name != "Binding")
        {
            yield return $"{site} — {markup.Name} is not a plain Binding.";
            yield break;
        }
        if (markup.Positional.Count > 1)
        {
            yield return $"{site} — a binding the census cannot read.";
        }
        foreach ((string key, string _) in markup.Named.Where(named => !PlainBindingKeys.Contains(named.Key)))
        {
            string cause = key switch
            {
                "StringFormat" => "a StringFormat adds host text around the title",
                "Converter" => "a Converter can add text the census cannot read",
                "ConverterParameter" or "ConverterCulture" => $"a {key} feeds a converter",
                "FallbackValue" or "TargetNullValue" => $"a {key} substitutes static text for the title",
                _ => $"{key} changes what is bound",
            };
            yield return $"{site} — {cause}; a title sink binds its property plainly.";
        }

        if (element.Attribute("AutomationProperties.LabeledBy") is not null)
        {
            yield return $"{site} — AutomationProperties.LabeledBy names the element from other text.";
        }
        foreach (XAttribute format in element.Attributes()
            .Where(attribute => attribute.Name.LocalName.EndsWith("StringFormat", StringComparison.Ordinal)))
        {
            yield return $"{site} — {format.Name.LocalName} adds host text around the title.";
        }
        if (property != "AutomationProperties.Name" && element.Attribute("AutomationProperties.Name") is not null)
        {
            yield return $"{site} — a static AutomationProperties.Name replaces the title for assistive technology.";
        }
        if (property == "Text" && element.Nodes().Any(node =>
            node is XElement || (node is XText text && !string.IsNullOrWhiteSpace(text.Value))))
        {
            yield return $"{site} — static sibling text inside the element is read with the title.";
        }
        if (Inlines.Contains(element.Name.LocalName)
            && element.Parent is { } parent
            && parent.Nodes().Any(node => node != element
                && (node is XElement || (node is XText text && !string.IsNullOrWhiteSpace(text.Value)))))
        {
            yield return $"{site} — static sibling text beside the inline is read with the title.";
        }
    }

    private static IEnumerable<string> XamlPopulationFailures(IReadOnlyList<XamlSink> sinks)
    {
        (string Namespace, string Element, string Attribute, string Path)[] expected = ExpectedXamlSinks();
        (XamlSink Sink, string? Path)[] bound = sinks
            .Select(sink => (sink, Markup.Parse(sink.Value) is { Name: "Binding" } markup ? markup.Path : null))
            .ToArray();
        bool Expected(XamlSink sink, string? path, (string Namespace, string Element, string Attribute, string Path) expectation) =>
            expectation.Namespace == sink.Namespace
            && expectation.Element == sink.Element
            && expectation.Attribute == sink.Attribute
            && expectation.Path == path;

        foreach ((string Namespace, string Element, string Attribute, string Path) expectation in expected)
        {
            int count = bound.Count(entry => Expected(entry.Sink, entry.Path, expectation));
            if (count != 1)
            {
                yield return $"XAML: {expectation.Element}.{expectation.Attribute} binds {expectation.Path} {count} "
                    + "times; the census reads exactly one.";
            }
        }
        foreach ((XamlSink sink, string? path) in bound.Where(entry =>
            !expected.Any(expectation => Expected(entry.Sink, entry.Path, expectation))))
        {
            yield return $"{sink.Where}: {sink.Element}.{sink.Attribute} = \"{sink.Value}\" (xmlns {sink.Namespace}) is "
                + $"a title sink the census does not expect (path {path ?? "unread"}); bind the title where the "
                + "census reads it.";
        }
    }

    private static bool MentionsSink(string value) =>
        XamlSinkProperties.Any(property => value.Contains(property, StringComparison.Ordinal));

    private const string Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    /// <summary>WPF's Expander, under either namespace XAML can name it by.</summary>
    private static readonly (string Namespace, string Name)[] Expander =
    [
        (Presentation, "Expander"),
        ("clr-namespace:System.Windows.Controls;assembly=PresentationFramework", "Expander"),
    ];

    /// <summary>System.String, under the namespaces XAML can name it by.</summary>
    private static readonly (string Namespace, string Name)[] SystemString =
    [
        (Xaml, "String"),
        ("clr-namespace:System;assembly=mscorlib", "String"),
        ("clr-namespace:System;assembly=System.Runtime", "String"),
        ("clr-namespace:System;assembly=netstandard", "String"),
        ("clr-namespace:System;assembly=System.Private.CoreLib", "String"),
    ];

    private static bool WrapsHeader(string property)
    {
        string member = property[(property.LastIndexOf('.') + 1)..];
        return member is "Header" or "Style"
            || member.EndsWith("StringFormat", StringComparison.Ordinal)
            || member.EndsWith("Template", StringComparison.Ordinal)
            || member.EndsWith("TemplateSelector", StringComparison.Ordinal);
    }

    /// <summary>Whether an attribute names one of these types — spelled
    /// `Prefix:Name`, bare, or as `{x:Type …}` (positional or TypeName=) —
    /// with its prefix resolved through the element's namespace map, never
    /// by the prefix's spelling. The x:Key attribute is read in the XAML
    /// namespace.</summary>
    private static bool IsType(XElement element, string attribute, (string Namespace, string Name)[] types)
    {
        XAttribute? found = attribute == "Key"
            ? element.Attribute(XName.Get("Key", Xaml))
            : element.Attribute(attribute);
        if (found is null)
        {
            return false;
        }
        string value = found.Value.Trim();
        if (Markup.Parse(value) is { } markup)
        {
            (string? extensionNamespace, string extension) = Qualified(element, markup.Name);
            if (extension != "Type" || extensionNamespace != Xaml)
            {
                return false;
            }
            value = markup.Named.Where(named => named.Key == "TypeName").Select(named => named.Value)
                .FirstOrDefault() ?? markup.Positional.FirstOrDefault() ?? string.Empty;
        }
        (string? typeNamespace, string typeName) = Qualified(element, value);
        return types.Any(type => type.Namespace == typeNamespace && type.Name == typeName);
    }

    /// <summary>`prefix:Name` resolved to (namespace, Name); a bare name is
    /// in the element's default namespace.</summary>
    private static (string? Namespace, string Name) Qualified(XElement element, string spelled)
    {
        int colon = spelled.IndexOf(':', StringComparison.Ordinal);
        return colon < 0
            ? (element.GetDefaultNamespace().NamespaceName, spelled)
            : (element.GetNamespaceOfPrefix(spelled[..colon])?.NamespaceName, spelled[(colon + 1)..]);
    }

    /// <summary>A XAML markup extension — <c>{Name positional, Key=Value}</c>
    /// — split at its top-level commas; nested braces and quoted text stay
    /// whole.</summary>
    internal sealed record Markup(
        string Name,
        IReadOnlyList<string> Positional,
        IReadOnlyList<(string Key, string Value)> Named)
    {
        internal string? Path =>
            Named.Where(named => named.Key == "Path").Select(named => named.Value).FirstOrDefault()
            ?? Positional.FirstOrDefault();

        internal static Markup? Parse(string value)
        {
            string text = value.Trim();
            if (text.Length < 2 || text[0] != '{' || text[^1] != '}' || text.StartsWith("{}", StringComparison.Ordinal))
            {
                return null;
            }
            string inner = text[1..^1].Trim();
            int nameEnd = 0;
            while (nameEnd < inner.Length && !char.IsWhiteSpace(inner[nameEnd]) && inner[nameEnd] != ',')
            {
                nameEnd++;
            }

            var positional = new List<string>();
            var named = new List<(string Key, string Value)>();
            foreach (string part in TopLevel(inner[nameEnd..], ','))
            {
                string piece = part.Trim();
                if (piece.Length == 0)
                {
                    continue;
                }
                int equals = TopLevel(piece, '=').First().Length;
                if (equals == piece.Length)
                {
                    positional.Add(Unquote(piece));
                }
                else
                {
                    named.Add((piece[..equals].Trim(), Unquote(piece[(equals + 1)..].Trim())));
                }
            }
            return new Markup(inner[..nameEnd], positional, named);
        }

        /// <summary>The text split at each top-level separator: not inside
        /// braces, not inside quotes, not escaped.</summary>
        private static IEnumerable<string> TopLevel(string text, char separator)
        {
            int depth = 0;
            int start = 0;
            char quote = '\0';
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\\')
                {
                    i++;
                    continue;
                }
                if (quote != '\0')
                {
                    if (c == quote)
                    {
                        quote = '\0';
                    }
                    continue;
                }
                switch (c)
                {
                    case '\'' or '"':
                        quote = c;
                        break;
                    case '{':
                        depth++;
                        break;
                    case '}':
                        depth--;
                        break;
                    default:
                        if (c == separator && depth == 0)
                        {
                            yield return text[start..i];
                            start = i + 1;
                        }
                        break;
                }
            }
            yield return text[start..];
        }

        private static string Unquote(string text) =>
            text.Length >= 2 && text[0] is '\'' or '"' && text[^1] == text[0] ? text[1..^1] : text;
    }
}
