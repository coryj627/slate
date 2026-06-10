// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import Foundation
import SwiftUI

/// Standalone renderer for one `EmbedResolution`. The host (the
/// read pane today, the editor's NSTextAttachment cell later)
/// passes in the resolution + optional "jump to source" callback;
/// this view owns the disclosure shell, the AT label, and the
/// per-variant content layout.
///
/// Standalone by design (no `@EnvironmentObject AppState`) so it
/// can render inside both SwiftUI hierarchies and AppKit
/// attachment cells without dragging an environment in.
///
/// Recursion: `FullNote` / `Section` carry a pre-resolved
/// `nested` tree from the backend. The view depth-guards locally
/// (defense in depth on top of the backend's own depth limit) so
/// a malformed resolver response can't blow the stack. Nested
/// embeds (`depth > 0`) start collapsed so VoiceOver users can
/// skip past a top-level embed without traversing every level of
/// its body (audit #195).
struct EmbedView: View {
    let resolution: EmbedResolution
    let jumpToSourceAction: ((String) -> Void)?

    /// Current nesting depth. Top-level callers leave this at 0.
    /// Each recursive `EmbedView` for a nested resolution bumps
    /// it; past `embedDepthLimit` we render the `DepthLimitReached`
    /// fallback even if the resolution would otherwise resolve.
    var depth: Int = 0

    /// Mirror of `slate_core::MAX_EMBED_DEPTH` for the local
    /// guard. Kept as a constant so a future change to the
    /// backend cap surfaces as a `Self.embedDepthLimit` update,
    /// not a magic number sprinkled through the view.
    static let embedDepthLimit: Int = 3

    var body: some View {
        if depth >= Self.embedDepthLimit {
            depthLimitView(target: targetForDepthLimit(resolution: resolution))
        } else {
            content
        }
    }

    @ViewBuilder
    private var content: some View {
        switch resolution {
        case .fullNote(let targetPath, let text, let nested):
            fullNoteView(targetPath: targetPath, text: text, nested: nested)
        case .section(let targetPath, let heading, let text, let nested):
            sectionView(targetPath: targetPath, heading: heading, text: text, nested: nested)
        case .block(let targetPath, let blockId, let text):
            blockView(targetPath: targetPath, blockId: blockId, text: text)
        case .image(let targetPath, let bytes, let mime, let alt):
            imageView(targetPath: targetPath, bytes: bytes, mime: mime, alt: alt)
        case .unresolved(let reason):
            unresolvedView(reason: reason)
        }
    }

    // MARK: Variants

    private func fullNoteView(
        targetPath: String,
        text: String,
        nested: [NestedEmbed]
    ) -> some View {
        EmbedDisclosure(
            label: "Embedded note: \(targetPath)",
            jumpToSourceAction: jumpToSourceAction,
            jumpToTarget: targetPath,
            initiallyExpanded: depth == 0
        ) {
            EmbeddedNoteBody(
                text: text,
                nested: nested,
                jumpToSourceAction: jumpToSourceAction,
                depth: depth + 1
            )
        }
    }

    private func sectionView(
        targetPath: String,
        heading: String,
        text: String,
        nested: [NestedEmbed]
    ) -> some View {
        EmbedDisclosure(
            label: "Embedded section: \(heading) from \(targetPath)",
            jumpToSourceAction: jumpToSourceAction,
            jumpToTarget: targetPath,
            initiallyExpanded: depth == 0
        ) {
            EmbeddedNoteBody(
                text: text,
                nested: nested,
                jumpToSourceAction: jumpToSourceAction,
                depth: depth + 1
            )
        }
    }

    private func blockView(
        targetPath: String,
        blockId: String,
        text: String
    ) -> some View {
        EmbedDisclosure(
            label: "Embedded block from \(targetPath)",
            jumpToSourceAction: jumpToSourceAction,
            jumpToTarget: targetPath,
            initiallyExpanded: depth == 0
        ) {
            // WCAG 2.5.3 (audit #193): the AX label is the visible
            // text first, with the block id named at the end so
            // VoiceOver reads the content before any metadata.
            // Block ids like `^my-block-1` would otherwise be
            // spelled out character-by-character ahead of the
            // user's actual content.
            Text(verbatim: text)
                .font(.body)
                .textSelection(.enabled)
                .frame(maxWidth: .infinity, alignment: .leading)
                .padding(.vertical, 4)
                .accessibilityLabel("\(text). Block id: \(blockId).")
        }
    }

