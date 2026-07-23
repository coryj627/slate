// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! W1-RT-14 bounded directory-page performance evidence.
//!
//! Kept separate from `scan_bench` so a targeted run does not construct that
//! target's unrelated 10,000-file fixtures before Criterion applies filters.

use std::hint::black_box;

use criterion::{Criterion, criterion_group, criterion_main};
use slate_core::{CancelToken, Paging, VaultSession};

fn bench_directory_pages(c: &mut Criterion) {
    let vault = tempfile::tempdir().expect("directory benchmark vault");
    for index in 0..10_000 {
        std::fs::create_dir(vault.path().join(format!("folder-{index:05}")))
            .expect("create benchmark directory");
    }
    let session = VaultSession::from_filesystem(vault.path().to_path_buf()).expect("open");
    session
        .scan_initial(&CancelToken::new())
        .expect("scan directories");

    let mut group = c.benchmark_group("directory_page_10k");
    group.bench_function("first_200", |b| {
        b.iter(|| {
            let page = session
                .list_dir_children_page("", Paging::first(200), &CancelToken::new())
                .expect("first bounded directory page");
            assert_eq!(page.dirs.len() + page.files.len(), 200);
            black_box(page)
        });
    });

    let mut cursor = None;
    let mut middle_cursor = None;
    for page_index in 0..49 {
        let page = session
            .list_dir_children_page(
                "",
                Paging {
                    cursor: cursor.take(),
                    limit: 200,
                },
                &CancelToken::new(),
            )
            .expect("prime directory cursor");
        cursor = page.next_cursor;
        if page_index == 24 {
            middle_cursor = cursor.clone();
        }
    }
    let middle_cursor = middle_cursor.expect("10k-directory level has a middle page");
    group.bench_function("middle_200", |b| {
        b.iter(|| {
            let page = session
                .list_dir_children_page(
                    "",
                    Paging::after(middle_cursor.clone(), 200),
                    &CancelToken::new(),
                )
                .expect("middle bounded directory page");
            assert_eq!(page.dirs.len() + page.files.len(), 200);
            black_box(page)
        });
    });

    let late_cursor = cursor.expect("10k-directory level has a late page");
    group.bench_function("late_200", |b| {
        b.iter(|| {
            let page = session
                .list_dir_children_page(
                    "",
                    Paging::after(late_cursor.clone(), 200),
                    &CancelToken::new(),
                )
                .expect("late bounded directory page");
            assert_eq!(page.dirs.len() + page.files.len(), 200);
            black_box(page)
        });
    });
    group.finish();
}

criterion_group! {
    name = benches;
    config = Criterion::default().sample_size(10);
    targets = bench_directory_pages
}
criterion_main!(benches);
