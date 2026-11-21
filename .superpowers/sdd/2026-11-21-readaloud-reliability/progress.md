# SDD ledger — plan: docs/superpowers/plans/2026-11-21-readaloud-reliability.md

Execution mode: native (inline), user asked to build directly.
Branch: reliability-pass (created from baseline d5d18d9).

Ruling: use a feature branch in the current working tree instead of a separate git worktree — the user's packaged app output and config live in this directory and a worktree would duplicate them; the branch still gives a clean review range. Cost if wrong: none for the app; only that the working directory is shared.
Ruling: ReadAloud.spec was not added to the baseline commit because .gitignore excludes *.spec — it is a PyInstaller-generated artifact and build.bat regenerates it. Cost if wrong: none.

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
