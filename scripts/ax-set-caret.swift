// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// ax-set-caret — park the insertion point in a running app's text
// area via the raw Accessibility API.
//
// Why this exists: System Events' AppleScript bridge CANNOT set
// AXSelectedTextRange — `set value of attribute "AXSelectedTextRange"
// of … to {loc, 0}` silently no-ops (the list never marshals into an
// AXValue CFRange), and its read-back is equally unreliable. The
// 2026-06-10 VO feature test parked carets that way, the caret never
// moved, and Cmd+E truthfully reported "No embed at cursor" — filed
// as #412 against an app path that was working all along. Raw
// AXUIElementSetAttributeValue works (the raw AX interface assistive
// clients drive), so harness caret-parking MUST go through this
// helper.
//
// Usage:
//   swift scripts/ax-set-caret.swift <AppName> <substring> [delta]
//
// Scans the app's windows (front to back) for the first AXTextArea
// whose value contains <substring>, sets the caret to (substring
// UTF-16 offset + delta, length 0), then prints the INDEPENDENTLY
// READ-BACK caret position. Exit 0 only if the read-back matches the
// requested offset.
//
// Requires Accessibility permission for the invoking terminal.

import ApplicationServices
import AppKit

func findTextArea(_ el: AXUIElement, containing needle: String, depth: Int = 0) -> AXUIElement? {
    if depth > 14 { return nil }
    var roleRef: CFTypeRef?
    AXUIElementCopyAttributeValue(el, kAXRoleAttribute as CFString, &roleRef)
    if let role = roleRef as? String, role == "AXTextArea" {
        var valRef: CFTypeRef?
        AXUIElementCopyAttributeValue(el, kAXValueAttribute as CFString, &valRef)
        if let v = valRef as? String, v.contains(needle) { return el }
    }
    var kidsRef: CFTypeRef?
    AXUIElementCopyAttributeValue(el, kAXChildrenAttribute as CFString, &kidsRef)
    if let kids = kidsRef as? [AXUIElement] {
        for k in kids {
            if let found = findTextArea(k, containing: needle, depth: depth + 1) { return found }
        }
    }
    return nil
}

// Fail fast when the invoking process lacks Accessibility trust —
// untrusted AX calls silently return empty data, which would
// otherwise surface as a misleading "no windows" (Codoki PR #425).
if !AXIsProcessTrusted() {
    FileHandle.standardError.write(
        Data(
            "Accessibility access not granted; enable it for the invoking terminal and retry.\n"
                .utf8))
    exit(1)
}

let args = CommandLine.arguments
guard args.count >= 3 else {
    FileHandle.standardError.write(Data("usage: ax-set-caret <AppName> <substring> [delta]\n".utf8))
    exit(2)
}
let appName = args[1]
let needle = args[2]
let delta = args.count >= 4 ? Int(args[3]) ?? 0 : 0

guard let app = NSWorkspace.shared.runningApplications.first(where: { $0.localizedName == appName })
else {
    FileHandle.standardError.write(Data("app not running: \(appName)\n".utf8))
    exit(1)
}
let axApp = AXUIElementCreateApplication(app.processIdentifier)
var winRef: CFTypeRef?
AXUIElementCopyAttributeValue(axApp, kAXWindowsAttribute as CFString, &winRef)
guard let wins = winRef as? [AXUIElement] else {
    FileHandle.standardError.write(Data("no windows\n".utf8))
    exit(1)
}
var textArea: AXUIElement?
for w in wins {
    if let ta = findTextArea(w, containing: needle) {
        textArea = ta
        break
    }
}
guard let ta = textArea else {
    FileHandle.standardError.write(Data("no text area containing the substring\n".utf8))
    exit(1)
}

var valRef: CFTypeRef?
AXUIElementCopyAttributeValue(ta, kAXValueAttribute as CFString, &valRef)
guard let value = valRef as? String else {
    FileHandle.standardError.write(Data("text area value unreadable on re-read\n".utf8))
    exit(1)
}
let ns = value as NSString
let found = ns.range(of: needle)
guard found.location != NSNotFound else {
    FileHandle.standardError.write(Data("substring vanished between find and re-read\n".utf8))
    exit(1)
}
let target = min(max(0, found.location + delta), ns.length)
var range = CFRange(location: target, length: 0)
guard let axVal = AXValueCreate(.cfRange, &range) else { exit(1) }
let setErr = AXUIElementSetAttributeValue(ta, kAXSelectedTextRangeAttribute as CFString, axVal)
usleep(200_000)

var afterRef: CFTypeRef?
AXUIElementCopyAttributeValue(ta, kAXSelectedTextRangeAttribute as CFString, &afterRef)
var got = CFRange(location: -1, length: -1)
if let a = afterRef, CFGetTypeID(a) == AXValueGetTypeID() {
    AXValueGetValue(a as! AXValue, .cfRange, &got)
}
print("requested=\(target) set_err=\(setErr.rawValue) readback=\(got.location),\(got.length)")
exit(got.location == target && got.length == 0 && setErr == .success ? 0 : 1)
