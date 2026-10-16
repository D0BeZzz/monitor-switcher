@echo off
setlocal
rem Build with the .NET Framework compiler shipped with Windows. No SDK needed.
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo [ERROR] csc.exe not found under %WINDIR%\Microsoft.NET\Framework*\v4.0.30319
    exit /b 1
)
"%CSC%" -nologo -target:winexe -out:"Control Center.exe" -r:System.Windows.Forms.dll -r:System.Drawing.dll ControlCenter.cs
if errorlevel 1 (
    echo [ERROR] build failed
    exit /b 1
)
echo OK  --^>  Control Center.exe
