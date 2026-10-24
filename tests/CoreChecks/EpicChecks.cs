using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AchievementTimelineReplay;

static class EpicChecks
{
    const string Account="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    const string Definitions="[{\"achievement\":{\"name\":\"FIRST\",\"unlockedDisplayName\":\"Primer paso\",\"unlockedDescription\":\"Completa el tutorial\"}},{\"achievement\":{\"name\":\"LOCKED\",\"unlockedDisplayName\":\"Final\"}}]";
    const string Progress="[{\"playerAchievement\":{\"achievementName\":\"FIRST\",\"unlocked\":true,\"unlockDate\":null}},{\"playerAchievement\":{\"achievementName\":\"LOCKED\",\"unlocked\":false,\"unlockDate\":\"2026-01-01T00:00:00Z\"}}]";
    static JsonElement J(string json){using var doc=JsonDocument.Parse(json);return doc.RootElement.Clone();}
    static void Check(bool condition,string text){if(!condition)throw new Exception(text);Console.WriteLine("PASS: Epic — "+text);}
    sealed class Service : HttpMessageHandler
    {
        public bool Repeat,Unauthorized,GraphErrors,NullProduct,MissingProgress,SwitchAccount,Headers=true;
        public int UnauthorizedRemaining;public bool DenyCatalog;public string DeniedSandbox,AllowedAccess="TEST_ACCESS_EPIC";
        public List<string> Urls=new List<string>();public List<string> Bodies=new List<string>();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();string url=request.RequestUri.AbsoluteUri;Urls.Add(url);string body=request.Content==null?"":await request.Content.ReadAsStringAsync(token);Bodies.Add(body);string json;
            if(Unauthorized || (!url.Contains("/oauth/token") && UnauthorizedRemaining-->0))return new HttpResponseMessage(HttpStatusCode.Unauthorized){Content=new StringContent("PRIVATE_RESPONSE_MUST_NOT_LEAK")};
            if(url.Contains("/oauth/token"))
            {
                Headers &= request.Headers.Authorization?.Scheme=="Basic";
                json="{\"access_token\":\"TEST_ACCESS_EPIC\",\"refresh_token\":\"TEST_REFRESH_EPIC\",\"account_id\":\""+(SwitchAccount?new string('b',32):Account)+"\",\"displayName\":\"Fixture user\"}";
            }
            else
            {
                Headers &= String.Equals(request.Headers.Authorization?.Scheme,"Bearer",StringComparison.OrdinalIgnoreCase) && request.Headers.Authorization?.Parameter==AllowedAccess;
                Headers &= request.Headers.UserAgent.ToString().Contains("EpicGamesLauncher") && request.Headers.Accept.Any(a=>a.MediaType=="application/json");
                if(url.Contains("/library/"))
                {
                    bool second=url.Contains("cursor=");
                    json=second && !Repeat?"{\"records\":[{\"appName\":\"other-app\",\"namespace\":\"other-sandbox\"}],\"responseMetadata\":{}}":"{\"records\":[{\"appName\":\"main-app\",\"namespace\":\"sandbox\",\"title\":\"Fixture game\"}],\"responseMetadata\":{\"nextCursor\":\"A+B /?\"}}";
                }
                else if(url.Contains("/graphql"))
                {
                    using var doc=JsonDocument.Parse(body);
                    if(DeniedSandbox!=null && doc.RootElement.GetProperty("variables").TryGetProperty("SandboxId",out var sandbox) && sandbox.GetString()==DeniedSandbox)return new HttpResponseMessage(HttpStatusCode.Forbidden){Content=new StringContent("{\"errorCode\":\"errors.com.epicgames.access.denied\",\"message\":\"PRIVATE_DETAIL\"}")};
                    string query=doc.RootElement.GetProperty("query").GetString();
                    if(GraphErrors)json="{\"errors\":[{\"message\":\"PRIVATE_GRAPHQL_DETAIL\"}],\"data\":{}}";
                    else if(query.Contains("productAchievementsRecordBySandbox"))json="{\"data\":{\"Achievement\":{\"productAchievementsRecordBySandbox\":"+(NullProduct?"null":"{\"productId\":\"product\",\"achievements\":"+Definitions+"}")+"}}}";
                    else json="{\"data\":{\"PlayerProfile\":{\"playerProfile\":{\"productAchievements\":{\"data\":{\"playerAchievements\":"+(MissingProgress?"null":Progress)+"}}}}}}";
                }
                else if(DenyCatalog)return new HttpResponseMessage(HttpStatusCode.Forbidden){Content=new StringContent("{}")};
                else json="{\"catalog-id\":{\"title\":\"Catalog game name\"}}";
            }
            return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(json)};
        }
    }
    public static void Run(string root)=>RunAsync(root).GetAwaiter().GetResult();
    static async Task RunAsync(string root)
    {
        string dir=Path.Combine(root,"epic-checks"),snapshots=Path.Combine(dir,"Snapshots");var asset=new EpicGame{AppName="main-app",Namespace="sandbox",Title="Fixture game"};
        var game=EpicProfile.ParseHistory(Account,asset,J(Definitions),J(Progress),dir);
        Check(game.Achievements.Count==1 && game.Achievements[0].ApiId=="FIRST" && game.Achievements[0].OriginalUnix==0,"explicit Epic unlock state preserves undated achievements and excludes locked achievements with misleading dates");
        Check(game.Platform=="Epic" && !game.SteamTarget && game.AppId==0 && game.ExternalGameId=="sandbox|main-app" && game.TotalAchievements==2,"Epic namespace/app identity and achievement codes remain distinct from Steam AppIDs");
        EpicProfile.SaveHistory(game,snapshots);var store=new LibraryStore(Path.Combine(dir,"Library"),snapshots);store.Save(new[]{game},"",null,"");var loaded=store.Load();
        Check(loaded.Games.Count==1 && loaded.Warnings.Count==0 && loaded.Games[0].Achievements[0].OriginalUnix==0 && loaded.Games[0].SourceLabel=="Epic Games (perfil)","Epic history, profile binding, undated unlocks and snapshots survive catalog reload");
        var none=EpicProfile.ParseHistory(Account,new EpicGame{AppName="empty",Namespace="sandbox"},J(Definitions),J("[]"),dir);
        Check(!EpicProfile.ShouldInclude(none) && EpicProfile.ShouldInclude(game),"only Epic games with at least one explicit unlock are eligible for import");
        var removed=EpicProfile.ParseHistory(Account,asset,J(Definitions),J("[{\"playerAchievement\":{\"achievementName\":\"REMOVED\",\"unlocked\":true,\"unlockDate\":\"invalid\"}}]"),dir);
        Check(removed.Achievements.Count==1 && removed.Achievements[0].ApiId=="REMOVED" && removed.TotalAchievements==3,"historic Epic unlock IDs absent from today's schema survive without inventing a Steam ID or understating the total");
        var other=EpicProfile.ParseHistory(new string('b',32),asset,J(Definitions),J(Progress),dir);
        Check(game.StorageKey!=other.StorageKey && !HistoryGroups.Compatible(game,other),"known different Epic profiles cannot merge even if achievement dates and codes match");
        var namespaceOther=EpicProfile.ParseHistory(Account,new EpicGame{AppName="main-app",Namespace="other"},J(Definitions),J(Progress),dir);
        Check(!HistoryGroups.Compatible(game,namespaceOther) && game.StorageKey!=namespaceOther.StorageKey,"different namespaces remain separate even if app names match");
        var options=new SourceSelection();Check(!options.Scans("Epic (perfil)") && !options.Allows(game,""),"Epic direct scanning is opt-in");options.Scan["Epic (perfil)"]=true;options.Scan["PA"]=false;Check(options.Allows(game,"") && options.Shows("Epic → Steam"),"Epic direct scanning is independent of PA and mapped plans retain the existing Epic library filter");
        var definition=new SteamDefinition{ApiId="STEAM_FIRST",Name="First"};var mapping=PlatformMapping.Capture(game,42,new[]{new AchievementMapping{SourceId="FIRST",TargetId=definition.ApiId,Confirmed=true}});
        var converted=PlatformMapping.Build(game,mapping,new[]{definition},"Steam game");PlayniteHistory.ProtectCurrent(converted,snapshots);store.Save(new[]{game,converted},"",null,"");
        Check(store.Load().Games.Count==2 && converted.Achievements[0].ApiId=="STEAM_FIRST" && converted.SourceLabel=="Epic Games → Steam" && game.Achievements[0].ApiId=="FIRST","confirmed Epic-to-Steam mappings persist separately and do not modify the original codes");
        string rawCode=new string('1',32);Check(EpicProfile.AuthorizationCode("{\"authorizationCode\":\""+rawCode+"\"}")==rawCode,"login accepts the explicit authorizationCode field from Epic's response");
        bool badCode=false;try{EpicProfile.AuthorizationCode("https://other.invalid/?code="+rawCode);}catch(InvalidDataException){badCode=true;}Check(badCode,"untrusted URLs are not interpreted as authorization codes");
        var service=new Service();using var api=new EpicProfile(service);var session=await api.LoginAsync(rawCode,CancellationToken.None);int protectedWrites=0;
        var connection=new EpicConnection(Path.Combine(dir,"Connection"),(bytes,encrypt)=>{if(encrypt)protectedWrites++;return bytes.Reverse().ToArray();});connection.Save(session);
        var renewed=await connection.SessionAsync(api,CancellationToken.None);Check(protectedWrites==2 && connection.Read().AccessToken==null && renewed.AccountId==Account && service.Bodies[0].Contains("grant_type=authorization_code"),"only a protected renewable session is persisted; the access token stays in memory and refresh rotation is saved");
        var games=await api.GamesAsync(renewed,CancellationToken.None);Check(games.Count==2 && service.Urls.Any(u=>u.Contains("cursor=A%2BB%20%2F%3F")),"Epic library pagination follows URL-escaped cursors");
        var fetched=await api.HistoryAsync(renewed,games[0],dir,_=>{},CancellationToken.None);Check(fetched.Achievements.Count==1 && fetched.GameName=="Fixture game" && service.Headers,"service lookup queries the schema and signed-in profile using the expected authorization headers");
        var alias=await api.HistoryAsync(renewed,new EpicGame{AppName="another-library-alias",Namespace="sandbox",Title="Another alias"},dir,_=>{},CancellationToken.None);
        Check(alias.StorageKey==fetched.StorageKey,"library aliases for one namespace/achievement product use one persistent history identity");
        var named=await api.HistoryAsync(renewed,new EpicGame{AppName="app-name",Namespace="sandbox",CatalogId="catalog-id"},dir,_=>{},CancellationToken.None);Check(named.GameName=="Catalog game name","catalog metadata supplies a readable game name when the library omits it");
        var catalogDenied=new Service{DenyCatalog=true};using var catalogApi=new EpicProfile(catalogDenied);
        var withoutName=await catalogApi.HistoryAsync(session,new EpicGame{AppName="app-name",Namespace="sandbox",CatalogId="catalog-id"},dir,_=>{},CancellationToken.None);
        Check(withoutName.Achievements.Count==1,"a denied optional catalog name cannot discard successfully read achievement progress");
        Check(service.Urls.All(u=>!u.Contains(rawCode) && !u.Contains("TEST_ACCESS_EPIC") && !u.Contains("TEST_REFRESH_EPIC")),"user session secrets never enter API URLs");
        var repeat=new Service{Repeat=true};using var repeatApi=new EpicProfile(repeat);bool repeated=false;try{await repeatApi.GamesAsync(session,CancellationToken.None);}catch(InvalidDataException){repeated=true;}Check(repeated && repeat.Urls.Count==2,"repeated library cursors fail instead of looping or returning a silently incomplete library");
        var missing=new Service{MissingProgress=true};using var missingApi=new EpicProfile(missing);bool unavailable=false;try{await missingApi.HistoryAsync(session,asset,dir,_=>{},CancellationToken.None);}catch(InvalidDataException){unavailable=true;}Check(unavailable,"a missing/private profile response is not misinterpreted as zero unlocks");
        var graphErrors=new Service{GraphErrors=true};using var errorApi=new EpicProfile(graphErrors);string message="";try{await errorApi.HistoryAsync(session,asset,dir,_=>{},CancellationToken.None);}catch(InvalidOperationException ex){message=ex.Message;}Check(message.Contains("error") && !message.Contains("PRIVATE_GRAPHQL_DETAIL"),"GraphQL errors reject partial data without exposing raw private server response text");
        var unsupported=new Service{NullProduct=true};using var unsupportedApi=new EpicProfile(unsupported);Check(await unsupportedApi.HistoryAsync(session,asset,dir,_=>{},CancellationToken.None)==null,"a title without an Epic Games achievement product is skipped without manufacturing achievements");
        var denied=new Service{Unauthorized=true};using var deniedApi=new EpicProfile(denied);bool rejected=false;try{await deniedApi.GamesAsync(session,CancellationToken.None);}catch(EpicServiceException ex){rejected=!ex.Message.Contains("PRIVATE_RESPONSE");}Check(rejected,"expired or forbidden sessions stop the remote phase with a sanitized reconnect message");
        var resourceDenied=new Service{DeniedSandbox="sandbox"};using var resourceApi=new EpicProfile(resourceDenied);EpicServiceException forbidden=null;
        try{await resourceApi.HistoryAsync(session,asset,dir,_=>{},CancellationToken.None);}catch(EpicServiceException ex){forbidden=ex;}
        Check(forbidden?.StatusCode==403 && forbidden.Message.Contains("esquema de logros") && forbidden.Message.Contains("errors.com.epicgames.access.denied") && !forbidden.Message.Contains("PRIVATE_DETAIL"),"resource denials preserve operation, HTTP status and public error code without leaking response text");
        var accessible=await resourceApi.HistoryAsync(session,new EpicGame{AppName="other",Namespace="accessible",Title="Accessible"},dir,_=>{},CancellationToken.None);
        Check(accessible.Achievements.Count==1,"a forbidden achievement resource does not invalidate the session for another title");
        var expired=new Service{UnauthorizedRemaining=1};using var refreshApi=new EpicProfile(expired);int renewals=0;var expiringSession=new EpicSession{AccessToken="TEST_ACCESS_EPIC",AccountId=Account};
        refreshApi.RenewSession=ct=>{renewals++;expired.AllowedAccess="REFRESHED_ACCESS_EPIC";return Task.FromResult(new EpicSession{AccessToken="REFRESHED_ACCESS_EPIC",AccountId=Account});};
        Check((await refreshApi.GamesAsync(expiringSession,CancellationToken.None)).Count==2 && renewals==1 && expired.Headers,"one 401 triggers a bounded session renewal and rebuilds requests with the new bearer token and launcher headers");
        var alwaysDenied=new Service{Unauthorized=true};using var boundedApi=new EpicProfile(alwaysDenied);int retries=0;boundedApi.RenewSession=ct=>{retries++;return Task.FromResult(session);};
        bool bounded=false;try{await boundedApi.GamesAsync(session,CancellationToken.None);}catch(EpicServiceException ex){bounded=ex.StatusCode==401;}Check(bounded && retries==1,"persistent authentication denial is retried at most once rather than looping");
        var switched=new Service{SwitchAccount=true};using var switchedApi=new EpicProfile(switched);bool mismatch=false;try{await connection.SessionAsync(switchedApi,CancellationToken.None);}catch(InvalidOperationException){mismatch=true;}Check(mismatch && connection.Read().AccountId==Account,"refresh cannot silently replace the connected account with another profile");
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();bool stopped=false;try{await api.GamesAsync(session,cancelled.Token);}catch(OperationCanceledException){stopped=true;}Check(stopped,"scan cancellation propagates to Epic requests");
        connection.Disconnect();Check(!connection.Connected && File.Exists(game.StatsPath) && File.Exists(store.SnapshotPath(game)),"disconnect removes only ATR's Epic session and preserves histories and snapshots");
    }
}
