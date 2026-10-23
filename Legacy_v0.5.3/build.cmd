@echo off
setlocal
cd /d "%~dp0"

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"

if not exist "%CSC%" (
  echo.
  echo No encontre el compilador C# de .NET Framework en Windows.
  echo Instala .NET Framework 4.8 Developer Pack o compila el proyecto con Visual Studio.
  echo.
  pause
  exit /b 1
)

echo Compilando AchievementTimelineReplay.exe...
"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ ^
 /r:System.dll ^
 /r:System.Core.dll ^
 /r:System.Drawing.dll ^
 /r:System.Windows.Forms.dll ^
 /r:System.Web.Extensions.dll ^
 /out:"AchievementTimelineReplay.exe" ^
 "AchievementTimelineReplay.cs"

if errorlevel 1 (
  echo.
  echo La compilacion fallo.
  pause
  exit /b 1
)

echo.
echo Listo: AchievementTimelineReplay.exe
echo.
start "" "AchievementTimelineReplay.exe"
