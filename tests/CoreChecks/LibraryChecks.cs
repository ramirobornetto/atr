using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;
using AchievementTimelineReplay;

static class LibraryChecks
{
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);Console.WriteLine("PASS: Library — "+message);}
    static void CopyFolder(string from,string to)
    {
        Directory.CreateDirectory(to);
        foreach(var file in Directory.GetFiles(from))File.Copy(file,Path.Combine(to,Path.GetFileName(file)),false);
        foreach(var directory in Directory.GetDirectories(from))CopyFolder(directory,Path.Combine(to,Path.GetFileName(directory)));
    }
    public static void Run(string testRoot)
    {
        string root=Path.Combine(testRoot,"library"),snapshots=Path.Combine(root,"snapshots");Directory.CreateDirectory(root);
        var store=new LibraryStore(Path.Combine(root,"catalog"),snapshots);
        Check(store.Load().Catalog==null,"first launch without a catalog returns an empty library");
        string fixtures=Path.Combine(AppContext.BaseDirectory,"MuestrasAAA");
        string goldDir=Path.Combine(root,"GSE Saves","2322010"),iniDir=Path.Combine(root,"CODEX","292030");
        CopyFolder(Path.Combine(fixtures,"GSE Saves","2322010"),goldDir);CopyFolder(Path.Combine(fixtures,"CODEX","292030"),iniDir);
        var gold=GoldbergHistory.Load(Path.Combine(goldDir,"achievements.json"),2322010,Path.Combine(goldDir,"steam_settings","achievements.json"));
        var ini=IniHistory.Load(Path.Combine(iniDir,"achievements.ini"),292030);HistorySnapshots.Protect(gold,snapshots);HistorySnapshots.Protect(ini,snapshots);
        GameNames.Apply(gold,new Dictionary<uint,string>());GameNames.Apply(ini,new Dictionary<uint,string>());
        string stats=Path.Combine(root,"appcache","stats","UserGameStats_7_292030.bin");Directory.CreateDirectory(Path.GetDirectoryName(stats));File.WriteAllText(stats,"deliberately unreadable binary source");
        var steam=new GameEntry{AppId=292030,AccountId=7,GameName="Steam title",StatsPath=stats,TotalAchievements=5,HasSnapshot=true,HasLocalHistory=true};
        string steamSnapshot=store.SnapshotPath(steam);Directory.CreateDirectory(Path.GetDirectoryName(steamSnapshot));
        File.WriteAllText(steamSnapshot,"{\"AppId\":292030,\"AccountId\":7,\"CreatedAtUtc\":\"/Date(0)/\",\"Achievements\":[{\"ApiId\":\"KNOWN\",\"DisplayName\":\"Protected original\",\"Description\":\"Original\",\"OriginalUnix\":1704067200}]}");
        var all=new[]{gold,ini,steam};DateTimeOffset scan=DateTimeOffset.UtcNow.AddMinutes(-15);
        store.Save(all,root,scan,"Original scan summary");var loaded=new LibraryStore(Path.Combine(root,"catalog"),snapshots).Load();
        Check(loaded.Games.Count==3 && loaded.Catalog.LastScanAtUtc==scan && loaded.Catalog.Summary=="Original scan summary","catalog survives a new store instance with sources and scan timestamp");
        Check(loaded.Games.Single(g=>g.HistoryId==null).Achievements.Single().DisplayName=="Protected original","Steam cache loads its protected snapshot without parsing the source binary");
        Check(loaded.Games.Single(g=>g.HistoryId==null).TotalAchievements==5 && loaded.Games.Single(g=>g.AppId==2322010).TotalAchievements==6,"known full totals survive catalog reload");
        Check(loaded.Games.Count(g=>g.AppId==292030)==2 && loaded.Games.Select(g=>g.StorageKey).Distinct().Count()==3,"Steam and INI remain independent for the same AppID");
        Check(loaded.Games.Where(g=>g.HistoryId!=null).All(g=>g.IsDemo && g.HasSnapshot && g.HasLocalHistory),"cached samples retain Steam action isolation and protected records");
        Check(loaded.Games.Single(g=>g.AppId==2322010).DetectedOrigin=="GSE" && loaded.Games.Single(g=>g.AppId==292030 && g.HistoryId!=null).DetectedOrigin=="CODEX","cached sources regain detected origins without storing manual overrides");
        var restoredSteam=loaded.Games.Single(g=>g.HistoryId==null);
        Check(LibraryStore.NeedsAccountRefresh(steam,8) && !LibraryStore.NeedsAccountRefresh(steam,7) && !LibraryStore.NeedsAccountRefresh(steam,0) && !LibraryStore.NeedsAccountRefresh(ini,8),"changed Steam account requires refresh while local emulation histories remain account-neutral");

        Check(restoredSteam.Achievements[0].Included && restoredSteam.Achievements[0].ManualGapSeconds==null && restoredSteam.Achievements[0].SteamState=="—","catalog does not treat old Steam states or draft edits as current official data");
        string snapshotBefore=File.ReadAllText(store.SnapshotPath(gold));
        File.WriteAllText(gold.StatsPath,"invalid source JSON");
        loaded=store.Load();Check(loaded.Games.Single(g=>g.AppId==2322010).Achievements.Count==4 && File.ReadAllText(store.SnapshotPath(gold))==snapshotBefore,"startup loads snapshots instead of rereading changed source JSON and never overwrites protected history");
        File.Delete(ini.StatsPath);loaded=store.Load();var missing=loaded.Games.Single(g=>g.HistoryId==ini.HistoryId);
        Check(missing.SourceMissing && missing.HasLocalHistory && missing.Achievements.Count==4,"missing original file remains visible with its protected history");
        string iniSnapshot=store.SnapshotPath(ini),iniSnapshotBytes=File.ReadAllText(iniSnapshot);File.WriteAllText(iniSnapshot,"broken");
        loaded=store.Load();var badSnapshot=loaded.Games.Single(g=>g.HistoryId==ini.HistoryId);
        Check(!badSnapshot.HasLocalHistory && !badSnapshot.HasSnapshot && !String.IsNullOrEmpty(badSnapshot.CatalogIssue) && File.ReadAllText(iniSnapshot)=="broken","corrupt snapshot keeps a marked library row but prevents execution without overwriting it");
        File.WriteAllText(iniSnapshot,iniSnapshotBytes);
        gold.RetainedFromCatalog=true;store.Save(all,root,scan,"Second summary");
        Check(File.Exists(store.BackupPath) && store.Load().Games.Single(g=>g.AppId==2322010).RetainedFromCatalog,"catalog keeps a previous copy and preserves entries not reimported on refresh");
        File.WriteAllText(store.FilePath,"broken catalog");loaded=store.Load();
        Check(loaded.UsedBackup && loaded.Games.Count==3 && File.ReadAllText(store.FilePath)=="broken catalog","corrupt main catalog falls back to previous copy without silently replacing it");
        store.Save(all,root,scan,"Repaired explicitly");
        Check(store.Load().Games.Count==3 && Directory.GetFiles(Path.GetDirectoryName(store.FilePath),"catalog.corrupt_*.json").Length==1,"explicit refresh preserves corrupt catalog bytes separately before saving a valid replacement");
        string valid=File.ReadAllText(store.FilePath);var tampered=JsonSerializer.Deserialize<LibraryCatalog>(valid);
        tampered.Entries.Add(new LibraryEntry{AppId=1,AccountId=0,GameName="Bad identity",StatsPath=Path.Combine(root,"1","achievements.json"),HistoryId="../../outside"});
        File.WriteAllText(store.FilePath,JsonSerializer.Serialize(tampered));loaded=store.Load();
        Check(loaded.Games.Count==3 && loaded.Warnings.Any(w=>w.Contains("identidad")),"invalid source identity is skipped without accessing snapshots outside the data directory");
        File.WriteAllText(store.FilePath,valid);tampered=JsonSerializer.Deserialize<LibraryCatalog>(valid);tampered.Entries.Add(tampered.Entries[0]);File.WriteAllText(store.FilePath,JsonSerializer.Serialize(tampered));loaded=store.Load();
        Check(loaded.Games.Count==3 && loaded.Warnings.Any(w=>w.Contains("duplicada")),"duplicate cached histories do not create duplicate library rows");
        File.WriteAllText(store.FilePath,valid);
        try{store.Save(new[]{new GameEntry{AppId=0,GameName="Bad",StatsPath=stats}},root,scan,"");throw new Exception("Expected invalid save rejection");}catch(InvalidDataException){Check(File.ReadAllText(store.FilePath)==valid,"invalid catalog save cannot replace the previous library");}
        gold.Achievements[0].DisplayName="Nombre agregado después de importar";gold.Achievements[0].Description="Descripción nueva";
        string frozen=File.ReadAllText(store.SnapshotPath(gold));
        store.Save(all,root,scan,"Labels");
        var named=store.Load().Games.Single(g=>g.StorageKey==gold.StorageKey);
        Check(named.Achievements[0].DisplayName==gold.Achievements[0].DisplayName && named.Achievements[0].Description==gold.Achievements[0].Description && File.ReadAllText(store.SnapshotPath(gold))==frozen,"new definition labels survive restart without modifying the original snapshot");
        Check(LibraryStore.ShouldRetainAfterScan(gold),"an existing source survives refresh when its reader cannot reimport it");
        Directory.Move(goldDir,goldDir+"-missing");
        Check(!LibraryStore.ShouldRetainAfterScan(gold),"deleted source folders are removed after a successful full refresh");
        store.Save(all.Where(g=>LibraryStore.ShouldRetainAfterScan(g)),root,scan,"Removed missing files");
        Check(store.Load().Games.All(g=>g.StorageKey!=gold.StorageKey) && File.ReadAllText(store.SnapshotPath(gold))==frozen,"removed entries stay absent after restart and their protected snapshots remain intact");
        Directory.Move(goldDir+"-missing",goldDir);
        Check(Directory.GetFiles(Path.GetDirectoryName(store.FilePath),"*.tmp").Length==0,"atomic library saves leave no temporary index files");
        var current=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){{"known","Bloqueado"}};
        SamRunner.ValidateSelectedIds(restoredSteam.Achievements,current);Check(true,"fresh Steam verification accepts known cached API IDs case-insensitively");
        try{SamRunner.ValidateSelectedIds(restoredSteam.Achievements,new Dictionary<string,string>{{"OTHER","Bloqueado"}});throw new Exception("Expected unknown API rejection");}catch(InvalidDataException){Check(true,"cached achievements absent from current Steam schema cannot be executed");}
        restoredSteam.Achievements[0].Included=false;SamRunner.ValidateSelectedIds(restoredSteam.Achievements,new Dictionary<string,string>{{"OTHER","Bloqueado"}});Check(true,"deselected historical API IDs do not prevent executing a valid remaining selection");
        var savedPlans=new PlanStore(Path.Combine(root,"plans"));savedPlans.Save(gold,PlanStore.Capture(gold,gold.Achievements,new PlanParameters()));
        var savedOrigins=new OriginStore(Path.Combine(root,"origins"));savedOrigins.Save(gold,"Otro");
        string planBytes=File.ReadAllText(savedPlans.FilePath(gold)),originBytes=File.ReadAllText(savedOrigins.FilePath(gold));
        store.Clear();
        Check(File.ReadAllText(savedPlans.FilePath(gold))==planBytes && File.ReadAllText(savedOrigins.FilePath(gold))==originBytes,"clearing the catalog leaves saved plans and manual origin files unchanged");
        Check(!File.Exists(store.FilePath) && !File.Exists(store.BackupPath) && store.Load().Catalog==null,"clear library removes both indexes so backup cannot restore the cleared list");
        Check(File.Exists(stats) && File.Exists(steamSnapshot) && File.ReadAllText(store.SnapshotPath(gold))==frozen,"clear library preserves original achievement files and protected snapshots");
        store.Clear();Check(store.Load().Games.Count==0,"clearing an already empty library is safe");
    }
}
