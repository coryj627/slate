// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Milestone M sync detection (M-1, #532).
//!
//! Detects external sync systems managing a vault — LiveSync, iCloud
//! Drive, Dropbox, OneDrive, Google Drive, Git, Syncthing — from their
//! on-disk markers. Detection is filesystem-probe based, not index
//! based: the scanner deliberately skips dot-prefixed entries, so
//! markers like `.stfolder` or `.icloud` placeholders never reach the
//! SQLite index (plan decision #5, `docs/plans/09_sync_cli/00_plan.md`).
//!
//! `detect_sync_providers` is a pure function over a vault root: no
//! SQLite, no session state, no unbounded walks — every probe is an
//! exact path, a bounded `ancestors()` walk, or a single `read_dir` of
//! the root. The detector-evidence table in
//! `docs/plans/09_sync_cli/m_spec.md` §M-1 is normative for probe
//! rules, risk levels, and recommendation copy.

use std::path::{Component, Path, PathBuf};

// --- Public types (mirrored over uniffi) -----------------------------

/// The sync systems the detector knows about, in the normative table
/// order (which is also the report's provider order).
#[derive(Debug, Clone, Copy, PartialEq, Eq, PartialOrd, Ord, Hash)]
pub enum SyncProviderKind {
    LiveSync,
    ICloudDrive,
    Dropbox,
    OneDrive,
    GoogleDrive,
    Git,
    Syncthing,
}

impl SyncProviderKind {
    /// Normative display name — used in `audio_summary`, human CLI
    /// output, and the diagnostics-panel row labels.
    pub fn display_name(&self) -> &'static str {
        match self {
            Self::LiveSync => "LiveSync",
            Self::ICloudDrive => "iCloud Drive",
            Self::Dropbox => "Dropbox",
            Self::OneDrive => "OneDrive",
            Self::GoogleDrive => "Google Drive",
            Self::Git => "Git",
            Self::Syncthing => "Syncthing",
        }
    }
}

/// How much risk the detected system carries for a vault Slate is
/// writing into. Ordering matters: the multi-sync warning counts
/// providers with `risk_level >= Medium`.
#[derive(Debug, Clone, Copy, PartialEq, Eq, PartialOrd, Ord, Hash)]
pub enum RiskLevel {
    Low,
    Medium,
    High,
}

/// One detected sync system plus the markers that produced the
/// detection and the user-facing recommendation copy.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct DetectedSyncProvider {
    pub kind: SyncProviderKind,
    /// Markers that produced the detection. Vault-relative when the
    /// marker is inside the vault; absolute when it is an
    /// ancestor/location signal.
    pub evidence_paths: Vec<String>,
    pub risk_level: RiskLevel,
    /// Full recommendation sentence(s) — the exact user-facing copy
    /// from the normative table.
    pub recommendation: String,
}

/// The full detection result for one vault root.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct SyncDetectionReport {
    /// Detected providers in detector-table order — deterministic for
    /// tests and TSV consumers.
    pub providers: Vec<DetectedSyncProvider>,
    /// `Some(copy)` when ≥ 2 providers with risk ≥ Medium are detected.
    /// Git is Low-risk and therefore never triggers (or appears in)
    /// the warning, though it still appears in `providers`.
    pub multi_sync_warning: Option<String>,
    /// Pre-rendered VoiceOver summary, same pattern as
    /// `QueryResultSet::summary`.
    pub audio_summary: String,
    /// `false` when the session has no filesystem root
    /// (provider-abstracted session): detection unsupported,
    /// `providers` empty.
    pub supported: bool,
}

impl SyncDetectionReport {
    /// Report for a session with no filesystem root. Not an error —
    /// the host renders from the `supported` flag.
    pub(crate) fn unsupported() -> Self {
        Self {
            providers: Vec::new(),
            multi_sync_warning: None,
            audio_summary: "Sync detection isn't available for this vault type.".to_string(),
            supported: false,
        }
    }
}

// --- Recommendation copy (normative table) ---------------------------

const REC_LIVESYNC: &str = "Self-hosted LiveSync replicates your saves at the file level. \
     Avoid editing the same note simultaneously in Slate and Obsidian.";
const REC_ICLOUD: &str = "This vault is inside iCloud Drive. Files may be evicted to the \
     cloud; Slate reads them transparently, but first-touch latency can spike and mid-write \
     eviction is outside Slate's control.";
const REC_DROPBOX: &str = "This vault is inside a Dropbox-synced folder. Dropbox replicates \
     whole files on save; concurrent edits from another device can produce conflicted copies.";
const REC_ONEDRIVE: &str = "This vault is inside a OneDrive-synced folder. OneDrive replicates \
     whole files on save; concurrent edits from another device can produce conflicted copies.";
const REC_GOOGLE_DRIVE: &str = "This vault is inside a Google Drive–synced folder. Drive \
     replicates whole files on save; concurrent edits from another device can produce \
     conflicted copies.";
const REC_GIT: &str = "This vault is a Git working tree. Slate's writes go through the \
     working tree like any editor; commit on your own cadence.";
const REC_SYNCTHING: &str = "This vault is a Syncthing folder. Syncthing replicates whole \
     files on save; concurrent edits from another device can produce conflicts.";

/// OneDrive plants this GUID-named marker file at every sync root.
const ONEDRIVE_MARKER: &str = ".849C9593-D756-4E56-8D6E-42412F2A707B";

/// iCloud file-provider xattr (current) and the legacy clouddocs
/// xattr prefix.
const ICLOUD_XATTR_FPFS: &str = "com.apple.fileprovider.fpfs#P";
const ICLOUD_XATTR_CLOUDDOCS_PREFIX: &str = "com.apple.clouddocs.";

// --- Probe seam -------------------------------------------------------

/// Filesystem probes the detector runs through. Split behind a trait so
/// fixture tests can exercise every detector arm — including the
/// `$HOME`-prefix and xattr arms CI runners can't plant reliably —
/// without touching the real environment.
trait FsProbe {
    fn exists(&self, path: &Path) -> bool;
    fn is_dir(&self, path: &Path) -> bool;
    /// Names of the direct children of `path`; empty on any error.
    fn read_dir_names(&self, path: &Path) -> Vec<String>;
    /// Extended-attribute names on `path`; empty on any error.
    fn xattr_names(&self, path: &Path) -> Vec<String>;
    /// `None` on canonicalization failure — the caller falls back to
    /// the raw path (detection degrades, never errors).
    fn canonicalize(&self, path: &Path) -> Option<PathBuf>;
    fn home(&self) -> Option<PathBuf>;
}

/// The real filesystem.
struct RealFs;

impl FsProbe for RealFs {
    fn exists(&self, path: &Path) -> bool {
        path.exists()
    }

    fn is_dir(&self, path: &Path) -> bool {
        path.is_dir()
    }

    fn read_dir_names(&self, path: &Path) -> Vec<String> {
        std::fs::read_dir(path)
            .map(|entries| {
                entries
                    .filter_map(|e| e.ok())
                    .map(|e| e.file_name().to_string_lossy().into_owned())
                    .collect()
            })
            .unwrap_or_default()
    }

    fn xattr_names(&self, path: &Path) -> Vec<String> {
        listxattr_names(path)
    }

    fn canonicalize(&self, path: &Path) -> Option<PathBuf> {
        std::fs::canonicalize(path).ok()
    }

    fn home(&self) -> Option<PathBuf> {
        std::env::var_os("HOME").map(PathBuf::from)
    }
}

/// `listxattr(2)` via libc — no new dependency (m_spec §M-1 rules).
/// Any failure (including the buffer-size race between the two calls)
/// degrades to "no xattrs": detection degrades, never errors.
#[cfg(target_os = "macos")]
fn listxattr_names(path: &Path) -> Vec<String> {
    use std::os::unix::ffi::OsStrExt;
    let Ok(c_path) = std::ffi::CString::new(path.as_os_str().as_bytes()) else {
        return Vec::new();
    };
    // SAFETY: c_path is a valid NUL-terminated string; a NULL buffer
    // with size 0 asks for the required buffer length.
    let len = unsafe { libc::listxattr(c_path.as_ptr(), std::ptr::null_mut(), 0, 0) };
    if len <= 0 {
        return Vec::new();
    }
    let mut buf = vec![0u8; len as usize];
    // SAFETY: buf is a live allocation of exactly `len` bytes.
    let n = unsafe {
        libc::listxattr(
            c_path.as_ptr(),
            buf.as_mut_ptr().cast::<libc::c_char>(),
            buf.len(),
            0,
        )
    };
    if n <= 0 {
        return Vec::new();
    }
    buf.truncate(n as usize);
    split_xattr_name_buffer(&buf)
}

#[cfg(all(unix, not(target_os = "macos")))]
fn listxattr_names(path: &Path) -> Vec<String> {
    use std::os::unix::ffi::OsStrExt;
    let Ok(c_path) = std::ffi::CString::new(path.as_os_str().as_bytes()) else {
        return Vec::new();
    };
    // SAFETY: as the macOS arm; Linux listxattr has no options arg.
    let len = unsafe { libc::listxattr(c_path.as_ptr(), std::ptr::null_mut(), 0) };
    if len <= 0 {
        return Vec::new();
    }
    let mut buf = vec![0u8; len as usize];
    // SAFETY: buf is a live allocation of exactly `len` bytes.
    let n = unsafe {
        libc::listxattr(
            c_path.as_ptr(),
            buf.as_mut_ptr().cast::<libc::c_char>(),
            buf.len(),
        )
    };
    if n <= 0 {
        return Vec::new();
    }
    buf.truncate(n as usize);
    split_xattr_name_buffer(&buf)
}