    private func imageView(
        targetPath: String,
        bytes: Data,
        mime: String,
        alt: String?
    ) -> some View {
        // Audit #196: image embeds wrap in the same EmbedDisclosure
        // shell as the other variants so sighted users see the
        // "Embedded image: <…>" title and everyone gets a
        // Jump-to-source affordance.
        let title = Self.imageEmbedTitle(targetPath: targetPath, alt: alt)
        return EmbedDisclosure(
            label: title,
            jumpToSourceAction: jumpToSourceAction,
            jumpToTarget: targetPath,
            initiallyExpanded: depth == 0
        ) {
            if let nsImage = NSImage(data: bytes) {
                Image(nsImage: nsImage)
                    .resizable()
                    .scaledToFit()
                    .frame(maxWidth: .infinity)
                    .accessibilityLabel(title)
                    .help(title)
            } else {
                imageDecodeFailureView(mime: mime)
            }
        }
    }

    /// AT/visible title for an image embed. The author's alt text is
    /// the description (WCAG 1.1.1, #419 — "alt text becomes the AT
    /// label" per M10); the filename is only the fallback.
    /// Audit #198: an empty/whitespace alt collapses to the filename,
    /// never to "Embedded image: ".
    static func imageEmbedTitle(targetPath: String, alt: String?) -> String {
        let trimmedAlt = alt?.trimmingCharacters(in: .whitespaces)
        let altText: String? =
            (trimmedAlt?.isEmpty == false) ? trimmedAlt : nil
        let descriptor =
            altText ?? (targetPath as NSString).lastPathComponent
        return "Embedded image: \(descriptor)"
    }

