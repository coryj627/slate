# YANA project commands.
#
# Thin wrapper around scripts/ and standard cargo invocations.
# Assumes `cargo` is on PATH (install via rustup; the rustup
# installer adds the env file to your shell rc, so a fresh shell
# picks it up). If you hit "cargo: command not found", source
# the rustup env once: `. "$HOME/.cargo/env"`.

.PHONY: help check test fmt fmt-check clippy ci swift-cli mac-app mac-app-run clean

help:
	@echo "YANA — common commands"
	@echo
	@echo "  make check         cargo check --workspace"
	@echo "  make test          cargo test --workspace (8 tests as of this writing)"
	@echo "  make fmt           cargo fmt --all"
	@echo "  make fmt-check     cargo fmt --check (fails if unformatted)"
	@echo "  make clippy        cargo clippy --all-targets --workspace -- -D warnings"
	@echo "  make ci            fmt-check + clippy + test"
	@echo "  make swift-cli     build + run the Swift command-line smoke test"
	@echo "  make mac-app       build the SwiftUI smoke-test app"
	@echo "  make mac-app-run   build + launch the SwiftUI smoke-test app"
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

ci: fmt-check clippy test

swift-cli:
	./scripts/build-swift-cli.sh

mac-app:
	./scripts/build-mac-app.sh

mac-app-run:
	./scripts/build-mac-app.sh --run

clean:
	cargo clean
	rm -rf apps/yana-mac/.build apps/yana-mac/.swiftpm target/generated target/swift-cli
