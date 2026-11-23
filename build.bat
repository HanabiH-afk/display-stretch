@echo off
setlocal
cd /d "%~dp0"

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo Could not find csc.exe ^(.NET Framework 4.x^).
    pause
    exit /b 1
)

"%CSC%" /nologo /target:winexe /codepage:65001 /win32icon:app.ico ^
    /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
    /out:DisplayStretch.exe DisplayStretch.cs

if errorlevel 1 (
    echo Build failed.
    pause
    exit /b 1
)
echo Built: DisplayStretch.exe
