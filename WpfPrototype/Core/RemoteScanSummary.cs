using System;
using System.Collections.Generic;
using System.Linq;

namespace AchievementTimelineReplay
{
    internal enum RemoteScanKind { Recovered,NoCatalog,NoProgress,NoUnlocks,Duplicate,Error }
    internal sealed class RemoteScanSummary
    {
        readonly Dictionary<RemoteScanKind,int> counts=new Dictionary<RemoteScanKind,int>();
        public int Count(RemoteScanKind kind)=>counts.TryGetValue(kind,out int count)?count:0;
        public int Reviewed=>counts.Values.Sum();
        public int PhaseErrors {get;private set;}
        public string StopReason {get;set;}
        public bool HasErrors=>Count(RemoteScanKind.Error)>0 || PhaseErrors>0;
        public void Record(RemoteScanKind kind)=>counts[kind]=Count(kind)+1;
        public void FailPhase(string reason){PhaseErrors++;StopReason=reason;}
        public string Format(string source)=>source+": "+Reviewed+" revisados · "+Count(RemoteScanKind.Recovered)+" recuperados · "+Count(RemoteScanKind.NoCatalog)+" sin catálogo · "+Count(RemoteScanKind.NoProgress)+" sin progreso disponible · "+Count(RemoteScanKind.NoUnlocks)+" sin desbloqueos · "+Count(RemoteScanKind.Duplicate)+" repetidos · "+Count(RemoteScanKind.Error)+" errores"+(PhaseErrors==0?"":" · "+PhaseErrors+" fallos de fase")+(StopReason==null?"":"\nFase detenida: "+StopReason);
    }
    internal sealed class EpicHistoryLookup
    {
        public GameEntry Game;
        public RemoteScanKind Kind;
    }
}
