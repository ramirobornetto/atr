using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace AchievementTimelineReplay
{
    internal sealed class LibraryEntry
    {
        public uint AppId {get;set;}
        public uint AccountId {get;set;}
        public string HistoryId {get;set;}
        public string GameName {get;set;}
        public string StatsPath {get;set;}
        public string SchemaPath {get;set;}
        public int TotalAchievements {get;set;}
        public string DemoGameName {get;set;}
        public bool IsDemo {get;set;}
        public bool RetainedFromCatalog {get;set;}
    }
    internal sealed class LibraryCatalog
    {
        public int Version {get;set;}=1;
        public DateTimeOffset SavedAtUtc {get;set;}
        public DateTimeOffset? LastScanAtUtc {get;set;}
        public string SteamRoot {get;set;}
        public string Summary {get;set;}
        public List<LibraryEntry> Entries {get;set;}=new List<LibraryEntry>();
    }
    internal sealed class LoadedLibrary
    {
        public LibraryCatalog Catalog;
        public readonly List<GameEntry> Games=new List<GameEntry>();
        public readonly List<string> Warnings=new List<string>();
        public bool UsedBackup;
    }
    internal sealed class LibraryStore
    {
        readonly string directory,snapshots;
        public LibraryStore(string directory,string snapshots){this.directory=directory;this.snapshots=snapshots;}
        public static bool NeedsAccountRefresh(GameEntry game,uint activeAccount) => String.IsNullOrEmpty(game.HistoryId) && activeAccount!=0 && game.AccountId!=activeAccount;
        public string FilePath => Path.Combine(directory,"catalog.json");
        public string BackupPath => Path.Combine(directory,"catalog.previous.json");
        public string SnapshotPath(GameEntry game) => Path.Combine(snapshots,game.StorageKey,"snapshot.json");
        static LibraryCatalog ReadCatalog(string path)
        {
            if(new FileInfo(path).Length>16*1024*1024)throw new InvalidDataException("Catálogo de biblioteca demasiado grande.");
            var catalog=JsonSerializer.Deserialize<LibraryCatalog>(File.ReadAllText(path));
            if(catalog==null || catalog.Version!=1 || catalog.SavedAtUtc==default || catalog.Entries==null || catalog.Entries.Count>20000 || (catalog.Summary?.Length??0)>20000 || (catalog.SteamRoot?.Length??0)>32768)throw new InvalidDataException("Formato de biblioteca guardada no compatible.");
            return catalog;
        }
        static GameEntry Model(LibraryEntry entry)
        {
            if(entry==null || entry.AppId==0 || String.IsNullOrWhiteSpace(entry.GameName) || entry.GameName.Length>1000 || String.IsNullOrWhiteSpace(entry.StatsPath) || entry.StatsPath.Length>32768 || (entry.SchemaPath?.Length??0)>32768 || !Path.IsPathFullyQualified(entry.StatsPath) || entry.TotalAchievements<0 || entry.TotalAchievements>100000 || (!String.IsNullOrEmpty(entry.SchemaPath) && !Path.IsPathFullyQualified(entry.SchemaPath)) || (entry.DemoGameName?.Length??0)>200)
                throw new InvalidDataException("Entrada de biblioteca inválida.");
            string id=entry.HistoryId;
            if(!String.IsNullOrEmpty(id))
            {
                if(id!=GoldbergHistory.Identity(entry.StatsPath) && id!=IniHistory.Identity(entry.StatsPath))throw new InvalidDataException("La identidad del historial no coincide con su ruta.");
                if(entry.AccountId!=0)throw new InvalidDataException("La fuente local no corresponde a una cuenta Steam.");
            }
            else if(!String.Equals(Path.GetFileName(entry.StatsPath),$"UserGameStats_{entry.AccountId}_{entry.AppId}.bin",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Archivo Steam y cuenta/AppID no coinciden.");
            return new GameEntry{AppId=entry.AppId,AccountId=entry.AccountId,HistoryId=String.IsNullOrEmpty(id)?null:id,GameName=entry.GameName,StatsPath=entry.StatsPath,SchemaPath=entry.SchemaPath,TotalAchievements=entry.TotalAchievements,DemoGameName=entry.DemoGameName,IsDemo=entry.IsDemo,RetainedFromCatalog=entry.RetainedFromCatalog,SourceLabel=String.IsNullOrEmpty(id)?"UserGameStats (.bin)":id.StartsWith("goldberg_",StringComparison.Ordinal)?"achievements.json (Goldberg/GBE)":"achievements.ini (CODEX/RUNE)"};
        }
        void LoadSnapshot(GameEntry game)
        {
            string path=SnapshotPath(game);
            if(!File.Exists(path) || new FileInfo(path).Length>16*1024*1024)throw new InvalidDataException("Snapshot ausente o demasiado grande.");
            using var doc=JsonDocument.Parse(File.ReadAllText(path));var root=doc.RootElement;
            if(root.GetProperty("AppId").GetUInt32()!=game.AppId)throw new InvalidDataException("El snapshot pertenece a otro juego.");
            if(String.IsNullOrEmpty(game.HistoryId))
            {
                if(root.GetProperty("AccountId").GetUInt32()!=game.AccountId)throw new InvalidDataException("El snapshot pertenece a otra cuenta.");
            }
            else
            {
                if(root.GetProperty("Version").GetInt32()!=1 || root.GetProperty("HistoryId").GetString()!=game.HistoryId)throw new InvalidDataException("Identidad de snapshot local incorrecta.");
                if(root.TryGetProperty("IsDemo",out var demo) && demo.ValueKind==JsonValueKind.True)game.IsDemo=true;
            }
            var records=root.GetProperty("Achievements");
            if(records.ValueKind!=JsonValueKind.Array || records.GetArrayLength()==0 || records.GetArrayLength()>50000)throw new InvalidDataException("Snapshot sin logros válidos.");
            var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var achievements=new List<AchievementRecord>();
            foreach(var record in records.EnumerateArray())
            {
                string api=record.GetProperty("ApiId").GetString();long unix=record.GetProperty("OriginalUnix").GetInt64();
                if(String.IsNullOrWhiteSpace(api) || api.Length>512 || !seen.Add(api) || unix<=0 || unix>253402300799)throw new InvalidDataException("Logro de snapshot inválido o duplicado.");
                string title=record.TryGetProperty("DisplayName",out var name) && name.ValueKind==JsonValueKind.String?name.GetString():api;
                string description=record.TryGetProperty("Description",out var desc) && desc.ValueKind==JsonValueKind.String?desc.GetString():"";
                if((title?.Length??0)>10000 || (description?.Length??0)>50000)throw new InvalidDataException("Metadatos del snapshot demasiado grandes.");
                achievements.Add(new AchievementRecord{ApiId=api,DisplayName=title,Description=description,OriginalUnix=unix});
            }
            if(achievements.All(a=>a.ApiId.StartsWith("ATR_DEMO_",StringComparison.Ordinal)))game.IsDemo=true;
            game.Achievements=achievements.OrderBy(a=>a.OriginalUnix).ThenBy(a=>a.ApiId,StringComparer.Ordinal).ToList();
            game.TotalAchievements=Math.Max(game.TotalAchievements,game.Achievements.Count);game.HasSnapshot=true;game.HasLocalHistory=true;
        }
        public LoadedLibrary Load()
        {
            var result=new LoadedLibrary();
            if(!File.Exists(FilePath) && !File.Exists(BackupPath))return result;
            try{result.Catalog=ReadCatalog(FilePath);}
            catch(Exception ex)
            {
                result.Warnings.Add("No se pudo leer el catálogo principal: "+ex.Message);
                if(!File.Exists(BackupPath))return result;
                try{result.Catalog=ReadCatalog(BackupPath);result.UsedBackup=true;}
                catch(Exception backupError){result.Warnings.Add("No se pudo leer la copia anterior: "+backupError.Message);return result;}
            }
            var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var entry in result.Catalog.Entries)
            {
                GameEntry game;
                try{game=Model(entry);if(!seen.Add(game.StorageKey))throw new InvalidDataException("Entrada duplicada.");}
                catch(Exception ex){result.Warnings.Add("Entrada de biblioteca omitida: "+ex.Message);continue;}
                try{LoadSnapshot(game);}
                catch(Exception ex){game.CatalogIssue="Sin snapshot válido";result.Warnings.Add("AppID "+game.AppId+": "+ex.Message);}
                game.SourceMissing=!File.Exists(game.StatsPath);OriginDetector.Apply(game,result.Catalog.SteamRoot);
                result.Games.Add(game);
            }
            return result;
        }
        public void Save(IEnumerable<GameEntry> games,string steamRoot,DateTimeOffset? lastScan,string summary)
        {
            var catalog=new LibraryCatalog{SavedAtUtc=DateTimeOffset.UtcNow,LastScanAtUtc=lastScan,SteamRoot=steamRoot,Summary=summary??""};
            foreach(var game in games.GroupBy(g=>g.StorageKey,StringComparer.OrdinalIgnoreCase).Select(g=>g.First()))
            {
                var entry=new LibraryEntry{AppId=game.AppId,AccountId=game.AccountId,HistoryId=game.HistoryId,GameName=game.GameName,StatsPath=Path.GetFullPath(game.StatsPath),SchemaPath=String.IsNullOrEmpty(game.SchemaPath)?null:Path.GetFullPath(game.SchemaPath),TotalAchievements=game.TotalAchievements,IsDemo=game.IsDemo,DemoGameName=game.DemoGameName,RetainedFromCatalog=game.RetainedFromCatalog};
                Model(entry);catalog.Entries.Add(entry);
            }
            Directory.CreateDirectory(directory);string temp=FilePath+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){JsonSerializer.Serialize(stream,catalog,new JsonSerializerOptions{WriteIndented=true});stream.Flush(true);}
                if(File.Exists(FilePath))
                {
                    bool readable=true;try{ReadCatalog(FilePath);}catch{readable=false;}
                    if(readable)
                    {
                        string previous=BackupPath+"."+Guid.NewGuid().ToString("N")+".tmp";
                        try{File.Copy(FilePath,previous,false);File.Move(previous,BackupPath,true);}finally{if(File.Exists(previous))File.Delete(previous);}
                    }
                    else File.Copy(FilePath,Path.Combine(directory,"catalog.corrupt_"+Guid.NewGuid().ToString("N")+".json"),false);
                }
                File.Move(temp,FilePath,true);
            }
            finally{if(File.Exists(temp))File.Delete(temp);}
        }
    }
}
