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
    /// <remarks>W5-2 (#742, contract S13): <c>summary</c> pins core's
    /// <c>summary_for</c> render — the string the overlay displays
    /// verbatim and the announcement template renders — byte-identical
    /// across both twins. Same key, same position in the Swift twin
    /// (<c>ParityHarnessTests.swift</c>), same commit.</remarks>
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
            j.Raw("{\"query\":").Str(PinnedSearchQueries[q])
             .Raw(",\"summary\":").Str(rs.Summary)
             .Raw(",\"rows\":[");
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

    // --- W6-2 PR 0b: the graph_queries section (contract 0b-13) -----------
    //
    // The pinned input lists below are DUPLICATED in the mac twin
    // (apps/slate-mac/Tests/SlateMacTests/ParityHarnessTests.swift,
    // `graphQueriesArtifact`), the same way the canvas lists are: the twins
    // cannot share a source file, so each carries its own copy and the
    // committed golden arbitrates. Every id in the artifact is a
    // `stable_key`, never a numeric node id (0bR-2; the census's no-id
    // guard proves it).

    /// Copy a fixture tree, folders included (the graph vault's nested
    /// note is a witness).
    public static void CopyTree(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (string dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(target, Path.GetRelativePath(source, dir)));
        }
        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            File.Copy(file, Path.Combine(target, Path.GetRelativePath(source, file)));
        }
    }

    /// UTF-8 byte order — the artifact's list order (0b-13), the same
    /// order the Swift twin's `utf8` comparison yields.
    public static int CompareUtf8(string a, string b)
    {
        byte[] x = System.Text.Encoding.UTF8.GetBytes(a);
        byte[] y = System.Text.Encoding.UTF8.GetBytes(b);
        int n = Math.Min(x.Length, y.Length);
        for (int i = 0; i < n; i++)
        {
            if (x[i] != y[i])
            {
                return x[i] - y[i];
            }
        }
        return x.Length - y.Length;
    }

    public static readonly GraphFilter GraphInclusiveFilter = new GraphFilter(true, true, false);

    /// The visibility queries the artifact pins: the inclusive filter under
    /// ten needles, the default filter, the Unresolved preset's kind
    /// overlay, and orphans-only.
    public static readonly (string Name, GraphVisibilityQuery Query)[] PinnedGraphQueries =
    {
        ("all", new GraphVisibilityQuery(GraphInclusiveFilter, "", null)),
        ("all:hub", new GraphVisibilityQuery(GraphInclusiveFilter, "hub", null)),
        ("all:HUB", new GraphVisibilityQuery(GraphInclusiveFilter, "HUB", null)),
        ("all:café", new GraphVisibilityQuery(GraphInclusiveFilter, "café", null)),
        ("all:cafe", new GraphVisibilityQuery(GraphInclusiveFilter, "cafe", null)),
        ("all:ghost", new GraphVisibilityQuery(GraphInclusiveFilter, "ghost", null)),
        ("all:istanbul", new GraphVisibilityQuery(GraphInclusiveFilter, "istanbul", null)),
        ("all:padded-2", new GraphVisibilityQuery(GraphInclusiveFilter, "  2  ", null)),
        ("all:newline-10", new GraphVisibilityQuery(GraphInclusiveFilter, "\n10\n", null)),
        ("all:zzz", new GraphVisibilityQuery(GraphInclusiveFilter, "zzz", null)),
        ("default", new GraphVisibilityQuery(new GraphFilter(false, true, false), "", null)),
        ("all:ghosts-only", new GraphVisibilityQuery(GraphInclusiveFilter, "", GraphNodeKind.Ghost)),
        ("orphans", new GraphVisibilityQuery(new GraphFilter(true, true, true), "", null)),
    };

    /// The sixteen table sorts: each non-Modified column both ways.
    /// Modified is deliberately absent: its order is the checkout time.
    public static readonly (GraphTableColumn Column, bool Ascending, string Name)[] PinnedGraphTableSorts =
    {
        (GraphTableColumn.Note, true, "note asc"), (GraphTableColumn.Note, false, "note desc"),
        (GraphTableColumn.LinksIn, true, "links_in asc"), (GraphTableColumn.LinksIn, false, "links_in desc"),
        (GraphTableColumn.LinksOut, true, "links_out asc"), (GraphTableColumn.LinksOut, false, "links_out desc"),
        (GraphTableColumn.EmbedsIn, true, "embeds_in asc"), (GraphTableColumn.EmbedsIn, false, "embeds_in desc"),
        (GraphTableColumn.EmbedsOut, true, "embeds_out asc"), (GraphTableColumn.EmbedsOut, false, "embeds_out desc"),
        (GraphTableColumn.Component, true, "component asc"), (GraphTableColumn.Component, false, "component desc"),
        (GraphTableColumn.Folder, true, "folder asc"), (GraphTableColumn.Folder, false, "folder desc"),
        (GraphTableColumn.Kind, true, "kind asc"), (GraphTableColumn.Kind, false, "kind desc"),
    };

    /// The (centre path, depth) pairs the connections section pins.
    public static readonly (string Path, uint Depth)[] PinnedGraphConnections =
    {
        ("hub.md", 1),
        ("hub.md", 2),
        ("hub.md", 3),
        ("notes/nested/deep.md", 2),
        ("10.md", 1),
        ("self.md", 1),
    };

    /// The ghost targets the ghost_paths section pins (0b-11's cases).
    public static readonly string[] PinnedGraphGhostTargets =
    {
        "Ghost One",
        "./Ghost One",
        "/ghost two",
        "notes/Foo.MD",
        "dir/",
        ".md",
        "a\\b",
        "café",
    };

    /// The spatial point table, labelled a–d (ids are local to the table
    /// and never written): a at the origin, b right, c below, d diagonal.
    public static readonly (string Label, GraphPoint Point)[] PinnedGraphSpatialPoints =
    {
        ("a", new GraphPoint(1, 0.0, 0.0)),
        ("b", new GraphPoint(2, 10.0, 0.0)),
        ("c", new GraphPoint(3, 0.0, 10.0)),
        ("d", new GraphPoint(4, 10.0, 10.0)),
    };

    /// a's neighbours: d alone, so the neighbour-first rule is visible.
    public static readonly ulong[] PinnedGraphSpatialNeighbors = { 4 };

    /// The four unit axes, from a.
    public static readonly (long Dx, long Dy)[] PinnedGraphSpatialDirections = { (1, 0), (0, 1), (-1, 0), (0, -1) };

    /// The structural order, by label.
    public static readonly string[] PinnedGraphStructuralOrder = { "a", "b", "c", "d" };

    /// The config text the config section decodes and re-encodes: a
    /// nested unknown key, a mixed groups array, out-of-range values, a
    /// fractional force, no verbosity.
    public const string PinnedGraphConfigInput =
        "{\"version\":1,\"futureThing\":{\"keep\":true,\"nested\":{\"z\":1,\"a\":[1,2]}},"
        + "\"filters\":{\"includeAttachments\":true,\"nameQuery\":\"café\"},"
        + "\"groups\":[{\"query\":\"project\",\"colorToken\":\"green\",\"ringStyle\":\"dashed\"},7,{\"colorToken\":\"blue\"},"
        + "{\"query\":\"x\",\"colorToken\":\"mauve\",\"ringStyle\":\"wavy\"}],"
        + "\"display\":{\"nodeSizeMultiplier\":100,\"textFadeZoom\":null},"
        + "\"forces\":{\"repel\":9.0,\"center\":-3,\"link\":0.25},"
        + "\"mode\":\"diagram\",\"connectionsDepth\":99}";

    public static string GraphKindName(GraphNodeKind kind) => kind switch
    {
        GraphNodeKind.Note => "note",
        GraphNodeKind.Attachment => "attachment",
        GraphNodeKind.Ghost => "ghost",
        _ => throw new InvalidOperationException($"unmapped GraphNodeKind {kind}"),
    };

    private static void GraphKeyList(CanonicalJson j, IEnumerable<string> keys)
    {
        j.Raw("[");
        int k = 0;
        foreach (string key in keys)
        {
            if (k++ > 0)
            {
                j.Raw(",");
            }
            j.Str(key);
        }
        j.Raw("]");
    }

    private static void GraphOptionalString(CanonicalJson j, string? value)
    {
        if (value == null)
        {
            j.Null();
        }
        else
        {
            j.Str(value);
        }
    }

    /// <summary>
    /// Vault-level artifact: the W6-2 PR 0b structural queries over the
    /// graph vault — the snapshot keyed by stable key in byte order, the
    /// visible sets under every pinned query, the table under every pinned
    /// sort (the nine cells minus Modified, named), the Connections trees
    /// with key-built occurrence ids, each node's visible neighbours, the
    /// ghost paths, the spatial and structural steps by label, the
    /// constants, the action set per kind, and the config round-trip.
    /// </summary>
    public static string GraphQueriesArtifact(VaultSession session)
    {
        var all = PinnedGraphQueries[0].Query;
        var read = SlateUniffiMethods.GraphConfigDecode(PinnedGraphConfigInput);
        var cfg = read.Config;
        var snapshot = session.GraphSnapshot(GraphInclusiveFilter);
        var keyOf = new Dictionary<ulong, string>();
        foreach (var n in snapshot.Nodes)
        {
            keyOf[n.Id] = n.StableKey;
        }
        var byKey = snapshot.Nodes
            .OrderBy(n => n, Comparer<GraphNode>.Create((a, b) => CompareUtf8(a.StableKey, b.StableKey)))
            .ToList();
        IEnumerable<string> KeysOf(IEnumerable<ulong> ids) => ids.Select(id => keyOf[id]);

        var j = new CanonicalJson();
        j.Raw("{\"snapshot\":[");
        for (int i = 0; i < byKey.Count; i++)
        {
            if (i > 0)
            {
                j.Raw(",");
            }
            var n = byKey[i];
            j.Raw("{\"key\":").Str(n.StableKey)
             .Raw(",\"label\":").Str(n.Label)
             .Raw(",\"kind\":").Str(GraphKindName(n.Kind))
             .Raw(",\"path\":");
            GraphOptionalString(j, n.Path == null ? null : Slash(n.Path));
            j.Raw(",\"in_links\":").Num((ulong)n.InLinks)
             .Raw(",\"out_links\":").Num((ulong)n.OutLinks)
             .Raw(",\"in_embeds\":").Num((ulong)n.InEmbeds)
             .Raw(",\"out_embeds\":").Num((ulong)n.OutEmbeds)
             .Raw(",\"component\":").Num((ulong)n.Component)
             .Raw(",\"is_orphan\":").Bool(n.IsOrphan)
             .Raw("}");
        }
        j.Raw("]");

        j.Raw(",\"visibility\":[");
        for (int q = 0; q < PinnedGraphQueries.Length; q++)
        {
            if (q > 0)
            {
                j.Raw(",");
            }
            var (name, query) = PinnedGraphQueries[q];
            var v = session.GraphVisibility(query);
            j.Raw("{\"query\":").Str(name)
             .Raw(",\"total\":").Num(v.Total)
             .Raw(",\"visible\":");
            GraphKeyList(j, KeysOf(v.Ids));
            j.Raw(",\"labeled\":");
            GraphKeyList(j, KeysOf(v.Labeled));
            j.Raw("}");
        }
        j.Raw("]");

        // The topology for the `all` query under the pinned config's groups
        // (0b-6b): the nodes and the visible edges, each in core's order,
        // the curve as an integer, every endpoint a key.
        j.Raw(",\"topology\":{\"nodes\":[");
        var topology = session.GraphTopology(all, cfg);
        for (int i = 0; i < topology.Nodes.Length; i++)
        {
            if (i > 0)
            {
                j.Raw(",");
            }
            var n = topology.Nodes[i];
            j.Raw("{\"key\":").Str(n.StableKey)
             .Raw(",\"diameter_x100\":").Num((long)Math.Round(n.Diameter * 100))
             .Raw(",\"group\":");
            if (n.Group == null)
            {
                j.Null();
            }
            else
            {
                j.Num((ulong)n.Group.Value);
            }
            j.Raw(",\"labeled\":").Bool(n.Labeled).Raw(",\"neighbors\":");
            GraphKeyList(j, n.Neighbors.Select(x => x.StableKey));
            j.Raw("}");
        }
        j.Raw("],\"edges\":[");
        for (int e = 0; e < topology.Edges.Length; e++)
        {
            if (e > 0)
            {
                j.Raw(",");
            }
            var edge = topology.Edges[e];
            // `from_key` / `to_key`: the schema walk types values by name,
            // and `from`/`to` (the step sections' labels) and `target` (the
            // ghost-path pin) are taken.
            j.Raw("{\"from_key\":").Str(keyOf[edge.SourceId])
             .Raw(",\"to_key\":").Str(keyOf[edge.TargetId])
             .Raw(",\"kind\":").Str(edge.Kind == GraphEdgeKind.Embed ? "embed" : "link")
             .Raw("}");
        }
        j.Raw("]}");

        j.Raw(",\"table\":[");
        // W6-2 PR A (AD-13): the summary the table's surface reads, under the
        // entry's filter — core's `audio_summary`, byte-identical on both lanes.
        string tableSummary = session.GraphSnapshot(all.Filter).AudioSummary;
        for (int s = 0; s < PinnedGraphTableSorts.Length; s++)
        {
            if (s > 0)
            {
                j.Raw(",");
            }
            var (column, ascending, name) = PinnedGraphTableSorts[s];
            var result = session.GraphTableRows(all, new GraphTableSort(column, ascending));
            j.Raw("{\"query\":").Str("all").Raw(",\"sort\":").Str(name)
             .Raw(",\"summary\":").Str(tableSummary)
             .Raw(",\"total\":").Num(result.Total).Raw(",\"rows\":[");
            for (int r = 0; r < result.Rows.Length; r++)
            {
                if (r > 0)
                {
                    j.Raw(",");
                }
                var row = result.Rows[r];
                // The nine cells minus Modified (index 6), named.
                j.Raw("{\"key\":").Str(row.StableKey)
                 .Raw(",\"note\":").Str(row.Cells[0])
                 .Raw(",\"links_in\":").Str(row.Cells[1])
                 .Raw(",\"links_out\":").Str(row.Cells[2])
                 .Raw(",\"embeds_in\":").Str(row.Cells[3])
                 .Raw(",\"embeds_out\":").Str(row.Cells[4])
                 .Raw(",\"component\":").Str(row.Cells[5])
                 .Raw(",\"folder\":").Str(Slash(row.Cells[7]))
                 .Raw(",\"kind\":").Str(row.Cells[8])
                 .Raw("}");
            }
            j.Raw("]}");
        }
        j.Raw("]");

        j.Raw(",\"connections\":[");
        for (int c = 0; c < PinnedGraphConnections.Length; c++)
        {
            if (c > 0)
            {
                j.Raw(",");
            }
            var (path, depth) = PinnedGraphConnections[c];
            var tree = session.GraphConnectionsTree(path, depth, GraphInclusiveFilter);
            j.Raw("{\"path\":").Str(path)
             .Raw(",\"depth\":").Num((ulong)depth)
             .Raw(",\"center_key\":").Str(tree.CenterKey)
             .Raw(",\"tree_depth\":").Num((ulong)tree.Depth)
             .Raw(",\"summary\":{\"center_label\":").Str(tree.SummaryCounts.CenterLabel)
             .Raw(",\"in_links\":").Num((ulong)tree.SummaryCounts.InLinks)
             .Raw(",\"out_links\":").Num((ulong)tree.SummaryCounts.OutLinks)
             .Raw(",\"note_count\":").Num(tree.SummaryCounts.NoteCount)
             .Raw(",\"depth\":").Num((ulong)tree.SummaryCounts.Depth)
             .Raw("}");
            foreach (var (name, list) in new[] { ("incoming", tree.Incoming), ("outgoing", tree.Outgoing) })
            {
                j.Raw(",\"" + name + "\":[");
                for (int r = 0; r < list.Length; r++)
                {
                    if (r > 0)
                    {
                        j.Raw(",");
                    }
                    var row = list[r];
                    j.Raw("{\"occurrence\":").Str(row.Id)
                     .Raw(",\"parent\":");
                    GraphOptionalString(j, row.ParentId);
                    j.Raw(",\"level\":").Num((ulong)row.Level)
                     .Raw(",\"key\":").Str(row.StableKey)
                     .Raw(",\"kind\":").Str(GraphKindName(row.Kind))
                     .Raw(",\"embed_only\":").Bool(row.EmbedOnly)
                     .Raw(",\"references\":").Num((ulong)row.References)
                     .Raw("}");
                }
                j.Raw("]");
            }
            j.Raw("}");
        }
        j.Raw("]");

        j.Raw(",\"ghost_paths\":[");
        for (int g = 0; g < PinnedGraphGhostTargets.Length; g++)
        {
            if (g > 0)
            {
                j.Raw(",");
            }
            j.Raw("{\"target\":").Str(PinnedGraphGhostTargets[g])
             .Raw(",\"path\":").Str(SlateUniffiMethods.GraphGhostNotePath(PinnedGraphGhostTargets[g]))
             .Raw("}");
        }
        j.Raw("]");

        var points = PinnedGraphSpatialPoints.Select(p => p.Point).ToArray();
        string? LabelOf(ulong? id) =>
            id == null ? null : PinnedGraphSpatialPoints.First(p => p.Point.Id == id.Value).Label;
        j.Raw(",\"spatial\":[");
        for (int t = 0; t < PinnedGraphSpatialDirections.Length; t++)
        {
            if (t > 0)
            {
                j.Raw(",");
            }
            var (dx, dy) = PinnedGraphSpatialDirections[t];
            ulong? to = SlateUniffiMethods.GraphSpatialStep(
                points, PinnedGraphSpatialNeighbors, points[0].Id, dx, dy);
            j.Raw("{\"from\":").Str("a")
             .Raw(",\"dx\":").Num(dx)
             .Raw(",\"dy\":").Num(dy)
             .Raw(",\"to\":");
            GraphOptionalString(j, LabelOf(to));
            j.Raw("}");
        }
        j.Raw("]");

        j.Raw(",\"structural\":[");
        var ids = PinnedGraphStructuralOrder
            .Select(label => PinnedGraphSpatialPoints.First(p => p.Label == label).Point.Id)
            .ToArray();
        bool first = true;
        foreach (string? from in PinnedGraphStructuralOrder.Select(l => (string?)l).Append(null))
        {
            foreach (bool forward in new[] { true, false })
            {
                if (!first)
                {
                    j.Raw(",");
                }
                first = false;
                ulong? fromId = from == null ? null : PinnedGraphSpatialPoints.First(p => p.Label == from).Point.Id;
                ulong? to = SlateUniffiMethods.GraphStructuralStep(ids, fromId, forward);
                j.Raw("{\"from\":");
                GraphOptionalString(j, from);
                j.Raw(",\"forward\":").Bool(forward).Raw(",\"to\":");
                GraphOptionalString(j, LabelOf(to));
                j.Raw("}");
            }
        }
        j.Raw("]");

        var constants = SlateUniffiMethods.GraphConstants();
        j.Raw(",\"constants\":{\"tier_b_threshold\":").Num((ulong)constants.TierBThreshold)
         .Raw(",\"label_cap\":").Num((ulong)constants.LabelCap)
         .Raw(",\"connections_depth_min\":").Num((ulong)constants.ConnectionsDepthMin)
         .Raw(",\"connections_depth_max\":").Num((ulong)constants.ConnectionsDepthMax)
         .Raw(",\"node_diameter_min\":").Num((long)constants.NodeDiameterMin)
         .Raw(",\"node_diameter_max\":").Num((long)constants.NodeDiameterMax)
         .Raw(",\"neighbor_label_cap\":").Num((ulong)constants.NeighborLabelCap)
         .Raw(",\"diameter_at_0\":").Num((long)SlateUniffiMethods.GraphNodeDiameter(0))
         .Raw(",\"diameter_at_1000000\":").Num((long)SlateUniffiMethods.GraphNodeDiameter(1000000))
         .Raw("}");

        j.Raw(",\"actions\":[");
        var kinds = new[] { GraphNodeKind.Note, GraphNodeKind.Attachment, GraphNodeKind.Ghost };
        for (int k = 0; k < kinds.Length; k++)
        {
            if (k > 0)
            {
                j.Raw(",");
            }
            j.Raw("{\"kind\":").Str(GraphKindName(kinds[k])).Raw(",\"actions\":[");
            var actions = SlateUniffiMethods.GraphRowActions(kinds[k]);
            for (int a = 0; a < actions.Length; a++)
            {
                if (a > 0)
                {
                    j.Raw(",");
                }
                j.Str(actions[a].Title);
            }
            j.Raw("]}");
        }
        j.Raw("]");

        j.Raw(",\"config\":{\"input\":").Str(PinnedGraphConfigInput)
         .Raw(",\"unknown_json\":").Str(read.UnknownJson)
         .Raw(",\"include_attachments\":").Bool(cfg.Filters.IncludeAttachments)
         .Raw(",\"name_query\":").Str(cfg.Filters.NameQuery)
         .Raw(",\"group_count\":").Num((ulong)cfg.Groups.Length)
         .Raw(",\"group_tags\":[");
        var tokens = SlateUniffiMethods.GraphColorTokens();
        var rings = SlateUniffiMethods.GraphRingStyles();
        for (int g = 0; g < cfg.Groups.Length; g++)
        {
            if (g > 0)
            {
                j.Raw(",");
            }
            var group = cfg.Groups[g];
            j.Str(tokens.First(t => t.Token == group.ColorToken).Tag + "/"
                + rings.First(r => r.Style == group.RingStyle).Tag);
        }
        j.Raw("],\"palette\":[");
        for (int p = 0; p < tokens.Length; p++)
        {
            if (p > 0)
            {
                j.Raw(",");
            }
            j.Str(tokens[p].Tag + "/" + tokens[p].Title);
        }
        j.Raw("],\"ring_styles\":[");
        for (int r = 0; r < rings.Length; r++)
        {
            if (r > 0)
            {
                j.Raw(",");
            }
            j.Str(rings[r].Tag + "/" + rings[r].Title);
        }
        j.Raw("],\"modes\":[");
        var modes = SlateUniffiMethods.GraphSurfaceModes();
        for (int m = 0; m < modes.Length; m++)
        {
            if (m > 0)
            {
                j.Raw(",");
            }
            j.Str(modes[m].Tag + "/" + modes[m].Title);
        }
        j.Raw("],\"verbosities\":[");
        var levels = SlateUniffiMethods.GraphVerbosities();
        for (int v = 0; v < levels.Length; v++)
        {
            if (v > 0)
            {
                j.Raw(",");
            }
            j.Str(levels[v].Tag + "/" + levels[v].Title);
        }
        j.Raw("],\"connections_depth\":").Num((ulong)cfg.ConnectionsDepth)
         .Raw(",\"mode\":").Str(cfg.Mode == GraphSurfaceMode.Diagram ? "diagram" : "table")
         .Raw(",\"verbosity\":").Str(cfg.Verbosity switch
         {
             GraphVerbosity.Terse => "terse",
             GraphVerbosity.Verbose => "verbose",
             _ => "standard",
         })
         .Raw(",\"repel_x100\":").Num((long)Math.Round(cfg.Forces.Repel * 100))
         .Raw(",\"center_x100\":").Num((long)Math.Round(cfg.Forces.Center * 100))
         .Raw(",\"link_x100\":").Num((long)Math.Round(cfg.Forces.Link * 100))
         .Raw(",\"node_size_x100\":").Num((long)Math.Round(cfg.Display.NodeSizeMultiplier * 100))
         .Raw(",\"encoded\":").Str(SlateUniffiMethods.GraphConfigEncode(cfg, PinnedGraphConfigInput))
         .Raw(",\"encoded_fresh\":").Str(SlateUniffiMethods.GraphConfigEncode(SlateUniffiMethods.GraphConfigDefault(), null))
         .Raw("}");

        j.Raw("}");
        return j + "\n";
    }

    // --- W6-1 PR 0b: the canvas_queries section (contract 0b-15) ----------
    //
    // The three pinned input lists below are DUPLICATED in the mac twin
    // (apps/slate-mac/Tests/SlateMacTests/ParityHarnessTests.swift), the
    // same way `PinnedSearchQueries` is: the twins cannot share a
    // declaration across languages, and the committed golden arbitrates
    // — two lists that drift produce different artifacts and the mac
    // census fails on the same golden this one passes.

    /// <summary>
    /// Filter queries the canvas artifact pins. The set is chosen for
    /// what each proves, not for coverage: the EMPTY query (which
    /// matches everything — the easiest rule to get backwards), a plain
    /// ASCII needle, the same needle mixed-case and whitespace-padded
    /// (case folding and trimming), a group-path element, the `kind`
    /// type word (typing "group" selects every group — shipped mac
    /// behaviour), and one that matches nothing.
    /// </summary>
    public static readonly string[] PinnedCanvasFilterQueries =
    {
        "",
        "card",
        "ReSeArCh",
        "  research  ",
        "Q3",
        "group",
        "zzz-nothing",
    };

    /// <summary>
    /// Rects the canvas artifact describes relatively. The last one
    /// coincides exactly with a card in <c>sample.canvas</c>, which is
    /// the zero-delta case whose axis rule is pinned in contract 0b-7.
    /// </summary>
    public static readonly CanvasRect[] PinnedCanvasRelativeRects =
    {
        new CanvasRect(0.0, 0.0, 240.0, 140.0),
        new CanvasRect(300.0, 150.0, 240.0, 140.0),
        new CanvasRect(-1000.0, -1000.0, 10.0, 10.0),
        new CanvasRect(640.0, 180.0, 240.0, 140.0),
    };

    /// <summary>
    /// Canvas fixtures deliberately kept OUT of the artifact.
    /// <c>large_2000.canvas</c> is the §K performance fixture; at 2,000
    /// nodes its per-node rows would commit a golden no reviewer reads.
    /// Its coverage is the in-crate census, which runs the same queries
    /// over it.
    /// </summary>
    public static readonly string[] CanvasArtifactExclusions = { "large_2000.canvas" };

    /// <summary>
    /// Vault-level artifact: the W6-1 PR 0b structural queries over
    /// every canvas fixture — bounds, then per node in reading order
    /// its speakable name, parent, children and traced path, then the
    /// pinned filter results and relative descriptions.
    /// </summary>
    public static string CanvasQueriesArtifact(
        VaultSession session,
        IReadOnlyList<string> canvasFiles)
    {
        var j = new CanonicalJson();
        j.Raw("{\"canvases\":[");
        for (int f = 0; f < canvasFiles.Count; f++)
        {
            if (f > 0)
            {
                j.Raw(",");
            }

            string rel = canvasFiles[f];
            CanvasOpenInfo info = session.OpenCanvas(rel);
            try
            {
                j.Raw("{\"file\":").Str(Slash(rel))
                 .Raw(",\"degraded\":").Bool(info.Degraded)
                 .Raw(",\"bounds\":");
                CanvasRect? bounds = session.CanvasBounds(info.Handle);
                if (bounds == null)
                {
                    j.Null();
                }
                else
                {
                    AppendCanvasRect(j, bounds);
                }

                j.Raw(",\"nodes\":[");
                var outline = session.CanvasOutline(info.Handle);
                for (int i = 0; i < outline.Length; i++)
                {
                    if (i > 0)
                    {
                        j.Raw(",");
                    }
                    string nodeId = outline[i].NodeId;
                    j.Raw("{\"node_id\":").Str(nodeId)
                     .Raw(",\"speakable_name\":").Str(outline[i].SpeakableName)
                     .Raw(",\"parent\":");
                    string? parent = session.CanvasParentOf(info.Handle, nodeId);
                    if (parent == null)
                    {
                        j.Null();
                    }
                    else
                    {
                        j.Str(parent);
                    }

                    j.Raw(",\"children\":[");
                    var children = session.CanvasChildrenOf(info.Handle, nodeId);
                    for (int c = 0; c < children.Length; c++)
                    {
                        if (c > 0)
                        {
                            j.Raw(",");
                        }
                        j.Str(children[c]);
                    }
                    j.Raw("]");

                    j.Raw(",\"trace\":[");
                    var hops = session.CanvasTracePath(info.Handle, nodeId);
                    for (int t = 0; t < hops.Length; t++)
                    {
                        if (t > 0)
                        {
                            j.Raw(",");
                        }
                        j.Raw("{\"edge_id\":").Str(hops[t].EdgeId)
                         .Raw(",\"node_id\":").Str(hops[t].NodeId)
                         .Raw(",\"title\":").Str(hops[t].Title)
                         .Raw(",\"label\":");
                        if (hops[t].Label == null)
                        {
                            j.Null();
                        }
                        else
                        {
                            j.Str(hops[t].Label!);
                        }
                        j.Raw("}");
                    }
                    j.Raw("]}");
                }
                j.Raw("]");

                j.Raw(",\"filters\":[");
                for (int q = 0; q < PinnedCanvasFilterQueries.Length; q++)
                {
                    if (q > 0)
                    {
                        j.Raw(",");
                    }
                    j.Raw("{\"query\":").Str(PinnedCanvasFilterQueries[q])
                     .Raw(",\"matched\":[");
                    var matched = session.CanvasFilter(info.Handle, PinnedCanvasFilterQueries[q]);
                    for (int m = 0; m < matched.Length; m++)
                    {
                        if (m > 0)
                        {
                            j.Raw(",");
                        }
                        j.Str(matched[m]);
                    }
                    j.Raw("]}");
                }
                j.Raw("]");

                j.Raw(",\"relative\":[");
                for (int r = 0; r < PinnedCanvasRelativeRects.Length; r++)
                {
                    if (r > 0)
                    {
                        j.Raw(",");
                    }
                    j.Raw("{\"rect\":");
                    AppendCanvasRect(j, PinnedCanvasRelativeRects[r]);
                    j.Raw(",\"descs\":[");
                    var descs = session.CanvasDescribeRelative(
                        info.Handle, PinnedCanvasRelativeRects[r], Array.Empty<string>());
                    for (int d = 0; d < descs.Length; d++)
                    {
                        if (d > 0)
                        {
                            j.Raw(",");
                        }
                        j.Raw("{\"kind\":").Str(RelativeDescName(descs[d]))
                         .Raw(",\"anchor\":").Str(RelativeDescAnchor(descs[d]))
                         .Raw("}");
                    }
                    j.Raw("]}");
                }
                j.Raw("]}");
            }
            finally
            {
                session.CloseCanvas(info.Handle);
            }
        }
        j.Raw("]}");
        return j + "\n";
    }

    public static string RelativeDescName(CanvasRelativeDesc desc) => desc switch
    {
        CanvasRelativeDesc.Below => "below",
        CanvasRelativeDesc.RightOf => "right_of",
        CanvasRelativeDesc.Above => "above",
        CanvasRelativeDesc.LeftOf => "left_of",
        CanvasRelativeDesc.AtOrigin => "at_origin",
        _ => throw new InvalidOperationException($"unmapped CanvasRelativeDesc {desc}"),
    };

    public static string RelativeDescAnchor(CanvasRelativeDesc desc) => desc switch
    {
        CanvasRelativeDesc.Below b => b.AnchorTitle,
        CanvasRelativeDesc.RightOf r => r.AnchorTitle,
        CanvasRelativeDesc.Above a => a.AnchorTitle,
        CanvasRelativeDesc.LeftOf l => l.AnchorTitle,
        CanvasRelativeDesc.AtOrigin => string.Empty,
        _ => throw new InvalidOperationException($"unmapped CanvasRelativeDesc {desc}"),
    };

    private static void AppendCanvasRect(CanonicalJson j, CanvasRect rect)
    {
        j.Raw("{\"x\":").Num(rect.X)
         .Raw(",\"y\":").Num(rect.Y)
         .Raw(",\"width\":").Num(rect.Width)
         .Raw(",\"height\":").Num(rect.Height)
         .Raw("}");
    }

    // --- W6-1 PR A: the canvas_read section (contract A20) ---------------
    //
    // The READ side of the canvas surface, over the same fixture corpus
    // and the same exclusions as `canvas_queries` (0b-15): the open
    // info, then the three projections PR A/B/D consume, then per node
    // in reading order the two per-node reads the outline and the
    // navigator make. The Swift twin lands with the mac lane and the
    // committed golden arbitrates, exactly as `canvas_queries` did.

    /// <summary>
    /// Vault-level artifact: the W6-1 PR A read surface over every
    /// canvas fixture — <c>open_canvas</c> info, outline rows, table
    /// rows, the scene, and per node <c>canvas_where_am_i</c> plus
    /// <c>canvas_neighbors</c>.
    /// </summary>
    public static string CanvasReadArtifact(
        VaultSession session,
        IReadOnlyList<string> canvasFiles)
    {
        var j = new CanonicalJson();
        j.Raw("{\"canvases\":[");
        for (int f = 0; f < canvasFiles.Count; f++)
        {
            if (f > 0)
            {
                j.Raw(",");
            }

            string rel = canvasFiles[f];
            CanvasOpenInfo info = session.OpenCanvas(rel);
            try
            {
                j.Raw("{\"file\":").Str(Slash(rel))
                 .Raw(",\"node_count\":").Num(info.NodeCount)
                 .Raw(",\"edge_count\":").Num(info.EdgeCount)
                 .Raw(",\"degraded\":").Bool(info.Degraded)
                 .Raw(",\"warnings\":[");
                for (int w = 0; w < info.Warnings.Length; w++)
                {
                    if (w > 0)
                    {
                        j.Raw(",");
                    }
                    j.Raw("{\"kind\":").Str(LoadWarningKindName(info.Warnings[w].Kind))
                     .Raw(",\"detail\":").Str(info.Warnings[w].Detail)
                     .Raw("}");
                }
                j.Raw("]");

                // A degraded open has nothing worth reading: core
                // returns an empty canvas and the host releases the
                // handle at once (contract A3, CD-28). The sections stay
                // present and EMPTY rather than absent, so the
                // artifact's shape never varies with the fixture.
                CanvasOutlineRow[] outline = info.Degraded
                    ? []
                    : session.CanvasOutline(info.Handle);
                CanvasTableRow[] tableRows = info.Degraded
                    ? []
                    : session.CanvasTableRows(info.Handle);
                CanvasScene scene = info.Degraded
                    ? new CanvasScene([], [])
                    : session.CanvasScene(info.Handle);

                j.Raw(",\"outline\":[");
                for (int i = 0; i < outline.Length; i++)
                {
                    if (i > 0)
                    {
                        j.Raw(",");
                    }
                    CanvasOutlineRow row = outline[i];
                    j.Raw("{\"node_id\":").Str(row.NodeId)
                     .Raw(",\"depth\":").Num(row.Depth)
                     .Raw(",\"kind\":").Str(row.Kind)
                     .Raw(",\"title\":").Str(row.Title)
                     .Raw(",\"speakable_name\":").Str(row.SpeakableName)
                     .Raw(",\"group_path\":");
                    AppendStrings(j, row.GroupPath);
                    j.Raw(",\"ordinal_n\":").Num(row.OrdinalN)
                     .Raw(",\"total_m\":").Num(row.TotalM)
                     .Raw(",\"connection_count\":").Num(row.ConnectionCount)
                     .Raw(",\"color_name\":");
                    AppendOptional(j, row.ColorName);
                    j.Raw("}");
                }
                j.Raw("]");

                j.Raw(",\"table\":[");
                for (int i = 0; i < tableRows.Length; i++)
                {
                    if (i > 0)
                    {
                        j.Raw(",");
                    }
                    CanvasTableRow row = tableRows[i];
                    j.Raw("{\"node_id\":").Str(row.NodeId)
                     .Raw(",\"kind\":").Str(row.Kind)
                     .Raw(",\"title\":").Str(row.Title)
                     .Raw(",\"speakable_name\":").Str(row.SpeakableName)
                     .Raw(",\"group_path\":");
                    AppendStrings(j, row.GroupPath);
                    j.Raw(",\"target\":").Str(row.Target)
                     .Raw(",\"connection_count\":").Num(row.ConnectionCount)
                     .Raw(",\"color_name\":");
                    AppendOptional(j, row.ColorName);
                    j.Raw("}");
                }
                j.Raw("]");

                j.Raw(",\"scene\":{\"nodes\":[");
                for (int i = 0; i < scene.Nodes.Length; i++)
                {
                    if (i > 0)
                    {
                        j.Raw(",");
                    }
                    CanvasSceneNode node = scene.Nodes[i];
                    j.Raw("{\"node_id\":").Str(node.NodeId)
                     .Raw(",\"kind\":").Str(node.Kind)
                     .Raw(",\"title\":").Str(node.Title)
                     .Raw(",\"speakable_name\":").Str(node.SpeakableName)
                     .Raw(",\"x\":").Num(node.X)
                     .Raw(",\"y\":").Num(node.Y)
                     .Raw(",\"width\":").Num(node.Width)
                     .Raw(",\"height\":").Num(node.Height)
                     .Raw(",\"color\":");
                    AppendOptional(j, node.Color);
                    j.Raw(",\"color_name\":");
                    AppendOptional(j, node.ColorName);
                    j.Raw(",\"subpath\":");
                    AppendOptional(j, node.Subpath);
                    j.Raw("}");
                }
                j.Raw("],\"edges\":[");
                for (int i = 0; i < scene.Edges.Length; i++)
                {
                    if (i > 0)
                    {
                        j.Raw(",");
                    }
                    CanvasSceneEdge edge = scene.Edges[i];
                    j.Raw("{\"edge_id\":").Str(edge.EdgeId)
                     .Raw(",\"from_node\":").Str(edge.FromNode)
                     .Raw(",\"from_side\":");
                    AppendSide(j, edge.FromSide);
                    j.Raw(",\"to_node\":").Str(edge.ToNode)
                     .Raw(",\"to_side\":");
                    AppendSide(j, edge.ToSide);
                    j.Raw(",\"from_arrow\":").Bool(edge.FromArrow)
                     .Raw(",\"to_arrow\":").Bool(edge.ToArrow)
                     .Raw(",\"label\":");
                    AppendOptional(j, edge.Label);
                    j.Raw(",\"color\":");
                    AppendOptional(j, edge.Color);
                    j.Raw("}");
                }
                j.Raw("]}");

                j.Raw(",\"nodes\":[");
                for (int i = 0; i < outline.Length; i++)
                {
                    if (i > 0)
                    {
                        j.Raw(",");
                    }
                    string nodeId = outline[i].NodeId;
                    j.Raw("{\"node_id\":").Str(nodeId)
                     .Raw(",\"where_am_i\":");
                    AppendWhereAmI(j, session, info.Handle, nodeId);
                    j.Raw(",\"neighbors\":[");
                    CanvasNeighbor[] neighbors =
                        session.CanvasNeighbors(info.Handle, nodeId);
                    for (int n = 0; n < neighbors.Length; n++)
                    {
                        if (n > 0)
                        {
                            j.Raw(",");
                        }
                        CanvasNeighbor neighbor = neighbors[n];
                        j.Raw("{\"edge_id\":").Str(neighbor.EdgeId)
                         .Raw(",\"other_node\":").Str(neighbor.OtherNode)
                         .Raw(",\"other_title\":").Str(neighbor.OtherTitle)
                         .Raw(",\"direction\":").Str(EdgeDirectionName(neighbor.Direction))
                         .Raw(",\"self_side\":");
                        AppendSide(j, neighbor.SelfSide);
                        j.Raw(",\"label\":");
                        AppendOptional(j, neighbor.Label);
                        j.Raw(",\"self_is_from\":").Bool(neighbor.SelfIsFrom)
                         .Raw("}");
                    }
                    j.Raw("]}");
                }
                j.Raw("]}");
            }
            finally
            {
                session.CloseCanvas(info.Handle);
            }
        }
        j.Raw("]}");
        return j + "\n";
    }

    /// <summary>
    /// Where-am-I is MODEL-backed, so under the 0b-6 skew it refuses for
    /// an id the SQLite-served outline rows still name. The artifact has
    /// to be able to express that shape, so a refusal serializes as
    /// <c>null</c> rather than aborting the run — the same shape the
    /// host handles gracefully (contracts A16/A20).
    /// </summary>
    private static void AppendWhereAmI(
        CanonicalJson j, VaultSession session, ulong handle, string nodeId)
    {
        CanvasWhereAmI where;
        try
        {
            where = session.CanvasWhereAmI(handle, nodeId);
        }
        catch (VaultException)
        {
            j.Null();
            return;
        }
        j.Raw("{\"title\":").Str(where.Title)
         .Raw(",\"speakable_name\":").Str(where.SpeakableName)
         .Raw(",\"kind\":").Str(where.Kind)
         .Raw(",\"group_path\":");
        AppendStrings(j, where.GroupPath);
        j.Raw(",\"ordinal_n\":").Num(where.OrdinalN)
         .Raw(",\"total_m\":").Num(where.TotalM)
         .Raw(",\"connection_count\":").Num(where.ConnectionCount)
         .Raw(",\"in_count\":").Num(where.InCount)
         .Raw(",\"out_count\":").Num(where.OutCount)
         .Raw(",\"color_name\":");
        AppendOptional(j, where.ColorName);
        j.Raw("}");
    }

    private static void AppendStrings(CanonicalJson j, IReadOnlyList<string> values)
    {
        j.Raw("[");
        for (int i = 0; i < values.Count; i++)
        {
            if (i > 0)
            {
                j.Raw(",");
            }
            j.Str(values[i]);
        }
        j.Raw("]");
    }

    private static void AppendOptional(CanonicalJson j, string? value)
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

    private static void AppendSide(CanonicalJson j, CanvasSide? side)
    {
        if (side is null)
        {
            j.Null();
        }
        else
        {
            j.Str(SideName(side.Value));
        }
    }

    public static string SideName(CanvasSide side) => side switch
    {
        CanvasSide.Top => "top",
        CanvasSide.Right => "right",
        CanvasSide.Bottom => "bottom",
        CanvasSide.Left => "left",
        _ => throw new InvalidOperationException($"unmapped CanvasSide {side}"),
    };

    public static string EdgeDirectionName(CanvasEdgeDirection direction) => direction switch
    {
        CanvasEdgeDirection.Outgoing => "outgoing",
        CanvasEdgeDirection.Incoming => "incoming",
        CanvasEdgeDirection.Bidirectional => "bidirectional",
        CanvasEdgeDirection.Undirected => "undirected",
        _ => throw new InvalidOperationException(
            $"unmapped CanvasEdgeDirection {direction}"),
    };

    public static string LoadWarningKindName(CanvasLoadWarningKind kind) => kind switch
    {
        CanvasLoadWarningKind.ParseFailed => "parse_failed",
        CanvasLoadWarningKind.SkippedEntry => "skipped_entry",
        CanvasLoadWarningKind.DanglingEdge => "dangling_edge",
        CanvasLoadWarningKind.IgnoredValue => "ignored_value",
        _ => throw new InvalidOperationException($"unmapped CanvasLoadWarningKind {kind}"),
    };
}
