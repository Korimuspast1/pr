@echo off
chcp 65001 >nul
setlocal EnableDelayedExpansion
title Социальная сеть
cd /d "%~dp0"

set "NEED_MAJOR=22"
set "PORT=3000"
set "HOST=0.0.0.0"

echo ==========================================
echo  Социальная сеть - запуск
echo ==========================================

call :check_node
if not defined NODE_OK (
  echo Node.js не найден или версия старше %NEED_MAJOR%. Устанавливаю...
  powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install-node.ps1"
  if errorlevel 1 (
    echo.
    echo Не удалось установить Node.js автоматически.
    echo Установите Node.js LTS вручную: https://nodejs.org/
    echo После установки запустите start.bat снова.
    pause
    exit /b 1
  )
  rem Новая установка не видна в уже открытом окне, добавляем путь вручную.
  set "PATH=%ProgramFiles%\nodejs;%PATH%"
  call :check_node
  if not defined NODE_OK (
    echo.
    echo Node.js установлен, но не найден в PATH. Закройте это окно и запустите start.bat снова.
    pause
    exit /b 1
  )
)

echo Node.js: 
node -v

if not exist "data" mkdir "data"

echo.
echo Сайт откроется в браузере: http://localhost:%PORT%
echo Чтобы остановить сервер, закройте это окно или нажмите Ctrl+C.
echo.

start "" powershell -NoProfile -WindowStyle Hidden -Command "Start-Sleep -Seconds 2; Start-Process 'http://localhost:%PORT%'"

node server\index.js
echo.
echo Сервер остановлен.
pause
exit /b 0

:check_node
set "NODE_OK="
set "NODE_MAJOR="
where node >nul 2>nul
if errorlevel 1 goto :eof
for /f "tokens=1 delims=v." %%a in ('node -v') do set "NODE_MAJOR=%%a"
if defined NODE_MAJOR if %NODE_MAJOR% GEQ %NEED_MAJOR% set "NODE_OK=1"
goto :eof
