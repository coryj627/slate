import Foundation

/// One `![[…]]` embed span discovered in the editor's text. `range`
/// is an NSRange over the buffer's UTF-16 view (the same coordinate
/// space NSTextView uses internally); `target` is the cache key
/// form (target + optional `#heading` / `^block` suffix) so a
/// lookup against `AppState.currentNoteEmbedResolutions` matches.
struct EditorEmbedSpan: Equatable {
    /// UTF-16 range of the full `![[…]]` syntax in the buffer.
    /// Includes the leading `![[` and trailing `]]`.
    let range: NSRange
    /// The text between `[[` and `]]` — the embed's target as
    /// authored. This matches the cache key for
    /// `AppState.currentNoteEmbedResolutions` lookups.
    let target: String
}

/// Find every `![[…]]` embed reference in `text`. Best-effort
/// regex match — covers the wikilink embed shape (the dominant
/// case). Markdown image embeds (`![alt](src)`) are not detected
/// here yet; the inline editor highlighting / Cmd+E flow targets
/// wikilink embeds first, since those are what `resolve_embed`
/// returns note / section / block resolutions for.
///
/// `range` is UTF-16 (the NSString / NSTextView coordinate space)
/// so the result drops into NSTextStorage attribute calls without
/// further conversion.
///
/// Drift risk: the backend's link parser (`slate_core::links`)
/// recognises wikilink embeds with anchor suffixes and escape
/// handling that this regex doesn't model exactly. For visual
/// highlighting + Cmd+E preview, a few edge-case misses are
/// acceptable. The full inline NSTextAttachment path (deferred
/// V1.x) will route through the backend's span output instead.
func findEditorEmbedSpans(in text: String) -> [EditorEmbedSpan] {
    // `!\[\[…\]\]` — opening `![[`, capture group of one or more
    // non-`]` / non-newline chars, closing `]]`. Newlines bail
    // out so a `![[…` that doesn't close on the same line doesn't
    // sweep across multiple paragraphs.
    let pattern = #"!\[\[([^\]\n]+)\]\]"#
    guard let regex = try? NSRegularExpression(pattern: pattern) else {
        return []
    }
    let nsText = text as NSString
    let fullRange = NSRange(location: 0, length: nsText.length)
    var out: [EditorEmbedSpan] = []
    regex.enumerateMatches(in: text, options: [], range: fullRange) { match, _, _ in
        guard let match, match.numberOfRanges >= 2 else { return }
        let fullSpan = match.range
        let targetRange = match.range(at: 1)
        guard targetRange.location != NSNotFound else { return }
        let target = nsText.substring(with: targetRange)
        // Trim any inadvertent whitespace inside the brackets so
        // the cache lookup matches what `resolve_embed` parsed.
        let trimmed = target.trimmingCharacters(in: .whitespaces)
        if trimmed.isEmpty { return }
        out.append(EditorEmbedSpan(range: fullSpan, target: trimmed))
    }
    return out
}

/// Find the embed span whose range contains `cursor` (UTF-16
/// offset). Used by the editor's Cmd+E handler — the user's
/// cursor is anywhere inside (or at the right edge of) an embed,
/// and we open the preview for that one.
///
/// Returns `nil` when the cursor isn't inside any embed.
func embedSpanContaining(cursor: Int, in spans: [EditorEmbedSpan]) -> EditorEmbedSpan? {
    spans.first { span in
        let end = span.range.location + span.range.length
        return cursor >= span.range.location && cursor <= end
    }
}
