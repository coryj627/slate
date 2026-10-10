# Stage 3f: Python for actions/setup-python, added in place to an existing
# toolchain image (scripts/toolchain-python.sh). Fresh toolchain builds get
# the same step from 03-toolchain.pkr.hcl. Follow with a warm rebuild.

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

variable "python_series" {
  type    = string
  default = "3.13"
}

variable "net_args" {
  type    = list(string)
  default = ["--net-softnet-block=out @host"]
}

source "tart-cli" "python" {
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
  sources = ["source.tart-cli.python"]

  provisioner "shell" {
    inline_shebang   = "/bin/bash -e"
    environment_vars = ["ADMIN_PASSWORD=${var.admin_password}", "PYTHON_SERIES=${var.python_series}"]
    script           = "scripts/toolchain-python.sh"
    timeout          = "20m"
  }
}
