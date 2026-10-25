@echo off
setlocal
cd /d "%~dp0"
if not exist "bin\Release\net8.0-windows\AchievementTimelineReplay.exe" (
  call build-wpf.cmd
  if errorlevel 1 exit /b 1
)
start "" "%~dp0bin\Release\net8.0-windows\AchievementTimelineReplay.exe"
