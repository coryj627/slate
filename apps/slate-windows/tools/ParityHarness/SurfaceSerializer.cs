// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// §W-A harness skeleton — surface serialization (w0_spec §W0-3 item 5).
// Serializes the skeleton's read-side surfaces (editor spans, headings,
// reading blocks, search, links) into canonical artifacts. The Swift twin
// (ParityHarnessTests.swift) mirrors every rule here; the committed
// goldens under crates/slate-core/tests/fixtures/parity_golden/ arbitrate
// byte-identity. W8-4 grows this to the full read-side surface and the
// two-platform CI pipeline.
//
// Path rule: artifact contents always use forward-slash relative paths
// (the one normalization the skeleton owns; list lives here per §W-A).

using uniffi.slate_uniffi;

namespace ParityHarness;

public static class SurfaceSerializer
{
    public static readonly string[] PinnedSearchQueries = { "fixture", "heading", "parity" };

    /// <summary>
    /// Per-file artifact: spans + headings + reading blocks + inline runs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>inline_runs</c> array (#967) is 1:1 with <c>blocks</c> and
    /// closes the §W-A gap that block-only serialization left: wikilink /
    /// embed / tag / citation payloads, per-grammar resolution, and
    /// accessible text are now byte-checked across platforms.
    /// </para>
    /// <para>
    /// <b>Citation join, now real (W4-5 #737).</b> The corpus ships
    /// <c>library.bib</c> + <c>ieee.csl</c> + a <c>slate.json</c> naming
    /// both, and the harness seeds the bibliography before serializing, so
    /// citation runs carry genuinely RENDERED display and speech text
    /// instead of the raw fallback. This closes the gap the W0 skeleton
    /// recorded as a W8-4 candidate. A render failure is fatal to the
    /// harness rather than falling back — a silent fallback would make the
    /// golden meaningless.
    /// </para>
    /// </remarks>
    public static string FileArtifact(string relPath, string text, VaultSession session)
    {
        var j = new CanonicalJson();
        j.Raw("{\"file\":").Str(relPath);

        j.Raw(",\"spans\":[");
        AppendSpans(j, SlateUniffiMethods.EditorHighlightSpans(text));
        j.Raw("]");

        j.Raw(",\"span_windows\":");
        AppendSpanWindows(j, text);

        j.Raw(",\"headings\":[");
        var headings = SlateUniffiMethods.ExtractHeadings(text);
        for (int i = 0; i < headings.Length; i++)
        {
            var h = headings[i];
            if (i > 0)
            {
                j.Raw(",");
            }
            j.Raw("{\"level\":").Num((ulong)h.Level)
             .Raw(",\"text\":").Str(h.Text)
             .Raw(",\"ordinal\":").Num((ulong)h.Ordinal)
             .Raw(",\"anchor\":").Str(h.AnchorId)
             .Raw(",\"offset\":").Num((ulong)h.ByteOffset)
             .Raw("}");
        }
        j.Raw("]");

        j.Raw(",\"blocks\":[");
        var blocks = SlateUniffiMethods.ReadingBlocksSource(text);
        for (int i = 0; i < blocks.Length; i++)
        {
            var b = blocks[i];
            if (i > 0)
            {
                j.Raw(",");
            }
            j.Raw("{\"kind\":").Str(BlockKindName(b.Kind))
             .Raw(",\"start\":").Num(b.ByteStart)
             .Raw(",\"end\":").Num(b.ByteEnd)
             .Raw(",\"source\":").Str(b.Source)
             .Raw("}");
        }
        j.Raw("]");

        // W4-5: the rendered citations for THIS file, reused by both the
        // inline-run join below and the `citations` section further down.
        var references = session.ListCitationsInFile(relPath);
        string styleId = CitationStyleId(session);
        var rendered = new RenderedCitation[references.Length];
        for (int i = 0; i < references.Length; i++)
        {
            rendered[i] = session.RenderCitation(references[i], styleId);
        }

        j.Raw(",\"inline_runs\":[");
        var inlines = SlateUniffiMethods.ReadingInlineSegmentsSource(
            text, rendered, session.OutgoingLinks(relPath));
        for (int i = 0; i < inlines.Length; i++)
        {
            if (i > 0)
            {
                j.Raw(",");
            }
            AppendBlockInlines(j, inlines[i]);
        }
        j.Raw("]");

        // W3-4: the READING-side canonical artifact. The span rows above
        // byte-check the editor highlight surface; these rows byte-check
        // {source, syntax_tokens, semantic_spans} + the core preamble —
        // exactly what the WPF reading renderer consumes. Token offsets
        // are block-local.
        j.Raw(",\"code_blocks\":[");
        var codeBlocks = session.GetSyntaxTokens(relPath);
        for (int i = 0; i < codeBlocks.Length; i++)
        {
            var c = codeBlocks[i];
            if (i > 0)
            {
                j.Raw(",");
            }
            j.Raw("{\"line\":").Num(c.Line)
             .Raw(",\"offset\":").Num(c.ByteOffset)
             .Raw(",\"language\":");
            if (c.Language == null)
            {
                j.Raw("null");
            }
            else
            {
                j.Str(c.Language);
            }
            j.Raw(",\"preamble\":").Str(
                SlateUniffiMethods.CodeBlockPreamble(c.Language, c.Source))
             .Raw(",\"source\":").Str(c.Source)
             .Raw(",\"tokens\":[");
            for (int t = 0; t < c.Tokens.Length; t++)
            {
                var token = c.Tokens[t];
                if (t > 0)
                {
                    j.Raw(",");
                }
                j.Raw("{\"start\":").Num(token.StartByte)
                 .Raw(",\"end\":").Num(token.EndByte)
                 .Raw(",\"kind\":").Str(TokenKindName(token.Kind))
                 .Raw("}");
            }
            j.Raw("],\"semantic_spans\":[");
            for (int t = 0; t < c.SemanticSpans.Length; t++)
            {
                var span = c.SemanticSpans[t];
                if (t > 0)
                {
                    j.Raw(",");
                }
                j.Raw("{\"start\":").Num(span.StartByte)
                 .Raw(",\"end\":").Num(span.EndByte)
                 .Raw(",\"kind\":").Str(SemanticKindName(span.Kind))
                 .Raw(",\"name\":").Str(span.Name)
                 .Raw("}");
            }
            j.Raw("]}");
        }
        j.Raw("]");

        // W3-2: the canonical math artifact — byte-checks MathCAT's
        // speech and braille output cross-platform for the first time
        // (both twins call the same core, same default prefs). Braille
        // is hex so byte identity is exact and encoding-agnostic.
        j.Raw(",\"math_blocks\":[");
        var mathBlocks = session.GetMathBlocks(relPath);
        for (int i = 0; i < mathBlocks.Length; i++)
        {
            var m = mathBlocks[i];
            if (i > 0)
            {
                j.Raw(",");
            }
            j.Raw("{\"line\":").Num(m.Line)
             .Raw(",\"offset\":").Num(m.ByteOffset)
             .Raw(",\"display\":").Str(
                m.DisplayStyle == MathDisplayStyle.Block ? "block" : "inline")
             .Raw(",\"source\":").Str(m.Source)
             .Raw(",\"mathml\":").Str(m.Mathml)
             .Raw(",\"speech\":").Str(m.Speech)
             .Raw(",\"braille_hex\":").Str(Convert.ToHexString(m.Braille))
             .Raw("}");
        }
        j.Raw("]");

        // W3-3: the canonical diagram artifact — byte-checks the
        // structured description and render status cross-platform.
        // SVG BYTES are deliberately excluded: the renderer's float
        // text-measurement varies by machine (measured: same-length,
        // different-digit SVGs between two Windows hosts), so byte
        // identity would pin an environment, not the contract. The
        // AT-facing contract IS description/status/source/position;
        // presence pins that rendering succeeded.
        j.Raw(",\"diagram_blocks\":[");
        var diagramBlocks = session.GetDiagramBlocks(relPath);
        for (int i = 0; i < diagramBlocks.Length; i++)
        {
            var d = diagramBlocks[i];
            if (i > 0)
            {
                j.Raw(",");
            }
            string status = d.RenderStatus switch
            {
                DiagramRenderStatus.Ok => "ok",
                DiagramRenderStatus.UnsupportedDialect u =>
                    "unsupported:" + u.Reason,
                DiagramRenderStatus.RenderFailed f => "failed:" + f.Message,
                _ => throw new InvalidOperationException(
                    $"unmapped DiagramRenderStatus {d.RenderStatus}"),
            };
            j.Raw("{\"line\":").Num(d.Line)
             .Raw(",\"offset\":").Num(d.ByteOffset)
             .Raw(",\"dialect\":").Str(
                d.Dialect == DiagramDialect.Mermaid ? "mermaid"
                    : throw new InvalidOperationException(
                        $"unmapped DiagramDialect {d.Dialect}"))
             .Raw(",\"source\":").Str(d.Source)
             .Raw(",\"description\":").Str(d.StructuredDescription)
             .Raw(",\"status\":").Str(status)
             .Raw(",\"svg_present\":").Raw(
                d.Svg is { Length: > 0 } ? "true" : "false")
             .Raw("}");
        }
        j.Raw("]");

        // W3-5: the canonical embed-resolution artifact — the first
        // cross-platform byte-check of resolve_embed_preview. One row
        // per DISTINCT BlockEmbedKey in document order (the exact set
        // the reading view resolves into cards), alt deliberately
        // null: the section pins CORE resolution, not the host's
        // record-threaded alt. Image bytes ride as length only (the
        // bytes are fixture content, deterministic by construction).
        j.Raw(",\"embed_resolutions\":[");
        var embedKeys = new List<string>();
        var seenEmbedKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var blockInlines in inlines)
        {
            if (blockInlines.BlockEmbedKey is { Length: > 0 } key
                && seenEmbedKeys.Add(key))
            {
                embedKeys.Add(key);
            }
        }
        for (int i = 0; i < embedKeys.Count; i++)
        {
            if (i > 0)
            {
                j.Raw(",");
            }
            var preview = session.ResolveEmbedPreview(relPath, embedKeys[i], null);
            j.Raw("{\"key\":").Str(embedKeys[i])
             .Raw(",\"truncated\":").Raw(preview.Truncated ? "true" : "false")
             .Raw(",\"resolution\":");
            AppendEmbedResolution(j, preview.Resolution);
            j.Raw("}");
        }
        j.Raw("]");

