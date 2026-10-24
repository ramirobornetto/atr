using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AchievementTimelineReplay
{
    internal sealed class MigrationItem
    {
        public string ApiId {get;set;}
        public string Name {get;set;}
        public string Status {get;set;}="Pendiente";
        public string Detail {get;set;}
    }
    internal sealed class MigrationRun
    {
        public int Version {get;set;}=1;
        public string Id {get;set;}=Guid.NewGuid().ToString("N");
        public string SourceKey {get;set;}
        public string SourceLabel {get;set;}
        public string SourcePath {get;set;}
        public uint SteamAccount {get;set;}
        public DateTime StartedUtc {get;set;}=DateTime.UtcNow;
        public DateTime UpdatedUtc {get;set;}=DateTime.UtcNow;
        public string Outcome {get;set;}="Preparada";
        public string Note {get;set;}
        public SavedPlan Plan {get;set;}
        public List<MigrationItem> Items {get;set;}=new List<MigrationItem>();
        public string Summary=>String.Join(" · ",Items.GroupBy(i=>i.Status).Select(g=>g.Count()+" "+g.Key.ToLowerInvariant()));
        public string Report()=>"Migración: "+Plan.GameName+" · AppID "+Plan.AppId+Environment.NewLine+"Historial: "+SourceLabel+Environment.NewLine+"Archivo: "+SourcePath+Environment.NewLine+"Cuenta Steam: "+(SteamAccount==0?"no identificada":SteamAccount.ToString())+Environment.NewLine+"Resultado: "+Outcome+" · "+UpdatedUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss")+Environment.NewLine+Summary+Environment.NewLine+Note+Environment.NewLine+String.Join(Environment.NewLine,Items.Select(i=>i.Status+" · "+i.Name+" ["+i.ApiId+"]"+(String.IsNullOrEmpty(i.Detail)?"":" · "+i.Detail)));
        public void Action(string api,string state)
        {
            var item=Items.Single(i=>String.Equals(i.ApiId,api,StringComparison.OrdinalIgnoreCase));
            item.Status=state;item.Detail=state=="Aplicado sin verificar"?"SAM aceptó la operación; falta comprobar el estado final.":null;
        }
        public void Reconcile(IReadOnlyDictionary<string,string> states)
        {
            foreach(var item in Items.Where(i=>i.Status!="Ya desbloqueado"))
            {
                if(!states.TryGetValue(item.ApiId,out var state))continue;
                if(SamRunner.IsUnlocked(state))
                {
                    // A state change without an attempted write is not attributed to ATR.
                    if(item.Status=="En curso" || item.Status=="Aplicado sin verificar")
                    {item.Status="Aplicado";item.Detail="Estado desbloqueado confirmado en Steam al finalizar.";}
                    else {bool rejected=item.Status=="Fallido";item.Status="Ya desbloqueado";item.Detail=rejected?"SAM informó un error; Steam indica desbloqueado, sin atribuir el cambio a ATR.":"Desbloqueado durante la ejecución; ATR no registró un intento para este logro.";}
                }
                else if(item.Status=="Aplicado sin verificar" || item.Status=="En curso")
                {item.Status="Fallido";item.Detail="Steam sigue informando el logro bloqueado.";}
            }
        }
    }
    internal sealed class MigrationPreview
    {
        public readonly List<MigrationItem> Items=new List<MigrationItem>();
        public readonly Dictionary<AchievementRecord,ScheduleItem> Schedule=new Dictionary<AchievementRecord,ScheduleItem>();
        public int Selected=>Items.Count;
        public int Already=>Items.Count(i=>i.Status=="Ya desbloqueado");
        public int Missing=>Items.Count(i=>i.Status=="No encontrado");
        public bool Verified;
        public static MigrationPreview Build(List<AchievementRecord> records,PlanParameters parameters,IReadOnlyDictionary<string,string> states)
        {
            var preview=new MigrationPreview{Verified=states!=null};
            if(states!=null && states.Count==0)throw new InvalidDataException("SAM no devolvió estados actuales utilizables.");
            foreach(var record in records.Where(a=>a.Included))
            {
                string state=states!=null && states.TryGetValue(record.ApiId,out var current)?current:null;
                preview.Items.Add(new MigrationItem{ApiId=record.ApiId,Name=record.DisplayName,Status=states!=null && state==null?"No encontrado":!parameters.Repair && SamRunner.IsUnlocked(state)?"Ya desbloqueado":"Pendiente",Detail=states==null?"CLI legacy: estado actual sin verificar.":parameters.Repair && SamRunner.IsUnlocked(state)?"Modo reparación: se bloqueará y desbloqueará nuevamente.":null});
            }
            var pending=new HashSet<string>(preview.Items.Where(i=>i.Status=="Pendiente").Select(i=>i.ApiId),StringComparer.OrdinalIgnoreCase);
            var full=LegacyPlanner.Calculate(records,parameters.DurationSeconds,parameters.LimitLongGaps?parameters.MaximumGapMinutes*60:null).Values.Where(i=>pending.Contains(i.Achievement.ApiId)).OrderBy(i=>i.OffsetSeconds).ToList();
            double first=full.Count==0?0:full[0].OffsetSeconds,previous=0;
            foreach(var item in full)
            {
                double offset=item.OffsetSeconds-first;
                preview.Schedule[item.Achievement]=new ScheduleItem{Achievement=item.Achievement,OffsetSeconds=offset,GapSeconds=offset-previous};previous=offset;
            }
            return preview;
        }
        public string PendingIdentity=>String.Join("\n",Items.Select(i=>i.ApiId+"|"+i.Status));
    }
    internal sealed class MigrationRunStore
    {
        readonly string directory;
        public MigrationRunStore(string directory){this.directory=directory;}
        string Folder(GameEntry game)
        {
            if(!Regex.IsMatch(game.StorageKey,@"^(?:(?:goldberg|codex_ini|pa|mapped|xbox|epic)_[A-F0-9]{24}|[0-9]+)_[0-9]+$"))throw new InvalidDataException("Identidad de migración inválida.");
            return Path.Combine(directory,game.StorageKey);
        }
        public string LatestPath(GameEntry game)=>Path.Combine(Folder(game),"latest.json");
        public bool Exists(GameEntry game)=>File.Exists(LatestPath(game));
        static void Atomic(string path,string json)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try{using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){var bytes=Encoding.UTF8.GetBytes(json);stream.Write(bytes);stream.Flush(true);}File.Move(temp,path,true);}finally{if(File.Exists(temp))File.Delete(temp);}
        }
        public void Save(GameEntry game,MigrationRun run)
        {
            if(run.SourceKey!=game.StorageKey || run.Plan?.AppId!=game.AppId || !Regex.IsMatch(run.Id??"",@"^[a-f0-9]{32}$"))throw new InvalidDataException("Informe de otro historial o juego.");
            run.UpdatedUtc=DateTime.UtcNow;string json=JsonSerializer.Serialize(run,new JsonSerializerOptions{WriteIndented=true});
            Atomic(Path.Combine(Folder(game),run.Id+".json"),json);Atomic(LatestPath(game),json);
        }
        public MigrationRun Load(GameEntry game)
        {
            string path=LatestPath(game);if(new FileInfo(path).Length>16*1024*1024)throw new InvalidDataException("Informe demasiado grande.");
            var run=JsonSerializer.Deserialize<MigrationRun>(File.ReadAllText(path));
            if(run==null || run.Version!=1 || run.SourceKey!=game.StorageKey || run.Plan==null || run.Plan.AppId!=game.AppId || run.Items==null || run.Items.Count>50000 || run.Items.Any(i=>i==null || String.IsNullOrWhiteSpace(i.ApiId)) || run.Items.Select(i=>i.ApiId).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=run.Items.Count)throw new InvalidDataException("Informe de otro historial o formato inválido.");
            PlanStore.Restore(game,run.Plan);
            if(run.Outcome=="En curso")
            {
                run.Outcome="Interrumpida";run.Note=(run.Note??"")+"\nSe recuperó una ejecución que no llegó a cerrarse. Consultar Steam antes de continuar.";
                foreach(var item in run.Items.Where(i=>i.Status=="En curso"))item.Status="Sin verificar";
            }
            return run;
        }
    }
}
