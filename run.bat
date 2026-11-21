@echo off
cd /d "%~dp0"
start "Read Aloud" ".venv\Scripts\pythonw.exe" "src\main.py"
