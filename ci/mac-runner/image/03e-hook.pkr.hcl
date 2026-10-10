# Stage 3e: refresh the admission hook in an existing toolchain image, in
# place, without rebuilding the layer (the hook is root-owned in the guest,
# so a job or the warm build cannot change it; only an image build can).
# After this, rebuild the warm layer so jobs get it. See image/refresh-hook.sh.

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

variable "net_args" {
  type    = list(string)
  default = ["--net-softnet-block=out @host"]
}

source "tart-cli" "hook" {
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
  sources = ["source.tart-cli.hook"]

  provisioner "shell" {
    inline_shebang = "/bin/bash -e"
    inline         = ["rm -rf ~/hook-stage && mkdir -p ~/hook-stage"]
  }

  provisioner "file" {
    source      = "../hook/"
    destination = "/Users/admin/hook-stage"
  }

  provisioner "file" {
    source      = "data/runner.env"
    destination = "/Users/admin/hook-stage/runner.env"
  }

  provisioner "shell" {
    inline_shebang   = "/bin/bash -e"
    environment_vars = ["ADMIN_PASSWORD=${var.admin_password}"]
    inline = [
      "as_root() { printf '%s\\n' \"$ADMIN_PASSWORD\" | sudo -S -p '' \"$@\"; }",
      "as_root install -o root -g wheel -m 755 ~/hook-stage/job-started.sh /usr/local/slate-runner/hooks/job-started.sh",
      "as_root install -o root -g wheel -m 644 ~/hook-stage/admission.py  /usr/local/slate-runner/hooks/admission.py",
      "as_root install -o runner -g staff -m 644 ~/hook-stage/runner.env /Users/runner/actions-runner/.env",
      "echo '.env:'; sed 's/^/  /' /Users/runner/actions-runner/.env",
      "/usr/bin/python3 -I -c 'import ast,sys; ast.parse(open(sys.argv[1]).read())' /usr/local/slate-runner/hooks/admission.py",
      "bash -n /usr/local/slate-runner/hooks/job-started.sh",
      "rm -rf ~/hook-stage",
      "stat -f '%Su %Sp %N' /usr/local/slate-runner/hooks/job-started.sh /usr/local/slate-runner/hooks/admission.py",
      "grep -c 'kill -KILL' /usr/local/slate-runner/hooks/job-started.sh | sed 's/^/kill lines: /'",
    ]
  }
}
