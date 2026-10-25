using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AchievementTimelineReplay
{
    internal sealed class UpdateRequest
    {
        public string TargetDirectory {get;set;}
        public string PackagePath {get;set;}
        public string Version {get;set;}
        public string Sha256 {get;set;}
        public long SizeBytes {get;set;}
        public int ParentProcessId {get;set;}
        public long ParentStartTicks {get;set;}
    }
    internal static class AutomaticUpdate
    {
        public static async Task DownloadAsync(UpdateInfo info,string destination,IProgress<int> progress,CancellationToken token,HttpMessageHandler handler=null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination));string partial=destination+".part";
            try
            {
                using var client=new HttpClient(handler??new HttpClientHandler()){Timeout=TimeSpan.FromMinutes(15)};
                using var response=await client.GetAsync(info.DownloadUrl,HttpCompletionOption.ResponseHeadersRead,token);
                response.EnsureSuccessStatusCode();using var source=await response.Content.ReadAsStreamAsync(token);
                using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);long total=0;var buffer=new byte[65536];int count;
                using(var file=new FileStream(partial,FileMode.CreateNew,FileAccess.Write,FileShare.None,65536,true))
                {
                    while((count=await source.ReadAsync(buffer,token))>0)
                    {
                        total+=count;if(total>info.SizeBytes)throw new InvalidDataException(UiText.Text("La descarga supera el tamaño publicado."));
                        hash.AppendData(buffer,0,count);await file.WriteAsync(buffer.AsMemory(0,count),token);progress?.Report((int)(100*total/info.SizeBytes));
                    }
                    await file.FlushAsync(token);file.Flush(true);
                }
                string actual=Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
                if(total!=info.SizeBytes || actual!=info.Sha256)throw new InvalidDataException(UiText.Text("La descarga no coincide con el tamaño/SHA256 publicado. No se instalará."));
                File.Move(partial,destination,false);
            }
            finally{if(File.Exists(partial))File.Delete(partial);}
        }
        public static ProcessStartInfo PrepareInstaller(UpdateInfo info,string package,string applicationDirectory,string workDirectory,CancellationToken token)
        {
            string helper=Path.Combine(workDirectory,"helper");Directory.CreateDirectory(helper);
            // The compact app includes its installer mode. Run a temporary copy so no
            // executable or bundled dependency is held open inside the installation.
            bool compact=File.Exists(Path.Combine(applicationDirectory,"AchievementTimelineReplay.exe"));
            string executable=Path.Combine(helper,"ATR.Updater.exe");
            if(compact)File.Copy(Path.Combine(applicationDirectory,"AchievementTimelineReplay.exe"),executable,false);
            else foreach(string file in Directory.GetFiles(applicationDirectory,"*",SearchOption.TopDirectoryOnly)){token.ThrowIfCancellationRequested();File.Copy(file,Path.Combine(helper,Path.GetFileName(file)),false);}
            if(!File.Exists(executable))throw new FileNotFoundException(UiText.Text("Falta el componente de actualización automática."),executable);
            using var current=Process.GetCurrentProcess();
            var request=new UpdateRequest{TargetDirectory=Path.GetFullPath(applicationDirectory),PackagePath=Path.GetFullPath(package),Version=info.Version,Sha256=info.Sha256,SizeBytes=info.SizeBytes,ParentProcessId=current.Id,ParentStartTicks=current.StartTime.ToUniversalTime().Ticks};
            string requestPath=Path.Combine(workDirectory,"request.json");File.WriteAllText(requestPath,JsonSerializer.Serialize(request));
            var start=new ProcessStartInfo{FileName=executable,UseShellExecute=true,WorkingDirectory=helper,WindowStyle=ProcessWindowStyle.Hidden};if(compact)start.ArgumentList.Add("--atr-update");start.ArgumentList.Add(requestPath);
            string probe=Path.Combine(applicationDirectory,".atr-update-"+Guid.NewGuid().ToString("N"));
            try{using var file=new FileStream(probe,FileMode.CreateNew,FileAccess.Write,FileShare.None,1,FileOptions.DeleteOnClose);}
            catch(UnauthorizedAccessException){start.Verb="runas";}
            return start;
        }
    }
}
