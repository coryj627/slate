# Stage 1 of the base layer: first boot and base settings.
# docs/plans/42_self_hosted_mac_runner_plan.md §4.5.
#
# Input: a never-booted VM created from Apple's IPSW
#   (tart create slate-mac-vanilla --from-ipsw ... --disk-format asif).
# Output: the clone named var.vm_name, booted once, with the admin account,
#   Remote Login and the settings in scripts/base-settings.sh.
#
# Modeled on cirruslabs/macos-image-templates' vanilla-golden-gate template,
# without its GUI steps: Gatekeeper stays on and Screen Sharing stays off.

packer {
  required_plugins {
    tart = {
      version = ">= 1.21.0"
      source  = "github.com/cirruslabs/tart"
    }
  }
}

variable "vanilla_vm" {
  type    = string
  default = "slate-mac-vanilla"
}

variable "vm_name" {
  type    = string
  default = "slate-mac-base-stage1"
}

variable "admin_password" {
  type      = string
  sensitive = true
  # Letters and digits only: it travels inside a comma-separated option.
  validation {
    condition     = can(regex("^[A-Za-z0-9]{16,64}$", var.admin_password))
    error_message = "The admin_password must be 16 to 64 letters and digits."
  }
}

source "tart-cli" "provision" {
  vm_base_name = var.vanilla_vm
  vm_name      = var.vm_name
  cpu_count    = 8
  memory_gb    = 12
  headless     = true

  ssh_username = "admin"
  ssh_password = var.admin_password
  # First boot of macOS 27 includes the automatic Setup Assistant run.
  ssh_timeout = "25m"

  # Tart's guest provisioning API (macOS 27 host and guest, Tart 2.33+): on the
  # clone's first boot it creates the admin account, logs it in and turns on
  # Remote Login, so no keystrokes are typed into Setup Assistant.
  run_extra_args = [
    "--provisioning-opts=fullName=Slate CI admin,username=admin,password=${var.admin_password},logsInAutomatically=true,enablesRemoteLogin=true",
  ]

  # Virtualization.framework may still be finishing the install after
  # tart create returns.
  create_grace_time = "30s"
}

build {
  sources = ["source.tart-cli.provision"]

  # Passwordless sudo for admin while images are built. The toolchain layer
  # removes it again (§4.4), so at run time sudo asks for the password that
  # only the host holds.
  provisioner "shell" {
    inline_shebang   = "/bin/bash -e"
    environment_vars = ["ADMIN_PASSWORD=${var.admin_password}"]
    inline = [
      "printf '%s\\n' \"$ADMIN_PASSWORD\" | sudo -S -p '' /bin/sh -c 'mkdir -p /etc/sudoers.d && printf \"admin ALL=(ALL) NOPASSWD: ALL\\n\" > /etc/sudoers.d/90-admin-build && chmod 440 /etc/sudoers.d/90-admin-build && visudo -c -q'",
      "sudo -n /usr/bin/true",
    ]
  }

  provisioner "shell" {
    inline_shebang   = "/bin/bash -e"
    environment_vars = ["ADMIN_PASSWORD=${var.admin_password}"]
    script           = "scripts/base-settings.sh"
  }

  provisioner "shell" {
    inline_shebang = "/bin/bash -e"
    inline = [
      "sw_vers",
      "echo \"spotlight: $(mdutil -s / 2>&1 | tail -1)\"",
      "echo \"gatekeeper: $(spctl --status)\"",
      "echo \"sip: $(csrutil status)\"",
      "echo \"dns: $(networksetup -getdnsservers Ethernet 2>/dev/null | tr '\\n' ' ')\"",
    ]
  }
}