#[cfg(not(unix))]
fn listxattr_names(_path: &Path) -> Vec<String> {
    Vec::new()
}

/// `listxattr` returns NUL-separated names in one buffer.
#[cfg(unix)]
fn split_xattr_name_buffer(buf: &[u8]) -> Vec<String> {
    buf.split(|b| *b == 0)
        .filter(|s| !s.is_empty())
        .map(|s| String::from_utf8_lossy(s).into_owned())
        .collect()
}

// --- Detection --------------------------------------------------------

/// Detect external sync systems managing the vault at `vault_root`.
///
/// Pure function; no SQLite, no session state. Synchronous and cheap
/// (a fixed set of exact-path probes plus two path-depth-bounded
/// ancestor walks); no `CancelToken`. Hosts call it off-main like any
/// FFI call.
pub fn detect_sync_providers(vault_root: &Path) -> SyncDetectionReport {
    detect_with_probe(vault_root, &RealFs)
}

fn detect_with_probe(root: &Path, fs: &dyn FsProbe) -> SyncDetectionReport {
    // Canonicalize once for the location-based probes; fall back to
    // the raw path on failure (m_spec: detection degrades, never
    // errors).
    let canon = fs.canonicalize(root).unwrap_or_else(|| root.to_path_buf());
    // `$HOME` gates the prefix probes only; when unset those are
    // skipped and the marker probes still run.
    let home = fs.home();

    let mut providers = Vec::new();

    // Detector-table order is the output order — deterministic by
    // construction.
    if let Some(p) = detect_livesync(root, fs) {
        providers.push(p);
    }
    if let Some(p) = detect_icloud(root, &canon, home.as_deref(), fs) {
        providers.push(p);
    }
    if let Some(p) = detect_dropbox(root, &canon, home.as_deref(), fs) {
        providers.push(p);
    }
    if let Some(p) = detect_onedrive(root, &canon, fs) {
        providers.push(p);
    }
    if let Some(p) = detect_google_drive(root, &canon, home.as_deref(), fs) {
        providers.push(p);
    }
    if let Some(p) = detect_git(root, fs) {
        providers.push(p);
    }
    if let Some(p) = detect_syncthing(root, fs) {
        providers.push(p);
    }

    let multi_sync_warning = multi_sync_warning(&providers);
    let audio_summary = audio_summary(&providers, multi_sync_warning.is_some());
    SyncDetectionReport {
        providers,
        multi_sync_warning,
        audio_summary,
        supported: true,
    }
}

/// dir `{root}/.obsidian/plugins/obsidian-livesync/` exists AND
/// contains `manifest.json` or `data.json`.
fn detect_livesync(root: &Path, fs: &dyn FsProbe) -> Option<DetectedSyncProvider> {
    let plugin_dir = root.join(".obsidian/plugins/obsidian-livesync");
    if !fs.is_dir(&plugin_dir) {
        return None;
    }
    let evidence: Vec<String> = ["manifest.json", "data.json"]
        .iter()
        .filter(|f| fs.exists(&plugin_dir.join(f)))
        .map(|f| format!(".obsidian/plugins/obsidian-livesync/{f}"))
        .collect();
    if evidence.is_empty() {
        return None;
    }
    Some(DetectedSyncProvider {
        kind: SyncProviderKind::LiveSync,
        evidence_paths: evidence,
        risk_level: RiskLevel::High,
        recommendation: REC_LIVESYNC.to_string(),
    })
}

/// (a) canonicalized root under `$HOME/Library/Mobile Documents/`, OR
/// (b) root or any ancestor up to `$HOME` carries the file-provider or
/// legacy clouddocs xattr, OR (c) ≥ 1 `*.icloud` placeholder among the
/// direct children of the vault root.
fn detect_icloud(
    root: &Path,
    canon: &Path,
    home: Option<&Path>,
    fs: &dyn FsProbe,
) -> Option<DetectedSyncProvider> {
    let mut evidence = Vec::new();

    if let Some(home) = home {
        // (a) location prefix.
        if canon.starts_with(home.join("Library/Mobile Documents")) {
            evidence.push(canon.display().to_string());
        }
        // (b) xattr walk — root and every ancestor that is still under
        // `$HOME` (bounded by path depth; no read_dir on ancestors).
        for ancestor in canon.ancestors() {
            if !ancestor.starts_with(home) {
                break;
            }
            let has_icloud_xattr = fs.xattr_names(ancestor).iter().any(|name| {
                name == ICLOUD_XATTR_FPFS || name.starts_with(ICLOUD_XATTR_CLOUDDOCS_PREFIX)
            });
            if has_icloud_xattr {
                evidence.push(ancestor.display().to_string());
            }
        }
    }

    // (c) placeholder children — one read_dir of the root only;
    // placeholders deeper in the tree are caught by (a)/(b). Sorted:
    // read_dir order is platform-dependent and evidence must be
    // deterministic.
    let mut placeholders: Vec<String> = fs
        .read_dir_names(root)
        .into_iter()
        .filter(|name| name.ends_with(".icloud"))
        .collect();
    placeholders.sort();
    evidence.extend(placeholders);

    let evidence = dedup_preserving_order(evidence);
    if evidence.is_empty() {
        return None;
    }
    Some(DetectedSyncProvider {
        kind: SyncProviderKind::ICloudDrive,
        evidence_paths: evidence,
        risk_level: RiskLevel::Medium,
        recommendation: REC_ICLOUD.to_string(),
    })
}

/// (a) `{root}/.dropbox` file or `{root}/.dropbox.cache` dir, OR (b)
/// any ancestor of root contains `.dropbox.cache`, OR (c) canonicalized
/// root has prefix `$HOME/Library/CloudStorage/Dropbox`.
fn detect_dropbox(
    root: &Path,
    canon: &Path,
    home: Option<&Path>,
    fs: &dyn FsProbe,
) -> Option<DetectedSyncProvider> {
    let mut evidence = Vec::new();

    let marker = root.join(".dropbox");
    if fs.exists(&marker) && !fs.is_dir(&marker) {
        evidence.push(".dropbox".to_string());
    }
    if fs.is_dir(&root.join(".dropbox.cache")) {
        evidence.push(".dropbox.cache".to_string());
    }

    // (b) ancestor walk to `/` — a single named-marker existence check
    // per ancestor, never a read_dir. skip(1): the root itself is
    // covered by (a).
    for ancestor in canon.ancestors().skip(1) {
        let cache = ancestor.join(".dropbox.cache");
        if fs.is_dir(&cache) {
            evidence.push(cache.display().to_string());
        }
    }

    // (c) macOS file-provider mount, mounted at
    // `$HOME/Library/CloudStorage/Dropbox`. The normative table is an
    // exact *path* prefix (`.../CloudStorage/Dropbox`), so this is a
    // component-exact match via `Path::starts_with` — NOT a
    // component-`starts_with`. Lookalike CloudStorage siblings like
    // `DropboxBackups` (or a hypothetical `Dropbox-Acme`) are outside
    // the contract and must not fire a false Medium-risk Dropbox
    // detection that could spuriously trip the multi-sync warning.
    if let Some(home) = home
        && canon.starts_with(home.join("Library/CloudStorage/Dropbox"))
    {
        evidence.push(canon.display().to_string());
    }

    let evidence = dedup_preserving_order(evidence);
    if evidence.is_empty() {
        return None;
    }
    Some(DetectedSyncProvider {
        kind: SyncProviderKind::Dropbox,
        evidence_paths: evidence,
        risk_level: RiskLevel::Medium,
        recommendation: REC_DROPBOX.to_string(),
    })
}

/// (a) canonicalized root contains a component exactly `OneDrive` or
/// starting `OneDrive-`, OR (b) the OneDrive sync-root marker file
/// exists at the root.
fn detect_onedrive(root: &Path, canon: &Path, fs: &dyn FsProbe) -> Option<DetectedSyncProvider> {
    let mut evidence = Vec::new();

    let has_onedrive_component = canon.components().any(|c| {
        matches!(c, Component::Normal(name)
            if name.to_str().is_some_and(|s| s == "OneDrive" || s.starts_with("OneDrive-")))
    });
    if has_onedrive_component {
        evidence.push(canon.display().to_string());
    }
    // The normative table's arm (b) is a marker *file*; a directory of
    // that exact GUID name is a lookalike and must not fire (same
    // file-vs-directory discipline the Dropbox `.dropbox` and Syncthing
    // `.stignore` arms enforce — Git is the sole dir-or-file exception,
    // by explicit spec wording).
    let marker = root.join(ONEDRIVE_MARKER);
    if fs.exists(&marker) && !fs.is_dir(&marker) {
        evidence.push(ONEDRIVE_MARKER.to_string());
    }

    if evidence.is_empty() {
        return None;
    }
    Some(DetectedSyncProvider {
        kind: SyncProviderKind::OneDrive,
        evidence_paths: evidence,
        risk_level: RiskLevel::Medium,
        recommendation: REC_ONEDRIVE.to_string(),
    })
}

/// (a) canonicalized root under `$HOME/Library/CloudStorage/GoogleDrive-*`,
/// OR (b) Drive's transfer-staging dirs exist at the root.
fn detect_google_drive(
    root: &Path,
    canon: &Path,
    home: Option<&Path>,
    fs: &dyn FsProbe,
) -> Option<DetectedSyncProvider> {
    let mut evidence = Vec::new();

    if let Some(home) = home
        && cloud_storage_component_starts_with(canon, home, "GoogleDrive-")
    {
        evidence.push(canon.display().to_string());
    }
    for staging_dir in [".tmp.driveupload", ".tmp.drivedownload"] {
        if fs.is_dir(&root.join(staging_dir)) {
            evidence.push(staging_dir.to_string());
        }
    }

    if evidence.is_empty() {
        return None;
    }
    Some(DetectedSyncProvider {
        kind: SyncProviderKind::GoogleDrive,
        evidence_paths: evidence,
        risk_level: RiskLevel::Medium,
        recommendation: REC_GOOGLE_DRIVE.to_string(),
    })
}

