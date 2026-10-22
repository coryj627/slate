# Windows native long-path ingress: diagnosis and verification

Date: 2026-09-30. Scope: the confirmed `CanvasMediaPolicy` media-query and `AnchoredVaultStore` metadata-open defects on the local Windows VM. No OS policy, test privilege, or security gate was changed.

## Finding

The existing `MediaSeventyDirectoriesDeepStillOpens` regression failed because the initial native `CreateFileW` call received an ordinary path longer than `MAX_PATH`. Managed IO had created the file successfully. On this VM, `LongPathsEnabled` is `0`. The application has a `longPathAware` manifest, but Microsoft documents that ordinary-path opt-in requires both the process manifest and machine setting. Test hosts also have their own manifests. Explicit extended paths avoid that dependency. See Microsoft's [maximum file path limitation](https://learn.microsoft.com/en-us/windows/win32/fileio/maximum-file-path-limitation) and [CreateFileW documentation](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createfilew).

A native probe compared ordinary and extended spellings of the exact same managed-created objects. It used only private temporary fixtures and query/read handles; it did not launch an external application. A separate assembly snapshot allowed the anchored-store probe to run without locking a shared build output.

| Witness | Before | After |
| --- | --- | --- |
| Canvas media, ordinary 371-character leaf | Invalid query handle, `ERROR_PATH_NOT_FOUND` (3), no `FileIdInfo`, full policy returned null | Valid handle, `FileIdInfo` succeeds; full policy returns the resolved 375-character extended path |
| Same Canvas leaf with an explicit extended spelling | Valid handle and identity | Same identity as the ordinary spelling |
| Anchored store, ordinary 369-character root that managed IO confirms exists | `DirectoryNotFoundException: Vault root does not exist.` | Opens and reads the expected bytes `1,2,3` |
| Same anchored root with an explicit extended spelling | Opens and reads `1,2,3` | Opens and reads `1,2,3` |
| Workspace persistence, ordinary 240-character root | Fails during temporary-child creation with `FileNotFoundException` | Initial save/load and replacement/load succeed; no temporary file leaks |
| Workspace persistence, ordinary 360-character root | Fails opening the root with `DirectoryNotFoundException` | Initial save/load and replacement/load succeed; no temporary file leaks |

The ordinary/extended comparison separates the native path spelling from identity, containment, and reparse policy. The two parameterized persistence cases distinguish root-directory ingress from child-file ingress. Both regression cases failed before the anchored source fix and passed afterward.

## Change

`WindowsNativePath.ForCreateFile` is shared by Canvas's `OpenForQuery` and the anchored store's directory and child native opens. It adds the extended namespace only for ordinary fully qualified paths at or above `MAX_PATH`, canonicalizing before prefixing. It uses the extended UNC spelling for UNC paths. Short paths and existing device/extended spellings retain their previous behavior.

The change preserves the native access/share/open flags, no-delete-share anchor handles, simple child-name validation, reparse refusal, resolved-path checks, handle-relative atomic rename, Canvas's 128-bit `FileIdInfo` identity, and fail-closed containment/revalidation. There is no shortened traversal, old 64-bit identity fallback, or global long-path opt-in. The existing 70-directory media fact remains intact; a second Canvas fact covers a root that itself exceeds `MAX_PATH`.

Files:

- `apps/slate-windows/src/SlateWindows/WindowsNativePath.cs`
- `apps/slate-windows/src/SlateWindows/ExternalLinkPolicy.cs`
- `apps/slate-windows/src/SlateWindows/AnchoredVaultStore.cs`
- `apps/slate-windows/tests/SlateWindows.Tests/CanvasDocumentTests.cs`
- `apps/slate-windows/tests/SlateWindows.Tests/W1AnchoredVaultStoreTests.cs`

## Verification

All commands below ran from `C:/dev/slate/apps/slate-windows`, using the current Release build and .NET 10.0.12. Native test runs used desktop/COM access. Source was frozen after the scoped formatter and before the final green build/runs.

