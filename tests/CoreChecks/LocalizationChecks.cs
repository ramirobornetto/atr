using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using AchievementTimelineReplay;

static class LocalizationChecks
{
    static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS: Localization — "+message);}
    public static void Run(string root)
    {
        var assembly=typeof(UiText).Assembly;
        var catalogs=UiText.Languages.ToDictionary(l=>l,l=>{
            using var stream=assembly.GetManifestResourceStream("ATR.Localization."+l+".json");
            return JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string,string>>(stream);
        });
        var baseline=catalogs["es"];
        Check(baseline.Count>700 && catalogs.Values.All(c=>c.Keys.OrderBy(k=>k).SequenceEqual(baseline.Keys.OrderBy(k=>k)) && c.Values.All(v=>!String.IsNullOrEmpty(v))),"all three embedded catalogs have complete matching keys and nonempty translations");
        string Slots(string value)=>String.Join("|",Regex.Matches(value,@"\{\d+(?:,[^}:]+)?(?::[^}]+)?\}").Select(m=>m.Value));
        Check(catalogs.Values.All(c=>c.All(p=>Slots(p.Key)==Slots(p.Value))),"format placeholders and time format specifiers survive every translation");
        var before=CultureInfo.CurrentCulture;
        string preference=Path.Combine(root,"language-preferences","ui-language.txt");
        Check(UiText.ReadPreference(preference)=="en","a fresh installation defaults to English without writing a preference");
        UiText.SavePreference("pt-BR",preference);
        Check(UiText.ReadPreference(preference)=="pt-BR","the selected interface language survives a fresh preference read");
        try{UiText.SavePreference("unsupported",preference);throw new Exception("Expected invalid language");}catch(ArgumentException){}
        Check(UiText.ReadPreference(preference)=="pt-BR","invalid language choices cannot overwrite the saved selection");
        File.WriteAllText(preference,"bad language");Check(UiText.ReadPreference(preference)=="en","an unknown preference safely falls back to English");
        File.WriteAllText(preference,new string('x',100));Check(UiText.ReadPreference(preference)=="en","an oversized preference is rejected without changing data");
        try
        {
            foreach(string language in new[]{"en","pt-BR"})
            {
                UiText.SetLanguage(language);
                Check(UiText.Text("Escaneo")==(language=="en"?"Scan":"Verificação") && UiText.Text("Guardar plan")==(language=="en"?"Save plan":"Salvar plano"),language+" translates navigation and actions");
                Check(UiText.Text("UNRECOGNIZED_APP_ID_42")=="UNRECOGNIZED_APP_ID_42" && UiText.Text(null)==null,language+" leaves unknown data and null values untouched");
                var records=new[]{new AchievementRecord{ApiId="API_UNLOCK",DisplayName="Guardar plan",OriginalUnix=1234,Included=true,ManualGapSeconds=15},new AchievementRecord{ApiId="API_LOCK",DisplayName="Pendiente",Included=true}};
                var states=SamRunner.States("[{\"id\":\"API_UNLOCK\",\"is_achieved\":true},{\"id\":\"API_LOCK\",\"is_achieved\":false}]");
                Check(states["API_UNLOCK"]=="Desbloqueado" && SamRunner.ApplySteamStates(records,states,true)==1 && !records[0].Included && records[1].Included,language+" keeps SAM protocol identifiers and unlocked detection independent of translated status labels");
                var game=new GameEntry{AppId=42,AccountId=7,GameName="Sin fecha",StatsPath=Path.Combine(root,"UserGameStats_7_42.bin"),Achievements=records.Select(r=>r.Clone()).ToList()};
                var saved=PlanStore.Capture(game,records,new PlanParameters{DurationSeconds=60});var restored=PlanStore.Restore(game,saved);
                Check(restored.Records[0].ApiId=="API_UNLOCK" && restored.Records[0].OriginalUnix==1234 && restored.Records[0].ManualGapSeconds==15 && !restored.Records[0].Included,language+" preserves saved plan codes, dates, manual waits and selections");
                var run=new MigrationRun{SourceKey=game.StorageKey,Plan=saved,Outcome="Preparada"};run.Items.Add(new MigrationItem{ApiId="API_LOCK",Name="Pendiente",Status="Pendiente"});string report=run.Report();
                Check(run.Outcome=="Preparada" && run.Items[0].Status=="Pendiente" && report.Contains("Sin fecha") && report.Contains("Pendiente [API_LOCK]"),language+" translates report labels without translating game/achievement names or persisted state identifiers");
                var options=new SourceSelection();options.Scan["PA"]=false;options.Scan["Epic (perfil)"]=true;string settings=Path.Combine(root,"localized-sources-"+language+".json");options.Save(settings);var read=SourceSelection.Load(settings);
                Check(!read.Scans("PA") && read.Scans("Epic (perfil)") && SourceSelection.LibraryCategory("Otro")=="Otro",language+" retains canonical origin keys and source preferences");
            }
            Check(CultureInfo.CurrentCulture.Equals(before),"changing UI language does not change numeric input or regional date formatting");
        }
        finally{UiText.SetLanguage("es");}
        string zip=Path.Combine(root,"language-overwrite.zip");
        using(var archive=ZipFile.Open(zip,ZipArchiveMode.Create))
        {
            foreach(string name in new[]{"AchievementTimelineReplay.Wpf.exe","AchievementTimelineReplay.Wpf.dll","ATR.Updater.exe","ATR.Updater.dll","ui-language.txt"})
            {using var writer=new StreamWriter(archive.CreateEntry(name).Open());writer.Write("test");}
        }
        bool blocked=false;try{UpdateInstaller.Extract(zip,Path.Combine(root,"language-overwrite-stage"));}catch(InvalidDataException ex){blocked=ex.Message==UiText.Text("Ruta no permitida en el ZIP.") && !File.Exists(Path.Combine(root,"language-overwrite-stage","ui-language.txt"));}
        Check(blocked,"update archives cannot replace a user's language preference");
    }
}