/// `{root}/.git` exists — dir **or** file (worktrees use a `.git` file).
fn detect_git(root: &Path, fs: &dyn FsProbe) -> Option<DetectedSyncProvider> {
    if !fs.exists(&root.join(".git")) {
        return None;
    }
    Some(DetectedSyncProvider {
        kind: SyncProviderKind::Git,
        evidence_paths: vec![".git".to_string()],
        risk_level: RiskLevel::Low,
        recommendation: REC_GIT.to_string(),
    })
}

/// dir `{root}/.stfolder/` or file `{root}/.stignore` exists.
fn detect_syncthing(root: &Path, fs: &dyn FsProbe) -> Option<DetectedSyncProvider> {
    let mut evidence = Vec::new();
    if fs.is_dir(&root.join(".stfolder")) {
        evidence.push(".stfolder".to_string());
    }
    let stignore = root.join(".stignore");
    if fs.exists(&stignore) && !fs.is_dir(&stignore) {
        evidence.push(".stignore".to_string());
    }
    if evidence.is_empty() {
        return None;
    }
    Some(DetectedSyncProvider {
        kind: SyncProviderKind::Syncthing,
        evidence_paths: evidence,
        risk_level: RiskLevel::Medium,
        recommendation: REC_SYNCTHING.to_string(),
    })
}

/// True when `canon` is under `$HOME/Library/CloudStorage/<component>`
/// with `<component>` starting with `component_prefix`.
fn cloud_storage_component_starts_with(canon: &Path, home: &Path, component_prefix: &str) -> bool {
    let Ok(under_cloud_storage) = canon.strip_prefix(home.join("Library/CloudStorage")) else {
        return false;
    };
    match under_cloud_storage.components().next() {
        Some(Component::Normal(name)) => name
            .to_str()
            .is_some_and(|s| s.starts_with(component_prefix)),
        _ => false,
    }
}

fn dedup_preserving_order(paths: Vec<String>) -> Vec<String> {
    let mut seen = std::collections::HashSet::new();
    paths
        .into_iter()
        .filter(|p| seen.insert(p.clone()))
        .collect()
}

/// "Multiple sync systems…" copy when ≥ 2 providers of risk ≥ Medium
/// are detected. Git (Low) never appears in — or triggers — the
/// warning.
fn multi_sync_warning(providers: &[DetectedSyncProvider]) -> Option<String> {
    let medium_or_higher: Vec<&str> = providers
        .iter()
        .filter(|p| p.risk_level >= RiskLevel::Medium)
        .map(|p| p.kind.display_name())
        .collect();
    if medium_or_higher.len() < 2 {
        return None;
    }
    Some(format!(
        "Multiple sync systems are managing this vault ({}). Consider disabling all but \
         one — overlapping sync tools can corrupt each other's state.",
        medium_or_higher.join(", ")
    ))
}

/// Pre-rendered VoiceOver summary, same pattern as
/// `QueryResultSet::summary`.
fn audio_summary(providers: &[DetectedSyncProvider], has_multi_sync_warning: bool) -> String {
    if providers.is_empty() {
        return "No sync systems detected.".to_string();
    }
    let names: Vec<&str> = providers.iter().map(|p| p.kind.display_name()).collect();
    let mut summary = if providers.len() == 1 {
        format!("1 sync system detected: {}.", names[0])
    } else {
        format!(
            "{} sync systems detected: {}.",
            providers.len(),
            names.join(", ")
        )
    };
    if has_multi_sync_warning {
        summary.push_str(" Warning: multiple sync systems on one vault.");
    }
    summary
}

// --- LiveSync config reader (M-2, #533) --------------------------------

/// Result of reading the LiveSync plugin config. Read failures are
/// data, not errors — the diagnostics panel renders every variant.
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum LiveSyncConfigStatus {
    /// No `data.json` (the plugin may still be detected via
    /// `manifest.json` — "plugin present, no config found").
    NotPresent,
    Parsed(LiveSyncConfig),
    /// Unreadable/unparseable — never a hard error.
    Malformed {
        reason: String,
    },
}

/// The credential-free subset of the LiveSync plugin's `data.json`.
///
/// **Structural credential safety:** these six fields are the ONLY
/// values ever read out of the JSON. `couchDB_USER`,
/// `couchDB_PASSWORD`, `passphrase`, and every other key are never
/// copied into any output type — no "redaction"; fields that aren't
/// read can't leak. Enforced by the planted-credential round-trip
/// test (the M-2 DoD gate).
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct LiveSyncConfig {
    /// Host (+ optional port) extracted from `couchDB_URI`. NEVER
    /// contains userinfo, path, query, or fragment. `None` when the
    /// URI is absent/unparseable.
    pub server_host: Option<String>,
    /// `couchDB_DBNAME` verbatim (a database name, not a credential).
    pub database: Option<String>,
    /// `"liveSync"`.
    pub live_sync_enabled: Option<bool>,
    /// `"syncOnSave"`.
    pub sync_on_save: Option<bool>,
    /// `"syncOnStart"`.
    pub sync_on_start: Option<bool>,
    /// `"encrypt"`.
    pub end_to_end_encryption: Option<bool>,
}

/// Defensive size bound on `data.json` (m_spec §M-2).
const LIVESYNC_CONFIG_MAX_BYTES: u64 = 1024 * 1024;

/// Read the LiveSync plugin config at
/// `{vault_root}/.obsidian/plugins/obsidian-livesync/data.json`.
///
/// Field-tolerant (`serde_json::Value` — the plugin's schema drifts):
/// absent fields land as `None`. Every failure mode maps to a status
/// variant; this function never errors and never panics.
pub fn read_livesync_config(vault_root: &Path) -> LiveSyncConfigStatus {
    use std::io::Read;
    let path = vault_root.join(".obsidian/plugins/obsidian-livesync/data.json");

    // **Vault-escape guard.** Canonicalize the target and require it to
    // stay under the canonicalized vault root — the same boundary
    // `vault::fs` enforces (fs.rs:300-309). Without it, a hostile vault
    // could point `data.json` at a regular JSON file OUTSIDE the vault
    // (another user's LiveSync config) and have us surface its
    // host/database. Canonicalize resolves symlinks, so a symlink to an
    // outside file lands outside the root and is refused here.
    // NotFound canonicalization → `NotPresent` (no file to read).
    let canonical_target = match std::fs::canonicalize(&path) {
        Ok(p) => p,
        Err(e) if e.kind() == std::io::ErrorKind::NotFound => {
            return LiveSyncConfigStatus::NotPresent;
        }
        Err(e) => {
            return LiveSyncConfigStatus::Malformed {
                reason: format!("config file unreadable: {e}"),
            };
        }
    };
    // Fall back to the raw root if canonicalization fails (matching the
    // detector's degrade-never-error rule); if BOTH canonicalize, the
    // containment check is authoritative.
    if let Ok(canonical_root) = std::fs::canonicalize(vault_root)
        && !canonical_target.starts_with(&canonical_root)
    {
        return LiveSyncConfigStatus::Malformed {
            reason: "config path escapes the vault".to_string(),
        };
    }

    // Open ONCE (the canonical target) and read through the open handle.
    // Never re-open by path after a stat: a separate `metadata` + `read`
    // pair is a TOCTOU window a hostile vault can swing (swap the file, or
    // point it at a FIFO/char device whose stat length is 0 so a naive
    // bound check passes and the subsequent read blocks or exhausts
    // memory).
    //
    // `open_guarded` adds `O_NOFOLLOW | O_NONBLOCK` on Unix: `O_NOFOLLOW`
    // refuses a symlink hot-swapped onto the final component in the
    // canonicalize→open race (the canonical final component is a real
    // file, so this only bites a racing attacker — same rationale as
    // `vault::fs::open_nofollow`); `O_NONBLOCK` keeps a FIFO/special file
    // from blocking the open. Both are no-ops for the regular files we go
    // on to read. The regular-file guard below rejects anything else.
    let file = match open_guarded(&canonical_target) {
        Ok(f) => f,
        Err(e) if e.kind() == std::io::ErrorKind::NotFound => {
            return LiveSyncConfigStatus::NotPresent;
        }
        Err(e) => {
            return LiveSyncConfigStatus::Malformed {
                reason: format!("config file unreadable: {e}"),
            };
        }
    };
    // Reject non-regular files (directory, FIFO, char/block device, socket)
    // using the OPEN handle's metadata — race-free, no second path lookup.
    match file.metadata() {
        Ok(m) if m.is_file() => {}
        Ok(_) => {
            return LiveSyncConfigStatus::Malformed {
                reason: "config file is not a regular file".to_string(),
            };
        }
        Err(e) => {
            return LiveSyncConfigStatus::Malformed {
                reason: format!("config file unreadable: {e}"),
            };
        }
    }
    // Cap the read at `MAX + 1` bytes regardless of the reported length, so
    // a growing/replaced/special file can never make us materialize more
    // than the cap in memory. `> MAX` after the read means "too large".
    let cap = LIVESYNC_CONFIG_MAX_BYTES.saturating_add(1);
    let mut bytes: Vec<u8> = Vec::new();
    if let Err(e) = file.take(cap).read_to_end(&mut bytes) {
        return LiveSyncConfigStatus::Malformed {
            reason: format!("config file unreadable: {e}"),
        };
    }
    if bytes.len() as u64 > LIVESYNC_CONFIG_MAX_BYTES {
        return LiveSyncConfigStatus::Malformed {
            reason: "config file too large".to_string(),
        };
    }
    parse_livesync_config(&bytes)
}

