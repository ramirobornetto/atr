// Puente de lógica de ATR 0.5.3 para la interfaz WPF.
// Los modelos y el lector de Binary KeyValues permanecen en SteamHistory.cs,
// extraídos del archivo original sin modificar sus algoritmos.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AchievementTimelineReplay
{
    internal sealed class ScanStatus
    {
        public string Message { get; set; } = "";
        public int Current { get; set; }
        public int Total { get; set; }
        public bool Indeterminate { get; set; }
    }

    internal static class LegacyTools
    {
        public static string DataDirectory
        {
            get
            {
                var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AchievementTimelineReplay");
                Directory.CreateDirectory(path);
                return path;
            }
        }
        public static string NamesPath => Path.Combine(DataDirectory, "steam_public_names.tsv");
        public static string SettingsPath => Path.Combine(DataDirectory, "settings.txt");
        public static string FormatSpan(long totalSeconds)
        {
            if (totalSeconds < 0) totalSeconds = 0;
            long d = totalSeconds / 86400, h = totalSeconds % 86400 / 3600, m = totalSeconds % 3600 / 60, s = totalSeconds % 60;
            return d > 0 ? String.Format("{0}d {1:00}:{2:00}:{3:00}", d, h, m, s) : String.Format("{0:00}:{1:00}:{2:00}", h, m, s);
        }
        public static DateTime UnixToLocal(long unix)
        {
            try { return DateTimeOffset.FromUnixTimeSeconds(unix).LocalDateTime; }
            catch { return DateTimeOffset.UnixEpoch.LocalDateTime; }
        }
        public static string DetectSteam()
        {
            try { using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"); var p = k?.GetValue("SteamPath") as string; if (Directory.Exists(p)) return p.Replace('/', '\\'); } catch { }
            try { using var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam"); var p = k?.GetValue("InstallPath") as string; if (Directory.Exists(p)) return p; } catch { }
            var fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
            return Directory.Exists(fallback) ? fallback : "";
        }
        public static string DetectSam()
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dirs = new[] { AppContext.BaseDirectory, Path.Combine(local,"Programs","SamRewritten"), Path.Combine(local,"SamRewritten"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"SamRewritten"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"SamRewritten") };
            var names = new[] {"samrewritten-cli.exe","SamRewritten-cli.exe","samrewritten.exe","SamRewritten.exe"};
            foreach (var d in dirs)
            {
                if (!Directory.Exists(d)) continue;
                foreach (var n in names) { var p = Path.Combine(d,n); if (File.Exists(p)) return p; }
                try { foreach (var n in names) { var matches = Directory.GetFiles(d,n,SearchOption.AllDirectories); if (matches.Length > 0) return matches[0]; } } catch { }
            }
            return "";
        }
        public static Dictionary<string,string> ReadSettings()
        {
            var d = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            try { foreach (var line in File.ReadAllLines(SettingsPath)) { var i=line.IndexOf('='); if (i>0) d[line.Substring(0,i)]=line.Substring(i+1); } } catch { }
            return d;
        }
        public static void WriteSettings(string steam, string sam)
        {
            try { File.WriteAllLines(SettingsPath,new[]{"steam=" + steam,"sam=" + sam}); } catch { }
        }
        public static string QuoteArg(string s) => s == null ? "\"\"" : "\"" + s.Replace("\\","\\\\").Replace("\"","\\\"") + "\"";
        public static string RepairMojibake(string value)
        {
            // Copia de RepairMojibake / MojibakeScore de AchievementTimelineReplay.cs v0.5.3.
            if (String.IsNullOrEmpty(value)) return value;
            int before = MojibakeScore(value);
            if (before == 0) return value;
            try
            {
                Encoding windows1252 = Encoding.GetEncoding(1252);
                byte[] bytes = windows1252.GetBytes(value);
                string repaired = Encoding.UTF8.GetString(bytes);
                if (MojibakeScore(repaired) < before) return repaired;
            }
            catch { }
            return value;
        }
        static int MojibakeScore(string value)
        {
            if (String.IsNullOrEmpty(value)) return 0;
            int score=0;
            foreach(char c in value)
                if(c=='Ã' || c=='Â' || c=='â' || c=='ð' || c=='�') score++;
            string[] fragments={"â„¢","â€“","â€”","â€™","â€œ","â€","Â®","Â©"};
            foreach(string fragment in fragments)
            {
                int start=0;
                while(true)
                {
                    int found=value.IndexOf(fragment,start,StringComparison.Ordinal);
                    if(found<0) break;
                    score+=3;
                    start=found+fragment.Length;
                }
            }
            return score;
        }
        public static bool TryParseDuration(string raw, out double seconds)
        {
            seconds = 0;
            if (String.IsNullOrWhiteSpace(raw)) return false;
            string s = raw.Trim().ToLowerInvariant().Replace(",", ".");
            Match m = Regex.Match(s, @"^(\d+)\s*d\s+(\d{1,2}):(\d{1,2}):(\d{1,2})$");
            if (m.Success) { seconds = double.Parse(m.Groups[1].Value,CultureInfo.InvariantCulture)*86400 + double.Parse(m.Groups[2].Value,CultureInfo.InvariantCulture)*3600 + double.Parse(m.Groups[3].Value,CultureInfo.InvariantCulture)*60 + double.Parse(m.Groups[4].Value,CultureInfo.InvariantCulture); return true; }
            m = Regex.Match(s, @"^(\d+)\.(\d{1,2}):(\d{1,2}):(\d{1,2})$");
            if (m.Success) { seconds = double.Parse(m.Groups[1].Value,CultureInfo.InvariantCulture)*86400 + double.Parse(m.Groups[2].Value,CultureInfo.InvariantCulture)*3600 + double.Parse(m.Groups[3].Value,CultureInfo.InvariantCulture)*60 + double.Parse(m.Groups[4].Value,CultureInfo.InvariantCulture); return true; }
            m = Regex.Match(s, @"^(\d+):(\d{1,2}):(\d{1,2})$");
            if (m.Success) { seconds = double.Parse(m.Groups[1].Value,CultureInfo.InvariantCulture)*3600 + double.Parse(m.Groups[2].Value,CultureInfo.InvariantCulture)*60 + double.Parse(m.Groups[3].Value,CultureInfo.InvariantCulture); return true; }
            m = Regex.Match(s, @"^\s*(?:(\d+(?:\.\d+)?)\s*d)?\s*(?:(\d+(?:\.\d+)?)\s*h)?\s*(?:(\d+(?:\.\d+)?)\s*m)?\s*(?:(\d+(?:\.\d+)?)\s*s)?\s*$");
            if (m.Success && Enumerable.Range(1,4).Any(i => m.Groups[i].Success))
            {
                double[] unit = {86400,3600,60,1};
                for (int i=1;i<=4;i++) if (m.Groups[i].Success) seconds += double.Parse(m.Groups[i].Value,CultureInfo.InvariantCulture)*unit[i-1];
                return true;
            }
            return false;
        }
    }

    internal static class SamRunner
    {
        public static ProcessResult RunProcess(string file,string args,int timeoutMs)
        {
            var r = new ProcessResult { ExitCode=-1, StdOut="", StdErr="" };
            try
            {
                using var p = new Process();
                p.StartInfo = new ProcessStartInfo { FileName=file, Arguments=args, UseShellExecute=false, CreateNoWindow=true, RedirectStandardOutput=true, RedirectStandardError=true, StandardOutputEncoding=Encoding.UTF8, StandardErrorEncoding=Encoding.UTF8, WorkingDirectory=Path.GetDirectoryName(file) ?? AppContext.BaseDirectory };
                p.Start();
                // Consumo simultáneo evita bloqueo cuando stderr/stdout llenan el buffer.
                var stdout = p.StandardOutput.ReadToEndAsync();
                var stderr = p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit(timeoutMs)) { r.TimedOut=true; try { p.Kill(true); } catch { } p.WaitForExit(5000); }
                else r.ExitCode=p.ExitCode;
                r.StdOut=stdout.GetAwaiter().GetResult(); r.StdErr=stderr.GetAwaiter().GetResult();
            }
            catch(Exception ex) { r.StdErr=ex.ToString(); }
            return r;
        }
        public static SamCliMode DetectMode(string path)
        {
            if (!File.Exists(path)) return SamCliMode.Unknown;
            var r=RunProcess(path,"--help",15000);
            var t=(r.StdOut+"\n"+r.StdErr).ToLowerInvariant();
            if(t.Contains("list-achievements") && t.Contains("list-apps") && t.Contains("unlock")) return SamCliMode.Modern;
            if(t.Contains("--unlock") && t.Contains("--lock")) return SamCliMode.Legacy;
            return SamCliMode.Unknown;
        }
        public static ProcessResult List(string path,SamCliMode mode,uint appId) => RunProcess(path,mode == SamCliMode.Modern ? "list-achievements " + appId : appId + " --ls --nostats",60000);
        public static ProcessResult Set(string path,SamCliMode mode,uint appId,List<string> ids,bool unlock)
        {
            string args;
            if (mode==SamCliMode.Modern) args=(unlock ? "unlock " : "lock ")+appId+string.Concat(ids.Select(id=>" "+LegacyTools.QuoteArg(id)));
            else args=appId+(unlock ? " --unlock " : " --lock ")+LegacyTools.QuoteArg(string.Join(",",ids));
            return RunProcess(path,args,90000);
        }
        public static bool Failed(ProcessResult r)
        {
            string text=(r.StdOut+"\n"+r.StdErr).ToLowerInvariant();
            return r.TimedOut || r.ExitCode != 0 || text.Contains("\"success\": false") || text.Contains("\"success\":false") || text.Contains("failed to ");
        }
        public static bool IsUnlocked(string state) => state=="Desbloqueado" || (state?.StartsWith("✓ ",StringComparison.Ordinal)??false);
        public static int ApplySteamStates(IEnumerable<AchievementRecord> achievements,Dictionary<string,string> states,bool omitUnlocked)
        {
            int omitted=0;
            foreach(var a in achievements)
            {
                a.SteamState=states.TryGetValue(a.ApiId,out var state)?state:"No encontrado";
                if(omitUnlocked && IsUnlocked(a.SteamState) && a.Included)
                {
                    a.Included=false;
                    omitted++;
                }
            }
            return omitted;
        }
        public static Dictionary<string,string> States(string response)
        {
            var d=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            using var doc=JsonDocument.Parse(response);
            if(doc.RootElement.ValueKind != JsonValueKind.Array) return d;
            foreach(var item in doc.RootElement.EnumerateArray())
            {
                if(!item.TryGetProperty("id",out var id)) continue;
                var api=id.GetString(); if(String.IsNullOrEmpty(api)) continue;
                bool achieved=item.TryGetProperty("is_achieved",out var v) && v.ValueKind==JsonValueKind.True;
                string state=achieved ? "Desbloqueado" : "Bloqueado";
                if(achieved && item.TryGetProperty("unlock_time",out var ts))
                {
                    long unix=0;
                    if(ts.ValueKind==JsonValueKind.Number) ts.TryGetInt64(out unix);
                    else if(ts.ValueKind==JsonValueKind.String) long.TryParse(ts.GetString(),out unix);
                    if(unix>0) state="✓ " + LegacyTools.UnixToLocal(unix).ToString("dd/MM HH:mm");
                }
                d[api]=state;
            }
            return d;
        }
    }

    internal sealed class LegacyScanner
    {
        readonly Action<string> log;
        public LegacyScanner(Action<string> logger) { log=logger; }
        static uint RecentAccount(string root)
        {
            try
            {
                string file=Path.Combine(root,"config","loginusers.vdf");
                if(!File.Exists(file)) return 0;
                foreach(Match m in Regex.Matches(File.ReadAllText(file),"\"(\\d{17})\"\\s*\\{(.*?)\\}",RegexOptions.Singleline))
                {
                    if(Regex.IsMatch(m.Groups[2].Value,"\"MostRecent\"\\s*\"1\"",RegexOptions.IgnoreCase))
                    {
                        ulong steamId=ulong.Parse(m.Groups[1].Value,CultureInfo.InvariantCulture);
                        const ulong baseId=76561197960265728UL;
                        if(steamId>=baseId) return (uint)(steamId-baseId);
                    }
                }
            } catch { }
            return 0;
        }
        static Dictionary<uint,string> LoadNames()
        {
            var d=new Dictionary<uint,string>();
            try
            {
                foreach(var line in File.ReadAllLines(LegacyTools.NamesPath,Encoding.UTF8))
                {
                    int i=line.IndexOf('\t');
                    if(i<1 || !uint.TryParse(line.Substring(0,i),out uint id)) continue;
                    string name=line.Substring(i+1).Replace("\\t","\t").Replace("\\n","\n").Replace("\\r","\r").Replace("\\\\","\\");
                    if(!String.IsNullOrWhiteSpace(name)) d[id]=LegacyTools.RepairMojibake(name);
                }
            } catch { }
            return d;
        }
        static void SaveNames(Dictionary<uint,string> names)
        {
            try
            {
                File.WriteAllLines(LegacyTools.NamesPath,names.OrderBy(kv=>kv.Key).Select(kv=>kv.Key.ToString(CultureInfo.InvariantCulture)+"\t"+kv.Value.Replace("\\","\\\\").Replace("\t","\\t").Replace("\r","\\r").Replace("\n","\\n")),Encoding.UTF8);
            } catch { }
        }
        static void Report(IProgress<ScanStatus> p,string text,int i=0,int n=0,bool ind=false) => p?.Report(new ScanStatus{Message=text,Current=i,Total=n,Indeterminate=ind});
        public List<GameEntry> Scan(string steamRoot,string samPath,SamCliMode samMode,IProgress<ScanStatus> progress,CancellationToken token)
        {
            string statsDir=Path.Combine(steamRoot,"appcache","stats");
            if(!Directory.Exists(statsDir)) throw new DirectoryNotFoundException("No encontré Steam\\appcache\\stats: "+statsDir);
            var samNames=new Dictionary<uint,string>();
            if(samMode==SamCliMode.Modern)
            {
                token.ThrowIfCancellationRequested();
                Report(progress,"Leyendo biblioteca y nombres conocidos con SAM…",ind:true);
                var apps=SamRunner.RunProcess(samPath,"list-apps",180000);
                if(!apps.TimedOut && apps.ExitCode==0)
                {
                    try
                    {
                        using var doc=JsonDocument.Parse(apps.StdOut);
                        if(doc.RootElement.ValueKind==JsonValueKind.Array)
                        foreach(var item in doc.RootElement.EnumerateArray())
                        {
                            if(!item.TryGetProperty("app_id",out var v) || !v.TryGetUInt32(out uint id) || id==0) continue;
                            if(item.TryGetProperty("app_type",out var type) && type.GetString()=="Junk") continue;
                            if(item.TryGetProperty("app_name",out var name))
                            {
                                string appName=LegacyTools.RepairMojibake(name.GetString());
                                if(!String.IsNullOrWhiteSpace(appName)) samNames[id]=appName;
                            }
                        }
                    } catch(Exception ex) { log("Aviso: biblioteca SAM: "+ex.Message); }
                }
                else log("SAM no pudo enumerar la biblioteca; continúo con los historiales locales.");
            }
            token.ThrowIfCancellationRequested();
            var stats=Directory.GetFiles(statsDir,"UserGameStats_*_*.bin",SearchOption.TopDirectoryOnly).ToList();
            uint recent=RecentAccount(steamRoot);
            if(recent != 0)
            {
                var filtered=stats.Where(f=>{var m=Regex.Match(Path.GetFileName(f),@"^UserGameStats_(\d+)_(\d+)\.bin$",RegexOptions.IgnoreCase);return m.Success && uint.TryParse(m.Groups[1].Value,out var a) && a==recent;}).ToList();
                if(filtered.Count>0) stats=filtered;
            }
            var byApp=new Dictionary<uint,GameEntry>();
            for(int i=0;i<stats.Count;i++)
            {
                token.ThrowIfCancellationRequested();
                string f=stats[i];
                var m=Regex.Match(Path.GetFileName(f),@"^UserGameStats_(\d+)_(\d+)\.bin$",RegexOptions.IgnoreCase);
                if(!m.Success || !uint.TryParse(m.Groups[1].Value,out var account) || !uint.TryParse(m.Groups[2].Value,out var app)) continue;
                Report(progress,$"Historiales: {i+1}/{stats.Count} · AppID {app}",i+1,stats.Count);
                string schema=Path.Combine(statsDir,"UserGameStatsSchema_"+app.ToString(CultureInfo.InvariantCulture)+".bin");
                if(!File.Exists(schema)) continue;
                try
                {
                    var game=HistoryLoader.Load(f,schema,account,app);
                    if(game?.Achievements==null || game.Achievements.Count==0) continue;
                    game.IsOwned=samNames.ContainsKey(app);
                    byApp[app]=game;
                }
                catch(Exception ex) { log("Error leyendo AppID "+app+": "+ex.Message); }
            }
            token.ThrowIfCancellationRequested();
            var names=LoadNames();
            foreach(var kv in samNames) names[kv.Key]=kv.Value;
            var missing=new HashSet<uint>(byApp.Keys.Where(id=>!names.ContainsKey(id)));
            if(missing.Count>0)
            {
                Report(progress,"Resolviendo "+missing.Count+" nombres públicos en Steam…",ind:true);
                try
                {
                    using var client=new HttpClient{Timeout=TimeSpan.FromSeconds(50)};
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("AchievementTimelineReplay/0.5.3");
                    using var request=new HttpRequestMessage(HttpMethod.Get,"https://api.steampowered.com/ISteamApps/GetAppList/v2/");
                    using var response=client.Send(request,token);
                    response.EnsureSuccessStatusCode();
                    using var doc=JsonDocument.Parse(response.Content.ReadAsStream());
                    foreach(var app in doc.RootElement.GetProperty("applist").GetProperty("apps").EnumerateArray())
                    {
                        token.ThrowIfCancellationRequested();
                        if(missing.Count==0) break;
                        if(!app.GetProperty("appid").TryGetUInt32(out uint id) || !missing.Contains(id)) continue;
                        string name=LegacyTools.RepairMojibake(app.GetProperty("name").GetString());
                        if(String.IsNullOrWhiteSpace(name)) continue;
                        names[id]=name; missing.Remove(id);
                    }
                }
                catch(Exception ex) { log("No pude actualizar nombres públicos desde Steam: "+ex.Message+". Uso caché o nombre interno."); }
            }
            SaveNames(names);
            var result=new List<GameEntry>();
            int n=byApp.Count, ix=0;
            foreach(var item in byApp.Values)
            {
                token.ThrowIfCancellationRequested();
                if(names.TryGetValue(item.AppId,out var publicName) && !String.IsNullOrWhiteSpace(publicName)) item.GameName=publicName;
                else if(String.IsNullOrWhiteSpace(item.GameName)) item.GameName="AppID "+item.AppId;
                try {result.Add(LoadOrCreateSnapshot(item));}
                catch(Exception ex) {log("Historial omitido para proteger sus datos: "+ex.Message);}
                Report(progress,$"Snapshots: {++ix}/{n} · {item.AppId}",ix,n);
            }
            return result.OrderBy(g=>g.GameName,StringComparer.CurrentCultureIgnoreCase).ToList();
        }
        GameEntry LoadOrCreateSnapshot(GameEntry current)
        {
            var dir=Path.Combine(LegacyTools.DataDirectory,"Snapshots",current.AccountId.ToString(CultureInfo.InvariantCulture)+"_"+current.AppId.ToString(CultureInfo.InvariantCulture));
            string jsonPath=Path.Combine(dir,"snapshot.json");
            if(File.Exists(jsonPath))
            {
                try
                {
                    // Lee tanto los JSON de JavaScriptSerializer (.NET Framework 4.x)
                    // como los de System.Text.Json, SIN reescribir historiales existentes.
                    // Ignora la forma de CreatedAtUtc: en 0.5.3 es /Date(...)/.
                    using var doc=JsonDocument.Parse(File.ReadAllText(jsonPath));
                    var root=doc.RootElement;
                    var ach=root.GetProperty("Achievements");
                    if(ach.ValueKind==JsonValueKind.Array && ach.GetArrayLength()>0)
                    {
                        uint dataApp=root.GetProperty("AppId").GetUInt32();
                        uint dataAccount=root.GetProperty("AccountId").GetUInt32();
                        if(dataApp!=current.AppId || dataAccount!=current.AccountId)
                            throw new InvalidDataException("El snapshot pertenece a otra cuenta o juego.");
                        var game=new GameEntry{AppId=current.AppId,AccountId=current.AccountId,GameName=current.GameName,IsOwned=current.IsOwned,HasLocalHistory=true,HasSnapshot=true,TotalAchievements=current.TotalAchievements,StatsPath=current.StatsPath,SchemaPath=current.SchemaPath};
                        foreach(var a in ach.EnumerateArray()) game.Achievements.Add(new AchievementRecord
                        {
                            ApiId=a.GetProperty("ApiId").GetString(),
                            DisplayName=a.GetProperty("DisplayName").GetString(),
                            Description=a.TryGetProperty("Description",out var description)?description.GetString():"",
                            OriginalUnix=a.GetProperty("OriginalUnix").GetInt64()
                        });
                        return game;
                    }
                    throw new InvalidDataException("El snapshot no contiene logros.");
                }
                catch(Exception ex)
                {
                    // NO sobrescribir nunca un snapshot preexistente. Esto protegerá
                    // los desbloqueos originales aun ante un JSON dañado o extraño.
                    throw new InvalidDataException("Snapshot existente de AppID "+current.AppId+" ilegible. No lo sobrescribí: "+ex.Message,ex);
                }
            }
            Directory.CreateDirectory(dir);
            var achievements=current.Achievements.Select(a=>new SnapshotAchievement{ApiId=a.ApiId,DisplayName=a.DisplayName,Description=a.Description,OriginalUnix=a.OriginalUnix}).ToList();
            // JavaScriptSerializer v0.5.3 escribe CreatedAtUtc en formato Microsoft /Date(...)/.
            // Escribir del mismo modo permite abrir snapshots nuevos desde 0.5.3.
            string date="/Date("+DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()+")/";
            var payload=new{Version=1,AppId=current.AppId,AccountId=current.AccountId,GameName=current.GameName,CreatedAtUtc=date,Achievements=achievements};
            File.WriteAllText(jsonPath,JsonSerializer.Serialize(payload),Encoding.UTF8);
            try {File.Copy(current.StatsPath,Path.Combine(dir,"original_UserGameStats.bin"),false);} catch { }
            try {File.Copy(current.SchemaPath,Path.Combine(dir,"original_UserGameStatsSchema.bin"),false);} catch { }
            current.HasSnapshot=true;
            log("Snapshot protegido creado para "+current.GameName+" ["+current.AppId+"].");
            return current;
        }
    }

    internal static class LegacyPlanner
    {
        public static long OriginalSpan(List<AchievementRecord> order)
        {
            var list=order.Where(a=>a.Included).OrderBy(a=>a.OriginalUnix).ToList();
            return list.Count<2?0:Math.Max(0,list[list.Count-1].OriginalUnix-list[0].OriginalUnix);
        }
        public static Dictionary<AchievementRecord,ScheduleItem> Calculate(List<AchievementRecord> order,double targetSpan,double? maximumAutomaticGapSeconds=null)
        {
            // Cálculo conservado de MainForm.CalculateSchedule en v0.5.3.
            var result=new Dictionary<AchievementRecord,ScheduleItem>();
            var replayOrder=order.Where(a=>a.Included).ToList();
            if(replayOrder.Count==0) return result;
            var chronological=replayOrder.OrderBy(a=>a.OriginalUnix).ToList();
            var originalGaps=new List<double>{0};
            for(int i=1;i<chronological.Count;i++) originalGaps.Add(Math.Max(0,(double)(chronological[i].OriginalUnix-chronological[i-1].OriginalUnix)));
            double originalSpan=originalGaps.Sum();
            double factor=originalSpan>0 ? targetSpan/originalSpan : 0;
            double cumulative=0;
            for(int i=0;i<replayOrder.Count;i++)
            {
                var a=replayOrder[i];
                double autoGap=i==0?0:originalGaps[i]*factor;
                // Limita solo las esperas automáticas; los tiempos explícitos conservan prioridad.
                if(maximumAutomaticGapSeconds.HasValue)
                {
                    if(!double.IsFinite(maximumAutomaticGapSeconds.Value) || maximumAutomaticGapSeconds.Value<=0)
                        throw new ArgumentOutOfRangeException(nameof(maximumAutomaticGapSeconds));
                    autoGap=Math.Min(autoGap,maximumAutomaticGapSeconds.Value);
                }
                double gap=a.ManualGapSeconds??autoGap;
                if(i==0 || gap<0) gap=0;
                cumulative+=gap;
                result[a]=new ScheduleItem{Achievement=a,GapSeconds=gap,OffsetSeconds=cumulative};
            }
            return result;
        }
        public static Dictionary<AchievementRecord,double> OriginalGaps(List<AchievementRecord> order)
        {
            var result=new Dictionary<AchievementRecord,double>();
            var chronological=order.Where(a=>a.Included).OrderBy(a=>a.OriginalUnix).ToList();
            for(int i=0;i<chronological.Count;i++) result[chronological[i]]=i==0?0:Math.Max(0,(double)(chronological[i].OriginalUnix-chronological[i-1].OriginalUnix));
            return result;
        }
    }

    internal sealed class ReplayEvents
    {
        public Action<string> Log;
        public Action<string> Status;
        public Action<int,int,string> Progress;
    }
    internal sealed class LegacyReplayer
    {
        readonly ReplayEvents ev;
        public LegacyReplayer(ReplayEvents events) {ev=events;}
        void Log(string s)=>ev.Log?.Invoke(s);
        void Status(string s)=>ev.Status?.Invoke(s);
        void Progress(int done,int total,string a)=>ev.Progress?.Invoke(done,total,a);
        static void Check(ProcessResult r,string action)
        {
            if(SamRunner.Failed(r)) throw new InvalidOperationException("SamRewritten informó un error al "+action+". "+r.StdErr+" "+r.StdOut);
        }
        static Process StartIdle(string cli,uint appId,bool enable,SamCliMode mode,Action<string> log,bool verbose=true)
        {
            if(!enable) return null;
            if(mode!=SamCliMode.Modern){log("SAM idle omitido: requiere CLI moderno.");return null;}
            try
            {
                string cmd=Environment.GetEnvironmentVariable("COMSPEC");
                if(String.IsNullOrWhiteSpace(cmd)) cmd="cmd.exe";
                var psi=new ProcessStartInfo{FileName=cmd, Arguments="/D /S /C \"\""+cli+"\" idle "+appId.ToString(CultureInfo.InvariantCulture)+"\"", UseShellExecute=true, WindowStyle=ProcessWindowStyle.Hidden, WorkingDirectory=Path.GetDirectoryName(cli)};
                var p=Process.Start(psi);
                if(p!=null && verbose) log("SAM idle iniciado vía cmd.exe para AppID "+appId+".");
                return p;
            }catch(Exception ex){log("No pude iniciar SAM idle: "+ex.Message);return null;}
        }
        static void StopIdle(Process p,Action<string> log,bool verbose=true)
        {
            if(p==null) return;
            try
            {
                if(!p.HasExited)
                {
                    var psi=new ProcessStartInfo{FileName="taskkill.exe",Arguments="/PID "+p.Id+" /T /F",UseShellExecute=false,CreateNoWindow=true};
                    using(var killer=Process.Start(psi)) killer?.WaitForExit(10000);
                    try {if(!p.HasExited)p.Kill(true);} catch { }
                    try {p.WaitForExit(5000);} catch { }
                }
                if(verbose) log("SAM idle detenido.");
            }catch(Exception ex){log("Aviso al detener SAM idle: "+ex.Message);}finally{p.Dispose();}
        }
        public async Task Run(List<ScheduleItem> plan,string cli,SamCliMode mode,uint appId,string name,bool lockFirst,bool idle,int initialDelay,CancellationToken cancellation)
        {
            await Task.Run(()=>RunSync(plan,cli,mode,appId,name,lockFirst,idle,initialDelay,cancellation));
        }
        void RunSync(List<ScheduleItem> plan,string cli,SamCliMode mode,uint appId,string name,bool lockFirst,bool idle,int initialDelay,CancellationToken cancellation)
        {
            Process idleProcess=null;
            try
            {
                if(lockFirst)
                {
                    Status("Bloqueando logros seleccionados…");
                    var r=SamRunner.Set(cli,mode,appId,plan.Select(x=>x.Achievement.ApiId).ToList(),false);
                    Log("LOCK: "+r.StdOut+" "+r.StdErr);
                    Check(r,"bloquear logros");
                }
                cancellation.ThrowIfCancellationRequested();
                idleProcess=StartIdle(cli,appId,idle,mode,Log);
                if(idle && mode==SamCliMode.Modern)
                {
                    Thread.Sleep(2000);
                    if(idleProcess==null || idleProcess.HasExited) throw new InvalidOperationException("No pude mantener activo SAM idle para registrar tiempo de juego.");
                }
                DateTime start=DateTime.Now.AddSeconds(initialDelay);
                foreach(var item in plan) item.Due=start.AddSeconds(item.OffsetSeconds);
                for(int i=0;i<plan.Count;i++)
                {
                    var item=plan[i];
                    while(true)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        TimeSpan left=item.Due-DateTime.Now;
                        if(left.TotalMilliseconds<=0) break;
                        Status("Próximo: "+item.Achievement.DisplayName+" en "+LegacyTools.FormatSpan((long)Math.Ceiling(left.TotalSeconds)));
                        Thread.Sleep((int)Math.Min(1000,Math.Max(100,left.TotalMilliseconds)));
                    }
                    cancellation.ThrowIfCancellationRequested();
                    Status("Desbloqueando: "+item.Achievement.DisplayName);
                    var r=SamRunner.Set(cli,mode,appId,new List<string>{item.Achievement.ApiId},true);
                    Log("UNLOCK "+item.Achievement.ApiId+": "+r.StdOut+" "+r.StdErr);
                    Check(r,"desbloquear "+item.Achievement.ApiId);
                    if(i<plan.Count-1 && idle && mode==SamCliMode.Modern)
                    {
                        StopIdle(idleProcess,Log,false);
                        idleProcess=StartIdle(cli,appId,true,mode,Log,false);
                        if(idleProcess==null) throw new InvalidOperationException("No pude reiniciar SAM idle después del desbloqueo.");
                        Thread.Sleep(700);
                        if(idleProcess.HasExited) throw new InvalidOperationException("SAM idle se cerró al intentar restaurar 'En ejecución'.");
                        Log("SAM idle restaurado después del logro.");
                    }
                    Progress(i+1,plan.Count,item.Achievement.DisplayName);
                }
                Log("=== Migración completada: "+name+" ===");
            }
            finally {StopIdle(idleProcess,Log);}
        }
    }
}
