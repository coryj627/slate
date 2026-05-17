// Swift smoke-test client for yana-uniffi.
//
// Validates that the Rust core can be called from Swift via the
// uniffi-rs-generated bindings. Reads a path argument if given (or uses an
// embedded sample), parses it as Markdown, and prints the extracted headings.

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

run()
