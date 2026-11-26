@echo off
rem Starts Pull from the Cloud calculator through Microsoft's signed dotnet.exe.
setlocal
set "DLL=%~dp0app\PullCloud.dll"

where dotnet >nul 2>&1 || goto :noruntime
dotnet --list-runtimes 2>nul | findstr /r /c:"Microsoft.WindowsDesktop.App [89]\." /c:"Microsoft.WindowsDesktop.App [1-9][0-9]\." >nul || goto :noruntime

start "PullCloud" /min dotnet "%DLL%"
exit /b 0

:noruntime
echo.
echo  Pull from the Cloud calculator needs the free Microsoft ".NET 8 Desktop Runtime".
echo.
echo  The download page will open now. Choose:
echo     .NET Desktop Runtime 8.x  -^>  Windows  -^>  x64  (Installer)
echo  Install it, then start PullCloud again.
echo.
start "" "https://dotnet.microsoft.com/en-us/download/dotnet/8.0"
pause
exit /b 1
