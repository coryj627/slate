// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.RegularExpressions;

namespace SlateWindows.Tests.Censuses;

/// <summary>
/// W6-1 §H TH-7 (H6, IH-44; §W-G rows P, Q, R): the label class — the
/// card reference, the positional status, the container clause, the
/// marked suffix, the connection status — is composed in ONE place,
/// <c>CanvasPhrase</c>, and every sink calls it. Two directions: no
/// string literal under <c>Canvas/</c> outside <c>CanvasPhrase</c>
/// carries a fragment of the class (whatever syntax joins it — an
/// interpolation, a concatenation, a format call, a builder — the
/// literal is what a second composition needs), and each known sink
/// names the helper it calls.
/// </summary>
[Trait("census", "canvas-label-class")]
public sealed class CanvasLabelClassCensus
{
    private static readonly string CanvasRoot =
        Path.Combine(SourceText.ShellSourceRoot(), "Canvas");

    private static readonly Regex Literal =
        new("\\$?\"(?:[^\"\\\\]|\\\\.)*\"", RegexOptions.Compiled);

    /// <summary>The shapes only <c>CanvasPhrase</c> may spell: a literal
    /// that opens the group reference, one that carries the card
    /// reference's <c> card "</c>, the marked suffix, the positional
    /// status's <c> of … in </c> pair, the connection status's
    /// <c>connection … of</c>, and the container fallback
    /// <c>?? "canvas"</c> on a line.</summary>
    private static string? Offence(string literal, string line)
    {
        string body = literal.TrimStart('$').Trim('"');
        if (body.StartsWith("Group \\\"", StringComparison.Ordinal))
        {
            return "the group reference";
        }
        if (body.Contains(" card \\\"", StringComparison.Ordinal))
        {
            return "the card reference";
        }
        if (body.Contains(", marked", StringComparison.Ordinal))
        {
            return "the marked suffix";
        }
        if (body.Contains(" of ", StringComparison.Ordinal) && body.Contains(" in ", StringComparison.Ordinal))
        {
            return "the positional status";
        }
        if (body.StartsWith("connection ", StringComparison.Ordinal) && body.Contains(" of ", StringComparison.Ordinal))
        {
            return "the connection status";
        }
        if (line.Contains("?? \"canvas\"", StringComparison.Ordinal))
        {
            return "the container fallback";
        }
        return null;
    }

    [Fact]
    public void TheLabelClassHasNoSecondComposition()
    {
        var offenders = new List<string>();
        foreach (string path in Directory.GetFiles(CanvasRoot, "*.cs", SearchOption.AllDirectories))
        {
            string source = File.ReadAllText(path);
            string scanned = source;
            int phrase = source.IndexOf("static class CanvasPhrase", StringComparison.Ordinal);
            if (phrase >= 0)
            {
                // CanvasPhrase is the last class in its file: the composer's
                // own literals are the one place the class is spelled.
                scanned = source[..phrase];
            }
            int line = 0;
            foreach (string raw in scanned.Split('\n'))
            {
                line++;
                string code = raw.TrimStart();
                if (code.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }
                foreach (Match literal in Literal.Matches(raw))
                {
                    if (Offence(literal.Value, raw) is { } what)
                    {
                        offenders.Add($"{Path.GetFileName(path)}:{line} {what}: {literal.Value}");
                    }
                }
            }
        }
        Assert.True(
            offenders.Count == 0,
            "a label-class fragment composed outside CanvasPhrase (§W-G rows P/Q/R):\n"
            + string.Join("\n", offenders));
    }

    /// <summary>Each sink of the class calls the helper that owns its
    /// spelling — the outline row, the card picker's row, the marks
    /// list's row, the move-into-group prompt's row — and the renderer's
    /// fit reads core's bounds through the document, never a union.</summary>
    [Theory]
    [InlineData("CanvasOutlineView.cs", "CanvasPhrase.CardReference(")]
    [InlineData("CanvasOutlineView.cs", "CanvasPhrase.RowStatus(")]
    [InlineData("CanvasDocumentViewModel.cs", "CanvasPhrase.PickerRowLabel(row.Kind, row.SpeakableName, row.GroupPath)")]
    [InlineData("CanvasPromptViewModel.cs", "CanvasPhrase.CardReference(row.Kind, row.SpeakableName) + CanvasPhrase.MarkedSuffix")]
    [InlineData("CanvasPromptViewModel.cs", "CanvasPhrase.CardReference(g.Kind, g.SpeakableName)")]
    [InlineData("CanvasRendererView.cs", "Model?.CurrentBounds()")]
    public void EverySinkCallsItsHelper(string file, string call)
    {
        string source = File.ReadAllText(Path.Combine(CanvasRoot, file));
        Assert.Contains(call, source);
    }

    /// <summary>The renderer keeps no union of its own (§W-G row H's
    /// Windows half, reopened and closed in PR H).</summary>
    [Fact]
    public void TheRendererComputesNoBoundsUnion()
    {
        string source = File.ReadAllText(Path.Combine(CanvasRoot, "CanvasRendererView.cs"));
        Assert.DoesNotContain("Math.Min(left", source);
        Assert.DoesNotContain("double.MaxValue, top", source);
    }
}
