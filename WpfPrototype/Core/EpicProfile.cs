using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AchievementTimelineReplay
{
    internal sealed class EpicServiceException : InvalidOperationException
    {public EpicServiceException(string message):base(message){}}
    internal sealed class EpicSession
    {
        public string AccessToken {get;set;}
        public string RefreshToken {get;set;}
        public string AccountId {get;set;}
        public string DisplayName {get;set;}
    }
    internal sealed class EpicGame
    {public string AppName,Namespace,CatalogId,Title;}
    // Protocol compatibility with Epic's launcher, as used by PlayniteAchievements (MIT).
    // Launcher client credentials below are public application constants, not user secrets.
    internal sealed class EpicProfile : IDisposable
    {
        public const string LoginUrl="https://www.epicgames.com/id/login";
        public const string CodeUrl="https://www.epicgames.com/id/api/redirect?clientId=34a02cf8f4414e29b15921876da36f9a&responseType=code";
        const string ClientAuthorization="MzRhMDJjZjhmNDQxNGUyOWIxNTkyMTg3NmRhMzZmOWE6ZGFhZmJjY2M3Mzc3NDUwMzlkZmZlNTNkOTRmYzc2Y2Y=";
        const string TokenUrl="https://account-public-service-prod03.ol.epicgames.com/account/api/oauth/token";
        const string LibraryUrl="https://library-service.live.use1a.on.epicgames.com/library/api/public/items?includeMetadata=true&platform=Windows";
        const string GraphUrl="https://launcher.store.epicgames.com/graphql";
        const string SchemaQuery=@"query Achievement($SandboxId: String!, $Locale: String!) { Achievement { productAchievementsRecordBySandbox(sandboxId: $SandboxId, locale: $Locale) { productId achievements { achievement { name unlockedDisplayName unlockedDescription } } } } }";
        const string PlayerQuery=@"query playerProfileAchievementsByProductId($EpicAccountId: String!, $ProductId: String!) { PlayerProfile { playerProfile(epicAccountId: $EpicAccountId) { productAchievements(productId: $ProductId) { ... on PlayerProductAchievementsResponseSuccess { data { playerAchievements { playerAchievement { achievementName unlocked unlockDate } } } } } } } }";
        readonly HttpClient http;
        public EpicProfile(HttpMessageHandler handler=null)
        {
            http=handler==null?new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}):new HttpClient(handler);
            http.Timeout=TimeSpan.FromSeconds(35);http.DefaultRequestHeaders.UserAgent.ParseAdd("ATR/0.14.0");
        }
        internal static string Text(JsonElement e,string key)=>e.ValueKind==JsonValueKind.Object && e.TryGetProperty(key,out var v) && v.ValueKind==JsonValueKind.String?v.GetString():null;
        static JsonElement At(JsonElement e,params string[] keys)
        {foreach(string key in keys){if(e.ValueKind!=JsonValueKind.Object || !e.TryGetProperty(key,out e))return default;}return e;}
        static bool ValidId(string value)=>!String.IsNullOrWhiteSpace(value) && value.Length<=512 && !value.Any(Char.IsControl);
        public static string AuthorizationCode(string input)
        {
            input=(input??"").Trim();if(input.Length>8192)throw new InvalidDataException("Pegá únicamente el valor authorizationCode de Epic.");
            if(input.StartsWith("{",StringComparison.Ordinal))
            {try{using var doc=JsonDocument.Parse(input);input=Text(doc.RootElement,"authorizationCode")??"";}catch(JsonException){throw new InvalidDataException("La respuesta de Epic no contiene un authorizationCode válido.");}}
            if(!Regex.IsMatch(input,@"^[A-Za-z0-9_-]{16,512}$"))throw new InvalidDataException("Pegá el valor authorizationCode, no una contraseña, URL ni un token de sesión.");
            return input;
        }
        async Task<JsonDocument> Send(HttpRequestMessage request,CancellationToken token)
        {
            using var response=await http.SendAsync(request,token).ConfigureAwait(false);
            if(response.StatusCode==HttpStatusCode.Unauthorized || response.StatusCode==HttpStatusCode.Forbidden)throw new EpicServiceException("Epic rechazó la sesión o sus permisos. Volvé a conectar la cuenta.");
            if((int)response.StatusCode==429)throw new EpicServiceException("Epic limitó las consultas. Esperá unos minutos antes de volver a escanear.");
            if(!response.IsSuccessStatusCode)throw new InvalidOperationException("La consulta a Epic falló (HTTP "+(int)response.StatusCode+"). Si estabas conectando, obtené un código nuevo.");
            if(response.Content.Headers.ContentLength>16*1024*1024)throw new InvalidDataException("Respuesta Epic demasiado grande.");
            using var stream=await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);using var memory=new MemoryStream();var buffer=new byte[8192];int count;
            while((count=await stream.ReadAsync(buffer,token).ConfigureAwait(false))>0){if(memory.Length+count>16*1024*1024)throw new InvalidDataException("Respuesta Epic demasiado grande.");memory.Write(buffer,0,count);}
            return JsonDocument.Parse(memory.ToArray());
        }
        async Task<EpicSession> Token(Dictionary<string,string> fields,CancellationToken token)
        {
            fields["token_type"]="eg1";using var request=new HttpRequestMessage(HttpMethod.Post,TokenUrl){Content=new FormUrlEncodedContent(fields)};
            request.Headers.Authorization=new AuthenticationHeaderValue("Basic",ClientAuthorization);
            using var doc=await Send(request,token).ConfigureAwait(false);var root=doc.RootElement;
            var session=new EpicSession{AccessToken=Text(root,"access_token"),RefreshToken=Text(root,"refresh_token"),AccountId=Text(root,"account_id"),DisplayName=Text(root,"displayName")};
            if(String.IsNullOrWhiteSpace(session.AccessToken) || String.IsNullOrWhiteSpace(session.RefreshToken) || !Regex.IsMatch(session.AccountId??"",@"^[a-fA-F0-9]{32}$"))throw new InvalidDataException("Epic no devolvió una sesión renovable con una cuenta válida.");return session;
        }
        public Task<EpicSession> LoginAsync(string code,CancellationToken token)=>Token(new Dictionary<string,string>{{"grant_type","authorization_code"},{"code",AuthorizationCode(code)}},token);
        public Task<EpicSession> RefreshAsync(string refresh,CancellationToken token)=>Token(new Dictionary<string,string>{{"grant_type","refresh_token"},{"refresh_token",refresh}},token);
        async Task<JsonDocument> Get(string url,EpicSession session,CancellationToken token)
        {using var request=new HttpRequestMessage(HttpMethod.Get,url);request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",session.AccessToken);return await Send(request,token).ConfigureAwait(false);}
        async Task<JsonDocument> Graph(string query,object variables,EpicSession session,CancellationToken token)
        {
            using var request=new HttpRequestMessage(HttpMethod.Post,GraphUrl){Content=new StringContent(JsonSerializer.Serialize(new{query,variables}),Encoding.UTF8,"application/json")};
            request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",session.AccessToken);request.Headers.AcceptLanguage.ParseAdd("es-ES");
            var doc=await Send(request,token).ConfigureAwait(false);
            if(doc.RootElement.TryGetProperty("errors",out var errors) && errors.ValueKind==JsonValueKind.Array && errors.GetArrayLength()>0)
            {doc.Dispose();throw new InvalidOperationException("Epic devolvió un error al consultar los logros. No se reemplazó el historial guardado por un resultado vacío.");}
            return doc;
        }
        public async Task<List<EpicGame>> GamesAsync(EpicSession session,CancellationToken token)
        {
            var games=new List<EpicGame>();var cursors=new HashSet<string>();string cursor=null;
            for(int page=0;page<1000;page++)
            {
                using var doc=await Get(LibraryUrl+(cursor==null?"":"&cursor="+Uri.EscapeDataString(cursor)),session,token).ConfigureAwait(false);var root=doc.RootElement;
                var records=At(root,"records");if(records.ValueKind!=JsonValueKind.Array)throw new InvalidDataException("Epic no devolvió una biblioteca válida.");
                foreach(var record in records.EnumerateArray())
                {
                    string app=Text(record,"appName"),ns=Text(record,"namespace");if(!ValidId(app) || !ValidId(ns) || app.Length+ns.Length+1>512)continue;
                    games.Add(new EpicGame{AppName=app,Namespace=ns,CatalogId=Text(record,"catalogItemId"),Title=Text(record,"title")??Text(At(record,"metadata"),"title")??Text(record,"displayName")});
                }
                if(games.Count>100000)throw new InvalidDataException("Biblioteca Epic demasiado grande.");
                cursor=Text(At(root,"responseMetadata"),"nextCursor");if(String.IsNullOrEmpty(cursor))return games.DistinctBy(g=>g.Namespace+"|"+g.AppName).ToList();
                if(cursor.Length>8192 || !cursors.Add(cursor))throw new InvalidDataException("Epic devolvió una paginación repetida o inválida.");
            }
            throw new InvalidDataException("Se excedió el límite de páginas de Epic; no se importó una biblioteca parcial.");
        }
        public async Task<GameEntry> HistoryAsync(EpicSession session,EpicGame asset,string directory,Action<string> log,CancellationToken token)
        {
            using var schema=await Graph(SchemaQuery,new{SandboxId=asset.Namespace,Locale="es-ES"},session,token).ConfigureAwait(false);
            var product=At(schema.RootElement,"data","Achievement","productAchievementsRecordBySandbox");
            if(product.ValueKind==JsonValueKind.Null)return null;
            string productId=Text(product,"productId");if(!ValidId(productId))throw new InvalidDataException("Epic no devolvió un producto de logros válido para este título.");
            var definitions=At(product,"achievements");if(definitions.ValueKind!=JsonValueKind.Array)throw new InvalidDataException("Esquema de logros Epic incompleto.");
            using var player=await Graph(PlayerQuery,new{EpicAccountId=session.AccountId,ProductId=productId},session,token).ConfigureAwait(false);
            var progress=At(player.RootElement,"data","PlayerProfile","playerProfile","productAchievements","data","playerAchievements");
            if(progress.ValueKind!=JsonValueKind.Array)throw new InvalidDataException("Epic no devolvió el progreso del perfil. Revisá la cuenta y la privacidad; no se interpretó como cero logros.");
            var game=ParseHistory(session.AccountId,asset,definitions,progress,directory,productId);
            if(game.Achievements.Count>0 && String.IsNullOrWhiteSpace(asset.Title) && ValidId(asset.CatalogId))
            {
                try
                {
                    string url="https://catalog-public-service-prod06.ol.epicgames.com/catalog/api/shared/namespace/"+Uri.EscapeDataString(asset.Namespace)+"/bulk/items?id="+Uri.EscapeDataString(asset.CatalogId)+"&includeDLCDetails=false&includeMainGameDetails=true&country=US&locale=es-ES";
                    using var title=await Get(url,session,token).ConfigureAwait(false);string name=Text(At(title.RootElement,asset.CatalogId),"title");if(!String.IsNullOrWhiteSpace(name))game.GameName=name;
                }
                catch(OperationCanceledException){throw;}
                catch(EpicServiceException){throw;}
                catch(Exception){log?.Invoke("Epic: nombre de catálogo no disponible; se conserva el identificador del juego.");}
            }
            return game;
        }
        public static string ProfileKey(string account)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("Epic|"+account.ToLowerInvariant()))).Substring(0,24);
        public static string Identity(string path,string profile,string external)=>"epic_"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(path).ToUpperInvariant()+"|"+profile+"|"+external))).Substring(0,24);
        public static bool IsEpic(GameEntry game)=>game.HistoryId?.StartsWith("epic_",StringComparison.Ordinal)==true;
        public static bool ShouldInclude(GameEntry game)=>!IsEpic(game) || game.Achievements.Count>0;
        public static GameEntry ParseHistory(string account,EpicGame asset,JsonElement definitions,JsonElement progress,string directory,string productId=null)
        {
            if(!Regex.IsMatch(account??"",@"^[a-fA-F0-9]{32}$") || !ValidId(asset.AppName) || !ValidId(asset.Namespace) || asset.AppName.Length+asset.Namespace.Length+1>512)throw new InvalidDataException("Identidad de cuenta/juego Epic inválida.");
            var names=new Dictionary<string,(string Name,string Description)>(StringComparer.OrdinalIgnoreCase);
            foreach(var wrapper in definitions.EnumerateArray())
            {
                var achievement=At(wrapper,"achievement");string api=Text(achievement,"name");if(!ValidId(api) || !names.TryAdd(api,(Text(achievement,"unlockedDisplayName")??api,Text(achievement,"unlockedDescription")??"")))throw new InvalidDataException("Esquema Epic con códigos ausentes o duplicados.");
            }
            string external=asset.Namespace+"|"+(productId??asset.AppName);
            string profile=ProfileKey(account),file=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(external))).Substring(0,24)+".json";
            string path=Path.GetFullPath(Path.Combine(directory,profile,file));
            var game=new GameEntry{Platform="Epic",ExternalGameId=external,ProfileKey=profile,GameName=String.IsNullOrWhiteSpace(asset.Title)?"Epic · "+asset.AppName:asset.Title,StatsPath=path,HistoryId=Identity(path,profile,external),SourceLabel="Epic Games (perfil)",DetectedOrigin="Epic",HasLocalHistory=true,UsesCurrentSnapshot=true,TotalAchievements=names.Count};
            var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var wrapper in progress.EnumerateArray())
            {
                var achievement=At(wrapper,"playerAchievement");string api=Text(achievement,"achievementName");if(!ValidId(api) || !seen.Add(api))throw new InvalidDataException("Progreso Epic con códigos ausentes o duplicados.");
                if(!achievement.TryGetProperty("unlocked",out var unlocked) || unlocked.ValueKind!=JsonValueKind.True)continue;
                var metadata=names.TryGetValue(api,out var known)?known:(Name:api,Description:"Sin definición en el esquema actual de Epic.");
                game.Achievements.Add(new AchievementRecord{ApiId=api,DisplayName=metadata.Name,Description=metadata.Description,OriginalUnix=PlayniteHistory.Timestamp(Text(achievement,"unlockDate"))});
            }
            game.Achievements=game.Achievements.OrderBy(a=>a.OriginalUnix>0?a.OriginalUnix:Int64.MaxValue).ThenBy(a=>a.ApiId,StringComparer.Ordinal).ToList();game.TotalAchievements=names.Keys.Concat(game.Achievements.Select(a=>a.ApiId)).Distinct(StringComparer.OrdinalIgnoreCase).Count();return game;
        }
        public static void SaveHistory(GameEntry game,string snapshots)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(game.StatsPath));string temp=game.StatsPath+"."+Guid.NewGuid().ToString("N")+".tmp";
            try{File.WriteAllText(temp,JsonSerializer.Serialize(new{Version=1,game.GameName,game.Platform,game.ExternalGameId,game.ProfileKey,Achievements=game.Achievements.Select(a=>new{a.ApiId,a.DisplayName,a.Description,a.OriginalUnix})}));File.Move(temp,game.StatsPath,true);}finally{if(File.Exists(temp))File.Delete(temp);}
            PlayniteHistory.ProtectCurrent(game,snapshots);
        }
        public void Dispose()=>http.Dispose();
    }
}
