@echo off
rem Starts Pull from the Cloud calculator with the bundled, Microsoft-signed .NET runtime (no install, no admin).
setlocal
set "DOTNET=%~dp0runtime\dotnet.exe"
set "DLL=%~dp0app\PullCloud.dll"

if not exist "%DOTNET%" goto :missing
if not exist "%DLL%" goto :missing

start "PullCloud" /min "%DOTNET%" "%DLL%"
exit /b 0

:missing
echo.
echo  Some files are missing. Please unzip the WHOLE PullCloud folder
echo  (including the "app" and "runtime" folders) and start again.
echo.
pause
exit /b 1