/// Open a file for reading with `O_NOFOLLOW | O_NONBLOCK` on Unix:
/// - `O_NOFOLLOW` refuses a final-component symlink (TOCTOU escape guard,
///   mirroring `vault::fs::open_nofollow`);
/// - `O_NONBLOCK` stops a FIFO/special file from blocking the open (the
///   caller rejects non-regular files via the open handle's metadata).
///
/// Both flags are no-ops for the regular files we actually read. On
/// non-Unix, a plain open (the platforms slate ships to are Unix-likes).
#[cfg(unix)]
fn open_guarded(path: &Path) -> std::io::Result<std::fs::File> {
    use std::os::unix::fs::OpenOptionsExt;
    std::fs::OpenOptions::new()
        .read(true)
        .custom_flags(libc::O_NOFOLLOW | libc::O_NONBLOCK)
        .open(path)
}

#[cfg(not(unix))]
fn open_guarded(path: &Path) -> std::io::Result<std::fs::File> {
    std::fs::File::open(path)
}

/// Parse `data.json` bytes. Split from the file read so the fuzz test
/// can drive arbitrary byte strings through the parser.
fn parse_livesync_config(bytes: &[u8]) -> LiveSyncConfigStatus {
    let value: serde_json::Value = match serde_json::from_slice(bytes) {
        Ok(v) => v,
        Err(e) => {
            return LiveSyncConfigStatus::Malformed {
                reason: format!("invalid JSON: {e}"),
            };
        }
    };
    let Some(obj) = value.as_object() else {
        return LiveSyncConfigStatus::Malformed {
            reason: "config is not a JSON object".to_string(),
        };
    };
    // The structural allow-list: these are the only keys ever read.
    LiveSyncConfigStatus::Parsed(LiveSyncConfig {
        server_host: obj
            .get("couchDB_URI")
            .and_then(|v| v.as_str())
            .and_then(extract_host),
        database: obj
            .get("couchDB_DBNAME")
            .and_then(|v| v.as_str())
            .map(str::to_string),
        live_sync_enabled: obj.get("liveSync").and_then(|v| v.as_bool()),
        sync_on_save: obj.get("syncOnSave").and_then(|v| v.as_bool()),
        sync_on_start: obj.get("syncOnStart").and_then(|v| v.as_bool()),
        end_to_end_encryption: obj.get("encrypt").and_then(|v| v.as_bool()),
    })
}

/// Manual `host[:port]` extraction from a URI string (no `url` crate,
/// m_spec §M-2): strip `scheme://`, cut the authority at the first
/// `/` / `?` / `#`, drop userinfo up to and including the last `@`,
/// then **validate** the remaining `host[:port]` against a strict
/// grammar and fail closed on anything that isn't host-shaped.
///
/// The validation is a credential-safety guard, not cosmetics. Two
/// malformed shapes both leak userinfo without care:
/// - `https://alice:hunter2/x@host/db` — the `/` terminates the
///   authority at `alice:hunter2` (RFC 3986); a naive "drop up to `@`"
///   would return the userinfo `alice:hunter2`.
/// - `https://alice:1234/x@host/db` — worse, `alice:1234` *looks like*
///   a valid `host:port`, so a shape check alone still emits the
///   password `1234`.
///
/// Both are caught the same way: a `@` that appears **after** the first
/// path delimiter proves userinfo was stranded by a malformed
/// authority, so the whole extraction fails closed. Combined with the
/// strict `host[:port]` shape check on the userinfo-stripped authority,
/// no userinfo can surface. Extraction/validation failure → `None`
/// (the config still parses).
fn extract_host(uri: &str) -> Option<String> {
    let after_scheme = uri.trim().split_once("://")?.1;
    let (authority, rest) = match after_scheme.find(['/', '?', '#']) {
        Some(i) => (&after_scheme[..i], &after_scheme[i..]),
        None => (after_scheme, ""),
    };
    // A stray `@` in the part past the authority delimiter means the
    // URI's userinfo was split off by that delimiter (malformed) — the
    // `alice:1234/x@host` leak. Refuse to guess: fail closed.
    if rest.contains('@') {
        return None;
    }
    // Drop userinfo up to and including the last `@` within the
    // authority. Whatever remains must still pass the strict shape
    // check below (userinfo colon-pairs that aren't host-shaped are
    // rejected there).
    let host = match authority.rfind('@') {
        Some(at) => &authority[at + 1..],
        None => authority,
    };
    if is_valid_host_port(host) {
        Some(host.to_string())
    } else {
        None
    }
}

/// Strict `host[:port]` validator — fail closed. Accepts:
/// - a reg-name / IPv4 host (`[A-Za-z0-9.-]+`) with an optional
///   `:port` (all digits), or
/// - a bracketed IPv6 literal `[...]` with an optional `:port`.
///
/// Rejects userinfo remnants (`alice:hunter2`), whitespace, control
/// characters, empty hosts, and anything else not host-shaped — so no
/// credential-looking text can ever surface as `server_host`.
fn is_valid_host_port(host: &str) -> bool {
    if host.is_empty() {
        return false;
    }
    // IPv6 literal: `[...]` optionally followed by `:port`.
    if let Some(rest) = host.strip_prefix('[') {
        let Some((inner, after)) = rest.split_once(']') else {
            return false;
        };
        // Inner is hex digits, ':', and '.' (v4-mapped) — non-empty.
        if inner.is_empty()
            || !inner
                .bytes()
                .all(|b| b.is_ascii_hexdigit() || b == b':' || b == b'.')
        {
            return false;
        }
        return after.is_empty() || is_valid_port(after);
    }
    // reg-name / IPv4 host, optionally `:port`.
    let (name, port) = match host.split_once(':') {
        Some((name, port)) => (name, Some(port)),
        None => (host, None),
    };
    if name.is_empty()
        || !name
            .bytes()
            .all(|b| b.is_ascii_alphanumeric() || b == b'.' || b == b'-')
    {
        return false;
    }
    match port {
        Some(port) => is_valid_port(port),
        None => true,
    }
}

/// A port suffix `:port` (the caller passes the text after the `:`):
/// non-empty and all ASCII digits.
fn is_valid_port(port: &str) -> bool {
    let digits = port.strip_prefix(':').unwrap_or(port);
    !digits.is_empty() && digits.bytes().all(|b| b.is_ascii_digit())
}

// --- Tests ------------------------------------------------------------

#[cfg(test)]
mod tests {
    use super::*;
    use std::collections::{BTreeMap, BTreeSet};

    /// Seam fixture: an in-memory filesystem the full detector table
    /// runs against without touching real `$HOME` or xattrs.
    #[derive(Default)]
    struct FakeFs {
        files: BTreeSet<PathBuf>,
        dirs: BTreeSet<PathBuf>,
        xattrs: BTreeMap<PathBuf, Vec<String>>,
        home: Option<PathBuf>,
        canonicalize_fails: bool,
    }

    impl FakeFs {
        fn with_home(home: &str) -> Self {
            Self {
                home: Some(PathBuf::from(home)),
                ..Self::default()
            }
        }

        fn file(mut self, path: &str) -> Self {
            let path = PathBuf::from(path);
            self.add_parent_dirs(&path);
            self.files.insert(path);
            self
        }

        fn dir(mut self, path: &str) -> Self {
            let path = PathBuf::from(path);
            self.add_parent_dirs(&path);
            self.dirs.insert(path);
            self
        }

        fn xattr(mut self, path: &str, name: &str) -> Self {
            self.xattrs
                .entry(PathBuf::from(path))
                .or_default()
                .push(name.to_string());
            self
        }

        fn canonicalize_fails(mut self) -> Self {
            self.canonicalize_fails = true;
            self
        }

        fn add_parent_dirs(&mut self, path: &Path) {
            let mut ancestor = path.parent();
            while let Some(dir) = ancestor {
                if dir.as_os_str().is_empty() {
                    break;
                }
                self.dirs.insert(dir.to_path_buf());
                ancestor = dir.parent();
            }
        }
    }

    impl FsProbe for FakeFs {
        fn exists(&self, path: &Path) -> bool {
            self.files.contains(path) || self.dirs.contains(path)
        }

        fn is_dir(&self, path: &Path) -> bool {
            self.dirs.contains(path)
        }

        fn read_dir_names(&self, path: &Path) -> Vec<String> {
            self.files
                .iter()
                .chain(self.dirs.iter())
                .filter(|p| p.parent() == Some(path))
                .filter_map(|p| p.file_name())
                .map(|n| n.to_string_lossy().into_owned())
                .collect()
        }

        fn xattr_names(&self, path: &Path) -> Vec<String> {
            self.xattrs.get(path).cloned().unwrap_or_default()
        }

        fn canonicalize(&self, path: &Path) -> Option<PathBuf> {
            if self.canonicalize_fails {
                None
            } else {
                Some(path.to_path_buf())
            }
        }

        fn home(&self) -> Option<PathBuf> {
            self.home.clone()
        }
    }

    const ROOT: &str = "/Users/u/vault";

    fn detect(fs: &FakeFs) -> SyncDetectionReport {
        detect_with_probe(Path::new(ROOT), fs)
    }

    fn kinds(report: &SyncDetectionReport) -> Vec<SyncProviderKind> {
        report.providers.iter().map(|p| p.kind).collect()
    }

    fn single(report: &SyncDetectionReport, kind: SyncProviderKind) -> DetectedSyncProvider {
        assert_eq!(
            kinds(report),
            vec![kind],
            "expected exactly one {kind:?} detection, got: {report:#?}"
        );
        report.providers[0].clone()
    }

    // --- LiveSync ------------------------------------------------------

