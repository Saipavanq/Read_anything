"""Optional local LLM processing through an OpenAI-compatible endpoint."""
from __future__ import annotations

import logging
from urllib.parse import urlparse

import requests

log = logging.getLogger("read_aloud")

CANDIDATES = [
    ("LM Studio", "http://localhost:1234/v1"),
    ("Ollama", "http://localhost:11434/v1"),
]

MODE_INSTRUCTIONS = {
    "summarize": "Summarize the following text in at most three sentences. Reply with only the summary.",
    "simplify": "Rewrite the following text in simple, clear language while keeping its meaning. Reply with only the rewritten text.",
    "translate": "Translate the following text into {language}. Reply with only the translation.",
    "custom": "{prompt}",
}


def build_messages(text: str, mode: str, config: dict) -> list[dict]:
    if mode == "translate":
        system = MODE_INSTRUCTIONS[mode].format(
            language=config.get("llm_translate_to", "English")
        )
    elif mode == "custom":
        system = MODE_INSTRUCTIONS[mode].format(prompt=config.get("llm_prompt", ""))
    else:
        system = MODE_INSTRUCTIONS.get(mode, MODE_INSTRUCTIONS["summarize"])
    return [
        {"role": "system", "content": system},
        {"role": "user", "content": text[:6000]},
    ]


def valid_base_url(url: str) -> bool:
    """Accept only absolute http(s) URLs — the local API base must be explicit."""
    try:
        parsed = urlparse((url or "").strip())
    except ValueError:
        return False
    return parsed.scheme in ("http", "https") and bool(parsed.netloc)


class LocalLLM:
    def __init__(self, config: dict):
        self.config = config

    def discover(self) -> tuple[str, list[str], str]:
        """Return (base_url, model_ids, provider_label) for a reachable endpoint."""
        configured = (self.config.get("llm_base_url") or "").strip()
        candidates = []
        if configured:
            if valid_base_url(configured):
                candidates.append(("Configured", configured.rstrip("/")))
            else:
                log.warning("Ignoring invalid llm_base_url: %r", configured)
        candidates.extend(CANDIDATES)
        for label, base in candidates:
            try:
                response = requests.get(f"{base}/models", timeout=1.5)
                if response.ok:
                    models = [
                        item.get("id", "")
                        for item in response.json().get("data", [])
                        if item.get("id")
                    ]
                    if models:
                        return base, models, label
            except Exception as exc:
                log.debug("LLM endpoint %s unavailable: %s", base, exc)
        return "", [], ""

    def process(self, text: str, mode: str) -> tuple[str, str | None]:
        """Process text with the local model; returns (text, error)."""
        if mode in ("", "verbatim") or not self.config.get("llm_enabled"):
            return text, None

        base, models, label = self.discover()
        if not base:
            return text, (
                "No local LLM server found. Start LM Studio's local server "
                "(localhost:1234) or Ollama (localhost:11434), or read verbatim."
            )

        model = (self.config.get("llm_model") or "").strip() or models[0]
        if model not in models:
            log.warning("Configured model %s not in %s; using %s", model, models, models[0])
            model = models[0]

        payload = {
            "model": model,
            "messages": build_messages(text, mode, self.config),
            "temperature": 0.2,
            "max_tokens": 1024,
            "stream": False,
        }
        try:
            response = requests.post(
                f"{base}/chat/completions",
                json=payload,
                timeout=(5, int(self.config.get("llm_timeout", 120))),
            )
            response.raise_for_status()
            result = response.json()["choices"][0]["message"]["content"].strip()
            log.info("Processed %d chars with %s/%s", len(text), label, model)
            return result or text, None
        except Exception as exc:
            log.warning("LLM request failed: %s", exc)
            return text, f"Local LLM failed: {exc}"
