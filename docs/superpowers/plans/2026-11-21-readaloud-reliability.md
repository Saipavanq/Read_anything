# Read Aloud Reliability Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the existing Read Aloud desktop app thread-safe, well-tested, observable, hardened, and consistently configured, plus light UI polish, then rebuild and verify the packaged exe.

**Architecture:** Keep the current design: `main.py` owns a Tk main-loop `App`, a `read-worker` thread processes read requests, a `HotkeyManager` wraps `pynput.GlobalHotKeys`, and separate modules handle config/capture/TTS/LLM/voice. The key change is that no worker thread ever touches a Tk variable: the main thread snapshots settings into a plain dict and puts it on the queue. All new tests use stdlib `unittest` + `unittest.mock`.

**Tech Stack:** Python 3.10 (`.venv`), tkinter, pystray, pynput, pywin32/win32clipboard, comtypes, pygame, requests, faster-whisper, PyInstaller; `unittest` + `unittest.mock`.

**Spec:** `docs/superpowers/specs/2026-11-21-readaloud-reliability-design.md`

## Global Constraints

- Python 3.10 at `.venv\Scripts\python.exe`; tests run with `.venv\Scripts\python.exe -m unittest discover -s tests`.
- Windows 10 only; default configuration must stay fully offline.
- `config/settings.json` format stays backward compatible; unknown keys are ignored, defaults fill gaps.
- No new third-party runtime dependency except promoting `comtypes` to an explicit entry in `requirements.txt`.
- No new test framework: stdlib `unittest` and `unittest.mock` only.
- Do not claim success without running the commands in the verification steps.
- Repository is not yet git-initialised; Task 0 creates it so later review has a baseline.

## Review Focus

- **Corrupt or hostile `settings.json`** (wrong types, out-of-range volume, negative timeouts) → app must start with safe clamped defaults, never crash. Test: `tests/test_config.py::test_invalid_values_are_clamped`.
- **Two configured hotkeys the same / malformed combo** → no action silently lost, user notified, app keeps running. Test: `tests/test_hotkeys.py::test_duplicate_hotkeys_rejected`.
- **Read requested while settings dialog is open or while speaking** → no Tk calls from worker threads, no crash. Test: `tests/test_settings_snapshot.py::test_snapshot_contains_only_plain_values`.
- **Network/audio failure in edge-tts or no SAPI device** → fall back offline and log, never raise to the UI. Test: `tests/test_tts.py::test_edge_failure_falls_back`.
- **LLM endpoint configured but down** → try LM Studio/Ollama candidates, then read verbatim with a clear message. Test: `tests/test_llm.py::test_discover_falls_back_to_candidates`.

---

### Task 0: Establish review baseline (git)

**Files:**
- Modify: `.gitignore` (already correct; verify only)
- Create: git repository with initial commit of current source

- [ ] **Step 1: Verify ignore rules cover generated output**

Run: `Get-Content .gitignore`
Expected: contains `.venv/`, `__pycache__/`, `*.pyc`, `logs/`, `config/settings.json`, `build/`, `dist/`, `*.spec`.

- [ ] **Step 2: Initialise repository and commit the current working state**

```powershell
git init
git add README.md PLAN.md requirements.txt build.bat run.bat run-debug.bat ReadAloud.spec .gitignore src tests
git commit -m "chore: baseline existing Read Aloud application"
```

Expected: a commit is created; `git status --short` shows no tracked modifications.

---

### Task 1: Harden configuration loading

**Files:**
- Modify: `src/config.py`
- Test: `tests/test_config.py` (create)

**Interfaces:**
- Consumes: nothing new.
- Produces: `normalise_config(cfg: dict) -> dict` (coerce/clamp known keys), `load_config()` uses it; `save_config()` unchanged signature.

- [ ] **Step 1: Write failing tests for coercion, clamping, corrupt data**

`tests/test_config.py`: `test_defaults_complete`, `test_invalid_values_are_clamped` (volume `"abc"`→100, `200`→100, `-5`→0; `sapi_rate` 99→10; `max_chars` -1→4000; `voice_seconds` negative→4.0), `test_unknown_keys_dropped`, `test_corrupt_json_falls_back_to_defaults` (patch `CONFIG_PATH` to a temp file containing `{bad`).

