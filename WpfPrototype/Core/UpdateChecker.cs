using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AchievementTimelineReplay
{
    internal sealed class UpdateInfo
    {
        public string Version {get;set;}
        public string DownloadUrl {get;set;}
        public string Sha256 {get;set;}
        public long SizeBytes {get;set;}
        public bool IsNewerThan(System.Version current)
        {
            var installed=new System.Version(current.Major,current.Minor,Math.Max(0,current.Build));
            return System.Version.Parse(Version)>installed;
        }
    }
    internal static class UpdateChecker
    {
        const string ManifestUrl="https://raw.githubusercontent.com/ramirobornetto/atr/main/downloads/latest.json";
        public static UpdateInfo Parse(string json)
        {
            if(String.IsNullOrEmpty(json) || json.Length>16384)throw new InvalidDataException("Respuesta de actualización inválida.");
            var info=JsonSerializer.Deserialize<UpdateInfo>(json);
            if(info==null || !Version.TryParse(info.Version,out var version) || version.Build<0 || version.Revision>=0 || version.ToString(3)!=info.Version)
                throw new InvalidDataException("Versión de actualización inválida.");
            string expected="https://github.com/ramirobornetto/atr/raw/refs/heads/main/downloads/ATR_v"+info.Version+"_Windows_x64.zip";
            if(info.DownloadUrl!=expected || info.SizeBytes<=0 || info.SizeBytes>200L*1024*1024 || info.Sha256==null || info.Sha256.Length!=64 || !info.Sha256.All(c=>(c>='0' && c<='9') || (c>='a' && c<='f')))
                throw new InvalidDataException("El manifiesto no describe un ZIP válido del repositorio de ATR.");
            return info;
        }
        public static async Task<UpdateInfo> CheckAsync(CancellationToken token)
        {
            using var client=new HttpClient{Timeout=TimeSpan.FromSeconds(15)};
            client.DefaultRequestHeaders.UserAgent.ParseAdd("AchievementTimelineReplay/0.8.1");
            client.DefaultRequestHeaders.CacheControl=new System.Net.Http.Headers.CacheControlHeaderValue{NoCache=true};
            using var response=await client.GetAsync(ManifestUrl,HttpCompletionOption.ResponseHeadersRead,token);
            response.EnsureSuccessStatusCode();
            if(response.Content.Headers.ContentLength>16384)throw new InvalidDataException("Manifiesto demasiado grande.");
            using var stream=await response.Content.ReadAsStreamAsync(token);
            using var bytes=new MemoryStream();var buffer=new byte[4096];int count;
            while((count=await stream.ReadAsync(buffer,token))>0)
            {
                if(bytes.Length+count>16384)throw new InvalidDataException("Manifiesto demasiado grande.");
                bytes.Write(buffer,0,count);
            }
            return Parse(System.Text.Encoding.UTF8.GetString(bytes.ToArray()));
        }
    }
}
