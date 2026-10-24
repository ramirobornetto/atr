using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using AchievementTimelineReplay;

static class GroupingMappingChecks
{
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);Console.WriteLine("PASS: Grouping/mapping — "+message);}
    public static void Run(string root)
    {
        var choices=new Dictionary<string,string>();
        var direct=new GameEntry{AppId=42,AccountId=7,StatsPath=Path.Combine(root,"UserGameStats_7_42.bin"),HasLocalHistory=true,HasSnapshot=true,GameName="Example",SourceLabel="Steam directo",Achievements=new List<AchievementRecord>{new AchievementRecord{ApiId="A",OriginalUnix=1704067200}}};
        GameEntry PA(string provider,string guid)=>new GameEntry{AppId=42,HistoryId="pa_"+guid,PAGroupKey=guid,PAProvider=provider,PAAccountId=7,HasLocalHistory=true,HasSnapshot=true,StatsPath=Path.Combine(root,"achievement_cache.db"),GameName="Example",SourceLabel="PA / "+provider,Achievements=new List<AchievementRecord>{new AchievementRecord{ApiId="A",OriginalUnix=1704067200}}};
        var steam=PA("Steam","1");var local=PA("Local","2");
        var group=HistoryGroups.Build(new[]{direct,steam,local},choices).Single();
        Check(group.Sources.Count==3 && ReferenceEquals(group.Selected,direct),"direct Steam and PA Steam/Local with different Playnite game IDs produce one row and retain three selectable sources");
        local.Achievements.Add(new AchievementRecord{ApiId="B",OriginalUnix=0});
        group=HistoryGroups.Build(new[]{direct,steam,local},choices).Single();
        Check(ReferenceEquals(group.Selected,local) && group.Sources.Count==3,"automatic source selection prioritizes more unlocks including undated ones without deleting alternatives");
        choices[direct.StorageKey]=steam.StorageKey;
        group=HistoryGroups.Build(new[]{direct,steam,local},choices).Single();
        Check(group.Manual && ReferenceEquals(group.Selected,steam),"manual source selection wins over automatic ranking and is visible in the summary");
        string choicePath=Path.Combine(root,"group-choices.json");HistoryGroups.SaveChoices(choicePath,choices);choices=HistoryGroups.LoadChoices(choicePath);
        Check(HistoryGroups.Build(new[]{direct,steam,local},choices).Single().Manual,"manual choice survives restart");
        var other=new GameEntry{AppId=42,AccountId=8,HasLocalHistory=true,HasSnapshot=true,Achievements=direct.Achievements};
        Check(HistoryGroups.Build(new[]{direct,steam,local,other},new Dictionary<string,string>()).Count==2,"known different Steam accounts cannot merge through an unknown bridge source");
        var all=PlayniteHistory.Load(Path.Combine(root,"pa","achievement_cache.db"),_=>{},default,true);
        Check(all.Count(g=>g.AppId==100)==2,"PA reader retains Steam and Manual alternatives instead of discarding the smaller source");
        Check(new[]{"Xbox","Epic","GOG","PlayStation"}.All(platform=>all.Any(g=>g.Platform==platform && !g.SteamTarget)),"common PA platforms import both numeric and opaque IDs without treating them as Steam AppIDs");
        var foreign=all.Single(g=>g.Platform=="Xbox");
        var sources=new SourceSelection();sources.Scan["PA"]=false;
        Check(!sources.Allows(foreign,root) && !sources.Allows(all.First(g=>PlayniteHistory.IsPA(g) && g.SteamTarget),root) && !SourceSelection.ScanSources.Contains("Xbox"),"a single PA scan switch controls Steam and external PA platforms");
        sources.Visible["Xbox"]=false;
        Check(!sources.Shows("Xbox") && sources.Shows("Epic") && SourceSelection.LibrarySources.Contains("PlayStation"),"platform visibility filters remain independent from the one PA scan switch");
        Check(!foreign.SteamTarget && foreign.AppId==0 && foreign.ExternalGameId=="2089483628" && foreign.Achievements.Count==2,"Xbox title ID remains separate from Steam AppID and its unlocks remain importable");
        var definitions=new List<SteamDefinition>{new SteamDefinition{ApiId="STEAM_A",Name="First step",Description="Finish tutorial"},new SteamDefinition{ApiId="STEAM_B",Name="Finish",Description="Finish game"}};
        foreign.Achievements[0].DisplayName="First step";foreign.Achievements[0].Description="Finish tutorial";
        Check(PlatformMapping.Suggest(foreign.Achievements[0],definitions)?.ApiId=="STEAM_A","unique display-name match is a suggestion without an automatic confirmation");
        var file=PlatformMapping.Capture(foreign,1182900,new[]{new AchievementMapping{SourceId=foreign.Achievements[0].ApiId,TargetId="STEAM_A",Confirmed=true},new AchievementMapping{SourceId=foreign.Achievements[1].ApiId,TargetId="STEAM_B",Confirmed=false}});
        var mapped=PlatformMapping.Build(foreign,file,definitions,"Steam target");
        Check(mapped.SteamTarget && mapped.AppId==1182900 && mapped.Achievements.Count==1 && mapped.Achievements[0].ApiId=="STEAM_A" && mapped.Achievements[0].OriginalUnix==foreign.Achievements[0].OriginalUnix,"only confirmed mappings produce Steam IDs and preserve source dates including unknown dates");
        string directory=Path.Combine(root,"mapping-store");PlatformMapping.Save(directory,foreign,file);
        Check(PlatformMapping.Load(directory,foreign,1182900).Entries.Count==2,"confirmed and pending correspondences are stored independently for source and destination");
        file.Entries[1].Confirmed=true;file.Entries[1].TargetId="STEAM_A";
        try{PlatformMapping.Build(foreign,file,definitions,"Steam");throw new Exception("Expected duplicate mapping rejection");}catch(InvalidDataException){Check(true,"two source achievements cannot unlock one target twice");}
        file.Entries[1].TargetId="REMOVED";
        try{PlatformMapping.Build(foreign,file,definitions,"Steam");throw new Exception("Expected stale target rejection");}catch(InvalidDataException){Check(true,"confirmed target absent from the loaded Steam definition is rejected");}
        file.Entries[1].TargetId="STEAM_B";mapped=PlatformMapping.Build(foreign,file,definitions,"Steam target");
        Check(mapped.Achievements.Count==2 && mapped.Achievements.Any(a=>a.OriginalUnix==0),"confirmed mappings retain undated foreign unlocks in the prepared Steam plan");
        string snapshots=Path.Combine(root,"mapping-snapshots");PlayniteHistory.ProtectCurrent(foreign,snapshots);PlayniteHistory.ProtectCurrent(mapped,snapshots);
        var catalog=new LibraryStore(Path.Combine(root,"mapping-library"),snapshots);catalog.Save(new[]{foreign,mapped},"",null,"");var loaded=catalog.Load();
        Check(loaded.Games.Count==2 && loaded.Games.Any(g=>g.Platform=="Xbox" && !g.SteamTarget) && loaded.Games.Any(g=>PlatformMapping.IsMapped(g) && g.SteamTarget),"foreign origins and mapped Steam plans round-trip through catalog without confusing platform identifiers");
        Check(SamRunner.Failed(new ProcessResult{ExitCode=0,StdOut="{\"success\" : false}"}) && SamRunner.Failed(new ProcessResult{ExitCode=0,StdOut="[{\"id\":\"A\",\"success\":false}]"}) && SamRunner.Failed(new ProcessResult{ExitCode=0,StdOut="{\"error\":\"Access denied\"}"}),"SAM rejects are detected even with exit code zero, spaced JSON or per-achievement failures");
        Check(!SamRunner.Failed(new ProcessResult{ExitCode=0,StdOut="[{\"id\":\"A\",\"is_achieved\":false}]"}) && !SamRunner.Failed(new ProcessResult{ExitCode=0,StdOut="{\"success\":true,\"error\":null}"}),"locked achievement state and null error are not mistaken for failed operations");
    }
}