- [ ] **Step 2: Run tests; verify they fail**

Run: `.venv\Scripts\python.exe -m unittest tests.test_config -v`
Expected: FAIL (`normalise_config` not defined / load returns bad values).

- [ ] **Step 3: Implement `normalise_config` and wire it into `load_config`**

Coerce each DEFAULTS entry by the type of its default; clamp numeric ranges; keep strings/triplet hotkey strings as-is; catch all read/parse errors and return defaults. Log a warning via `logging.getLogger("read_aloud")` on recovery.

- [ ] **Step 4: Run tests; verify they pass**

Run: `.venv\Scripts\python.exe -m unittest tests.test_config -v`
Expected: PASS.

- [ ] **Step 5: Commit**

`git add src/config.py tests/test_config.py && git commit -m "fix: validate and clamp configuration values"`

---

### Task 2: Validate hotkeys and prevent silent collisions

**Files:**
- Modify: `src/hotkeys.py`, `src/main.py`
- Test: `tests/test_hotkeys.py` (create)

**Interfaces:**
- Consumes: `to_pynput(combo: str) -> str`, `describe(combo: str) -> str` (existing).
- Produces: `validate_hotkeys(config: dict) -> tuple[dict[str, str], list[str]]` — returns normalised `{action: pynput_binding}` and a list of human-readable problems.

- [ ] **Step 1: Write failing tests**

`tests/test_hotkeys.py`: keep the three existing cases, add `test_duplicate_hotkeys_rejected` (read == stop → problems list contains "duplicate", stop action absent), `test_malformed_combo_rejected` (`"ctrl+shift+"` → problem, no binding), `test_unknown_key_accepted` (`"ctrl+alt+f13"` → `<ctrl>+<alt>+f13`), `test_validate_returns_all_three_actions_when_valid`.

- [ ] **Step 2: Run tests; verify they fail**

Expected: FAIL (`validate_hotkeys` not defined).

- [ ] **Step 3: Implement `validate_hotkeys` and use it in `HotkeyManager.start`**

Normalise each configured combo with `to_pynput`; drop invalid ones; detect duplicate bindings and keep the first action in priority order read → voice → stop. If problems exist, still register the valid actions and call `notify("Hotkeys", ...)` with the problem list.

- [ ] **Step 4: Run tests; verify they pass**

Expected: PASS.

- [ ] **Step 5: Commit**

`git add src/hotkeys.py src/main.py tests/test_hotkeys.py && git commit -m "fix: validate hotkeys and detect collisions"`

---

### Task 3: Make settings flow thread-safe and persistent

**Files:**
- Modify: `src/main.py`, `src/settings_ui.py`
- Test: `tests/test_settings_snapshot.py` (create)

**Interfaces:**
- Consumes: `validate_hotkeys`, `normalise_config`.
- Produces: `snapshot_settings(vars_dict: dict, config: dict) -> dict` returning only plain `str`/`int`/`float` values; `App.apply_setting(key, value)` persists one setting to `self.config` and calls `save_config`; main-loop event tokens `__settings__`.

- [ ] **Step 1: Write failing test**

`tests/test_settings_snapshot.py::test_snapshot_contains_only_plain_values`: pass fake objects exposing `.get()` and assert every returned value is an instance of `(str, int, float, bool, type(None))` and that no Tk/StringVar object is present; `test_snapshot_reflects_current_controls`.

- [ ] **Step 2: Run test; verify it fails**

Expected: FAIL (`snapshot_settings` not defined).

- [ ] **Step 3: Implement snapshot + queue payload**

In `main.py`: add `snapshot_settings`; `request_read()` snapshots on the calling thread and queues `(mode, snapshot)`; `worker()` unpacks it; `handle_request(mode, snapshot)` uses the snapshot dict only. Change the engine/mode/volume Tk `command=` callbacks to call `self.apply_setting(...)` immediately (save + refresh, no read needed). Add `__settings__` handling in `poll_events()` so `open_settings()` opens the dialog on the main thread. Change `settings_ui.open_settings` to a plain synchronous function (remove its internal thread).

- [ ] **Step 4: Run tests; verify they pass**

Run the new tests plus the full suite. Expected: PASS, and `grep` shows no `.get()` on a Tk variable inside `worker`/`handle_request`.