    #[test]
    fn livesync_fires_on_manifest() {
        let fs = FakeFs::with_home("/Users/u")
            .file("/Users/u/vault/.obsidian/plugins/obsidian-livesync/manifest.json");
        let p = single(&detect(&fs), SyncProviderKind::LiveSync);
        assert_eq!(
            p.evidence_paths,
            vec![".obsidian/plugins/obsidian-livesync/manifest.json"]
        );
        assert_eq!(p.risk_level, RiskLevel::High);
        assert_eq!(p.recommendation, REC_LIVESYNC);
    }

    #[test]
    fn livesync_fires_on_data_json() {
        let fs = FakeFs::with_home("/Users/u")
            .file("/Users/u/vault/.obsidian/plugins/obsidian-livesync/data.json");
        let p = single(&detect(&fs), SyncProviderKind::LiveSync);
        assert_eq!(
            p.evidence_paths,
            vec![".obsidian/plugins/obsidian-livesync/data.json"]
        );
    }

    #[test]
    fn livesync_plugin_dir_without_config_files_does_not_fire() {
        let fs =
            FakeFs::with_home("/Users/u").dir("/Users/u/vault/.obsidian/plugins/obsidian-livesync");
        assert!(detect(&fs).providers.is_empty());
    }

    // --- iCloud Drive ----------------------------------------------------

    #[test]
    fn icloud_fires_on_mobile_documents_prefix() {
        let root = "/Users/u/Library/Mobile Documents/iCloud~md~obsidian/Documents/vault";
        let fs = FakeFs::with_home("/Users/u").dir(root);
        let report = detect_with_probe(Path::new(root), &fs);
        let p = single(&report, SyncProviderKind::ICloudDrive);
        assert_eq!(p.evidence_paths, vec![root.to_string()]);
        assert_eq!(p.risk_level, RiskLevel::Medium);
        assert_eq!(p.recommendation, REC_ICLOUD);
    }

    #[test]
    fn icloud_fires_on_fileprovider_xattr_on_ancestor() {
        let fs = FakeFs::with_home("/Users/u")
            .dir(ROOT)
            .xattr("/Users/u", ICLOUD_XATTR_FPFS);
        let p = single(&detect(&fs), SyncProviderKind::ICloudDrive);
        assert_eq!(p.evidence_paths, vec!["/Users/u".to_string()]);
    }

    #[test]
    fn icloud_fires_on_legacy_clouddocs_xattr_on_root() {
        let fs = FakeFs::with_home("/Users/u")
            .dir(ROOT)
            .xattr(ROOT, "com.apple.clouddocs.security");
        let p = single(&detect(&fs), SyncProviderKind::ICloudDrive);
        assert_eq!(p.evidence_paths, vec![ROOT.to_string()]);
    }

    #[test]
    fn icloud_xattr_walk_stops_at_home() {
        // The xattr sits ABOVE $HOME — outside the bounded walk, so it
        // must not fire.
        let fs = FakeFs::with_home("/Users/u")
            .dir(ROOT)
            .xattr("/Users", ICLOUD_XATTR_FPFS);
        assert!(detect(&fs).providers.is_empty());
    }

    #[test]
    fn icloud_fires_on_placeholder_children() {
        let fs = FakeFs::with_home("/Users/u")
            .file("/Users/u/vault/.note.md.icloud")
            .file("/Users/u/vault/.older.md.icloud");
        let p = single(&detect(&fs), SyncProviderKind::ICloudDrive);
        // Sorted for determinism, vault-relative names.
        assert_eq!(
            p.evidence_paths,
            vec![
                ".note.md.icloud".to_string(),
                ".older.md.icloud".to_string()
            ]
        );
    }

    #[test]
    fn icloud_placeholder_deeper_than_root_does_not_fire() {
        let fs = FakeFs::with_home("/Users/u").file("/Users/u/vault/sub/.note.md.icloud");
        assert!(detect(&fs).providers.is_empty());
    }

    // --- Dropbox ---------------------------------------------------------

    #[test]
    fn dropbox_fires_on_marker_file() {
        let fs = FakeFs::with_home("/Users/u").file("/Users/u/vault/.dropbox");
        let p = single(&detect(&fs), SyncProviderKind::Dropbox);
        assert_eq!(p.evidence_paths, vec![".dropbox".to_string()]);
        assert_eq!(p.risk_level, RiskLevel::Medium);
        assert_eq!(p.recommendation, REC_DROPBOX);
    }

    #[test]
    fn dropbox_marker_as_dir_does_not_fire() {
        // The table says FILE `.dropbox`; a directory of that name is a
        // lookalike.
        let fs = FakeFs::with_home("/Users/u").dir("/Users/u/vault/.dropbox");
        assert!(detect(&fs).providers.is_empty());
    }

    #[test]
    fn dropbox_fires_on_cache_dir() {
        let fs = FakeFs::with_home("/Users/u").dir("/Users/u/vault/.dropbox.cache");
        let p = single(&detect(&fs), SyncProviderKind::Dropbox);
        assert_eq!(p.evidence_paths, vec![".dropbox.cache".to_string()]);
    }

    #[test]
    fn dropbox_fires_on_ancestor_cache_dir() {
        let fs = FakeFs::with_home("/Users/u")
            .dir(ROOT)
            .dir("/Users/u/.dropbox.cache");
        let p = single(&detect(&fs), SyncProviderKind::Dropbox);
        assert_eq!(
            p.evidence_paths,
            vec!["/Users/u/.dropbox.cache".to_string()]
        );
    }

    #[test]
    fn dropbox_fires_on_cloudstorage_prefix() {
        let root = "/Users/u/Library/CloudStorage/Dropbox/vault";
        let fs = FakeFs::with_home("/Users/u").dir(root);
        let report = detect_with_probe(Path::new(root), &fs);
        let p = single(&report, SyncProviderKind::Dropbox);
        assert_eq!(p.evidence_paths, vec![root.to_string()]);
    }

    #[test]
    fn dropbox_cloudstorage_lookalike_sibling_does_not_fire() {
        // The normative table's arm (c) is an exact path prefix
        // `$HOME/Library/CloudStorage/Dropbox` (component-exact
        // "Dropbox"). A sibling CloudStorage folder whose component
        // merely *starts with* "Dropbox" is a lookalike outside the
        // contract — it must NOT produce a false Medium Dropbox
        // detection.
        let root = "/Users/u/Library/CloudStorage/DropboxBackups/vault";
        let fs = FakeFs::with_home("/Users/u").dir(root);
        assert!(
            detect_with_probe(Path::new(root), &fs).providers.is_empty(),
            "DropboxBackups is a CloudStorage lookalike, not a Dropbox mount"
        );
    }

    // --- OneDrive --------------------------------------------------------

    #[test]
    fn onedrive_fires_on_exact_path_component() {
        let root = "/Users/u/OneDrive/vault";
        let fs = FakeFs::with_home("/Users/u").dir(root);
        let report = detect_with_probe(Path::new(root), &fs);
        let p = single(&report, SyncProviderKind::OneDrive);
        assert_eq!(p.evidence_paths, vec![root.to_string()]);
        assert_eq!(p.risk_level, RiskLevel::Medium);
        assert_eq!(p.recommendation, REC_ONEDRIVE);
    }

    #[test]
    fn onedrive_fires_on_dashed_path_component() {
        let root = "/Users/u/OneDrive-Contoso/vault";
        let fs = FakeFs::with_home("/Users/u").dir(root);
        single(
            &detect_with_probe(Path::new(root), &fs),
            SyncProviderKind::OneDrive,
        );
    }

    #[test]
    fn onedrive_component_prefix_without_dash_does_not_fire() {
        // "OneDriveBackup" is neither exactly "OneDrive" nor
        // "OneDrive-" prefixed.
        let root = "/Users/u/OneDriveBackup/vault";
        let fs = FakeFs::with_home("/Users/u").dir(root);
        assert!(detect_with_probe(Path::new(root), &fs).providers.is_empty());
    }

    #[test]
    fn onedrive_fires_on_sync_root_marker_file() {
        let fs = FakeFs::with_home("/Users/u")
            .file("/Users/u/vault/.849C9593-D756-4E56-8D6E-42412F2A707B");
        let p = single(&detect(&fs), SyncProviderKind::OneDrive);
        assert_eq!(p.evidence_paths, vec![ONEDRIVE_MARKER.to_string()]);
    }

    #[test]
    fn onedrive_marker_as_dir_does_not_fire() {
        // The table says marker FILE; a directory of that exact GUID
        // name is a lookalike (mirrors the Dropbox/Syncthing
        // file-vs-directory discipline).
        let fs = FakeFs::with_home("/Users/u")
            .dir("/Users/u/vault/.849C9593-D756-4E56-8D6E-42412F2A707B");
        assert!(detect(&fs).providers.is_empty());
    }

    // --- Google Drive ------------------------------------------------------

    #[test]
    fn google_drive_fires_on_cloudstorage_prefix() {
        let root = "/Users/u/Library/CloudStorage/GoogleDrive-cj@example.com/My Drive/vault";
        let fs = FakeFs::with_home("/Users/u").dir(root);
        let report = detect_with_probe(Path::new(root), &fs);
        let p = single(&report, SyncProviderKind::GoogleDrive);
        assert_eq!(p.evidence_paths, vec![root.to_string()]);
        assert_eq!(p.risk_level, RiskLevel::Medium);
        assert_eq!(p.recommendation, REC_GOOGLE_DRIVE);
    }

