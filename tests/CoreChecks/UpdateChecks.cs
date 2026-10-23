using System;
using System.IO;
using System.Text.Json;
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
        UpdateInstallChecks.Run(root);

    }
}
