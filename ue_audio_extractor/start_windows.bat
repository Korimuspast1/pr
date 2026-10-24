@echo off
setlocal
cd /d "%~dp0\.."
py -3 -m ue_audio_extractor %*
if errorlevel 1 pause