    #[test]
    fn google_drive_fires_on_staging_dirs() {
        let fs = FakeFs::with_home("/Users/u")
            .dir("/Users/u/vault/.tmp.driveupload")
            .dir("/Users/u/vault/.tmp.drivedownload");
        let p = single(&detect(&fs), SyncProviderKind::GoogleDrive);
        assert_eq!(
            p.evidence_paths,
            vec![
                ".tmp.driveupload".to_string(),
                ".tmp.drivedownload".to_string()
            ]
        );
    }

    // --- Git ----------------------------------------------------------------

    #[test]
    fn git_fires_on_git_dir() {
        let fs = FakeFs::with_home("/Users/u").dir("/Users/u/vault/.git");
        let p = single(&detect(&fs), SyncProviderKind::Git);
        assert_eq!(p.evidence_paths, vec![".git".to_string()]);
        assert_eq!(p.risk_level, RiskLevel::Low);
        assert_eq!(p.recommendation, REC_GIT);
    }

    #[test]
    fn git_fires_on_git_file() {
        // Worktrees use a `.git` FILE pointing at the real gitdir.
        let fs = FakeFs::with_home("/Users/u").file("/Users/u/vault/.git");
        single(&detect(&fs), SyncProviderKind::Git);
    }

    // --- Syncthing ------------------------------------------------------------

    #[test]
    fn syncthing_fires_on_stfolder() {
        let fs = FakeFs::with_home("/Users/u").dir("/Users/u/vault/.stfolder");
        let p = single(&detect(&fs), SyncProviderKind::Syncthing);
        assert_eq!(p.evidence_paths, vec![".stfolder".to_string()]);
        assert_eq!(p.risk_level, RiskLevel::Medium);
        assert_eq!(p.recommendation, REC_SYNCTHING);
    }

    #[test]
    fn syncthing_fires_on_stignore() {
        let fs = FakeFs::with_home("/Users/u").file("/Users/u/vault/.stignore");
        let p = single(&detect(&fs), SyncProviderKind::Syncthing);
        assert_eq!(p.evidence_paths, vec![".stignore".to_string()]);
    }

    // --- Cross-cutting rules -----------------------------------------------

    #[test]
    fn home_unset_skips_prefix_probes_but_marker_probes_still_run() {
        let root = "/Users/u/Library/Mobile Documents/iCloud~md~obsidian/Documents/vault";
        let fs = FakeFs::default().dir(root).dir(&format!("{root}/.git"));
        let report = detect_with_probe(Path::new(root), &fs);
        // Location says iCloud, but with no $HOME the prefix probe is
        // skipped; the in-vault Git marker still fires.
        assert_eq!(kinds(&report), vec![SyncProviderKind::Git]);
    }

    #[test]
    fn canonicalize_failure_degrades_to_raw_path() {
        let root = "/Users/u/OneDrive/vault";
        let fs = FakeFs::with_home("/Users/u").dir(root).canonicalize_fails();
        // Path-component probe still sees the raw path.
        single(
            &detect_with_probe(Path::new(root), &fs),
            SyncProviderKind::OneDrive,
        );
    }

    #[test]
    fn multi_sync_warning_fires_for_two_medium_or_higher() {
        let fs = FakeFs::with_home("/Users/u")
            .file("/Users/u/vault/.obsidian/plugins/obsidian-livesync/manifest.json")
            .file("/Users/u/vault/.note.md.icloud");
        let report = detect(&fs);
        assert_eq!(
            kinds(&report),
            vec![SyncProviderKind::LiveSync, SyncProviderKind::ICloudDrive]
        );
        let warning = report.multi_sync_warning.expect("warning expected");
        assert_eq!(
            warning,
            "Multiple sync systems are managing this vault (LiveSync, iCloud Drive). \
             Consider disabling all but one — overlapping sync tools can corrupt each \
             other's state."
        );
        assert_eq!(
            report.audio_summary,
            "2 sync systems detected: LiveSync, iCloud Drive. Warning: multiple sync \
             systems on one vault."
        );
    }

    #[test]
    fn git_plus_icloud_yields_no_multi_sync_warning() {
        // Git is Low risk; the rule is ≥ 2 providers of risk ≥ Medium.
        let fs = FakeFs::with_home("/Users/u")
            .dir("/Users/u/vault/.git")
            .file("/Users/u/vault/.note.md.icloud");
        let report = detect(&fs);
        assert_eq!(
            kinds(&report),
            vec![SyncProviderKind::ICloudDrive, SyncProviderKind::Git]
        );
        assert_eq!(report.multi_sync_warning, None);
        assert_eq!(
            report.audio_summary,
            "2 sync systems detected: iCloud Drive, Git."
        );
    }

    #[test]
    fn dropbox_plus_icloud_yields_multi_sync_warning() {
        let fs = FakeFs::with_home("/Users/u")
            .file("/Users/u/vault/.dropbox")
            .file("/Users/u/vault/.note.md.icloud");
        let report = detect(&fs);
        let warning = report.multi_sync_warning.expect("warning expected");
        // iCloud sorts before Dropbox in table order.
        assert!(warning.contains("(iCloud Drive, Dropbox)"), "{warning}");
        // Git absent from the warning even when present in providers:
        // pinned by git_plus_icloud test; here both participants are
        // Medium.
    }

    #[test]
    fn all_seven_systems_detected_in_table_order() {
        let root = "/Users/u/Library/CloudStorage/GoogleDrive-x/OneDrive-y/vault";
        let fs = FakeFs::with_home("/Users/u")
            .file(&format!(
                "{root}/.obsidian/plugins/obsidian-livesync/manifest.json"
            ))
            .file(&format!("{root}/.note.md.icloud"))
            .file(&format!("{root}/.dropbox"))
            .dir(&format!("{root}/.git"))
            .dir(&format!("{root}/.stfolder"));
        // Location covers GoogleDrive (CloudStorage prefix) and
        // OneDrive (path component); in-vault markers cover the rest.
        let report = detect_with_probe(Path::new(root), &fs);
        assert_eq!(
            kinds(&report),
            vec![
                SyncProviderKind::LiveSync,
                SyncProviderKind::ICloudDrive,
                SyncProviderKind::Dropbox,
                SyncProviderKind::OneDrive,
                SyncProviderKind::GoogleDrive,
                SyncProviderKind::Git,
                SyncProviderKind::Syncthing,
            ]
        );
        assert_eq!(
            report.audio_summary,
            "7 sync systems detected: LiveSync, iCloud Drive, Dropbox, OneDrive, \
             Google Drive, Git, Syncthing. Warning: multiple sync systems on one vault."
        );
        assert!(report.multi_sync_warning.is_some());
        // Git appears in providers but never in the warning copy.
        assert!(
            !report
                .multi_sync_warning
                .as_deref()
                .unwrap()
                .contains("Git")
        );
    }

    #[test]
    fn audio_summary_singular_form() {
        let fs = FakeFs::with_home("/Users/u").file("/Users/u/vault/.note.md.icloud");
        assert_eq!(
            detect(&fs).audio_summary,
            "1 sync system detected: iCloud Drive."
        );
    }

    #[test]
    fn empty_vault_detects_nothing() {
        let fs = FakeFs::with_home("/Users/u").dir(ROOT);
        let report = detect(&fs);
        assert!(report.providers.is_empty());
        assert_eq!(report.multi_sync_warning, None);
        assert_eq!(report.audio_summary, "No sync systems detected.");
        assert!(report.supported);
    }

    // --- Real-filesystem integration (marker-file arms only; the
    // xattr/$HOME-prefix arms are seam-tested above — CI runners can't
    // plant xattrs reliably) -------------------------------------------

    #[test]
    fn real_fs_detects_planted_markers() {
        let tmp = tempfile::tempdir().unwrap();
        let root = tmp.path();
        let plugin = root.join(".obsidian/plugins/obsidian-livesync");
        std::fs::create_dir_all(&plugin).unwrap();
        std::fs::write(plugin.join("manifest.json"), "{}").unwrap();
        std::fs::write(root.join(".note.md.icloud"), "").unwrap();
        std::fs::write(root.join(".dropbox"), "").unwrap();
        std::fs::write(root.join(ONEDRIVE_MARKER), "").unwrap();
        std::fs::create_dir(root.join(".tmp.driveupload")).unwrap();
        std::fs::create_dir(root.join(".git")).unwrap();
        std::fs::write(root.join(".stignore"), "").unwrap();

        let report = detect_sync_providers(root);
        assert_eq!(
            kinds(&report),
            vec![
                SyncProviderKind::LiveSync,
                SyncProviderKind::ICloudDrive,
                SyncProviderKind::Dropbox,
                SyncProviderKind::OneDrive,
                SyncProviderKind::GoogleDrive,
                SyncProviderKind::Git,
                SyncProviderKind::Syncthing,
            ]
        );
        assert!(report.supported);
    }

    #[test]
    fn real_fs_git_file_worktree_marker() {
        let tmp = tempfile::tempdir().unwrap();
        std::fs::write(
            tmp.path().join(".git"),
            "gitdir: /elsewhere/.git/worktrees/x",
        )
        .unwrap();
        let report = detect_sync_providers(tmp.path());
        assert_eq!(kinds(&report), vec![SyncProviderKind::Git]);
    }

    #[test]
    fn real_fs_clean_vault_detects_nothing() {
        let tmp = tempfile::tempdir().unwrap();
        std::fs::write(tmp.path().join("note.md"), "# hi").unwrap();
        let report = detect_sync_providers(tmp.path());
        assert!(
            report.providers.is_empty(),
            "clean tempdir vault must produce zero detections, got: {report:#?}"
        );
    }

    // --- Negative census -------------------------------------------------

