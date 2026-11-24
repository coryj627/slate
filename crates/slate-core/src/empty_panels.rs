// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! The line a right-pane panel shows while a host has no body for it (plan 47 CB-23, #1800,
//! CD15). Windows offers Math, Code and Diagrams before it has built them, and a reader meets
//! this line under the panel's title: what the panel will show, and where that is read today.
//! The words are core's, as every accessible sentence is (the owner's answer to CD15); the
//! line varies with where the reader is, and names the reading view's shortcut, which the host
//! hands in from its own chord table (program decision 12: the chord table is host-owned). The
//! Mac has a body for each of these panels and shows none of these lines.

/// A right-pane panel a host offers before it has a body for it.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum EmptyPanel {
    Math,
    Code,
    Diagrams,
}

/// Where the reader is while the panel is open, which decides where the line points them.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum EmptyPanelContext {
    /// No note is in view: no tab, or a tab that is not a note.
    NoNote,
    /// A note is in view in its editor.
    NoteInEditor,
    /// A note is in view in its reading view.
    NoteInReadingView,
}

/// The panel's subject as the line opens with it, its verb, and what a note holds of it.
fn words(panel: EmptyPanel) -> (&'static str, &'static str, &'static str) {
    match panel {
        EmptyPanel::Math => ("Math", "isn't", "math"),
        EmptyPanel::Code => ("Code blocks", "aren't", "code blocks"),
        EmptyPanel::Diagrams => ("Diagrams", "aren't", "diagrams"),
    }
}

/// What the panel will show and where that is today. `reading_view_chord` is the host's
/// shortcut for the reading view, in the form the line is shown or spoken in ("Ctrl+Shift+E",
/// or a screen reader's "Control Shift E"); a blank one is left out of the line.
pub fn empty_panel_line(
    panel: EmptyPanel,
    context: EmptyPanelContext,
    reading_view_chord: &str,
) -> String {
    let (subject, verb, held) = words(panel);
    let chord = reading_view_chord.trim();
    let shortcut = if chord.is_empty() {
        String::new()
    } else {
        format!(" ({chord})")
    };
    match context {
        EmptyPanelContext::NoteInEditor => format!(
            "{subject} for this note {verb} listed here yet. Open the note in the reading view{shortcut} to read its {held}."
        ),
        EmptyPanelContext::NoteInReadingView => format!(
            "{subject} for this note {verb} listed here yet. Read its {held} in the reading view, which is open now."
        ),
        EmptyPanelContext::NoNote => format!(
            "{subject} {verb} listed here yet. Open a note, then the reading view{shortcut}, to read its {held}."
        ),
    }
}

#[cfg(test)]
mod tests {
    use super::{EmptyPanel, EmptyPanelContext, empty_panel_line};

    const CHORD: &str = "Ctrl+Shift+E";

    /// The nine lines the owner read, word for word (CD15; the owner's answers of
    /// 2026-11-23: "Add the shortcut", "Vary by state").
    #[test]
    fn each_panel_without_a_body_has_the_owners_line_in_each_context() {
        let expected = [
            (
                EmptyPanel::Math,
                EmptyPanelContext::NoteInEditor,
                "Math for this note isn't listed here yet. Open the note in the reading view (Ctrl+Shift+E) to read its math.",
            ),
            (
                EmptyPanel::Math,
                EmptyPanelContext::NoteInReadingView,
                "Math for this note isn't listed here yet. Read its math in the reading view, which is open now.",
            ),
            (
                EmptyPanel::Math,
                EmptyPanelContext::NoNote,
                "Math isn't listed here yet. Open a note, then the reading view (Ctrl+Shift+E), to read its math.",
            ),
            (
                EmptyPanel::Code,
                EmptyPanelContext::NoteInEditor,
                "Code blocks for this note aren't listed here yet. Open the note in the reading view (Ctrl+Shift+E) to read its code blocks.",
            ),
            (
                EmptyPanel::Code,
                EmptyPanelContext::NoteInReadingView,
                "Code blocks for this note aren't listed here yet. Read its code blocks in the reading view, which is open now.",
            ),
            (
                EmptyPanel::Code,
                EmptyPanelContext::NoNote,
                "Code blocks aren't listed here yet. Open a note, then the reading view (Ctrl+Shift+E), to read its code blocks.",
            ),
            (
                EmptyPanel::Diagrams,
                EmptyPanelContext::NoteInEditor,
                "Diagrams for this note aren't listed here yet. Open the note in the reading view (Ctrl+Shift+E) to read its diagrams.",
            ),
            (
                EmptyPanel::Diagrams,
                EmptyPanelContext::NoteInReadingView,
                "Diagrams for this note aren't listed here yet. Read its diagrams in the reading view, which is open now.",
            ),
            (
                EmptyPanel::Diagrams,
                EmptyPanelContext::NoNote,
                "Diagrams aren't listed here yet. Open a note, then the reading view (Ctrl+Shift+E), to read its diagrams.",
            ),
        ];
        for (panel, context, line) in expected {
            assert_eq!(
                empty_panel_line(panel, context, CHORD),
                line,
                "{panel:?} {context:?}"
            );
        }
    }

    /// The shortcut is the host's, in whatever form it hands in: the spoken form for a
    /// screen reader's name reads in the same place.
    #[test]
    fn the_shortcut_is_the_hosts_in_the_form_it_hands_in() {
        assert_eq!(
            empty_panel_line(
                EmptyPanel::Math,
                EmptyPanelContext::NoteInEditor,
                "Control Shift E"
            ),
            "Math for this note isn't listed here yet. Open the note in the reading view (Control Shift E) to read its math."
        );
        assert_eq!(
            empty_panel_line(EmptyPanel::Diagrams, EmptyPanelContext::NoNote, "⇧⌘E"),
            "Diagrams aren't listed here yet. Open a note, then the reading view (⇧⌘E), to read its diagrams."
        );
    }

    /// A host with no shortcut for the reading view leaves the brackets out rather than
    /// showing empty ones.
    #[test]
    fn a_blank_shortcut_is_left_out() {
        assert_eq!(
            empty_panel_line(EmptyPanel::Math, EmptyPanelContext::NoteInEditor, "  "),
            "Math for this note isn't listed here yet. Open the note in the reading view to read its math."
        );
        assert_eq!(
            empty_panel_line(EmptyPanel::Code, EmptyPanelContext::NoNote, ""),
            "Code blocks aren't listed here yet. Open a note, then the reading view, to read its code blocks."
        );
    }

    /// The reading view's own line names no shortcut, whatever the host hands in: there
    /// the shortcut would leave the view the line points to.
    #[test]
    fn the_reading_views_line_names_no_shortcut() {
        for panel in [EmptyPanel::Math, EmptyPanel::Code, EmptyPanel::Diagrams] {
            let line = empty_panel_line(panel, EmptyPanelContext::NoteInReadingView, CHORD);
            assert!(!line.contains(CHORD), "{line}");
            assert!(
                line.ends_with("in the reading view, which is open now."),
                "{line}"
            );
        }
    }
}
