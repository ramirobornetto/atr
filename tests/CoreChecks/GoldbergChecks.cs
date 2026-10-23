using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using AchievementTimelineReplay;

static class GoldbergChecks
{
    static void Expect(bool condition,string message){if(!condition)throw new Exception(message);Console.WriteLine("PASS: Goldberg — "+message);}
    public static void Run(string root)
    {
        string folder=Path.Combine(root,"goldberg","480");Directory.CreateDirectory(folder);
        string state=Path.Combine(folder,"achievements.json"),defs=Path.Combine(folder,"definitions.json");
        string original="{\"LATE\":{\"earned\":true,\"earned_time\":1704153600},\"FIRST\":{\"earned\":true,\"earned_time\":1704067200},\"LOCKED\":{\"earned\":false,\"earned_time\":1704067201},\"NO_DATE\":{\"earned\":true,\"earned_time\":0}}";
        File.WriteAllText(state,original);
        File.WriteAllText(defs,"[{\"name\":\"FIRST\",\"displayName\":{\"english\":\"First\",\"spanish\":\"Primero\"},\"description\":\"Texto\"},{\"name\":\"EXTRA\",\"displayName\":\"Extra\"}]");
        var warnings=new List<string>();
        var game=GoldbergHistory.Load(state,480,defs,log:warnings.Add);
        Expect(game.Achievements.Count==2 && game.Achievements[0].ApiId=="FIRST" && game.Achievements[1].ApiId=="LATE","only earned achievements with valid dates are imported chronologically");
        Expect(game.Achievements[0].DisplayName=="Primero" && game.Achievements[0].Description=="Texto","localized definitions join by API ID");
        Expect(game.TotalAchievements==5 && warnings.Count==1,"schema and state IDs are combined; missing timestamps are reported");
        Expect(game.AccountId==0 && game.DetectedOrigin=="Goldberg" && game.SourceLabel.Contains("json"),"emulator data is not assigned to the official Steam account");
        var raw=GoldbergHistory.Load(state,480);
        Expect(raw.TotalAchievements==4 && raw.Achievements[0].DisplayName=="FIRST","state-only import works without Steam or a definition");
        string snapshots=Path.Combine(root,"goldberg-snapshots");GoldbergHistory.Protect(game,snapshots);
        string snapshot=Path.Combine(snapshots,game.StorageKey,"snapshot.json");string protectedJson=File.ReadAllText(snapshot);
        Expect(File.ReadAllText(state)==original && File.ReadAllText(Path.Combine(snapshots,game.StorageKey,"original_achievements.json"))==original,"source is unchanged and original bytes are backed up");
        File.WriteAllText(state,"{\"REPLACED\":{\"earned\":true,\"earned_time\":1704200000}}");
        var reloaded=GoldbergHistory.Protect(GoldbergHistory.Load(state,480,defs),snapshots);
        Expect(reloaded.Achievements.Count==2 && reloaded.Achievements[0].ApiId=="FIRST" && File.ReadAllText(snapshot)==protectedJson,"later source edits never replace protected history");
        string other=Path.Combine(root,"another-profile","480");Directory.CreateDirectory(other);File.WriteAllText(Path.Combine(other,"achievements.json"),original);
        var second=GoldbergHistory.Load(Path.Combine(other,"achievements.json"),480);
        var official=new GameEntry{AppId=480,AccountId=7};
        Expect(game.StorageKey!=second.StorageKey && game.StorageKey!=official.StorageKey,"same AppID in different sources or profiles has independent storage");
        var plans=new PlanStore(Path.Combine(root,"goldberg-plans"));
        var plan=PlanStore.Capture(game,game.Achievements,new PlanParameters());plans.Save(game,plan);
        Expect(plans.FilePath(game)!=plans.FilePath(second) && plans.Load(game).HistoryId==game.HistoryId,"saved plans retain source identity");
        try{PlanStore.Restore(second,plan);throw new Exception("Expected source rejection");}catch(InvalidDataException){Expect(true,"cross-profile plans are rejected");}
        var origins=new OriginStore(Path.Combine(root,"goldberg-origins"));origins.Save(game,"Otro");
        Expect(origins.Load(game)=="Otro" && origins.Load(second)==null,"manual origin is independent between imported sources");
        File.WriteAllText(Path.Combine(folder,".atr-demo"),"Synthetic data, never execute on Steam.");
        Expect(GoldbergHistory.Load(state,480).IsDemo,"synthetic fixture is explicitly marked as a demo");
        string demoSnapshots=Path.Combine(root,"goldberg-demo-snapshots");
        GoldbergHistory.Protect(GoldbergHistory.Load(state,480),demoSnapshots);
        File.Delete(Path.Combine(folder,".atr-demo"));
        try{GoldbergHistory.Protect(GoldbergHistory.Load(state,480),demoSnapshots);throw new Exception("Expected demo mismatch rejection");}catch(InvalidDataException){Expect(true,"removing the demo marker cannot turn a protected demo into a live history");}
        File.WriteAllText(state,"[{\"name\":\"FIRST\"}]");
        try{GoldbergHistory.Load(state,480);throw new Exception("Expected format rejection");}catch(InvalidDataException){Expect(true,"definition arrays cannot be mistaken for unlocked state");}
        File.WriteAllText(state,"{\"A\":{\"earned\":true,\"earned_time\":1704067200},\"a\":{\"earned\":true,\"earned_time\":1704067200}}");
        try{GoldbergHistory.Load(state,480);throw new Exception("Expected duplicate rejection");}catch(InvalidDataException){Expect(true,"duplicate API IDs are rejected");}
        File.WriteAllText(state,"{\"A\":{\"earned\":true,\"earned_time\":1704067200000},\"B\":{\"earned\":true,\"earned_time\":\"1704067200\"},\"C\":{\"earned\":true,\"earned_time\":-1}}");
        Expect(GoldbergHistory.Load(state,480).Achievements.Count==0,"milliseconds, string timestamps and negative dates are not guessed");
        File.WriteAllText(state,original);File.WriteAllText(snapshot,"broken");
        try{GoldbergHistory.Protect(GoldbergHistory.Load(state,480),snapshots);throw new Exception("Expected corrupt snapshot rejection");}catch(System.Text.Json.JsonException){Expect(File.ReadAllText(snapshot)=="broken","corrupt snapshot is never overwritten");}
        string scanSnapshots=Path.Combine(root,"goldberg-scan-snapshots");
        var found=GoldbergHistory.Scan(Path.GetDirectoryName(folder),null,scanSnapshots,warnings.Add,CancellationToken.None);
        Expect(found.Count==1 && found[0].AppId==480,"standard root scan discovers numeric AppID folders offline");
        string sample=Path.Combine(AppContext.BaseDirectory,"GoldbergSample","480");
        var shipped=GoldbergHistory.Load(Path.Combine(sample,"achievements.json"),480,Path.Combine(sample,"steam_settings","achievements.json"));
        Expect(shipped.IsDemo && shipped.Achievements.Count==4 && shipped.TotalAchievements==6 && shipped.Achievements[0].DisplayName=="Primer paso","shipped demo contains 4 / 6 with localized names and Steam isolation marker");
        Expect(shipped.Achievements[2].OriginalUnix-shipped.Achievements[1].OriginalUnix==64200,"shipped demo contains the documented 17h 50m gap");
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();
        try{GoldbergHistory.Scan(folder,null,scanSnapshots,warnings.Add,cancelled.Token);throw new Exception("Expected cancel");}catch(OperationCanceledException){Expect(true,"scan cancellation propagates");}
    }
}
