# Stage 3d: add the accessibility analyzer to an existing toolchain VM, in
# place. Used once in Phase 0 because the first toolchain build predates the
# analyzer step; fresh builds get it from 03-toolchain.pkr.hcl.

packer {
  required_plugins {
    tart = {
      version = ">= 1.21.0"
      source  = "github.com/cirruslabs/tart"
    }
  }
}

variable "vm_name" {
  type    = string
  default = "slate-mac-toolchain"
}

variable "admin_password" {
  type      = string
  sensitive = true
}

variable "a11y_check_ref" {
  type    = string
  default = "bcaddd56931ce14d32cebcf42ea9f5b08ed5f7d8"
}

variable "net_args" {
  type    = list(string)
  default = ["--net-softnet-block=out @host"]
}

source "tart-cli" "analyzer" {
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
  sources = ["source.tart-cli.analyzer"]

  provisioner "shell" {
    inline_shebang = "/bin/bash -e"
    environment_vars = [
      "A11Y_CHECK_REF=${var.a11y_check_ref}",
      "ADMIN_PASSWORD=${var.admin_password}",
    ]
    script  = "scripts/toolchain-analyzer.sh"
    timeout = "30m"
  }
}
