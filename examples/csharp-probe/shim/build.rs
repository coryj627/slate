// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

fn main() {
    println!("cargo:rerun-if-changed=src/lib.rs");
    csbindgen::Builder::default()
        .input_extern_file("src/lib.rs")
        .csharp_dll_name("slate_csabi_shim")
        .csharp_namespace("SlateShim")
        .csharp_class_name("NativeMethods")
        .generate_csharp_file("../ShimProbe/generated/NativeMethods.g.cs")
        .expect("csbindgen generation failed");
}
