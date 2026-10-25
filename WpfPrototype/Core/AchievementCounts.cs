using System;
using System.Collections.Generic;
using System.Linq;

namespace AchievementTimelineReplay
{
    internal static class AchievementCounts
    {
        public static string Format(GameEntry game,IReadOnlyDictionary<string,string> steamStates=null)
        {
            // A partial Steam response must not shrink a known full schema count.
            game.TotalAchievements=Math.Max(game.TotalAchievements,Math.Max(game.Achievements.Count,steamStates?.Count??0));
            int unlocked=steamStates==null?game.Achievements.Count:steamStates.Values.Count(SamRunner.IsUnlocked);
            return unlocked+" / "+game.TotalAchievements;
        }
    }
}
