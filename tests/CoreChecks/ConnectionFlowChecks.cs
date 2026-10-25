using System;
using System.IO;
using System.Linq;
using AchievementTimelineReplay;

static class ConnectionFlowChecks
{
    static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS: Connections/scan — "+message);}
    public static void Run(string root)
    {
        var xbox=new XboxConnection(Path.Combine(root,"xbox-default-client"));
        Check(xbox.ClientId==XboxConnection.DefaultClientId && Guid.TryParse(xbox.ClientId,out _),"a clean installation uses ATR's registered public Xbox client without manual setup");
        Check(!Directory.Exists(Path.Combine(root,"xbox-default-client")),"reading built-in Xbox configuration does not create or modify session files");
        var summary=new RemoteScanSummary();for(int i=0;i<36;i++)summary.Record(RemoteScanKind.NoCatalog);for(int i=0;i<31;i++)summary.Record(RemoteScanKind.NoProgress);for(int i=0;i<2;i++)summary.Record(RemoteScanKind.Recovered);
        Check(summary.Reviewed==69 && !summary.HasErrors && summary.Count(RemoteScanKind.Recovered)==2,"the user's 69-item Epic case reconciles 2 recovered, 36 unavailable catalogs and 31 unavailable progress records without inventing 67 errors");
        Check(summary.Format("Epic").Contains("31 sin progreso disponible") && summary.Format("Epic").Contains("0 errores"),"missing progress stays distinct from both zero unlocks and actual failures in the user summary");
        summary.Record(RemoteScanKind.NoUnlocks);summary.Record(RemoteScanKind.Duplicate);summary.Record(RemoteScanKind.Error);
        Check(summary.HasErrors && summary.Reviewed==72 && summary.Count(RemoteScanKind.NoUnlocks)==1 && summary.Count(RemoteScanKind.Duplicate)==1,"duplicates, zero-unlock titles and real errors retain separate reconciled counts");
        var phase=new RemoteScanSummary();phase.FailPhase("HTTP 401");Check(phase.Reviewed==0 && phase.HasErrors && phase.Format("Epic").Contains("1 fallos de fase"),"a session failure before title enumeration is not counted as a reviewed game");
        var preferences=new SourceSelection();preferences.Scan["PA"]=false;preferences.Visible["PA"]=false;
        Check(SourceSelection.DisplayOrigin("PA")=="Playnite Achievements" && !preferences.Scans("PA") && !preferences.Shows("Playnite Achievements"),"Playnite Achievements presentation keeps the persisted PA scan/filter keys and preferences");
        Check(SourceSelection.DisplayOrigin("Xbox (perfil)")=="Xbox Live" && SourceSelection.DisplayOrigin("Epic (perfil)")=="Epic","visible profile source names omit the parenthetical suffix without changing their keys");
        Check(!SourceSelection.LibraryFilterSources.Intersect(new[]{"GOG","PlayStation","OnlineFix"}).Any() && new[]{"GOG","PlayStation","OnlineFix"}.All(k=>SourceSelection.LibraryCategory(k)=="Otro"),"removed library filter buttons keep their histories reachable through Other");
        preferences.Scan["Xbox (perfil)"]=true;preferences.Scan["Epic (perfil)"]=true;
        Check(preferences.MissingConnections(false,true).SequenceEqual(new[]{"Xbox (perfil)"}) && preferences.MissingConnections(true,true).Count==0,"connection warnings identify only selected sources that lack a linked session");
        string settings=Path.Combine(root,"renamed-source-preferences.json");preferences.Save(settings);var reloaded=SourceSelection.Load(settings);
        Check(!reloaded.Scan["PA"] && !reloaded.Visible["PA"] && reloaded.Scan["Epic (perfil)"],"renamed presentation survives preference roundtrip using the original data keys");
        var unique=PlatformMapping.UniqueTargets(new[]{"A","a",null,"","B","C","C"});
        Check(unique.SetEquals(new[]{"B"}),"bulk confirmation accepts only non-empty unique destination IDs and treats case variants as conflicts");
        string code=new string('7',32),json="{\"authorizationCode\":\""+code+"\"}";
        Check(EpicLoginCapture.ResponseCode(EpicProfile.CodeUrl,json)==code,"embedded login accepts a code only from Epic's expected authorization endpoint");
        Check(EpicLoginCapture.ResponseCode("https://www.epicgames.com.attacker.invalid/id/api/redirect",json)==null && EpicLoginCapture.ResponseCode("http://www.epicgames.com/id/api/redirect",json)==null && EpicLoginCapture.ResponseCode("https://www.epicgames.com:444/id/api/redirect",json)==null,"lookalike domains, HTTP and unexpected ports cannot supply embedded login credentials");
        Check(EpicLoginCapture.ResponseCode(EpicProfile.CodeUrl.Replace("34a02cf8f4414e29b15921876da36f9a","other-client"),json)==null && EpicLoginCapture.ResponseCode(EpicProfile.CodeUrl+"&clientId=other",json)==null,"another OAuth client or duplicate client parameters cannot complete the login flow");
        Check(EpicLoginCapture.ResponseCode(EpicProfile.CodeUrl,"{\"errorCode\":\"PRIVATE_ERROR\"}")==null && EpicLoginCapture.ResponseCode(EpicProfile.CodeUrl,"not JSON")==null,"non-code responses are ignored without exposing their contents");
        string callback="http://localhost/launcher/authorized?code="+code;
        Check(EpicLoginCapture.Callback(callback,true)==code && EpicLoginCapture.Callback(callback,false)==null,"localhost callback capture requires an outstanding expected authorization navigation");
        Check(EpicLoginCapture.Callback(callback.Replace("localhost","attacker.invalid"),true)==null && EpicLoginCapture.Callback(callback+"&code="+code,true)==null,"external or ambiguous callback URLs do not supply authorization codes");
    }
}
