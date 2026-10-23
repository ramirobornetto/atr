@echo off
cd /d "%~dp0"
if not exist "AchievementTimelineReplay.exe" (
  call build.cmd
  exit /b
)
start "" "AchievementTimelineReplay.exe"
