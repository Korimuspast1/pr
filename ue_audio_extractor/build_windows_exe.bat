@echo off
setlocal
cd /d "%~dp0\.."

echo Building the windowed UE Audio Extractor executable...
py -3 -m pip install --upgrade pyinstaller
if errorlevel 1 goto failed

py -3 -m PyInstaller --noconfirm --clean --onefile --windowed --name UE-Audio-Extractor-windows-x64 "UE Audio Extractor.pyw"
if errorlevel 1 goto failed

echo.
echo Done: dist\UE-Audio-Extractor-windows-x64.exe
pause
exit /b 0

:failed
echo.
echo Build failed. Install Python 3 with Tkinter and check your internet connection.
pause
exit /b 1
