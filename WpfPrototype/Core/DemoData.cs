using System;
using System.IO;
using System.Text.Json;
namespace AchievementTimelineReplay
{
    internal static class DemoData
    {
        public static void Apply(GameEntry game,Action<string> log)
        {
            if(!game.IsDemo)return;
            string path=Path.Combine(Path.GetDirectoryName(game.StatsPath),"atr-demo.json");
            if(!File.Exists(path))return;
            try
            {
                if(new FileInfo(path).Length>16384)throw new InvalidDataException("Descriptor de muestra demasiado grande.");
                using var doc=JsonDocument.Parse(File.ReadAllText(path));var root=doc.RootElement;
                if(root.GetProperty("AppId").GetUInt32()!=game.AppId)throw new InvalidDataException("El descriptor de muestra pertenece a otro AppID.");
                string name=root.GetProperty("GameName").GetString();
                if(String.IsNullOrWhiteSpace(name) || name.Length>200)throw new InvalidDataException("Nombre de muestra inválido.");
                game.DemoGameName=name;
                if(root.TryGetProperty("Achievements",out var metadata) && metadata.ValueKind==JsonValueKind.Object)
                    foreach(var record in game.Achievements)
                        if(metadata.TryGetProperty(record.ApiId,out var entry) && entry.ValueKind==JsonValueKind.Object)
                        {
                            if(entry.TryGetProperty("DisplayName",out var title) && title.ValueKind==JsonValueKind.String)record.DisplayName=title.GetString();
                            if(entry.TryGetProperty("Description",out var description) && description.ValueKind==JsonValueKind.String)record.Description=description.GetString();
                        }
            }
            catch(Exception ex){log("Descriptor de muestra ignorado: "+ex.Message);}
        }
    }
}