        // W4-3: the canonical task-row artifact — byte-checks the
        // exact TaskItem surface both task panels consume: text,
        // the raw status char, the completed derivation, the
        // Tasks-plugin metadata axes, and the parser-owned checkbox
        // action range (migration 034's contract).
        j.Raw(",\"tasks\":[");
        var tasks = session.TasksForFile(relPath);
        for (int i = 0; i < tasks.Length; i++)
        {
            var t = tasks[i];
            if (i > 0)
            {
                j.Raw(",");
            }
            AppendTaskItem(j, t);
        }
        j.Raw("]");

        // W4-4: the canonical property-row artifact — byte-checks
        // the exact Property surface the in-note header consumes:
        // key, the inferred kind, and the stored value's JSON
        // encoding EXACTLY as the FFI hands it (value_json is never
        // re-parsed or re-serialized here — float formatting and the
        // tagged list-element objects must round-trip untouched).
        // Source is GetFileMetadata so the SQLite round-trip is
        // pinned, not just the parser.
        j.Raw(",\"properties\":[");
        var properties = session.GetFileMetadata(relPath)?.Properties ?? [];
        for (int i = 0; i < properties.Length; i++)
        {
            var p = properties[i];
            if (i > 0)
            {
                j.Raw(",");
            }
            AppendProperty(j, p);
        }
        j.Raw("]");

