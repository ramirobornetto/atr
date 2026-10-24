using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using AchievementTimelineReplay;

static class MigrationChecks
{
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);Console.WriteLine("PASS: Migration — "+message);}
    public static void Run(string root)
    {
        var game=new GameEntry{AppId=42,AccountId=7,GameName="Fixture",StatsPath=Path.Combine(root,"UserGameStats_7_42.bin")};
        var records=new List<AchievementRecord>{new AchievementRecord{ApiId="A",DisplayName="First",OriginalUnix=100},new AchievementRecord{ApiId="B",DisplayName="Second",OriginalUnix=110},new AchievementRecord{ApiId="C",DisplayName="Third",OriginalUnix=120}};game.Achievements=records;
        var parameters=new PlanParameters{DurationSeconds=20,InitialDelaySeconds=0,Idle=false};
        var states=new Dictionary<string,string>{{"A","Desbloqueado"},{"B","Bloqueado"},{"C","Bloqueado"}};
        var preview=MigrationPreview.Build(records,parameters,states);
        Check(preview.Selected==3 && preview.Already==1 && preview.Schedule.Count==2 && preview.Missing==0,"preflight separates selected, already-unlocked and pending achievements from a fresh Steam response");
        Check(preview.Schedule[records[1]].OffsetSeconds==0 && preview.Schedule[records[2]].OffsetSeconds==10 && records.All(r=>r.Included),"resuming rebases the first pending achievement and preserves remaining intervals without changing the draft selection");
        var changed=new Dictionary<string,string>(states){{"OTHER","Bloqueado"}};changed["B"]="Desbloqueado";
        Check(MigrationPreview.Build(records,parameters,changed).PendingIdentity!=preview.PendingIdentity,"state changes during review require a new confirmation");
        states.Remove("C");preview=MigrationPreview.Build(records,parameters,states);
        Check(preview.Missing==1 && preview.Items.Single(i=>i.ApiId=="C").Status=="No encontrado","unknown target codes remain visibly blocked instead of becoming unlock commands");
        bool emptyRejected=false;try{MigrationPreview.Build(records,parameters,new Dictionary<string,string>());}catch(InvalidDataException){emptyRejected=true;}Check(emptyRejected,"empty Steam state responses cannot produce a supposedly verified preview");
        states["C"]="Desbloqueado";states["B"]="Desbloqueado";
        preview=MigrationPreview.Build(records,parameters,states);Check(preview.Schedule.Count==0 && preview.Already==3,"a fully migrated plan produces no additional unlock operations");
        parameters.Repair=true;preview=MigrationPreview.Build(records,parameters,states);Check(preview.Schedule.Count==3 && preview.Already==0 && preview.Items.All(i=>i.Detail.Contains("reparación")),"explicit repair mode retains selected unlocks and exposes its destructive behavior in the preview");parameters.Repair=false;
        preview=MigrationPreview.Build(records,parameters,null);Check(!preview.Verified && preview.Schedule.Count==3 && preview.Items.All(i=>i.Detail.Contains("legacy")),"legacy CLI states are labeled unverified rather than advertised as checked");
        var strict=SamRunner.States("[{\"id\":\"NO_STATE\"},{\"id\":\"MALFORMED\",\"is_achieved\":\"false\"},{\"id\":\"VALID\",\"is_achieved\":false}]");Check(strict.Count==1 && strict.ContainsKey("VALID"),"missing or malformed state fields cannot silently be treated as locked achievements");
        states["B"]="Bloqueado";states["C"]="Bloqueado";preview=MigrationPreview.Build(records,parameters,states);
        var run=new MigrationRun{SourceKey=game.StorageKey,SourcePath=game.StatsPath,SourceLabel="Goldberg",SteamAccount=7,Outcome="En curso",Plan=PlanStore.Capture(game,records,parameters),Items=preview.Items};
        var store=new MigrationRunStore(Path.Combine(root,"Runs"));store.Save(game,run);run.Action("B","En curso");store.Save(game,run);
        var recovered=store.Load(game);Check(recovered.Plan.Achievements.Count==3 && recovered.Plan.Parameters.DurationSeconds==20 && recovered.SteamAccount==7 && recovered.Outcome=="Interrumpida","an interrupted run recovers the bound source, Steam account, order and timing without trusting its command journal as Steam state");
        Check(File.Exists(Path.Combine(Path.GetDirectoryName(store.LatestPath(game)),run.Id+".json")),"each run retains its own report alongside the recovery pointer");
        run.Action("B","Aplicado sin verificar");run.Reconcile(new Dictionary<string,string>{{"A","Desbloqueado"},{"B","Desbloqueado"},{"C","Bloqueado"}});
        Check(run.Items.Single(i=>i.ApiId=="B").Status=="Aplicado" && run.Items.Single(i=>i.ApiId=="C").Status=="Pendiente","final Steam verification confirms applied writes and keeps unattempted achievements pending");
        Check(run.Report().Contains("[B]") && run.Report().Contains("AppID 42") && run.Report().Contains("Goldberg"),"the report includes game, destination, source and named achievement codes");
        run.Action("C","Aplicado sin verificar");run.Reconcile(new Dictionary<string,string>{{"C","Bloqueado"}});Check(run.Items.Single(i=>i.ApiId=="C").Status=="Fallido","SAM accepting a write is insufficient when final Steam verification still reports locked");
        var other=new GameEntry{AppId=42,AccountId=8,GameName="Other",Achievements=records};bool wrongSource=false;try{store.Save(other,run);}catch(InvalidDataException){wrongSource=true;}Check(wrongSource,"run persistence rejects a different account/history even for the same AppID");
        var frames=new List<ScanStatus>();var phases=new ScanPhases(new[]{"Steam","PA","Guardar"},new ImmediateProgress<ScanStatus>(frames.Add));
        phases.Begin("Steam");phases.Report(new ScanStatus{Current=10,Total=10,Message="Steam done"});
        Check(frames.Last().Current<1000 && frames.Last().Message.Contains("1/3"),"finishing Steam cannot fill the global bar while other phases remain");
        phases.Report(new ScanStatus{Current=1,Total=10});phases.Begin("PA");phases.Report(new ScanStatus{Message="Reading PA",Indeterminate=true});phases.Begin("Guardar");
        Check(frames.Zip(frames.Skip(1),(a,b)=>a.Current<=b.Current).All(b=>b) && frames.All(f=>f.Current<1000),"phase progress remains monotonic and below 100 percent until persistence completes");phases.Complete();Check(frames.Last().Current==1000,"global progress reaches 100 percent only after explicit completion");
        if(!OperatingSystem.IsWindows())EngineCheck(root,game,records,parameters);
    }
    static void EngineCheck(string root,GameEntry game,List<AchievementRecord> records,PlanParameters parameters)
    {
        string dir=Path.Combine(root,"migration-fake-sam");Directory.CreateDirectory(dir);string cli=Path.Combine(dir,"sam.sh");
        File.WriteAllText(cli,"""
#!/bin/sh
printf '%s\n' "$*" >> commands
case "$*" in
  *B*) printf '{"success":false,"error":"simulated rejection"}';;
  *) printf '{"success":true}';;
esac
""");
        File.SetUnixFileMode(cli,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
        var run=new MigrationRun{SourceKey=game.StorageKey,Plan=PlanStore.Capture(game,records,parameters),Items=records.Select(a=>new MigrationItem{ApiId=a.ApiId,Name=a.DisplayName}).ToList(),Outcome="En curso"};
        var store=new MigrationRunStore(Path.Combine(dir,"Reports"));store.Save(game,run);
        var replayer=new LegacyReplayer(new ReplayEvents{Achievement=(a,state)=>{run.Action(a.ApiId,state);store.Save(game,run);}});
        bool rejected=false;try{replayer.Run(records.Select(a=>new ScheduleItem{Achievement=a}).ToList(),cli,SamCliMode.Modern,42,"Fixture",false,false,0,CancellationToken.None).GetAwaiter().GetResult();}catch(InvalidOperationException){rejected=true;}
        var recovered=store.Load(game);
        Check(rejected && recovered.Items[0].Status=="Aplicado sin verificar" && recovered.Items[1].Status=="Fallido" && recovered.Items[2].Status=="Pendiente" && File.ReadAllLines(Path.Combine(dir,"commands")).Length==2,"real replay orchestration journals partial success and rejection without executing later achievements (fake SAM only)");
        run.Reconcile(new Dictionary<string,string>{{"A","Desbloqueado"},{"B","Bloqueado"},{"C","Bloqueado"}});var preview=MigrationPreview.Build(records,parameters,new Dictionary<string,string>{{"A","Desbloqueado"},{"B","Bloqueado"},{"C","Bloqueado"}});
        Check(preview.Schedule.Count==2 && preview.Schedule.Keys.All(a=>a.ApiId!="A"),"recovery uses a fresh query to skip the successful achievement and retry only remaining selections");
        using var cancel=new CancellationTokenSource();cancel.Cancel();int callbacks=0;
        var cancelled=new LegacyReplayer(new ReplayEvents{Achievement=(a,state)=>callbacks++});bool stopped=false;try{cancelled.Run(preview.Schedule.Values.ToList(),cli,SamCliMode.Modern,42,"Fixture",true,false,0,cancel.Token).GetAwaiter().GetResult();}catch(OperationCanceledException){stopped=true;}Check(stopped && callbacks==0 && File.ReadAllLines(Path.Combine(dir,"commands")).Length==2,"cancellation before replay makes no lock or unlock attempt and preserves the pending report entries");
    }
}
