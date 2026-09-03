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
        // Review round 1 (IH-61): the positional status joined from its
        // WORDS — string.Join(" ", n, "of", m, "in", c), a Concat, a
        // Format — carries no literal with both " of " and " in ", so the
        // fragment words are offences on any joining line.
        string word = body.Trim();
        if ((word == "of" || word == "in")
            && Regex.IsMatch(line, @"\b(?:Join|Concat|Format|Append|Insert)\s*\(|\+\s*\$?""|""\s*\+"))
        {
            return "the positional status (joined from its words)";
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
    [InlineData("CanvasOutlineView.cs", "ForNode", "CanvasPhrase.CardReference(")]
    [InlineData("CanvasOutlineView.cs", "ForNode", "CanvasPhrase.RowStatus(")]
    [InlineData("CanvasOutlineView.cs", "RefreshStatus", "CanvasPhrase.RowStatus(")]
    [InlineData("CanvasDocumentViewModel.cs", "BuildCardPickerModel", "CanvasPhrase.PickerRowLabel(row.Kind, row.SpeakableName, row.GroupPath)")]
    [InlineData("CanvasPromptViewModel.cs", "Reproject", "CanvasPhrase.CardReference(row.Kind, row.SpeakableName) + CanvasPhrase.MarkedSuffix")]
    [InlineData("CanvasPromptViewModel.cs", "CanvasMoveIntoGroupPrompt", "CanvasPhrase.CardReference(g.Kind, g.SpeakableName)")]
    [InlineData("CanvasRendererView.cs", "AllBounds", "Model?.CurrentBounds()")]
    public void EverySinkCallsItsHelper(string file, string member, string call)
    {
        // Review round 1 (IH-61): the call must sit in the SINK's own
        // member, read without comments — a helper named in a comment or
        // a dead member elsewhere in the file is not a call.
        string span = MemberSpan(File.ReadAllText(Path.Combine(CanvasRoot, file)), member);
        Assert.Contains(call, span);
    }

    /// <summary>The member's source from its declaration line to the next
    /// member-level declaration or the class's closing brace, comments
    /// stripped.</summary>
    private static string MemberSpan(string source, string member)
    {
        string[] lines = source.Split('\n');
        var declaration = new Regex(@"^    (?:internal|private|public|protected)\b[^=(]*?\b" + member + @"\s*\(");
        var spans = new System.Text.StringBuilder();
        bool found = false;
        for (int start = 0; start < lines.Length; start++)
        {
            if (!declaration.IsMatch(lines[start]))
            {
                continue;
            }
            found = true;
            int end = start + 1;
            while (end < lines.Length
                && !Regex.IsMatch(lines[end], @"^    (?:internal|private|public|protected|static|\[|///|\})")
                && !lines[end].StartsWith("}", StringComparison.Ordinal))
            {
                end++;
            }
            spans.Append(string.Join("\n", lines[start..end])).Append('\n');
        }
        Assert.True(found, $"no member {member}");
        return Regex.Replace(spans.ToString(), @"//[^\n]*", "");
    }

    /// <summary>The renderer keeps no union of its own (§W-G row H's
    /// Windows half, reopened and closed in PR H).</summary>
    [Fact]
    public void TheRendererComputesNoBoundsUnion()
    {
        // Review round 1 (IH-62): the OPERATION is forbidden, not two
        // spellings of it — the fit's bounds member is a pure delegation
        // to core through the document, with no fold, no union, no
        // extreme value and no loop of its own; and the renderer joins no
        // rectangles anywhere.
        string source = File.ReadAllText(Path.Combine(CanvasRoot, "CanvasRendererView.cs"));
        string span = MemberSpan(source, "AllBounds");
        Assert.Contains("Model?.CurrentBounds()", span);
        Assert.False(
            Regex.IsMatch(span, @"Math\.Min|Math\.Max|\.Min\(|\.Max\(|Union|MaxValue|MinValue|foreach|\bfor\s*\(|Aggregate"),
            "the fit's bounds member computes something of its own:\n" + span);
        string code = Regex.Replace(source, @"//[^\n]*", "");
        Assert.DoesNotContain("Rect.Union", code);
        Assert.DoesNotContain(".Union(", code);
    }
}
