# Stage 3b of the toolchain layer: Finder grants, then tighten admin's sudo.
# docs/plans/42_self_hosted_mac_runner_plan.md §4.6.
#
# Boots var.vm_name in place. The runner account is auto-logged in by now,
# so its per-user TCC database exists and scripts/toolchain-tcc.sh can find
# it the way GitHub's and Cirrus's image scripts do, through tccd's open files.
# The last step replaces admin's build-time passwordless sudo with a rule for
# shutdown only, so at run time sudo asks for the password only the host holds.

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

variable "net_args" {
  type    = list(string)
  default = ["--net-softnet-block=out @host"]
}

source "tart-cli" "tcc" {
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
  sources = ["source.tart-cli.tcc"]

  provisioner "shell" {
    inline_shebang = "/bin/bash -e"
    script         = "scripts/toolchain-tcc.sh"
  }

  provisioner "shell" {
    inline_shebang = "/bin/bash -e"
    inline = [
      "echo '==> Record'",
      "{ echo \"built=$(date -u +%Y-%m-%dT%H:%M:%SZ)\"; echo \"macos=$(sw_vers -productVersion) ($(sw_vers -buildVersion))\"; echo \"layer=toolchain\"; echo \"xcode=$(/usr/libexec/PlistBuddy -c 'Print :ProductBuildVersion' /Applications/Xcode.app/Contents/version.plist)\"; echo \"rust=$(sudo -u runner env CARGO_HOME=/Users/runner/toolchains/cargo RUSTUP_HOME=/Users/runner/toolchains/rustup /Users/runner/toolchains/cargo/bin/rustc --version)\"; echo \"runner=$(sudo -u runner /Users/runner/actions-runner/bin/Runner.Listener --version 2>/dev/null | tail -1 || echo unknown)\"; echo \"agent=$(/usr/local/bin/tart-guest-agent --version 2>&1 | head -1)\"; echo \"sip=$(csrutil status)\"; } | sudo tee /etc/slate-image-info >/dev/null",
      "cat /etc/slate-image-info",
      "echo '==> admin: sudo now asks for a password, except for shutdown'",
      "sudo /bin/sh -c \"printf 'admin ALL=(ALL) NOPASSWD: /sbin/shutdown\\n' > /etc/sudoers.d/90-admin-build && chmod 440 /etc/sudoers.d/90-admin-build && visudo -c -q\"",
      "sudo -n /usr/bin/true && { echo 'admin still has passwordless sudo'; exit 1; } || echo 'ok: admin sudo needs a password'",
    ]
  }
}
