// Swift smoke-test client for slate-uniffi.
//
// Three modes:
//   (no args)        — Parse an embedded Markdown sample and print headings.
//   <path-to-file>   — Read that file via the Rust core, print headings.
//   --vault <path>   — Open the directory as a vault, scan, list files
//                      via VaultSession (Milestone A demo).

import Foundation

let sample = """
# Hello, Slate

A paragraph.

## A subheading

With `inline code` in it.

### Deeper still

End of sample.
"""

func run() {
    let arguments = CommandLine.arguments

    if arguments.count > 2, arguments[1] == "--vault" {
        runVaultDemo(rootPath: arguments[2])
        return
    }

    if arguments.count > 1 {
        let path = arguments[1]
        print("Reading headings from: \(path)")
        do {
            let headings = try readHeadings(path: path)
            printHeadings(headings)
        } catch let error as VaultError {
            FileHandle.standardError.write(Data("error: \(describe(error))\n".utf8))
            exit(1)
        } catch {
            FileHandle.standardError.write(Data("error: \(error)\n".utf8))
            exit(1)
        }
    } else {
        print("Extracting headings from embedded sample (pass a path to read a file):")
        let headings = extractHeadings(source: sample)
        printHeadings(headings)
    }
}

/// Render a `VaultError` as a single-line message. Centralising the
/// switch here keeps the FFI enum exhaustively covered in one place
/// — adding a new variant to the Rust side will fail to compile
/// here, not silently fall through a `default` clause.
func describe(_ error: VaultError) -> String {
    switch error {
    case .Io(let m), .Db(let m), .Trash(let m), .InvalidQuery(let m),
        .InvalidArgument(let m):
        return m
    case .InvalidPath(let path, let reason):
        return "invalid path \(path): \(reason)"
    case .Cancelled:
        return "operation cancelled"
    case .InvalidUtf8(let path):
        return "file at \(path) is not valid UTF-8"
    case .FileTooLarge(let path, let size):
        return "file at \(path) is \(size) bytes, larger than the configured refuse threshold"
    case .Unsupported(let feature):
        return "operation not supported yet: \(feature)"
    case .WriteConflict(let current, let expected, _):
        return
            "write conflict: file has been modified since it was read (expected \(expected), current \(current))"
    case .MalformedFrontmatter(let path, let reason):
        return "frontmatter at \(path) is malformed: \(reason)"
    }
}

func printHeadings(_ headings: [Heading]) {
    print("Got \(headings.count) heading\(headings.count == 1 ? "" : "s"):")
    for heading in headings {
        let prefix = String(repeating: "#", count: Int(heading.level))
        print("  \(prefix) \(heading.text)")
    }
}

func runVaultDemo(rootPath: String) {
    print("Opening vault at: \(rootPath)")
    do {
        let session = try VaultSession.openFilesystem(rootPath: rootPath)
        print("Vault opened. Scanning…")

        // CancelToken is constructed up-front so a real UI could hand
        // the same instance to a Cancel button. The CLI never cancels,
        // but exercising the parameter keeps the FFI shape honest.
        let cancel = CancelToken()
        let scanReport = try session.scanInitial(cancel: cancel)
        print(
            "Scan complete: \(scanReport.filesIndexed) files indexed, "
                + "\(scanReport.bytesProcessed) bytes processed, "
                + "\(scanReport.errors.count) errors."
        )
        for err in scanReport.errors {
            print("  warn: \(err)")
        }

        let paging = Paging(cursor: nil, limit: 20)
        let page = try session.listFiles(filter: .markdownOnly, paging: paging)
        print(
            "Markdown files (\(page.items.count) of \(page.totalFiltered) shown):"
        )
        for file in page.items {
            print("  \(file.path) — \(file.sizeBytes) bytes")
        }
        if page.nextCursor != nil {
            print("  … more pages available")
        }
    } catch let error as VaultError {
        FileHandle.standardError.write(Data("error: \(describe(error))\n".utf8))
        exit(1)
    } catch {
        FileHandle.standardError.write(Data("error: \(error)\n".utf8))
        exit(1)
    }
}

run()
