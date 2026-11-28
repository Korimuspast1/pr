@echo off
rem Установка native messaging хоста Browser Terminal для Windows.
rem Использование: install.bat ^<ID_РАСШИРЕНИЯ^>
rem ID расширения: chrome://extensions -> режим разработчика -> поле ID.
setlocal EnableDelayedExpansion

if "%~1"=="" (
  echo Ошибка: не указан ID расширения.
  echo Использование: install.bat ^<ID_РАСШИРЕНИЯ^>
  exit /b 1
)
set "EXT_ID=%~1"
set "DIR=%~dp0"
if "%DIR:~-1%"=="\" set "DIR=%DIR:~0,-1%"
set "HOST_NAME=com.korimuspast.browserterminal"
set "HOST_DIR=%LOCALAPPDATA%\BrowserTerminal"
set "MANIFEST=%HOST_DIR%\%HOST_NAME%.json"
set "HOST_PATH=%DIR%\host\run-host.bat"

if not exist "%HOST_DIR%" mkdir "%HOST_DIR%"

rem В JSON обратные слеши пути должны быть экранированы (\ -> \\)
set "HOST_PATH_JSON=%HOST_PATH:\=\\%"

powershell -NoProfile -Command "$t = Get-Content -Raw '%DIR%\host\%HOST_NAME%.json'; $t = $t -replace '__HOST_ABS_PATH__', '%HOST_PATH_JSON%'; $t = $t -replace '__EXTENSION_ID__', '%EXT_ID%'; Set-Content -NoNewline -Encoding UTF8 -Path '%MANIFEST%' -Value $t"
if errorlevel 1 (
  echo Не удалось сгенерировать манифест хоста.
  exit /b 1
)
echo OK  манифест: %MANIFEST%

for %%B in ("Software\Google\Chrome" "Software\Chromium" "Software\Microsoft\Edge" "Software\BraveSoftware\Brave-Browser" "Software\Vivaldi" "Software\Opera Software") do (
  reg add "HKCU\%%~B\NativeMessagingHosts\%HOST_NAME%" /ve /t REG_SZ /d "%MANIFEST%" /f >nul 2>&1 && (echo OK  %%~B) || (echo пропуск %%~B)
)

echo.
echo Готово! Откройте расширение кликом по иконке — откроется вкладка терминала.
echo Проверить хост без браузера: python host\selftest.py
exit /b 0
