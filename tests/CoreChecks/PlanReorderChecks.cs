using System;
using System.Collections.Generic;
using System.Linq;
using AchievementTimelineReplay;

static class PlanReorderChecks
{
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS: Live plan reorder — "+message);}
    public static void Run()
    {
        var a=new AchievementRecord{ApiId="A",OriginalUnix=100,Included=true};
        var b=new AchievementRecord{ApiId="B",OriginalUnix=200,Included=false,ManualGapSeconds=42};
        var c=new AchievementRecord{ApiId="C",OriginalUnix=300,Included=true,ManualGapSeconds=15};
        var d=new AchievementRecord{ApiId="D",OriginalUnix=400,Included=true};
        var original=new[]{a,b,c,d};var records=original.ToList();var drag=new PlanReorderSession(records,a,false);
        Check(!drag.MoveOver(records,b,false) && records.SequenceEqual(original),"hovering above the adjacent midpoint does not move the row prematurely");
        Check(drag.MoveOver(records,b,true) && records.SequenceEqual(new[]{b,a,c,d}),"crossing a midpoint moves the source and shifts the intervening row immediately");
        Check(!drag.MoveOver(records,b,true) && records.SequenceEqual(new[]{b,a,c,d}),"a stationary pointer does not bounce the source back after layout changes");
        Check(drag.MoveOver(records,d,true) && records.SequenceEqual(new[]{b,c,d,a}),"a fast downward jump reaches the last position without losing rows");
        Check(drag.MoveOver(records,b,false) && records.SequenceEqual(original),"moving back above the first row restores its position");
        Check(drag.MoveOver(records,d,false) && records.SequenceEqual(new[]{b,c,a,d}),"a fast jump into an upper half inserts before the hovered row");
        Check(!drag.MoveOver(records,a,true) && !drag.MoveOver(records,new AchievementRecord(),false),"the placeholder and foreign records cannot duplicate or remove achievements");
        drag.Restore(records);
        Check(records.SequenceEqual(original) && !drag.WasCustom && !b.Included && b.ManualGapSeconds==42 && c.ManualGapSeconds==15 && a.OriginalUnix==100,"cancel restores exact object order, custom-mode flag, selections, manual waits and original dates");
        var custom=new PlanReorderSession(new[]{c,a,d,b},d,true);records=new List<AchievementRecord>{c,a,d,b};custom.MoveOver(records,c,false);custom.Restore(records);
        Check(custom.WasCustom && records.SequenceEqual(new[]{c,a,d,b}),"cancel also restores an existing nonchronological custom order");
    }
}
