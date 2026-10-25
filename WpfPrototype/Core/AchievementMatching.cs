using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace AchievementTimelineReplay
{
    internal sealed class AchievementSuggestion
    {
        public SteamDefinition Target;
        public bool Exact;
        public string Reason;
    }
    internal static class AchievementMatching
    {
        static string Normalize(string value)=>new string((value??"").Normalize(NormalizationForm.FormD).Where(Char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        static string Roman(string text)=>Regex.Replace(text??"",@"\b(isle|isla|world|mundo|chapter|capitulo)\s+(?:tintero\s+)?(iii|ii|iv|i|v)\b",m=>m.Groups[1].Value+" "+new Dictionary<string,int>{{"i",1},{"ii",2},{"iii",3},{"iv",4},{"v",5}}[m.Groups[2].Value.ToLowerInvariant()],RegexOptions.IgnoreCase);
        static string Numbers(string text)=>String.Join("|",Regex.Matches(Roman(text),@"\d+(?:[.,]\d+)?%?").Select(m=>m.Value));
        static HashSet<string> Tokens(string value)
        {
            string text=(value??"").Normalize(NormalizationForm.FormD);text=new string(text.Where(c=>CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark).ToArray()).ToLowerInvariant();
            text=Roman(text);text=Regex.Replace(text,@"\bextra\s+especial\b","ex");
            var words=new Dictionary<string,string>{{"derrota","defeat"},{"derrotar","defeat"},{"jefe","boss"},{"jefes","boss"},{"bosses","boss"},{"todos","all"},{"todas","all"},{"every","all"},{"isla","isle"},{"tintero","inkwell"},{"realiza","perform"},{"paradas","parry"},{"parada","parry"},{"parries","parry"},{"encuentra","find"},{"atajo","shortcut"},{"completa","complete"},{"nivel","level"},{"niveles","level"},{"levels","level"},{"sin","not"},{"without","not"},{"no","not"},{"recibir","take"},{"taking","take"},{"dano","damage"},{"movimiento","move"},{"moves","move"},{"monedas","coin"},{"coins","coin"},{"seguidas","row"},{"consecutivas","row"},{"supera","complete"},{"capitulo","chapter"}};
            var stop=new HashSet<string>(new[]{"a","an","the","un","una","de","del","la","las","el","los","en","con","in","on","with","of","perform","times","vez","veces"});
            return Regex.Matches(text,@"[a-z0-9%]+").Select(m=>words.TryGetValue(m.Value,out var word)?word:m.Value).Where(w=>!stop.Contains(w)).ToHashSet();
        }
        static bool Compatible(string source,string target)
        {
            var a=Tokens(source);var b=Tokens(target);
            return Numbers(source)==Numbers(target) && a.Contains("not")==b.Contains("not");
        }
        public static AchievementSuggestion Suggest(AchievementRecord source,IEnumerable<SteamDefinition> targets)
        {
            var ranked=new List<(SteamDefinition Target,double Score,bool Exact,string Reason)>();
            foreach(var target in targets)
            {
                double best=0;bool exact=false;string reason=null;
                foreach(var text in target.Aliases.Prepend(new SteamText{Name=target.Name,Description=target.Description}))
                {
                    bool compatible=String.IsNullOrWhiteSpace(source.Description) || String.IsNullOrWhiteSpace(text.Description) || Compatible(source.Description,text.Description);
                    if(!compatible)continue;
                    bool name=Normalize(source.DisplayName).Length>0 && Normalize(source.DisplayName)==Normalize(text.Name);
                    bool description=Normalize(source.Description).Length>=12 && Normalize(source.Description)==Normalize(text.Description);
                    double score=name && description ? 1 : description ? .99 : name ? .94 : 0;
                    string why=name && description?"Nombre y descripción exactos":description?"Descripción exacta":name?"Nombre exacto; revisar descripción":null;
                    bool strict=description;
                    var a=Tokens(source.Description);var b=Tokens(text.Description);
                    if(a.Count>=2 && b.Count>=2)
                    {
                        double similarity=(double)a.Intersect(b).Count()/a.Union(b).Count();
                        if(similarity>=.84 && score<.90){score=.80+similarity*.10;why="Descripción semejante (es/en); revisar";strict=false;}
                    }
                    if(score>best){best=score;exact=strict;reason=why;}
                }
                if(best>0)ranked.Add((target,best,exact,reason));
            }
            var ordered=ranked.OrderByDescending(x=>x.Score).ToList();
            if(ordered.Count==0 || (ordered.Count>1 && (Math.Abs(ordered[0].Score-ordered[1].Score)<.0001 || (ordered[0].Score<.94 && ordered[0].Score-ordered[1].Score<.035))))return new AchievementSuggestion{Reason=UiText.Text("Sin coincidencia única; revisar manualmente")};
            return new AchievementSuggestion{Target=ordered[0].Target,Exact=ordered[0].Exact,Reason=ordered[0].Reason};
        }
    }
}
