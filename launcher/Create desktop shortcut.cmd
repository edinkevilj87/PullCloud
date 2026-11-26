@echo off
rem Creates a "Pull from the Cloud" shortcut on the Desktop (no console window when starting).
setlocal
set "APPDIR=%~dp0app"
set "DOTNET=%~dp0runtime\dotnet.exe"
if not exist "%DOTNET%" (
  echo Some files are missing. Please unzip the WHOLE PullCloud folder and try again.
  pause
  exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$s = (New-Object -ComObject WScript.Shell).CreateShortcut([IO.Path]::Combine([Environment]::GetFolderPath('Desktop'), 'Pull from the Cloud.lnk'));" ^
  "$s.TargetPath = $env:DOTNET;" ^
  "$s.Arguments = '\"' + $env:APPDIR + '\PullCloud.dll\"';" ^
  "$s.WorkingDirectory = $env:APPDIR;" ^
  "$s.WindowStyle = 7;" ^
  "$s.IconLocation = $env:APPDIR + '\PullCloud.ico';" ^
  "$s.Description = 'Pull from the Cloud calculator';" ^
  "$s.Save()"

if errorlevel 1 (
  echo Could not create the shortcut. You can always use "Start PullCloud.cmd".
) else (
  echo Shortcut "Pull from the Cloud" created on your Desktop.
)
pause
