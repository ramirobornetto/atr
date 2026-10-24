using System;
using System.Linq;
using System.Text.Json;

namespace AchievementTimelineReplay
{
    internal static class EpicLoginCapture
    {
        public static bool Trusted(string address)=>Uri.TryCreate(address,UriKind.Absolute,out var uri) && uri.Scheme=="https" && uri.Host.Equals("www.epicgames.com",StringComparison.OrdinalIgnoreCase) && uri.Port==443 && String.IsNullOrEmpty(uri.UserInfo);
        public static bool CodeEndpoint(string address)
        {
            if(!Trusted(address) || new Uri(address).AbsolutePath!="/id/api/redirect")return false;
            var ids=new Uri(address).Query.TrimStart('?').Split('&').Select(p=>p.Split('=',2)).Where(p=>p.Length==2 && p[0].Equals("clientId",StringComparison.OrdinalIgnoreCase)).ToList();
            return ids.Count==1 && ids[0][1]=="34a02cf8f4414e29b15921876da36f9a";
        }
        public static string ResponseCode(string source,string payload)
        {
            if(!CodeEndpoint(source) || String.IsNullOrEmpty(payload) || payload.Length>8192)return null;
            try
            {
                using var doc=JsonDocument.Parse(payload);var root=doc.RootElement;
                if(root.ValueKind!=JsonValueKind.Object)return null;
                return EpicProfile.AuthorizationCode(EpicProfile.Text(root,"authorizationCode"));
            }
            catch{return null;}
        }
        public static string Callback(string address,bool expected)
        {
            if(!expected || !Uri.TryCreate(address,UriKind.Absolute,out var uri) || uri.Scheme!="http" || uri.Host!="localhost" || uri.Port!=80 || uri.AbsolutePath!="/launcher/authorized")return null;
            try
            {
                var fields=uri.Query.TrimStart('?').Split('&').Select(p=>p.Split('=',2)).Where(p=>p.Length==2 && p[0]=="code").ToList();
                return fields.Count==1?EpicProfile.AuthorizationCode(Uri.UnescapeDataString(fields[0][1])):null;
            }
            catch{return null;}
        }
    }
}
