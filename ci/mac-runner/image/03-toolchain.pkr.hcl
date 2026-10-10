# Stage 3 of the toolchain layer: Xcode, accounts, runner, agent, Rust.
# docs/plans/42_self_hosted_mac_runner_plan.md §4.4 and §4.5.
#
# Input: slate-mac-base. Output: var.vm_name, shut down with auto-login
# switched to the runner account. Stage 3b (03b-toolchain-tcc.pkr.hcl) boots
# it again to write the Finder grants into that account's TCC database.
#
# Everything the guest receives comes from the host's cache, verified there:
# the host's own Xcode as a tar, the Actions runner and guest agent release
# tarballs, and rustup-init. The guest downloads only the Rust toolchain.

packer {
  required_plugins {
    tart = {
      version = ">= 1.21.0"
      source  = "github.com/cirruslabs/tart"
    }
  }
}

variable "base_vm" {
  type    = string
  default = "slate-mac-base"
}

variable "vm_name" {
  type    = string
  default = "slate-mac-toolchain-stage"
}

variable "admin_password" {
  type      = string
  sensitive = true
}

variable "runner_password" {
  type      = string
  sensitive = true
}

variable "builder_password" {
  type      = string
  sensitive = true
}

variable "builder_pubkey" {
  type        = string
  description = "ssh-ed25519 public key the host uses to reach the builder account."
}

variable "xcode_tar" {
  type        = string
  description = "Host path of the Xcode.app tarball."
}

variable "xcode_build" {
  type    = string
  default = "27A266a"
}

variable "runner_tar" {
  type = string
}

variable "agent_tar" {
  type = string
}

variable "rustup_init" {
  type = string
}

variable "rust_version" {
  type    = string
  default = "1.97.1"
}

variable "a11y_check_ref" {
  type = string
  # The commit a11y-check.yml pins (A11Y_CHECK_REF).
  default = "bcaddd56931ce14d32cebcf42ea9f5b08ed5f7d8"
}

variable "net_args" {
  type = list(string)
  # Build mode: the guest may not open connections to the host, but the host
  # may still SSH in (plan §4.4). Jobs use "@host" instead.
  default = ["--net-softnet-block=out @host"]
}

source "tart-cli" "toolchain" {
  vm_base_name   = var.base_vm
  vm_name        = var.vm_name
  cpu_count      = 8
  memory_gb      = 12
  headless       = true
  ssh_username   = "admin"
  ssh_password   = var.admin_password
  ssh_timeout    = "15m"
  run_extra_args = var.net_args
}

build {
  sources = ["source.tart-cli.toolchain"]

  provisioner "shell" {
    inline_shebang = "/bin/bash -e"
    inline = [
      "sudo -n /usr/bin/true",
      # The file provisioner needs existing destination directories, or a
      # one-file directory upload lands as a file of that name.
      "rm -rf ~/stage && mkdir -p ~/stage/data ~/stage/hooks",
      # Stage 3b writes TCC rows, which needs SIP off (02-disable-sip).
      "csrutil status | tee /dev/stderr | grep -q disabled",
    ]
  }

  provisioner "file" {
    source      = var.xcode_tar
    destination = "/Users/admin/stage/Xcode.tar"
  }

  provisioner "file" {
    source      = var.runner_tar
    destination = "/Users/admin/stage/actions-runner.tar.gz"
  }

  provisioner "file" {
    source      = var.agent_tar
    destination = "/Users/admin/stage/tart-guest-agent.tar.gz"
  }

  provisioner "file" {
    source      = var.rustup_init
    destination = "/Users/admin/stage/rustup-init.sh"
  }

  provisioner "file" {
    source      = "data/"
    destination = "/Users/admin/stage/data"
  }

  # The admission hook lives in ci/mac-runner/hook with its tests.
  provisioner "file" {
    source      = "../hook/"
    destination = "/Users/admin/stage/hooks"
  }

  provisioner "shell" {
    inline_shebang   = "/bin/bash -e"
    environment_vars = ["XCODE_BUILD=${var.xcode_build}"]
    script           = "scripts/toolchain-xcode.sh"
  }

  provisioner "shell" {
    inline_shebang = "/bin/bash -e"
    environment_vars = [
      "RUNNER_PASSWORD=${var.runner_password}",
      "BUILDER_PASSWORD=${var.builder_password}",
      "BUILDER_PUBKEY=${var.builder_pubkey}",
    ]
    script = "scripts/toolchain-accounts.sh"
  }

  provisioner "shell" {
    inline_shebang   = "/bin/bash -e"
    environment_vars = ["RUST_VERSION=${var.rust_version}"]
    script           = "scripts/toolchain-runner.sh"
  }

  provisioner "shell" {
    inline_shebang   = "/bin/bash -e"
    environment_vars = ["A11Y_CHECK_REF=${var.a11y_check_ref}"]
    script           = "scripts/toolchain-analyzer.sh"
    timeout          = "30m"
  }

  provisioner "shell" {
    inline_shebang = "/bin/bash -e"
    inline         = ["rm -rf ~/stage"]
  }
}
