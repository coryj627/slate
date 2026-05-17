import SwiftUI

// Entry point for the smoke-test SwiftUI app.
//
// The window shows headings parsed from either an embedded sample or a
// Markdown file the user opens via a file picker. The parsing itself
// happens inside the Rust core via the uniffi-rs bindings — there's
// no Markdown parser anywhere on the Swift side.

@main
struct YanaMacApp: App {
    var body: some Scene {
        WindowGroup("YANA Smoke Test") {
            ContentView()
                .frame(minWidth: 480, minHeight: 360)
        }
    }
}
