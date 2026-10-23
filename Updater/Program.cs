using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Security.Cryptography;
using System.Text;
using AchievementTimelineReplay;

internal static class Program
{
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int MessageBoxW(IntPtr owner,string text,string title,uint type);
    static void Main(string[] args)
    {
        if(args.Length!=1)return;
        string work=Path.GetDirectoryName(Path.GetFullPath(args[0]));UpdateRequest request=null;InstallBackup installed=null;bool parentStopped=false;Mutex installLock=null;bool ownsLock=false;
        void Log(string text){try{File.AppendAllText(Path.Combine(work,"update.log"),DateTime.UtcNow.ToString("O")+" "+text+Environment.NewLine);}catch{}}
        Process Restart() => Process.Start(new ProcessStartInfo{FileName=Path.Combine(request.TargetDirectory,"AchievementTimelineReplay.Wpf.exe"),WorkingDirectory=request.TargetDirectory,UseShellExecute=true});
        try
        {
            if(new FileInfo(args[0]).Length>16384)throw new InvalidDataException("Solicitud de actualización inválida.");
            request=JsonSerializer.Deserialize<UpdateRequest>(File.ReadAllText(args[0]));
            if(request==null || !Directory.Exists(request.TargetDirectory) || !Path.IsPathFullyQualified(request.TargetDirectory) || !Path.IsPathFullyQualified(request.PackagePath))throw new InvalidDataException("Solicitud incompleta.");
            string lockName="Local\\ATR_Update_"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(request.TargetDirectory).ToUpperInvariant()))).Substring(0,24);
            installLock=new Mutex(false,lockName);ownsLock=installLock.WaitOne(0);
            if(!ownsLock)throw new IOException("Ya hay otra actualización en curso para esta carpeta.");
            // Revalidate the publication fields independently of the UI process.
            UpdateChecker.Parse(JsonSerializer.Serialize(new UpdateInfo{Version=request.Version,DownloadUrl="https://github.com/ramirobornetto/atr/raw/refs/heads/main/downloads/ATR_v"+request.Version+"_Windows_x64.zip",Sha256=request.Sha256,SizeBytes=request.SizeBytes}));
            try
            {
                using var parent=Process.GetProcessById(request.ParentProcessId);
                if(parent.StartTime.ToUniversalTime().Ticks==request.ParentStartTicks && !parent.WaitForExit(120000))throw new IOException("ATR no terminó de cerrarse; no se modificó la instalación.");
            }
            catch(ArgumentException){} // The original process already exited.
            parentStopped=true;
            string application=Path.Combine(request.TargetDirectory,"AchievementTimelineReplay.Wpf.exe");
            foreach(var process in Process.GetProcessesByName("AchievementTimelineReplay.Wpf"))
                using(process)
                {
                    try{if(String.Equals(process.MainModule?.FileName,application,StringComparison.OrdinalIgnoreCase)){parentStopped=false;throw new IOException("Hay otra instancia de ATR abierta en esta carpeta. Cerrala y reintentá.");}}
                    catch(System.ComponentModel.Win32Exception){throw new IOException("No se pudo comprobar si otra instancia mantiene archivos abiertos.");}
                }
            UpdateInstaller.Verify(request.PackagePath,request.Sha256,request.SizeBytes);
            string stage=Path.Combine(work,"stage");UpdateInstaller.Extract(request.PackagePath,stage);
            var version=AssemblyName.GetAssemblyName(Path.Combine(stage,"AchievementTimelineReplay.Wpf.dll")).Version;
            if(new Version(version.Major,version.Minor,version.Build)!=Version.Parse(request.Version))throw new InvalidDataException("La versión de la DLL no coincide con la publicación.");
            installed=UpdateInstaller.Install(stage,request.TargetDirectory,Path.Combine(work,"backup"));
            using var started=Restart()??throw new IOException("No se pudo reiniciar ATR.");
            if(started.WaitForExit(2000) && started.ExitCode!=0)throw new IOException("La nueva versión terminó con error al arrancar.");
            Log("Actualización instalada y ATR reiniciado. Copia anterior: "+Path.Combine(work,"backup"));
        }
        catch(Exception ex)
        {
            Log(ex.ToString());string recovery="No se aplicó la actualización.";
            if(installed!=null)
            {
                try{installed.Rollback();recovery="Se restauró la versión anterior.";}
                catch(Exception rollback){Log(rollback.ToString());recovery="La restauración requiere revisión. La copia anterior está en "+Path.Combine(work,"backup");}
            }
            if(parentStopped && request!=null)try{Restart()?.Dispose();}catch(Exception restart){Log(restart.ToString());}
            MessageBoxW(IntPtr.Zero,"No se pudo completar la actualización. "+recovery+"\n\n"+ex.Message+"\n\nDetalle: "+Path.Combine(work,"update.log"),"ATR · Actualización",0x10);
        }
        finally{if(ownsLock)installLock.ReleaseMutex();installLock?.Dispose();}
    }
}
