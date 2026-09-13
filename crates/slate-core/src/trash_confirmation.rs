// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Session-local, single-use authorization for a staged Trash inventory.

use crate::vault::TrashSnapshot;
use crate::{BatchTrashRequest, StructuralBatchItem, VaultError, VaultProvider};

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct StagedTrashItem {
    pub item: StructuralBatchItem,
    pub item_count: u64,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct StagedTrash {
    pub token: u64,
    pub items: Vec<StagedTrashItem>,
}

#[derive(Debug)]
pub(crate) struct TrashConfirmation {
    pub token: u64,
    pub request: BatchTrashRequest,
    snapshots: Vec<TrashSnapshot>,
}

pub(crate) fn preserve_cancellation(error: VaultError) -> VaultError {
    if matches!(error, VaultError::Cancelled) {
        error
    } else {
        stale()
    }
}

pub(crate) fn stale() -> VaultError {
    VaultError::TrashConfirmationChanged {
        message: "Files changed or Trash confirmation expired. Request deletion again".into(),
    }
}

impl TrashConfirmation {
    pub fn capture(
        provider: &dyn VaultProvider,
        request: BatchTrashRequest,
        cancel: &crate::CancelToken,
    ) -> Result<Self, VaultError> {
        cancel.check()?;
        static NEXT_TOKEN: std::sync::atomic::AtomicU64 = std::sync::atomic::AtomicU64::new(1);
        let token = NEXT_TOKEN
            .fetch_update(
                std::sync::atomic::Ordering::Relaxed,
                std::sync::atomic::Ordering::Relaxed,
                |n| n.checked_add(1),
            )
            .map_err(|_| stale())?;
        let mut snapshots = Vec::new();
        let mut total = 0;
        for item in &request.items {
            let snapshot = provider.trash_snapshot_cancellable(&item.path, cancel)?;
            if item.is_directory != snapshot.is_directory {
                return Err(stale());
            }
            total += snapshot.item_count + 1;
            if total > 100_000 {
                return Err(VaultError::InvalidArgument {
                    message: "Too many items to verify for Trash; select a smaller group".into(),
                });
            }
            snapshots.push(snapshot);
        }
        cancel.check()?;
        Ok(Self {
            token,
            request,
            snapshots,
        })
    }

    pub fn staged(&self) -> StagedTrash {
        StagedTrash {
            token: self.token,
            items: self
                .request
                .items
                .iter()
                .zip(&self.snapshots)
                .map(|(item, snapshot)| StagedTrashItem {
                    item: item.clone(),
                    item_count: snapshot.item_count,
                })
                .collect(),
        }
    }

    #[cfg(test)]
    pub fn validate(
        &self,
        provider: &dyn VaultProvider,
        request: &BatchTrashRequest,
    ) -> Result<(), VaultError> {
        self.validate_cancellable(provider, request, &crate::CancelToken::new())
    }

    pub fn validate_cancellable(
        &self,
        provider: &dyn VaultProvider,
        request: &BatchTrashRequest,
        cancel: &crate::CancelToken,
    ) -> Result<(), VaultError> {
        cancel.check()?;
        if *request != self.request {
            return Err(stale());
        }
        for item in &request.items {
            self.validate_item_cancellable(provider, &item.path, cancel)?;
        }
        Ok(())
    }

    pub fn validate_item_cancellable(
        &self,
        provider: &dyn VaultProvider,
        path: &str,
        cancel: &crate::CancelToken,
    ) -> Result<(), VaultError> {
        cancel.check()?;
        let index = self
            .request
            .items
            .iter()
            .position(|item| item.path == path)
            .ok_or_else(stale)?;
        if provider
            .trash_snapshot_cancellable(path, cancel)
            .map_err(preserve_cancellation)?
            != self.snapshots[index]
        {
            return Err(stale());
        }
        Ok(())
    }
}
