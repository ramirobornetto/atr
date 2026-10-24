using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AchievementTimelineReplay;

static class XboxChecks
{
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);Console.WriteLine("PASS: Xbox — "+message);}
    static JsonElement J(string json){using var doc=JsonDocument.Parse(json);return doc.RootElement.Clone();}
    sealed class Service : HttpMessageHandler
    {
        public readonly List<string> Paths=new List<string>();
        public readonly List<string> Bodies=new List<string>();
        public bool Repeat,Reject,Headers=true,DeviceMode;
        int tokenCalls;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();Paths.Add(request.RequestUri.AbsoluteUri);Bodies.Add(request.Content==null?"":await request.Content.ReadAsStringAsync(token));
            string path=request.RequestUri.AbsoluteUri;string json;
            if(path.Contains("oauth2/v2.0/devicecode"))json="{\"device_code\":\"DEVICE_TEST_ONLY\",\"user_code\":\"ABCD1234\",\"verification_uri\":\"https://microsoft.com/devicelogin\",\"interval\":1,\"expires_in\":30}";
            else if(DeviceMode && path.Contains("oauth2/v2.0/token") && tokenCalls++==0)
                return new HttpResponseMessage(HttpStatusCode.BadRequest){Content=new StringContent("{\"error\":\"authorization_pending\"}")};
            else if(path.Contains("oauth2/v2.0/token"))
                json="{\"access_token\":\"ACCESS_TEST_ONLY\",\"refresh_token\":\"ROTATED_TEST_ONLY\"}";
            else if(path.Contains("user.auth.xboxlive.com"))json="{\"Token\":\"USER_TEST_ONLY\"}";
            else if(path.Contains("xsts.auth.xboxlive.com"))json="{\"Token\":\"XSTS_TEST_ONLY\",\"DisplayClaims\":{\"xui\":[{\"xid\":\"123456789\",\"uhs\":\"HASH_TEST_ONLY\"}]}}";
            else
            {
                Headers &= request.Headers.TryGetValues("Authorization",out var values) && values.Single()=="XBL3.0 x=HASH_TEST_ONLY;XSTS_TEST_ONLY";
                Headers &= request.Headers.TryGetValues("x-xbl-contract-version",out var versions) && versions.Single()=="2";
                if(Reject)return new HttpResponseMessage(HttpStatusCode.Forbidden){Content=new StringContent("PRIVATE_SERVER_DETAIL_SHOULD_NOT_BE_LOGGED")};
                if(path.Contains("titlehub"))
                {
                    bool second=path.Contains("continuationToken=");
                    json=second && !Repeat?"{\"titles\":[{\"titleId\":8,\"name\":\"Second game\"}],\"pagingInfo\":{}}":"{\"titles\":[{\"titleId\":7,\"name\":\"First game\",\"achievement\":{\"totalAchievements\":42}}],\"pagingInfo\":{\"continuationToken\":\"A+B /?\"}}";
                }
                else json="{\"achievements\":[{\"id\":\"2\",\"name\":\"Unlocked without date\",\"progressState\":\"Achieved\"}],\"pagingInfo\":{}}";
            }
            return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(json,Encoding.UTF8,"application/json")};
        }
    }
    public static void Run(string testRoot)=>RunAsync(testRoot).GetAwaiter().GetResult();
    static async Task RunAsync(string root)
    {
        string dir=Path.Combine(root,"xbox-tests"),snapshots=Path.Combine(dir,"Snapshots");
        var title=new XboxTitle{Id="623325843",Name="Cuphead",Total=42};
        var records=new[]{J("{\"id\":\"1\",\"name\":\"Boss\",\"progressState\":\"Achieved\",\"progression\":{\"timeUnlocked\":\"2026-04-06T20:30:00Z\"}}"),J("{\"id\":2,\"name\":\"No date\",\"progressState\":\"Achieved\",\"progression\":{\"timeUnlocked\":\"0001-01-01T00:00:00Z\"}}"),J("{\"id\":\"3\",\"name\":\"Locked with a misleading date\",\"progressState\":\"NotStarted\",\"progression\":{\"timeUnlocked\":\"2026-04-06T20:30:00Z\"}}")};
        var game=XboxProfile.ParseHistory("123456789",title,records,dir);
        Check(game.Achievements.Count==2 && game.Achievements.Single(a=>a.ApiId=="2").OriginalUnix==0 && game.Achievements.All(a=>a.ApiId!="3"),"explicit unlock state wins over timestamps; unlocked records without dates survive and locked records are excluded");
        Check(game.AppId==0 && !game.SteamTarget && game.ExternalGameId==title.Id && game.Achievements[0].ApiId=="1","Xbox title and achievement identifiers remain distinct from Steam AppIDs/API names");
        Check(game.TotalAchievements==42 && game.GameName=="Cuphead","title metadata retains total achievement count and display name");
        XboxProfile.SaveHistory(game,snapshots);
        var library=new LibraryStore(Path.Combine(dir,"Library"),snapshots);library.Save(new[]{game},"",null,"Xbox fixture");var loaded=library.Load();
        Check(loaded.Games.Count==1 && loaded.Warnings.Count==0 && loaded.Games[0].Achievements.Count==2 && loaded.Games[0].Achievements.Any(a=>a.OriginalUnix==0) && loaded.Games[0].SourceLabel=="Xbox Live","direct Xbox snapshots and identities survive catalog reload, including undated unlocks");
        var empty=XboxProfile.ParseHistory("123456789",new XboxTitle{Id="8",Name="No unlocks"},Array.Empty<JsonElement>(),dir);XboxProfile.SaveHistory(empty,snapshots);library.Save(new[]{empty},"",null,"");var emptyLoaded=library.Load();
        Check(emptyLoaded.Games.Count==0,"old direct Xbox 0/0 entries are hidden when reloading the saved catalog without deleting source files");
        Check(File.Exists(empty.StatsPath) && !XboxProfile.ShouldInclude(empty),"empty Xbox entries are excluded without deleting their cache or snapshots");
        var locked=XboxProfile.ParseHistory("123456789",new XboxTitle{Id="10",Name="Locked Xbox game",Total=35},Array.Empty<JsonElement>(),dir);XboxProfile.SaveHistory(locked,snapshots);library.Save(new[]{locked},"",null,"");
        Check(!XboxProfile.ShouldInclude(locked) && library.Load().Games.Count==0,"Xbox titles with a catalog but zero unlocks are excluded during scan eligibility and catalog reload");
        Check(XboxProfile.ShouldInclude(game) && XboxProfile.ShouldInclude(new GameEntry{Platform="Steam"}),"the empty Xbox filter preserves real unlocks and does not filter native Steam histories");
        Check(SourceSelection.DisplayOrigin("Xbox")=="Xbox Live" && SourceSelection.DisplayOrigin("Xbox → Steam")=="Xbox Live → Steam" && SourceSelection.LibraryCategory("Xbox Live → Steam")=="Xbox","display labels identify Xbox Live while converted entries retain the Xbox filter category");
        var legacy=XboxProfile.ParseHistory("123456789",new XboxTitle{Id="9",Name="Legacy",Legacy=true},new[]{J("{\"id\":4,\"unlocked\":true,\"timeUnlocked\":null}"),J("{\"id\":5,\"unlocked\":true,\"isRevoked\":true}")},dir);
        Check(legacy.Achievements.Count==1 && legacy.Achievements[0].ApiId=="4" && legacy.Achievements[0].OriginalUnix==0,"Xbox 360 parser respects explicit unlocks, revocation and missing dates");
        bool duplicate=false;try{XboxProfile.ParseHistory("123456789",title,new[]{records[0],records[0]},dir);}catch(InvalidDataException){duplicate=true;}Check(duplicate,"duplicate provider IDs reject a title rather than creating conflicting mappings");
        var other=XboxProfile.ParseHistory("987654321",title,records,dir);
        Check(other.StorageKey!=game.StorageKey && !HistoryGroups.Compatible(game,other),"different Xbox profiles cannot overwrite or merge each other's histories");
        var pa=new GameEntry{Platform="Xbox",ExternalGameId=title.Id,HistoryId="pa_"+new string('A',24),ProfileKey=game.ProfileKey,PAProvider="Xbox",Achievements=game.Achievements.Take(1).ToList(),HasLocalHistory=true,HasSnapshot=true};
        var groups=HistoryGroups.Build(new[]{pa,game},new Dictionary<string,string>());
        Check(groups.Count==1 && groups[0].Sources.Count==2 && groups[0].Selected==game,"PA and direct Xbox histories of the same title/profile share a group and prefer more unlocks");
        pa.ProfileKey=other.ProfileKey;Check(HistoryGroups.Build(new[]{pa,game},new Dictionary<string,string>()).Count==2,"matching achievement dates cannot merge known different Xbox profiles");
        var options=new SourceSelection();Check(!options.Scans("Xbox (perfil)") && options.Scans("PA") && !options.Allows(game,""),"direct Xbox requests require opting in and remain independent from the single PA scan switch");
        options.Scan["Xbox (perfil)"]=true;options.Scan["PA"]=false;Check(options.Allows(game,"") && !options.Allows(pa,""),"Xbox profile scanning works without enabling PA");
        options.Visible["Xbox"]=false;Check(!options.Shows("Xbox → Steam") && SourceSelection.LibraryCategory("Xbox → Steam")=="Xbox","converted display labels continue to follow the Xbox library filter");
        var target=new SteamDefinition{ApiId="DefeatBoss",Name="Boss",Description="Defeat a boss"};
        var mapping=PlatformMapping.Capture(game,268910,new[]{new AchievementMapping{SourceId="1",TargetId=target.ApiId,Confirmed=true}});
        var converted=PlatformMapping.Build(game,mapping,new[]{target},"Cuphead");PlayniteHistory.ProtectCurrent(converted,snapshots);library.Save(new[]{game,converted},"",null,"");loaded=library.Load();
        Check(loaded.Games.Count==2 && loaded.Warnings.Count==0 && loaded.Games.Any(g=>g.SteamTarget && g.SourceLabel=="Xbox Live → Steam"),"confirmed direct Xbox mappings prepare and persist a separate Steam plan without changing the original IDs");
        int protects=0;var connection=new XboxConnection(Path.Combine(dir,"Connection"),(bytes,encrypt)=>{if(encrypt){protects++;return bytes.Reverse().ToArray();}return bytes.Reverse().ToArray();});
        connection.Configure("11111111-2222-3333-4444-555555555555");connection.SaveToken("REFRESH_TEST_ONLY");
        Check(protects==1 && !File.ReadAllText(Path.Combine(dir,"Connection","session.dat")).Contains("REFRESH_TEST_ONLY"),"session storage invokes the secret protector before writing; tests use an injected protector without Windows credentials");
        var service=new Service();using var api=new XboxProfile(service);var session=await connection.SessionAsync(api,CancellationToken.None);
        Check(session.Xuid=="123456789" && protects==2 && service.Bodies[0].Contains("REFRESH_TEST_ONLY"),"session renewal rotates the protected refresh token and obtains Xbox/XSTS authorization");
        var titles=await api.TitlesAsync(session,CancellationToken.None);
        Check(titles.Count==2 && titles[0].Total==42 && service.Paths.Any(p=>p.Contains("continuationToken=A%2BB%20%2F%3F")),"title history follows continuation tokens with URL escaping rather than truncating at the first page");
        var fetched=await api.HistoryAsync(session,titles[0],dir,CancellationToken.None);
        Check(fetched.Achievements.Count==1 && fetched.Achievements[0].OriginalUnix==0 && service.Headers,"authenticated service reads use the expected Xbox contract and retain undated unlocks");
        Check(service.Paths.All(p=>!p.Contains("REFRESH_TEST_ONLY") && !p.Contains("XSTS_TEST_ONLY")),"session secrets never enter request URLs");
        var repeat=new Service{Repeat=true};using var repeatApi=new XboxProfile(repeat);bool repeated=false;try{await repeatApi.TitlesAsync(session,CancellationToken.None);}catch(InvalidDataException){repeated=true;}Check(repeated && repeat.Paths.Count==2,"repeated pagination tokens fail explicitly instead of looping or silently returning partial results");
        var rejection=new Service{Reject=true};using var rejectedApi=new XboxProfile(rejection);string message="";try{await rejectedApi.TitlesAsync(session,CancellationToken.None);}catch(InvalidOperationException ex){message=ex.Message;}Check(message.Contains("sesión") && !message.Contains("PRIVATE_SERVER_DETAIL"),"permission failures are actionable and do not expose server response bodies in logs");
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();bool stopped=false;try{await api.TitlesAsync(session,cancelled.Token);}catch(OperationCanceledException){stopped=true;}Check(stopped,"cancellation reaches the service request without applying any Steam operation");
        connection.Disconnect();Check(!connection.Connected && File.Exists(game.StatsPath) && File.Exists(library.SnapshotPath(game)),"disconnect deletes only ATR's session and preserves histories and plans");
        var deviceService=new Service{DeviceMode=true};using var deviceApi=new XboxProfile(deviceService);
        var device=await deviceApi.BeginAsync(connection.ClientId,CancellationToken.None);var oauth=await deviceApi.WaitAsync(connection.ClientId,device,CancellationToken.None);
        Check(device.UserCode=="ABCD1234" && device.VerificationUri=="https://microsoft.com/devicelogin" && oauth.AccessToken=="ACCESS_TEST_ONLY" && deviceService.Paths.Count==3,"device authorization waits through pending status and uses Microsoft's official verification page without collecting a password");
        Check(deviceService.Bodies[0].Contains("XboxLive.signin") && deviceService.Bodies[0].Contains("offline_access") && deviceService.Bodies[1].Contains("DEVICE_TEST_ONLY"),"device flow requests Xbox sign-in/offline scope and polls using the issued device code");
    }
}
