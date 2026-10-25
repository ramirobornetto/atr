using System;
using System.Collections.Generic;
using System.Linq;

namespace AchievementTimelineReplay
{
    // A drag changes only ordering. The original record objects, selections and timings survive cancellation.
    internal sealed class PlanReorderSession
    {
        readonly List<AchievementRecord> original;
        public AchievementRecord Moving { get; }
        public bool WasCustom { get; }
        public PlanReorderSession(IEnumerable<AchievementRecord> records,AchievementRecord moving,bool wasCustom)
        {
            original=records.ToList();
            if(moving==null || !original.Contains(moving))throw new ArgumentException(UiText.Text("La fila no pertenece al plan."));
            Moving=moving;WasCustom=wasCustom;
        }
        public bool MoveOver(List<AchievementRecord> records,AchievementRecord hovered,bool belowMidpoint)
        {
            int from=records.IndexOf(Moving),target=records.IndexOf(hovered);
            if(from<0 || target<0 || from==target)return false;
            // Before/after the hovered midpoint also handles fast jumps across several rows.
            int insertion=target+(belowMidpoint?1:0);
            if(from<insertion)insertion--;
            if(insertion==from)return false;
            records.RemoveAt(from);records.Insert(insertion,Moving);return true;
        }
        public void Restore(List<AchievementRecord> records){records.Clear();records.AddRange(original);}
    }
}
