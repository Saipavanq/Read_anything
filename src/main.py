"""Read Aloud — Windows desktop application with a visible window."""
from __future__ import annotations

import logging
import os
import queue
import sys
import threading
import tkinter as tk
from tkinter import ttk

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import pystray
from PIL import Image, ImageDraw
from pynput import keyboard

from config import load_config, save_config
from hotkeys import describe, to_pynput
from llm import LocalLLM
from logging_setup import setup_logging
from selection import capture_selected_text
from settings_ui import open_settings
from tts import TTSManager
from voice import VoiceCommandListener

log: logging.Logger = setup_logging()
ui_queue: queue.Queue = queue.Queue()
_STOP = object()

ENGINES = ["offline", "edge"]
MODES = ["verbatim", "summarize", "simplify", "translate", "custom"]


def notify(title: str, message: str) -> None:
    """Send a message to the window log and, if present, the tray icon."""
    ui_queue.put((title, message))
    log.info("%s: %s", title, message)
    app = globals().get("app")
    if app is not None and app.icon is not None:
        try:
            app.icon.notify(message, title)
        except Exception as exc:
            log.debug("Tray notification failed: %s", exc)


class HotkeyManager:
    def __init__(self, config: dict, actions: dict):
        self.config = config
        self.actions = actions
        self.listener = None

    def start(self) -> None:
        mapping = {
            to_pynput(self.config["hotkey_read"]): self.actions["read"],
            to_pynput(self.config["hotkey_voice"]): self.actions["voice"],
            to_pynput(self.config["hotkey_stop"]): self.actions["stop"],
        }
        try:
            self.listener = keyboard.GlobalHotKeys(mapping)
            self.listener.start()
            log.info(
                "Hotkeys registered: read=%s voice=%s stop=%s",
                self.config["hotkey_read"],
                self.config["hotkey_voice"],
                self.config["hotkey_stop"],
            )
        except Exception as exc:
            log.exception("Could not register hotkeys")
            notify("Hotkeys", f"Registration failed: {exc}")

    def stop(self) -> None:
        if self.listener is not None:
            try:
                self.listener.stop()
            except Exception:
                pass
            self.listener = None

    def rebuild(self) -> None:
        self.stop()
        self.start()


