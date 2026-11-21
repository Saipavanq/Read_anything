"""Integration test for the main window's combobox construction."""
import os
import sys
import tkinter as tk
from tkinter import ttk
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src"))

from main import make_combobox


class ComboboxTests(unittest.TestCase):
    def test_combobox_created_with_event_binding(self):
        root = tk.Tk()
        root.withdraw()
        try:
            var = tk.StringVar(master=root, value="offline")
            combo = make_combobox(
                root, var, ["offline", "edge"], 14, lambda _event: None
            )
            self.assertIsInstance(combo, ttk.Combobox)
            self.assertIn("<<ComboboxSelected>>", combo.bind())
        finally:
            root.destroy()


if __name__ == "__main__":
    unittest.main()
