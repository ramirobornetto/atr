using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using AchievementTimelineReplay;

static class IniChecks
{
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);Console.WriteLine("PASS: INI — "+message);}
    static void Reject(string path,string text)
    {
        File.WriteAllText(path,text);
        try{IniHistory.Load(path,42);throw new Exception("Expected invalid INI rejection");}catch(InvalidDataException){}
    }
    public static void Run(string root)
    {
        string dir=Path.Combine(root,"ini","CODEX","42");Directory.CreateDirectory(dir);
        string path=Path.Combine(dir,"achievements.ini");var warnings=new List<string>();
        string text="; header\n[LATE]\nAchieved=1\nUnlockTime=1704153600\n[FIRST]\nachieved=1 ; comment\nunlocktime=1704067200\n[LOCKED]\nAchieved=0\nUnlockTime=1704067000\nCurProgress=10\nMaxProgress=10\n[NO_DATE]\nAchieved=1\nUnlockTime=0\n[PROGRESS_ONLY]\nCurProgress=10\nMaxProgress=10\nUnlockTime=1704067100\n[SteamAchievements]\nCount=4\n00000=FIRST\n";
        File.WriteAllText(path,text);var original=File.ReadAllBytes(path);
        var game=IniHistory.Load(path,42,log:warnings.Add);
        Check(game.Achievements.Count==2 && game.Achievements[0].ApiId=="FIRST" && game.Achievements[1].ApiId=="LATE","only explicit achieved states with valid dates enter chronological plan");
        Check(game.TotalAchievements==4 && warnings.Count==2,"index and progress-only sections do not invent achievements or dates");
        Check(game.DetectedOrigin=="CODEX" && game.OriginEvidence.Contains("directorio"),"CODEX directory suggests source without asserting the writer");
        string snapshots=Path.Combine(root,"ini-snapshots");HistorySnapshots.Protect(game,snapshots);
        Check(game.HasSnapshot && File.ReadAllBytes(path).SequenceEqual(original) && File.ReadAllBytes(Path.Combine(snapshots,game.StorageKey,"original_achievements.ini")).SequenceEqual(original),"snapshot preserves original INI bytes and leaves source untouched");
        File.WriteAllText(path,"[OTHER]\nAchieved=1\nUnlockTime=1704200000\n");
        var restored=HistorySnapshots.Protect(IniHistory.Load(path,42),snapshots);
        Check(restored.Achievements.Count==2 && restored.Achievements[0].ApiId=="FIRST","source edits never overwrite original INI history");
        var plans=new PlanStore(Path.Combine(root,"ini-plans"));var plan=PlanStore.Capture(game,game.Achievements,new PlanParameters());plans.Save(game,plan);
        Check(plans.Load(game).Achievements.Count==2 && plans.Load(game).HistoryId==game.HistoryId,"INI plans persist with independent source identity");
        string remote=Path.Combine(root,"ini","RUNE","42","remote");Directory.CreateDirectory(remote);string remoteFile=Path.Combine(remote,"Achievements.ini");File.WriteAllText(remoteFile,text);
        var other=IniHistory.Load(remoteFile,42);
        Check(other.DetectedOrigin=="RUNE" && other.StorageKey!=game.StorageKey,"RUNE history is isolated from CODEX for the same AppID");
        Check(IniHistory.AppIdFromPath(remoteFile)==42 && IniHistory.AppIdFromPath(path)==42 && IniHistory.AppIdFromPath(Path.Combine(root,"bad","achievements.ini"))==0,"AppID inference supports numeric folder and remote without guessing");
        var summary=new ScanSummary();var found=IniHistory.Scan(Path.Combine(root,"ini","RUNE"),null,Path.Combine(root,"ini-scan"),warnings.Add,CancellationToken.None,summary);
        Check(found.Count==1 && summary.Recovered==1 && summary.Reviewed==1 && summary.Classified==1,"standard root discovers remote state once with reconciled counters");
        var neutral=IniHistory.Load(path,42);neutral.StatsPath=Path.Combine(root,"custom","42","achievements.ini");OriginDetector.Apply(neutral,null);
        Check(neutral.DetectedOrigin=="CODEX/RUNE compatible","unknown directories retain a compatible-format source label");
        File.WriteAllText(path,text,Encoding.Unicode);
        Check(IniHistory.Load(path,42).Achievements.Count==2,"UTF-16 BOM state is supported");
        File.WriteAllText(path,text,new UTF8Encoding(true));
        Check(IniHistory.Load(path,42).Achievements.Count==2,"UTF-8 BOM state is supported");
        Reject(path,"[A]\nAchieved=1\nUnlockTime=1704067200\n[a]\nAchieved=0");Check(true,"duplicate case-insensitive API sections are rejected");
        Reject(path,"[A]\nAchieved=1\nachieved=0");Check(true,"duplicate conflicting keys are rejected");
        Reject(path,"[A]\nAchieved=true\nUnlockTime=1704067200");Check(true,"undocumented achieved values are not guessed");
        Reject(path,"[A]\nCurProgress=10\nMaxProgress=10\nUnlockTime=1704067200");Check(true,"progress-only variants require explicit support");
        Reject(path,"Achieved=1\n[A]\nUnlockTime=1704067200");Check(true,"fields outside sections are rejected");
        File.WriteAllText(path,"[A]\nAchieved=1\nUnlockTime=1704067200000\n[B]\nAchieved=1\nUnlockTime=-1\n[C]\nAchieved=1\nUnlockTime=0x12345678");
        Check(IniHistory.Load(path,42).Achievements.Count==0,"milliseconds, negatives and hex dates are not fabricated into valid times");
        using(var cancellation=new CancellationTokenSource())
        {
            cancellation.Cancel();try{IniHistory.Scan(dir,null,snapshots,warnings.Add,cancellation.Token);throw new Exception("Expected cancellation");}catch(OperationCanceledException){Check(true,"scan cancellation propagates");}
        }
        string fixtureRoot=Path.Combine(AppContext.BaseDirectory,"MuestrasAAA");
        using var catalog=JsonDocument.Parse(File.ReadAllText(Path.Combine(fixtureRoot,"catalogo.json")));
        int fixtureCount=0;
        foreach(var entry in catalog.RootElement.EnumerateArray())
        {
            string source=entry.GetProperty("Origen").GetString();uint id=entry.GetProperty("AppID").GetUInt32();
            string file=Path.Combine(fixtureRoot,entry.GetProperty("Archivo").GetString());bool ini=file.EndsWith(".ini",StringComparison.OrdinalIgnoreCase);
            var fixture=ini?IniHistory.Load(file,id):GoldbergHistory.Load(file,id,Path.Combine(Path.GetDirectoryName(file),"steam_settings","achievements.json"));
            GameNames.Apply(fixture,new Dictionary<uint,string>());
            Check(fixture.IsDemo && fixture.Achievements.Count==4 && fixture.TotalAchievements==6 && fixture.GameName.StartsWith(entry.GetProperty("Juego").GetString(),StringComparison.Ordinal) && fixture.Achievements.All(a=>a.ApiId.StartsWith("ATR_DEMO_",StringComparison.Ordinal)),"shipped AAA fixture: "+source+" / "+id+" gives named 4 / 6 in demo mode");
            Check(fixture.DetectedOrigin==(source=="Goldberg SteamEmu Saves"?"Goldberg":source=="GSE Saves"?"GSE":source),"shipped source inference: "+source+" / "+id);
            if(ini)
            {
                using var reference=JsonDocument.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(file),"referencia-logros.json")));
                Check(reference.RootElement.EnumerateObject().Count()==6 && fixture.Achievements.All(a=>reference.RootElement.GetProperty(a.ApiId).GetProperty("earned_time").GetInt64()==a.OriginalUnix),"INI matches JSON reference: "+id);
            }
            fixtureCount++;
        }
        Check(fixtureCount==20,"all twenty requested AAA folders are verified");
        foreach(string provider in new[]{"Goldberg SteamEmu Saves","GSE Saves","CODEX","RUNE"})
        {
            string folder=Path.Combine(fixtureRoot,provider);var totals=new ScanSummary();
            var imported=provider=="CODEX" || provider=="RUNE"?IniHistory.Scan(folder,null,Path.Combine(root,"AAA-snapshots"),warnings.Add,CancellationToken.None,totals):GoldbergHistory.Scan(folder,null,Path.Combine(root,"AAA-snapshots"),warnings.Add,CancellationToken.None,totals);
            Check(imported.Count==5 && totals.Reviewed==5 && totals.Recovered==5 && totals.Classified==5,"all five fixtures are discoverable under "+provider);
        }
    }
}
