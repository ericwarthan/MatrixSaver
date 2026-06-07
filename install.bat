@echo off
:: Install MatrixSaver — run from inside the extracted folder, as Administrator
setlocal EnableDelayedExpansion

echo ============================================================
echo  Matrix Screensaver (Native WebView2) — Installer
echo ============================================================
echo.

net session >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Run as Administrator.
    pause & exit /b 1
)

set SRCDIR=%~dp0
if "!SRCDIR:~-1!"=="\" set SRCDIR=!SRCDIR:~0,-1!
set SRCSCR=%SRCDIR%\MatrixSaver.scr

if not exist "%SRCSCR%" (
    echo ERROR: MatrixSaver.scr not found in %SRCDIR%
    pause & exit /b 1
)

:: Copy entire folder to Program Files
set DESTDIR=%ProgramFiles%\MatrixSaver
set DESTSCR=%DESTDIR%\MatrixSaver.scr
echo Installing to %DESTDIR% ...
if not exist "%DESTDIR%" mkdir "%DESTDIR%"
xcopy /E /I /Y "%SRCDIR%\*" "%DESTDIR%\" >nul

:: Register
echo Registering screensaver...
reg add "HKCU\Control Panel\Desktop" /v SCRNSAVE.EXE       /t REG_SZ /d "%DESTSCR%" /f >nul
reg add "HKCU\Control Panel\Desktop" /v ScreenSaveActive   /t REG_SZ /d "1"         /f >nul
reg add "HKCU\Control Panel\Desktop" /v ScreenSaveTimeOut  /t REG_SZ /d "600"       /f >nul

echo.
echo Done!  MatrixSaver is installed at %DESTDIR%
echo Keep this folder in place — the screensaver loads matrix/ from here.
echo.
choice /C YN /M "Open Screen Saver settings now?"
if %ERRORLEVEL% EQU 1 start "" "%SystemRoot%\System32\control.exe" desk.cpl,,@screensaver
endlocal
