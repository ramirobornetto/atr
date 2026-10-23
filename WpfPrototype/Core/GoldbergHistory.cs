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
    // Read-only adapter for Goldberg / GBE's user-state object and definition array.
    internal static class GoldbergHistory
    {
        public static string Identity(string statePath) => "goldberg_"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(statePath).ToUpperInvariant()))).Substring(0,24);
        public static string[] DefaultRoots() => new[]{"Goldberg SteamEmu Saves","GSE Saves"}.Select(n=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),n)).ToArray();
        static JsonDocument Read(string path)
        {
            using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);
            if(stream.Length>16*1024*1024)throw new InvalidDataException("El JSON supera el tamaño admitido (16 MB).");
            // Some generators write a UTF-8 BOM.
            using var reader=new StreamReader(stream,Encoding.UTF8,true);
            return JsonDocument.Parse(reader.ReadToEnd());
        }
        static string Text(JsonElement obj,string key,string fallback)
        {
            if(!obj.TryGetProperty(key,out var v))return fallback;
            if(v.ValueKind==JsonValueKind.String)return v.GetString()??fallback;
            if(v.ValueKind!=JsonValueKind.Object)return fallback;
            foreach(var lang in new[]{"latam","latam_spanish","spanish","english"})
                if(v.TryGetProperty(lang,out var s) && s.ValueKind==JsonValueKind.String && !String.IsNullOrWhiteSpace(s.GetString()))return s.GetString();
            return v.EnumerateObject().Where(p=>p.Value.ValueKind==JsonValueKind.String).Select(p=>p.Value.GetString()).FirstOrDefault(s=>!String.IsNullOrWhiteSpace(s))??fallback;
        }
        public static GameEntry Load(string statePath,uint appId,string definitionsPath=null,string steamSchemaPath=null,Action<string> log=null)
        {
            if(appId==0)throw new InvalidDataException("Se necesita un AppID válido.");
            log??=_=>{};
            using var state=Read(statePath);
            if(state.RootElement.ValueKind!=JsonValueKind.Object)throw new InvalidDataException("Se esperaba el estado de usuario de Goldberg (objeto JSON), no el array de steam_settings.");
            var names=new Dictionary<string,(string Name,string Description)>(StringComparer.OrdinalIgnoreCase);
            string gameName="AppID "+appId;
            if(!String.IsNullOrEmpty(steamSchemaPath) && File.Exists(steamSchemaPath))
            {
                try
                {
                    var app=BinaryKvReader.ReadFile(steamSchemaPath)[appId.ToString(CultureInfo.InvariantCulture)] as Dictionary<string,object>;
                    if(app.TryGetValue("gamename",out var title) && title is string name)gameName=name;
                    if(app.TryGetValue("stats",out var stats) && stats is Dictionary<string,object> groups)
                    foreach(var g in groups.Values.OfType<Dictionary<string,object>>())
                    {
                        if(!g.TryGetValue("type",out var type) || !String.Equals(Convert.ToString(type),"ACHIEVEMENTS",StringComparison.OrdinalIgnoreCase) || !g.TryGetValue("bits",out var bits) || bits is not Dictionary<string,object> entries)continue;
                        foreach(var bit in entries.Values.OfType<Dictionary<string,object>>())
                        {
                            if(!bit.TryGetValue("name",out var id) || id is not string api || String.IsNullOrWhiteSpace(api))continue;
                            string display=api,description="";
                            if(bit.TryGetValue("display",out var d) && d is Dictionary<string,object> values)
                            {
                                string Local(string field,string fallback)
                                {
                                    if(!values.TryGetValue(field,out var o) || o is not Dictionary<string,object> langs)return fallback;
                                    foreach(string lang in new[]{"latam","spanish","english"})if(langs.TryGetValue(lang,out var v) && v is string s && !String.IsNullOrWhiteSpace(s))return s;
                                    return fallback;
                                }
                                display=Local("name",api);description=Local("desc","");
                            }
                            names[api]=(display,description);
                        }
                    }
                }
                catch(Exception ex){log("Esquema Steam no disponible: "+ex.Message);}
            }
            if(!String.IsNullOrEmpty(definitionsPath))
            {
                using var definitions=Read(definitionsPath);
                if(definitions.RootElement.ValueKind!=JsonValueKind.Array)throw new InvalidDataException("La definición Goldberg debe ser un array JSON de steam_settings.");
                var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach(var entry in definitions.RootElement.EnumerateArray())
                {
                    if(entry.ValueKind!=JsonValueKind.Object || !entry.TryGetProperty("name",out var n) || n.ValueKind!=JsonValueKind.String || String.IsNullOrWhiteSpace(n.GetString()))throw new InvalidDataException("Definición sin nombre API válido.");
                    string id=n.GetString();
                    if(!seen.Add(id))throw new InvalidDataException("Definición duplicada: "+id);
                    names[id]=(Text(entry,"displayName",id),Text(entry,"description",""));
                }
            }
            var game=new GameEntry{AppId=appId,AccountId=0,GameName=gameName,StatsPath=Path.GetFullPath(statePath),SchemaPath=definitionsPath,HasLocalHistory=true,HistoryId=Identity(statePath),SourceLabel="achievements.json (Goldberg/GBE)",DetectedOrigin="Goldberg",IsDemo=File.Exists(Path.Combine(Path.GetDirectoryName(statePath),".atr-demo"))};
            var all=new HashSet<string>(names.Keys,StringComparer.OrdinalIgnoreCase);
            var seenState=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var property in state.RootElement.EnumerateObject())
            {
                string id=property.Name;var value=property.Value;
                if(String.IsNullOrWhiteSpace(id) || !seenState.Add(id))throw new InvalidDataException("Nombre de logro vacío o duplicado: "+id);
                if(value.ValueKind!=JsonValueKind.Object || !value.TryGetProperty("earned",out var earned) || (earned.ValueKind!=JsonValueKind.True && earned.ValueKind!=JsonValueKind.False))throw new InvalidDataException("Estado Goldberg inválido para "+id+": falta earned booleano.");
                all.Add(id);
                if(!earned.GetBoolean())continue;
                if(!value.TryGetProperty("earned_time",out var ts) || !ts.TryGetInt64Safe(out long unix) || unix<=0 || unix>UInt32.MaxValue)
                {log("Goldberg: "+id+" desbloqueado sin fecha Unix válida; no se añade al plan.");continue;}
                var metadata=names.TryGetValue(id,out var m)?m:(id,"");
                game.Achievements.Add(new AchievementRecord{ApiId=id,DisplayName=metadata.Item1,Description=metadata.Item2,OriginalUnix=unix});
            }
            game.TotalAchievements=all.Count;
            game.Achievements=game.Achievements.OrderBy(a=>a.OriginalUnix).ThenBy(a=>a.ApiId,StringComparer.Ordinal).ToList();
            if(game.IsDemo)game.GameName="Demostración Goldberg (sin conexión a Steam)";
            return game;
        }
        static bool TryGetInt64Safe(this JsonElement element,out long value){value=0;return element.ValueKind==JsonValueKind.Number && element.TryGetInt64(out value);}
        public static GameEntry Protect(GameEntry game,string snapshotsDirectory)
        {
            string directory=Path.Combine(snapshotsDirectory,game.StorageKey);
            string path=Path.Combine(directory,"snapshot.json");
            if(File.Exists(path))
            {
                var snapshot=JsonSerializer.Deserialize<GoldbergSnapshot>(File.ReadAllText(path));
                if(snapshot==null || snapshot.Version!=1 || snapshot.AppId!=game.AppId || snapshot.HistoryId!=game.HistoryId || snapshot.IsDemo!=game.IsDemo || snapshot.Achievements==null || snapshot.Achievements.Count==0 || snapshot.Achievements.Any(a=>a==null || String.IsNullOrWhiteSpace(a.ApiId) || a.OriginalUnix<=0 || a.OriginalUnix>UInt32.MaxValue) || snapshot.Achievements.Select(a=>a.ApiId).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=snapshot.Achievements.Count)
                    throw new InvalidDataException("Snapshot Goldberg inválido. No se sobrescribió.");
                game.Achievements=snapshot.Achievements.Select(a=>new AchievementRecord{ApiId=a.ApiId,DisplayName=a.DisplayName,Description=a.Description,OriginalUnix=a.OriginalUnix}).ToList();
                game.TotalAchievements=Math.Max(game.TotalAchievements,game.Achievements.Count);
            }
            else
            {
                if(game.Achievements.Count==0)throw new InvalidDataException("No hay logros desbloqueados con fecha válida para importar.");
                Directory.CreateDirectory(directory);
                // Keep original bytes separate from our protected historical representation.
                File.Copy(game.StatsPath,Path.Combine(directory,"original_achievements.json"),false);
                if(!String.IsNullOrEmpty(game.SchemaPath))File.Copy(game.SchemaPath,Path.Combine(directory,"original_definitions.json"),false);
                var snapshot=new GoldbergSnapshot{AppId=game.AppId,HistoryId=game.HistoryId,IsDemo=game.IsDemo,Achievements=game.Achievements.Select(a=>new SnapshotAchievement{ApiId=a.ApiId,DisplayName=a.DisplayName,Description=a.Description,OriginalUnix=a.OriginalUnix}).ToList()};
                using var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);
                JsonSerializer.Serialize(stream,snapshot);stream.Flush(true);
            }
            game.HasSnapshot=true;return game;
        }
        public static List<GameEntry> Scan(string root,string steamRoot,string snapshotsDirectory,Action<string> log,CancellationToken token)
        {
            if(!Directory.Exists(root))throw new DirectoryNotFoundException("No existe la carpeta Goldberg: "+root);
            var candidates=new List<string>();
            if(File.Exists(Path.Combine(root,"achievements.json")))candidates.Add(root);
            candidates.AddRange(Directory.GetDirectories(root).Where(d=>File.Exists(Path.Combine(d,"achievements.json"))));
            var result=new List<GameEntry>();
            foreach(string folder in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                token.ThrowIfCancellationRequested();
                if(!UInt32.TryParse(Path.GetFileName(folder),NumberStyles.None,CultureInfo.InvariantCulture,out uint appId) || appId==0){log("Goldberg: carpeta sin AppID numérico omitida: "+folder);continue;}
                try
                {
                    string definitions=Path.Combine(folder,"steam_settings","achievements.json");
                    string schema=String.IsNullOrWhiteSpace(steamRoot)?null:Path.Combine(steamRoot,"appcache","stats","UserGameStatsSchema_"+appId+".bin");
                    var game=Load(Path.Combine(folder,"achievements.json"),appId,File.Exists(definitions)?definitions:null,schema,log);
                    token.ThrowIfCancellationRequested();
                    if(game.Achievements.Count==0){log("Goldberg AppID "+appId+": sin logros con fecha válida.");continue;}
                    result.Add(Protect(game,snapshotsDirectory));log("Goldberg: importado "+appId+" desde "+folder);
                }
                catch(OperationCanceledException){throw;}
                catch(Exception ex){log("Goldberg AppID "+appId+" omitido: "+ex.Message);}
            }
            token.ThrowIfCancellationRequested();return result;
        }
    }
    internal sealed class GoldbergSnapshot
    {
        public int Version {get;set;}=1;
        public uint AppId {get;set;}
        public string HistoryId {get;set;}
        public bool IsDemo {get;set;}
        public List<SnapshotAchievement> Achievements {get;set;}
    }
}
