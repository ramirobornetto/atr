using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace AchievementTimelineReplay
{
    internal sealed class HistoryGroup
    {
        public List<GameEntry> Sources=new List<GameEntry>();
        public GameEntry Selected;
        public bool Manual;
        public static string Label(GameEntry game)=>String.IsNullOrEmpty(game.HistoryId)?"Steam directo":XboxProfile.IsXbox(game)?"Xbox Live (perfil)":PlayniteHistory.IsPA(game)?"PA / "+game.PAProvider:PlatformMapping.IsMapped(game)?(game.MappedSourceKey?.StartsWith("xbox_",StringComparison.Ordinal)==true?"Xbox Live":"PA / "+game.MappedFromPlatform)+" → Steam":game.DetectedOrigin??game.SourceLabel;
        public string Summary => (Manual?"Manual: ":"Automático: ")+Label(Selected)+" · "+Sources.Count+" fuentes";
        public string Details => String.Join(Environment.NewLine,Sources.Select(g=>g.SourceLabel+": "+g.Achievements.Count+"/"+g.TotalAchievements+(g.SourceMissing?" · archivo faltante":"")))+Environment.NewLine+"Elegida: "+Selected.SourceLabel+". Automático prioriza desbloqueos, después fechas válidas y archivo directo.";
    }
    internal static class HistoryGroups
    {
        public static bool Compatible(GameEntry a,GameEntry b)
        {
            if(a.Platform!=b.Platform || a.AppId!=b.AppId || (a.AppId==0 && a.ExternalGameId!=b.ExternalGameId))return false;
            uint first=a.AccountId!=0?a.AccountId:a.PAAccountId,second=b.AccountId!=0?b.AccountId:b.PAAccountId;
            if(first!=0 && second!=0 && first!=second)return false;
            if(PlatformMapping.IsMapped(a) && PlatformMapping.IsMapped(b) && a.MappedFromPlatform==b.MappedFromPlatform && !String.IsNullOrEmpty(a.ProfileKey) && !String.IsNullOrEmpty(b.ProfileKey) && a.ProfileKey!=b.ProfileKey)return false;
            if((XboxProfile.IsXbox(a) || PlayniteHistory.IsPA(a)) && (XboxProfile.IsXbox(b) || PlayniteHistory.IsPA(b)) && (a.Platform!="Steam" || a.PAProvider==b.PAProvider) && !String.IsNullOrEmpty(a.ProfileKey) && !String.IsNullOrEmpty(b.ProfileKey) && a.ProfileKey!=b.ProfileKey)return false;
            return true;
        }
        public static bool Same(GameEntry a,GameEntry b)
        {
            if(!Compatible(a,b))return false;
            if(a.StorageKey==b.StorageKey)return true;
            uint accountA=a.AccountId!=0?a.AccountId:a.PAAccountId,accountB=b.AccountId!=0?b.AccountId:b.PAAccountId;
            if(a.SteamTarget && accountA!=0 && accountA==accountB && (String.IsNullOrEmpty(a.HistoryId) || PlayniteHistory.IsPA(a)) && (String.IsNullOrEmpty(b.HistoryId) || PlayniteHistory.IsPA(b)))return true;
            if(!a.SteamTarget && (PlayniteHistory.IsPA(a) || XboxProfile.IsXbox(a)) && (PlayniteHistory.IsPA(b) || XboxProfile.IsXbox(b)) && !String.IsNullOrEmpty(a.ProfileKey) && a.ProfileKey==b.ProfileKey)return true;
            if(PlatformMapping.IsMapped(a) || PlatformMapping.IsMapped(b))return true;
            if(PlayniteHistory.IsPA(a) && PlayniteHistory.IsPA(b) && String.Equals(a.StatsPath,b.StatsPath,StringComparison.OrdinalIgnoreCase) && a.PAGroupKey?.Split('|')[0]==b.PAGroupKey?.Split('|')[0])return true;
            var left=a.Achievements.Where(x=>x.OriginalUnix>0).ToDictionary(x=>x.ApiId,StringComparer.OrdinalIgnoreCase);
            var shared=b.Achievements.Where(x=>x.OriginalUnix>0 && left.ContainsKey(x.ApiId)).ToList();
            return shared.Count>0 && shared.All(x=>left[x.ApiId].OriginalUnix==x.OriginalUnix);
        }
        public static List<HistoryGroup> Build(IEnumerable<GameEntry> entries,IDictionary<string,string> preferences)
        {
            var groups=new List<HistoryGroup>();
            var all=entries.DistinctBy(g=>g.StorageKey).OrderBy(g=>g.StorageKey,StringComparer.Ordinal).ToList();
            bool AmbiguousTarget(GameEntry game)=>game.SteamTarget && all.Where(g=>g.SteamTarget && g.AppId==game.AppId).Select(g=>g.AccountId!=0?g.AccountId:g.PAAccountId).Where(id=>id!=0).Distinct().Skip(1).Any();
            foreach(var game in all)
            {
                var candidates=groups.Where(g=>g.Sources.All(s=>Compatible(s,game)) && g.Sources.Any(s=>Same(s,game) && !((PlatformMapping.IsMapped(s) || PlatformMapping.IsMapped(game)) && AmbiguousTarget(game)))).ToList();
                if(candidates.Count>1 && candidates.Any(a=>candidates.Any(b=>a!=b && a.Sources.Any(x=>b.Sources.Any(y=>!Compatible(x,y))))))candidates.Clear();
                var group=candidates.FirstOrDefault()??new HistoryGroup();
                if(candidates.Count==0)groups.Add(group);
                group.Sources.Add(game);
                foreach(var other in candidates.Skip(1).Where(g=>group.Sources.All(a=>g.Sources.All(b=>Compatible(a,b)))).ToList()){group.Sources.AddRange(other.Sources);groups.Remove(other);}
            }
            foreach(var group in groups)
            {
                var selected=group.Sources.Select(s=>preferences.TryGetValue(s.StorageKey,out var key)?key:null).Where(k=>k!=null && k!="auto").Select(key=>group.Sources.FirstOrDefault(s=>s.StorageKey==key)).FirstOrDefault(g=>g!=null);
                group.Manual=selected!=null;
                group.Selected=selected??group.Sources.OrderByDescending(s=>s.HasLocalHistory && s.HasSnapshot).ThenByDescending(s=>s.Achievements.Select(a=>a.ApiId).Distinct(StringComparer.OrdinalIgnoreCase).Count()).ThenByDescending(s=>s.Achievements.Count(a=>a.OriginalUnix>0)).ThenBy(s=>PlayniteHistory.IsPA(s)).ThenBy(s=>s.StorageKey,StringComparer.Ordinal).First();
            }
            return groups;
        }
        public static Dictionary<string,string> LoadChoices(string path)
        {
            if(!File.Exists(path))return new Dictionary<string,string>();
            if(new FileInfo(path).Length>4*1024*1024)throw new InvalidDataException("Preferencias de fuentes demasiado grandes.");
            return JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(path))??new Dictionary<string,string>();
        }
        public static void SaveChoices(string path,IDictionary<string,string> values)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try{File.WriteAllText(temp,JsonSerializer.Serialize(values));File.Move(temp,path,true);}finally{if(File.Exists(temp))File.Delete(temp);}
        }
    }
}
