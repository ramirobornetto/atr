using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace AchievementTimelineReplay
{
    internal sealed class SteamLocalDefinitions
    {
        public string Name;
        public List<SteamDefinition> Definitions=new List<SteamDefinition>();
    }
    internal static class SteamDefinitionSources
    {
        public static List<string> Databases(IEnumerable<GameEntry> histories,IEnumerable<string> configured)
            =>histories.Where(PlayniteHistory.IsPA).Select(g=>g.StatsPath).Concat(configured)
                .Where(p=>!String.IsNullOrWhiteSpace(p) && String.Equals(Path.GetFileName(p),"achievement_cache.db",StringComparison.OrdinalIgnoreCase) && File.Exists(p))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        public static SteamLocalDefinitions Load(string schema,uint appId,IEnumerable<string> databases,Action<string> log)
        {
            if(File.Exists(schema))
            {
                try{var definitions=PlatformMapping.FromSchema(schema,appId);if(definitions.Count>0)return new SteamLocalDefinitions{Definitions=definitions};}
                catch(Exception ex){log?.Invoke("Esquema Steam no utilizable; se continúa con PA/SAM: "+ex.Message);}
            }
            foreach(var path in databases)
            {
                try
                {
                    var definitions=PlatformMapping.FromPA(path,appId);if(definitions.Count==0)continue;
                    string name=null;
                    try
                    {
                        using var db=new ReadOnlySqlite(path);
                        name=db.Query("SELECT GameName FROM Games WHERE ProviderKey IN ('Steam','Local') AND ProviderGameId="+appId.ToString(CultureInfo.InvariantCulture)).FirstOrDefault()?["GameName"];
                    }
                    catch(Exception ex){log?.Invoke("Nombre del destino PA no disponible: "+ex.Message);}
                    return new SteamLocalDefinitions{Name=name,Definitions=definitions};
                }
                catch(Exception ex){log?.Invoke("Caché PA no utilizable ("+path+"); se continúa con otras fuentes: "+ex.Message);}
            }
            return new SteamLocalDefinitions();
        }
    }
}
