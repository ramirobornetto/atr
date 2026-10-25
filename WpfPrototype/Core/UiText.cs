using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace AchievementTimelineReplay
{
    internal static class UiText
    {
        public static readonly string[] Languages={"es","en","pt-BR"};
        public static string Language {get;private set;}="en";
        static Dictionary<string,string> texts=new Dictionary<string,string>(StringComparer.Ordinal);
        public static string PreferencePath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AchievementTimelineReplay","ui-language.txt");
        public static string ReadPreference(string path=null)
        {
            try{if(new FileInfo(path??PreferencePath).Length>64)return "en";string value=File.ReadAllText(path??PreferencePath).Trim();return Languages.Contains(value)?value:"en";}catch{return "en";}
        }
        public static void SavePreference(string language,string path=null)
        {
            if(!Languages.Contains(language))throw new ArgumentException("Unsupported interface language.");
            path=path??PreferencePath;Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try{File.WriteAllText(temp,language);File.Move(temp,path,true);}finally{if(File.Exists(temp))File.Delete(temp);}
        }
        public static void Initialize()=>SetLanguage(ReadPreference());
        public static void SetLanguage(string language)
        {
            if(!Languages.Contains(language))language="en";
            var assembly=typeof(UiText).Assembly;
            string name=assembly.GetManifestResourceNames().Single(n=>n.EndsWith(".Localization."+language+".json",StringComparison.Ordinal));
            using var stream=assembly.GetManifestResourceStream(name);
            var loaded=JsonSerializer.Deserialize<Dictionary<string,string>>(stream);
            texts=loaded??new Dictionary<string,string>();Language=language;
            // Numeric inputs and date formatting retain the user's regional settings.
            CultureInfo.DefaultThreadCurrentUICulture=CultureInfo.GetCultureInfo(language);
            CultureInfo.CurrentUICulture=CultureInfo.GetCultureInfo(language);
        }
        public static string Text(string source)=>source!=null && texts.TryGetValue(source,out string translated)?translated:source;
        public static string Format(FormattableString source)=>String.Format(CultureInfo.CurrentCulture,Text(source.Format),source.GetArguments());
    }
}
