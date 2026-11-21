@echo off
cd /d "%~dp0"
".venv\Scripts\python.exe" -m PyInstaller --noconfirm --clean --onedir --windowed --name ReadAloud ^
  --hidden-import pynput.keyboard._win32 ^
  --hidden-import pynput.mouse._win32 ^
  --hidden-import pystray._win32 ^
  --collect-all comtypes ^
  --collect-all uiautomation ^
  --collect-all faster_whisper ^
  --collect-all ctranslate2 ^
  --collect-all av ^
  --collect-all onnxruntime ^
  --collect-all edge_tts ^
  --collect-all aiohttp ^
  src\main.py
