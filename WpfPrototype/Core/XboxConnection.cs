using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AchievementTimelineReplay
{
    internal sealed class XboxConnectionSettings
    {
        public string ClientId {get;set;}
    }
    internal sealed class XboxConnection
    {
        readonly string directory;
        readonly Func<byte[],bool,byte[]> protect;
        public XboxConnection(string directory,Func<byte[],bool,byte[]> protector=null){this.directory=directory;protect=protector??WindowsSecret.Transform;}
        string SettingsPath=>Path.Combine(directory,"connection.json");
        string TokenPath=>Path.Combine(directory,"session.dat");
        public bool Connected=>File.Exists(TokenPath);
        public string ClientId
        {
            get
            {
                if(!File.Exists(SettingsPath))return "";
                if(new FileInfo(SettingsPath).Length>4096)throw new InvalidDataException("Configuración Xbox inválida.");
                return XboxProfile.ClientId(JsonSerializer.Deserialize<XboxConnectionSettings>(File.ReadAllText(SettingsPath))?.ClientId);
            }
        }
        static void Atomic(string path,byte[] bytes)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try{File.WriteAllBytes(temp,bytes);File.Move(temp,path,true);}finally{if(File.Exists(temp))File.Delete(temp);}
        }
        public void Configure(string client)
        {
            client=XboxProfile.ClientId(client);if(Connected && ClientId!=client)Disconnect();
            Atomic(SettingsPath,Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new XboxConnectionSettings{ClientId=client})));
        }
        public void SaveToken(string refresh)
        {
            if(String.IsNullOrWhiteSpace(refresh) || refresh.Length>65536)throw new InvalidDataException("Sesión Microsoft inválida.");
            var bytes=Encoding.UTF8.GetBytes(refresh);
            try{Atomic(TokenPath,protect(bytes,true));}finally{Array.Clear(bytes);}
        }
        string ReadToken()
        {
            if(!Connected)throw new InvalidOperationException("Conectá Xbox desde Escaneo antes de consultar el perfil.");
            if(new FileInfo(TokenPath).Length>131072)throw new InvalidDataException("Sesión Xbox inválida; volvé a conectar.");
            var bytes=protect(File.ReadAllBytes(TokenPath),false);
            try{return Encoding.UTF8.GetString(bytes);}finally{Array.Clear(bytes);}
        }
        public async Task<XboxSession> SessionAsync(XboxProfile api,CancellationToken token)
        {
            var oauth=await api.RefreshAsync(ClientId,ReadToken(),token).ConfigureAwait(false);
            SaveToken(oauth.RefreshToken);
            return await api.AuthorizeAsync(oauth.AccessToken,token).ConfigureAwait(false);
        }
        public void Disconnect(){if(File.Exists(TokenPath))File.Delete(TokenPath);}
    }
    // DPAPI CurrentUser: the refresh token can only be decrypted by this Windows user.
    internal static class WindowsSecret
    {
        [StructLayout(LayoutKind.Sequential)]struct Blob{public int Size;public IntPtr Data;}
        [DllImport("crypt32.dll",SetLastError=true,CharSet=CharSet.Unicode)]
        static extern bool CryptProtectData(ref Blob input,string description,IntPtr entropy,IntPtr reserved,IntPtr prompt,int flags,out Blob output);
        [DllImport("crypt32.dll",SetLastError=true)]
        static extern bool CryptUnprotectData(ref Blob input,IntPtr description,IntPtr entropy,IntPtr reserved,IntPtr prompt,int flags,out Blob output);
        [DllImport("kernel32.dll")]static extern IntPtr LocalFree(IntPtr value);
        public static byte[] Transform(byte[] bytes,bool encrypt)
        {
            if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException("La sesión Xbox se protege con el usuario de Windows.");
            var input=new Blob{Size=bytes.Length,Data=Marshal.AllocHGlobal(bytes.Length)};Blob output=default;
            try
            {
                Marshal.Copy(bytes,0,input.Data,bytes.Length);
                bool ok=encrypt?CryptProtectData(ref input,"ATR sesión",IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out output):CryptUnprotectData(ref input,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out output);
                if(!ok)throw new InvalidOperationException("Windows no pudo "+(encrypt?"proteger":"leer")+" la sesión Xbox. Volvé a conectar desde este usuario de Windows.");
                var result=new byte[output.Size];Marshal.Copy(output.Data,result,0,result.Length);return result;
            }
            finally
            {
                for(int i=0;i<input.Size;i++)Marshal.WriteByte(input.Data,i,0);
                Marshal.FreeHGlobal(input.Data);
                if(output.Data!=IntPtr.Zero){for(int i=0;i<output.Size;i++)Marshal.WriteByte(output.Data,i,0);LocalFree(output.Data);}
            }
        }
    }
}
