using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;

namespace AchievementTimelineReplay
{
    internal static class GameNames
    {
        internal static string StoreName(uint appId,string json)
        {
            using var doc=JsonDocument.Parse(json);
            if(!doc.RootElement.TryGetProperty(appId.ToString(),out var record) || !record.TryGetProperty("success",out var success) || success.ValueKind!=JsonValueKind.True || !record.TryGetProperty("data",out var data) || !data.TryGetProperty("name",out var name) || name.ValueKind!=JsonValueKind.String)return null;
            return LegacyTools.RepairMojibake(name.GetString());
        }
        public static void Apply(GameEntry game,IReadOnlyDictionary<uint,string> names)
        {
            if(game.IsDemo && !String.IsNullOrWhiteSpace(game.DemoGameName))game.GameName=game.DemoGameName+" (muestra "+game.DemoProvider+")";
            else if(names.TryGetValue(game.AppId,out var name) && !String.IsNullOrWhiteSpace(name))game.GameName=name+(game.IsDemo?" (muestra "+game.DemoProvider+")":"");
            else if(game.IsDemo)game.GameName="Muestra "+game.DemoProvider+" · AppID "+game.AppId;
            else if(!String.IsNullOrEmpty(game.HistoryId))game.GameName="AppID "+game.AppId;
        }
        public static void Resolve(IEnumerable<GameEntry> entries,Action<string> log,CancellationToken token)
        {
            var games=entries.ToList();var names=LegacyScanner.LoadNames();
            var missing=games.Where(g=>!names.ContainsKey(g.AppId) && String.IsNullOrWhiteSpace(g.DemoGameName)).Select(g=>g.AppId).Distinct().ToList();
            if(missing.Count>0)
            {
                using var client=new HttpClient{Timeout=TimeSpan.FromSeconds(10)};
                client.DefaultRequestHeaders.UserAgent.ParseAdd("AchievementTimelineReplay/0.8.1");
                bool first=true;
                foreach(uint id in missing)
                {
                    token.ThrowIfCancellationRequested();
                    if(!first && token.WaitHandle.WaitOne(500))token.ThrowIfCancellationRequested();first=false;
                    try
                    {
                        using var request=new HttpRequestMessage(HttpMethod.Get,"https://store.steampowered.com/api/appdetails?appids="+id+"&filters=basic");
                        using var response=client.Send(request,token);
                        if(response.StatusCode==HttpStatusCode.Forbidden || (int)response.StatusCode==429)
                        {log("Nombres públicos: Steam rechazó o limitó la consulta; se conservan AppIDs sin inventar nombres.");break;}
                        response.EnsureSuccessStatusCode();
                        string name=StoreName(id,response.Content.ReadAsStringAsync(token).GetAwaiter().GetResult());
                        if(!String.IsNullOrWhiteSpace(name))names[id]=name;
                        else log("Nombre público no disponible para AppID "+id);
                    }
                    catch(OperationCanceledException){token.ThrowIfCancellationRequested();log("Nombres públicos: tiempo de consulta agotado; se conservan AppIDs.");break;}
                    catch(HttpRequestException ex){log("Nombres públicos no disponibles: "+ex.Message);break;}
                    catch(JsonException ex){log("Nombre inválido para AppID "+id+": "+ex.Message);}
                }
                LegacyScanner.SaveNames(names);
            }
            foreach(var game in games){token.ThrowIfCancellationRequested();Apply(game,names);}
        }
    }
}