    private func imageDecodeFailureView(mime: String) -> some View {
        // Audit #192: conveying error state by red color alone
        // dropped contrast below 4.5:1. Lead with an SF Symbol
        // (shape-encoded warning) + primary-color text — the
        // semantic is carried by the icon, the text passes
        // contrast trivially.
        //
        // The EmbedDisclosure wrapper already labels this region
        // with "Embedded image: <descriptor>"; the inner view only
        // needs to carry the failure detail, not re-state the
        // outer label (audit fixup PR #191).
        let message =
            "Could not decode image. MIME: \(mime). The file may be corrupt or an unsupported codec."
        return HStack(alignment: .top, spacing: 6) {
            Image(systemName: "exclamationmark.triangle.fill")
                .foregroundStyle(.orange)
                .accessibilityHidden(true)
            Text(verbatim: message)
                .font(.caption)
                .foregroundStyle(.primary)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(.vertical, 4)
        .accessibilityElement(children: .combine)
        .accessibilityLabel(message)
    }

    private func unresolvedView(reason: EmbedUnresolvedReason) -> some View {
        let (visible, axLabel) = unresolvedText(reason: reason)
        // Same shape as imageDecodeFailureView — color isn't the
        // only cue.
        return HStack(alignment: .top, spacing: 6) {
            Image(systemName: "exclamationmark.triangle.fill")
                .foregroundStyle(.orange)
                .accessibilityHidden(true)
            Text(verbatim: visible)
                .font(.callout)
                .foregroundStyle(.primary)
        }
        .padding(.vertical, 4)
        .accessibilityElement(children: .combine)
        .accessibilityLabel(axLabel)
    }

    private func depthLimitView(target: String?) -> some View {
        // Defense-in-depth fallback when the depth counter trips
        // even though the backend's `MAX_EMBED_DEPTH` should have
        // already stopped us. WCAG 2.5.3: AX label begins with the
        // visible text. Audit #197: name the target if we have one
        // + point at a remediation so the user knows what they're
        // missing and how to see it.
        let head = target.map { "Embed depth limit reached for \($0)." }
            ?? "Embed depth limit reached."
        let remediation =
            "Open the source note directly to see embeds nested beyond depth \(Self.embedDepthLimit)."
        let visible = "\(head) \(remediation)"
        return HStack(alignment: .top, spacing: 6) {
            Image(systemName: "arrow.down.right.and.arrow.up.left")
                .foregroundStyle(.secondary)
                .accessibilityHidden(true)
            Text(verbatim: visible)
                .font(.callout)
                .foregroundStyle(.primary)
        }
        .padding(.vertical, 4)
        .accessibilityElement(children: .combine)
        .accessibilityLabel(visible)
    }

    private func targetForDepthLimit(resolution: EmbedResolution) -> String? {
        switch resolution {
        case .fullNote(let target, _, _),
            .section(let target, _, _, _),
            .block(let target, _, _),
            .image(let target, _, _, _):
            return target
        case .unresolved:
            return nil
        }
    }

    private func unresolvedText(reason: EmbedUnresolvedReason) -> (visible: String, ax: String) {
        switch reason {
        case .targetNotFound(let target):
            let v = "Unresolved embed: \(target)"
            return (v, "\(v). The target note or attachment doesn't exist in this vault.")
        case .headingNotFound(let targetPath, let heading):
            let v = "Unresolved embed: \(targetPath)#\(heading)"
            return (v, "\(v). The heading wasn't found in the target note.")
        case .blockNotFound(let targetPath, let blockId):
            let v = "Unresolved embed: \(targetPath)^\(blockId)"
            return (v, "\(v). The block anchor wasn't found in the target note.")
        case .depthLimitReached:
            let v = "Unresolved embed: depth limit reached."
            return (v, "\(v). Further nested embeds inside this one are not rendered.")
        case .readError(let message):
            let v = "Unresolved embed: read error — \(message)"
            return (v, "\(v). Reading the target failed.")
        }
    }
}

// MARK: - Disclosure shell

/// One disclosure-group + optional "jump to source" button shared by
/// the FullNote / Section / Block / Image variants. Splitting it out
/// keeps the per-variant AT label / activation behavior in one spot.
///
/// `initiallyExpanded` controls whether the group starts open. The
/// per-variant call sites pass `depth == 0`: top-level embeds are
/// open by default (sighted users see the body without clicking),
/// nested embeds start collapsed so screen-reader users can skip
/// over an embedded note without traversing four levels of body
/// content first (audit #195).
private struct EmbedDisclosure<Content: View>: View {
    let label: String
    let jumpToSourceAction: ((String) -> Void)?
    let jumpToTarget: String?
    let initiallyExpanded: Bool
    let content: () -> Content

    @State private var expanded: Bool

    init(
        label: String,
        jumpToSourceAction: ((String) -> Void)?,
        jumpToTarget: String?,
        initiallyExpanded: Bool,
        @ViewBuilder content: @escaping () -> Content
    ) {
        self.label = label
        self.jumpToSourceAction = jumpToSourceAction
        self.jumpToTarget = jumpToTarget
        self.initiallyExpanded = initiallyExpanded
        self.content = content
        _expanded = State(initialValue: initiallyExpanded)
    }

    var body: some View {
        DisclosureGroup(isExpanded: $expanded) {
            VStack(alignment: .leading, spacing: 6) {
                content()
                if let action = jumpToSourceAction, let target = jumpToTarget {
                    Button("Jump to source") {
                        action(target)
                    }
                    .accessibilityLabel("Jump to source: \(target)")
                }
            }
            .padding(.leading, 12)
        } label: {
            // Audit #194: dropped `.accessibilityAddTraits(.isHeader)`.
            // Embed-disclosure labels were polluting VoiceOver's
            // Headings rotor with embed chrome — inside a note that
            // contains four embeds the rotor would show four
            // "Embedded note:" entries among the user's real
            // headings. DisclosureGroup's native role already
            // announces "disclosure group, collapsed/expanded".
            Text(verbatim: label)
                .font(.callout.weight(.semibold))
        }
        .padding(.vertical, 4)
        .background(
            RoundedRectangle(cornerRadius: 4)
                .fill(Color(nsColor: .quaternaryLabelColor).opacity(0.3))
        )
    }
}

// MARK: - Embedded note body with recursive nested embeds

/// Body renderer for FullNote and Section variants. Renders the
/// embedded text, splicing recursive `EmbedView`s in at each
/// nested embed's byte offset. Stays in plain `Text` for the V1
/// rendering — Markdown rendering pipelines (headings rotor, etc.)
/// land alongside Milestone K's content pipelines.
///
/// The nested-embed splicing partitions the parent text by
/// `byte_offset_in_parent`, drops the embed's literal source span,
/// and re-renders the recursive `EmbedView` between the
/// surrounding text spans.
private struct EmbeddedNoteBody: View {
    let text: String
    let nested: [NestedEmbed]
    let jumpToSourceAction: ((String) -> Void)?
    let depth: Int

