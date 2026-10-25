using System;
using System.IO;
using System.Text.Json;
using System.Net;
using System.Net.Http;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AchievementTimelineReplay;

static class UpdateChecks
{
    static void Check(bool condition,string text){if(!condition)throw new Exception(text);Console.WriteLine("PASS: Updates — "+text);}
    static UpdateInfo Info(string version="0.8.0")=>new UpdateInfo{Version=version,DownloadUrl="https://github.com/ramirobornetto/atr/raw/refs/heads/main/downloads/ATR_v"+version+"_Windows_x64.zip",Sha256=new string('a',64),SizeBytes=70000000};
    static void Reject(UpdateInfo info,string text)
    {
        try{UpdateChecker.Parse(JsonSerializer.Serialize(info));throw new Exception("Expected invalid update");}catch(InvalidDataException){Check(true,text);}
    }
    public static void Run(string root)
    {
        var parsed=UpdateChecker.Parse(JsonSerializer.Serialize(Info()));
        Check(parsed.IsNewerThan(new Version(0,7,1,0)),"newer published version is detected");
        Check(!parsed.IsNewerThan(new Version(0,8,0,0)) && !parsed.IsNewerThan(new Version(0,9,0,0)),"equal build revision and older publications do not trigger an update");
        var bad=Info();bad.DownloadUrl="https://example.com/update.exe";Reject(bad,"external download targets are rejected");
        bad=Info();bad.DownloadUrl=bad.DownloadUrl.Replace("https:","http:");Reject(bad,"non-HTTPS targets are rejected");
        bad=Info();bad.DownloadUrl=bad.DownloadUrl.Replace("0.8.0","0.9.0");Reject(bad,"version and download filename must agree");
        bad=Info();bad.Sha256="invalid";Reject(bad,"malformed package hash is rejected");
        bad=Info();bad.SizeBytes=0;Reject(bad,"empty download declaration is rejected");
        bad=Info();bad.Version="0.8.0.1";Reject(bad,"unsupported manifest version format is rejected");
        using(var transport=new ManifestHandler())
        {
            var first=UpdateChecker.CheckAsync(CancellationToken.None,transport).GetAwaiter().GetResult();
            var second=UpdateChecker.CheckAsync(CancellationToken.None,transport).GetAwaiter().GetResult();
            Check(first.Version=="0.17.4" && second.IsNewerThan(new Version(0,17,3)),"cache-resistant requests still parse and compare the published update normally");
            Check(transport.Urls.Count==2 && transport.Urls[0]!=transport.Urls[1] && transport.ValidRequests,"successive lookups bypass a fixed-URL cache without changing the trusted endpoint or weakening validation");
        }
        UpdateInstallChecks.Run(root);
        CompactPackageChecks.Run(root);

    }
    sealed class ManifestHandler : HttpMessageHandler
    {
        public List<string> Urls=new List<string>();
        public bool ValidRequests=true;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            var uri=request.RequestUri;Urls.Add(uri.AbsoluteUri);
            ValidRequests &= request.Method==HttpMethod.Get && uri.Scheme=="https" && uri.Host=="raw.githubusercontent.com" && uri.AbsolutePath=="/ramirobornetto/atr/main/downloads/latest-compact.json" && uri.Query.StartsWith("?request=") && Guid.TryParseExact(uri.Query.Substring(9),"N",out _) && request.Headers.CacheControl?.NoCache==true && request.Headers.CacheControl?.NoStore==true && request.Headers.CacheControl?.MaxAge==TimeSpan.Zero;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(Info("0.17.4")))});
        }
    }

}
