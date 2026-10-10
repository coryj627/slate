# Stage 4: the warm layer. docs/plans/42_self_hosted_mac_runner_plan.md §4.4
# and §4.5.
#
# Input: slate-mac-toolchain. Output: var.vm_name with main checked out at the
# runner's work path and fully built, so a PR job only rebuilds what changed.
#
# Trust boundary: main's own build scripts run here, as the builder account.
# Packer connects as admin (sudo asks for the password, which only the host
# holds); admin hands the workspace to builder before the build and to runner
# after it, and refreshes the Actions runner from a host-verified tarball.
# builder never gets sudo, cannot write runner's home or anything root owns,
# and is never logged in, so nothing it leaves behind runs before a job's
# admission hook (§4.4).

packer {
  required_plugins {
    tart = {
      version = ">= 1.21.0"
      source  = "github.com/cirruslabs/tart"
    }
  }
}

variable "toolchain_vm" {
  type    = string
  default = "slate-mac-toolchain"
}

variable "vm_name" {
  type    = string
  default = "slate-mac-warm-stage"
}

variable "admin_password" {
  type      = string
  sensitive = true
}

variable "repo_url" {
  type    = string
  default = "https://github.com/coryj627/slate.git"
}

variable "ref" {
  type    = string
  default = "main"
}

variable "runner_tar" {
  type        = string
  description = "Host-verified Actions runner tarball to install over the existing runner."
}

variable "cpu_count" {
  type    = number
  default = 12
}

variable "memory_gb" {
  type    = number
  default = 16
}

variable "net_args" {
  type    = list(string)
  default = ["--net-softnet-block=out @host"]
}

source "tart-cli" "warm" {
  vm_base_name   = var.toolchain_vm
  vm_name        = var.vm_name
  cpu_count      = var.cpu_count
  memory_gb      = var.memory_gb
  headless       = true
  ssh_username   = "admin"
  ssh_password   = var.admin_password
  ssh_timeout    = "15m"
  run_extra_args = var.net_args
}

build {
  sources = ["source.tart-cli.warm"]

  provisioner "file" {
    source      = var.runner_tar
    destination = "/Users/admin/actions-runner.tar.gz"
  }

  # One script, three accounts: admin prepares, builder builds, admin hands off.
  provisioner "shell" {
    inline_shebang = "/bin/bash -e"
    environment_vars = [
      "ADMIN_PASSWORD=${var.admin_password}",
      "REPO_URL=${var.repo_url}",
      "REF=${var.ref}",
    ]
    script  = "scripts/warm-build.sh"
    timeout = "90m"
  }
}
