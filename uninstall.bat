@echo off
setlocal EnableDelayedExpansion
net session >nul 2>&1
if %ERRORLEVEL% NEQ 0 ( echo Run as Administrator. & pause & exit /b 1 )

set DESTDIR=%ProgramFiles%\MatrixSaver
set DESTSCR=%DESTDIR%\MatrixSaver.scr

for /f "tokens=3" %%A in ('reg query "HKCU\Control Panel\Desktop" /v SCRNSAVE.EXE 2^>nul') do set CURRENT=%%A
if /I "!CURRENT!"=="%DESTSCR%" (
    reg delete "HKCU\Control Panel\Desktop" /v SCRNSAVE.EXE     /f >nul 2>&1
    reg add    "HKCU\Control Panel\Desktop" /v ScreenSaveActive /t REG_SZ /d "0" /f >nul
)

if exist "%DESTDIR%" rd /S /Q "%DESTDIR%"

if exist "%APPDATA%\MatrixSaver" (
    choice /C YN /M "Remove saved settings (%APPDATA%\MatrixSaver)?"
    if !ERRORLEVEL! EQU 1 rd /S /Q "%APPDATA%\MatrixSaver"
)
echo Uninstalled.
pause
endlocal
