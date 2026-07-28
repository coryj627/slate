// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

/// Live-source coherence for diagram artifact matching (W3-3, the
/// rule both twins share): the reading block is parsed from the LIVE
/// buffer, the artifact from the SAVED file, and a same-position
/// unsaved edit keeps byte containment — so containment alone can
/// select a stale artifact, displaying the old SVG and speaking its
/// old structured description. The artifact applies only when its
/// LF-normalized source equals the live fence interior (diagram
/// fences are code-shaped; the code-fence normalization precedent,
/// not math's delimiter stripping).
enum ReadingDiagramCoherence {
    static func isCoherent(artifactSource: String, interior: String) -> Bool {
        let normalized = artifactSource.replacingOccurrences(of: "\r\n", with: "\n")
        return normalized == interior
            || normalized == interior + "\n"
            || normalized + "\n" == interior
    }
}
