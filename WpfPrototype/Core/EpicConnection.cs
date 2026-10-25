using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AchievementTimelineReplay
{
    internal sealed class EpicConnection
    {
        readonly string path;readonly Func<byte[],bool,byte[]> protect;
        public EpicConnection(string directory,Func<byte[],bool,byte[]> protector=null){path=Path.Combine(directory,"session.dat");protect=protector??WindowsSecret.Transform;}
        public bool Connected=>File.Exists(path);
        public void Save(EpicSession session)
        {
            var bytes=Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new EpicSession{RefreshToken=session.RefreshToken,AccountId=session.AccountId,DisplayName=session.DisplayName}));
            string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try{Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(temp,protect(bytes,true));File.Move(temp,path,true);}finally{Array.Clear(bytes);if(File.Exists(temp))File.Delete(temp);}
        }
        public EpicSession Read()
        {
            if(!Connected)throw new InvalidOperationException(UiText.Text("Conectá Epic desde Escaneo antes de consultar el perfil."));
            if(new FileInfo(path).Length>131072)throw new InvalidDataException(UiText.Text("Sesión Epic inválida; volvé a conectar."));
            var bytes=protect(File.ReadAllBytes(path),false);
            try{var saved=JsonSerializer.Deserialize<EpicSession>(bytes);if(String.IsNullOrWhiteSpace(saved?.RefreshToken) || String.IsNullOrWhiteSpace(saved.AccountId))throw new InvalidDataException(UiText.Text("Sesión Epic incompleta; volvé a conectar."));return saved;}finally{Array.Clear(bytes);}
        }
        public async Task<EpicSession> SessionAsync(EpicProfile api,CancellationToken token)
        {
            var old=Read();var current=await api.RefreshAsync(old.RefreshToken,token).ConfigureAwait(false);
            if(!String.Equals(old.AccountId,current.AccountId,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException(UiText.Text("La renovación devolvió otro perfil Epic. Desconectá y conectá explícitamente la cuenta correcta."));
            Save(current);return current;
        }
        public void Disconnect(){if(File.Exists(path))File.Delete(path);}
    }
}
