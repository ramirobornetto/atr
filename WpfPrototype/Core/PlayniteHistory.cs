using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace AchievementTimelineReplay
{
    internal static class PlayniteHistory
    {
        public static bool IsPA(GameEntry game)=>game.HistoryId?.StartsWith("pa_",StringComparison.Ordinal)==true;
        public static string Identity(string path,string group)=>"pa_"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(path).ToUpperInvariant()+"|"+group))).Substring(0,24);
        public static IEnumerable<string> FindDatabases(string playniteRoot=null)
        {
            var roots=new List<string>{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Playnite","ExtensionsData")};
            if(!String.IsNullOrWhiteSpace(playniteRoot))roots.Add(Path.Combine(playniteRoot,"ExtensionsData"));
            foreach(var root in roots.Distinct(StringComparer.OrdinalIgnoreCase).Where(Directory.Exists))
                foreach(var folder in Directory.EnumerateDirectories(root))
                    if(File.Exists(Path.Combine(folder,"achievement_cache.db")))yield return Path.Combine(folder,"achievement_cache.db");
        }
        static string Text(JsonElement element,string name)=>element.TryGetProperty(name,out var value) && value.ValueKind==JsonValueKind.String?value.GetString():null;
        public static long Timestamp(string text)
        {
            if(!String.IsNullOrWhiteSpace(text) && DateTimeOffset.TryParse(text,CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal,out var date))
            {long unix=date.ToUnixTimeSeconds();if(unix>0 && unix<=253402300799)return unix;}
            return 0;
        }
        public static GameEntry Choose(IEnumerable<GameEntry> games)=>games.OrderByDescending(g=>g.Achievements.Select(a=>a.ApiId).Distinct(StringComparer.OrdinalIgnoreCase).Count()).ThenByDescending(g=>g.Achievements.Count(a=>a.OriginalUnix>0)).ThenByDescending(g=>g.PAProvider=="Manual").ThenBy(g=>g.PAProvider,StringComparer.Ordinal).First();
        public static List<GameEntry> Load(string cachePath,Action<string> log,CancellationToken token=default,bool retainAll=false)
        {
            log??=_=>{};cachePath=Path.GetFullPath(cachePath);
            var customizations=new Dictionary<string,JsonElement>(StringComparer.OrdinalIgnoreCase);
            var links=new Dictionary<string,JsonElement>(StringComparer.OrdinalIgnoreCase);
            string customPath=Path.Combine(Path.GetDirectoryName(cachePath),"game_custom_data.db");
            if(File.Exists(customPath))
            {
                using var custom=new ReadOnlySqlite(customPath);
                foreach(var row in custom.Query("SELECT PlayniteGameId,PayloadJson FROM GameCustomData",token))
                {
                    try{using var doc=JsonDocument.Parse(row["PayloadJson"]);customizations[row["PlayniteGameId"]]=doc.RootElement.Clone();if(doc.RootElement.TryGetProperty("ManualLink",out var link) && link.ValueKind==JsonValueKind.Object)links[row["PlayniteGameId"]]=link.Clone();}
                    catch(JsonException){log("PA: personalización ilegible; no se importó ese vínculo manual.");}
                }
            }
            using var cache=new ReadOnlySqlite(cachePath);
            // Current-user progress only: friends' achievements must never become replay sources.
            var rows=cache.Query(@"SELECT g.PlayniteGameId,g.ProviderKey,g.ProviderGameId,g.ProviderGameKey,g.GameName,u.Id AS UserId,u.ProviderKey AS UserProvider,u.ExternalUserId,
                p.Id AS ProgressId,d.ApiName,d.DisplayName,d.Description,a.Unlocked,a.UnlockTimeUtc
                FROM Games g JOIN UserGameProgress p ON p.GameId=g.Id JOIN Users u ON u.Id=p.UserId
                JOIN AchievementDefinitions d ON d.GameId=g.Id
                LEFT JOIN UserAchievements a ON a.UserGameProgressId=p.Id AND a.AchievementDefinitionId=d.Id
                WHERE (u.IsCurrentUser=1 OR (g.ProviderKey='Local' AND u.ProviderKey='Local' AND u.FriendSource IS NULL)) AND g.ProviderKey IN ('Steam','Manual','Local','Xbox','Epic','GOG','PSN','PlayStation')",token);
            var candidates=new List<GameEntry>();
            foreach(var bucket in rows.GroupBy(r=>r["ProgressId"]))
            {
                token.ThrowIfCancellationRequested();var first=bucket.First();string provider=first["ProviderKey"],guid=first["PlayniteGameId"];
                string platform=provider=="PSN"?"PlayStation":provider=="Steam" || provider=="Manual" || provider=="Local"?"Steam":provider;
                uint.TryParse(first["ProviderGameId"],out uint appId);string externalId=first["ProviderGameKey"]??first["ProviderGameId"];
                if(!Guid.TryParse(guid,out var parsedGuid) || (platform=="Steam" && appId==0) || (platform!="Steam" && String.IsNullOrWhiteSpace(externalId))){log("PA: historial sin AppID Steam o identidad de juego válidos; no se adivinó el destino.");continue;}
                JsonElement manual=default;
                if(provider=="Manual" && (!links.TryGetValue(guid,out manual) || Text(manual,"SourceKey")!="Steam" || Text(manual,"SourceGameId")!=appId.ToString(CultureInfo.InvariantCulture))){log("PA Manual: falta un vínculo Steam válido para este juego; conservá game_custom_data.db junto a la caché.");continue;}
                if(platform!="Steam")appId=0;
                string profile=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(first["UserProvider"]+"|"+(first["ExternalUserId"]??first["UserId"])))).Substring(0,24);
                string group=parsedGuid.ToString("D");
                // Ambiguous multiple current Steam profiles remain separate from manual/local.
                if(rows.Where(r=>r["PlayniteGameId"]==guid && r["ProviderKey"]=="Steam").Select(r=>r["UserId"]).Distinct().Skip(1).Any())group+="|"+first["UserProvider"]+"|"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(first["ExternalUserId"]??first["UserId"]))).Substring(0,24);
                if(platform!="Steam")group+="|game:"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(externalId))).Substring(0,16);
                if(platform!="Steam" && !retainAll)group+="|platform:"+platform;
                if(retainAll)group+="|platform:"+platform+"|source:"+provider+"|"+profile;
                var steamUsers=rows.Where(r=>String.Equals(r["PlayniteGameId"],guid,StringComparison.OrdinalIgnoreCase) && r["ProviderKey"]=="Steam").Select(r=>r["ExternalUserId"]).Distinct().ToList();
                uint steamAccount=0;
                string external=provider=="Steam"?first["ExternalUserId"]:steamUsers.Count==1?steamUsers[0]:null;
                if(UInt64.TryParse(external,out var steam64) && steam64>76561197960265728UL && steam64-76561197960265728UL<=UInt32.MaxValue)steamAccount=(uint)(steam64-76561197960265728UL);
                var game=new GameEntry{AppId=appId,Platform=platform,ExternalGameId=platform=="Steam"?null:externalId,ProfileKey=profile,PAAccountId=steamAccount,AccountId=0,HistoryId=Identity(cachePath,group),PAGroupKey=group,PAProvider=provider,GameName=first["GameName"]??"AppID "+appId,StatsPath=cachePath,SchemaPath=File.Exists(customPath)?customPath:null,SourceLabel="achievement_cache.db (PA / "+provider+")",DetectedOrigin=platform=="Steam"?"PA":platform,HasLocalHistory=true};
                var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach(var row in bucket)
                {
                    string api=row["ApiName"];if(String.IsNullOrWhiteSpace(api) || !seen.Add(api))continue;
                    bool unlocked=row["Unlocked"]=="1";long unix=Timestamp(row["UnlockTimeUtc"]);
                    if(provider=="Manual")
                    {
                        bool hasState=false;unlocked=false;unix=0;
                        if(manual.TryGetProperty("UnlockTimes",out var times) && times.ValueKind==JsonValueKind.Object)
                            foreach(var time in times.EnumerateObject().Where(p=>String.Equals(p.Name.Trim(),api,StringComparison.OrdinalIgnoreCase)))
                            {unix=time.Value.ValueKind==JsonValueKind.String?Timestamp(time.Value.GetString()):0;unlocked=time.Value.ValueKind==JsonValueKind.String;break;}
                        if(manual.TryGetProperty("UnlockStates",out var states) && states.ValueKind==JsonValueKind.Object)
                            foreach(var state in states.EnumerateObject().Where(p=>String.Equals(p.Name.Trim(),api,StringComparison.OrdinalIgnoreCase)))
                            {hasState=true;unlocked=state.Value.ValueKind==JsonValueKind.True;break;}
                        if(!hasState && unix==0)unlocked=false;
                    }
                    if(unlocked)game.Achievements.Add(new AchievementRecord{ApiId=api,DisplayName=String.IsNullOrWhiteSpace(row["DisplayName"])?api:row["DisplayName"],Description=row["Description"]??"",OriginalUnix=unix});
                }
                if(provider=="Manual")
                {
                    var extra=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    if(manual.TryGetProperty("UnlockStates",out var states) && states.ValueKind==JsonValueKind.Object)
                        foreach(var state in states.EnumerateObject())if(state.Value.ValueKind==JsonValueKind.True)extra.Add(state.Name.Trim());
                    if(manual.TryGetProperty("UnlockTimes",out var times) && times.ValueKind==JsonValueKind.Object)
                        foreach(var time in times.EnumerateObject())
                        {
                            bool explicitlyLocked=manual.TryGetProperty("UnlockStates",out var explicitStates) && explicitStates.ValueKind==JsonValueKind.Object && explicitStates.EnumerateObject().Any(p=>String.Equals(p.Name.Trim(),time.Name.Trim(),StringComparison.OrdinalIgnoreCase) && p.Value.ValueKind==JsonValueKind.False);
                            if(!explicitlyLocked && time.Value.ValueKind==JsonValueKind.String && Timestamp(time.Value.GetString())>0)extra.Add(time.Name.Trim());
                        }
                    foreach(string api in extra.Where(id=>!String.IsNullOrWhiteSpace(id) && !seen.Contains(id)))
                    {
                        long unix=0;
                        if(manual.TryGetProperty("UnlockTimes",out var extraTimes) && extraTimes.ValueKind==JsonValueKind.Object)
                            foreach(var time in extraTimes.EnumerateObject().Where(p=>String.Equals(p.Name.Trim(),api,StringComparison.OrdinalIgnoreCase)))if(time.Value.ValueKind==JsonValueKind.String){unix=Timestamp(time.Value.GetString());break;}
                        game.Achievements.Add(new AchievementRecord{ApiId=api,DisplayName=api,Description="Sin definición en la caché PA.",OriginalUnix=unix});seen.Add(api);
                    }
                }
                if(customizations.TryGetValue(guid,out var customization) && customization.TryGetProperty("AchievementOverrides",out var overrides) && overrides.ValueKind==JsonValueKind.Object)
                    foreach(var achievement in game.Achievements)
                        foreach(var entry in overrides.EnumerateObject().Where(p=>String.Equals(p.Name.Trim(),achievement.ApiId,StringComparison.OrdinalIgnoreCase) && p.Value.ValueKind==JsonValueKind.Object))
                        {
                            var value=entry.Value;string name=Text(value,"DisplayName"),description=Text(value,"Description");
                            if(!String.IsNullOrWhiteSpace(name))achievement.DisplayName=name;
                            if(value.TryGetProperty("ClearDescription",out var clearDescription) && clearDescription.ValueKind==JsonValueKind.True)achievement.Description="";
                            else if(description!=null)achievement.Description=description;
                            if(value.TryGetProperty("ClearUnlockTime",out var clearTime) && clearTime.ValueKind==JsonValueKind.True)achievement.OriginalUnix=0;
                            else if(value.TryGetProperty("UnlockTimeUtc",out var date) && date.ValueKind==JsonValueKind.String)achievement.OriginalUnix=Timestamp(date.GetString());
                            break;
                        }
                game.TotalAchievements=seen.Count;
                game.Achievements=game.Achievements.OrderBy(a=>a.OriginalUnix>0?a.OriginalUnix:Int64.MaxValue).ThenBy(a=>a.ApiId,StringComparer.Ordinal).ToList();
                game.IsDemo=game.Achievements.Count>0 && game.Achievements.All(a=>a.ApiId.StartsWith("ATR_DEMO_",StringComparison.Ordinal));
                if(game.Achievements.Count>0 || retainAll)candidates.Add(game);
            }
            if(retainAll)return candidates.Where(g=>g.Achievements.Count>0 || candidates.Any(other=>other.Achievements.Count>0 && HistoryGroups.Compatible(g,other) && g.PAGroupKey.Split('|')[0]==other.PAGroupKey.Split('|')[0])).ToList();
            var result=new List<GameEntry>();
            foreach(var group in candidates.GroupBy(g=>g.StorageKey))
            {
                var winner=Choose(group);result.Add(winner);
                log("PA · "+winner.GameName+": "+winner.Achievements.Count+"/"+winner.TotalAchievements+" · elegido "+winner.PAProvider+" · "+winner.Achievements.Count(a=>a.OriginalUnix==0)+" sin fecha válida (conservados).");
            }
            return result;
        }
        public static bool SameHistory(GameEntry direct,GameEntry pa)
        {
            if(!IsPA(pa) || IsPA(direct) || !direct.HasLocalHistory || direct.AppId!=pa.AppId)return false;
            if(String.IsNullOrEmpty(direct.HistoryId) && direct.AccountId!=0 && pa.PAAccountId!=0 && direct.AccountId!=pa.PAAccountId)return false;
            var dates=direct.Achievements.Where(a=>a.OriginalUnix>0).ToDictionary(a=>a.ApiId,StringComparer.OrdinalIgnoreCase);
            var overlap=pa.Achievements.Where(a=>a.OriginalUnix>0 && dates.ContainsKey(a.ApiId)).ToList();
            // AppID alone cannot identify an account/profile. Require shared historical evidence.
            return overlap.Count>0 && overlap.All(a=>dates[a.ApiId].OriginalUnix==a.OriginalUnix);
        }
        public static List<GameEntry> Deduplicate(List<GameEntry> entries,Action<string> log)
        {
            var result=new List<GameEntry>(entries);
            foreach(var pa in entries.Where(IsPA))
            {
                var matches=result.Where(g=>SameHistory(g,pa)).ToList();
                if(matches.Count!=1 || entries.Count(g=>IsPA(g) && SameHistory(matches[0],g))!=1)continue;
                var direct=matches[0];var winner=direct.Achievements.Count>=pa.Achievements.Count?direct:pa;
                if(direct.Achievements.Count==pa.Achievements.Count && pa.Achievements.Count(a=>a.OriginalUnix>0)>direct.Achievements.Count(a=>a.OriginalUnix>0))winner=pa;
                var loser=ReferenceEquals(winner,pa)?direct:pa;winner.AlternatePlanKey=loser.StorageKey;
                if(ReferenceEquals(winner,pa)){winner.PAOrigin=direct.DetectedOrigin;winner.DetectedOrigin=direct.DetectedOrigin;winner.OriginEvidence="Coincide con el historial directo por AppID, IDs y fechas; se eligió PA por cantidad de desbloqueos.";}
                result.Remove(loser);log?.Invoke("Historial coincidente · "+winner.GameName+": se eligió "+winner.SourceLabel+" ("+winner.Achievements.Count+" desbloqueados). Los planes de la otra fuente se conservan.");
            }
            return result;
        }
        public static void ProtectCurrent(GameEntry game,string snapshots)
        {
            string folder=Path.Combine(snapshots,game.StorageKey);Directory.CreateDirectory(folder);
            string original=Path.Combine(folder,"snapshot.json"),current=Path.Combine(folder,IsPA(game)?"pa-current.json":"source-current.json");
            foreach(string existing in new[]{original,current}.Where(File.Exists))
            {
                using var doc=JsonDocument.Parse(File.ReadAllText(existing));var root=doc.RootElement;
                if(root.GetProperty("AppId").GetUInt32()!=game.AppId || (root.TryGetProperty("HistoryId",out var identity)?identity.GetString():null)!=game.HistoryId || (String.IsNullOrEmpty(game.HistoryId) && root.GetProperty("AccountId").GetUInt32()!=game.AccountId))throw new InvalidDataException("El snapshot actual pertenece a otro historial. No se sobrescribió.");
                if(root.TryGetProperty("IsDemo",out var demo) && demo.ValueKind==JsonValueKind.True)game.IsDemo=true;
            }
            var data=new HistorySnapshot{AppId=game.AppId,AccountId=game.AccountId,HistoryId=game.HistoryId,IsDemo=game.IsDemo,Achievements=game.Achievements.Select(a=>new SnapshotAchievement{ApiId=a.ApiId,DisplayName=a.DisplayName,Description=a.Description,OriginalUnix=a.OriginalUnix}).ToList()};
            string json=JsonSerializer.Serialize(data);
            if(!File.Exists(original))File.WriteAllText(original,json);
            string temp=current+"."+Guid.NewGuid().ToString("N")+".tmp";
            try{File.WriteAllText(temp,json);File.Move(temp,current,true);}finally{if(File.Exists(temp))File.Delete(temp);}
            game.HasSnapshot=true;
        }
    }
}
