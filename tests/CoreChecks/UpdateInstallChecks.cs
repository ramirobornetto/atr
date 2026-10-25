using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using AchievementTimelineReplay;

static class UpdateInstallChecks
{
    static void Check(bool value,string text){if(!value)throw new Exception(text);Console.WriteLine("PASS: Auto-update — "+text);}
    static void Add(ZipArchive zip,string name,string text="new"){using var writer=new StreamWriter(zip.CreateEntry(name).Open());writer.Write(text);}
    static string Package(string root,string name,Action<ZipArchive> extra=null)
    {
        string path=Path.Combine(root,name);
        using var zip=ZipFile.Open(path,ZipArchiveMode.Create);
        foreach(string file in new[]{"AchievementTimelineReplay.Wpf.exe","AchievementTimelineReplay.Wpf.dll","ATR.Updater.exe","ATR.Updater.dll"})Add(zip,file);
        extra?.Invoke(zip);return path;
    }
    static void RejectArchive(string package,string stage,string text)
    {
        try{UpdateInstaller.Extract(package,stage);throw new Exception("Expected rejected ZIP");}catch(InvalidDataException){Check(true,text);}
    }
    sealed class FakeDownload : HttpMessageHandler
    {
        public byte[] Bytes;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {token.ThrowIfCancellationRequested();return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(Bytes)});}
    }
    public static void Run(string testRoot)
    {
        string root=Path.Combine(testRoot,"update-install");Directory.CreateDirectory(root);
        string package=Package(root,"good.zip",z=>Add(z,"muestras/demo.json","demo"));
        string hash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(package))).ToLowerInvariant();long size=new FileInfo(package).Length;
        UpdateInstaller.Verify(package,hash,size);Check(true,"actual ZIP hash and length verify before installation");
        try{UpdateInstaller.Verify(package,new string('0',64),size);throw new Exception("Expected hash rejection");}catch(InvalidDataException){Check(true,"corrupt or changed ZIP cannot install");}
        string stage=Path.Combine(root,"stage");UpdateInstaller.Extract(package,stage);
        Check(File.ReadAllText(Path.Combine(stage,"muestras","demo.json"))=="demo","complete ZIP extracts into isolated staging");
        RejectArchive(Package(root,"traversal.zip",z=>Add(z,"../escape.txt")),Path.Combine(root,"bad-traversal"),"ZIP directory traversal is rejected");
        Check(!File.Exists(Path.Combine(root,"escape.txt")),"rejected traversal writes nothing outside staging");
        RejectArchive(Package(root,"profile.zip",z=>Add(z,"Snapshots/overwrite.json")),Path.Combine(root,"bad-profile"),"package cannot replace user snapshot directories");
        RejectArchive(Package(root,"library.zip",z=>Add(z,"Library/catalog.json")),Path.Combine(root,"bad-library"),"update package cannot replace persisted user library index");

        foreach(string reserved in new[]{"Mappings/record.json","Xbox/session.dat","Epic/session.dat","Runs/latest.json","history-choices.json","source-options.json"})
            RejectArchive(Package(root,"protected-"+reserved.Replace('/','-')+".zip",z=>Add(z,reserved)),Path.Combine(root,"protected-"+reserved.Replace('/','-')),"update package preserves authored mappings and source preferences: "+reserved);
        RejectArchive(Package(root,"duplicate.zip",z=>Add(z,"atr.updater.DLL")),Path.Combine(root,"bad-duplicate"),"case-colliding archive entries are rejected on every platform");
        RejectArchive(Package(root,"symlink.zip",z=>{var link=z.CreateEntry("link");link.ExternalAttributes=unchecked((int)0xA1FF0000);}),Path.Combine(root,"bad-symlink"),"ZIP symbolic links are rejected");
        string target=Path.Combine(root,"application");Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target,"AchievementTimelineReplay.Wpf.exe"),"old executable");File.WriteAllText(Path.Combine(target,"user-notes.txt"),"keep notes");
        Directory.CreateDirectory(Path.Combine(target,"Plans"));File.WriteAllText(Path.Combine(target,"Plans","keep.json"),"keep plan");
        string[] userData={"Library/catalog.json","Snapshots/source/snapshot.json","Runs/source/latest.json","Xbox/session.dat","Epic/session.dat","Epic/Browser/Default/Cookies","Mappings/choices.json","source-options.json"};
        foreach(string relative in userData){string file=Path.Combine(target,relative);Directory.CreateDirectory(Path.GetDirectoryName(file));File.WriteAllText(file,"fixture "+relative);}
        File.WriteAllText(Path.Combine(stage,"Microsoft.Web.WebView2.Core.dll"),"SDK fixture");
        var installed=UpdateInstaller.Install(stage,target,Path.Combine(root,"backup"));
        Check(File.ReadAllText(Path.Combine(target,"AchievementTimelineReplay.Wpf.exe"))=="new" && File.ReadAllText(Path.Combine(target,"user-notes.txt"))=="keep notes" && File.ReadAllText(Path.Combine(target,"Plans","keep.json"))=="keep plan","installation replaces program files while preserving unrelated files and plans");
        Check(userData.All(relative=>File.ReadAllText(Path.Combine(target,relative))=="fixture "+relative) && File.Exists(Path.Combine(target,"Microsoft.Web.WebView2.Core.dll")),"updating browser assemblies preserves library, sessions, browser profile, reports, snapshots and mappings");
        installed.Rollback();
        Check(userData.All(relative=>File.ReadAllText(Path.Combine(target,relative))=="fixture "+relative) && !File.Exists(Path.Combine(target,"Microsoft.Web.WebView2.Core.dll")),"rollback removes introduced browser files and preserves all user data");
        Check(File.ReadAllText(Path.Combine(target,"AchievementTimelineReplay.Wpf.exe"))=="old executable" && !File.Exists(Path.Combine(target,"ATR.Updater.dll")),"rollback restores old files and removes files introduced by update");
        string partialStage=Path.Combine(root,"partial-stage"),partialTarget=Path.Combine(root,"partial-target");Directory.CreateDirectory(partialStage);Directory.CreateDirectory(partialTarget);
        File.WriteAllText(Path.Combine(partialStage,"a.dll"),"new a");File.WriteAllText(Path.Combine(partialStage,"b.dll"),"new b");File.WriteAllText(Path.Combine(partialStage,"z.dll"),"blocked");
        File.WriteAllText(Path.Combine(partialTarget,"a.dll"),"old a");Directory.CreateDirectory(Path.Combine(partialTarget,"z.dll"));
        try{UpdateInstaller.Install(partialStage,partialTarget,Path.Combine(root,"partial-backup"));throw new Exception("Expected copy failure");}
        catch(Exception ex)when(ex is IOException || ex is UnauthorizedAccessException)
        {Check(File.ReadAllText(Path.Combine(partialTarget,"a.dll"))=="old a" && !File.Exists(Path.Combine(partialTarget,"b.dll")) && Directory.Exists(Path.Combine(partialTarget,"z.dll")),"mid-install failure automatically rolls back earlier replacements without deleting existing directories");}
        var info=new UpdateInfo{Version="0.8.0",DownloadUrl="https://github.com/ramirobornetto/atr/raw/refs/heads/main/downloads/ATR_v0.8.0_Windows_x64.zip",Sha256=hash,SizeBytes=size};
        string download=Path.Combine(root,"download.zip");
        AutomaticUpdate.DownloadAsync(info,download,null,CancellationToken.None,new FakeDownload{Bytes=File.ReadAllBytes(package)}).GetAwaiter().GetResult();
        Check(File.ReadAllBytes(download).SequenceEqual(File.ReadAllBytes(package)) && !File.Exists(download+".part"),"automatic download completes only after size and SHA256 verification");
        info.Sha256=new string('0',64);string corrupt=Path.Combine(root,"corrupt-download.zip");
        try{AutomaticUpdate.DownloadAsync(info,corrupt,null,CancellationToken.None,new FakeDownload{Bytes=File.ReadAllBytes(package)}).GetAwaiter().GetResult();throw new Exception("Expected bad download rejection");}
        catch(InvalidDataException){Check(!File.Exists(corrupt) && !File.Exists(corrupt+".part"),"failed download verification deletes partial package and never installs");}
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();string cancelledFile=Path.Combine(root,"cancelled.zip");
        try{AutomaticUpdate.DownloadAsync(info,cancelledFile,null,cancelled.Token,new FakeDownload{Bytes=File.ReadAllBytes(package)}).GetAwaiter().GetResult();throw new Exception("Expected cancellation");}
        catch(OperationCanceledException){Check(!File.Exists(cancelledFile) && !File.Exists(cancelledFile+".part"),"cancelling automatic download leaves installed files unchanged");}
        var game=new GameEntry{AppId=42,AccountId=7};game.Achievements.Add(new AchievementRecord{ApiId="A",OriginalUnix=100});
        var savedStore=new PlanStore(Path.Combine(root,"saved-plans"));var recoveryStore=new PlanStore(Path.Combine(root,"update-drafts"));
        savedStore.Save(game,PlanStore.Capture(game,game.Achievements,new PlanParameters()));string savedBytes=File.ReadAllText(savedStore.FilePath(game));
        var draft=game.Achievements[0].Clone();draft.Included=false;draft.ManualGapSeconds=33;
        recoveryStore.Save(game,PlanStore.Capture(game,new[]{draft},new PlanParameters{CustomOrder=true}));
        var recovered=PlanStore.Restore(game,recoveryStore.Load(game));
        Check(!recovered.Records[0].Included && recovered.Records[0].ManualGapSeconds==33 && File.ReadAllText(savedStore.FilePath(game))==savedBytes,"update drafts can be recovered without overwriting explicitly saved plans");
    }
}
