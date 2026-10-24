using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace AchievementTimelineReplay
{
    internal sealed class PlanParameters
    {
        public double DurationSeconds { get; set; }
        public bool CustomOrder { get; set; }
        public int InitialDelaySeconds { get; set; } = 10;
        public bool Repair { get; set; }
        public bool Idle { get; set; } = true;
        public bool LimitLongGaps { get; set; }
        public double MaximumGapMinutes { get; set; } = 30;
    }
    internal sealed class SavedAchievement
    {
        public string ApiId { get; set; }
        public bool Included { get; set; }
        public double? ManualGapSeconds { get; set; }
    }
    internal sealed class SavedPlan
    {
        public int Version { get; set; } = 1;
        public uint AccountId { get; set; }
        public uint AppId { get; set; }
        public string HistoryId { get; set; }
        public string GameName { get; set; }
        public DateTime SavedAtUtc { get; set; }
        public PlanParameters Parameters { get; set; }
        public List<SavedAchievement> Achievements { get; set; }
    }
    internal sealed class RestoredPlan
    {
        public List<AchievementRecord> Records { get; set; }
        public int Missing { get; set; }
        public int Added { get; set; }
    }
    internal sealed class PlanStore
    {
        readonly string directory;
        public PlanStore(string directory) { this.directory=directory; }
        public string FilePath(GameEntry game) => Path.Combine(directory,game.StorageKey+".json");
        string RelatedPath(GameEntry game)=>game.AlternatePlanKey==null?null:Path.Combine(directory,game.AlternatePlanKey+".json");
        public bool Exists(GameEntry game) => File.Exists(FilePath(game)) || File.Exists(RelatedPath(game));
        public static string Signature(SavedPlan plan) => JsonSerializer.Serialize(new{plan.Parameters,plan.Achievements});
        public static SavedPlan Capture(GameEntry game,IEnumerable<AchievementRecord> order,PlanParameters parameters)
        {
            var plan=new SavedPlan
            {
                AccountId=game.AccountId,AppId=game.AppId,HistoryId=game.HistoryId,GameName=game.GameName,
                SavedAtUtc=DateTime.UtcNow,Parameters=parameters,
                Achievements=order.Select(a=>new SavedAchievement{ApiId=a.ApiId,Included=a.Included,ManualGapSeconds=a.ManualGapSeconds}).ToList()
            };
            Validate(plan,game);
            return plan;
        }
        static void Validate(SavedPlan plan,GameEntry game)
        {
            if(plan==null || plan.Version!=1 || plan.AppId!=game.AppId || ((plan.AccountId!=game.AccountId || plan.HistoryId!=game.HistoryId) && game.AlternatePlanKey!=(String.IsNullOrEmpty(plan.HistoryId)?plan.AccountId.ToString():plan.HistoryId)+"_"+plan.AppId))
                throw new InvalidDataException("El plan no corresponde a esta cuenta/juego o usa un formato no compatible.");
            var p=plan.Parameters;
            if(p==null || !double.IsFinite(p.DurationSeconds) || p.DurationSeconds<0 || p.DurationSeconds>35999999 ||
                p.InitialDelaySeconds<0 || p.InitialDelaySeconds>3600 || !double.IsFinite(p.MaximumGapMinutes) || p.MaximumGapMinutes<=0 || p.MaximumGapMinutes>525600)
                throw new InvalidDataException("El plan contiene parámetros de tiempo inválidos.");
            if(plan.Achievements==null || plan.Achievements.Any(a=>a==null || String.IsNullOrWhiteSpace(a.ApiId) ||
                (a.ManualGapSeconds.HasValue && (!double.IsFinite(a.ManualGapSeconds.Value) || a.ManualGapSeconds.Value<0 || a.ManualGapSeconds.Value>315360000))) ||
                plan.Achievements.Select(a=>a.ApiId).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=plan.Achievements.Count)
                throw new InvalidDataException("El plan contiene logros duplicados o tiempos inválidos.");
            if(plan.Achievements.Where(a=>a.Included).Sum(a=>a.ManualGapSeconds??0)>315360000)
                throw new InvalidDataException("La suma de tiempos manuales del plan es demasiado grande.");
        }
        public void Save(GameEntry game,SavedPlan plan)
        {
            Validate(plan,game);
            string json=JsonSerializer.Serialize(plan,new JsonSerializerOptions{WriteIndented=true});
            Directory.CreateDirectory(directory);
            string temp=FilePath(game)+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                {
                    var bytes=System.Text.Encoding.UTF8.GetBytes(json);
                    stream.Write(bytes);stream.Flush(true);
                }
                File.Move(temp,FilePath(game),true);
            }
            finally {if(File.Exists(temp))File.Delete(temp);}
        }
        public SavedPlan Load(GameEntry game)
        {
            var plan=JsonSerializer.Deserialize<SavedPlan>(File.ReadAllText(File.Exists(FilePath(game))?FilePath(game):RelatedPath(game)));
            Validate(plan,game);
            return plan;
        }
        public static RestoredPlan Restore(GameEntry game,SavedPlan plan)
        {
            Validate(plan,game);
            var original=game.Achievements.ToDictionary(a=>a.ApiId,StringComparer.OrdinalIgnoreCase);
            var result=new RestoredPlan{Records=new List<AchievementRecord>()};
            var used=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var saved in plan.Achievements)
            {
                if(!original.TryGetValue(saved.ApiId,out var source)){result.Missing++;continue;}
                var record=source.Clone();record.Included=saved.Included;record.ManualGapSeconds=saved.ManualGapSeconds;
                result.Records.Add(record);used.Add(saved.ApiId);
            }
            foreach(var source in game.Achievements.OrderBy(a=>a.OriginalUnix>0?a.OriginalUnix:Int64.MaxValue))
            {
                if(used.Contains(source.ApiId))continue;
                var record=source.Clone();record.Included=false;record.ManualGapSeconds=null;
                result.Records.Add(record);result.Added++;
            }
            if(!plan.Parameters.CustomOrder)result.Records=result.Records.OrderBy(a=>a.OriginalUnix>0?a.OriginalUnix:Int64.MaxValue).ToList();
            return result;
        }
    }
}
