// Swift smoke-test client for yana-uniffi.
//
// Three modes:
//   (no args)        — Parse an embedded Markdown sample and print headings.
//   <path-to-file>   — Read that file via the Rust core, print headings.
//   --vault <path>   — Open the directory as a vault, scan, list files
//                      via VaultSession (Milestone A demo).

import Foundation

let sample = """
# Hello, YANA

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
            let message: String
            switch error {
            case .Io(let m), .Db(let m), .Trash(let m):
                message = m
            case .InvalidPath(let path, let reason):
                message = "invalid path \(path): \(reason)"
            case .Cancelled:
                message = "operation cancelled"
            }
            FileHandle.standardError.write(Data("error: \(message)\n".utf8))
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

        let scanReport = try session.scanInitial()
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
        let message: String
        switch error {
        case .Io(let m), .Db(let m), .Trash(let m):
            message = m
        case .InvalidPath(let path, let reason):
            message = "invalid path \(path): \(reason)"
        case .Cancelled:
            message = "operation cancelled"
        }
        FileHandle.standardError.write(Data("error: \(message)\n".utf8))
        exit(1)
    } catch {
        FileHandle.standardError.write(Data("error: \(error)\n".utf8))
        exit(1)
    }
}

run()
