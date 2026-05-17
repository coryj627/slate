# YANA project commands.
#
# Thin wrapper around scripts/ and standard cargo invocations.
# Assumes `cargo` is on PATH (install via rustup; the rustup
# installer adds the env file to your shell rc, so a fresh shell
# picks it up). If you hit "cargo: command not found", source
# the rustup env once: `. "$HOME/.cargo/env"`.

.PHONY: help check test fmt fmt-check clippy bench-check ci swift-cli mac-app mac-app-run bench clean

help:
	@echo "YANA — common commands"
	@echo
	@echo "  make check         cargo check --workspace"
	@echo "  make test          cargo test --workspace (8 tests as of this writing)"
	@echo "  make fmt           cargo fmt --all"
	@echo "  make fmt-check     cargo fmt --check (fails if unformatted)"
	@echo "  make clippy        cargo clippy --all-targets --workspace -- -D warnings"
	@echo "  make bench-check   cargo bench --no-run (compile benches without executing)"
	@echo "  make ci            fmt-check + clippy + test + bench-check"
	@echo "  make swift-cli     build + run the Swift command-line smoke test"
	@echo "  make mac-app       build the SwiftUI smoke-test app"
	@echo "  make mac-app-run   build + launch the SwiftUI smoke-test app"
	@echo "  make bench         run the criterion benchmark suite (BENCHMARKS.md baseline)"
	@echo "  make clean         cargo clean + remove SwiftPM build artifacts"
	@echo
	@echo "See README.md and docs/plans/05_locked_architecture_decisions.md for context."

check:
	cargo check --workspace

test:
	cargo test --workspace

fmt:
	cargo fmt --all

fmt-check:
	cargo fmt --all --check

clippy:
	cargo clippy --all-targets --workspace -- -D warnings

bench-check:
	cargo bench -p yana-core --bench scan_bench --no-run

ci: fmt-check clippy test bench-check

swift-cli:
	./scripts/build-swift-cli.sh

mac-app:
	./scripts/build-mac-app.sh

mac-app-run:
	./scripts/build-mac-app.sh --run

# Criterion suite for yana-core. Full run is ~15–20 min on a modern
# laptop because the 50k-file cold scan dominates. Pass extra args
# through with BENCH_ARGS, e.g.:
#   make bench BENCH_ARGS='--bench scan_bench first_open_and_scan/1000'
bench:
	cargo bench -p yana-core --bench scan_bench -- $(BENCH_ARGS)

clean:
	cargo clean
	rm -rf apps/yana-mac/.build apps/yana-mac/.swiftpm target/generated target/swift-cli
