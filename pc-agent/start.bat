@echo off
REM запуск агента на windows: токен и chat_id берутся из config.json
cd /d "%~dp0"
python run.py
if errorlevel 1 (
  echo.
  echo агент завершился с ошибкой. проверь, что установлен python и выполнено:
  echo    pip install requests
  pause
)
