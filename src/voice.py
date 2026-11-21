"""Local speech-to-text voice commands using the cached faster-whisper model."""
from __future__ import annotations

import glob
import logging
import os
import threading

log = logging.getLogger("read_aloud")


def parse_command(text: str) -> tuple[str, str | None]:
    """Turn a recognised phrase into (action, mode)."""
    value = (text or "").strip().lower().rstrip(".!?")
    if not value:
        return "none", None
    if any(word in value for word in ("stop", "cancel", "quiet", "silence")):
        return "stop", None
    if "summar" in value:
        return "read", "summarize"
    if "simpl" in value or "plain" in value:
        return "read", "simplify"
    if "translat" in value:
        return "read", "translate"
    if any(word in value for word in ("read", "speak", "say", "aloud")):
        return "read", "verbatim"
    return "unknown", value


def find_model_path(config: dict) -> tuple[str, bool]:
    """Locate a locally cached faster-whisper snapshot; (path, is_local)."""
    override = (config.get("whisper_model_path") or "").strip()
    if override and os.path.isdir(override):
        return override, True

    cache = config.get("whisper_cache") or os.path.join(
        os.path.expanduser("~"), "voicebox models"
    )
    repo = config.get("whisper_model", "Systran/faster-whisper-base")
    folder = "models--" + repo.replace("/", "--")
    pattern = os.path.join(cache, folder, "snapshots", "*", "model.bin")
    matches = sorted(glob.glob(pattern))
    if matches:
        return os.path.dirname(matches[0]), True
    return repo, False


class VoiceCommandListener:
    def __init__(self, config: dict):
        self.config = config
        self._model = None
        self._lock = threading.Lock()

    def _get_model(self):
        if self._model is None:
            from faster_whisper import WhisperModel

            with self._lock:
                if self._model is None:
                    path, local = find_model_path(self.config)
                    log.info("Loading Whisper model from %s (local=%s)", path, local)
                    self._model = WhisperModel(
                        path,
                        device="cpu",
                        compute_type="int8",
                        local_files_only=local,
                        download_root=self.config.get("whisper_cache") or None,
                    )
        return self._model

    def listen_once(self) -> tuple[str, str | None]:
        """Record a short phrase, transcribe it locally, and parse the command."""
        import numpy as np
        import sounddevice as sd

        seconds = float(self.config.get("voice_seconds", 4.0))
        samplerate = 16000
        device = self.config.get("input_device") or None
        log.info("Listening for %.1f seconds", seconds)
        audio = sd.rec(
            int(seconds * samplerate),
            samplerate=samplerate,
            channels=1,
            dtype="float32",
            device=device,
        )
        sd.wait()
        model = self._get_model()
        language = (self.config.get("whisper_language") or "").strip() or None
        segments, _ = model.transcribe(
            np.asarray(audio).flatten(),
            language=language,
            beam_size=1,
            vad_filter=True,
        )
        text = " ".join(segment.text for segment in segments).strip()
        log.info("Heard: %r", text)
        return parse_command(text)
