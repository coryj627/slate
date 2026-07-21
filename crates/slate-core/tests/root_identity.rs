// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! FL-06 round-27: a filesystem session records the physical identity
//! of the root it opened, as one observation made inside the open
//! itself. Hosts anchor their own per-surface root checks to it.

#[cfg(unix)]
use slate_core::VaultSession;

#[cfg(unix)]
#[test]
fn filesystem_open_observes_the_roots_physical_identity() {
    use std::os::unix::fs::MetadataExt;
    let tmp = tempfile::tempdir().unwrap();
    let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
    let identity = session.root_identity().expect("unix observes an identity");
    let meta = std::fs::metadata(tmp.path()).unwrap();
    assert_eq!(identity.device, meta.dev());
    assert_eq!(identity.inode, meta.ino());

    let other = tempfile::tempdir().unwrap();
    let second = VaultSession::from_filesystem(other.path().to_path_buf()).unwrap();
    assert_ne!(
        session.root_identity(),
        second.root_identity(),
        "distinct roots observe distinct identities"
    );
}
