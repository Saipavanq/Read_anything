# ADR 0001 — Snapshot settings on the main thread for the read worker

**Date:** 2026-11-21
**Status:** Accepted
**Context:** The `read-worker` thread called `engine_var.get()`, `mode_var.get()` and `volume_var.get()` on Tk variables owned by the main thread. Tkinter is not thread-safe; this can raise `RuntimeError: main thread is not in main loop`, corrupt widget state, or fail intermittently under load. The settings dialog was also created as a second `tk.Tk()` root inside a daemon thread, which has the same problem.

**Decision:** `App.request_read()` calls `snapshot_settings()` on the main thread, producing a plain, normalised `dict` of strings/numbers. That dict is queued with the read mode; `worker()` and `handle_request()` use only the dict. Settings dialogs are opened via a `__settings__` queue event and are `Toplevel` children of the main window, never a second `Tk` root. Setting changes persist immediately through `apply_setting()` on the main thread.

**Alternatives considered:**
1. Guard every Tk call with a lock — does not make Tk thread-safe; would still be fragile.
2. Move all reading work onto the main thread with `root.after()` — would block the UI during capture and network TTS.
3. Poll Tk variables from the worker with retry — hides the race rather than removing it.

**Consequences:** The worker receives a consistent view of settings as of the moment the read was requested. A setting changed mid-read affects later reads only, which is acceptable. Tests can exercise `snapshot_settings()` without a display. The `Toplevel` dialog is tied to the main window lifetime; closing the main window closes the dialog, which is intended.

**Verification:** `tests/test_settings_snapshot.py` asserts only plain values cross the boundary; a source scan confirms no Tk `.get()` calls remain in `worker()` or `handle_request()`.
