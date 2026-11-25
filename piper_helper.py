#!/usr/bin/env python3
"""Small bridge between the C# ReadAloud app and offline Piper voices.

Two commands are supported:
  --list --model-dir DIR
  --synthesize --model FILE --config FILE --text-file FILE --wav FILE
              [--speaker N] [--length-scale X]
"""

from __future__ import annotations

import argparse
import json
import sys
import wave
from pathlib import Path


class HelperError(Exception):
    """A user-facing failure that should not produce a traceback."""


def list_voices(model_dir: str) -> int:
    folder = Path(model_dir).expanduser()
    if not folder.is_dir():
        raise HelperError(f"Piper model folder not found: {folder}")

    voices = [
        {"name": model.stem, "path": str(model)}
        for model in sorted(folder.glob("*.onnx"))
    ]
    print(json.dumps(voices))
    return 0


def synthesize(
    model_path: str,
    config_path: str | None,
    text_file: str,
    wav_path: str,
    speaker: int,
    length_scale: float,
) -> int:
    model = Path(model_path).expanduser()
    if not model.is_file():
        raise HelperError(f"Piper model not found: {model}")

    if config_path:
        config = Path(config_path).expanduser()
    else:
        config = Path(str(model) + ".json")
        if not config.is_file():
            config = model.with_suffix(".json")
    if not config.is_file():
        raise HelperError(f"Piper model config not found: {config}")

    text = Path(text_file).expanduser()
    if not text.is_file():
        raise HelperError(f"Text file not found: {text}")

    try:
        from piper import PiperVoice
        from piper.config import SynthesisConfig
    except Exception as exc:
        raise HelperError(f"Could not load the piper package: {exc}") from exc

    wav = Path(wav_path).expanduser()
    try:
        voice = PiperVoice.load(model, config_path=config, use_cuda=False)
        settings = SynthesisConfig(speaker_id=speaker, length_scale=length_scale)
        wav.parent.mkdir(parents=True, exist_ok=True)
        with wave.open(str(wav), "wb") as output:
            voice.synthesize_wav(text.read_text(encoding="utf-8"), output, syn_config=settings)
        with wave.open(str(wav), "rb") as produced:
            duration = produced.getnframes() / float(produced.getframerate())
    except Exception as exc:
        raise HelperError(f"Piper synthesis failed: {exc}") from exc

    print(f"DURATION={duration:.3f}")
    return 0


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Piper voice helper for ReadAloud")
    parser.add_argument("--list", action="store_true", help="list *.onnx voices as JSON")
    parser.add_argument("--model-dir", help="folder containing Piper .onnx models")
    parser.add_argument("--synthesize", action="store_true", help="synthesize text to a WAV file")
    parser.add_argument("--model", help="Piper .onnx model file")
    parser.add_argument("--config", help="model .json config (defaults to the sibling file)")
    parser.add_argument("--text-file", help="UTF-8 file containing the text to speak")
    parser.add_argument("--wav", help="output WAV file")
    parser.add_argument("--speaker", type=int, default=0, help="speaker id (default: 0)")
    parser.add_argument("--length-scale", type=float, default=1.0, help="voice rate (default: 1.0)")
    return parser


def main(argv: list[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    try:
        if args.list == args.synthesize:
            raise HelperError("choose exactly one command: --list or --synthesize")

        if args.list:
            if not args.model_dir:
                raise HelperError("--list requires --model-dir DIR")
            return list_voices(args.model_dir)

        missing = [
            flag
            for flag, value in (
                ("--model", args.model),
                ("--text-file", args.text_file),
                ("--wav", args.wav),
            )
            if not value
        ]
        if missing:
            raise HelperError("--synthesize requires " + ", ".join(missing))
        if args.speaker < 0:
            raise HelperError("--speaker must be zero or greater")
        if args.length_scale <= 0:
            raise HelperError("--length-scale must be greater than zero")

        return synthesize(
            args.model,
            args.config,
            args.text_file,
            args.wav,
            args.speaker,
            args.length_scale,
        )
    except HelperError as exc:
        print(f"piper_helper: {exc}", file=sys.stderr)
        return 1
    except Exception as exc:
        print(f"piper_helper: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
