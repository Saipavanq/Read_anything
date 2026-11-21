"""Tests for local LLM prompt building and endpoint discovery."""
import os
import sys
import unittest
from unittest.mock import Mock, patch

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src"))

import requests
from config import DEFAULTS
from llm import CANDIDATES, LocalLLM, build_messages, valid_base_url


class LLMPromptTests(unittest.TestCase):
    def test_translate_prompt(self):
        cfg = dict(DEFAULTS, llm_translate_to="Telugu")
        messages = build_messages("hello", "translate", cfg)
        self.assertIn("Telugu", messages[0]["content"])
        self.assertEqual("hello", messages[1]["content"])

    def test_input_truncation(self):
        messages = build_messages("x" * 10000, "summarize", dict(DEFAULTS))
        self.assertEqual(6000, len(messages[1]["content"]))


class BaseUrlTests(unittest.TestCase):
    def test_invalid_url_rejected(self):
        self.assertFalse(valid_base_url("ftp://example.com"))
        self.assertFalse(valid_base_url("not-a-url"))
        self.assertFalse(valid_base_url(""))

    def test_valid_url_accepted(self):
        self.assertTrue(valid_base_url("http://localhost:1234/v1"))
        self.assertTrue(valid_base_url("https://example.com/v1"))


class DiscoveryTests(unittest.TestCase):
    def test_discover_falls_back_to_candidates(self):
        cfg = dict(DEFAULTS, llm_base_url="http://localhost:9999/v1")

        def fake_get(url, timeout=None):
            if "9999" in url:
                raise requests.ConnectionError("configured endpoint is down")
            response = Mock()
            response.ok = True
            response.json.return_value = {"data": [{"id": "test-model"}]}
            return response

        with patch("llm.requests.get", side_effect=fake_get):
            base, models, label = LocalLLM(cfg).discover()
        self.assertEqual(CANDIDATES[0][1], base)
        self.assertEqual(["test-model"], models)
        self.assertEqual(CANDIDATES[0][0], label)

    def test_process_without_server_reads_verbatim(self):
        cfg = dict(DEFAULTS, llm_enabled=True)
        manager = LocalLLM(cfg)
        with patch.object(manager, "discover", return_value=("", [], "")):
            text, error = manager.process("hello", "summarize")
        self.assertEqual("hello", text)
        self.assertIn("No local LLM server", error)


if __name__ == "__main__":
    unittest.main()