    /// Deterministic split-mix RNG (census convention, no rand dep).
    struct SplitMix64(u64);
    impl SplitMix64 {
        fn next(&mut self) -> u64 {
            self.0 = self.0.wrapping_add(0x9E37_79B9_7F4A_7C15);
            let mut z = self.0;
            z = (z ^ (z >> 30)).wrapping_mul(0xBF58_476D_1CE4_E5B9);
            z = (z ^ (z >> 27)).wrapping_mul(0x94D0_49BB_1331_11EB);
            z ^ (z >> 31)
        }
        fn below(&mut self, n: usize) -> usize {
            (self.next() % n.max(1) as u64) as usize
        }
    }

    fn census_scale() -> u64 {
        if std::env::var("SLATE_CENSUS_FULL").as_deref() == Ok("1") {
            10_000
        } else {
            2_000
        }
    }

    /// 10k randomized lookalike vaults (SLATE_CENSUS_FULL=1; 2k in the
    /// default dev run) → zero detections on every one. The lookalike
    /// list is part of the fixture — extended whenever a false positive
    /// is ever found in the wild.
    #[test]
    fn census_sync_detect_no_false_positives() {
        // (name, is_dir) — every entry is a near-miss for some probe.
        const LOOKALIKES: &[(&str, bool)] = &[
            ("dropbox-notes.md", false),
            ("git", true),
            ("stfolder.md", false),
            ("OneDriveBackup.md", false), // a FILE named like the component
            (".gitignore", false),
            (".obsidian/plugins/other-plugin", true),
            (".obsidian/plugins/obsidian-livesync-notes", true), // prefix lookalike
            ("Dropbox", true),                                   // plain dir, not the marker file
            ("dropbox.cache", true),                             // missing the leading dot
            ("tmp.driveupload", true),                           // missing the leading dot
            ("notes-icloud.md", false),                          // contains "icloud", wrong suffix
            ("icloud", true),
            ("stignore", false), // missing the leading dot
            (".stversions-notes.md", false),
            ("OneDrive.md", false),
            (".849C9593-D756-4E56-8D6E-42412F2A707B-backup", false), // suffix ruins the marker
            (".849C9593-D756-4E56-8D6E-42412F2A707B", true), // exact GUID name but a DIR, not the file marker
            ("GoogleDrive-stuff", true), // component under the vault, not CloudStorage
            ("Library/Mobile Documents-notes", true),
        ];
        const NESTS: &[&str] = &["", "notes", "daily/2026", "projects/alpha/deep"];

        let mut rng = SplitMix64(0x5EED_5EED);
        for i in 0..census_scale() {
            let root = PathBuf::from(format!("/Users/u/vaults/v{i}"));
            let mut fs = FakeFs::with_home("/Users/u").dir(root.to_str().unwrap());
            let entries = rng.below(8) + 1;
            for _ in 0..entries {
                let (name, is_dir) = LOOKALIKES[rng.below(LOOKALIKES.len())];
                let nest = NESTS[rng.below(NESTS.len())];
                let path = if nest.is_empty() {
                    root.join(name)
                } else {
                    root.join(nest).join(name)
                };
                let path = path.to_str().unwrap().to_string();
                fs = if is_dir {
                    fs.dir(&path)
                } else {
                    fs.file(&path)
                };
            }
            let report = detect_with_probe(&root, &fs);
            assert!(
                report.providers.is_empty(),
                "census vault v{i} produced a false positive: {report:#?}"
            );
            assert_eq!(report.audio_summary, "No sync systems detected.");
        }
    }

    // --- LiveSync config reader (M-2, #533) ------------------------------

    /// Plant a `data.json` under a tempdir vault and read it back.
    fn vault_with_livesync_data(json: &[u8]) -> (tempfile::TempDir, LiveSyncConfigStatus) {
        let tmp = tempfile::tempdir().unwrap();
        let plugin = tmp.path().join(".obsidian/plugins/obsidian-livesync");
        std::fs::create_dir_all(&plugin).unwrap();
        std::fs::write(plugin.join("data.json"), json).unwrap();
        let status = read_livesync_config(tmp.path());
        (tmp, status)
    }

    /// The M-2 DoD credential gate: a realistic config with planted
    /// credentials round-trips, and the ENTIRE debug-formatted output
    /// contains none of the planted secrets. Structural allow-listing,
    /// not redaction.
    #[test]
    fn livesync_config_never_contains_planted_credentials() {
        let json = br#"{
            "couchDB_URI": "https://alice:hunter2@couch.example.com:5984/db",
            "couchDB_USER": "alice",
            "couchDB_PASSWORD": "hunter2",
            "couchDB_DBNAME": "obsidian-notes",
            "passphrase": "secret123",
            "liveSync": true,
            "syncOnSave": false,
            "syncOnStart": true,
            "encrypt": true,
            "customChunkSize": 100
        }"#;
        let (_tmp, status) = vault_with_livesync_data(json);
        let LiveSyncConfigStatus::Parsed(config) = &status else {
            panic!("expected Parsed, got {status:?}");
        };
        assert_eq!(
            config.server_host.as_deref(),
            Some("couch.example.com:5984")
        );
        assert_eq!(config.database.as_deref(), Some("obsidian-notes"));
        assert_eq!(config.live_sync_enabled, Some(true));
        assert_eq!(config.sync_on_save, Some(false));
        assert_eq!(config.sync_on_start, Some(true));
        assert_eq!(config.end_to_end_encryption, Some(true));

