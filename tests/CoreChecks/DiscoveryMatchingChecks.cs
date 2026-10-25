using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AchievementTimelineReplay;

static class DiscoveryMatchingChecks
{
    static void Check(bool condition,string text){if(!condition)throw new Exception(text);Console.WriteLine("PASS: Discovery/matching — "+text);}
    sealed class PublicPages : HttpMessageHandler
    {
        public string English,Spanish;public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {Calls++;return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(request.RequestUri.Query.Contains("spanish")?Spanish:English)});}
    }
    public static void Run(string root)
    {
        string xboxJson=Path.Combine(root,"2064168993.json");File.WriteAllText(xboxJson,"{}");
        string actualPA=Path.Combine(root,"pa","achievement_cache.db");
        var databases=SteamDefinitionSources.Databases(new[]{new GameEntry{Platform="Xbox",HistoryId="xbox_"+new string('A',24),StatsPath=xboxJson},new GameEntry{HistoryId="pa_"+new string('B',24),StatsPath=actualPA}},new[]{xboxJson,actualPA});
        Check(databases.Count==1 && databases[0]==actualPA,"Xbox JSON histories cannot become PA database candidates, even when the source path is configured");
        string invalidDir=Path.Combine(root,"invalid-pa");Directory.CreateDirectory(invalidDir);string invalid=Path.Combine(invalidDir,"achievement_cache.db");File.WriteAllText(invalid,"not a sqlite database");
        var warnings=new List<string>();var valid=PlatformMapping.FromPA(actualPA,100);Check(valid.Count>0,"destination lookup fixture supplies real Steam definitions");
        var recovered=SteamDefinitionSources.Load(null,100,new[]{invalid,actualPA},warnings.Add);
        Check(recovered.Definitions.Count==valid.Count && warnings.Count>0,"a corrupt PA candidate cannot prevent trying another valid cache");
        var unavailable=SteamDefinitionSources.Load(null,999999,new[]{invalid},warnings.Add);
        Check(unavailable.Definitions.Count==0,"unusable or absent PA definitions return control to the SAM fallback rather than aborting lookup");
        string badSchema=Path.Combine(root,"bad-steam-schema.bin");File.WriteAllText(badSchema,"invalid binary");
        Check(SteamDefinitionSources.Load(badSchema,100,new[]{actualPA},warnings.Add).Definitions.Count==valid.Count,"an unreadable local Steam schema also falls back to valid PA definitions");
        var games=SteamDiscovery.ParseSearch("{\"items\":[{\"id\":268910,\"name\":\"Cuphead\",\"type\":\"app\"},{\"id\":268910,\"name\":\"Duplicate\",\"type\":\"app\"},{\"id\":1,\"name\":\"Package\",\"type\":\"sub\"}]}");
        Check(games.Count==1 && games[0].AppId==268910 && games[0].Name=="Cuphead","public game search keeps candidate AppIDs without interpreting packages as games or duplicating results");
        string[] ids={"DefeatBoss","EXShot","ParryApprentice","CuttingCorners","ParryMaster","CompleteWorld1","NoHitsTaken"};
        string[] names={"Taking Names","Ceramic Strike","Parry Persistence","Cutting Corners","Parry Performance","A Walk in the Park","Perfect Run"};
        string[] descriptions={"Defeat a boss.","Defeat a boss with an EX move.","Parry 20 times.","Find a shortcut.","Parry 100 times.","Defeat every boss in Inkwell Isle I.","Complete a level without taking damage."};
        string[] spanishNames={"Presentación","Ataque de cerámica","Parador tenaz","Recortando","Parador exhaustivo","Un paseo por el parque","Partida perfecta"};
        string[] spanishDescriptions={"Derrota a un jefe.","Derrota a un jefe con un movimiento extra especial.","Realiza 20 paradas.","Encuentra un atajo.","Realiza 100 paradas.","Derrota a todos los jefes de la Isla Tintero 1.","Completa un nivel sin recibir daño."};
        var definitions=ids.Select((id,i)=>new SteamDefinition{ApiId=id,Name=names[i],Description=descriptions[i]}).ToList();
        var source=ids.Select((id,i)=>new AchievementRecord{ApiId="XBOX_"+i,DisplayName=spanishNames[i],Description=spanishDescriptions[i]}).ToList();
        for(int i=0;i<source.Count;i++)Check(AchievementMatching.Suggest(source[i],definitions).Target?.ApiId==ids[i],"Spanish/English description proposes the expected Cuphead-style ID: "+ids[i]);
        Check(!AchievementMatching.Suggest(source[2],definitions).Exact,"bilingual rule-based proposals remain reviewable rather than being treated as exact translated text");
        var wrong=new SteamDefinition{ApiId="WRONG",Name="Parador tenaz",Description="Realiza 100 paradas."};
        Check(AchievementMatching.Suggest(source[2],new[]{wrong}).Target==null,"matching names cannot override contradictory required quantities");
        wrong=new SteamDefinition{ApiId="WRONG",Name="Partida perfecta",Description="Completa un nivel recibiendo daño."};
        Check(AchievementMatching.Suggest(source[6],new[]{wrong}).Target==null,"negation conflicts prevent misleading damage/no-damage matches");
        var duplicate=new SteamDefinition{ApiId="OTHER",Name=definitions[0].Name,Description=definitions[0].Description};
        Check(AchievementMatching.Suggest(source[0],new[]{definitions[0],duplicate}).Target==null,"equal best candidates remain ambiguous instead of selecting an arbitrary achievement");
        string Page(string[] title,string[] description)=>String.Join("",ids.Select((id,i)=>"<div class=\"achieveRow\"><div class=\"achieveImgHolder\"><img src=\"https://example.invalid/"+i.ToString("x40")+".jpg\"></div><div><h3>"+WebUtility.HtmlEncode(title[i])+"</h3><h5>"+WebUtility.HtmlEncode(description[i])+"</h5></div></div>"));
        var handler=new PublicPages{English=Page(names,descriptions),Spanish=Page(spanishNames,spanishDescriptions)};
        string cache=Path.Combine(root,"public-steam-texts");
        int count=SteamDiscovery.LocalizeAsync(268910,definitions,"spanish",cache,default,handler).GetAwaiter().GetResult();
        Check(count==7 && definitions.Select(d=>d.ApiId).SequenceEqual(ids),"localized public texts join through English labels and stable image identity without inventing or changing API IDs");
        Check(source.All(s=>AchievementMatching.Suggest(s,definitions).Exact),"actual same-language names/descriptions become exact reviewable correspondences");
        Check(definitions.All(d=>d.Aliases.Count==1) && handler.Calls==2,"original English labels are retained and localization uses two bounded public requests");
        var next=ids.Select((id,i)=>new SteamDefinition{ApiId=id,Name=names[i],Description=descriptions[i]}).ToList();
        SteamDiscovery.LocalizeAsync(268910,next,"spanish",cache,default,handler).GetAwaiter().GetResult();
        Check(handler.Calls==2,"cached public translations avoid repeated network fetches");
        SteamDiscovery.LocalizeAsync(268910,next,"english",cache,default,handler).GetAwaiter().GetResult();
        Check(next[0].Name==names[0] && next[0].Aliases.Any(a=>a.Name==spanishNames[0]),"English comparison can retain verified Spanish aliases without losing the original Steam API ID");
        var local=SteamDiscovery.LocalGames(new[]{Path.Combine(root,"pa","achievement_cache.db")},Array.Empty<GameEntry>(),"Fixture 500");
        Check(local.Any(g=>g.AppId==500),"name discovery includes PA Steam titles even when none of their achievements are unlocked");
        var exact=new SteamDefinition{ApiId="EXACT",Name=source[0].DisplayName,Description=source[0].Description};
        var sameDescription=new SteamDefinition{ApiId="OTHER_NAME",Name="Different",Description=source[0].Description};
        Check(AchievementMatching.Suggest(source[0],new[]{exact,sameDescription}).Target==exact,"exact name plus description disambiguates an otherwise duplicated description");
        var foreign=PlayniteHistory.Load(Path.Combine(root,"pa","achievement_cache.db"),_=>{},default,true).Single(g=>g.Platform=="Xbox");
        var saved=PlatformMapping.Capture(foreign,268910,new[]{new AchievementMapping{SourceId=foreign.Achievements[0].ApiId,NoEquivalent=true}});
        string mappingFolder=Path.Combine(root,"no-equivalent-mappings");PlatformMapping.Save(mappingFolder,foreign,saved);
        Check(PlatformMapping.Load(mappingFolder,foreign,268910).Entries.Single().NoEquivalent,"explicit no-equivalent decisions remain distinguishable from older unfilled rows");
        Check(SteamDiscovery.ParseAchievements("<html>Please sign in</html>").Count==0,"sign-in or error pages cannot become invented achievement definitions");
        Check(SteamDiscovery.IconKey(new string('a',64)).Length==0,"unrelated 64-character cache hashes cannot masquerade as Steam image identities");
    }
}
