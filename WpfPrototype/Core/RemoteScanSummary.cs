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
        public string Format(string source)=>source+UiText.Text(": ")+Reviewed+UiText.Text(" revisados · ")+Count(RemoteScanKind.Recovered)+UiText.Text(" recuperados · ")+Count(RemoteScanKind.NoCatalog)+UiText.Text(" sin catálogo · ")+Count(RemoteScanKind.NoProgress)+UiText.Text(" sin progreso disponible · ")+Count(RemoteScanKind.NoUnlocks)+UiText.Text(" sin desbloqueos · ")+Count(RemoteScanKind.Duplicate)+UiText.Text(" repetidos · ")+Count(RemoteScanKind.Error)+UiText.Text(" errores")+(PhaseErrors==0?"":UiText.Text(" · ")+PhaseErrors+UiText.Text(" fallos de fase"))+(StopReason==null?"":UiText.Text("\nFase detenida: ")+StopReason);
    }
    internal sealed class EpicHistoryLookup
    {
        public GameEntry Game;
        public RemoteScanKind Kind;
    }
}
