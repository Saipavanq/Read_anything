"""Capture text selected in the foreground Windows application."""
from __future__ import annotations

import logging
import threading
import time
import uuid

import win32clipboard

from config import DEFAULTS

log = logging.getLogger("read_aloud")

SENTINEL_PREFIX = "__READ_ALOUD_EMPTY_"


def _clipboard_snapshot() -> dict:
    """Copy all clipboard formats so the user's clipboard can be restored."""
    data = {}
    try:
        win32clipboard.OpenClipboard()
    except Exception as exc:
        log.debug("Clipboard snapshot skipped: %s", exc)
        return {}
    try:
        fmt = 0
        while True:
            fmt = win32clipboard.EnumClipboardFormats(fmt)
            if fmt == 0:
                break
            try:
                data[fmt] = win32clipboard.GetClipboardData(fmt)
            except Exception as exc:
                log.debug("Skipped clipboard format %s: %s", fmt, exc)
    except Exception as exc:
        log.debug("Clipboard snapshot failed: %s", exc)
    finally:
        try:
            win32clipboard.CloseClipboard()
        except Exception:
            pass
    return data


def _restore_clipboard(snapshot: dict) -> None:
    if not snapshot:
        return
    try:
        win32clipboard.OpenClipboard()
        win32clipboard.EmptyClipboard()
        for fmt, value in snapshot.items():
            try:
                win32clipboard.SetClipboardData(fmt, value)
            except Exception as exc:
                log.debug("Could not restore clipboard format %s: %s", fmt, exc)
    except Exception as exc:
        log.debug("Clipboard restore failed: %s", exc)
    finally:
        try:
            win32clipboard.CloseClipboard()
        except Exception:
            pass


def _set_sentinel(text: str) -> None:
    win32clipboard.OpenClipboard()
    try:
        win32clipboard.EmptyClipboard()
        win32clipboard.SetClipboardText(text, win32clipboard.CF_UNICODETEXT)
    finally:
        win32clipboard.CloseClipboard()


def capture_via_clipboard(timeout: float = 1.2) -> str | None:
    """Simulate Ctrl+C, read the clipboard, then restore it."""
    from pynput.keyboard import Controller, Key

    snapshot = _clipboard_snapshot()
    sentinel = SENTINEL_PREFIX + uuid.uuid4().hex
    try:
        _set_sentinel(sentinel)
    except Exception as exc:
        log.warning("Could not prepare clipboard: %s", exc)
        _restore_clipboard(snapshot)
        return None

    # Give the previous hotkey time to fully release its modifiers.
    time.sleep(0.12)
    try:
        keyboard = Controller()
        with keyboard.pressed(Key.ctrl):
            keyboard.press("c")
            keyboard.release("c")
    except Exception as exc:
        log.warning("Could not send Ctrl+C: %s", exc)
        _restore_clipboard(snapshot)
        return None

    deadline = time.monotonic() + timeout
    text = sentinel
    while time.monotonic() < deadline:
        time.sleep(0.05)
        try:
            win32clipboard.OpenClipboard()
            try:
                text = win32clipboard.GetClipboardData(win32clipboard.CF_UNICODETEXT)
            finally:
                win32clipboard.CloseClipboard()
        except Exception:
            continue
        if text != sentinel:
            break

    _restore_clipboard(snapshot)

    if not text or text == sentinel or not text.strip():
        return None
    return text.strip()


def _uia_worker(result: list) -> None:
    import pythoncom
    import uiautomation as auto

    pythoncom.CoInitialize()
    try:
        control = auto.GetFocusedControl()
        if control is None:
            return
        node = control
        for _ in range(10):
            if node is None:
                break
            try:
                pattern = node.GetPattern(auto.PatternId.TextPattern)
            except Exception:
                pattern = None
            if pattern:
                try:
                    ranges = pattern.GetSelection()
                    parts = [r.GetText(-1) for r in ranges]
                    text = "\n".join(p for p in parts if p).strip()
                    if text:
                        result.append(text)
                        return
                except Exception as exc:
                    log.debug("TextPattern selection failed: %s", exc)
            try:
                node = node.Parent
            except Exception:
                break
    except Exception as exc:
        log.debug("UIA capture failed: %s", exc)
    finally:
        pythoncom.CoUninitialize()


def capture_via_uia(timeout: float = 1.5) -> str | None:
    """Try reading the selection through UI Automation without touching the clipboard."""
    result: list[str] = []
    thread = threading.Thread(target=_uia_worker, args=(result,), daemon=True)
    thread.start()
    thread.join(timeout)
    return result[0] if result else None


def capture_selected_text(method: str | None = None) -> str | None:
    method = (method or DEFAULTS["capture_method"]).lower()
    if method in ("auto", "uia"):
        text = capture_via_uia()
        if text:
            log.debug("Selection captured through UI Automation (%d chars)", len(text))
            return text
        if method == "uia":
            return None
    text = capture_via_clipboard()
    if text:
        log.debug("Selection captured through clipboard (%d chars)", len(text))
    return text
