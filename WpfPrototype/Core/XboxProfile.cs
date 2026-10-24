using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AchievementTimelineReplay
{
    internal sealed class XboxServiceException : InvalidOperationException
    {public XboxServiceException(string message):base(message){}}
    internal sealed class XboxTitle
    {
        public string Id,Name;
        public bool Legacy;
        public int Total;
    }
    internal sealed class XboxSession
    {
        public string Xuid,UserHash,Token;
    }
    internal sealed class XboxDeviceCode
    {
        public string DeviceCode,UserCode,VerificationUri;
        public int Interval,ExpiresIn;
    }
    internal sealed class XboxOAuthToken
    {
        public string AccessToken,RefreshToken;
    }
    // These calls only authenticate and read the signed-in user's history. No unlock API is used.
    internal sealed class XboxProfile : IDisposable
    {
        readonly HttpClient http;
        const string OAuth="https://login.microsoftonline.com/consumers/oauth2/v2.0/";
        const string Scope="XboxLive.signin offline_access";
        public XboxProfile(HttpMessageHandler handler=null)
        {
            http=handler==null?new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}):new HttpClient(handler);
            http.Timeout=TimeSpan.FromSeconds(35);
            http.DefaultRequestHeaders.UserAgent.ParseAdd("ATR/0.17.1");
        }
        internal static string Text(JsonElement e,string key)
        {
            if(e.ValueKind!=JsonValueKind.Object || !e.TryGetProperty(key,out var v))return null;
            return v.ValueKind==JsonValueKind.String?v.GetString():v.ValueKind==JsonValueKind.Number?v.GetRawText():null;
        }
        static int Number(JsonElement e,string key,int fallback=0)=>Int32.TryParse(Text(e,key),out int n)?n:fallback;
        static bool Yes(JsonElement e,string key)=>e.TryGetProperty(key,out var v) && v.ValueKind==JsonValueKind.True;
        public static string ClientId(string value)=>Guid.TryParse(value,out var id) && id!=Guid.Empty?id.ToString():throw new InvalidDataException("Configurá el Client ID público registrado para ATR en Microsoft.");
        static async Task<JsonDocument> Read(HttpResponseMessage response,CancellationToken token)
        {
            if(response.StatusCode==HttpStatusCode.Unauthorized || response.StatusCode==HttpStatusCode.Forbidden)
                throw new XboxServiceException("Xbox rechazó la sesión o sus permisos. Volvé a conectar la cuenta y revisá la privacidad del perfil.");
            if((int)response.StatusCode==429)throw new XboxServiceException("Xbox limitó las consultas. Esperá unos minutos antes de volver a escanear.");
            if(!response.IsSuccessStatusCode)throw new InvalidOperationException("La consulta a Microsoft/Xbox falló (HTTP "+(int)response.StatusCode+").");
            if(response.Content.Headers.ContentLength>16*1024*1024)throw new InvalidDataException("Respuesta Xbox demasiado grande.");
            using var stream=await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            using var memory=new MemoryStream();var buffer=new byte[8192];int read;
            while((read=await stream.ReadAsync(buffer,token).ConfigureAwait(false))>0)
            {if(memory.Length+read>16*1024*1024)throw new InvalidDataException("Respuesta Xbox demasiado grande.");memory.Write(buffer,0,read);}
            return JsonDocument.Parse(memory.ToArray());
        }
        public async Task<XboxDeviceCode> BeginAsync(string client,CancellationToken token)
        {
            using var response=await http.PostAsync(OAuth+"devicecode",new FormUrlEncodedContent(new Dictionary<string,string>{{"client_id",ClientId(client)},{"scope",Scope}}),token).ConfigureAwait(false);
            using var doc=await Read(response,token).ConfigureAwait(false);var e=doc.RootElement;
            var code=new XboxDeviceCode{DeviceCode=Text(e,"device_code"),UserCode=Text(e,"user_code"),VerificationUri=Text(e,"verification_uri"),Interval=Math.Clamp(Number(e,"interval",5),1,60),ExpiresIn=Math.Clamp(Number(e,"expires_in",900),1,1800)};
            if(String.IsNullOrWhiteSpace(code.DeviceCode) || String.IsNullOrWhiteSpace(code.UserCode) || !Uri.TryCreate(code.VerificationUri,UriKind.Absolute,out var uri) || uri.Scheme!="https" || !(uri.Host=="microsoft.com" || uri.Host.EndsWith(".microsoft.com",StringComparison.OrdinalIgnoreCase) || uri.Host=="login.live.com"))throw new InvalidDataException("Microsoft devolvió un código de acceso inválido.");
            return code;
        }
        static XboxOAuthToken OAuthToken(JsonElement e)
        {
            string access=Text(e,"access_token"),refresh=Text(e,"refresh_token");
            if(String.IsNullOrWhiteSpace(access) || String.IsNullOrWhiteSpace(refresh))throw new InvalidDataException("Microsoft no devolvió una sesión renovable. Revisá los permisos de la aplicación.");
            return new XboxOAuthToken{AccessToken=access,RefreshToken=refresh};
        }
        public async Task<XboxOAuthToken> WaitAsync(string client,XboxDeviceCode code,CancellationToken token)
        {
            var deadline=DateTime.UtcNow.AddSeconds(code.ExpiresIn);int interval=code.Interval;
            while(DateTime.UtcNow<deadline)
            {
                await Task.Delay(TimeSpan.FromSeconds(interval),token).ConfigureAwait(false);
                using var response=await http.PostAsync(OAuth+"token",new FormUrlEncodedContent(new Dictionary<string,string>{{"client_id",ClientId(client)},{"grant_type","urn:ietf:params:oauth:grant-type:device_code"},{"device_code",code.DeviceCode}}),token).ConfigureAwait(false);
                if(response.StatusCode==HttpStatusCode.BadRequest)
                {
                    using var error=JsonDocument.Parse(await response.Content.ReadAsStringAsync(token).ConfigureAwait(false));
                    switch(Text(error.RootElement,"error"))
                    {case "authorization_pending":continue;case "slow_down":interval=Math.Min(interval+5,60);continue;case "authorization_declined":case "access_denied":throw new InvalidOperationException("Inicio de sesión cancelado en Microsoft.");default:throw new InvalidOperationException("El código venció o Microsoft rechazó la configuración de ATR. Generá otro código.");}
                }
                using var doc=await Read(response,token).ConfigureAwait(false);return OAuthToken(doc.RootElement);
            }
            throw new InvalidOperationException("El código de Microsoft venció. Intentá conectar otra vez.");
        }
        public async Task<XboxOAuthToken> RefreshAsync(string client,string refresh,CancellationToken token)
        {
            using var response=await http.PostAsync(OAuth+"token",new FormUrlEncodedContent(new Dictionary<string,string>{{"client_id",ClientId(client)},{"grant_type","refresh_token"},{"refresh_token",refresh},{"scope",Scope}}),token).ConfigureAwait(false);
            if(response.StatusCode==HttpStatusCode.BadRequest)throw new InvalidOperationException("La sesión de Microsoft venció o fue revocada. Volvé a conectar Xbox.");
            using var doc=await Read(response,token).ConfigureAwait(false);
            // Microsoft may rotate the refresh token; never keep using the previous one after success.
            var root=doc.RootElement;
            if(String.IsNullOrEmpty(Text(root,"refresh_token")))
            {string access=Text(root,"access_token");if(String.IsNullOrWhiteSpace(access))throw new InvalidDataException("Sesión Microsoft inválida.");return new XboxOAuthToken{AccessToken=access,RefreshToken=refresh};}
            return OAuthToken(root);
        }
        async Task<JsonDocument> PostJson(string url,object body,CancellationToken token)
        {
            using var content=new StringContent(JsonSerializer.Serialize(body),Encoding.UTF8,"application/json");
            using var response=await http.PostAsync(url,content,token).ConfigureAwait(false);return await Read(response,token).ConfigureAwait(false);
        }
        public async Task<XboxSession> AuthorizeAsync(string access,CancellationToken token)
        {
            using var user=await PostJson("https://user.auth.xboxlive.com/user/authenticate",new{Properties=new{AuthMethod="RPS",SiteName="user.auth.xboxlive.com",RpsTicket="d="+access},RelyingParty="http://auth.xboxlive.com",TokenType="JWT"},token).ConfigureAwait(false);
            string userToken=Text(user.RootElement,"Token");if(String.IsNullOrEmpty(userToken))throw new InvalidDataException("Sesión Xbox Live inválida.");
            using var xsts=await PostJson("https://xsts.auth.xboxlive.com/xsts/authorize",new{Properties=new{SandboxId="RETAIL",UserTokens=new[]{userToken}},RelyingParty="http://xboxlive.com",TokenType="JWT"},token).ConfigureAwait(false);
            var root=xsts.RootElement;
            if(!root.TryGetProperty("DisplayClaims",out var claims) || !claims.TryGetProperty("xui",out var xui) || xui.ValueKind!=JsonValueKind.Array || xui.GetArrayLength()==0)throw new InvalidDataException("La cuenta Microsoft no devolvió un perfil Xbox.");
            var session=new XboxSession{Xuid=Text(xui[0],"xid"),UserHash=Text(xui[0],"uhs"),Token=Text(root,"Token")};
            if(!UInt64.TryParse(session.Xuid,out _) || String.IsNullOrWhiteSpace(session.UserHash) || String.IsNullOrWhiteSpace(session.Token))throw new InvalidDataException("Perfil Xbox inválido.");return session;
        }
        async Task<JsonDocument> Get(string url,string version,XboxSession session,CancellationToken token)
        {
            using var request=new HttpRequestMessage(HttpMethod.Get,url);
            request.Headers.TryAddWithoutValidation("Authorization","XBL3.0 x="+session.UserHash+";"+session.Token);
            request.Headers.Add("x-xbl-contract-version",version);request.Headers.Add("Accept-Language","es-ES");
            using var response=await http.SendAsync(request,token).ConfigureAwait(false);return await Read(response,token).ConfigureAwait(false);
        }
        static string Next(JsonElement root)=>root.TryGetProperty("pagingInfo",out var paging)?Text(paging,"continuationToken"):null;
        async Task<List<JsonElement>> Pages(string url,string property,string version,XboxSession session,CancellationToken token)
        {
            var result=new List<JsonElement>();string next=null;var seen=new HashSet<string>();
            for(int page=0;page<1000;page++)
            {
                using var doc=await Get(url+(next==null?"":"&continuationToken="+Uri.EscapeDataString(next)),version,session,token).ConfigureAwait(false);
                if(!doc.RootElement.TryGetProperty(property,out var array) || array.ValueKind!=JsonValueKind.Array)throw new InvalidDataException("Xbox no devolvió la lista de "+property+" esperada.");
                result.AddRange(array.EnumerateArray().Select(e=>e.Clone()));if(result.Count>100000)throw new InvalidDataException("Historial Xbox demasiado grande.");
                next=Next(doc.RootElement);if(String.IsNullOrEmpty(next))return result;
                if(next.Length>8192 || !seen.Add(next))throw new InvalidDataException("Xbox devolvió una paginación repetida o inválida.");
            }
            throw new InvalidDataException("Se excedió el límite de páginas Xbox; el historial no se importó parcialmente.");
        }
        public async Task<List<XboxTitle>> TitlesAsync(XboxSession session,CancellationToken token)
        {
            var entries=await Pages("https://titlehub.xboxlive.com/users/xuid("+session.Xuid+")/titles/titlehistory/decoration/achievement?maxItems=100","titles","2",session,token).ConfigureAwait(false);
            return entries.Select(ParseTitle).Where(t=>t!=null).DistinctBy(t=>t.Id).ToList();
        }
        internal static XboxTitle ParseTitle(JsonElement e)
        {
            string id=Text(e,"titleId"),name=Text(e,"name");if(!UInt32.TryParse(id,out _) || String.IsNullOrWhiteSpace(name))return null;
            bool legacy=e.TryGetProperty("devices",out var devices) && devices.ValueKind==JsonValueKind.Array && devices.EnumerateArray().Any(d=>d.ValueKind==JsonValueKind.String && d.GetString().Equals("Xbox360",StringComparison.OrdinalIgnoreCase));
            return new XboxTitle{Id=id,Name=name,Legacy=legacy,Total=e.TryGetProperty("achievement",out var ach)?Number(ach,"totalAchievements"):0};
        }
        public async Task<GameEntry> HistoryAsync(XboxSession session,XboxTitle title,string directory,CancellationToken token)
        {
            var entries=await Pages("https://achievements.xboxlive.com/users/xuid("+session.Xuid+")/achievements?titleId="+title.Id+"&maxItems=1000","achievements",title.Legacy?"1":"2",session,token).ConfigureAwait(false);
            return ParseHistory(session.Xuid,title,entries,directory);
        }
        public static string ProfileKey(string xuid)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("Xbox|"+xuid))).Substring(0,24);
        public static string Identity(string path,string profile,string title)=>"xbox_"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(path).ToUpperInvariant()+"|"+profile+"|"+title))).Substring(0,24);
        public static bool ShouldInclude(GameEntry game)=>!IsXbox(game) || game.Achievements.Count>0;
        public static bool IsXbox(GameEntry game)=>game.HistoryId?.StartsWith("xbox_",StringComparison.Ordinal)==true;
        public static GameEntry ParseHistory(string xuid,XboxTitle title,IEnumerable<JsonElement> entries,string directory)
        {
            if(!UInt64.TryParse(xuid,out _) || !UInt32.TryParse(title.Id,out _))throw new InvalidDataException("Identidad Xbox inválida.");
            string profile=ProfileKey(xuid),path=Path.GetFullPath(Path.Combine(directory,profile,title.Id+".json"));
            var game=new GameEntry{Platform="Xbox",ExternalGameId=title.Id,ProfileKey=profile,StatsPath=path,HistoryId=Identity(path,profile,title.Id),GameName=title.Name,SourceLabel="Xbox Live",DetectedOrigin="Xbox",HasLocalHistory=true,UsesCurrentSnapshot=true,TotalAchievements=title.Total};
            var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);int total=0;
            foreach(var e in entries)
            {
                string id=Text(e,"id");if(String.IsNullOrWhiteSpace(id) || id.Length>512 || !seen.Add(id))throw new InvalidDataException("Código de logro Xbox ausente o duplicado.");total++;
                bool unlocked=title.Legacy?Yes(e,"unlocked"):String.Equals(Text(e,"progressState"),"Achieved",StringComparison.OrdinalIgnoreCase);
                if(!unlocked || Yes(e,"isRevoked"))continue;
                string date=title.Legacy?Text(e,"timeUnlocked"):e.TryGetProperty("progression",out var progression)?Text(progression,"timeUnlocked"):null;
                game.Achievements.Add(new AchievementRecord{ApiId=id,DisplayName=Text(e,"name")??id,Description=Text(e,"description")??Text(e,"lockedDescription")??"",OriginalUnix=PlayniteHistory.Timestamp(date)});
            }
            game.TotalAchievements=Math.Max(game.TotalAchievements,total);
            game.Achievements=game.Achievements.OrderBy(a=>a.OriginalUnix>0?a.OriginalUnix:Int64.MaxValue).ThenBy(a=>a.ApiId,StringComparer.Ordinal).ToList();return game;
        }
        public static void SaveHistory(GameEntry game,string snapshots)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(game.StatsPath));
            string temp=game.StatsPath+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {File.WriteAllText(temp,JsonSerializer.Serialize(new{Version=1,game.GameName,game.Platform,game.ExternalGameId,game.ProfileKey,Achievements=game.Achievements.Select(a=>new{a.ApiId,a.DisplayName,a.Description,a.OriginalUnix})}));File.Move(temp,game.StatsPath,true);}
            finally{if(File.Exists(temp))File.Delete(temp);}
            PlayniteHistory.ProtectCurrent(game,snapshots);
        }
        public void Dispose()=>http.Dispose();
    }
}