```powershell
dotnet build SlateWindows.slnx -c Release --no-restore

dotnet test tests/SlateWindows.Tests/SlateWindows.Tests.csproj -c Release --no-build --filter FullyQualifiedName~WorkspaceMetadataReadAndReplaceWorkWithAnOrdinaryLongVaultRoot --logger "console;verbosity=detailed"

dotnet test tests/SlateWindows.Tests/SlateWindows.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~WorkspaceMetadataReadAndReplaceWorkWithAnOrdinaryLongVaultRoot|FullyQualifiedName~MediaSeventyDirectoriesDeepStillOpens|FullyQualifiedName~MediaOpensWhenTheVaultRootItselfRequiresAnExtendedPath" --logger "console;verbosity=detailed"

dotnet test tests/SlateWindows.Tests/SlateWindows.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~CanvasDocumentTests&FullyQualifiedName!~AnInVaultSymlinkPointingOutsideTheVaultIsRefused"

dotnet test tests/SlateWindows.Tests/SlateWindows.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~W1AnchoredVaultStoreTests&FullyQualifiedName!~WorkspaceReadAndReplaceDoNotFollowAnExternalFileReparsePoint&FullyQualifiedName!~SidebarReadAndUpdateFailClosedOnAnExternalFileReparsePoint&FullyQualifiedName!~LegacyRecentsMigrationDoesNotReadOrDeleteAnExternalFileReparsePoint"
```

Results:

- Release build: passed, zero warnings/errors.
- Parameterized anchored regression before fix: 2/2 failed at the two distinct native boundaries; exit code 1.
- Focused media/metadata regressions after fix: 4/4 passed, 0.9160 seconds.
- Canvas class with the single local-privilege exclusion: 173/173 passed, 28.0606 seconds. Included identity-query failure, 128-bit identity, adjacent-directory containment, junction chains, junction-rooted vault, trailing-dot spelling, volume-GUID resolution, and TOCTOU revalidation witnesses.
- Anchored class with the three local-privilege exclusions: 10/10 passed, 0.8777 seconds. Included bounded serialization, denied replacement/cleanup, committed replacement after an injected failure, directory-swap handling, ancestor-swap handling, and both pointer-size rename-layout witnesses.
- Both native probes pass after the fix; query/read identities and bytes agree across ordinary and extended spellings.
- Scoped `dotnet format whitespace` and `git diff --check`: passed.

TRX artifacts are under `C:/Users/cory/AppData/Local/Temp/slate-windows-ci-2026-09-30/native-path/`: `anchored-long-root-red.trx`, `native-long-path-green.trx`, `canvas-native-path-final.trx`, and `anchored-native-path-final.trx`. The temporary probe scripts are `slate-media-probe-2026-09-30.ps1` and `slate-anchored-probe-2026-09-30.ps1` in the same user's Temp directory; all private probe fixture directories were cleaned after their handles/assembly snapshot were released.

## Limits and testing implications

The four excluded facts require creating a file symlink, which this VM cannot do with its current privileges. Their source and assertions remain unchanged. The green filtered suites do not establish those four adversarial behaviors locally, and no Developer Mode or privilege setting was changed. Existing swap tests that tolerate the OS refusing a swap remain included; their passing result does not imply that every swap succeeded on this VM.

Live long-path probes used local filesystem paths. The new metadata witness covers `WorkspacePersistence` through `AnchoredVaultStore`; it does not validate every app subsystem with a long root, a live UNC share, or ReFS. Preservation of `FileIdInfo` means the fix retains the existing identity contract, without claiming a new filesystem certification.

This was a real production portability defect exposed by the VM's capability profile. A successful run on another runner does not establish its long-path policy or certify the policy-off case. Windows pilot records should capture process/runtime, long-path policy, and symlink capability, and retain a policy-off long-path witness alongside a runner capable of the symlink adversarial facts. This finding strengthens environment coverage; it does not justify weakening the existing security witnesses or tightening timing gates from one local run.
