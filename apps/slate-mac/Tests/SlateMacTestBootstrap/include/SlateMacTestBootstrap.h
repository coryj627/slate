// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

// #1455: the mac test bundle's load-time bootstrap (bootstrap.c), which
// routes the test process's Trash into a scratch bin before any test runs,
// and (plan 43 PR 20c) sets the vault watcher's test switch for the run.
// It has no API: nothing imports this module, the test target only links it.

#ifndef SLATE_MAC_TEST_BOOTSTRAP_H
#define SLATE_MAC_TEST_BOOTSTRAP_H
#endif
