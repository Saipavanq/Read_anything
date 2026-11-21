"""Text-to-speech routing: offline Windows SAPI with optional online edge-tts."""
from __future__ import annotations

import asyncio
import logging
import os
import tempfile
import threading

log = logging.getLogger("read_aloud")

SVS_FLAGS_ASYNC = 1
SVSF_PURGE_BEFORE_SPEAK = 2


def list_offline_voices() -> list[str]:
    import comtypes.client as cc

    try:
        voice = cc.CreateObject("SAPI.SpVoice")
        voices = voice.GetVoices()
        return [voices.Item(i).GetDescription() for i in range(voices.Count)]
    except Exception as exc:
        log.warning("Could not list SAPI voices: %s", exc)
        return []


class TTSManager:
    """Speaks text, supporting immediate stop and interruption."""

    def __init__(self, config: dict):
        self.config = config
        self.stop_event = threading.Event()
        self.generation = 0
        self._mixer_ready = False

    def stop(self) -> None:
        self.stop_event.set()
        self.generation += 1
        log.debug("Speech stop requested (generation %d)", self.generation)

    def speak(self, text: str) -> None:
        if not text:
            return
        self.stop_event.clear()
        generation = self.generation
        if self.config.get("tts_engine", "offline") == "edge":
            try:
                self._speak_edge(text, generation)
            except Exception as exc:
                if generation != self.generation:
                    return
                log.warning("edge-tts failed, falling back to offline voice: %s", exc)
            else:
                return
        if generation != self.generation or self.stop_event.is_set():
            return
        self._speak_offline(text)

    def _speak_offline(self, text: str) -> None:
        import comtypes.client as cc

        voice = cc.CreateObject("SAPI.SpVoice")
        wanted = self.config.get("offline_voice", "")
        if wanted:
            voices = voice.GetVoices()
            for i in range(voices.Count):
                item = voices.Item(i)
                if item.GetDescription() == wanted:
                    voice.Voice = item
                    break
        voice.Rate = int(self.config.get("sapi_rate", 0))
        voice.Volume = int(self.config.get("volume", 100))
        voice.Speak(text, SVS_FLAGS_ASYNC)
        log.info("Speaking %d characters with offline SAPI voice", len(text))
        while not self.stop_event.is_set():
            # WaitUntilDone returns False (S_OK) when finished, True while speaking.
            if not voice.WaitUntilDone(100):
                break
        if self.stop_event.is_set():
            voice.Speak("", SVSF_PURGE_BEFORE_SPEAK)
            log.debug("Offline speech purged")

    def _ensure_mixer(self) -> None:
        if self._mixer_ready:
            return
        import pygame

        try:
            pygame.mixer.init()
            self._mixer_ready = True
        except Exception as exc:
            log.warning("Audio mixer could not be initialised: %s", exc)

    def _speak_edge(self, text: str, generation: int) -> None:
        import edge_tts
        import pygame

        fd, path = tempfile.mkstemp(suffix=".mp3", prefix="read_aloud_")
        os.close(fd)
        try:

            async def save() -> None:
                comm = edge_tts.Communicate(
                    text,
                    self.config.get("edge_voice", "en-US-AriaNeural"),
                    rate=self.config.get("edge_rate", "+0%"),
                )
                await comm.save(path)

            asyncio.run(save())
            if generation != self.generation or self.stop_event.is_set():
                log.debug("Speech cancelled while edge-tts was synthesising")
                return
            self._ensure_mixer()
            pygame.mixer.music.load(path)
            if generation != self.generation or self.stop_event.is_set():
                return
            pygame.mixer.music.set_volume(float(self.config.get("volume", 100)) / 100.0)
            pygame.mixer.music.play()
            clock = pygame.time.Clock()
            while pygame.mixer.music.get_busy() and not self.stop_event.is_set():
                clock.tick(10)
            pygame.mixer.music.stop()
            log.info("Speaking %d characters with edge-tts", len(text))
        finally:
            try:
                os.remove(path)
            except OSError:
                pass
