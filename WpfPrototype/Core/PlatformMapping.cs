using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AchievementTimelineReplay
{
    internal sealed class SteamText
    {
        public string Name {get;set;}
        public string Description {get;set;}
        public string IconKey {get;set;}
    }
    internal sealed class SteamDefinition
    {
        public string ApiId {get;set;}
        public string Icon {get;set;}
        public List<SteamText> Aliases {get;set;}=new List<SteamText>();
        public string Name {get;set;}
        public string Description {get;set;}
        public override string ToString()=>String.IsNullOrEmpty(ApiId)?Name:Name+" ["+ApiId+"]";
    }
    internal sealed class AchievementMapping
    {
        public string SourceId {get;set;}
        public string TargetId {get;set;}
        public bool Confirmed {get;set;}
        public bool NoEquivalent {get;set;}
    }
    internal sealed class MappingFile
    {
        public int Version {get;set;}=1;
        public string SourceKey {get;set;}
        public string SourcePlatform {get;set;}
        public string ExternalGameId {get;set;}
        public uint TargetAppId {get;set;}
        public List<AchievementMapping> Entries {get;set;}=new List<AchievementMapping>();
    }
    internal static class PlatformMapping
    {
        public static string SourceName(string sourceKey,string platform)=>sourceKey?.StartsWith("xbox_",StringComparison.Ordinal)==true?"Xbox Live":sourceKey?.StartsWith("epic_",StringComparison.Ordinal)==true?"Epic Games":"Playnite Achievements / "+platform;
        public static HashSet<string> UniqueTargets(IEnumerable<string> targets)=>targets.Where(id=>!String.IsNullOrWhiteSpace(id)).GroupBy(id=>id,StringComparer.OrdinalIgnoreCase).Where(g=>g.Count()==1).Select(g=>g.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        public static bool IsMapped(GameEntry game)=>game.HistoryId?.StartsWith("mapped_",StringComparison.Ordinal)==true;
        public static string Identity(string path,string sourceKey,uint appId)=>"mapped_"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(path).ToUpperInvariant()+"|"+sourceKey+"|"+appId))).Substring(0,24);
        static string Normalize(string text)=>new string((text??"").Normalize(NormalizationForm.FormD).Where(c=>Char.IsLetterOrDigit(c)).ToArray()).ToUpperInvariant();
        public static SteamDefinition Suggest(AchievementRecord source,IEnumerable<SteamDefinition> targets)
        {
            return AchievementMatching.Suggest(source,targets).Target;
        }
        public static List<SteamDefinition> FromSchema(string path,uint appId)
        {
            var root=BinaryKvReader.ReadFile(path);
            if(!root.TryGetValue(appId.ToString(CultureInfo.InvariantCulture),out var raw) || raw is not Dictionary<string,object> app || !app.TryGetValue("stats",out var stats) || stats is not Dictionary<string,object> groups)throw new InvalidDataException(UiText.Text("El esquema no corresponde al AppID elegido."));
            var result=new Dictionary<string,SteamDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach(var group in groups.Values.OfType<Dictionary<string,object>>())
            {
                if(!group.TryGetValue("type",out var type) || !String.Equals(Convert.ToString(type),"ACHIEVEMENTS",StringComparison.OrdinalIgnoreCase) || !group.TryGetValue("bits",out var bits) || bits is not Dictionary<string,object> entries)continue;
                foreach(var bit in entries.Values.OfType<Dictionary<string,object>>())
                {
                    if(!bit.TryGetValue("name",out var id) || id is not string api || String.IsNullOrWhiteSpace(api))continue;
                    string Local(string key,string fallback)
                    {
                        if(!bit.TryGetValue("display",out var display) || display is not Dictionary<string,object> fields || !fields.TryGetValue(key,out var item) || item is not Dictionary<string,object> languages)return fallback;
                        foreach(string lang in new[]{"latam","spanish","english"})if(languages.TryGetValue(lang,out var name) && name is string text && !String.IsNullOrWhiteSpace(text))return text;
                        return fallback;
                    }
                    string icon=bit.TryGetValue("display",out var rawDisplay) && rawDisplay is Dictionary<string,object> displayFields && displayFields.TryGetValue("icon",out var rawIcon)?Convert.ToString(rawIcon):null;
                    result[api]=new SteamDefinition{ApiId=api,Icon=icon,Name=Local("name",api),Description=Local("desc","")};
                }
            }
            return result.Values.ToList();
        }
        public static List<SteamDefinition> FromPA(string path,uint appId)
        {
            using var db=new ReadOnlySqlite(path);
            return db.Query("SELECT d.ApiName,d.DisplayName,d.Description,d.UnlockedIconPath FROM AchievementDefinitions d JOIN Games g ON g.Id=d.GameId WHERE g.ProviderKey IN ('Steam','Local') AND g.ProviderGameId="+appId.ToString(CultureInfo.InvariantCulture)).Where(r=>!String.IsNullOrWhiteSpace(r["ApiName"])).DistinctBy(r=>r["ApiName"],StringComparer.OrdinalIgnoreCase).Select(r=>new SteamDefinition{ApiId=r["ApiName"],Icon=r["UnlockedIconPath"],Name=r["DisplayName"]??r["ApiName"],Description=r["Description"]??""}).ToList();
        }
        public static List<SteamDefinition> FromSam(string text)
        {
            using var doc=JsonDocument.Parse(text);if(doc.RootElement.ValueKind!=JsonValueKind.Array)throw new InvalidDataException(UiText.Text("SAM no devolvió una lista de logros."));
            var result=new List<SteamDefinition>();
            foreach(var entry in doc.RootElement.EnumerateArray())
            {
                string Get(string field)=>entry.TryGetProperty(field,out var value) && value.ValueKind==JsonValueKind.String?value.GetString():null;
                string id=Get("id");if(!String.IsNullOrWhiteSpace(id))result.Add(new SteamDefinition{ApiId=id,Icon=Get("icon")??Get("icon_normal")??Get("icon_unlocked"),Name=Get("display_name")??Get("name")??id,Description=Get("description")??""});
            }
            return result.DistinctBy(d=>d.ApiId,StringComparer.OrdinalIgnoreCase).ToList();
        }
        public static string FilePath(string directory,GameEntry source,uint appId)=>Path.Combine(directory,Identity(source.StatsPath,source.StorageKey,appId)+".json");
        public static MappingFile Capture(GameEntry source,uint appId,IEnumerable<AchievementMapping> entries)=>new MappingFile{SourceKey=source.StorageKey,SourcePlatform=source.Platform,ExternalGameId=source.ExternalGameId,TargetAppId=appId,Entries=entries.ToList()};
        static void Validate(MappingFile file,GameEntry source,uint target)
        {
            if(source.SteamTarget || (!PlayniteHistory.IsPA(source) && !XboxProfile.IsXbox(source) && !EpicProfile.IsEpic(source)) || source.AppId!=0)throw new InvalidDataException(UiText.Text("Las correspondencias requieren un historial Playnite Achievements externo o una consulta de perfil Xbox/Epic."));
            if(file==null || file.Version!=1 || target==0 || file.TargetAppId!=target || file.SourceKey!=source.StorageKey || file.SourcePlatform!=source.Platform || file.ExternalGameId!=source.ExternalGameId || file.Entries==null || file.Entries.Count>50000 || file.Entries.Any(e=>e==null || String.IsNullOrWhiteSpace(e.SourceId) || e.SourceId.Length>512 || (e.TargetId?.Length??0)>512 || (e.NoEquivalent && (e.Confirmed || !String.IsNullOrEmpty(e.TargetId)))) || file.Entries.Select(e=>e.SourceId).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=file.Entries.Count)throw new InvalidDataException(UiText.Text("Correspondencias de otra fuente o formato inválido."));
        }
        public static void Save(string directory,GameEntry source,MappingFile file)
        {
            Validate(file,source,file.TargetAppId);Directory.CreateDirectory(directory);string path=FilePath(directory,source,file.TargetAppId),temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try{File.WriteAllText(temp,JsonSerializer.Serialize(file,new JsonSerializerOptions{WriteIndented=true}));File.Move(temp,path,true);}finally{if(File.Exists(temp))File.Delete(temp);}
        }
        public static MappingFile Load(string directory,GameEntry source,uint target)
        {
            string path=FilePath(directory,source,target);if(!File.Exists(path))return null;
            if(new FileInfo(path).Length>16*1024*1024)throw new InvalidDataException(UiText.Text("Correspondencias demasiado grandes."));
            var file=JsonSerializer.Deserialize<MappingFile>(File.ReadAllText(path));Validate(file,source,target);return file;
        }
        public static GameEntry Build(GameEntry source,MappingFile file,IEnumerable<SteamDefinition> targets,string targetName)
        {
            Validate(file,source,file.TargetAppId);
            var definitions=targets.ToDictionary(d=>d.ApiId,StringComparer.OrdinalIgnoreCase);var unlocked=source.Achievements.ToDictionary(a=>a.ApiId,StringComparer.OrdinalIgnoreCase);
            var chosen=file.Entries.Where(e=>e.Confirmed).ToList();
            if(chosen.Count==0 || chosen.Any(e=>e.TargetId==null || !definitions.ContainsKey(e.TargetId) || !unlocked.ContainsKey(e.SourceId)) || chosen.Select(e=>e.TargetId).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=chosen.Count)throw new InvalidDataException(UiText.Text("Confirmá al menos una correspondencia válida, sin repetir el mismo logro de Steam."));
            var game=new GameEntry{AppId=file.TargetAppId,Platform="Steam",MappedFromPlatform=source.Platform,MappedSourceKey=source.StorageKey,HistoryId=Identity(source.StatsPath,source.StorageKey,file.TargetAppId),GameName=targetName,StatsPath=source.StatsPath,SchemaPath=source.SchemaPath,SourceLabel=SourceName(source.StorageKey,source.Platform)+" → Steam",DetectedOrigin=source.Platform,HasLocalHistory=true,IsDemo=source.IsDemo,UsesCurrentSnapshot=true,ProfileKey=source.ProfileKey,TotalAchievements=definitions.Count};
            foreach(var entry in chosen){var record=unlocked[entry.SourceId].Clone();var target=definitions[entry.TargetId];record.ApiId=target.ApiId;record.DisplayName=target.Name;record.Description=target.Description;record.SteamState="—";game.Achievements.Add(record);}
            return game;
        }
    }
}
