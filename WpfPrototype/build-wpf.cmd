@echo off
setlocal
cd /d "%~dp0"
where dotnet >nul 2>&1
if errorlevel 1 (
 echo No encontre dotnet. Se requiere .NET 8 SDK.
 pause
 exit /b 1
)
echo.
echo Compilando Achievement Timeline Replay WPF v0.7.1...
dotnet build "AchievementTimelineReplay.Wpf.csproj" -c Release
if errorlevel 1 (
 echo.
 echo ERROR: No se pudo compilar la version WPF. Conserva v0.5.3 estable.
 pause
 exit /b 1
)
echo.
echo Compilacion terminada. El .exe esta en bin\Release\net8.0-windows\
pause