        // W4-5 (#736 shape, appended after "properties" so existing key
        // order never moves): the per-file citation surface — parsed
        // reference, its cited items, and the RENDERED output both
        // platforms must agree on byte for byte.
        j.Raw(",\"citations\":[");
        for (int i = 0; i < references.Length; i++)
        {
            if (i > 0)
            {
                j.Raw(",");
            }
            AppendCitation(j, references[i], rendered[i]);
        }
        j.Raw("]}");
        return j + "\n";
    }

    private static void AppendProperty(CanonicalJson j, Property p)
    {
        j.Raw("{\"key\":").Str(p.Key)
         .Raw(",\"kind\":").Str(p.Kind)
         .Raw(",\"value_json\":").Str(p.ValueJson)
         .Raw("}");
    }

    /// <summary>The style id the harness renders with: the configured
    /// default style's file stem (core matches ids, not paths). An
    /// unconfigured style yields an empty id, which renders nothing —
    /// the corpus configures one, so that path is not exercised.</summary>
    private static string CitationStyleId(VaultSession session)
    {
        string? defaultStyle = session.CitationsPrefs().DefaultStyle;
        return string.IsNullOrEmpty(defaultStyle)
            ? string.Empty
            : System.IO.Path.GetFileNameWithoutExtension(defaultStyle);
    }

    private static void AppendCitation(
        CanonicalJson j, CitationReference reference, RenderedCitation rendered)
    {
        j.Raw("{\"raw\":").Str(reference.Raw)
         .Raw(",\"line\":").Num((ulong)reference.Line)
         .Raw(",\"offset\":").Num((ulong)reference.ByteOffset)
         .Raw(",\"items\":[");
        for (int i = 0; i < reference.Citations.Length; i++)
        {
            var item = reference.Citations[i];
            if (i > 0)
            {
                j.Raw(",");
            }
            j.Raw("{\"key\":").Str(item.Key)
             .Raw(",\"mode\":").Str(CitationModeToken(item.Mode))
             .Raw(",\"locator\":");
            if (item.Locator is { } locator)
            {
                j.Raw("{\"label\":").Str(locator.Label)
                 .Raw(",\"value\":").Str(locator.Value)
                 .Raw("}");
            }
            else
            {
                j.Null();
            }
            j.Raw(",\"prefix\":");
            AppendOptionalString(j, item.Prefix);
            j.Raw(",\"suffix\":");
            AppendOptionalString(j, item.Suffix);
            j.Raw("}");
        }
        j.Raw("]")
         .Raw(",\"rendered\":{\"visual\":").Str(rendered.VisualText)
         .Raw(",\"speech\":").Str(rendered.SpeechText)
         .Raw(",\"style_id\":").Str(rendered.StyleId)
         .Raw(",\"bib_key\":");
        AppendOptionalString(j, rendered.BibEntry?.Key);
        j.Raw("}}");
    }

    private static void AppendOptionalString(CanonicalJson j, string? value)
    {
        if (value is null)
        {
            j.Null();
        }
        else
        {
            j.Str(value);
        }
    }

    /// <summary>Stable wire tokens for the citation mode — the enum's
    /// C# spelling is a binding detail, the token is the contract.</summary>
    private static string CitationModeToken(CitationMode mode) => mode switch
    {
        CitationMode.Bracketed => "bracketed",
        CitationMode.InText => "in_text",
        CitationMode.SuppressAuthor => "suppress_author",
        _ => mode.ToString().ToLowerInvariant(),
    };

    /// <summary>W4-4: the vault-wide property-key artifact — pins
    /// list_property_keys' ordering contract (key-sorted, unpaged)
    /// plus the per-key file counts and kind sets the add-property
    /// and bulk-rename surfaces consume.</summary>
    public static string PropertiesArtifact(VaultSession session)
    {
        var j = new CanonicalJson();
        var keys = session.ListPropertyKeys();
        j.Raw("{\"keys\":[");
        for (int i = 0; i < keys.Length; i++)
        {
            var k = keys[i];
            if (i > 0)
            {
                j.Raw(",");
            }
            j.Raw("{\"key\":").Str(k.Key)
             .Raw(",\"file_count\":").Num(k.FileCount)
             .Raw(",\"kinds\":[");
            for (int v = 0; v < k.ValueKinds.Length; v++)
            {
                if (v > 0)
                {
                    j.Raw(",");
                }
                j.Str(k.ValueKinds[v]);
            }
            j.Raw("]}");
        }
        j.Raw("]}");
        return j + "\n";
    }

    /// <summary>W4-5 (#737): the vault-wide citation artifact — the
    /// configured sources and style set, the load warnings core reported
    /// when the bibliography was seeded, the resolved entries, and the
    /// vault's unresolved citation keys.
    ///
    /// <para><c>raw_csl_json</c> is deliberately EXCLUDED: it is a serde
    /// re-serialization of a foreign document, so its field ordering is a
    /// serializer contract rather than a citation contract.
    /// <c>abstract_text</c> rides as <c>abstract_present</c> only — the DB
    /// read path hardcodes it absent while the in-memory path populates it,
    /// which is a cache-state property, not a platform property.</para>
    ///
    /// <para>Entry and unresolved ORDER is core's, emitted as given: the
    /// artifact pins whatever core guarantees rather than imposing a host
    /// sort that would hide an ordering regression.</para></summary>
    public static string BibliographyArtifact(
        VaultSession session, BibLoadWarning[] loadWarnings)
    {
        var j = new CanonicalJson();
        var prefs = session.CitationsPrefs();

        j.Raw("{\"prefs\":{\"sources\":[");
        for (int i = 0; i < prefs.Sources.Length; i++)
        {
            var source = prefs.Sources[i];
            if (i > 0)
            {
                j.Raw(",");
            }
            j.Raw("{\"path\":").Str(Slash(source.Path))
             .Raw(",\"format\":").Str(BibFormatToken(source.Format))
             .Raw(",\"watch\":").Bool(source.Watch)
             .Raw("}");
        }
        j.Raw("],\"default_style\":");
        AppendOptionalString(j, prefs.DefaultStyle);
        j.Raw(",\"additional_styles\":[");
        for (int i = 0; i < prefs.AdditionalStyles.Length; i++)
        {
            if (i > 0)
            {
                j.Raw(",");
            }
            j.Str(prefs.AdditionalStyles[i]);
        }
        j.Raw("]}");

        j.Raw(",\"load_warnings\":[");
        for (int i = 0; i < loadWarnings.Length; i++)
        {
            if (i > 0)
            {
                j.Raw(",");
            }
            j.Raw("{\"source\":").Str(Slash(loadWarnings[i].SourcePath))
             .Raw(",\"message\":").Str(loadWarnings[i].Message)
             .Raw("}");
        }
        j.Raw("]");

        // Style PATHS are absolute and machine-dependent, so only the id
        // and title (both read out of the CSL document) are pinned.
        j.Raw(",\"styles\":[");
        var styles = session.ListCslStyles();
        for (int i = 0; i < styles.Length; i++)
        {
            if (i > 0)
            {
                j.Raw(",");
            }
            j.Raw("{\"id\":").Str(styles[i].Id)
             .Raw(",\"title\":").Str(styles[i].Title)
             .Raw("}");
        }
        j.Raw("]");

        j.Raw(",\"entries\":[");
        var entries = session.GetBibliographyEntries();
        for (int i = 0; i < entries.Length; i++)
        {
            var e = entries[i];
            if (i > 0)
            {
                j.Raw(",");
            }
            j.Raw("{\"key\":").Str(e.Key)
             .Raw(",\"item_type\":").Str(e.ItemType)
             .Raw(",\"title\":").Str(e.Title)
             .Raw(",\"authors\":[");
            for (int a = 0; a < e.Authors.Length; a++)
            {
                if (a > 0)
                {
                    j.Raw(",");
                }
                j.Raw("{\"family\":").Str(e.Authors[a].Family)
                 .Raw(",\"given\":");
                AppendOptionalString(j, e.Authors[a].Given);
                j.Raw("}");
            }
            j.Raw("],\"year\":");
            if (e.Year is int year)
            {
                j.Num(year);
            }
            else
            {
                j.Null();
            }
            j.Raw(",\"journal\":");
            AppendOptionalString(j, e.Journal);
            j.Raw(",\"doi\":");
            AppendOptionalString(j, e.Doi);
            j.Raw(",\"url\":");
            AppendOptionalString(j, e.Url);
            j.Raw(",\"publisher\":");
            AppendOptionalString(j, e.Publisher);
            j.Raw(",\"abstract_present\":").Bool(e.AbstractText is not null)
             .Raw("}");
        }
        j.Raw("]");

        j.Raw(",\"unresolved\":[");
        var unresolved = session.ListUnresolvedCitations();
        for (int i = 0; i < unresolved.Length; i++)
        {
            if (i > 0)
            {
                j.Raw(",");
            }
            j.Raw("{\"path\":").Str(Slash(unresolved[i].Path))
             .Raw(",\"key\":").Str(unresolved[i].Key)
             .Raw("}");
        }
        j.Raw("]}");
        return j + "\n";
    }

    private static string BibFormatToken(BibFormat format) => format switch
    {
        BibFormat.BibTeX => "bibtex",
        BibFormat.BibLaTeX => "biblatex",
        BibFormat.CslJson => "csl_json",
        _ => format.ToString().ToLowerInvariant(),
    };

    private static void AppendTaskItem(CanonicalJson j, TaskItem t)
    {
        j.Raw("{\"ordinal\":").Num((ulong)t.Ordinal)
         .Raw(",\"text\":").Str(t.Text)
         .Raw(",\"status\":").Str(t.StatusChar)
         .Raw(",\"completed\":").Bool(t.Completed)
         .Raw(",\"due_ms\":");
        if (t.DueMs is long due)
        {
            j.Num(due);
        }
        else
        {
            j.Null();
        }
        j.Raw(",\"scheduled_ms\":");
        if (t.ScheduledMs is long scheduled)
        {
            j.Num(scheduled);
        }
        else
        {
            j.Null();
        }
        j.Raw(",\"priority\":");
        if (t.Priority is int priority)
        {
            j.Num(priority);
        }
        else
        {
            j.Null();
        }
        j.Raw(",\"recurrence\":");
        if (t.Recurrence is { } recurrence)
        {
            j.Str(recurrence);
        }
        else
        {
            j.Null();
        }
        j.Raw(",\"line\":").Num((ulong)t.Line)
         .Raw(",\"offset\":").Num((ulong)t.ByteOffset)
         .Raw(",\"checkbox_start\":").Num((ulong)t.CheckboxStartByte)
         .Raw(",\"checkbox_end\":").Num((ulong)t.CheckboxEndByte)
         .Raw("}");
    }

    /// <summary>W4-3: the vault-wide task-review artifact — pins
    /// tasks_in_vault's DEFAULT-filter ordering contract (due ASC
    /// NULLS LAST, priority DESC NULLS LAST, path, ordinal) plus the
    /// joined location fields the review rows consume. The default
    /// filter carries no due windows, so the artifact is
    /// wall-clock-free and deterministic.</summary>
    public static string TasksArtifact(VaultSession session)
    {
        var j = new CanonicalJson();
        var page = session.TasksInVault(
            new TaskFilter(null, null, null, null),
            new Paging(null, 1000));
        j.Raw("{\"total\":").Num(page.TotalFiltered)
         .Raw(",\"rows\":[");
        for (int i = 0; i < page.Items.Length; i++)
        {
            var row = page.Items[i];
            if (i > 0)
            {
                j.Raw(",");
            }
            j.Raw("{\"path\":").Str(Slash(row.Path))
             .Raw(",\"file\":").Str(row.FileName)
             .Raw(",\"task\":");
            AppendTaskItem(j, row.Task);
            j.Raw("}");
        }
        j.Raw("]}");
        return j + "\n";
    }

    private static void AppendEmbedResolution(CanonicalJson j, EmbedResolution resolution)
    {
        switch (resolution)
        {
            case EmbedResolution.FullNote fullNote:
                j.Raw("{\"kind\":").Str("note")
                 .Raw(",\"target\":").Str(fullNote.TargetPath)
                 .Raw(",\"text\":").Str(fullNote.Text)
                 .Raw(",\"nested\":[");
                AppendNestedEmbeds(j, fullNote.Nested);
                j.Raw("]}");
                break;
            case EmbedResolution.Section section:
                j.Raw("{\"kind\":").Str("section")
                 .Raw(",\"target\":").Str(section.TargetPath)
                 .Raw(",\"heading\":").Str(section.Heading)
                 .Raw(",\"text\":").Str(section.Text)
                 .Raw(",\"nested\":[");
                AppendNestedEmbeds(j, section.Nested);
                j.Raw("]}");
                break;
            case EmbedResolution.Block block:
                j.Raw("{\"kind\":").Str("block")
                 .Raw(",\"target\":").Str(block.TargetPath)
                 .Raw(",\"block_id\":").Str(block.BlockId)
                 .Raw(",\"text\":").Str(block.Text)
                 .Raw("}");
                break;
            case EmbedResolution.Image image:
                // SHA-256 over the payload: unlike diagram SVG (a
                // RENDERER output, machine-dependent), image bytes are
                // vault CONTENT — identical fixtures must round-trip
                // the FFI byte-identically on both twins (round 1
                // [medium]: length alone couldn't prove that).
                j.Raw("{\"kind\":").Str("image")
                 .Raw(",\"target\":").Str(image.TargetPath)
                 .Raw(",\"mime\":").Str(image.Mime)
                 .Raw(",\"image_len\":").Num((ulong)image.Bytes.Length)
                 .Raw(",\"image_sha256\":").Str(
                    image.Bytes.Length == 0
                        ? ""
                        : Convert.ToHexString(
                            System.Security.Cryptography.SHA256.HashData(image.Bytes)))
                 .Raw(",\"alt\":").Str(image.Alt ?? "")
                 .Raw("}");
                break;
            case EmbedResolution.Unresolved unresolved:
                j.Raw("{\"kind\":").Str("unresolved:" + unresolved.Reason switch
                {
                    EmbedUnresolvedReason.TargetNotFound n => "target_not_found:" + n.Target,
                    EmbedUnresolvedReason.HeadingNotFound h =>
                        "heading_not_found:" + h.TargetPath + "#" + h.Heading,
                    EmbedUnresolvedReason.BlockNotFound b =>
                        "block_not_found:" + b.TargetPath + "^" + b.BlockId,
                    EmbedUnresolvedReason.DepthLimitReached => "depth_limit",
                    EmbedUnresolvedReason.ReadError e => "read_error:" + e.Message,
                    _ => throw new InvalidOperationException(
                        $"unmapped EmbedUnresolvedReason {unresolved.Reason}"),
                }).Raw("}");
                break;
            default:
                throw new InvalidOperationException(
                    $"unmapped EmbedResolution {resolution}");
        }
    }

    private static void AppendNestedEmbeds(CanonicalJson j, NestedEmbed[] nested)
    {
        for (int i = 0; i < nested.Length; i++)
        {
            if (i > 0)
            {
                j.Raw(",");
            }
            j.Raw("{\"raw\":").Str(nested[i].RawTarget)
             .Raw(",\"start\":").Num(nested[i].ByteOffsetInParent)
             .Raw(",\"end\":").Num(nested[i].ByteEndInParent)
             .Raw(",\"resolution\":");
            AppendEmbedResolution(j, nested[i].Resolution);
            j.Raw("}");
        }
    }

    public static string SemanticKindName(SemanticKind kind) => kind switch
    {
        SemanticKind.Function => "function",
        SemanticKind.Type => "type",
        SemanticKind.Variable => "variable",
        _ => throw new InvalidOperationException($"unmapped SemanticKind {kind}"),
    };

    private static void AppendBlockInlines(CanonicalJson j, ReadingBlockInlines inlines)
    {
        j.Raw("{\"embed\":");
        if (inlines.BlockEmbedKey == null)
        {
            j.Null();
        }
        else
        {
            j.Str(inlines.BlockEmbedKey);
        }
        j.Raw(",\"marker\":");
        if (inlines.ListMarker == null)
        {
            j.Null();
        }
        else
        {
            j.Str(inlines.ListMarker);
        }
        j.Raw(",\"segments\":[");
        for (int s = 0; s < inlines.Segments.Length; s++)
        {
            if (s > 0)
            {
                j.Raw(",");
            }
            var segment = inlines.Segments[s];
            j.Raw("{\"content\":").Str(segment.Content).Raw(",\"task\":");
            if (segment.TaskCompleted is bool completed)
            {
                j.Bool(completed);
            }
            else
            {
                j.Null();
            }
            j.Raw(",\"runs\":[");
            for (int r = 0; r < segment.Runs.Length; r++)
            {
                if (r > 0)
                {
                    j.Raw(",");
                }
                AppendInlineRun(j, segment.Runs[r]);
            }
            j.Raw("]}");
        }
        j.Raw("]}");
    }

    private static void AppendInlineRun(CanonicalJson j, ReadingInlineRun run)
    {
        j.Raw("{\"start\":").Num((ulong)run.Start)
         .Raw(",\"end\":").Num((ulong)run.End)
         .Raw(",\"styles\":[");
        for (int i = 0; i < run.Styles.Length; i++)
        {
            if (i > 0)
            {
                j.Raw(",");
            }
            j.Str(StyleName(run.Styles[i]));
        }
        j.Raw("]");
        // Naming convention (both twins): `kind` is the snake_case
        // discriminator with enum-ish scalars colon-joined (mirroring
        // `list_item:{depth}:{ordered}:{task}`); free-text payloads are
        // separate escaped fields, because a URL or target contains `:`
        // and colon-joining would make the value ambiguous.
        switch (run.Kind)
        {
            case ReadingInlineRunKind.Text:
                j.Raw(",\"kind\":").Str("text");
                break;
            case ReadingInlineRunKind.ExternalLink external:
                j.Raw(",\"kind\":").Str("external_link")
                 .Raw(",\"url\":").Str(external.Url);
                break;
            case ReadingInlineRunKind.Wikilink wiki:
                j.Raw(",\"kind\":").Str(
                    $"wikilink:{GrammarName(wiki.Grammar)}:"
                    + (wiki.Resolved ? "resolved" : "unresolved"))
                 .Raw(",\"target\":").Str(wiki.Target)
                 .Raw(",\"base_target\":").Str(wiki.BaseTarget)
                 .Raw(",\"anchor\":");
                if (wiki.Anchor == null)
                {
                    j.Null();
                }
                else
                {
                    j.Str(AnchorName(wiki.Anchor));
                }
                break;
            case ReadingInlineRunKind.Embed embed:
                j.Raw(",\"kind\":").Str("embed").Raw(",\"key\":").Str(embed.Key);
                break;
            case ReadingInlineRunKind.Tag tag:
                j.Raw(",\"kind\":").Str("tag").Raw(",\"name\":").Str(tag.Name);
                break;
            case ReadingInlineRunKind.Citation citation:
                j.Raw(",\"kind\":").Str("citation")
                 .Raw(",\"raw\":").Str(citation.Raw)
                 .Raw(",\"speech\":").Str(citation.Speech);
                break;
            default:
                throw new InvalidOperationException(
                    $"unmapped ReadingInlineRunKind {run.Kind}");
        }
        j.Raw(",\"ax\":");
        if (run.AxText == null)
        {
            j.Null();
        }
        else
        {
            j.Str(run.AxText);
        }
        j.Raw("}");
    }

    public static string StyleName(ReadingInlineStyle style) => style switch
    {
        ReadingInlineStyle.Emphasis => "emphasis",
        ReadingInlineStyle.Strong => "strong",
        ReadingInlineStyle.Strikethrough => "strikethrough",
        ReadingInlineStyle.InlineCode => "inline_code",
        _ => throw new InvalidOperationException($"unmapped ReadingInlineStyle {style}"),
    };

    public static string GrammarName(ReadingWikiGrammar grammar) => grammar switch
    {
        ReadingWikiGrammar.Wikilink => "wikilink",
        ReadingWikiGrammar.MarkdownDestination => "markdown_destination",
        _ => throw new InvalidOperationException($"unmapped ReadingWikiGrammar {grammar}"),
    };

    /// <summary>
    /// `h:&lt;text&gt;` / `b:&lt;text&gt;` — the anchor form both twins emit.
    /// </summary>
    public static string AnchorName(LinkAnchor anchor) =>
        (anchor.Kind == "block" ? "b:" : "h:") + anchor.Text;

    /// <summary>
    /// Editor-scale §W-A artifact. Sources are deterministic ASCII fixtures
    /// generated identically by both harness twins; only canonical window
    /// results are serialized, so the golden remains small at the 8 MiB tier.
    /// </summary>
    public static string EditorScaleArtifact()
    {
        var j = new CanonicalJson();
        j.Raw("{\"sizes\":[");
        int[] sizes = [100 * 1024, 1024 * 1024, 8 * 1024 * 1024];
        for (int index = 0; index < sizes.Length; index++)
        {
            if (index > 0)
            {
                j.Raw(",");
            }

            string text = EditorScaleFixture(sizes[index]);
            j.Raw("{\"bytes\":").Num((ulong)sizes[index])
             .Raw(",\"span_windows\":");
            AppendSpanWindows(j, text);
            j.Raw("}");
        }
        j.Raw("]}");
        return j + "\n";
    }

    /// <summary>Vault-level artifact: pinned full-text-search queries.</summary>
    public static string SearchArtifact(VaultSession session, CancelToken cancel)
    {
        var j = new CanonicalJson();
        j.Raw("{\"queries\":[");
        for (int q = 0; q < PinnedSearchQueries.Length; q++)
        {
            if (q > 0)
            {
                j.Raw(",");
            }
            var rs = session.FullTextSearch(PinnedSearchQueries[q], new SearchScope.Vault(), cancel);
            var rows = rs.Rows
                .OrderBy(r => r.Path, StringComparer.Ordinal)
                .ThenBy(r => r.Snippet, StringComparer.Ordinal)
                .ToArray();
            j.Raw("{\"query\":").Str(PinnedSearchQueries[q]).Raw(",\"rows\":[");
            for (int i = 0; i < rows.Length; i++)
            {
                if (i > 0)
                {
                    j.Raw(",");
                }
                j.Raw("{\"path\":").Str(Slash(rows[i].Path))
                 .Raw(",\"snippet\":").Str(rows[i].Snippet)
                 .Raw(",\"score\":").Num(rows[i].Score)
                 .Raw("}");
            }
            j.Raw("]}");
        }
        j.Raw("]}");
        return j + "\n";
    }

    /// <summary>Vault-level artifact: outgoing links + backlinks per file.</summary>
    public static string LinksArtifact(VaultSession session, IReadOnlyList<string> relPaths)
    {
        var j = new CanonicalJson();
        j.Raw("{\"files\":[");
        for (int f = 0; f < relPaths.Count; f++)
        {
            if (f > 0)
            {
                j.Raw(",");
            }
            string rel = relPaths[f];
            j.Raw("{\"file\":").Str(Slash(rel));

            j.Raw(",\"outgoing\":[");
            var outgoing = session.OutgoingLinks(rel);
            for (int i = 0; i < outgoing.Length; i++)
            {
                var o = outgoing[i];
                if (i > 0)
                {
                    j.Raw(",");
                }
                j.Raw("{\"target\":");
                if (o.TargetPath == null)
                {
                    j.Null();
                }
                else
                {
                    j.Str(Slash(o.TargetPath));
                }
                j.Raw(",\"raw\":").Str(o.TargetRaw)
                 .Raw(",\"kind\":").Str(o.Kind)
                 .Raw(",\"embed\":").Bool(o.IsEmbed)
                 .Raw(",\"external\":").Bool(o.IsExternal)
                 .Raw(",\"unresolved\":").Bool(o.IsUnresolved)
                 .Raw(",\"ordinal\":").Num((ulong)o.Ordinal)
                 .Raw("}");
            }
            j.Raw("]");

            j.Raw(",\"backlinks\":[");
            var backlinks = session.Backlinks(rel, new Paging(null, 500)).Items;
            for (int i = 0; i < backlinks.Length; i++)
            {
                var b = backlinks[i];
                if (i > 0)
                {
                    j.Raw(",");
                }
                j.Raw("{\"source\":").Str(Slash(b.SourcePath))
                 .Raw(",\"snippet\":").Str(b.Snippet)
                 .Raw(",\"ordinal\":").Num((ulong)b.Ordinal)
                 .Raw(",\"kind\":").Str(b.Kind)
                 .Raw(",\"embed\":").Bool(b.IsEmbed)
                 .Raw("}");
            }
            j.Raw("]}");
        }
        j.Raw("]}");
        return j + "\n";
    }

    public static string SpanKindName(EditorSpanKind kind) => kind switch
    {
        EditorSpanKind.Heading h => $"heading:{h.Level}",
        EditorSpanKind.Emphasis => "emphasis",
        EditorSpanKind.Strong => "strong",
        EditorSpanKind.Strikethrough => "strikethrough",
        EditorSpanKind.InlineCode => "inline_code",
        EditorSpanKind.CodeFence => "code_fence",
        EditorSpanKind.Link => "link",
        EditorSpanKind.Image => "image",
        EditorSpanKind.BlockQuote => "block_quote",
        EditorSpanKind.Wikilink => "wikilink",
        EditorSpanKind.Embed => "embed",
        EditorSpanKind.Tag => "tag",
        EditorSpanKind.Citation => "citation",
        EditorSpanKind.Comment => "comment",
        EditorSpanKind.Frontmatter => "frontmatter",
        EditorSpanKind.Code c => $"code:{TokenKindName(c.Token)}",
        _ => throw new InvalidOperationException($"unmapped EditorSpanKind {kind}"),
    };

    public static string TokenKindName(TokenKind token) => token switch
    {
        TokenKind.Keyword => "keyword",
        TokenKind.String => "string",
        TokenKind.Number => "number",
        TokenKind.Comment => "comment",
        TokenKind.Identifier => "identifier",
        TokenKind.Type => "type",
        TokenKind.Function => "function",
        TokenKind.Operator => "operator",
        TokenKind.Punctuation => "punctuation",
        TokenKind.Other o => $"other:{o.Label}",
        _ => throw new InvalidOperationException($"unmapped TokenKind {token}"),
    };

    private static void AppendSpanWindows(CanonicalJson j, string text)
    {
        using var buffer = new DocumentBuffer(text);
        int length = text.Length;
        int[] anchors = [0, length / 2, length];
        j.Raw("[");
        for (int index = 0; index < anchors.Length; index++)
        {
            if (index > 0)
            {
                j.Raw(",");
            }

            int start = Math.Max(0, anchors[index] - 32);
            int end = Math.Min(length, anchors[index] + 32);
            RangedHighlight ranged = buffer.HighlightInRange(
                checked((uint)start),
                checked((uint)end));
            j.Raw("{\"request_start_utf16\":").Num((ulong)start)
             .Raw(",\"request_end_utf16\":").Num((ulong)end)
             .Raw(",\"applied_start\":").Num(ranged.AppliedStart)
             .Raw(",\"applied_end\":").Num(ranged.AppliedEnd)
             .Raw(",\"spans\":[");
            AppendSpans(j, ranged.Spans);
            j.Raw("]}");
        }
        j.Raw("]");
    }

    private static void AppendSpans(CanonicalJson j, IReadOnlyList<EditorSpan> spans)
    {
        for (int i = 0; i < spans.Count; i++)
        {
            if (i > 0)
            {
                j.Raw(",");
            }
            j.Raw("{\"start\":").Num(spans[i].StartByte)
             .Raw(",\"end\":").Num(spans[i].EndByte)
             .Raw(",\"kind\":").Str(SpanKindName(spans[i].Kind))
             .Raw("}");
        }
    }

    private static string EditorScaleFixture(int targetBytes)
    {
        const string block =
            "## Section\n\nProse with [[Wikilink]] and #tag plus `code` and [@citation].\n\n";
        var text = new System.Text.StringBuilder(targetBytes + block.Length);
        while (text.Length < targetBytes)
        {
            text.Append(block);
        }

        return text.ToString(0, targetBytes);
    }

    public static string BlockKindName(ReadingBlockKind kind) => kind switch
    {
        ReadingBlockKind.Heading h => $"heading:{h.Level}",
        ReadingBlockKind.Paragraph => "paragraph",
        ReadingBlockKind.ListItem l =>
            $"list_item:{l.Depth}:{(l.Ordered ? "ordered" : "unordered")}:{l.Task ?? "-"}",
        ReadingBlockKind.BlockQuote q => $"block_quote:{q.Depth}",
        ReadingBlockKind.CodeFence c => $"code_fence:{c.Language}",
        ReadingBlockKind.MathBlock => "math_block",
        ReadingBlockKind.Diagram d => $"diagram:{d.Dialect}",
        ReadingBlockKind.Table => "table",
        ReadingBlockKind.ThematicBreak => "thematic_break",
        ReadingBlockKind.Html => "html",
        _ => throw new InvalidOperationException($"unmapped ReadingBlockKind {kind}"),
    };

    private static string Slash(string path) => path.Replace('\\', '/');
}
