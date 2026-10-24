using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using AchievementTimelineReplay;

static class Program
{
    static Dictionary<string,object> D(params object[] pairs)
    {
        var d=new Dictionary<string,object>();
        for(int i=0;i<pairs.Length;i+=2)d[(string)pairs[i]]=pairs[i+1];
        return d;
    }
    static void WriteObject(BinaryWriter w,Dictionary<string,object> d)
    {
        foreach(var kv in d)
        {
            w.Write((byte)(kv.Value is Dictionary<string,object>?0:kv.Value is string?1:2));
            w.Write(Encoding.UTF8.GetBytes(kv.Key));w.Write((byte)0);
            if(kv.Value is Dictionary<string,object> child)WriteObject(w,child);
            else if(kv.Value is string s){w.Write(Encoding.UTF8.GetBytes(s));w.Write((byte)0);}
            else w.Write(Convert.ToUInt32(kv.Value));
        }
        w.Write((byte)8);
    }
    static void Save(string path,Dictionary<string,object> d)
    {
        using var w=new BinaryWriter(File.Create(path));WriteObject(w,d);
    }
    static void Expect(bool condition,string message)
    {
        if(!condition)throw new Exception(message);
        Console.WriteLine("PASS: "+message);
    }
    static void Main()
    {
        string root=Environment.GetEnvironmentVariable("ATR_TEST_ROOT") ?? throw new Exception("Run with tests/run-core-checks.sh to isolate test data.");
        Directory.CreateDirectory(root);
        try
        {
            SavedPlanChecks.Run(root);
            GoldbergChecks.Run(root);
            IniChecks.Run(root);
            UpdateChecks.Run(root);
            LibraryChecks.Run(root);
            SourceChecks.Run(root);
            PlayniteChecks.Run(root);
            GroupingMappingChecks.Run(root);
            DiscoveryMatchingChecks.Run(root);
            XboxChecks.Run(root);
            EpicChecks.Run(root);
            MigrationChecks.Run(root);
            var countedGame=new GameEntry{TotalAchievements=40};
            for(int i=0;i<16;i++)countedGame.Achievements.Add(new AchievementRecord{ApiId="ACH_"+i});
            var partialStates=new Dictionary<string,string>();
            for(int i=0;i<16;i++)partialStates["ACH_"+i]="Desbloqueado";
            Expect(AchievementCounts.Format(countedGame)=="16 / 40","Historical progress uses full schema total");
            Expect(AchievementCounts.Format(countedGame,partialStates)=="16 / 40","Partial Steam response retains 16 / 40 instead of 16 / 16");
            var fullStates=new Dictionary<string,string>(partialStates);
            for(int i=16;i<40;i++)fullStates["ACH_"+i]="Bloqueado";
            Expect(AchievementCounts.Format(countedGame,fullStates)=="16 / 40","Full Steam response counts only unlocked achievements");
            for(int i=40;i<45;i++)fullStates["ACH_"+i]="Bloqueado";
            Expect(AchievementCounts.Format(countedGame,fullStates)=="16 / 45" && AchievementCounts.Format(countedGame,partialStates)=="16 / 45","New larger totals are remembered across later partial responses");
            Expect(AchievementCounts.Format(countedGame,new Dictionary<string,string>{{"ACH_0","Bloqueado"}})=="0 / 45","A zero-unlock response retains the known total");

            var first=new AchievementRecord{ApiId="A",OriginalUnix=100};
            var second=new AchievementRecord{ApiId="B",OriginalUnix=100+18*3600};
            var third=new AchievementRecord{ApiId="C",OriginalUnix=100+18*3600+3*86400};
            var order=new List<AchievementRecord>{first,second,third};
            var full=LegacyPlanner.Calculate(order,LegacyPlanner.OriginalSpan(order));
            Expect(full[second].GapSeconds==18*3600 && full[third].GapSeconds==3*86400,"Default retains waits across hours and days");
            var limited=LegacyPlanner.Calculate(order,LegacyPlanner.OriginalSpan(order),1800);
            Expect(limited[first].OffsetSeconds==0 && limited[second].GapSeconds==1800 && limited[third].OffsetSeconds==3600,"Optional cap compresses each long wait including different days");
            second.ManualGapSeconds=7200;
            limited=LegacyPlanner.Calculate(order,LegacyPlanner.OriginalSpan(order),1800);
            Expect(limited[second].GapSeconds==7200 && limited[third].OffsetSeconds==9000,"Manual waits override the automatic cap");
            second.ManualGapSeconds=null;
            limited=LegacyPlanner.Calculate(order,600,1800);
            Expect(limited[third].OffsetSeconds==600,"Short scaled waits are preserved");
            second.Included=false;
            limited=LegacyPlanner.Calculate(order,LegacyPlanner.OriginalSpan(order),1800);
            Expect(limited.Count==2 && limited[third].OffsetSeconds==1800 && !limited.ContainsKey(second),"Deselected achievements are excluded before limiting gaps");
            Expect(second.OriginalUnix==100+18*3600 && third.OriginalUnix==100+18*3600+3*86400,"Original historical timestamps remain unchanged");
            var steam=SamRunner.States("[{\"id\":\"OFFICIAL\",\"is_achieved\":true,\"unlock_time\":1234567890},{\"id\":\"NO_DATE\",\"is_achieved\":true},{\"id\":\"LOCKED\",\"is_achieved\":false}]");
            var official=new AchievementRecord{ApiId="OFFICIAL",Included=true,OriginalUnix=99,ManualGapSeconds=77};
            var noDate=new AchievementRecord{ApiId="NO_DATE",Included=true};
            var locked=new AchievementRecord{ApiId="LOCKED",Included=true};
            var excluded=new AchievementRecord{ApiId="LOCKED",Included=false};
            var missing=new AchievementRecord{ApiId="UNKNOWN",Included=true};
            var selection=new List<AchievementRecord>{official,noDate,locked,excluded,missing};
            Expect(SamRunner.ApplySteamStates(selection,steam,true)==2 && !official.Included && !noDate.Included,"Steam query deselects official unlocks with and without timestamps");
            Expect(locked.Included && !excluded.Included && missing.Included && missing.SteamState=="No encontrado","Steam query preserves other selections and does not invent missing states");
            Expect(official.OriginalUnix==99 && official.ManualGapSeconds==77,"Steam query leaves historical dates and manual timings unchanged");
            Expect(SamRunner.ApplySteamStates(selection,steam,true)==0,"Repeating the same Steam query is idempotent");
            official.Included=true;
            Expect(SamRunner.ApplySteamStates(selection,steam,false)==0 && official.Included,"Explicit repair verification can retain a manually reselected unlock");
            string statsDir=Path.Combine(root,"appcache","stats");Directory.CreateDirectory(statsDir);
            string schema=Path.Combine(statsDir,"UserGameStatsSchema_42.bin");
            string stats=Path.Combine(statsDir,"UserGameStats_7_42.bin");
            Save(schema,D("42",D("gamename","Fixture","stats",D(
                "1",D("type","ACHIEVEMENTS","bits",D("0",D("name","FIRST"),"1",D("name","SECOND"))),
                "2",D("type","ACHIEVEMENTS","bits",D("0",D("name","THIRD"))),
                "3",D("type","INT","bits",D("0",D("name","NOT_AN_ACHIEVEMENT")))))));
            Save(stats,D("cache",D("1",D("AchievementTimes",D("0",100,"1",0)))));
            var g=HistoryLoader.Load(stats,schema,7,42);
            Expect(g.Achievements.Count==1 && g.TotalAchievements==3,"Partial history counts 1 / 3, including schema groups absent from cache");
            Save(stats,D("cache",D("1",D("AchievementTimes",D("0",100,"1",200)),"2",D("AchievementTimes",D("0",300)))));
            g=HistoryLoader.Load(stats,schema,7,42);
            Expect(g.Achievements.Count==3 && g.TotalAchievements==3,"Complete history counts 3 / 3, excluding ordinary stats");
            string sampleFolder=Path.Combine(AppContext.BaseDirectory,"GoldbergSample","480");
            var renamedSample=GoldbergHistory.Load(Path.Combine(sampleFolder,"achievements.json"),42,Path.Combine(sampleFolder,"steam_settings","achievements.json"),schema);
            Expect(renamedSample.IsDemo && renamedSample.TotalAchievements==6,"Synthetic achievements never merge with an unrelated real Steam schema after changing AppID");
            GameNames.Apply(renamedSample,new Dictionary<uint,string>{{42,"Public title"}});
            Expect(renamedSample.GameName=="Public title (muestra Goldberg)","Resolved public names keep the demo designation after renaming AppID");

            string data=LegacyTools.DataDirectory;
            Expect(Path.GetFullPath(data).StartsWith(root+Path.DirectorySeparatorChar,StringComparison.Ordinal),"Test data directory is isolated");
            File.WriteAllText(LegacyTools.NamesPath,"42\tFixture\n");
            string snapshot=Path.Combine(data,"Snapshots","7_42","snapshot.json");
            Directory.CreateDirectory(Path.GetDirectoryName(snapshot));
            string payload="{\"AppId\":42,\"AccountId\":7,\"Achievements\":[{\"ApiId\":\"FIRST\",\"DisplayName\":\"Original\",\"OriginalUnix\":100}]}";
            File.WriteAllText(snapshot,payload);
            var scanner=new LegacyScanner(_=>{});
            var found=scanner.Scan(root,"",SamCliMode.Unknown,null,CancellationToken.None);
            Expect(found.Count==1 && found[0].Achievements.Count==1 && found[0].TotalAchievements==3,"Existing snapshot retains historical 1 / 3 with total from current schema");
            Expect(File.ReadAllText(snapshot)==payload,"Existing snapshot remains unchanged");
            Save(stats,D("cache",D()));
            g=HistoryLoader.Load(stats,schema,7,42);
            Expect(g.Achievements.Count==0 && g.TotalAchievements==3,"Empty history counts 0 / 3 without inventing unlocks");
            string summaryRoot=Path.Combine(root,"summary-steam"),summaryStats=Path.Combine(summaryRoot,"appcache","stats");Directory.CreateDirectory(summaryStats);
            foreach(int id in new[]{42,43,45,46})
                Save(Path.Combine(summaryStats,"UserGameStatsSchema_"+id+".bin"),D(id.ToString(),D("gamename","Internal codename","stats",D("1",D("type","ACHIEVEMENTS","bits",D("0",D("name","FIRST")))))));
            foreach(string name in new[]{"UserGameStats_17_42.bin","UserGameStats_18_42.bin","UserGameStats_17_46.bin"})Save(Path.Combine(summaryStats,name),D("cache",D("1",D("AchievementTimes",D("0",100)))));
            Save(Path.Combine(summaryStats,"UserGameStats_17_43.bin"),D("cache",D()));
            File.WriteAllText(Path.Combine(summaryStats,"UserGameStats_17_44.bin"),"no schema");
            File.WriteAllText(Path.Combine(summaryStats,"UserGameStats_17_45.bin"),"broken binary");
            File.WriteAllText(Path.Combine(summaryStats,"UserGameStats_bad_47.bin"),"invalid filename");
            string brokenSnapshot=Path.Combine(data,"Snapshots","17_46");Directory.CreateDirectory(brokenSnapshot);File.WriteAllText(Path.Combine(brokenSnapshot,"snapshot.json"),"broken");
            File.WriteAllText(LegacyTools.NamesPath,"42\tPublic title\n46\tSecond title\n");
            found=scanner.Scan(summaryRoot,"",SamCliMode.Unknown,null,CancellationToken.None);
            var summary=scanner.Summary;
            Expect(summary.Reviewed==7 && summary.Recovered==1 && summary.NoDates==1 && summary.NoSchema==1 && summary.ReadErrors==1 && summary.SnapshotErrors==1 && summary.Duplicates==1 && summary.InvalidIds==1,"Scan summary classifies every reviewed candidate by its actual outcome");
            Expect(summary.Classified==summary.Reviewed && found.Count==summary.Recovered,"Summary categories sum to candidates and recovered count matches the library");
            Expect(summary.Format("Steam").Contains("7 revisados") && summary.Format("Steam").Contains("1 sin esquema"),"Scan summary exposes omission reasons in the result text");
            var inferred=new GameEntry{AppId=42,StatsPath=Path.Combine(summaryStats,"UserGameStats_17_42.bin")};OriginDetector.Apply(inferred,summaryRoot);
            Expect(inferred.DetectedOrigin=="Steam","Steam histories get an automatic neutral Steam origin");
            string luaDirectory=Path.Combine(summaryRoot,"config","stplug-in");Directory.CreateDirectory(luaDirectory);File.WriteAllText(Path.Combine(luaDirectory,"42.lua"),"not executed");
            OriginDetector.Apply(inferred,summaryRoot);
            Expect(inferred.DetectedOrigin=="SteamIdra/LuaTools" && inferred.OriginEvidence.Contains("no demuestra"),"Matching Lua suggests Steamidra without claiming proven provenance");
            File.WriteAllText(Path.Combine(luaDirectory,"99.lua"),"not executed");File.Delete(Path.Combine(luaDirectory,"42.lua"));OriginDetector.Apply(inferred,summaryRoot);
            Expect(inferred.DetectedOrigin=="Steam","Unrelated Lua does not classify a game as Steamidra");
            var sourceGame=new GameEntry{AppId=42,GameName="Internal codename",HistoryId="goldberg_fixture",StatsPath="Goldberg/42/achievements.json"};OriginDetector.Apply(sourceGame,summaryRoot);
            Expect(sourceGame.DetectedOrigin=="Goldberg","Goldberg format retains its origin regardless of Steam Lua presence");
            GameNames.Apply(sourceGame,new Dictionary<uint,string>{{42,"Public title"}});
            Expect(sourceGame.GameName=="Public title","Goldberg titles use public name cache rather than schema codenames");
            GameNames.Apply(sourceGame,new Dictionary<uint,string>());
            Expect(sourceGame.GameName=="AppID 42","Unavailable public names fall back to explicit AppID instead of internal codenames");
            Expect(GameNames.StoreName(2322010,"{\"2322010\":{\"success\":true,\"data\":{\"name\":\"God of War Ragnarok\"}}}")=="God of War Ragnarok" && GameNames.StoreName(499,"{\"499\":{\"success\":false}}")==null,"Store metadata is matched to the requested AppID and unavailable titles stay unknown");
            string orphanRoot=Path.Combine(root,"orphan-dates"),orphanStats=Path.Combine(orphanRoot,"appcache","stats");Directory.CreateDirectory(orphanStats);
            string orphanSchema=Path.Combine(orphanStats,"UserGameStatsSchema_700.bin"),orphanFile=Path.Combine(orphanStats,"UserGameStats_1_700.bin");
            Save(orphanSchema,D("700",D("gamename","Current game","stats",D("2",D("type","ACHIEVEMENTS","bits",D()),"358",D("type","ACHIEVEMENTS","bits",D("0",D("name","CURRENT")))))));
            Save(orphanFile,D("cache",D("2",D("AchievementTimes",D("4",1495847257,"14",1495849401)),"16",D("AchievementTimes",D("2",1495847450)))));
            var orphan=HistoryLoader.Load(orphanFile,orphanSchema,1,700);
            Expect(orphan.UnmatchedHistoricalDates==3 && orphan.Achievements.Count==0,"Historic dates remain detectable after their definitions disappear from current schema");
            found=scanner.Scan(orphanRoot,"",SamCliMode.Unknown,null,CancellationToken.None);
            Expect(found.Count==0 && scanner.Summary.UnmappedHistories==1 && scanner.Summary.UnmatchedDates==3 && scanner.Summary.NoDates==0 && scanner.Summary.Classified==scanner.Summary.Reviewed,"Unmapped historic dates have a distinct reconciled scan category");
            Save(orphanSchema,D("700",D("gamename","Current game","stats",D("2",D("type","ACHIEVEMENTS","bits",D("4",D("name","STILL_DEFINED")))))));
            var partial=HistoryLoader.Load(orphanFile,orphanSchema,1,700);
            Expect(partial.UnmatchedHistoricalDates==2 && partial.Achievements.Count==1 && partial.Achievements[0].ApiId=="STILL_DEFINED","Partial schema recovers only matched dates without inventing API names for the remainder");
            Save(orphanSchema,D("700",D("gamename","Current game","stats",D("2",D("type","ACHIEVEMENTS","bits",D("4",D("display",D())))))));
            var unnamed=HistoryLoader.Load(orphanFile,orphanSchema,1,700);
            Expect(unnamed.Achievements.Count==0 && unnamed.UnmatchedHistoricalDates==3,"Definitions without API IDs never produce invented bit_N unlock targets");



        }
        finally {Directory.Delete(root,true);}
    }
}
