using System;
using System.Collections.Generic;
using System.Linq;

namespace AchievementTimelineReplay
{
    internal sealed class ImmediateProgress<T> : IProgress<T>
    {
        readonly Action<T> report;
        public ImmediateProgress(Action<T> report){this.report=report;}
        public void Report(T value)=>report(value);
    }
    // The global bar counts phases, not elapsed time or a guessed global file count.
    internal sealed class ScanPhases
    {
        readonly List<string> phases;
        readonly IProgress<ScanStatus> output;
        int index=-1;double fraction,last;
        public ScanPhases(IEnumerable<string> phases,IProgress<ScanStatus> output){this.phases=phases.ToList();this.output=output;if(this.phases.Count==0)throw new ArgumentException(UiText.Text("Escaneo sin etapas."));}
        public void Begin(string name)
        {
            int next=phases.IndexOf(name);if(next<0 || next<=index)throw new InvalidOperationException(UiText.Text("Orden de etapas inválido."));index=next;fraction=0;Report(new ScanStatus{Message=name});
        }
        public void Report(ScanStatus local)
        {
            if(index<0)return;
            if(local.Total>0)fraction=Math.Max(fraction,Math.Clamp((double)local.Current/local.Total,0,.99));
            double value=Math.Max(last,1000*(index+fraction)/phases.Count);last=value;
            output.Report(new ScanStatus{Current=(int)value,Total=1000,Message=UiText.Text("Etapa ")+(index+1)+"/"+phases.Count+UiText.Text(" · ")+UiText.Text(phases[index])+UiText.Text(" · ")+UiText.Text(local.Message)});
        }
        public void Complete(){last=1000;output.Report(new ScanStatus{Current=1000,Total=1000,Message=UiText.Text("Escaneo completo")});}
    }
}
