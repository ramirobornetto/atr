@echo off
setlocal
cd /d "%~dp0"
where dotnet >nul 2>&1
if errorlevel 1 (
 echo Se requiere .NET 8 SDK solo para regenerar el paquete.
 exit /b 1
)
dotnet publish "..\Updater\ATR.Updater.csproj" -c Release -r win-x64 --self-contained true -o "..\artifacts\Updater-win-x64"
if errorlevel 1 exit /b 1
dotnet publish "AchievementTimelineReplay.Wpf.csproj" -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=false -o "..\artifacts\Windows-x64"
if errorlevel 1 exit /b 1
for %%F in (ATR.Updater.exe ATR.Updater.dll ATR.Updater.deps.json ATR.Updater.runtimeconfig.json) do copy /Y "..\artifacts\Updater-win-x64\%%F" "..\artifacts\Windows-x64\%%F" >nul
if errorlevel 1 exit /b 1
echo Ejecutable, actualizador y dependencias en artifacts\Windows-x64. Distribuir la carpeta completa.
echo Para publicar ZIP, muestras y latest.json: python ..\tools\publicar-windows.py
