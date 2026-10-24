using System;
using System.IO;
using System.Linq;
using System.Threading;
using AchievementTimelineReplay;

static class SourceChecks
{
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);Console.WriteLine("PASS: Sources — "+message);}
    public static void Run(string testRoot)
    {
        string root=Path.Combine(testRoot,"sources"),lua=Path.Combine(root,"config","stplug-in");Directory.CreateDirectory(lua);
        var options=new SourceSelection();
        Check(SourceSelection.ScanSources.Where(s=>s!="Xbox (perfil)").All(options.Scans) && SourceSelection.LibrarySources.All(options.Shows),"first launch includes local sources and shows all library origins; direct online sources are opt-in");
        File.WriteAllText(Path.Combine(lua,"42.lua"),"contents deliberately never executed");
        options.Scan["Steam"]=false;
        Check(options.AllowsSteam(42,root) && !options.AllowsSteam(43,root),"Lua-only scan includes matching AppIDs and excludes BIN histories without a Lua");
        options.Scan["Steam"]=true;options.Scan["SteamIdra/LuaTools"]=false;
        Check(!options.AllowsSteam(42,root) && options.AllowsSteam(43,root),"Steam-only scan excludes Lua matches even if the game is also owned");
        options.Scan["SteamIdra/LuaTools"]=true;
        Check(options.AllowsSteam(42,root) && options.AllowsSteam(43,root),"both Steam scan switches include the complete BIN set without separate duplicate passes");
        var codex=new GameEntry{AppId=1,HistoryId="codex_ini_fixture",StatsPath=Path.Combine(root,"CODEX","1","achievements.ini")};
        var rune=new GameEntry{AppId=1,HistoryId="codex_ini_fixture",StatsPath=Path.Combine(root,"RUNE","1","achievements.ini")};
        var gold=new GameEntry{AppId=1,HistoryId="goldberg_fixture",StatsPath=Path.Combine(root,"Goldberg SteamEmu Saves","1","achievements.json")};
        var gse=new GameEntry{AppId=1,HistoryId="goldberg_fixture",StatsPath=Path.Combine(root,"GSE Saves","1","achievements.json")};
        options.Scan["CODEX"]=false;options.Scan["GSE"]=false;
        Check(!options.Allows(codex,root) && options.Allows(rune,root) && options.Allows(gold,root) && !options.Allows(gse,root),"CODEX/RUNE and Goldberg/GSE have independent scan eligibility by location");
        Check(LibraryStore.ShouldRetainAfterScan(codex,options.Allows(codex,root)) && !LibraryStore.ShouldRetainAfterScan(rune,options.Allows(rune,root)),"disabled origins retain cached entries even when missing, while enabled missing sources are removed");
        options.Visible["RUNE"]=false;options.Visible["Goldberg"]=false;
        Check(options.Shows("CODEX") && !options.Shows("RUNE") && !options.Shows("Goldberg") && options.Shows("GSE") && options.Scans("RUNE"),"library visibility is independent from scan selection for every provider");
        options.Visible["Otro"]=false;
        Check(!options.Shows("An older custom origin") && options.Shows("Sin clasificar"),"unknown manual origins remain reachable through Other rather than disappearing from every filter");
        string path=Path.Combine(root,"options.json");options.Save(path);var restored=SourceSelection.Load(path);
        Check(!restored.Scans("CODEX") && !restored.Scans("GSE") && !restored.Shows("RUNE") && restored.Scans("RUNE"),"scan and visibility preferences survive restart independently");
        string stats=Path.Combine(root,"appcache","stats");Directory.CreateDirectory(stats);
        File.WriteAllText(Path.Combine(stats,"UserGameStats_1_42.bin"),"invalid binary excluded by filter");
        File.WriteAllText(Path.Combine(stats,"UserGameStats_1_43.bin"),"invalid binary excluded by filter");
        var scanner=new LegacyScanner(_=>{});
        var found=scanner.Scan(root,"",SamCliMode.Unknown,null,CancellationToken.None,_=>false);
        Check(found.Count==0 && scanner.Summary.FilteredSources==2 && scanner.Summary.Reviewed==0 && scanner.Summary.ReadErrors==0 && scanner.Summary.NoSchema==0,"disabled BIN sources are filtered before schema checks, parsing or snapshot creation");
        options.Scan["Steam"]=false;options.Scan["SteamIdra/LuaTools"]=true;
        found=scanner.Scan(root,"",SamCliMode.Unknown,null,CancellationToken.None,id=>options.AllowsSteam(id,root));
        Check(found.Count==0 && scanner.Summary.FilteredSources==1 && scanner.Summary.Reviewed==1 && scanner.Summary.NoSchema==1 && scanner.Summary.Classified==scanner.Summary.Reviewed,"Lua matching changes actual scanner eligibility while summary counts stay reconciled");
    }
}
