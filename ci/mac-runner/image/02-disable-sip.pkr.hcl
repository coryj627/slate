# Stage 2 of the base layer: turn System Integrity Protection off.
# docs/plans/42_self_hosted_mac_runner_plan.md §4.6.
#
# The toolchain layer writes Finder Apple Events grants straight into the
# guest's TCC databases, which needs SIP off, as GitHub's own hosted macOS
# images and Cirrus's base images have it. Operates in place on var.vm_name.
#
# Copied from cirruslabs/macos-image-templates' disable-sip-with-username
# template, with the password supplied as a variable instead of "admin".
# The keystrokes go to the VM over Virtualization.framework's VNC server.

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

source "tart-cli" "sip" {
  vm_name      = var.vm_name
  recovery     = true
  headless     = true
  cpu_count    = 8
  memory_gb    = 12
  communicator = "none"

  # The plugin reads the VM's screen: <wait 'text'> blocks until the text
  # shows, <click 'text'> clicks it. Cirrus's fixed waits typed blind and, on
  # this host, left SIP enabled (Phase 0 log), so every step here waits for
  # what it expects to see.
  boot_command = [
    # Startup options: pick "Options", then "Continue", to enter Recovery.
    "<wait 'Options'>",
    "<click 'Options'>",
    "<wait2s><click 'Continue'>",
    # Recovery is up when its menu bar shows Utilities. Open Terminal from it.
    "<wait 'Utilities'>",
    "<wait5s><click 'Utilities'>",
    "<wait2s><click 'Terminal'>",
    # The shell prompt in Recovery's Terminal reads "-bash-3.2#".
    "<wait 'bash-3.2'>",
    "<wait2s>csrutil disable<enter>",
    # Prompts as read from the screen on macOS 27.0.1 (26A434), Phase 0 log:
    #   Allow booting unsigned operating systems ... for OS "Macintosh HD"? [y/n]:
    #   Enter password for user admin:
    # There is no separate username prompt on this build.
    "<wait 'Allow booting'>",
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
