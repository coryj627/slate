// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Documents;
using System.Windows.Media;
using uniffi.slate_uniffi;

namespace ContainerSpike;

/// Core runs → WPF inlines, per `w3_inline_runs_spec.md` §10.2/§10.3.
///
/// Shared by BOTH containers on purpose: `Run` and `Hyperlink` are the
/// same vocabulary in `TextBlock.Inlines` and `Paragraph.Inlines`, so the
/// spike varies only the container and never the inline mapping. If that
/// assumption is wrong it fails loudly here rather than skewing the
/// comparison.
internal static class InlineBuilder
{
    /// §10.2: `Start`/`End` are UTF-8 BYTE offsets into `Content`, and C#
    /// strings are UTF-16. Decode once, slice the byte array — slicing the
    /// string directly silently corrupts any non-ASCII note.
    public static List<Inline> Build(ReadingInlineSegment segment)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(segment.Content);
        var inlines = new List<Inline>();

        foreach (ReadingInlineRun run in segment.Runs)
        {
            int start = (int)run.Start;
            int length = (int)(run.End - run.Start);
            if (start < 0 || length <= 0 || start + length > utf8.Length)
            {
                continue;
            }

            string text = Encoding.UTF8.GetString(utf8, start, length);
            var body = new Run(text);
            ApplyStyles(body, run.Styles);

            Inline inline = run.Kind switch
            {
                ReadingInlineRunKind.Text => body,
                _ => Activatable(body, run.Kind),
            };

            // §10.3: per-run accessible text is core's string, stamped
            // with the host's AX mechanism. Never composed here.
            if (run.AxText is { Length: > 0 } axText)
            {
                AutomationProperties.SetHelpText(inline, axText);
            }

            inlines.Add(inline);
        }

        return inlines;
    }

    private static void ApplyStyles(Run body, ReadingInlineStyle[] styles)
    {
        foreach (ReadingInlineStyle style in styles)
        {
            switch (style)
            {
                case ReadingInlineStyle.Emphasis:
                    body.FontStyle = FontStyles.Italic;
                    break;
                case ReadingInlineStyle.Strong:
                    body.FontWeight = FontWeights.Bold;
                    break;
                case ReadingInlineStyle.Strikethrough:
                    body.TextDecorations = TextDecorations.Strikethrough;
                    break;
                case ReadingInlineStyle.InlineCode:
                    body.FontFamily = new FontFamily("Cascadia Mono, Consolas, monospace");
                    break;
            }
        }
    }

    /// Every activatable run is a `Hyperlink` carrying accent + underline
    /// — the affordance is never colour-only. An unresolved wikilink keeps
    /// the underline and takes the warning treatment, which is the state
    /// the owner call made a first-class token.
    private static Inline Activatable(Run body, ReadingInlineRunKind kind)
    {
        var link = new Hyperlink(body)
        {
            TextDecorations = TextDecorations.Underline,
            Foreground = IsUnresolved(kind) ? Palette.Warning : Palette.Accent,
        };

        // A destination is REQUIRED, not decorative. Without NavigateUri
        // NVDA announces "Link has no apparent destination" on every link
        // — observed in the 2026-07-25 manual pass, where it was the
        // spike's own omission rather than anything about the container.
        //
        // The scheme carries the grammar, exactly as the mac applier's
        // `activationURL(for:)` does, so the routed value keeps its source
        // grammar and `^` cannot mean an anchor in one and a path
        // character in the other.
        Uri? destination = RoutingUri(kind);
        if (destination is not null)
        {
            link.NavigateUri = destination;
        }

        // Activation is a CLICK HANDLER, deliberately not a Command.
        //
        // A `RoutedCommand` with no `CommandBinding` reports CanExecute
        // false, which disables the Hyperlink — and a disabled hyperlink
        // is neither focusable nor, possibly, present in the UIA tree at
        // all. The spike measures exposure, so anything that suppresses
        // exposure for a reason unrelated to the container would corrupt
        // the very comparison it exists to make.
        link.Click += (_, _) => { /* exposure spike: routing is W3-1's */ };
        link.Tag = Describe(kind);
        return link;
    }

    private static bool IsUnresolved(ReadingInlineRunKind kind) =>
        kind is ReadingInlineRunKind.Wikilink { Resolved: false };

    /// The URI a run activates with. Mirrors the mac applier's scheme
    /// table so both hosts route the same value; the spike does not
    /// resolve it, only exposes it.
    private static Uri? RoutingUri(ReadingInlineRunKind kind)
    {
        (string Scheme, string Target)? routed = kind switch
        {
            ReadingInlineRunKind.ExternalLink external => ("", external.Url),
            // `slate-wiki` vs `slate-wikimd` keeps the authored grammar
            // attached to the routed value.
            ReadingInlineRunKind.Wikilink wiki => (
                wiki.Grammar == ReadingWikiGrammar.Wikilink ? "slate-wiki" : "slate-wikimd",
                wiki.Target),
            ReadingInlineRunKind.Embed embed => ("slate-embed", embed.Key),
            ReadingInlineRunKind.Tag tag => ("slate-tag", tag.Name),
            ReadingInlineRunKind.Citation citation => ("slate-cite", citation.Raw),
            _ => null,
        };

        if (routed is not { } value)
        {
            return null;
        }
        if (value.Scheme.Length == 0)
        {
            return Uri.TryCreate(value.Target, UriKind.Absolute, out Uri? external)
                ? external : null;
        }
        return Uri.TryCreate(
            $"{value.Scheme}://{Uri.EscapeDataString(value.Target)}",
            UriKind.Absolute,
            out Uri? routedUri) ? routedUri : null;
    }

    public static string Describe(ReadingInlineRunKind kind) => kind switch
    {
        ReadingInlineRunKind.Text => "text",
        ReadingInlineRunKind.ExternalLink external => $"external:{external.Url}",
        ReadingInlineRunKind.Wikilink wiki =>
            $"wikilink:{wiki.Target}:{(wiki.Resolved ? "resolved" : "unresolved")}",
        ReadingInlineRunKind.Embed embed => $"embed:{embed.Key}",
        ReadingInlineRunKind.Tag tag => $"tag:{tag.Name}",
        ReadingInlineRunKind.Citation citation => $"citation:{citation.Raw}",
        _ => "unknown",
    };
}

/// Stand-in tokens. The real W3-1 pulls these from `ThemeManager`; the
/// spike only needs two distinguishable brushes so a probe can see that
/// resolved and unresolved differ.
internal static class Palette
{
    public static readonly Brush Accent = new SolidColorBrush(Color.FromRgb(0x0A, 0x4A, 0x8F));
    public static readonly Brush Warning = new SolidColorBrush(Color.FromRgb(0x8A, 0x4B, 0x00));
    public static readonly Brush Surface = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
    public static readonly Brush Text = new SolidColorBrush(Color.FromRgb(0x11, 0x11, 0x11));
}
