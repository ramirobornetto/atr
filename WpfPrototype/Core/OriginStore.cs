using System;
using System.IO;
using System.Text.Json;

namespace AchievementTimelineReplay
{
    internal sealed class OriginAssignment
    {
        public int Version { get; set; } = 1;
        public uint AccountId { get; set; }
        public uint AppId { get; set; }
        public string HistoryId { get; set; }
        public string Label { get; set; }
        public string Method { get; set; } = "manual";
    }
    internal sealed class OriginStore
    {
        readonly string directory;
        public OriginStore(string directory){this.directory=directory;}
        public string FilePath(GameEntry game) => Path.Combine(directory,game.StorageKey+".json");
        public string Load(GameEntry game)
        {
            if(!File.Exists(FilePath(game)))return null;
            var entry=JsonSerializer.Deserialize<OriginAssignment>(File.ReadAllText(FilePath(game)));
            if(entry==null || entry.Version!=1 || entry.AccountId!=game.AccountId || entry.AppId!=game.AppId || entry.HistoryId!=game.HistoryId || entry.Method!="manual" || String.IsNullOrWhiteSpace(entry.Label) || entry.Label.Length>120)
                throw new InvalidDataException("El origen guardado no corresponde a este juego/cuenta o no es válido.");
            return Normalize(entry.Label);
        }
        static string Normalize(string label)
        {
            if(String.Equals(label,"Steam oficial",StringComparison.OrdinalIgnoreCase))return "Steam";
            if(String.Equals(label,"Steamidra",StringComparison.OrdinalIgnoreCase))return "SteamIdra/LuaTools";
            if(String.Equals(label,"Steamidra (indicio LUA)",StringComparison.OrdinalIgnoreCase) || String.Equals(label,"SteamIdra/LuaTools (indicio LUA)",StringComparison.OrdinalIgnoreCase))return "SteamIdra/LuaTools";
            return label;
        }
        public void Save(GameEntry game,string label)
        {
            label=label?.Trim();
            label=Normalize(label);
            if(String.IsNullOrEmpty(label)){File.Delete(FilePath(game));return;}
            if(label.Length>120)throw new InvalidDataException("El origen admite hasta 120 caracteres.");
            var entry=new OriginAssignment{AccountId=game.AccountId,AppId=game.AppId,HistoryId=game.HistoryId,Label=label};
            string json=JsonSerializer.Serialize(entry,new JsonSerializerOptions{WriteIndented=true});
            Directory.CreateDirectory(directory);
            string temp=FilePath(game)+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                File.WriteAllText(temp,json);
                File.Move(temp,FilePath(game),true);
            }
            finally {if(File.Exists(temp))File.Delete(temp);}
        }
    }
    internal static class OriginDetector
    {
        public static void Apply(GameEntry game,string steamRoot)
        {
            if(PlatformMapping.IsMapped(game)){game.DetectedOrigin=game.MappedFromPlatform;game.OriginEvidence="Plan Steam preparado con correspondencias confirmadas desde "+(game.MappedSourceKey?.StartsWith("xbox_",StringComparison.Ordinal)==true?"Xbox Live":("PA / "+game.MappedFromPlatform))+".";return;}
            if(XboxProfile.IsXbox(game)){game.DetectedOrigin="Xbox";game.OriginEvidence="Historial consultado directamente en Xbox Live y conservado en una copia local de ATR.";return;}
            if(PlayniteHistory.IsPA(game))
            {game.DetectedOrigin=game.Platform!="Steam"?game.Platform:game.PAOrigin??"PA";game.OriginEvidence="Historial leído de PlayniteAchievements · proveedor "+game.PAProvider+". La caché no identifica por sí sola el emulador que lo escribió.";return;}
            if(!String.IsNullOrEmpty(game.HistoryId) && game.HistoryId.StartsWith("goldberg_",StringComparison.Ordinal))
            {game.DetectedOrigin=Array.Exists(Path.GetFullPath(game.StatsPath).Split(new[]{Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar},StringSplitOptions.RemoveEmptyEntries),part=>String.Equals(part,"GSE Saves",StringComparison.OrdinalIgnoreCase))?"GSE":"Goldberg";game.OriginEvidence="Estado leído con formato Goldberg/GBE: "+game.StatsPath;return;}
            if(game.HistoryId?.StartsWith("codex_ini_",StringComparison.Ordinal)==true)
            {
                var parts=Path.GetFullPath(game.StatsPath).Split(new[]{Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar},StringSplitOptions.RemoveEmptyEntries);
                string provider=Array.FindLast(parts,p=>String.Equals(p,"CODEX",StringComparison.OrdinalIgnoreCase) || String.Equals(p,"RUNE",StringComparison.OrdinalIgnoreCase));
                game.DetectedOrigin=provider?.ToUpperInvariant()??"CODEX/RUNE compatible";
                game.OriginEvidence="Formato INI compatible con CODEX/RUNE"+(provider==null?"; no se puede distinguir el proveedor por el formato.":"; origen sugerido por el directorio "+provider+". No prueba qué ejecutable lo escribió.");return;
            }
            game.DetectedOrigin="Steam";
            game.OriginEvidence="Historial leído de Steam/appcache/stats; no acredita licencia ni procedencia de la instalación.";
            if(!String.IsNullOrWhiteSpace(steamRoot))
            {
                string lua=Path.Combine(steamRoot,"config","stplug-in",game.AppId+".lua");
                if(File.Exists(lua))
                {game.DetectedOrigin="SteamIdra/LuaTools";game.OriginEvidence="Existe "+lua+". Es un indicio de SteamIdra/LuaTools; no demuestra que este historial provenga de allí ni que el juego no sea oficial.";}
            }
        }
    }

}
