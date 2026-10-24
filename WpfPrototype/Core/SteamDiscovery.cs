using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AchievementTimelineReplay
{
    internal sealed class SteamGameMatch
    {
        public uint AppId {get;set;}
        public string Name {get;set;}
        public string Type {get;set;}
        public string Origin {get;set;}
        public override string ToString()=>Name+" · "+(Type=="dlc"?"DLC · ":"")+"AppID "+AppId+" · "+Origin;
    }
    internal sealed class PublicAchievement
    {
        public string IconKey;
        public string Name;
        public string Description;
    }
    internal static class SteamDiscovery
    {
        public static string IconKey(string value)=>Regex.Match(value??"",@"(?<![a-fA-F0-9])[a-fA-F0-9]{40}(?![a-fA-F0-9])").Value.ToLowerInvariant();
        public static List<SteamGameMatch> ParseSearch(string json)
        {
            using var doc=JsonDocument.Parse(json);var result=new List<SteamGameMatch>();
            if(!doc.RootElement.TryGetProperty("items",out var items) || items.ValueKind!=JsonValueKind.Array)return result;
            foreach(var item in items.EnumerateArray())
            {
                if(!item.TryGetProperty("id",out var id) || (id.ValueKind!=JsonValueKind.Number || !id.TryGetUInt32(out uint appId)) || appId==0 || !item.TryGetProperty("name",out var name) || name.ValueKind!=JsonValueKind.String || String.IsNullOrWhiteSpace(name.GetString()))continue;
                string type=item.TryGetProperty("type",out var kind) && kind.ValueKind==JsonValueKind.String?kind.GetString():"app";
                if(type!="app" && type!="game" && type!="dlc")continue;
                result.Add(new SteamGameMatch{AppId=appId,Name=name.GetString(),Type=type,Origin="Tienda Steam"});
            }
            return result.DistinctBy(g=>g.AppId).ToList();
        }
        public static List<SteamGameMatch> LocalGames(IEnumerable<string> databases,IEnumerable<GameEntry> histories,string term)
        {
            var games=histories.Where(g=>g.SteamTarget).Select(g=>new SteamGameMatch{AppId=g.AppId,Name=g.GameName,Origin="Biblioteca ATR"}).ToList();
            foreach(string path in databases.Distinct(StringComparer.OrdinalIgnoreCase).Where(File.Exists))
                try
                {
                    using var db=new ReadOnlySqlite(path);
                    foreach(var row in db.Query("SELECT ProviderGameId,GameName FROM Games WHERE ProviderKey IN ('Steam','Local')"))
                        if(UInt32.TryParse(row["ProviderGameId"],out uint id) && id>0 && !String.IsNullOrWhiteSpace(row["GameName"]))games.Add(new SteamGameMatch{AppId=id,Name=row["GameName"],Origin="Caché Playnite Achievements"});
                }
                catch(InvalidDataException){}
            try{games.AddRange(LegacyScanner.LoadNames().Where(p=>p.Value.Contains(term,StringComparison.OrdinalIgnoreCase)).Take(40).Select(p=>new SteamGameMatch{AppId=p.Key,Name=p.Value,Origin="Caché nombres Steam"}));}catch(IOException){}
            return games.Where(g=>g.Name?.Contains(term,StringComparison.OrdinalIgnoreCase)==true).DistinctBy(g=>g.AppId).Take(40).ToList();
        }
        static HttpClient Client(HttpMessageHandler handler=null)
        {
            var client=handler==null?new HttpClient():new HttpClient(handler,false);client.Timeout=TimeSpan.FromSeconds(12);client.DefaultRequestHeaders.UserAgent.ParseAdd("AchievementTimelineReplay/0.16.0");return client;
        }
        public static async Task<List<SteamGameMatch>> SearchAsync(string term,CancellationToken token=default,HttpMessageHandler handler=null)
        {
            if(String.IsNullOrWhiteSpace(term) || term.Length>200)return new List<SteamGameMatch>();
            using var client=Client(handler);
            return ParseSearch(await client.GetStringAsync("https://store.steampowered.com/api/storesearch/?term="+Uri.EscapeDataString(term.Trim())+"&l=spanish&cc=us",token));
        }
        public static List<PublicAchievement> ParseAchievements(string html)
        {
            if(html.Length>4*1024*1024)throw new InvalidDataException("Respuesta pública de logros demasiado grande.");
            string Text(string value)=>Regex.Replace(WebUtility.HtmlDecode(Regex.Replace(value??"",@"<[^>]+>"," ")),@"\s+"," ").Trim();
            var result=new List<PublicAchievement>();
            foreach(string block in Regex.Split(html,@"<div\b[^>]*class\s*=\s*[""'][^""']*\bachieveRow\b[^""']*[""'][^>]*>",RegexOptions.IgnoreCase).Skip(1))
            {
                string image=Regex.Match(block,@"<img\b[^>]*src\s*=\s*[""']([^""']+)[""']",RegexOptions.IgnoreCase).Groups[1].Value;
                string name=Text(Regex.Match(block,@"<h3\b[^>]*>(.*?)</h3>",RegexOptions.IgnoreCase|RegexOptions.Singleline).Groups[1].Value);
                string description=Text(Regex.Match(block,@"<h5\b[^>]*>(.*?)</h5>",RegexOptions.IgnoreCase|RegexOptions.Singleline).Groups[1].Value);
                string key=IconKey(image);if(key.Length>0 && name.Length>0)result.Add(new PublicAchievement{IconKey=key,Name=name,Description=description});
            }
            return result;
        }
        public static int ApplyLocalization(List<SteamDefinition> definitions,List<PublicAchievement> english,List<PublicAchievement> localized)
        {
            string Norm(string text)=>new string((text??"").Normalize(System.Text.NormalizationForm.FormD).Where(Char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
            var localizedKeys=localized.GroupBy(a=>a.IconKey).Where(g=>g.Count()==1).ToDictionary(g=>g.Key,g=>g.Single());int applied=0;
            foreach(var definition in definitions)
            {
                PublicAchievement translated=null;string key=IconKey(definition.Icon);
                if(key.Length>0)localizedKeys.TryGetValue(key,out translated);
                if(translated==null)
                {
                    var matches=english.Where(a=>Norm(a.Name)==Norm(definition.Name) && (String.IsNullOrWhiteSpace(definition.Description) || Norm(a.Description)==Norm(definition.Description))).ToList();
                    if(matches.Count==1 && english.Count(a=>a.IconKey==matches[0].IconKey)==1)localizedKeys.TryGetValue(matches[0].IconKey,out translated);
                }
                if(translated==null)
                {
                    var ownLanguage=localized.Where(a=>Norm(a.Name)==Norm(definition.Name) && (String.IsNullOrWhiteSpace(definition.Description) || Norm(a.Description)==Norm(definition.Description))).ToList();
                    if(ownLanguage.Count==1)translated=ownLanguage[0];
                }
                if(translated==null)continue;
                var original=english.Where(a=>a.IconKey==translated.IconKey).ToList();
                if(original.Count==1 && !definition.Aliases.Any(a=>a.Name==original[0].Name && a.Description==original[0].Description))definition.Aliases.Add(new SteamText{Name=original[0].Name,Description=original[0].Description});
                definition.Name=translated.Name;definition.Description=translated.Description;applied++;
            }
            return applied;
        }
        public static async Task<int> LocalizeAsync(uint appId,List<SteamDefinition> definitions,string language,string cacheDirectory,CancellationToken token=default,HttpMessageHandler handler=null)
        {
            if(language!="spanish" && language!="english")throw new ArgumentOutOfRangeException(nameof(language));
            using var client=Client(handler);Directory.CreateDirectory(cacheDirectory);
            async Task<List<PublicAchievement>> Read(string lang)
            {
                string path=Path.Combine(cacheDirectory,appId+"-"+lang+".json");
                if(File.Exists(path) && DateTime.UtcNow-File.GetLastWriteTimeUtc(path)<TimeSpan.FromDays(7))
                    try{var saved=JsonSerializer.Deserialize<List<SteamText>>(File.ReadAllText(path));if(saved!=null && saved.Count>0)return saved.Select(s=>new PublicAchievement{IconKey=s.IconKey,Name=s.Name,Description=s.Description}).ToList();}catch(JsonException){}
                string html=await client.GetStringAsync("https://steamcommunity.com/stats/"+appId+"/achievements/?l="+lang,token);
                var rows=ParseAchievements(html);if(rows.Count==0)throw new InvalidDataException("Steam no publicó textos utilizables para ese idioma.");
                string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
                try{File.WriteAllText(temp,JsonSerializer.Serialize(rows.Select(r=>new SteamText{Name=r.Name,Description=r.Description,IconKey=r.IconKey})));File.Move(temp,path,true);}finally{if(File.Exists(temp))File.Delete(temp);}
                return rows;
            }
            var localized=await Read(language);var english=await Read(language=="english"?"spanish":"english");return ApplyLocalization(definitions,english,localized);
        }
    }
}
