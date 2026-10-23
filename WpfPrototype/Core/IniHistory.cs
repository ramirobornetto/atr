using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace AchievementTimelineReplay
{
    // Initial, deliberately explicit support for CODEX/RUNE-compatible INI state.
    internal static class IniHistory
    {
        public static string Identity(string path) => "codex_ini_"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(path).ToUpperInvariant()))).Substring(0,24);
        public static string[] DefaultRoots()
        {
            var paths=new List<string>();
            string common=Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments);
            if(!String.IsNullOrWhiteSpace(common))foreach(string provider in new[]{"CODEX","RUNE"})paths.Add(Path.Combine(common,"Steam",provider));
            string roaming=Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if(!String.IsNullOrWhiteSpace(roaming))foreach(string provider in new[]{"CODEX","RUNE"})paths.Add(Path.Combine(roaming,"Steam",provider));
            return paths.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }
        public static uint AppIdFromPath(string file)
        {
            var dir=Path.GetDirectoryName(Path.GetFullPath(file));
            if(String.Equals(Path.GetFileName(dir),"remote",StringComparison.OrdinalIgnoreCase))dir=Path.GetDirectoryName(dir);
            return UInt32.TryParse(Path.GetFileName(dir),NumberStyles.None,CultureInfo.InvariantCulture,out var id) && id>0?id:0;
        }
        static Dictionary<string,Dictionary<string,string>> Read(string path)
        {
            using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);
            if(stream.Length>16*1024*1024)throw new InvalidDataException("El INI supera 16 MB.");
            using var reader=new StreamReader(stream,new UTF8Encoding(false,true),true);
            var sections=new Dictionary<string,Dictionary<string,string>>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string,string> current=null;
            string line;int lineNumber=0;
            while((line=reader.ReadLine())!=null)
            {
                lineNumber++;line=line.Trim();
                if(line.Length==0 || line.StartsWith(";") || line.StartsWith("#"))continue;
                if(line.StartsWith("["))
                {
                    int close=line.IndexOf(']');
                    if(close<=1 || (line.Substring(close+1).Trim().Length>0 && !line.Substring(close+1).TrimStart().StartsWith(";")))throw new InvalidDataException("Sección INI inválida en línea "+lineNumber);
                    string id=line.Substring(1,close-1).Trim();
                    if(String.IsNullOrWhiteSpace(id) || sections.ContainsKey(id))throw new InvalidDataException("Sección INI vacía o duplicada: "+id);
                    current=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);sections.Add(id,current);continue;
                }
                int equals=line.IndexOf('=');
                if(current==null || equals<=0)throw new InvalidDataException("Campo INI fuera de sección o inválido en línea "+lineNumber);
                string key=line.Substring(0,equals).Trim(),value=line.Substring(equals+1).Split(';')[0].Trim();
                if(key.Length==0 || !current.TryAdd(key,value))throw new InvalidDataException("Clave INI vacía o duplicada en línea "+lineNumber);
            }
            return sections;
        }
        static Dictionary<string,(string Name,string Description)> Metadata(string schemaPath,uint appId,Action<string> log)
        {
            var names=new Dictionary<string,(string,string)>(StringComparer.OrdinalIgnoreCase);
            if(String.IsNullOrWhiteSpace(schemaPath) || !File.Exists(schemaPath))return names;
            try
            {
                var root=BinaryKvReader.ReadFile(schemaPath);
                if(!root.TryGetValue(appId.ToString(CultureInfo.InvariantCulture),out var app) || app is not Dictionary<string,object> a || !a.TryGetValue("stats",out var stats) || stats is not Dictionary<string,object> groups)return names;
                foreach(var group in groups.Values.OfType<Dictionary<string,object>>())
                {
                    if(!group.TryGetValue("type",out var t) || !String.Equals(Convert.ToString(t),"ACHIEVEMENTS",StringComparison.OrdinalIgnoreCase) || !group.TryGetValue("bits",out var b) || b is not Dictionary<string,object> bits)continue;
                    foreach(var bit in bits.Values.OfType<Dictionary<string,object>>())
                    {
                        if(!bit.TryGetValue("name",out var n) || n is not string id || String.IsNullOrWhiteSpace(id))continue;
                        string Local(string field,string fallback)
                        {
                            if(!bit.TryGetValue("display",out var display) || display is not Dictionary<string,object> d || !d.TryGetValue(field,out var value) || value is not Dictionary<string,object> langs)return fallback;
                            foreach(string lang in new[]{"latam","spanish","english"})if(langs.TryGetValue(lang,out var text) && text is string s && !String.IsNullOrWhiteSpace(s))return s;
                            return fallback;
                        }
                        names[id]=(Local("name",id),Local("desc",""));
                    }
                }
            }
            catch(Exception ex){log("Definiciones Steam no disponibles para INI: "+ex.Message);}
            return names;
        }
        public static GameEntry Load(string path,uint appId,string steamSchemaPath=null,Action<string> log=null)
        {
            if(appId==0)throw new InvalidDataException("El archivo INI necesita una carpeta con AppID válido.");
            log??=_=>{};
            var sections=Read(path);
            var states=sections.Where(s=>!String.Equals(s.Key,"SteamAchievements",StringComparison.OrdinalIgnoreCase) && s.Value.ContainsKey("Achieved")).ToList();
            if(states.Count==0)throw new InvalidDataException("No se encontraron secciones de logros con Achieved=0/1. Variante INI no admitida.");
            bool synthetic=states.All(s=>s.Key.StartsWith("ATR_DEMO_",StringComparison.Ordinal));
            var metadata=synthetic?new Dictionary<string,(string Name,string Description)>(StringComparer.OrdinalIgnoreCase):Metadata(steamSchemaPath,appId,log);
            var all=new HashSet<string>(metadata.Keys,StringComparer.OrdinalIgnoreCase);
            var game=new GameEntry{AppId=appId,AccountId=0,StatsPath=Path.GetFullPath(path),HistoryId=Identity(path),SourceLabel="achievements.ini (CODEX/RUNE)",GameName="AppID "+appId,HasLocalHistory=true,IsDemo=synthetic || File.Exists(Path.Combine(Path.GetDirectoryName(path),".atr-demo"))};
            foreach(var section in sections.Where(s=>!String.Equals(s.Key,"SteamAchievements",StringComparison.OrdinalIgnoreCase) && !s.Value.ContainsKey("Achieved")))log("INI: sección "+section.Key+" sin Achieved; no se infiere un desbloqueo a partir del progreso.");
            foreach(var state in states)
            {
                all.Add(state.Key);
                string achieved=state.Value["Achieved"];
                if(achieved!="0" && achieved!="1")throw new InvalidDataException("Achieved inválido en "+state.Key+": se admite 0 o 1.");
                if(achieved=="0")continue;
                if(!state.Value.TryGetValue("UnlockTime",out var text) || !UInt32.TryParse(text,NumberStyles.None,CultureInfo.InvariantCulture,out var unix) || unix==0)
                {log("INI: "+state.Key+" desbloqueado sin UnlockTime Unix válido; no se añade al plan.");continue;}
                var label=metadata.TryGetValue(state.Key,out var name)?name:(state.Key,"");
                game.Achievements.Add(new AchievementRecord{ApiId=state.Key,DisplayName=label.Item1,Description=label.Item2,OriginalUnix=unix});
            }
            game.TotalAchievements=all.Count;
            game.Achievements=game.Achievements.OrderBy(a=>a.OriginalUnix).ThenBy(a=>a.ApiId,StringComparer.Ordinal).ToList();
            if(game.IsDemo)game.GameName="Muestra CODEX/RUNE · AppID "+appId;
            DemoData.Apply(game,log);
            OriginDetector.Apply(game,null);return game;
        }
        public static List<GameEntry> Scan(string root,string steamRoot,string snapshots,Action<string> log,CancellationToken token,ScanSummary summary=null)
        {
            summary??=new ScanSummary();token.ThrowIfCancellationRequested();
            var result=new List<GameEntry>();var files=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            // Inspect only the selected root, its AppID children and their remote directories.
            var dirs=new List<string>{root};dirs.AddRange(Directory.GetDirectories(root));
            foreach(string dir in dirs.ToArray())if(Directory.Exists(Path.Combine(dir,"remote")))dirs.Add(Path.Combine(dir,"remote"));
            foreach(string dir in dirs)
            {
                token.ThrowIfCancellationRequested();
                foreach(string file in Directory.GetFiles(dir).Where(f=>String.Equals(Path.GetFileName(f),"achievements.ini",StringComparison.OrdinalIgnoreCase)))files.Add(file);
            }
            foreach(string path in files.OrderBy(p=>p,StringComparer.OrdinalIgnoreCase))
            {
                token.ThrowIfCancellationRequested();summary.Reviewed++;
                uint id=AppIdFromPath(path);
                if(id==0){summary.InvalidIds++;log("INI sin carpeta AppID válida: "+path);continue;}
                try
                {
                    string schema=String.IsNullOrWhiteSpace(steamRoot)?null:Path.Combine(steamRoot,"appcache","stats","UserGameStatsSchema_"+id+".bin");
                    var game=Load(path,id,schema,log);token.ThrowIfCancellationRequested();
                    if(game.Achievements.Count==0){summary.NoDates++;log("INI AppID "+id+": sin desbloqueos con fecha válida.");continue;}
                    try{result.Add(HistorySnapshots.Protect(game,snapshots));summary.Recovered++;}
                    catch(Exception ex){summary.SnapshotErrors++;log("INI AppID "+id+": error de snapshot: "+ex.Message);continue;}
                    log("INI importado: "+path);
                }
                catch(OperationCanceledException){throw;}
                catch(Exception ex){summary.ReadErrors++;log("INI omitido "+path+": "+ex.Message);}
            }
            token.ThrowIfCancellationRequested();return result;
        }
    }
}
