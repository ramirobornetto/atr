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
        }
        finally {Directory.Delete(root,true);}
    }
}
