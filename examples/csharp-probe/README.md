# csharp-probe — W0-1 binding spike probe

Runtime probe for the Windows C# binding decision
([#714](https://github.com/coryj627/slate/issues/714), spec
`docs/plans/18_windows_port/specs/w0_spec.md` §W0-1). It binds the spec's
**fixed probe surface** — not the whole API — and exercises it end-to-end
against a real `slate_uniffi` dynamic library:

- `VaultSession` open / drop (handle lifetime)
- `scan_initial_with_progress` with a foreign `ScanProgressListener`
  (callbacks from a Rust thread)
- a `VaultEventListener` subscription receiving all three event kinds
  (`on_error` / `on_file_change` / `on_index_phase`) across an operation
- `CancelToken` cancellation mid-scan (latency measured)
- `CommandRegistry` + foreign `CommandAction` invocation round-trips,
  success **and** error
- typed `VaultError` mapping
- `DocumentBuffer` create → `apply_edit` → read-back (the keystroke hot path)
- §W-E stress patterns: GC pressure on handles, callback concurrency across
  all three traits, listener registration/unregistration lifetime

Parking discipline (program §Entry criteria): this directory is the spike's
probe, **not** the start of `apps/slate-windows/` — no WPF, no app scaffold.
The probe survives as the seed of W0-3's smoke tests.

## Layout

```
csharp-probe/
├── generate.ps1     # builds slate_uniffi + generates C# bindings into generated/
├── SlateProbe.csproj
├── Program.cs       # probe runner: numbered sections, PASS/FAIL, non-zero exit on failure
├── generated/       # uniffi-bindgen-cs output + native DLL (git-ignored, regenerated)
└── shim/            # csbindgen counter-candidate (C-ABI shim crate + generated P/Invoke)
```

## Running (Windows)

Prereqs: Rust toolchain (repo-pinned version), .NET 10 SDK,
`uniffi-bindgen-cs` matching the workspace's uniffi version:

```powershell
cargo install uniffi-bindgen-cs --git https://github.com/NordSecurity/uniffi-bindgen-cs --tag v0.11.0+v0.31.0
```

Then, from this directory:

```powershell
./generate.ps1          # cargo build -p slate-uniffi + bindgen into generated/
dotnet run              # runs every probe section; exit 0 = all pass
```

`generate.ps1` respects `CARGO_TARGET_DIR`; on dev machines building over a
network share, set it to a local-disk path first.
