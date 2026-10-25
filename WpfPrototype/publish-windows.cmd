@echo off
setlocal
cd /d "%~dp0.."
where dotnet >nul 2>&1
if errorlevel 1 (
 echo Se requiere .NET 8 SDK para regenerar el paquete.
 exit /b 1
)
python tools\publicar-windows.py
if errorlevel 1 exit /b 1
echo Paquete compacto y manifiesto generados en downloads.
