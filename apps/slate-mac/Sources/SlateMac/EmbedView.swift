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
/// a malformed resolver response can't blow the stack.
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
            depthLimitView
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
            jumpToTarget: targetPath
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
            jumpToTarget: targetPath
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
            jumpToTarget: targetPath
        ) {
            Text(text)
                .font(.body)
                .textSelection(.enabled)
                .frame(maxWidth: .infinity, alignment: .leading)
                .padding(.vertical, 4)
                .accessibilityLabel(
                    "Block \(blockId) content: \(text)"
                )
        }
    }

    private func imageView(
        targetPath: String,
        bytes: Data,
        mime: String,
        alt: String?
    ) -> some View {
        let label = alt.map { "Embedded image: \($0)" }
            ?? "Embedded image: \((targetPath as NSString).lastPathComponent)"
        return Group {
            if let nsImage = NSImage(data: bytes) {
                Image(nsImage: nsImage)
                    .resizable()
                    .scaledToFit()
                    .frame(maxWidth: .infinity)
                    .accessibilityLabel(label)
                    .help(label)
            } else {
                imageDecodeFailureView(label: label, mime: mime)
            }
        }
    }

    private func imageDecodeFailureView(label: String, mime: String) -> some View {
        // Decode failure (corrupt file, unsupported codec) — surface
        // the cue so the user knows why no image rendered. Same AT
        // label as the success path so a screen-reader user
        // navigating an embed-heavy note doesn't lose the per-image
        // anchor.
        let message =
            "Could not decode image. MIME: \(mime). The file may be corrupt or an unsupported codec."
        return VStack(alignment: .leading, spacing: 4) {
            Text(message)
                .font(.caption)
                .foregroundStyle(.red)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(.vertical, 4)
        .accessibilityElement(children: .combine)
        .accessibilityLabel("\(label). \(message)")
    }

    private func unresolvedView(reason: EmbedUnresolvedReason) -> some View {
        let (visible, axLabel) = unresolvedText(reason: reason)
        return Text(visible)
            .font(.callout)
            .foregroundStyle(.red)
            .padding(.vertical, 4)
            .accessibilityLabel(axLabel)
    }

    private var depthLimitView: some View {
        // Defense-in-depth fallback when the depth counter trips
        // even though the backend's `MAX_EMBED_DEPTH` should have
        // already stopped us. WCAG 2.5.3: AX label begins with the
        // visible text.
        let msg = "Embed depth limit reached."
        return Text(msg)
            .font(.callout)
            .foregroundStyle(.secondary)
            .padding(.vertical, 4)
            .accessibilityLabel("\(msg) Further embeds inside this one are not rendered.")
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
/// the FullNote / Section / Block variants. Splitting it out keeps
/// the per-variant AT label / activation behavior in one spot.
private struct EmbedDisclosure<Content: View>: View {
    let label: String
    let jumpToSourceAction: ((String) -> Void)?
    let jumpToTarget: String?
    let content: () -> Content

    @State private var expanded: Bool = true

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
            Text(label)
                .font(.callout.weight(.semibold))
                .accessibilityAddTraits(.isHeader)
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
                        Text(s)
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
/// Length of the dropped span is the embed's `raw_target` plus
/// the surrounding `![[…]]` (4 bytes) — close enough for the V1
/// splice. A more precise length needs the backend to surface the
/// span end alongside the start; tracked as a follow-up if the
/// approximation produces visible artifacts.
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
        // Best-effort span length: `![[target]]` = 4 + target.utf8.count.
        let approxLen = 4 + ne.rawTarget.utf8.count
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
