using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using AchievementTimelineReplay;

static class PlayniteChecks
{
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);Console.WriteLine("PASS: PA — "+message);}
    public static void Run(string root)
    {
        string folder=Path.Combine(root,"pa"),path=Path.Combine(folder,"achievement_cache.db");
        string before=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))),walBefore=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path+"-wal")));
        var logs=new List<string>();var loaded=PlayniteHistory.Load(path,logs.Add);
        Check(before==Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))) && walBefore==Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path+"-wal"))),"source database and committed WAL bytes remain unchanged by import");
        var manual=loaded.Single(g=>g.AppId==100);var local=loaded.Single(g=>g.AppId==200);
        Check(manual.PAProvider=="Manual" && manual.Achievements.Count==4 && manual.TotalAchievements==5,"more unlocks wins even when the smaller Steam candidate has more complete timestamps");
        Check(manual.Achievements.Count(a=>a.OriginalUnix==0)==3 && manual.Achievements.Single(a=>a.ApiId=="A").DisplayName=="Title A","empty and invalid dates retain unlocked achievements and join names from definitions");
        Check(manual.Achievements.All(a=>a.ApiId!="D") && manual.Achievements.Any(a=>a.ApiId=="E"),"manual explicit false overrides cached unlocks and manual IDs absent from definitions are retained");
        Check(local.Achievements.Count==2 && local.Achievements.All(a=>a.OriginalUnix==0),"Local provider imports with no dates even when its internal user is not flagged as current");
        Check(local.Achievements.Single(a=>a.ApiId=="L1").DisplayName=="Edited L1" && local.Achievements.Single(a=>a.ApiId=="L1").OriginalUnix==0,"PA display overrides and explicit cleared dates are respected without dropping unlocks");
        Check(loaded.All(g=>g.AppId!=300 && g.AppId!=500),"friends and games with no unlocked achievements do not become replay sources");
        Check(loaded.Count(g=>g.AppId==600)==2 && loaded.Where(g=>g.AppId==600).Select(g=>g.StorageKey).Distinct().Count()==2,"ambiguous multiple current profiles remain separate rather than selecting another account's progress");
        Check(File.Exists(path+"-wal") && loaded.Count==8,"read-only SQLite reader sees committed WAL data without requiring a checkpoint");
        var plans=LegacyPlanner.Calculate(local.Achievements,LegacyPlanner.OriginalSpan(local.Achievements));
        Check(plans.Values.Max(x=>x.OffsetSeconds)==60 && local.Achievements.All(a=>a.OriginalUnix==0),"undated plans use editable suggested intervals without fabricating original dates or epoch-length waits");
        local.Achievements[1].ManualGapSeconds=17;plans=LegacyPlanner.Calculate(local.Achievements,60);
        Check(plans[local.Achievements[1]].GapSeconds==17,"explicit manual gaps retain priority for undated achievements");
        var tie1=new GameEntry{PAProvider="Steam",Achievements=new List<AchievementRecord>{new AchievementRecord{ApiId="A",OriginalUnix=1}}};
        var tie2=new GameEntry{PAProvider="Manual",Achievements=new List<AchievementRecord>{new AchievementRecord{ApiId="A",OriginalUnix=0}}};
        Check(ReferenceEquals(PlayniteHistory.Choose(new[]{tie1,tie2}),tie1),"valid dates break equal unlock counts, never outrank a larger count");
        tie2.Achievements[0].OriginalUnix=1;
        Check(ReferenceEquals(PlayniteHistory.Choose(new[]{tie1,tie2}),tie2),"manual wins an otherwise exact Steam/manual tie");
        var upstream=PlayniteHistory.Load(Path.Combine(root,"pa-upstream","achievement_cache.db"),logs.Add).Single(g=>g.AppId==100);
        Check(upstream.PAProvider==manual.PAProvider && upstream.Achievements.Select(a=>a.ApiId).SequenceEqual(manual.Achievements.Select(a=>a.ApiId)),"same reader accepts upstream schema 8 and Santodan schema 9 manual payloads");
        string snapshots=Path.Combine(root,"pa-snapshots");PlayniteHistory.ProtectCurrent(manual,snapshots);
        string original=File.ReadAllText(Path.Combine(snapshots,manual.StorageKey,"snapshot.json"));
        var store=new LibraryStore(Path.Combine(root,"pa-library"),snapshots);store.Save(new[]{manual},"",null,"");
        var restored=store.Load().Games.Single();
        Check(restored.HasSnapshot && restored.Achievements.Count==4 && restored.Achievements.Count(a=>a.OriginalUnix==0)==3 && restored.PAProvider=="Manual","catalog and snapshots preserve undated PA unlocks across restart");
        manual.Achievements.Add(new AchievementRecord{ApiId="NEW",DisplayName="New",OriginalUnix=0});PlayniteHistory.ProtectCurrent(manual,snapshots);store.Save(new[]{manual},"",null,"");
        Check(store.Load().Games.Single().Achievements.Count==5 && File.ReadAllText(Path.Combine(snapshots,manual.StorageKey,"snapshot.json"))==original,"PA refresh updates current history while preserving the first original snapshot");
        var gold=new GameEntry{AppId=100,AccountId=0,HistoryId=GoldbergHistory.Identity(Path.Combine(root,"Goldberg SteamEmu Saves","100","achievements.json")),StatsPath=Path.Combine(root,"Goldberg SteamEmu Saves","100","achievements.json"),DetectedOrigin="Goldberg",HasLocalHistory=true,GameName="Same history",SourceLabel="Goldberg JSON"};
        gold.Achievements.Add(manual.Achievements.First(a=>a.OriginalUnix>0).Clone());
        var planStore=new PlanStore(Path.Combine(root,"pa-plans"));gold.Achievements[0].ManualGapSeconds=31;
        planStore.Save(gold,PlanStore.Capture(gold,gold.Achievements,new PlanParameters()));string saved=File.ReadAllText(planStore.FilePath(gold));
        var unified=PlayniteHistory.Deduplicate(new List<GameEntry>{gold,manual},logs.Add);
        Check(unified.Count==1 && ReferenceEquals(unified[0],manual) && manual.PAOrigin=="Goldberg","same AppID plus matching historical evidence deduplicates to the source with more unlocks");
        var recovered=PlanStore.Restore(manual,planStore.Load(manual));
        Check(recovered.Records.Single(a=>a.ApiId==gold.Achievements[0].ApiId).ManualGapSeconds==31 && recovered.Added==4 && recovered.Records.Count(a=>a.Included)==1 && File.ReadAllText(planStore.FilePath(gold))==saved,"source selection can recover the previous source's saved plan and leaves new achievements unchecked");
        var conflicting=new GameEntry{AppId=100,HasLocalHistory=true,HistoryId="goldberg_other",Achievements=new List<AchievementRecord>{new AchievementRecord{ApiId=gold.Achievements[0].ApiId,OriginalUnix=gold.Achievements[0].OriginalUnix+1}}};
        Check(!PlayniteHistory.SameHistory(conflicting,manual) && PlayniteHistory.Deduplicate(new List<GameEntry>{conflicting,manual},logs.Add).Count==2,"conflicting dated profiles stay separate");
        Check(PlayniteHistory.Deduplicate(new List<GameEntry>{gold,local},logs.Add).Count==2,"undated data is never declared the same profile just from an AppID");
        var official=new GameEntry{AppId=100,AccountId=2,HasLocalHistory=true,Achievements=gold.Achievements};manual.PAAccountId=3;
        Check(!PlayniteHistory.SameHistory(official,manual),"known different Steam accounts cannot deduplicate even when dated evidence matches");
        string nativeFolder=Path.Combine(root,"pa-native","700");Directory.CreateDirectory(nativeFolder);
        string nativePath=Path.Combine(nativeFolder,"achievements.json");
        File.WriteAllText(nativePath,"{\"N1\":{\"earned\":true,\"earned_time\":1704067200}}");
        var firstNative=GoldbergHistory.Load(nativePath,700);HistorySnapshots.Protect(firstNative,snapshots);
        string firstNativeBytes=File.ReadAllText(Path.Combine(snapshots,firstNative.StorageKey,"snapshot.json"));
        File.WriteAllText(nativePath,"{\"N1\":{\"earned\":true,\"earned_time\":1704067200},\"N2\":{\"earned\":true,\"earned_time\":1704067260}}");
        var latestNative=HistorySnapshots.ProtectLatest(GoldbergHistory.Load(nativePath,700),snapshots);
        store.Save(new[]{latestNative},"",null,"");
        Check(store.Load().Games.Single().Achievements.Count==2 && File.ReadAllText(Path.Combine(snapshots,firstNative.StorageKey,"snapshot.json"))==firstNativeBytes,"current direct source progress can participate in PA comparison without overwriting its first snapshot");
        File.WriteAllText(nativePath,"{\"N1\":{\"earned\":true,\"earned_time\":1704067200},\"N2\":{\"earned\":true,\"earned_time\":1704067260},\"N3\":{\"earned\":true,\"earned_time\":1704067320}}");
        var nextNative=HistorySnapshots.Protect(GoldbergHistory.Load(nativePath,700),snapshots);
        Check(nextNative.UsesCurrentSnapshot && nextNative.Achievements.Count==3 && File.ReadAllText(Path.Combine(snapshots,firstNative.StorageKey,"snapshot.json"))==firstNativeBytes,"later native scans keep updating the separate current view while original protection remains intact");
        var preferences=new SourceSelection();preferences.PAPaths.Add(path);string preferencePath=Path.Combine(root,"pa-options.json");preferences.Save(preferencePath);
        Check(SourceSelection.Load(preferencePath).PAPaths.Single()==path,"portable PA database locations are remembered independently of library entries");
        local.IsDemo=true;PlayniteHistory.ProtectCurrent(local,snapshots);local.IsDemo=false;PlayniteHistory.ProtectCurrent(local,snapshots);
        Check(local.IsDemo,"PA refresh cannot turn previously imported demo data into an executable real history");
        Check(PlayniteHistory.Timestamp("bad")==0 && PlayniteHistory.Timestamp(null)==0 && PlayniteHistory.Timestamp("2024-01-01T00:00:00Z")==1704067200,"date parsing explicitly marks invalid dates as unknown");
    }
}
