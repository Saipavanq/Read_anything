# SDD ledger — plan: docs/superpowers/plans/2026-11-21-readaloud-reliability.md

Execution mode: native (inline), user asked to build directly.
Branch: reliability-pass (created from baseline d5d18d9).

Ruling: use a feature branch in the current working tree instead of a separate git worktree — the user's packaged app output and config live in this directory and a worktree would duplicate them; the branch still gives a clean review range. Cost if wrong: none for the app; only that the working directory is shared.
Ruling: ReadAloud.spec was not added to the baseline commit because .gitignore excludes *.spec — it is a PyInstaller-generated artifact and build.bat regenerates it. Cost if wrong: none.

Final review outcome (independent review of d5d18d9..9a446d3; verdict was "Ready to merge? No"): all Critical and Important findings are fixed and verified locally. No push or merge was performed.

- Fixed (Critical, startup): `ttk.Combobox` was created with an invalid `command=` option, raising `_tkinter.TclError: unknown option "-command"` before the window could be built. Added `make_combobox()` using the supported `<<ComboboxSelected>>` event. RED: packaged smoke test produced a 0-byte log and source run showed the TclError. GREEN: `tests/test_window.py::ComboboxTests::test_combobox_created_with_event_binding` and packaged startup wrote the expected log line.
- Fixed (Critical #1): `__settings__` carried a callback in `message` but fell through to `message[:70]`, raising `TypeError` and killing the `root.after` poll loop. `poll_events()` now uses `continue` for every internal event and ignores unknown `__*` tokens. RED: `test_settings_event_stays_off_activity_log` error; `test_internal_events_stay_off_activity_log` saw three leaked log lines. GREEN: both pass; status/activity remain reserved for user-facing messages.
- Fixed (Critical #2): `request_read()` read Tk variables and stopped TTS from pynput/pystray/voice threads. It now only enqueues `("__read__", mode)`; `poll_events()` takes the snapshot, stops TTS and queues `(mode, snapshot)` on the main thread. RED: `test_request_read_only_queues_main_thread_event` hit the wrong-thread guard; `test_read_event_snapshot_and_stop_happen_on_main_thread` saw no TTS stop. GREEN: both pass.
- Fixed (Important #3): tray `set_engine`/`set_mode` called Tk `.set()` from the pystray thread. They now enqueue `__engine__`/`__mode__` events applied by `_apply_engine`/`_apply_mode` in `poll_events()`. RED: `test_tray_setters_only_queue_main_thread_events` hit the wrong-thread guard; `test_engine_and_mode_events_apply_on_main_thread` saw no variable writes. GREEN: both pass.
- Fixed (Important #4): packaged smoke-test evidence is now recorded below: rebuilt exe stayed alive for six seconds and `dist\ReadAloud\logs\app.log` was 116 bytes with the startup line.
- Fixed (Minor): README no longer claims the activity panel shows request IDs; `PLAN.md` status no longer says implementation is underway. No code path changed.
- Deferred (Minor, deferred): `settings_ui.save()` does not pass values through `normalise_config`; load and read snapshots still normalise, and all UI controls emit bounded values, so the persisted file can briefly hold out-of-range manual text until the next load. Cost if wrong: a manually typed `volume=200` is saved as 200 but read as 100.
- Deferred (Minor, deferred): `tts._speak_edge()` does not call `pygame.mixer.music.unload()` before deleting the temporary mp3; Windows may leave an orphaned temp file if the mixer still holds a handle. Cost if wrong: small temp-file accumulation after edge-tts playback.
- Deferred (Minor, deferred): `main.py` still uses `setup_logging()` with the default console handler in frozen builds; the handler is skipped when `sys.stdout is None`, so this is cosmetic rather than a startup risk.
- Ruling: the startup TclError was found after the earlier build, so that build was discarded and rebuilt after all event-dispatch fixes. Cost if wrong: none; the delivered `dist\ReadAloud` now matches the tested source.

Final verification: `.venv\Scripts\python.exe -m unittest discover -s tests` → 53 tests OK. `.\build.bat` → "Build complete!". Smoke test: `dist\ReadAloud\ReadAloud.exe` modified 2026-11-21 19:07:04, `RUNNING=True`, `LOG=...dist\ReadAloud\logs\app.log Size=116`, containing `Read Aloud window started. Read=Ctrl+Alt+R Voice=Ctrl+Alt+V Stop=Ctrl+Alt+X`. Review blockers remaining: none in the reviewed scope.

Task 6: Ruling: the plan claimed voice._get_model had broken double-checked locking; it already had the inner check. The real gap was silent model-load failure, so error logging was added and the plan's claim was not acted on. Cost if wrong: none.
Task 6: Ruling: the clipboard-warning test initially asserted on the log format string rather than its arguments; corrected to assert call_args[0][1] == 2. Cost if wrong: none.
Task 6: complete (commit 08d2936, tests: .venv\Scripts\python.exe -m unittest discover -s tests → 46 tests OK). RED: 2 errors (setup_logging signature) + 2 failures (clipboard warning, voice log).

Task 5: complete (commit a759fa8, tests: .venv\Scripts\python.exe -m unittest discover -s tests → 42 tests OK). RED: ImportError for valid_base_url; GREEN after implementation.

Task 4: complete (commit c386006, tests: .venv\Scripts\python.exe -m unittest discover -s tests → 36 tests OK). RED: 1 failure (load called after stop) + 1 error (missing generation attribute); GREEN after implementation. Added comtypes to requirements.txt (build-time, no test per TDD config exception).

Task 3: Ruling: the plan asked for the settings dialog to be dispatched on the main thread; a second `tk.Tk()` root is still unsafe even there, so the dialog is now a `Toplevel` parented to the main window, opened via a `__settings__` queue event. Cost if wrong: none for the app; the dialog is now tied to the main window's lifetime, which is the intended behaviour.
Task 3: complete (commit 2dbaaa9, tests: .venv\Scripts\python.exe -m unittest discover -s tests → 32 tests OK). RED: ImportError for snapshot_settings; GREEN after implementation. Verified no Tk variable reads remain in worker()/handle_request().

Task 2: Ruling: the plan's example "ctrl+alt+f13 → <ctrl>+<alt>+f13" was inconsistent with to_pynput's documented behaviour for multi-character keys; f13 correctly becomes <f13>, and the test asserts that. Cost if wrong: none — pynput requires angle brackets for function keys.
Task 2: complete (commit f65e04b, tests: .venv\Scripts\python.exe -m unittest discover -s tests → 28 tests OK). RED: ImportError for validate_hotkeys; GREEN after implementation.

Task 1: Ruling: the plan says "clamp numeric ranges", but nonsensical non-positive values for max_chars/voice_seconds/llm_timeout should revert to defaults while bounded UI values (volume, sapi_rate) clamp to bounds — the test expected this and it is safer than a 1-character limit or 0.5s listen. Cost if wrong: a user who deliberately sets voice_seconds=0 gets 4.0 instead.
Task 1: complete (commit 6aee48f, tests: .venv\Scripts\python.exe -m unittest discover -s tests → 18 tests OK). RED had 9 assertion failures; GREEN after implementation.

Pre-flight: no shared interfaces conflict found. Task 1 produces `normalise_config` consumed by Task 3's snapshot (Task 3 only reads config dict values, no signature dependency). Task 2 produces `validate_hotkeys` consumed by Task 3 indirectly through `HotkeyManager.start` (already called there). Task 4's `TTSManager.generation` is internal to Task 4. Task 5's `valid_base_url` is internal to Task 5. Task 6's `setup_logging(include_console)` is consumed by Task 6 only. All signatures match the plan.
