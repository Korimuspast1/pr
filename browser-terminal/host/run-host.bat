@echo off
rem Обёртка-хост для Windows: манифест native messaging ссылается на этот файл.
rem Требуется python (или python3) в PATH. Если Python называется иначе —
rem поправьте следующую строку.
where python >nul 2>nul && (python "%~dp0host.py" %* & exit /b)
where python3 >nul 2>nul && (python3 "%~dp0host.py" %* & exit /b)
echo [browser-terminal-host] python не найден в PATH 1>&2
exit /b 1
