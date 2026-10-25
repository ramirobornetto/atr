using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AchievementTimelineReplay;

static class SavedPlanChecks
{
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS: "+message);}
    static void Reject(Action action,string message)
    {
        try {action();}catch(InvalidDataException){Check(true,message);return;}
        throw new Exception(message);
    }
    public static void Run(string root)
    {
        var game=new GameEntry{AppId=42,AccountId=7,GameName="Fixture"};
        game.Achievements.Add(new AchievementRecord{ApiId="A",OriginalUnix=100});
        game.Achievements.Add(new AchievementRecord{ApiId="B",OriginalUnix=200});
        var records=game.Achievements.Select(a=>a.Clone()).Reverse().ToList();
        records[1].Included=false;records[1].ManualGapSeconds=45;
        var options=new PlanParameters{CustomOrder=true,DurationSeconds=150,InitialDelaySeconds=9,Repair=true,Idle=false,LimitLongGaps=true,MaximumGapMinutes=4};
        var store=new PlanStore(Path.Combine(root,"Plans"));
        store.Save(game,PlanStore.Capture(game,records,options));
        var loaded=new PlanStore(Path.Combine(root,"Plans")).Load(game);
        var restored=PlanStore.Restore(game,loaded);
        Check(restored.Records[0].ApiId=="B" && !restored.Records[1].Included && restored.Records[1].ManualGapSeconds==45,"Saved plan survives reload with custom order, selection and manual waits");
        var p=loaded.Parameters;
        Check(p.CustomOrder && p.DurationSeconds==150 && p.InitialDelaySeconds==9 && p.Repair && !p.Idle && p.LimitLongGaps && p.MaximumGapMinutes==4,"All per-game parameters survive reload");
        Check(restored.Records[1].OriginalUnix==100 && game.Achievements[0].Included && game.Achievements[0].ManualGapSeconds==null,"Restoring a saved plan leaves imported history unchanged");
        var other=new GameEntry{AppId=42,AccountId=8};
        Check(!store.Exists(other),"Plans are isolated by account as well as game");
        Reject(()=>PlanStore.Restore(other,loaded),"Plan for another account is rejected");
        var changed=new GameEntry{AppId=42,AccountId=7};
        changed.Achievements.Add(game.Achievements[0]);
        changed.Achievements.Add(new AchievementRecord{ApiId="C",OriginalUnix=300});
        var updated=PlanStore.Restore(changed,loaded);
        Check(updated.Missing==1 && updated.Added==1 && updated.Records.Last().ApiId=="C" && !updated.Records.Last().Included,"Changed histories report removed achievements and append new achievements unchecked");
        string before=File.ReadAllText(store.FilePath(game));
        loaded.Achievements.Add(loaded.Achievements[0]);
        Reject(()=>store.Save(game,loaded),"Duplicate achievement IDs cannot overwrite a saved plan");
        Check(File.ReadAllText(store.FilePath(game))==before,"Invalid save preserves the previous file");
        loaded=store.Load(game);loaded.Parameters.MaximumGapMinutes=-1;
        Reject(()=>store.Save(game,loaded),"Invalid timing parameters are rejected");
        File.WriteAllText(store.FilePath(game),"{\"Version\":999,\"AccountId\":7,\"AppId\":42}");
        Reject(()=>store.Load(game),"Unsupported saved format is rejected");
        Check(Directory.GetFiles(Path.Combine(root,"Plans"),"*.tmp").Length==0,"Atomic saves leave no temporary files");
        var origins=new OriginStore(Path.Combine(root,"Origins"));
        Check(origins.Load(game)==null,"Unclassified origin does not imply an official Steam license");
        origins.Save(game,"  Steamidra  ");
        Check(new OriginStore(Path.Combine(root,"Origins")).Load(game)=="SteamIdra/LuaTools","Manual origin survives a new store instance and trims whitespace");
        Check(origins.Load(other)==null,"Manual origin is isolated by account");
        Check(File.ReadAllText(origins.FilePath(game)).Contains("manual"),"Origin metadata explicitly identifies a manual assignment");
        var originBytes=File.ReadAllText(origins.FilePath(game));
        Reject(()=>origins.Save(game,new string('x',121)),"Oversized origin labels are rejected");
        Check(File.ReadAllText(origins.FilePath(game))==originBytes,"Invalid origin edit preserves the saved label");
        origins.Save(game,"Steam oficial");
        Check(origins.Load(game)=="Steam" && !File.ReadAllText(origins.FilePath(game)).Contains("Steam oficial"),"Retired Steam oficial label is normalized to Steam on save");
        File.WriteAllText(origins.FilePath(game),System.Text.Json.JsonSerializer.Serialize(new OriginAssignment{AccountId=game.AccountId,AppId=game.AppId,HistoryId=game.HistoryId,Label="Steam oficial"}));
        Check(origins.Load(game)=="Steam","Legacy Steam oficial assignments display as Steam without losing their association");
        File.WriteAllText(origins.FilePath(game),System.Text.Json.JsonSerializer.Serialize(new OriginAssignment{AccountId=game.AccountId,AppId=game.AppId,HistoryId=game.HistoryId,Label="Steamidra"}));
        Check(origins.Load(game)=="SteamIdra/LuaTools","Legacy Steamidra manual assignments display the new combined category");
        File.WriteAllText(origins.FilePath(game),System.Text.Json.JsonSerializer.Serialize(new OriginAssignment{AccountId=game.AccountId,AppId=game.AppId,HistoryId=game.HistoryId,Label="Steamidra (indicio LUA)"}));
        Check(origins.Load(game)=="SteamIdra/LuaTools","Legacy Lua hints display the new combined category");
        File.WriteAllText(origins.FilePath(game),System.Text.Json.JsonSerializer.Serialize(new OriginAssignment{AccountId=game.AccountId,AppId=game.AppId,HistoryId=game.HistoryId,Label="SteamIdra/LuaTools (indicio LUA)"}));
        Check(origins.Load(game)=="SteamIdra/LuaTools","Previously saved Lua hint category merges into the single SteamIdra/LuaTools category");
        origins.Save(game,null);
        Check(origins.Load(game)==null,"Clearing an origin restores unclassified state");
        Check(File.ReadAllText(store.FilePath(game)).Contains("999"),"Origin edits do not change saved plan files");
        ReplayQueryCheck(root);
    }
    static void ReplayQueryCheck(string root)
    {
        string dir=Path.Combine(root,"fake-sam");Directory.CreateDirectory(dir);
        string cli=Path.Combine(dir,"sam.sh");
        File.WriteAllText(cli,"""
#!/bin/sh
cd -- "$(dirname -- "$0")" || exit 1
if ! mkdir command.lock 2>/dev/null; then echo overlap >> overlap; exit 1; fi
trap 'rmdir command.lock' EXIT
printf '%s %s\n' "$1" "$2" >> commands
case "$1" in
  list-achievements) sleep 1; printf '[{"id":"X","is_achieved":false}]';;
  unlock) printf '{"success":true}';;
  *) exit 1;;
esac
""");
        File.SetUnixFileMode(cli,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
        using var firstDone=new ManualResetEventSlim();
        var replayer=new LegacyReplayer(new ReplayEvents{Log=_=>{},Status=_=>{},Progress=(n,_,_)=>{if(n==1)firstDone.Set();}});
        var plan=new List<ScheduleItem>
        {
            new ScheduleItem{Achievement=new AchievementRecord{ApiId="A",DisplayName="A"},OffsetSeconds=0},
            new ScheduleItem{Achievement=new AchievementRecord{ApiId="B",DisplayName="B"},OffsetSeconds=0.5}
        };
        var run=replayer.Run(plan,cli,SamCliMode.Modern,42,"Fixture",false,false,0,CancellationToken.None);
        Check(firstDone.Wait(TimeSpan.FromSeconds(10)),"Replay starts with the fake SAM engine");
        try {replayer.QueryOtherGame(cli,42);throw new Exception("Active game query was not rejected");}
        catch(InvalidOperationException){Check(true,"Active game query is rejected during replay");}
        var query=replayer.QueryOtherGame(cli,99);
        run.GetAwaiter().GetResult();
        Check(!SamRunner.Failed(query) && !File.Exists(Path.Combine(dir,"overlap")),"Other-game query and unlock commands execute without overlap");
        var commands=File.ReadAllLines(Path.Combine(dir,"commands"));
        Check(commands.SequenceEqual(new[]{"unlock 42","list-achievements 99","unlock 42"}),"Consulting another game keeps both unlocks targeted at the original game");
        using var waiting=new ManualResetEventSlim();
        using var cancellation=new CancellationTokenSource();
        var cancelledReplayer=new LegacyReplayer(new ReplayEvents{Log=_=>{},Status=_=>waiting.Set()});
        var waitingRun=cancelledReplayer.Run(new List<ScheduleItem>{new ScheduleItem{Achievement=new AchievementRecord{ApiId="C"},OffsetSeconds=60}},cli,SamCliMode.Modern,42,"Fixture",false,false,0,cancellation.Token);
        Check(waiting.Wait(TimeSpan.FromSeconds(10)),"Coordinated replay reaches its cancellable wait");
        cancellation.Cancel();
        try {waitingRun.GetAwaiter().GetResult();throw new Exception("Expected replay cancellation");}
        catch(OperationCanceledException){Check(File.ReadAllLines(Path.Combine(dir,"commands")).Length==3,"Cancelling a coordinated replay issues no further unlocks");}
        File.WriteAllText(cli,"#!/bin/sh\nprintf '{\"success\" : false,\"error\":\"License rejected\"}'\n");
        int progress=0;var errors=new List<string>();
        var denied=new LegacyReplayer(new ReplayEvents{Log=errors.Add,Progress=(n,_,_)=>progress=n});
        try{denied.Run(new List<ScheduleItem>{new ScheduleItem{Achievement=new AchievementRecord{ApiId="DENIED"}}},cli,SamCliMode.Modern,42,"Rejected",false,false,0,CancellationToken.None).GetAwaiter().GetResult();throw new Exception("Expected rejection");}
        catch(InvalidOperationException){Check(progress==0 && !errors.Any(line=>line.Contains("Migración completada")),"SAM refusal stops replay without advancing progress or announcing completion");}


    }
}