- [ ] **Step 5: Commit**

`git add src/main.py src/settings_ui.py tests/test_settings_snapshot.py && git commit -m "fix: keep Tk access on the main thread and persist settings"`

---

### Task 4: Robust TTS with cancellation and fallback

**Files:**
- Modify: `src/tts.py`, `requirements.txt`
- Test: `tests/test_tts.py` (create)

**Interfaces:**
- Consumes: `TTSManager(config: dict)`, `.stop()`, `.speak(text: str)` (existing signatures).
- Produces: `TTSManager.generation: int` (incremented by `stop()`; `speak` captures it and aborts if it changes), `speak` never raises for engine failures.

- [ ] **Step 1: Write failing tests**

`tests/test_tts.py`: `test_empty_text_does_nothing` (mock `_speak_offline`, assert not called), `test_edge_failure_falls_back` (patch `_speak_edge` to raise, patch `_speak_offline`, assert offline called once), `test_stop_increments_generation`, `test_stop_during_edge_save_aborts_playback` (patch `edge_tts.Communicate.save` to set `stop_event`, assert `pygame.mixer.music.play` not called).

- [ ] **Step 2: Run tests; verify they fail**

Expected: FAIL for generation/stop-during-save tests.

- [ ] **Step 3: Implement in `tts.py`**

Add `generation`; `stop()` sets event and increments; `speak()` captures generation and re-checks before/after engine calls; wrap `_speak_edge` in try/finally with mixer guard and temp-file cleanup; in the async save coroutine poll `stop_event` between chunks or check immediately after `save()` before playback; make `_ensure_mixer` catch init failure and log. Add `comtypes>=1.2` to `requirements.txt`.

- [ ] **Step 4: Run tests; verify they pass**

Expected: PASS.

- [ ] **Step 5: Commit**

`git add src/tts.py requirements.txt tests/test_tts.py && git commit -m "fix: make TTS cancellation robust and fallback safe"`

---

### Task 5: LLM discovery fallback and URL validation

**Files:**
- Modify: `src/llm.py`
- Test: `tests/test_llm.py` (create)

**Interfaces:**
- Consumes: `build_messages(text, mode, config)`, `LocalLLM.discover() -> (base, models, label)` (existing).
- Produces: `valid_base_url(url: str) -> bool`; `discover()` tries configured URL first, then `CANDIDATES`, returning the first reachable one.

- [ ] **Step 1: Write failing tests**

`tests/test_llm.py`: keep existing prompt/truncation tests; add `test_discover_falls_back_to_candidates` (configured URL raises `requests.ConnectionError`; mock LM Studio `GET /models` 200 with one model → returns that base), `test_invalid_url_rejected` (`ftp://x`, `not-a-url`), `test_process_without_server_reads_verbatim` (discover returns empty → returns original text + error).

- [ ] **Step 2: Run tests; verify they fail**

Expected: FAIL for new tests.

- [ ] **Step 3: Implement**

`valid_base_url` allows only `http`/`https` scheme with a netloc. `discover()` builds candidate list = configured (if valid) + CANDIDATES; probes each in order; returns first with models. Keep `process()` behaviour (verbatim + error when none found).

- [ ] **Step 4: Run tests; verify they pass**

Expected: PASS.

- [ ] **Step 5: Commit**

`git add src/llm.py tests/test_llm.py && git commit -m "fix: fall back across local LLM endpoints"`

---

### Task 6: Observability, clipboard and voice race

**Files:**
- Modify: `src/logging_setup.py`, `src/selection.py`, `src/voice.py`, `src/main.py`
- Test: `tests/test_logging.py` (create), extend `tests/test_core.py`

**Interfaces:**
- Consumes: `setup_logging()` (existing).
- Produces: `setup_logging(include_console: bool = True)`; `handle_request` emits a `request_id=<hex>` start and completion/failure log line with elapsed ms.

- [ ] **Step 1: Write failing tests**

`tests/test_logging.py`: `test_no_console_handler_when_disabled` (call `setup_logging(include_console=False)` after clearing handlers; assert only `RotatingFileHandler` present), `test_rotating_file_handler_configured`. Extend `tests/test_core.py` with `test_clipboard_restore_warns_on_unsupported_format` (mock `win32clipboard.SetClipboardData` to raise for one format; assert `log.warning` called) and `test_voice_model_double_check` (call `_get_model` from 2 threads with a mocked `WhisperModel`; assert constructed once).