        let debug_dump = format!("{status:?}");
        for secret in ["alice", "hunter2", "secret123"] {
            assert!(
                !debug_dump.contains(secret),
                "credential {secret:?} leaked into output: {debug_dump}"
            );
        }
    }

    #[test]
    fn livesync_host_extraction_variants() {
        // Userinfo + port dropped down to host:port.
        assert_eq!(
            extract_host("https://user:pass@couch.example.com:5984/db"),
            Some("couch.example.com:5984".to_string())
        );
        // Bare IP, no path.
        assert_eq!(
            extract_host("http://10.0.0.5:5984"),
            Some("10.0.0.5:5984".to_string())
        );
        // Query/fragment never leak.
        assert_eq!(
            extract_host("https://h.example.com?token=x#frag"),
            Some("h.example.com".to_string())
        );
        // Garbage URIs → None.
        assert_eq!(extract_host("not a uri"), None);
        assert_eq!(extract_host(""), None);
        assert_eq!(extract_host("https://"), None);
        assert_eq!(extract_host("https://user@"), None);
    }

    #[test]
    fn livesync_garbage_uri_still_parses_with_no_host() {
        let (_tmp, status) =
            vault_with_livesync_data(br#"{"couchDB_URI": "garbage", "couchDB_DBNAME": "db"}"#);
        let LiveSyncConfigStatus::Parsed(config) = status else {
            panic!("expected Parsed");
        };
        assert_eq!(config.server_host, None);
        assert_eq!(config.database.as_deref(), Some("db"));
    }

    #[test]
    fn livesync_missing_file_is_not_present() {
        let tmp = tempfile::tempdir().unwrap();
        assert_eq!(
            read_livesync_config(tmp.path()),
            LiveSyncConfigStatus::NotPresent
        );
    }

    #[test]
    fn livesync_invalid_json_is_malformed_with_reason() {
        let (_tmp, status) = vault_with_livesync_data(b"{not json");
        let LiveSyncConfigStatus::Malformed { reason } = status else {
            panic!("expected Malformed, got {status:?}");
        };
        assert!(!reason.is_empty());
    }

    #[test]
    fn livesync_non_object_json_is_malformed() {
        let (_tmp, status) = vault_with_livesync_data(b"[1, 2, 3]");
        assert!(matches!(status, LiveSyncConfigStatus::Malformed { .. }));
    }

    #[test]
    fn livesync_partial_json_parses_with_nones() {
        let (_tmp, status) = vault_with_livesync_data(br#"{"couchDB_DBNAME": "only-db"}"#);
        let LiveSyncConfigStatus::Parsed(config) = status else {
            panic!("expected Parsed");
        };
        assert_eq!(config.database.as_deref(), Some("only-db"));
        assert_eq!(config.server_host, None);
        assert_eq!(config.live_sync_enabled, None);
        assert_eq!(config.sync_on_save, None);
        assert_eq!(config.sync_on_start, None);
        assert_eq!(config.end_to_end_encryption, None);
    }

    #[test]
    fn livesync_schema_drift_wrong_types_parse_as_none() {
        // The plugin's schema drifts: wrong-typed fields degrade to
        // None instead of failing the whole parse.
        let (_tmp, status) = vault_with_livesync_data(
            br#"{"liveSync": "yes", "couchDB_DBNAME": 42, "couchDB_URI": false}"#,
        );
        let LiveSyncConfigStatus::Parsed(config) = status else {
            panic!("expected Parsed");
        };
        assert_eq!(config.live_sync_enabled, None);
        assert_eq!(config.database, None);
        assert_eq!(config.server_host, None);
    }

    #[test]
    fn livesync_oversized_file_is_malformed() {
        let big = vec![b' '; (LIVESYNC_CONFIG_MAX_BYTES + 1) as usize];
        let (_tmp, status) = vault_with_livesync_data(&big);
        assert_eq!(
            status,
            LiveSyncConfigStatus::Malformed {
                reason: "config file too large".to_string()
            }
        );
    }

    /// Boundary: a file of exactly `MAX` bytes is under the bound and
    /// reaches the parser (spaces aren't valid JSON → `invalid JSON`,
    /// NOT `too large`). Proves the cap is inclusive, not off-by-one.
    #[test]
    fn livesync_at_bound_file_reaches_parser() {
        let at_bound = vec![b' '; LIVESYNC_CONFIG_MAX_BYTES as usize];
        let (_tmp, status) = vault_with_livesync_data(&at_bound);
        let LiveSyncConfigStatus::Malformed { reason } = status else {
            panic!("expected Malformed, got {status:?}");
        };
        assert!(
            reason.starts_with("invalid JSON"),
            "expected a parse failure, not the size guard: {reason}"
        );
    }

    /// A `data.json` that is a directory (not a regular file) is rejected
    /// before any read — a hostile vault can't make us treat a directory
    /// as config.
    #[test]
    fn livesync_directory_at_config_path_is_malformed() {
        let tmp = tempfile::tempdir().unwrap();
        let plugin = tmp.path().join(".obsidian/plugins/obsidian-livesync");
        std::fs::create_dir_all(plugin.join("data.json")).unwrap();
        assert_eq!(
            read_livesync_config(tmp.path()),
            LiveSyncConfigStatus::Malformed {
                reason: "config file is not a regular file".to_string(),
            }
        );
    }

    /// `data.json` symlinked to a character device (`/dev/null`):
    /// `/dev/null` is outside the vault, so the vault-escape guard
    /// refuses it before any read (and, had it been in-vault, the
    /// regular-file guard would still reject the char device). Either
    /// way the blocking-special-file read never runs.
    #[cfg(unix)]
    #[test]
    fn livesync_symlink_to_special_file_is_malformed() {
        let tmp = tempfile::tempdir().unwrap();
        let plugin = tmp.path().join(".obsidian/plugins/obsidian-livesync");
        std::fs::create_dir_all(&plugin).unwrap();
        std::os::unix::fs::symlink("/dev/null", plugin.join("data.json")).unwrap();
        assert_eq!(
            read_livesync_config(tmp.path()),
            LiveSyncConfigStatus::Malformed {
                reason: "config path escapes the vault".to_string(),
            }
        );
    }

    /// **Vault-escape credential guard (adversarial-review r4).** A
    /// hostile vault symlinks `data.json` at a regular JSON file OUTSIDE
    /// the vault (e.g. another user's real LiveSync config). The reader
    /// must refuse it — NO allow-listed field from the outside file may
    /// surface. Canonicalization lands the target outside the root and
    /// the escape guard fires.
    #[cfg(unix)]
    #[test]
    fn livesync_symlink_escaping_vault_is_refused() {
        let outside = tempfile::tempdir().unwrap();
        let secret = outside.path().join("other-vault-data.json");
        std::fs::write(
            &secret,
            br#"{"couchDB_URI": "https://secret.example.com:5984/x", "couchDB_DBNAME": "victim-db"}"#,
        )
        .unwrap();

        let tmp = tempfile::tempdir().unwrap();
        let plugin = tmp.path().join(".obsidian/plugins/obsidian-livesync");
        std::fs::create_dir_all(&plugin).unwrap();
        std::os::unix::fs::symlink(&secret, plugin.join("data.json")).unwrap();

        let status = read_livesync_config(tmp.path());
        assert_eq!(
            status,
            LiveSyncConfigStatus::Malformed {
                reason: "config path escapes the vault".to_string(),
            },
            "outside file must not be parsed"
        );
        // Belt and suspenders: nothing from the outside file leaked.
        let dump = format!("{status:?}");
        assert!(!dump.contains("secret.example.com"));
        assert!(!dump.contains("victim-db"));
    }

    /// A `data.json` symlinked to a regular JSON file that stays INSIDE
    /// the vault is legitimate and still read — the escape guard is
    /// scoped to escapes, not all symlinks.
    #[cfg(unix)]
    #[test]
    fn livesync_symlink_inside_vault_is_read() {
        let tmp = tempfile::tempdir().unwrap();
        let plugin = tmp.path().join(".obsidian/plugins/obsidian-livesync");
        std::fs::create_dir_all(&plugin).unwrap();
        // Real config elsewhere inside the vault; data.json points at it.
        let real = tmp.path().join("real-data.json");
        std::fs::write(&real, br#"{"couchDB_DBNAME": "inside-db"}"#).unwrap();
        std::os::unix::fs::symlink(&real, plugin.join("data.json")).unwrap();

        let LiveSyncConfigStatus::Parsed(config) = read_livesync_config(tmp.path()) else {
            panic!("expected Parsed for an in-vault symlink");
        };
        assert_eq!(config.database.as_deref(), Some("inside-db"));
    }

    /// `data.json` is a FIFO with no writer connected. A blocking
    /// `File::open` would hang here forever; the `O_NONBLOCK` in
    /// `open_guarded` returns immediately and the regular-file guard
    /// rejects it. If this test ever hangs, the non-blocking open
    /// regressed. (Direct FIFO — not via symlink — per the review.)
    #[cfg(unix)]
    #[test]
    fn livesync_fifo_at_config_path_does_not_hang() {
        let tmp = tempfile::tempdir().unwrap();
        let plugin = tmp.path().join(".obsidian/plugins/obsidian-livesync");
        std::fs::create_dir_all(&plugin).unwrap();
        let fifo = plugin.join("data.json");
        let c_path = std::ffi::CString::new(fifo.as_os_str().as_encoded_bytes()).unwrap();
        // 0o600 rw-------; return value 0 == success.
        let rc = unsafe { libc::mkfifo(c_path.as_ptr(), 0o600) };
        assert_eq!(rc, 0, "mkfifo failed: {}", std::io::Error::last_os_error());
        assert_eq!(
            read_livesync_config(tmp.path()),
            LiveSyncConfigStatus::Malformed {
                reason: "config file is not a regular file".to_string(),
            }
        );
    }

    /// Credential-safety regression: userinfo must never leak into
    /// `server_host` via a malformed authority. A path/query delimiter
    /// that appears *before* the `@` terminates the authority at the
    /// userinfo (RFC 3986), and a naive "drop up to `@`" would return
    /// the password. Strict `host[:port]` validation fails closed here.
    #[test]
    fn livesync_extract_host_fails_closed_on_userinfo_leak() {
        // A `@` stranded after the first authority delimiter means the
        // userinfo was split off by a malformed authority → fail closed,
        // for `/`, `?`, and `#`, whether the password is alphabetic…
        assert_eq!(
            extract_host("https://alice:hunter2/extra@couch.example.com/db"),
            None
        );
        assert_eq!(
            extract_host("https://alice:hunter2?x@couch.example.com/db"),
            None
        );
        assert_eq!(
            extract_host("https://alice:hunter2#f@couch.example.com/db"),
            None
        );
        // …or NUMERIC — the dangerous case, since `alice:1234` on its own
        // is shaped exactly like a valid `host:port`. The stranded `@`
        // is what proves it's userinfo; without this guard `1234` would
        // leak (adversarial-review r3 finding).
        assert_eq!(
            extract_host("https://alice:1234/extra@couch.example.com/db"),
            None
        );
        assert_eq!(
            extract_host("https://alice:1234?x@couch.example.com/db"),
            None
        );
        assert_eq!(
            extract_host("https://alice:1234#f@couch.example.com/db"),
            None
        );
        // Whitespace / control characters are not host-shaped → None.
        assert_eq!(extract_host("https://ali ce@host .com"), None);
        assert_eq!(extract_host("https://ho\tst.com"), None);
        assert_eq!(extract_host("https://host\n.com"), None);
        // A non-numeric password with no `@` at all is still not
        // host-shaped (`hunter2` isn't a port) → None.
        assert_eq!(extract_host("https://alice:hunter2"), None);
    }

    /// A bare `label:digits` authority with NO `@` anywhere is
    /// indistinguishable from a legitimate `host:port` (`localhost:5984`,
    /// an internal hostname, a container name) — CouchDB URIs routinely
    /// use exactly this. It is accepted as a host; only a stranded `@`
    /// (see the leak test) turns a `name:digits` value into userinfo.
    /// Documenting the deliberate boundary so it isn't "fixed" into an
    /// over-rejection that breaks real configs.
    #[test]
    fn livesync_extract_host_accepts_bare_host_port() {
        assert_eq!(
            extract_host("http://localhost:5984"),
            Some("localhost:5984".to_string())
        );
        assert_eq!(
            extract_host("http://couchdb:5984/db"),
            Some("couchdb:5984".to_string())
        );
    }

    /// The valid host shapes the extractor must still accept, including
    /// a bracketed IPv6 literal (defensive support) — regression guard
    /// against the strict validator over-rejecting.
    #[test]
    fn livesync_extract_host_accepts_valid_shapes() {
        assert_eq!(
            extract_host("https://couch.example.com:5984/db"),
            Some("couch.example.com:5984".to_string())
        );
        assert_eq!(
            extract_host("http://10.0.0.5:5984"),
            Some("10.0.0.5:5984".to_string())
        );
        assert_eq!(
            extract_host("https://user:pass@couch.example.com:5984/db"),
            Some("couch.example.com:5984".to_string())
        );
        assert_eq!(
            extract_host("https://host-name.internal"),
            Some("host-name.internal".to_string())
        );
        // Bracketed IPv6, with and without a port.
        assert_eq!(
            extract_host("https://[2001:db8::1]:5984/db"),
            Some("[2001:db8::1]:5984".to_string())
        );
        assert_eq!(extract_host("https://[::1]"), Some("[::1]".to_string()));
    }

    /// 1k random byte-strings through the parser — no panics; every
    /// outcome is a status variant (m_spec §M-2).
    #[test]
    fn livesync_fuzz_random_bytes_never_panic() {
        let mut rng = SplitMix64(0xF0CC_F0CC);
        for _ in 0..1_000 {
            let len = rng.below(512);
            let bytes: Vec<u8> = (0..len).map(|_| (rng.next() & 0xFF) as u8).collect();
            let status = parse_livesync_config(&bytes);
            match status {
                LiveSyncConfigStatus::Parsed(_) | LiveSyncConfigStatus::Malformed { .. } => {}
                LiveSyncConfigStatus::NotPresent => {
                    panic!("parser must never return NotPresent")
                }
            }
        }
    }
}
