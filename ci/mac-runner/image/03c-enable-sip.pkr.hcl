# Optional stage 3c: turn System Integrity Protection back on.
# docs/plans/42_self_hosted_mac_runner_plan.md §4.6 says to try this after the
# TCC rows are written; if the Finder grants stop working, skip this stage and
# the guest keeps SIP off like GitHub's hosted images. Same mechanism as
# 02-disable-sip.pkr.hcl, operating in place on var.vm_name.

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
  default = "slate-mac-toolchain-stage"
}

variable "admin_password" {
  type      = string
  sensitive = true
}

source "tart-cli" "sip" {
  vm_name      = var.vm_name
  recovery     = true
  headless     = true
  cpu_count    = 8
  memory_gb    = 12
  communicator = "none"

  # Screen-synchronised like 02-disable-sip.pkr.hcl.
  boot_command = [
    "<wait 'Options'>",
    "<click 'Options'>",
    "<wait2s><click 'Continue'>",
    "<wait 'Utilities'>",
    "<wait5s><click 'Utilities'>",
    "<wait2s><click 'Terminal'>",
    "<wait 'bash-3.2'>",
    "<wait2s>csrutil enable<enter>",
    # csrutil enable may or may not ask a question first; the password prompt
    # is the one fixed point (see 02-disable-sip.pkr.hcl for the prompts).
    "<wait '(y/n|password for user admin)'>",
    "y<enter>",
    "<wait 'password for user admin'>",
    "${var.admin_password}<enter>",
    "<wait '(Successfully|restart)'>",
    "<wait3s>halt<enter>",
  ]
}

build {
  sources = ["source.tart-cli.sip"]
}
