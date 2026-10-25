using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using AchievementTimelineReplay;

static class CompactPackageChecks
{
    static void Check(bool value,string text){if(!value)throw new Exception(text);Console.WriteLine("PASS: Compact package — "+text);}
    static string Hash(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    static void Write(string directory,string relative,string value)
    {
        string file=Path.Combine(directory,relative);Directory.CreateDirectory(Path.GetDirectoryName(file));File.WriteAllText(file,value);
    }
    public static void Run(string testRoot)
    {
        string root=Path.Combine(testRoot,"compact-package");Directory.CreateDirectory(root);
        string stage=Path.Combine(root,"stage"),target=Path.Combine(root,"Application with spaces á");
        var metadata=new UpdateInstaller.CompactPackage{Layout=2,Version="0.19.0"};
        metadata.LegacyFiles["AchievementTimelineReplay.Wpf.exe"]=new[]{Hash("legacy exe")};
        metadata.LegacyFiles["old-runtime.dll"]=new[]{Hash("legacy runtime")};
        metadata.LegacyFiles["muestras/demo.json"]=new[]{Hash("shipped sample")};
        metadata.LegacyFiles["DESCRIPCION.md"]=new[]{Hash("shipped description")};
        Write(stage,"AchievementTimelineReplay.exe","compact exe");
        Write(stage,"Support/package.json",JsonSerializer.Serialize(metadata));
        Write(stage,"Support/Documentation/en-howto.md","English tutorial");
        string zip=Path.Combine(root,"compact.zip");ZipFile.CreateFromDirectory(stage,zip);
        string extracted=Path.Combine(root,"extracted");UpdateInstaller.Extract(zip,extracted);
        Check(Directory.GetFileSystemEntries(extracted).Select(Path.GetFileName).OrderBy(x=>x).SequenceEqual(new[]{"AchievementTimelineReplay.exe","Support"}),"fresh ZIP contains only the public executable and Support at its root");
        Write(target,"AchievementTimelineReplay.Wpf.exe","legacy exe");Write(target,"old-runtime.dll","legacy runtime");
        Write(target,"muestras/demo.json","shipped sample");Write(target,"DESCRIPCION.md","user edited description");Write(target,"personal.txt","user notes");
        Write(target,"Library/catalog.json","user library");Write(target,"Plans/plan.json","user plan");Write(target,"Epic/session.dat","user session");
        var installed=UpdateInstaller.Install(extracted,target,Path.Combine(root,"backup"));
        Check(!File.Exists(Path.Combine(target,"AchievementTimelineReplay.Wpf.exe")) && !File.Exists(Path.Combine(target,"old-runtime.dll")) && !Directory.Exists(Path.Combine(target,"muestras")),"legacy executable, runtime and unmodified samples are retired together with empty sample folders");
        Check(File.ReadAllText(Path.Combine(target,"DESCRIPCION.md"))=="user edited description" && File.ReadAllText(Path.Combine(target,"personal.txt"))=="user notes","edited documentation and unrelated files survive migration");
        Check(File.ReadAllText(Path.Combine(target,"Library/catalog.json"))=="user library" && File.ReadAllText(Path.Combine(target,"Plans/plan.json"))=="user plan" && File.ReadAllText(Path.Combine(target,"Epic/session.dat"))=="user session","library, saved plans and sessions survive compact migration");
        Check(UpdateInstaller.ApplicationPath(target)==Path.Combine(target,"AchievementTimelineReplay.exe"),"restart resolves the renamed public executable");
        installed.Rollback();
        Check(File.ReadAllText(Path.Combine(target,"old-runtime.dll"))=="legacy runtime" && File.ReadAllText(Path.Combine(target,"muestras/demo.json"))=="shipped sample" && !File.Exists(Path.Combine(target,"AchievementTimelineReplay.exe")),"rollback restores retired files and removed sample folders as well as the old executable");
        Check(UpdateInstaller.ApplicationPath(target)==Path.Combine(target,"AchievementTimelineReplay.Wpf.exe"),"failed migration restarts the previous executable");
        metadata.LegacyFiles["Plans/plan.json"]=new[]{Hash("user plan")};
        Write(extracted,"Support/package.json",JsonSerializer.Serialize(metadata));
        try{UpdateInstaller.Install(extracted,target,Path.Combine(root,"rejected-backup"));throw new Exception("Expected protected-path rejection");}
        catch(InvalidDataException){Check(File.ReadAllText(Path.Combine(target,"old-runtime.dll"))=="legacy runtime" && File.ReadAllText(Path.Combine(target,"Plans/plan.json"))=="user plan" && !File.Exists(Path.Combine(target,"AchievementTimelineReplay.exe")),"malformed cleanup inventory cannot retire user data and rolls back files already changed");}
        metadata.LegacyFiles.Remove("Plans/plan.json");metadata.LegacyFiles["../outside.txt"]=new[]{Hash("outside")};Write(extracted,"Support/package.json",JsonSerializer.Serialize(metadata));
        try{UpdateInstaller.Install(extracted,target,Path.Combine(root,"traversal-backup"));throw new Exception("Expected traversal rejection");}
        catch(InvalidDataException){Check(!File.Exists(Path.Combine(target,"AchievementTimelineReplay.exe")),"cleanup path traversal is rejected and changes rolled back");}
        string compact=Path.Combine(root,"compact installed");Write(compact,"AchievementTimelineReplay.exe","bundled app and installer");Write(compact,"Support/Documentation/en-howto.md","docs");
        string work=Path.Combine(root,"work");Directory.CreateDirectory(work);
        var info=new UpdateInfo{Version="0.19.0",Sha256=new string('a',64),SizeBytes=1};
        var start=AutomaticUpdate.PrepareInstaller(info,zip,compact,work,CancellationToken.None);
        var request=JsonSerializer.Deserialize<UpdateRequest>(File.ReadAllText(Path.Combine(work,"request.json")));
        Check(start.ArgumentList.Count==2 && start.ArgumentList[0]=="--atr-update" && request.TargetDirectory==Path.GetFullPath(compact) && File.ReadAllText(start.FileName)=="bundled app and installer" && Directory.GetFiles(Path.GetDirectoryName(start.FileName)).Length==1,"compact updater copies just the bundled executable outside the installation and passes an explicit installer-mode argument");
        string legacy=Path.Combine(root,"legacy installed");Write(legacy,"ATR.Updater.exe","old helper");Write(legacy,"ATR.Updater.dll","helper dll");
        var legacyStart=AutomaticUpdate.PrepareInstaller(info,zip,legacy,Path.Combine(root,"legacy-work"),CancellationToken.None);
        Check(legacyStart.ArgumentList.Count==1 && File.Exists(Path.Combine(Path.GetDirectoryName(legacyStart.FileName),"ATR.Updater.dll")),"transition release retains the legacy helper launch until compact installation");
    }
}
