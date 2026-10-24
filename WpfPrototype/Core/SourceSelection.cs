using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace AchievementTimelineReplay
{
    internal sealed class SourceSelection
    {
        public static readonly string[] ScanSources={"Steam","SteamIdra/LuaTools","Goldberg","GSE","CODEX","RUNE","CODEX/RUNE compatible","PA","Xbox (perfil)","Epic (perfil)"};
        public static readonly string[] LibrarySources=ScanSources.Where(s=>s!="Xbox (perfil)" && s!="Epic (perfil)").Concat(new[]{"Xbox","Epic","GOG","PlayStation","OnlineFix","Otro","Sin clasificar"}).ToArray();
        public List<string> PAPaths {get;set;}=new List<string>();
        public Dictionary<string,bool> Scan {get;set;}=new Dictionary<string,bool>();
        public Dictionary<string,bool> Visible {get;set;}=new Dictionary<string,bool>();
        public bool Scans(string source) => Scan.TryGetValue(source,out bool enabled)?enabled:source!="Xbox (perfil)" && source!="Epic (perfil)";
        public bool Shows(string source) => !Visible.TryGetValue(LibraryCategory(source),out bool enabled) || enabled;
        public static string DisplayOrigin(string source)=>source=="Xbox"?"Xbox Live":source=="Xbox → Steam"?"Xbox Live → Steam":source;
        public static string LibraryCategory(string source) => source=="Xbox Live"?"Xbox":LibrarySources.Contains(source)?source:source!=null && source.EndsWith(" → Steam",StringComparison.Ordinal)?LibraryCategory(source.Substring(0,source.Length-8)):"Otro";
        public bool AllowsSteam(uint appId,string steamRoot) => Scans(File.Exists(Path.Combine(steamRoot,"config","stplug-in",appId+".lua"))?"SteamIdra/LuaTools":"Steam");
        public bool Allows(GameEntry game,string steamRoot)
        {
            // Scan eligibility follows the file's detected format/location, never a manual label.
            if(EpicProfile.IsEpic(game) || (PlatformMapping.IsMapped(game) && game.MappedSourceKey?.StartsWith("epic_",StringComparison.Ordinal)==true))return Scans("Epic (perfil)");
            if(XboxProfile.IsXbox(game))return Scans("Xbox (perfil)");
            if(PlatformMapping.IsMapped(game) && game.MappedSourceKey?.StartsWith("xbox_",StringComparison.Ordinal)==true)return Scans("Xbox (perfil)");
            if(PlayniteHistory.IsPA(game) || PlatformMapping.IsMapped(game))return Scans("PA");
            if(String.IsNullOrEmpty(game.HistoryId))return AllowsSteam(game.AppId,steamRoot);
            OriginDetector.Apply(game,steamRoot);return Scans(game.DetectedOrigin??"CODEX/RUNE compatible");
        }
        public static SourceSelection Load(string path)
        {
            if(!File.Exists(path))return new SourceSelection();
            if(new FileInfo(path).Length>32768)throw new InvalidDataException("Preferencias de fuentes demasiado grandes.");
            var value=JsonSerializer.Deserialize<SourceSelection>(File.ReadAllText(path));
            if(value?.Scan==null || value.Visible==null)throw new InvalidDataException("Preferencias de fuentes inválidas.");
            if(value.PAPaths==null || value.PAPaths.Count>64 || value.PAPaths.Any(p=>String.IsNullOrWhiteSpace(p) || p.Length>32768 || !Path.IsPathFullyQualified(p) || !String.Equals(Path.GetFileName(p),"achievement_cache.db",StringComparison.OrdinalIgnoreCase)))throw new InvalidDataException("Rutas PA inválidas.");
            value.Scan=value.Scan.Where(p=>ScanSources.Contains(p.Key)).ToDictionary(p=>p.Key,p=>p.Value);
            value.Visible=value.Visible.Where(p=>LibrarySources.Contains(p.Key)).ToDictionary(p=>p.Key,p=>p.Value);
            return value;
        }
        public void Save(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try{File.WriteAllText(temp,JsonSerializer.Serialize(this));File.Move(temp,path,true);}
            finally{if(File.Exists(temp))File.Delete(temp);}
        }
    }
}
