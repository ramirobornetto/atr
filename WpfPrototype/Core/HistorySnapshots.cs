using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;
namespace AchievementTimelineReplay
{
    internal static class HistorySnapshots
    {
        public static GameEntry Protect(GameEntry game,string snapshotsDirectory)
        {
            string directory=Path.Combine(snapshotsDirectory,game.StorageKey);
            string path=Path.Combine(directory,"snapshot.json");
            if(File.Exists(path))
            {
                var snapshot=JsonSerializer.Deserialize<HistorySnapshot>(File.ReadAllText(path));
                if(snapshot==null || snapshot.Version!=1 || snapshot.AppId!=game.AppId || snapshot.HistoryId!=game.HistoryId || (snapshot.IsDemo!=game.IsDemo && !(game.IsDemo && snapshot.Achievements!=null && snapshot.Achievements.All(a=>a!=null && a.ApiId!=null && a.ApiId.StartsWith("ATR_DEMO_",StringComparison.Ordinal)))) || snapshot.Achievements==null || snapshot.Achievements.Count==0 || snapshot.Achievements.Any(a=>a==null || String.IsNullOrWhiteSpace(a.ApiId) || a.OriginalUnix<=0 || a.OriginalUnix>UInt32.MaxValue) || snapshot.Achievements.Select(a=>a.ApiId).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=snapshot.Achievements.Count)
                    throw new InvalidDataException("Snapshot local inválido. No se sobrescribió.");
                game.Achievements=snapshot.Achievements.Select(a=>new AchievementRecord{ApiId=a.ApiId,DisplayName=a.DisplayName,Description=a.Description,OriginalUnix=a.OriginalUnix}).ToList();
                game.TotalAchievements=Math.Max(game.TotalAchievements,game.Achievements.Count);
            }
            else
            {
                if(game.Achievements.Count==0)throw new InvalidDataException("No hay logros desbloqueados con fecha válida para importar.");
                Directory.CreateDirectory(directory);
                // Keep original bytes separate from our protected historical representation.
                File.Copy(game.StatsPath,Path.Combine(directory,"original_"+Path.GetFileName(game.StatsPath)),false);
                if(!String.IsNullOrEmpty(game.SchemaPath))File.Copy(game.SchemaPath,Path.Combine(directory,"original_definitions.json"),false);
                var snapshot=new HistorySnapshot{AppId=game.AppId,HistoryId=game.HistoryId,IsDemo=game.IsDemo,Achievements=game.Achievements.Select(a=>new SnapshotAchievement{ApiId=a.ApiId,DisplayName=a.DisplayName,Description=a.Description,OriginalUnix=a.OriginalUnix}).ToList()};
                using var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);
                JsonSerializer.Serialize(stream,snapshot);stream.Flush(true);
            }
            game.HasSnapshot=true;return game;
        }
    }
    internal sealed class HistorySnapshot
    {
        public int Version {get;set;}=1;
        public uint AppId {get;set;}
        public string HistoryId {get;set;}
        public bool IsDemo {get;set;}
        public List<SnapshotAchievement> Achievements {get;set;}
    }
}
