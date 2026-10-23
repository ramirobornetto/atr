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
            return entry.Label;
        }
        public void Save(GameEntry game,string label)
        {
            label=label?.Trim();
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
}