    var body: some View {
        let segments = splice(text: text, nested: nested)
        VStack(alignment: .leading, spacing: 6) {
            ForEach(Array(segments.enumerated()), id: \.offset) { _, segment in
                switch segment {
                case .text(let s):
                    if !s.isEmpty {
                        Text(verbatim: s)
                            .font(.body)
                            .textSelection(.enabled)
                            .frame(maxWidth: .infinity, alignment: .leading)
                    }
                case .embed(let nested):
                    EmbedView(
                        resolution: nested.resolution,
                        jumpToSourceAction: jumpToSourceAction,
                        depth: depth
                    )
                }
            }
        }
    }
}

/// One slice of an embedded note's body: either a literal text
/// span or one nested embed (rendered as a recursive `EmbedView`).
enum EmbedBodySegment {
    case text(String)
    case embed(NestedEmbed)
}

/// Partition `text` by `nested`'s byte offsets, dropping the
/// `![[…]]` literal each embed's offset points at. Returns an
/// alternating sequence of `.text` and `.embed` segments in source
/// order.
///
/// The dropped span length is `5 + raw_target.utf8.count` —
/// `![[` (3 bytes) + target + `]]` (2 bytes) for the wikilink
/// embed form. Audit #199: earlier shape used `4 + …` which left a
/// stray `]` in the trailing text that VoiceOver would read as
/// "right bracket". For Markdown-image embeds (`![alt](src)`) the
/// length is variable; until the backend reports a real span_end
/// the approximation may leave a small remainder for those —
/// documented limitation, mostly invisible because Markdown-image
/// embeds inside an embedded note are rare.
func splice(text: String, nested: [NestedEmbed]) -> [EmbedBodySegment] {
    if nested.isEmpty {
        return [.text(text)]
    }
    let sorted = nested.sorted { $0.byteOffsetInParent < $1.byteOffsetInParent }
    var out: [EmbedBodySegment] = []
    let bytes = text.utf8
    var cursor: Int = 0
    let totalLen = bytes.count
    for ne in sorted {
        let offset = Int(ne.byteOffsetInParent)
        guard offset >= cursor, offset <= totalLen else { continue }
        if offset > cursor {
            let leading = substringByByteRange(text: text, start: cursor, end: offset)
            out.append(.text(leading))
        }
        // Wikilink embed span: `![[target]]` = 5 + target.utf8.count.
        let approxLen = 5 + ne.rawTarget.utf8.count
        let consumed = min(offset + approxLen, totalLen)
        out.append(.embed(ne))
        cursor = consumed
    }
    if cursor < totalLen {
        let trailing = substringByByteRange(text: text, start: cursor, end: totalLen)
        out.append(.text(trailing))
    }
    return out
}

/// UTF-8 byte-range substring with String.Index conversion.
/// Falls back to the entire string if the byte boundaries don't
/// land on UTF-8 character boundaries (defensive — the resolver
/// should always hand us valid offsets).
private func substringByByteRange(text: String, start: Int, end: Int) -> String {
    let utf8 = text.utf8
    guard start <= end, end <= utf8.count else { return text }
    let startIdx = utf8.index(utf8.startIndex, offsetBy: start)
    let endIdx = utf8.index(utf8.startIndex, offsetBy: end)
    guard let startStr = startIdx.samePosition(in: text),
        let endStr = endIdx.samePosition(in: text)
    else {
        return String(decoding: Array(utf8[startIdx..<endIdx]), as: UTF8.self)
    }
    return String(text[startStr..<endStr])
}