class App:
    def __init__(self, root: tk.Tk):
        self.root = root
        self.config = load_config()
        self.tts = TTSManager(self.config)
        self.llm = LocalLLM(self.config)
        self.voice = VoiceCommandListener(self.config)
        self.requests_queue: queue.Queue = queue.Queue()
        self.icon: pystray.Icon | None = None
        self._closing = False

        self.engine_var = tk.StringVar(value=self.config.get("tts_engine", "offline"))
        self.mode_var = tk.StringVar(value=self.config.get("llm_mode", "verbatim"))
        self.volume_var = tk.IntVar(value=int(self.config.get("volume", 100)))
        self.status_var = tk.StringVar(value="Ready")

        self.hotkeys = HotkeyManager(
            self.config,
            {
                "read": self.request_read,
                "voice": self.start_voice_command,
                "stop": self.stop_speaking,
            },
        )

        self._build_window()
        self._start_workers()

    # ---------- UI ----------
    def _build_window(self) -> None:
        self.root.title("Read Aloud — Selected Text Speech")
        self.root.geometry("640x560")
        self.root.minsize(560, 480)
        self.root.configure(padx=10, pady=10)

        header = ttk.Frame(self.root)
        header.pack(fill=tk.X, pady=(0, 8))
        ttk.Label(
            header,
            text="Read Aloud",
            font=("Segoe UI", 16, "bold"),
        ).pack(side=tk.LEFT)
        ttk.Label(
            header,
            textvariable=self.status_var,
            foreground="#2563a8",
        ).pack(side=tk.LEFT, padx=(16, 0))

        ttk.Label(
            self.root,
            text="Select text in any application, then press Ctrl+Alt+R.",
        ).pack(anchor=tk.W, pady=(0, 10))

        buttons = ttk.Frame(self.root)
        buttons.pack(fill=tk.X, pady=4)
        ttk.Button(buttons, text="Read selected text", command=self.request_read).pack(
            side=tk.LEFT, padx=(0, 6)
        )
        ttk.Button(buttons, text="Voice command…", command=self.start_voice_command).pack(
            side=tk.LEFT, padx=(0, 6)
        )
        ttk.Button(buttons, text="Stop speaking", command=self.stop_speaking).pack(
            side=tk.LEFT
        )

        controls = ttk.LabelFrame(self.root, text="Speech", padding=10)
        controls.pack(fill=tk.X, pady=10)
        ttk.Label(controls, text="Engine:").grid(row=0, column=0, sticky=tk.W)
        ttk.Combobox(
            controls,
            textvariable=self.engine_var,
            values=ENGINES,
            state="readonly",
            width=14,
        ).grid(row=0, column=1, sticky=tk.W, padx=(8, 20))
        ttk.Label(controls, text="Reading mode:").grid(row=0, column=2, sticky=tk.W)
        ttk.Combobox(
            controls,
            textvariable=self.mode_var,
            values=MODES,
            state="readonly",
            width=14,
        ).grid(row=0, column=3, sticky=tk.W, padx=(8, 0))

        ttk.Label(controls, text="Volume:").grid(row=1, column=0, sticky=tk.W, pady=(10, 0))
        scale = ttk.Scale(
            controls,
            from_=0,
            to=100,
            variable=self.volume_var,
            orient=tk.HORIZONTAL,
            length=260,
            command=self._on_volume,
        )
        scale.grid(row=1, column=1, columnspan=2, sticky=tk.W, padx=(8, 0), pady=(10, 0))
        self.volume_label = ttk.Label(controls, text=str(self.volume_var.get()) + "%")
        self.volume_label.grid(row=1, column=3, sticky=tk.W, padx=(10, 0), pady=(10, 0))

        bottom = ttk.Frame(self.root)
        bottom.pack(fill=tk.X, pady=(0, 8))
        ttk.Button(bottom, text="Settings…", command=self.open_settings).pack(
            side=tk.LEFT, padx=(0, 6)
        )
        ttk.Button(bottom, text="Hide to tray", command=self.hide_to_tray).pack(
            side=tk.LEFT, padx=(0, 6)
        )
        ttk.Button(bottom, text="Open log", command=self.open_log).pack(side=tk.LEFT)

        log_frame = ttk.LabelFrame(self.root, text="Activity", padding=6)
        log_frame.pack(fill=tk.BOTH, expand=True)
        self.log_text = tk.Text(log_frame, height=12, wrap=tk.WORD, state=tk.DISABLED)
        self.log_text.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)
        scrollbar = ttk.Scrollbar(log_frame, command=self.log_text.yview)
        scrollbar.pack(side=tk.RIGHT, fill=tk.Y)
        self.log_text.configure(yscrollcommand=scrollbar.set)

        self.root.protocol("WM_DELETE_WINDOW", self.on_close)
        self._append_log("Read Aloud", "Window ready. Ready to read selected text.")

    def _on_volume(self, _value) -> None:
        self.volume_label.configure(text=str(int(self.volume_var.get())) + "%")

    def _append_log(self, title: str, message: str) -> None:
        self.log_text.configure(state=tk.NORMAL)
        self.log_text.insert(tk.END, f"{title}: {message}\n")
        lines = self.log_text.get("1.0", tk.END).splitlines()
        if len(lines) > 200:
            self.log_text.delete("1.0", f"{len(lines) - 200}.0")
        self.log_text.see(tk.END)
        self.log_text.configure(state=tk.DISABLED)

    def poll_events(self) -> None:
        while True:
            try:
                title, message = ui_queue.get_nowait()
            except queue.Empty:
                break
            if title == "__refresh__":
                self.refresh_controls()
            elif title == "__show__":
                self.root.deiconify()
                self.root.lift()
            elif title == "__quit__":
                self.shutdown()
                self.root.destroy()
                return
            self._append_log(title, message)
            self.status_var.set(message[:70])
        self.root.after(120, self.poll_events)

    def refresh_controls(self) -> None:
        self.engine_var.set(self.config.get("tts_engine", "offline"))
        self.mode_var.set(self.config.get("llm_mode", "verbatim"))
        self.volume_var.set(int(self.config.get("volume", 100)))
        self.volume_label.configure(text=str(int(self.volume_var.get())) + "%")

    # ---------- Actions ----------
    def request_read(self, mode: str | None = None, *_args) -> None:
        self.tts.stop()
        self.requests_queue.put(mode)

    def stop_speaking(self, *_args) -> None:
        self.tts.stop()
        notify("Read Aloud", "Speech stopped")

    def start_voice_command(self, *_args) -> None:
        def run() -> None:
            try:
                action, mode = self.voice.listen_once()
                if action == "stop":
                    self.stop_speaking()
                elif action == "read":
                    self.request_read(mode)
                elif action == "unknown":
                    notify("Voice command", f"Heard “{mode}”. Try “read this”.")
                else:
                    notify("Voice command", "Nothing heard.")
            except Exception as exc:
                log.exception("Voice command failed")
                notify("Voice command", f"Could not listen: {exc}")

        threading.Thread(target=run, daemon=True, name="voice-command").start()

    def handle_request(self, mode: str | None) -> None:
        try:
            self.config["tts_engine"] = self.engine_var.get()
            self.config["llm_mode"] = self.mode_var.get()
            self.config["volume"] = int(self.volume_var.get())

            text = capture_selected_text(self.config.get("capture_method"))
            if not text:
                notify("Read Aloud", "No text selected. Highlight text, then press the hotkey.")
                return

            limit = int(self.config.get("max_chars", 4000))
            if len(text) > limit:
                text = text[:limit]
                notify("Read Aloud", f"Selection truncated to {limit} characters.")

            effective_mode = mode or self.config.get("llm_mode", "verbatim")
            if effective_mode != "verbatim":
                if not self.config.get("llm_enabled"):
                    notify("Read Aloud", "Local LLM is disabled; reading verbatim.")
                    effective_mode = "verbatim"
                else:
                    text, error = self.llm.process(text, effective_mode)
                    if error:
                        notify("Read Aloud", error)

            notify("Read Aloud", f"Reading {len(text)} characters…")
            self.tts.speak(text)
        except Exception as exc:
            log.exception("Failed to handle read request")
            notify("Read Aloud", f"Error: {exc}")

    def worker(self) -> None:
        while True:
            mode = self.requests_queue.get()
            if mode is _STOP:
                break
            while True:
                try:
                    self.requests_queue.get_nowait()
                except queue.Empty:
                    break
            self.handle_request(mode)

    # ---------- Tray ----------
    def make_icon_image(self) -> Image.Image:
        image = Image.new("RGBA", (64, 64), (0, 0, 0, 0))
        draw = ImageDraw.Draw(image)
        draw.ellipse((3, 3, 61, 61), fill=(32, 102, 170, 255))
        draw.polygon(
            [(17, 27), (25, 27), (36, 17), (36, 47), (25, 37), (17, 37)],
            fill=(255, 255, 255, 255),
        )
        draw.arc((38, 20, 54, 44), start=295, end=65, fill=(255, 255, 255, 255), width=3)
        return image

    def build_menu(self) -> pystray.Menu:
        engine_menu = pystray.Menu(
            pystray.MenuItem(
                "Offline Windows voice",
                lambda *_: self.set_engine("offline"),
                checked=lambda item: self.config.get("tts_engine") == "offline",
                radio=True,
            ),
            pystray.MenuItem(
                "Online natural voice",
                lambda *_: self.set_engine("edge"),
                checked=lambda item: self.config.get("tts_engine") == "edge",
                radio=True,
            ),
        )
        mode_menu = pystray.Menu(
            *[
                pystray.MenuItem(
                    label,
                    lambda *_, value=value: self.set_mode(value),
                    checked=lambda item, value=value: self.config.get("llm_mode") == value,
                    radio=True,
                )
                for label, value in [
                    ("Verbatim", "verbatim"),
                    ("Summarize", "summarize"),
                    ("Simplify", "simplify"),
                    ("Translate", "translate"),
                    ("Custom prompt", "custom"),
                ]
            ]
        )
        return pystray.Menu(
            pystray.MenuItem("Show window", lambda *_: self.show_window(), default=True),
            pystray.MenuItem("Read selected text", lambda *_: self.request_read()),
            pystray.MenuItem("Voice command…", lambda *_: self.start_voice_command()),
            pystray.MenuItem("Stop speaking", lambda *_: self.stop_speaking()),
            pystray.Menu.SEPARATOR,
            pystray.MenuItem("Speech engine", engine_menu),
            pystray.MenuItem("Reading mode", mode_menu),
            pystray.Menu.SEPARATOR,
            pystray.MenuItem("Settings…", lambda *_: self.open_settings()),
            pystray.MenuItem("Quit", lambda *_: self.quit_from_tray()),
        )

    def start_tray(self) -> None:
        try:
            self.icon = pystray.Icon(
                "read_aloud", self.make_icon_image(), "Read Aloud", self.build_menu()
            )
            threading.Thread(target=self.icon.run, daemon=True, name="tray").start()
        except Exception as exc:
            log.warning("Tray icon could not start: %s", exc)

    def show_window(self) -> None:
        ui_queue.put(("__show__", ""))

    def hide_to_tray(self) -> None:
        self.root.withdraw()
        notify("Read Aloud", "Hidden to tray. Use the tray icon to restore.")

    def set_engine(self, engine: str) -> None:
        self.engine_var.set(engine)
        self.config["tts_engine"] = engine
        save_config(self.config)
        ui_queue.put(("__refresh__", ""))
        notify("Read Aloud", f"Speech engine: {engine}")

    def set_mode(self, mode: str) -> None:
        self.mode_var.set(mode)
        self.config["llm_mode"] = mode
        save_config(self.config)
        ui_queue.put(("__refresh__", ""))
        notify("Read Aloud", f"Reading mode: {mode}")

    # ---------- Settings / lifecycle ----------
    def open_settings(self, *_args) -> None:
        def saved(cfg: dict) -> None:
            self.hotkeys.rebuild()
            ui_queue.put(("__refresh__", ""))
            notify("Read Aloud", "Settings saved; hotkeys refreshed.")

        open_settings(self.config, saved)

    def open_log(self) -> None:
        from config import LOG_DIR

        os.startfile(str(LOG_DIR / "app.log"))

    def _start_workers(self) -> None:
        threading.Thread(target=self.worker, daemon=True, name="read-worker").start()
        self.hotkeys.start()
        self.start_tray()
        self.root.after(120, self.poll_events)

    def shutdown(self) -> None:
        if self._closing:
            return
        self._closing = True
        self.tts.stop()
        self.hotkeys.stop()
        self.requests_queue.put(_STOP)
        if self.icon is not None:
            try:
                self.icon.stop()
            except Exception:
                pass

    def on_close(self) -> None:
        self.shutdown()
        self.root.destroy()

    def quit_from_tray(self) -> None:
        ui_queue.put(("__quit__", ""))


def main() -> None:
    global app
    root = tk.Tk()
    app = App(root)
    log.info(
        "Read Aloud window started. Read=%s Voice=%s Stop=%s",
        describe(app.config["hotkey_read"]),
        describe(app.config["hotkey_voice"]),
        describe(app.config["hotkey_stop"]),
    )
    root.mainloop()


if __name__ == "__main__":
    main()
