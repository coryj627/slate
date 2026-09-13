# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later

import importlib.util
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch


SPEC = importlib.util.spec_from_file_location(
    "canvas_trigger_ledger", Path(__file__).resolve().parents[1] / "canvas_trigger_ledger.py"
)
ledger = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(ledger)


class CanvasTriggerLedgerTests(unittest.TestCase):
    def test_local_generated_swift_does_not_supply_host_evidence(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / "Sources"
            source.mkdir()
            binding = root / "binding.cs"
            binding.write_text(
                "public record CanvasStatusNote {\n}\n"
                "public record CanvasBlockedReason {\n}\n"
                "public enum CanvasMutationRefusal: int {\nReadOnly,\n}\n"
                "public enum CanvasFailedAction: int {\nCanvasAction,\n}\n",
                encoding="utf-8",
            )
            (source / "Document.swift").write_text(
                "struct Document {\n"
                "    var loadNotice: CanvasA11yEvent {\n"
                "        .canvasLoadedReadOnly(available: 3)\n"
                "    }\n}\n",
                encoding="utf-8",
            )
            with patch.object(ledger, "MAC_SRC", source), patch.object(ledger, "BINDING", binding):
                expected = {"CanvasLoadedReadOnly": {"Document.swift#loadNotice"}}
                self.assertEqual(expected, ledger.mac_scan())
                codec = (
                    "struct Codec {\n"
                    "    func read() {\n"
                    "        .canvasLoadedReadOnly(available: 3)\n"
                    "        .canvasViewportNoPane\n"
                    "    }\n}\n"
                )
                (source / "slate_uniffi.swift").write_text(codec, encoding="utf-8")
                generated = source / "generated"
                generated.mkdir()
                (generated / "OtherCodec.swift").write_text(codec, encoding="utf-8")
                self.assertEqual(expected, ledger.mac_scan())


if __name__ == "__main__":
    unittest.main()
