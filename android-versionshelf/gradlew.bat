@echo off
setlocal
where gradle >NUL 2>NUL
if %ERRORLEVEL% EQU 0 (
  gradle %*
  exit /b %ERRORLEVEL%
)
echo Gradle 8.9 or newer is required. Install it, then run this command again.
exit /b 1
