# Stage 2b of the base layer: boot normally and prove SIP is off.
# The recovery-mode typing in 02-disable-sip.pkr.hcl runs blind to the
# builder, so the result is checked here before the layer is promoted.

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
  default = "slate-mac-base-stage1"
}

variable "admin_password" {
  type      = string
  sensitive = true
}

variable "expect" {
  type    = string
  default = "disabled"
}

source "tart-cli" "verify" {
  vm_name      = var.vm_name
  cpu_count    = 8
  memory_gb    = 12
  headless     = true
  ssh_username = "admin"
  ssh_password = var.admin_password
  ssh_timeout  = "10m"
}

build {
  sources = ["source.tart-cli.verify"]

  provisioner "shell" {
    inline_shebang   = "/bin/bash -e"
    environment_vars = ["EXPECT=${var.expect}"]
    inline = [
      "status=\"$(csrutil status)\"; echo \"$status\"",
      "echo \"$status\" | grep -q \"$EXPECT\" || { echo \"expected SIP $EXPECT\" >&2; exit 1; }",
    ]
  }
}
