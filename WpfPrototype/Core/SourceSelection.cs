using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace AchievementTimelineReplay
{
    internal sealed class SourceSelection
    {
        public static readonly string[] ScanSources={"Steam","SteamIdra/LuaTools","Goldberg","GSE","CODEX","RUNE","CODEX/RUNE compatible"};
        public static readonly string[] LibrarySources=ScanSources.Concat(new[]{"OnlineFix","Otro","Sin clasificar"}).ToArray();
        public Dictionary<string,bool> Scan {get;set;}=new Dictionary<string,bool>();
        public Dictionary<string,bool> Visible {get;set;}=new Dictionary<string,bool>();
        public bool Scans(string source) => !Scan.TryGetValue(source,out bool enabled) || enabled;
        public bool Shows(string source) => !Visible.TryGetValue(LibraryCategory(source),out bool enabled) || enabled;
        public static string LibraryCategory(string source) => LibrarySources.Contains(source)?source:"Otro";
        public bool AllowsSteam(uint appId,string steamRoot) => Scans(File.Exists(Path.Combine(steamRoot,"config","stplug-in",appId+".lua"))?"SteamIdra/LuaTools":"Steam");
        public bool Allows(GameEntry game,string steamRoot)
        {
            // Scan eligibility follows the file's detected format/location, never a manual label.
            if(String.IsNullOrEmpty(game.HistoryId))return AllowsSteam(game.AppId,steamRoot);
            OriginDetector.Apply(game,steamRoot);return Scans(game.DetectedOrigin??"CODEX/RUNE compatible");
        }
        public static SourceSelection Load(string path)
        {
            if(!File.Exists(path))return new SourceSelection();
            if(new FileInfo(path).Length>32768)throw new InvalidDataException("Preferencias de fuentes demasiado grandes.");
            var value=JsonSerializer.Deserialize<SourceSelection>(File.ReadAllText(path));
            if(value?.Scan==null || value.Visible==null)throw new InvalidDataException("Preferencias de fuentes inválidas.");
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