- [ ] **Step 2: Run tests; verify they fail**

Expected: FAIL for new signatures/behaviours.

- [ ] **Step 3: Implement**

`logging_setup.py`: guard `StreamHandler` creation with `include_console` and `sys.stdout is not None`; keep rotating file handler. `selection.py`: log a warning per format that fails to restore. `voice.py`: move the outer `_model is None` check inside the lock (double-checked locking done correctly). `main.py`: generate `uuid.uuid4().hex[:8]` per request, log start/end/failure with elapsed time via `time.perf_counter()`. `open_log()`: create parent dir and touch the file if missing, and catch `OSError` with a `notify`.

- [ ] **Step 4: Run tests; verify they pass**

Expected: PASS.

- [ ] **Step 5: Commit**

`git add src/logging_setup.py src/selection.py src/voice.py src/main.py tests/test_logging.py tests/test_core.py && git commit -m "feat: add request tracing and harden capture/voice"`

---

### Task 7: Light UI polish, documentation and packaged build

**Files:**
- Modify: `src/main.py`, `README.md`, `PLAN.md`, `.opencode/skills/SKILLS.md` (create), `docs/adr/0001-main-thread-settings-snapshot.md` (create)
- Modify: `build.bat` only if verification requires it.

**Interfaces:**
- Consumes: all of the above.
- Produces: a working `dist\ReadAloud\ReadAloud.exe`.

- [ ] **Step 1: Write the documentation first (failing check: docs are stale)**

Update `PLAN.md` header from "Draft for review and approval. No application code has been written yet." to reflect the approved, implemented state. Add a "Reliability pass" section summarising the changes. Create `SKILLS.md` listing the 17 installed skills, their source repos, star counts at selection time, and licence caveats. Create the ADR for the main-thread snapshot decision. Add a README subsection about request IDs in the log.

- [ ] **Step 2: Verify docs check**

Run: `Select-String -Path PLAN.md -Pattern "No application code has been written yet"`
Expected: no matches.

- [ ] **Step 3: Apply light UI polish**

In `main.py`: set a window icon using the existing `make_icon_image()` via `ImageTk.PhotoImage`; add `ttk.Label` tooltips or `helptext` to the three action buttons; set the status label colour to green on success / red on notification failures via a tag or a dedicated label; prefix activity log lines with a `HH:MM:SS` timestamp. Keep behaviour unchanged.

- [ ] **Step 4: Run the full test suite**

Run: `.venv\Scripts\python.exe -m unittest discover -s tests -v`
Expected: all tests PASS; record the count.

- [ ] **Step 5: Rebuild the packaged app**

Run: `.\build.bat`
Expected: PyInstaller completes without error; `dist\ReadAloud\ReadAloud.exe` exists with a newer timestamp.

- [ ] **Step 6: Smoke-test the packaged app**

Run: `Start-Process -FilePath .\dist\ReadAloud\ReadAloud.exe`; wait 5 seconds; check `Get-Process ReadAloud`; verify `dist\ReadAloud\logs\app.log` was created and contains the startup line; then stop the process.

- [ ] **Step 7: Commit**

`git add -A && git commit -m "feat: polish UI, update docs and rebuild packaged app"`

---

## Self-review (completed by author)

- **Spec coverage:** every numbered defect in the spec maps to Tasks 1–6; goals 6–7 map to Task 7; test-first criteria map to each task's Step 1; docs drift maps to Task 7 Step 1. No gaps.
- **Step scan:** each step has one checkable result; no "TBD" lines. Task 0 has no failing test because it establishes the review baseline, not product behaviour.
- **Type consistency:** `snapshot_settings` returns `dict`; `handle_request(mode, snapshot)` is used consistently in Task 3; `validate_hotkeys` returns `(dict, list[str])` in Task 2; `setup_logging(include_console: bool)` in Task 6.
- **Review Focus:** all five risky input classes have a named test in the owning task.
- **Proportion:** plan is roughly twice the spec length, dominated by verification commands and signatures, not program bodies.
