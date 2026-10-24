using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using AchievementTimelineReplay;

namespace AchievementTimelineReplay.Wpf
{
    public sealed class GameRow : INotifyPropertyChanged
    {
        public string GameName { get; set; }
        public uint AppId { get; set; }
        public string Source { get; set; }
        string origin;
        public string Origin
        {
            get => origin;
            set {if(origin!=value){origin=value;PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(nameof(Origin)));}}
        }
        string count;
        public string Count
        {
            get => count;
            set {if(count!=value){count=value;PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(nameof(Count)));}}
        }
        public event PropertyChangedEventHandler PropertyChanged;
        string snapshot;
        public string Snapshot {get=>snapshot;set{snapshot=value;PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(nameof(Snapshot)));}}
        string sourceStatus;
        public string SourceStatus {get=>sourceStatus;set{sourceStatus=value;PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(nameof(SourceStatus)));}}
        internal GameEntry Game { get; set; }
    }
    public sealed class AchievementView : INotifyPropertyChanged
    {
        internal AchievementRecord Record;
        readonly Action changed;
        internal AchievementView(AchievementRecord a, Action onChange) { Record=a; changed=onChange; }
        public string Order { get; set; }
        public string DisplayName => Record.DisplayName;
        public string Description => Record.Description;
        public string OriginalDate => LegacyTools.UnixToLocal(Record.OriginalUnix).ToString("dd/MM/yyyy HH:mm:ss");
        public string OriginalGap { get; set; }
        public string NewGap { get; set; }
        public string TargetOffset { get; set; }
        public string TargetTime { get; set; }
        public string SteamState => Record.SteamState;
        public bool Included
        {
            get => Record.Included;
            set { if(Record.Included!=value) {Record.Included=value;OnPropertyChanged(nameof(Included));changed?.Invoke();} }
        }
        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(name));
    }

    public partial class MainWindow : Window
    {
        readonly ObservableCollection<GameRow> games=new ObservableCollection<GameRow>();
        readonly ObservableCollection<AchievementView> achievementRows=new ObservableCollection<AchievementView>();
        readonly List<AchievementRecord> order=new List<AchievementRecord>();
        sealed class BrowsedPlan
        {
            public List<AchievementRecord> Records;
            public PlanParameters Parameters;
            public AchievementRecord Selected;
        }
        readonly Dictionary<GameEntry,string> savedSignatures=new Dictionary<GameEntry,string>();
        readonly Dictionary<GameEntry,BrowsedPlan> browsedPlans=new Dictionary<GameEntry,BrowsedPlan>();
        GameEntry currentGame;
        GameEntry executingGame;
        Dictionary<AchievementRecord,ScheduleItem> activeSchedule;
        SamCliMode samMode=SamCliMode.Unknown;
        CancellationTokenSource scanCancellation;
        CancellationTokenSource replayCancellation;
        bool selectingOrigin;
        bool checkingUpdates;
        bool recoverUpdateDrafts;
        static string UpdateReadyPath => Path.Combine(LegacyTools.DataDirectory,"UpdateDrafts",".ready");
        readonly PlanStore updateDraftStore=new PlanStore(Path.Combine(LegacyTools.DataDirectory,"UpdateDrafts"));
        CancellationTokenSource updateCancellation;
        bool querying;
        bool preparing;
        LegacyReplayer activeReplayer;
        readonly OriginStore originStore=new OriginStore(Path.Combine(LegacyTools.DataDirectory,"Origins"));
        readonly LibraryStore libraryStore=new LibraryStore(Path.Combine(LegacyTools.DataDirectory,"Library"),Path.Combine(LegacyTools.DataDirectory,"Snapshots"));
        DateTimeOffset? lastLibraryScan;
        string lastLibrarySummary="";
        readonly PlanStore planStore=new PlanStore(Path.Combine(LegacyTools.DataDirectory,"Plans"));
        bool CanEditPlan => currentGame!=null && currentGame.HasLocalHistory && !scanning && !preparing && !checkingUpdates && (!running || !ReferenceEquals(currentGame,executingGame));
        bool CanQueryPlan => CanEditPlan && !querying;
        bool scanning;
        bool running;
        bool initialized;
        bool updating;
        string activePage="Escaneo";
        Point dragStart;
        AchievementView dragRow;

        public MainWindow()
        {
            InitializeComponent();
            GamesGrid.ItemsSource=games;
            AchievementsGrid.ItemsSource=achievementRows;
            GamesGrid.CanUserSortColumns=true;
            GamesGrid.Columns[0].SortDirection=ListSortDirection.Ascending;
            CollectionViewSource.GetDefaultView(games).SortDescriptions.Add(new SortDescription(nameof(GameRow.GameName),ListSortDirection.Ascending));
            AchievementsGrid.CanUserSortColumns=false;
            var settings=LegacyTools.ReadSettings();
            SteamPath.Text=settings.TryGetValue("steam",out var steam) && !String.IsNullOrWhiteSpace(steam) ? steam : LegacyTools.DetectSteam();
            SamPath.Text=settings.TryGetValue("sam",out var sam) && !String.IsNullOrWhiteSpace(sam) ? sam : LegacyTools.DetectSam();
            recoverUpdateDrafts=File.Exists(UpdateReadyPath);
            initialized=true;
            GoTo("Escaneo");
            UpdateButtons();
            Log("WPF v0.8.1 iniciada. Motor derivado de v0.5.3. No se ha modificado Steam.");
            LoadSavedLibrary();
            if(!String.IsNullOrWhiteSpace(SamPath.Text)) _=ValidateSamAsync(false);
        }
        void Log(string s)
        {
            if(!Dispatcher.CheckAccess()) {Dispatcher.BeginInvoke(new Action(()=>Log(s)));return;}
            string text=DateTime.Now.ToString("HH:mm:ss")+"  "+s+Environment.NewLine;
            LogBox.AppendText(text);
            LogBox.ScrollToEnd();
        }
        void SetStatus(string status)
        {
            if(!Dispatcher.CheckAccess()) {Dispatcher.BeginInvoke(new Action(()=>SetStatus(status)));return;}
            GlobalStatus.Text=status;
        }
        void SavePaths()
        {
            if(initialized) LegacyTools.WriteSettings(SteamPath.Text.Trim(),SamPath.Text.Trim());
        }
        void Navigate(object sender,RoutedEventArgs e) => GoTo(Convert.ToString(((Button)sender).Tag));
        void GoTo(string screen)
        {
            activePage=screen;
            if(initialized && screen=="Biblioteca"){RefreshLibraryAvailability();UpdateButtons();}
            EscaneoPanel.Visibility=screen=="Escaneo" ? Visibility.Visible : Visibility.Collapsed;
            BibliotecaPanel.Visibility=screen=="Biblioteca" ? Visibility.Visible : Visibility.Collapsed;
            PlanPanel.Visibility=screen=="Plan" ? Visibility.Visible : Visibility.Collapsed;
            ExecutionPanel.Visibility=screen=="Ejecucion" ? Visibility.Visible : Visibility.Collapsed;
            LogsPanel.Visibility=screen=="Logs" ? Visibility.Visible : Visibility.Collapsed;
            foreach(var b in new[]{NavScan,NavLibrary,NavPlan,NavExecution,NavLogs})
            {
                bool selected=Convert.ToString(b.Tag)==screen;
                b.Background=new SolidColorBrush(selected ? Color.FromRgb(17,84,102) : Colors.Transparent);
                b.BorderBrush=new SolidColorBrush(selected ? Color.FromRgb(32,205,213) : Colors.Transparent);
                b.ClearValue(Control.ForegroundProperty);
            }
        }
        void BrowseSteam(object sender,RoutedEventArgs e)
        {
            if(running || preparing || querying)return;
            // WPF usa el diálogo nativo de Windows sin agregar paquetes externos.
            var dlg=new Microsoft.Win32.OpenFolderDialog {Title="Seleccioná la carpeta raíz de Steam",InitialDirectory=Directory.Exists(SteamPath.Text)?SteamPath.Text:Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)};
            if(dlg.ShowDialog(this)==true) {SteamPath.Text=dlg.FolderName;SavePaths();}
        }
        void BrowseSam(object sender,RoutedEventArgs e)
        {
            if(running || preparing || querying)return;
            var dlg=new OpenFileDialog {Filter="SamRewritten CLI (*.exe)|*.exe|Todos los archivos (*.*)|*.*",Title="Seleccioná samrewritten-cli.exe"};
            if(dlg.ShowDialog(this)==true) {SamPath.Text=dlg.FileName;SavePaths();_=ValidateSamAsync(true);}
        }
        async void ValidateSamClick(object sender,RoutedEventArgs e) {await ValidateSamAsync(true);}
        async Task<bool> ValidateSamAsync(bool notify)
        {
            SavePaths();
            string cli=SamPath.Text.Trim();
            if(!File.Exists(cli))
            {
                samMode=SamCliMode.Unknown;SamInfo.Text="SAM: no encontrado";
                if(notify) MessageBox.Show(this,"No se encontró el ejecutable seleccionado.","SamRewritten",MessageBoxButton.OK,MessageBoxImage.Warning);
                UpdateButtons();return false;
            }
            SamInfo.Text="Validando SamRewritten…";
            try {samMode=await Task.Run(()=>SamRunner.DetectMode(cli));}
            catch(Exception ex) {samMode=SamCliMode.Unknown;Log("SAM: "+ex.Message);}
            SamInfo.Text=samMode switch {SamCliMode.Modern=>"SAM: CLI moderno",SamCliMode.Legacy=>"SAM: CLI legacy",_=>"SAM: formato no reconocido"};
            UpdateButtons();
            if(notify) MessageBox.Show(this,samMode==SamCliMode.Unknown?"No pude reconocer la interfaz CLI de SAM.":"SamRewritten validado correctamente ("+samMode+").","SAM",MessageBoxButton.OK,samMode==SamCliMode.Unknown?MessageBoxImage.Warning:MessageBoxImage.Information);
            return samMode!=SamCliMode.Unknown;
        }
        GameRow LibraryRow(GameEntry game) => new GameRow{Game=game,GameName=game.GameName,AppId=game.AppId,Source=game.SourceLabel,Origin=ReadLibraryOrigin(game),Count=game.HasLocalHistory?AchievementCounts.Format(game):"— / "+game.TotalAchievements,Snapshot=game.HasSnapshot?"Protegido ✓":"No disponible"};
        GameRow AdoptImportedGame(GameEntry game)
        {
            var row=games.FirstOrDefault(r=>r.Game.StorageKey==game.StorageKey);
            if(row==null){row=LibraryRow(game);games.Add(row);return row;}
            var existing=row.Game;bool hadHistory=existing.HasLocalHistory;
            existing.Achievements=game.Achievements;existing.HasLocalHistory=game.HasLocalHistory;existing.HasSnapshot=game.HasSnapshot;existing.IsDemo=game.IsDemo;
            existing.DemoGameName=game.DemoGameName;existing.GameName=game.GameName;existing.TotalAchievements=game.TotalAchievements;existing.SchemaPath=game.SchemaPath;
            existing.CatalogIssue=null;existing.RetainedFromCatalog=false;existing.SourceMissing=false;existing.RequiresAccountRefresh=false;
            existing.DetectedOrigin=game.DetectedOrigin;existing.OriginEvidence=game.OriginEvidence;
            row.GameName=game.GameName;row.Count=AchievementCounts.Format(existing);row.Origin=ReadLibraryOrigin(existing);
            CollectionViewSource.GetDefaultView(games).Refresh();
            if(!hadHistory && ReferenceEquals(currentGame,existing)){currentGame=null;order.Clear();achievementRows.Clear();browsedPlans.Remove(existing);}
            else if(ReferenceEquals(currentGame,existing))HeaderGame.Text=existing.GameName;
            return row;
        }
        void RefreshLibraryAvailability()
        {
            uint account=LegacyScanner.RecentAccount(SteamPath.Text.Trim());
            foreach(var row in games)
            {
                var game=row.Game;game.SourceMissing=!File.Exists(game.StatsPath);
                game.RequiresAccountRefresh=LibraryStore.NeedsAccountRefresh(game,account);
                game.HasSnapshot=game.HasLocalHistory && File.Exists(libraryStore.SnapshotPath(game));
                row.SourceStatus=!String.IsNullOrEmpty(game.CatalogIssue)?game.CatalogIssue:!game.HasSnapshot?"Sin snapshot":game.RequiresAccountRefresh?"Otra cuenta · actualizar":game.SourceMissing?"Archivo no encontrado":game.RetainedFromCatalog?"Guardado · sin reimportar":"Disponible";
                row.Snapshot=game.HasSnapshot?"Protegido ✓":"No disponible";
            }
            LibraryCount.Text=games.Count+" historiales";
        }
        void LoadSavedLibrary()
        {
            var saved=libraryStore.Load();foreach(var warning in saved.Warnings)Log("Biblioteca guardada: "+warning);
            if(saved.Catalog==null)return;
            lastLibraryScan=saved.Catalog.LastScanAtUtc;lastLibrarySummary=saved.Catalog.Summary??"";
            foreach(var game in saved.Games){OriginDetector.Apply(game,SteamPath.Text.Trim());games.Add(LibraryRow(game));}
            RefreshLibraryAvailability();LibraryStamp.Text="Catálogo guardado: "+saved.Catalog.SavedAtUtc.LocalDateTime.ToString("dd/MM/yyyy HH:mm")+" · Último escaneo: "+(lastLibraryScan?.LocalDateTime.ToString("dd/MM/yyyy HH:mm")??"solo importaciones manuales")+(saved.UsedBackup?" · Recuperado de copia anterior":"");
            ScanResult.Text="Biblioteca guardada cargada sin escanear. Usá Actualizar biblioteca para revisar los archivos actuales."+(String.IsNullOrEmpty(lastLibrarySummary)?"":"\nResumen del último escaneo guardado:\n"+lastLibrarySummary);
            SetStatus(ScanResult.Text);Log(ScanResult.Text);
            if(games.Count>0){GoTo("Biblioteca");GamesGrid.SelectedIndex=0;GameChanged(GamesGrid,null);}
        }
        bool PersistLibrary()
        {
            try
            {
                libraryStore.Save(games.Select(r=>r.Game),SteamPath.Text.Trim(),lastLibraryScan,lastLibrarySummary);
                RefreshLibraryAvailability();LibraryStamp.Text="Catálogo guardado · Último escaneo: "+(lastLibraryScan?.LocalDateTime.ToString("dd/MM/yyyy HH:mm")??"solo importaciones manuales");return true;
            }
            catch(Exception ex){Log("No se pudo guardar la biblioteca: "+ex.Message);SetStatus("Biblioteca cargada, pero no se pudo guardar el catálogo. Revisá Logs.");return false;}
        }
        void RemoveLibraryClick(object sender,RoutedEventArgs e)
        {
            if(checkingUpdates || scanning || running || preparing || querying || GamesGrid.SelectedItem is not GameRow row)return;
            if(MessageBox.Show(this,"¿Quitar este historial de la biblioteca guardada? No borra archivos de origen, snapshots, planes ni etiquetas. Puede reaparecer al escanear si su archivo sigue en una ruta conocida.","Quitar de biblioteca",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
            games.Remove(row);browsedPlans.Remove(row.Game);savedSignatures.Remove(row.Game);PersistLibrary();UpdateButtons();
        }
        async void ScanClick(object sender,RoutedEventArgs e)
        {
            if(checkingUpdates || scanning || running || querying || preparing) return;
            SavePaths();
            string steam=SteamPath.Text.Trim();
            string cli=SamPath.Text.Trim();
            bool scanGoldberg=ScanGoldbergDefaults.IsChecked==true;
            bool scanIni=ScanIniDefaults.IsChecked==true;
            bool scanSteam=Directory.Exists(Path.Combine(steam,"appcache","stats"));
            if(currentGame!=null && (!CommitPlanEdits() || !GapLimitValid()))return;
            var knownGames=games.Select(r=>r.Game).ToList();string previousSelection=currentGame?.StorageKey;
            var drafts=new Dictionary<string,SavedPlan>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach(var pair in browsedPlans.Where(p=>p.Key.HasLocalHistory))drafts[pair.Key.StorageKey]=PlanStore.Capture(pair.Key,pair.Value.Records,pair.Value.Parameters);
                if(currentGame?.HasLocalHistory==true)drafts[currentGame.StorageKey]=PlanStore.Capture(currentGame,order,ReadPlanParameters());
            }
            catch(Exception ex){MessageBox.Show(this,"No se pudo conservar un borrador antes de actualizar: "+ex.Message,"Biblioteca",MessageBoxButton.OK,MessageBoxImage.Information);return;}
            scanning=true;
            scanCancellation=new CancellationTokenSource();
            var token=scanCancellation.Token;
            ScanProgress.IsIndeterminate=true;
            ScanResult.Text="Iniciando escaneo…";
            UpdateButtons();
            SetStatus("Escaneando historiales locales…");
            Log("=== Inicio de escaneo ===");
            var progress=new Progress<ScanStatus>(p=>
            {
                ScanProgress.IsIndeterminate=p.Indeterminate || p.Total==0;
                if(p.Total>0) {ScanProgress.Maximum=p.Total;ScanProgress.Value=Math.Min(p.Current,p.Total);}
                ScanResult.Text=p.Message;
                SetStatus(p.Message);
            });
            try
            {
                var scanner=new LegacyScanner(Log);
                var goldbergSummary=new ScanSummary();
                var iniSummary=new ScanSummary();
                var found=await Task.Run(()=>
                {
                    var all=scanSteam?scanner.Scan(steam,cli,samMode,progress,token):new List<GameEntry>();
                    if(scanGoldberg)foreach(var root in GoldbergHistory.DefaultRoots().Where(Directory.Exists))
                        all.AddRange(GoldbergHistory.Scan(root,steam,Path.Combine(LegacyTools.DataDirectory,"Snapshots"),Log,token,goldbergSummary));
                    if(scanIni)foreach(var root in IniHistory.DefaultRoots().Where(Directory.Exists))
                        all.AddRange(IniHistory.Scan(root,steam,Path.Combine(LegacyTools.DataDirectory,"Snapshots"),Log,token,iniSummary));
                    var seen=new HashSet<string>(all.Select(g=>g.StorageKey),StringComparer.OrdinalIgnoreCase);
                    foreach(var known in knownGames.Where(g=>!String.IsNullOrEmpty(g.HistoryId)))
                    {
                        token.ThrowIfCancellationRequested();if(seen.Contains(known.StorageKey) || !File.Exists(known.StatsPath))continue;
                        try
                        {
                            string schema=Path.Combine(steam,"appcache","stats","UserGameStatsSchema_"+known.AppId+".bin");
                            var refreshed=known.HistoryId.StartsWith("goldberg_",StringComparison.Ordinal)?GoldbergHistory.Load(known.StatsPath,known.AppId,File.Exists(known.SchemaPath)?known.SchemaPath:null,schema,Log):IniHistory.Load(known.StatsPath,known.AppId,schema,Log);
                            refreshed=HistorySnapshots.Protect(refreshed,Path.Combine(LegacyTools.DataDirectory,"Snapshots"));all.Add(refreshed);seen.Add(refreshed.StorageKey);
                        }
                        catch(Exception ex){Log("No se pudo reimportar la fuente recordada "+known.StatsPath+": "+ex.Message);}
                    }
                    foreach(var refreshed in all.Where(g=>g.HistoryId?.StartsWith("goldberg_",StringComparison.Ordinal)==true))
                    {
                        var known=knownGames.FirstOrDefault(g=>g.StorageKey==refreshed.StorageKey);
                        if(known==null)continue;
                        if(GoldbergHistory.NeedsDefinitions(refreshed) && File.Exists(known.SchemaPath))
                            try{GoldbergHistory.ApplyDefinitions(refreshed,known.SchemaPath);}catch(Exception ex){Log("Definición recordada no disponible: "+ex.Message);}
                        var labels=known.Achievements.ToDictionary(a=>a.ApiId,StringComparer.OrdinalIgnoreCase);
                        foreach(var achievement in refreshed.Achievements)
                            if(labels.TryGetValue(achievement.ApiId,out var label))
                            {
                                if(achievement.DisplayName==achievement.ApiId || String.IsNullOrWhiteSpace(achievement.DisplayName))achievement.DisplayName=label.DisplayName;
                                if(String.IsNullOrEmpty(achievement.Description))achievement.Description=label.Description;
                            }
                    }
                    foreach(var known in knownGames)
                        if(LibraryStore.ShouldRetainAfterScan(known) && seen.Add(known.StorageKey)){known.RetainedFromCatalog=true;all.Add(known);}
                    GameNames.Resolve(all.Where(g=>!String.IsNullOrEmpty(g.HistoryId)),Log,token);
                    return all.OrderBy(g=>g.GameName,StringComparer.CurrentCultureIgnoreCase).ToList();
                },token);
                token.ThrowIfCancellationRequested();
                games.Clear();achievementRows.Clear();order.Clear();browsedPlans.Clear();savedSignatures.Clear();currentGame=null;
                foreach(var g in found)
                {
                    if(g.HasLocalHistory && drafts.TryGetValue(g.StorageKey,out var draft))
                        try{browsedPlans[g]=new BrowsedPlan{Records=PlanStore.Restore(g,draft).Records,Parameters=draft.Parameters};}catch(Exception ex){Log("Borrador no restaurado: "+ex.Message);}
                    games.Add(LibraryRow(g));
                }
                LibraryCount.Text=games.Count+" juegos";
                ScanResult.Text="Actualización terminada: "+games.Count+" historiales en biblioteca ("+found.Count(g=>g.RetainedFromCatalog)+" conservados del catálogo).\n"+(scanSteam?scanner.Summary.Format("Steam"):"Steam: appcache/stats no disponible")+(scanGoldberg?"\n"+goldbergSummary.Format("Goldberg/GBE"):"")+(scanIni?"\n"+iniSummary.Format("CODEX/RUNE"):"");
                SetStatus(ScanResult.Text);
                Log(ScanResult.Text);
                ScanProgress.IsIndeterminate=false;ScanProgress.Maximum=1;ScanProgress.Value=1;
                lastLibraryScan=DateTimeOffset.UtcNow;lastLibrarySummary=ScanResult.Text;PersistLibrary();
                GoTo("Biblioteca");
                GamesGrid.SelectedItem=games.FirstOrDefault(r=>r.Game.StorageKey==previousSelection)??games.FirstOrDefault();
            }
            catch(OperationCanceledException)
            {
                ScanResult.Text="Escaneo cancelado.";SetStatus(ScanResult.Text);Log(ScanResult.Text);
            }
            catch(Exception ex)
            {
                ScanResult.Text="ERROR: "+ex.Message;Log(ex.ToString());SetStatus(ScanResult.Text);
                MessageBox.Show(this,ex.Message,"Escaneo",MessageBoxButton.OK,MessageBoxImage.Error);
            }
            finally {scanning=false;scanCancellation.Dispose();scanCancellation=null;ScanProgress.IsIndeterminate=false;GameChanged(GamesGrid,null);UpdateButtons();}
        }
        async void ImportGoldbergClick(object sender,RoutedEventArgs e)
        {
            if(checkingUpdates || scanning || running || querying || preparing)return;
            var dialog=new OpenFileDialog{Title="Seleccioná el estado Goldberg: <AppID>/achievements.json (no el de steam_settings)",Filter="Estado Goldberg (achievements.json)|achievements.json|JSON (*.json)|*.json"};
            var existing=GoldbergHistory.DefaultRoots().FirstOrDefault(Directory.Exists);
            if(existing!=null)dialog.InitialDirectory=existing;
            if(dialog.ShowDialog(this)!=true)return;
            string folder=Path.GetDirectoryName(dialog.FileName);
            if(!UInt32.TryParse(Path.GetFileName(folder),out uint appId) || appId==0)
            {MessageBox.Show(this,"El archivo de estado debe estar dentro de una carpeta con el AppID del juego como nombre. No selecciones la definición de steam_settings.","Goldberg",MessageBoxButton.OK,MessageBoxImage.Information);return;}
            string definitions=Path.Combine(folder,"steam_settings","achievements.json");
            if(!File.Exists(definitions))definitions=null;
            string statePath=dialog.FileName,steam=SteamPath.Text.Trim();
            scanning=true;scanCancellation=new CancellationTokenSource();var token=scanCancellation.Token;UpdateButtons();
            ScanProgress.IsIndeterminate=true;SetStatus("Importando Goldberg…");
            try
            {
                var game=await Task.Run(()=>
                {
                    token.ThrowIfCancellationRequested();
                    string schema=Path.Combine(steam,"appcache","stats","UserGameStatsSchema_"+appId+".bin");
                    var imported=GoldbergHistory.Load(statePath,appId,definitions,schema,Log);
                    token.ThrowIfCancellationRequested();
                    var protectedGame=GoldbergHistory.Protect(imported,Path.Combine(LegacyTools.DataDirectory,"Snapshots"));
                    GameNames.Resolve(new[]{protectedGame},Log,token);
                    return protectedGame;
                },token);
                token.ThrowIfCancellationRequested();
                var row=AdoptImportedGame(game);
                // Existing imported sources retain drafts and their protected original.
                scanning=false;GamesGrid.SelectedItem=row;GameChanged(GamesGrid,null);
                ScanResult.Text=game.IsDemo?"Demostración cargada. Podés editar y guardar planes; Steam y SAM están bloqueados para esta muestra.":"Goldberg importado. Solo se añadieron logros desbloqueados con fecha válida. Revisá el log para omisiones.";
                LibraryCount.Text=games.Count+" historiales";SetStatus(ScanResult.Text);Log(ScanResult.Text);PersistLibrary();GoTo("Plan");
            }
            catch(OperationCanceledException){SetStatus("Importación Goldberg cancelada.");}
            catch(Exception ex){Log(ex.ToString());MessageBox.Show(this,ex.Message,"Goldberg",MessageBoxButton.OK,MessageBoxImage.Warning);}
            finally{scanning=false;scanCancellation.Dispose();scanCancellation=null;ScanProgress.IsIndeterminate=false;UpdateButtons();}
        }
        void FindDefinitionsClick(object sender,RoutedEventArgs e)
        {
            if(!CanEditPlan || querying || !GoldbergHistory.NeedsDefinitions(currentGame) || !CommitPlanEdits())return;
            var dialog=new OpenFileDialog{Title="Elegí steam_settings/achievements.json del mismo juego",Filter="Definición Goldberg (achievements.json)|achievements.json|JSON (*.json)|*.json"};
            if(dialog.ShowDialog(this)!=true)return;
            try
            {
                GoldbergHistory.ApplyDefinitions(currentGame,dialog.FileName);
                var metadata=currentGame.Achievements.ToDictionary(a=>a.ApiId,StringComparer.OrdinalIgnoreCase);
                foreach(var record in order)
                    if(metadata.TryGetValue(record.ApiId,out var source)){record.DisplayName=source.DisplayName;record.Description=source.Description;}
                var row=games.First(r=>ReferenceEquals(r.Game,currentGame));row.Count=AchievementCounts.Format(currentGame);
                RefreshPlan();PersistLibrary();UpdateButtons();
                SetStatus(GoldbergHistory.NeedsDefinitions(currentGame)?"Definición cargada. Algunos IDs siguen sin nombre en ese archivo.":"Nombres y descripciones cargados; el orden y los tiempos del plan se conservaron.");
            }
            catch(Exception ex){MessageBox.Show(this,ex.Message,"Definición de nombres",MessageBoxButton.OK,MessageBoxImage.Information);}
        }
        async void ImportIniClick(object sender,RoutedEventArgs e)
        {
            if(checkingUpdates || scanning || running || querying || preparing)return;
            var dialog=new OpenFileDialog{Title="Seleccioná <AppID>/achievements.ini o <AppID>/remote/achievements.ini",Filter="Estado CODEX/RUNE (achievements.ini)|achievements.ini|INI (*.ini)|*.ini"};
            var root=IniHistory.DefaultRoots().FirstOrDefault(Directory.Exists);if(root!=null)dialog.InitialDirectory=root;
            if(dialog.ShowDialog(this)!=true)return;
            uint appId=IniHistory.AppIdFromPath(dialog.FileName);
            if(appId==0){MessageBox.Show(this,"Elegí un archivo dentro de la carpeta numérica del AppID, o de su subcarpeta remote.","CODEX/RUNE",MessageBoxButton.OK,MessageBoxImage.Information);return;}
            string path=dialog.FileName,steam=SteamPath.Text.Trim();
            scanning=true;scanCancellation=new CancellationTokenSource();var token=scanCancellation.Token;UpdateButtons();ScanProgress.IsIndeterminate=true;
            SetStatus("Importando CODEX/RUNE…");
            try
            {
                var game=await Task.Run(()=>
                {
                    token.ThrowIfCancellationRequested();
                    string schema=Path.Combine(steam,"appcache","stats","UserGameStatsSchema_"+appId+".bin");
                    var imported=IniHistory.Load(path,appId,schema,Log);token.ThrowIfCancellationRequested();
                    var protectedGame=HistorySnapshots.Protect(imported,Path.Combine(LegacyTools.DataDirectory,"Snapshots"));
                    GameNames.Resolve(new[]{protectedGame},Log,token);return protectedGame;
                },token);
                token.ThrowIfCancellationRequested();
                var row=AdoptImportedGame(game);
                scanning=false;GamesGrid.SelectedItem=row;GameChanged(GamesGrid,null);
                ScanResult.Text=game.IsDemo?"Muestra CODEX/RUNE cargada. Las acciones de Steam están bloqueadas.":"INI importado. Solo se añadieron desbloqueos con fecha válida; revisá Logs para omisiones.";
                LibraryCount.Text=games.Count+" historiales";SetStatus(ScanResult.Text);Log(ScanResult.Text);PersistLibrary();GoTo("Plan");
            }
            catch(OperationCanceledException){SetStatus("Importación INI cancelada.");}
            catch(Exception ex){Log(ex.ToString());MessageBox.Show(this,ex.Message,"CODEX/RUNE",MessageBoxButton.OK,MessageBoxImage.Warning);}
            finally{scanning=false;scanCancellation.Dispose();scanCancellation=null;ScanProgress.IsIndeterminate=false;UpdateButtons();}
        }
        void SaveUpdateDrafts()
        {
            if(currentGame?.HasLocalHistory==true)
            {
                if(!CommitPlanEdits() || !GapLimitValid())throw new InvalidDataException("Corregí el plan actual antes de actualizar para poder conservar el borrador.");
                updateDraftStore.Save(currentGame,PlanStore.Capture(currentGame,order,ReadPlanParameters()));
            }
            foreach(var pair in browsedPlans)
                if(pair.Key.HasLocalHistory && !ReferenceEquals(pair.Key,currentGame))updateDraftStore.Save(pair.Key,PlanStore.Capture(pair.Key,pair.Value.Records,pair.Value.Parameters));
        }
        async void CheckUpdatesClick(object sender,RoutedEventArgs e)
        {
            if(checkingUpdates || running || scanning || preparing || querying)return;
            if(currentGame!=null && (!CommitPlanEdits() || !GapLimitValid()))return;
            checkingUpdates=true;updateCancellation=new CancellationTokenSource();var token=updateCancellation.Token;
            UpdateButton.Content="Buscando…";UpdateButtons();bool hadReady=File.Exists(UpdateReadyPath),installerStarted=false;
            try
            {
                var info=await UpdateChecker.CheckAsync(token);token.ThrowIfCancellationRequested();
                var current=typeof(MainWindow).Assembly.GetName().Version;
                if(!info.IsNewerThan(current))
                {MessageBox.Show(this,"Ya tenés una versión igual o más nueva que la publicada ("+info.Version+").","ATR · Actualizaciones",MessageBoxButton.OK,MessageBoxImage.Information);return;}
                string work=Path.Combine(LegacyTools.DataDirectory,"Updates",info.Version+"_"+Guid.NewGuid().ToString("N"));
                string package=Path.Combine(work,"update.zip");
                var progress=new Progress<int>(percent=>UpdateButton.Content="Descargando "+percent+" %");
                await AutomaticUpdate.DownloadAsync(info,package,progress,token);token.ThrowIfCancellationRequested();
                UpdateButton.Content="Preparando instalación…";
                SaveUpdateDrafts();SavePaths();Directory.CreateDirectory(Path.GetDirectoryName(UpdateReadyPath));File.WriteAllText(UpdateReadyPath,"1");
                var start=await Task.Run(()=>AutomaticUpdate.PrepareInstaller(info,package,AppContext.BaseDirectory,work,token),token);token.ThrowIfCancellationRequested();
                using var installer=Process.Start(start)??throw new IOException("No se pudo iniciar el instalador de actualización.");installerStarted=true;
                Close();
            }
            catch(OperationCanceledException){if(!token.IsCancellationRequested)MessageBox.Show(this,"Se agotó el tiempo de consulta o descarga. Reintentá más tarde.","Actualizaciones",MessageBoxButton.OK,MessageBoxImage.Information);}
            catch(Exception ex){Log("Actualización no iniciada: "+ex.Message);MessageBox.Show(this,"No se pudo iniciar la actualización. La instalación actual se conserva.\n"+ex.Message,"Actualizaciones",MessageBoxButton.OK,MessageBoxImage.Information);}
            finally
            {
                if(!installerStarted && !hadReady)try{File.Delete(UpdateReadyPath);}catch{}
                checkingUpdates=false;updateCancellation.Dispose();updateCancellation=null;
                if(IsLoaded){UpdateButton.Content="Actualizar ATR";UpdateButtons();}
            }
        }
        void OpenHistoryLocation(object sender,MouseButtonEventArgs e)
        {
            if(currentGame==null || String.IsNullOrWhiteSpace(currentGame.StatsPath))return;
            try
            {
                string path=Path.GetFullPath(currentGame.StatsPath);
                var start=new ProcessStartInfo{FileName="explorer.exe",UseShellExecute=false};
                if(File.Exists(path))start.Arguments="/select,\""+path+"\"";
                else
                {
                    string directory=Path.GetDirectoryName(path);
                    if(!Directory.Exists(directory))throw new DirectoryNotFoundException("El archivo y su carpeta ya no existen: "+path);
                    start.ArgumentList.Add(directory);
                }
                Process.Start(start);e.Handled=true;
            }
            catch(Exception ex){MessageBox.Show(this,ex.Message,"Abrir ubicación",MessageBoxButton.OK,MessageBoxImage.Information);}
        }
        void CancelScanClick(object sender,RoutedEventArgs e) {scanCancellation?.Cancel();CancelScanButton.IsEnabled=false;ScanResult.Text="Cancelando escaneo…";}
        void LibraryFilterChanged(object sender,TextChangedEventArgs e)
        {
            if(!initialized)return;
            ICollectionView view=CollectionViewSource.GetDefaultView(GamesGrid.ItemsSource);
            if(view==null)return;
            string q=LibrarySearch.Text.Trim();
            view.Filter=o=>o is GameRow g && (q.Length==0 || g.GameName.IndexOf(q,StringComparison.CurrentCultureIgnoreCase)>=0 || g.AppId.ToString().Contains(q,StringComparison.OrdinalIgnoreCase));
        }
        void GameChanged(object sender,SelectionChangedEventArgs e)
        {
            if(!initialized || scanning || preparing)return;
            var next=(GamesGrid.SelectedItem as GameRow)?.Game;
            if(ReferenceEquals(next,currentGame))return;
            // Navegar conserva el plan mostrado. La ejecución retiene su propia lista
            // de ScheduleItems y no depende del juego seleccionado en la biblioteca.
            if(currentGame?.HasLocalHistory==true)
                browsedPlans[currentGame]=new BrowsedPlan
                {
                    Records=order.ToList(),
                    Parameters=ReadPlanParameters(),Selected=(AchievementsGrid.SelectedItem as AchievementView)?.Record
                };
            currentGame=next;
            order.Clear();achievementRows.Clear();
            if(currentGame!=null)
            {
                if(recoverUpdateDrafts && !browsedPlans.ContainsKey(currentGame) && updateDraftStore.Exists(currentGame))
                {
                    try
                    {
                        var savedDraft=updateDraftStore.Load(currentGame);var restoredDraft=PlanStore.Restore(currentGame,savedDraft);
                        browsedPlans[currentGame]=new BrowsedPlan{Records=restoredDraft.Records,Parameters=savedDraft.Parameters};
                        File.Delete(updateDraftStore.FilePath(currentGame));
                        if(Directory.GetFiles(Path.GetDirectoryName(UpdateReadyPath),"*.json").Length==0)File.Delete(UpdateReadyPath);
                        Log("Borrador recuperado después de actualizar: "+currentGame.GameName+". El plan guardado explícitamente se conserva por separado.");
                    }
                    catch(Exception ex){Log("No se pudo recuperar el borrador de actualización: "+ex.Message);}
                }
                bool saved=browsedPlans.TryGetValue(currentGame,out var previous);
                if(saved)order.AddRange(previous.Records);
                else foreach(var a in currentGame.Achievements.OrderBy(x=>x.OriginalUnix))order.Add(a.Clone());
                HeaderGame.Text=currentGame.GameName;
                PlanHistoryPath.Text=currentGame.StatsPath;
                HeaderGameInfo.Text=(currentGame.IsDemo?"DEMO · ":"")+"AppID "+currentGame.AppId+" · "+order.Count+" logros · "+(currentGame.HasSnapshot?"Snapshot protegido":"Sin snapshot");
                if(!running)ExecutionGame.Text=currentGame.GameName;
                ApplyPlanParameters(saved?previous.Parameters:new PlanParameters{DurationSeconds=LegacyPlanner.OriginalSpan(order)});
                Log("Cargado "+currentGame.GameName+" ["+currentGame.AppId+"]: "+order.Count+" logros.");
                RefreshPlan();
                LoadOrigin();

                if(saved && previous.Selected!=null)
                {
                    AchievementsGrid.SelectedItem=achievementRows.FirstOrDefault(a=>ReferenceEquals(a.Record,previous.Selected));
                    AchievementsGrid.ScrollIntoView(AchievementsGrid.SelectedItem);
                }
            }
            else
            {
                PlanHistoryPath.Text="";HeaderGame.Text="Sin juego seleccionado";HeaderGameInfo.Text="Elegí un historial";
                if(!running)ExecutionGame.Text="No hay ningún plan preparado";
                SelectOrigin("Sin clasificar");OriginStatus.Text="";
            }
            UpdateButtons();
        }
        string ReadLibraryOrigin(GameEntry game)
        {
            try {return originStore.Load(game)??game.DetectedOrigin??"Sin clasificar";}
            catch(Exception ex){Log("No se pudo leer el origen de "+game.AppId+": "+ex.Message);return "Sin clasificar";}
        }
        void SelectOrigin(string label)
        {
            var item=OriginChoice.Items.Cast<ComboBoxItem>().FirstOrDefault(i=>Convert.ToString(i.Content)==label);
            if(item==null){item=new ComboBoxItem{Content=label};OriginChoice.Items.Add(item);}
            selectingOrigin=true;
            try {OriginChoice.SelectedItem=item;}
            finally {selectingOrigin=false;}
        }
        void LoadOrigin()
        {
            try
            {
                var origin=originStore.Load(currentGame);
                SelectOrigin(origin??currentGame.DetectedOrigin??"Sin clasificar");
                OriginStatus.Text=origin==null?(currentGame.DetectedOrigin==null?"Sin origen asignado.":currentGame.OriginEvidence??"Origen detectado; podés corregirlo manualmente."):"Origen manual guardado para esta cuenta y juego.";
            }
            catch(Exception ex){SelectOrigin("Sin clasificar");OriginStatus.Text="No se pudo leer el origen: "+ex.Message;}
        }
        void OriginChanged(object sender,SelectionChangedEventArgs e)
        {
            if(!initialized || selectingOrigin || !CanEditPlan)return;
            try
            {
                string label=Convert.ToString((OriginChoice.SelectedItem as ComboBoxItem)?.Content)??"Sin clasificar";
                originStore.Save(currentGame,label=="Sin clasificar"?null:label);
                LoadOrigin();
                var row=games.FirstOrDefault(g=>ReferenceEquals(g.Game,currentGame));
                if(row!=null)row.Origin=ReadLibraryOrigin(currentGame);
                SetStatus("Origen actualizado para "+currentGame.GameName+". No modifica archivos de logros ni Steam.");
            }
            catch(Exception ex){LoadOrigin();MessageBox.Show(this,ex.Message,"No se pudo guardar el origen",MessageBoxButton.OK,MessageBoxImage.Warning);}
        }
        void LibraryDoubleClick(object sender,MouseButtonEventArgs e) {if(currentGame!=null)GoTo("Plan");}
        void SetDuration(long seconds)
        {
            if(seconds<0) seconds=0;
            updating=true;
            DurationHours.Text=Math.Min(9999,seconds/3600).ToString();
            DurationMinutes.Text=((seconds%3600)/60).ToString();
            DurationSeconds.Text=(seconds%60).ToString();
            updating=false;
        }
        double TargetDuration()
        {
            bool h=long.TryParse(DurationHours.Text,out long hours);
            bool m=long.TryParse(DurationMinutes.Text,out long minutes);
            bool s=long.TryParse(DurationSeconds.Text,out long seconds);
            if(!h||!m||!s||hours<0||minutes<0||minutes>59||seconds<0||seconds>59) return Math.Max(0,LegacyPlanner.OriginalSpan(order));
            return hours*3600.0+minutes*60.0+seconds;
        }
        double? AutomaticGapLimit()
        {
            if(LimitLongGaps.IsChecked!=true)return null;
            return double.TryParse(MaxGapMinutes.Text,out double minutes) && double.IsFinite(minutes) && minutes>0 && minutes<=525600 ? minutes*60 : null;
        }
        bool GapLimitValid() => LimitLongGaps.IsChecked!=true || AutomaticGapLimit().HasValue;
        void GapLimitChanged(object sender,RoutedEventArgs e)
        {
            if(initialized && !updating && CanEditPlan)RefreshPlan();
        }
        PlanParameters ReadPlanParameters() => new PlanParameters
        {
            DurationSeconds=TargetDuration(),CustomOrder=CustomOrder.IsChecked==true,
            InitialDelaySeconds=InitialSeconds(),Repair=LockFirst.IsChecked==true,Idle=IdleDuringMigration.IsChecked==true,
            LimitLongGaps=LimitLongGaps.IsChecked==true,
            MaximumGapMinutes=double.TryParse(MaxGapMinutes.Text,out var maximum) && double.IsFinite(maximum) && maximum>0 && maximum<=525600?maximum:30
        };
        void ApplyPlanParameters(PlanParameters p)
        {
            SetDuration((long)p.DurationSeconds);
            updating=true;
            CustomOrder.IsChecked=p.CustomOrder;InitialDelay.Text=p.InitialDelaySeconds.ToString();
            LockFirst.IsChecked=p.Repair;IdleDuringMigration.IsChecked=p.Idle;
            LimitLongGaps.IsChecked=p.LimitLongGaps;MaxGapMinutes.Text=p.MaximumGapMinutes.ToString();
            updating=false;
        }
        void UpdatePlanSaveStatus()
        {
            if(currentGame==null){PlanSaveStatus.Text="Sin juego seleccionado.";return;}
            try
            {
                if(!planStore.Exists(currentGame)){PlanSaveStatus.Text="Sin plan guardado en disco. Guardá para conservar los cambios.";return;}
                if(!savedSignatures.TryGetValue(currentGame,out var signature))
                    savedSignatures[currentGame]=signature=PlanStore.Signature(planStore.Load(currentGame));
                PlanSaveStatus.Text=PlanStore.Signature(PlanStore.Capture(currentGame,order,ReadPlanParameters()))==signature
                    ? "El borrador coincide con el plan guardado en disco."
                    : "El borrador difiere del guardado. Guardá los cambios o usá Cargar guardado.";
            }
            catch(Exception ex){PlanSaveStatus.Text="No se pudo comprobar el plan guardado: "+ex.Message;}
        }
        bool CommitPlanEdits() => AchievementsGrid.CommitEdit(DataGridEditingUnit.Cell,true) && AchievementsGrid.CommitEdit(DataGridEditingUnit.Row,true);
        void SavePlanClick(object sender,RoutedEventArgs e)
        {
            if(!CanEditPlan || !GapLimitValid() || !CommitPlanEdits())return;
            try
            {
                var saved=PlanStore.Capture(currentGame,order,ReadPlanParameters());
                planStore.Save(currentGame,saved);savedSignatures[currentGame]=PlanStore.Signature(saved);
                PlanSaveStatus.Text="Plan guardado en disco: "+DateTime.Now.ToString("dd/MM HH:mm:ss");
                SetStatus("Plan de "+currentGame.GameName+" guardado. No modifica Steam ni el historial original.");
            }
            catch(Exception ex){MessageBox.Show(this,ex.Message,"No se pudo guardar el plan",MessageBoxButton.OK,MessageBoxImage.Warning);}
        }
        void LoadPlanClick(object sender,RoutedEventArgs e)
        {
            if(!CanEditPlan)return;
            if(MessageBox.Show(this,"Cargar el plan guardado reemplazará los cambios de este borrador. No modifica Steam. ¿Continuar?","Cargar plan",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
            try
            {
                var saved=planStore.Load(currentGame);
                var restored=PlanStore.Restore(currentGame,saved);
                savedSignatures[currentGame]=PlanStore.Signature(saved);
                order.Clear();order.AddRange(restored.Records);ApplyPlanParameters(saved.Parameters);RefreshPlan();
                PlanSaveStatus.Text="Plan cargado. Consultá Steam antes de ejecutarlo.";
                if(restored.Added>0 || restored.Missing>0)
                    MessageBox.Show(this,$"El historial cambió: {restored.Missing} logros guardados ya no están y {restored.Added} son nuevos. Los nuevos se agregaron desmarcados. Revisá el plan.","Plan actualizado",MessageBoxButton.OK,MessageBoxImage.Information);
            }
            catch(Exception ex){MessageBox.Show(this,ex.Message,"No se pudo cargar el plan",MessageBoxButton.OK,MessageBoxImage.Warning);}
        }
        void RestoreOriginalClick(object sender,RoutedEventArgs e)
        {
            if(!CanEditPlan)return;
            if(MessageBox.Show(this,"Restaurar el historial importado descarta los cambios de este borrador: selección, orden, tiempos y opciones. El plan guardado en disco y Steam no cambian. ¿Continuar?","Restaurar original",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
            order.Clear();
            foreach(var source in currentGame.Achievements.OrderBy(a=>a.OriginalUnix))
            {
                var a=source.Clone();a.Included=true;a.ManualGapSeconds=null;order.Add(a);
            }
            ApplyPlanParameters(new PlanParameters{DurationSeconds=LegacyPlanner.OriginalSpan(order)});
            RefreshPlan();PlanSaveStatus.Text="Historial original restaurado en el borrador. El guardado en disco no cambió.";
        }
        int InitialSeconds() => int.TryParse(InitialDelay.Text,out int delay)?Math.Clamp(delay,0,3600):10;
        void DurationChanged(object sender,TextChangedEventArgs e) {if(initialized && !updating && CanEditPlan)RefreshPlan();}
        void IncludedChanged()
        {
            if(!initialized || updating || !CanEditPlan)return;
            // En la versión 0.5.3 al cambiar selección se ajusta la duración automática al intervalo histórico seleccionado.
            SetDuration(LegacyPlanner.OriginalSpan(order));
            Dispatcher.BeginInvoke(new Action(RefreshPlan));
        }
        void RefreshPlan()
        {
            if(!initialized || updating)return;
            updating=true;
            var selectedRecord=(AchievementsGrid.SelectedItem as AchievementView)?.Record;
            var schedule=running && ReferenceEquals(currentGame,executingGame) && activeSchedule!=null ? activeSchedule : LegacyPlanner.Calculate(order,TargetDuration(),AutomaticGapLimit());
            var originals=LegacyPlanner.OriginalGaps(order);
            achievementRows.Clear();
            int index=0;
            foreach(var a in order)
            {
                var r=new AchievementView(a,IncludedChanged);
                r.Order=a.Included?(++index).ToString():"—";
                r.OriginalGap=a.Included && originals.TryGetValue(a,out double gap)?LegacyTools.FormatSpan((long)Math.Round(gap)):"—";
                if(a.Included && schedule.TryGetValue(a,out var item))
                {
                    r.NewGap=LegacyTools.FormatSpan((long)Math.Round(item.GapSeconds));
                    r.TargetOffset=LegacyTools.FormatSpan((long)Math.Round(item.OffsetSeconds));
                    r.TargetTime=running && ReferenceEquals(currentGame,executingGame) && item.Due!=default
                        ? item.Due.ToString("dd/MM HH:mm:ss")
                        : DateTime.Now.AddSeconds(InitialSeconds()+item.OffsetSeconds).ToString("dd/MM HH:mm:ss");
                }
                else {r.NewGap="—";r.TargetOffset="—";r.TargetTime="—";}
                achievementRows.Add(r);
                if(ReferenceEquals(a,selectedRecord)) AchievementsGrid.SelectedItem=r;
            }
            SelectedCount.Text="Seleccionados: "+index+" / "+order.Count;
            OriginalDuration.Text="Original: "+LegacyTools.FormatSpan(LegacyPlanner.OriginalSpan(order));
            double last=schedule.Count>0?schedule.Values.Max(x=>x.OffsetSeconds):0;
            EffectiveDuration.Text="Efectiva: "+LegacyTools.FormatSpan((long)Math.Round(last));
            GapLimitValidation.Text=GapLimitValid()?"Solo esperas automáticas; los tiempos manuales tienen prioridad.":"Ingresá un máximo mayor que 0 y hasta 525600 minutos.";
            if(!running)ExecutionSummary.Text=index+" logros · "+LegacyTools.FormatSpan((long)Math.Round(last))+" · espera inicial "+InitialSeconds()+"s";
            ReplayProgressText.Text=running ? ReplayProgressText.Text : "0 / "+index+" logros";
            updating=false;
            UpdatePlanSaveStatus();
            UpdateButtons();
        }
        void AchievementChanged(object sender,SelectionChangedEventArgs e)
        {
            var r=AchievementsGrid.SelectedItem as AchievementView;
            DetailTitle.Text=r?.DisplayName??"Seleccioná un logro";
            DetailDescription.Text=r?.Description??"";
            DetailTime.Text=r==null?"":"Fecha original: "+r.OriginalDate;
            DetailSource.Text="Fuente: "+(r==null?"—":currentGame.SourceLabel);
            DetailSteam.Text="Steam: "+(r?.SteamState??"—");
            DetailOrder.Text="Orden: "+(r?.Order??"—")+" de "+order.Count(a=>a.Included);
        }
        void AchievementCellEditEnding(object sender,DataGridCellEditEndingEventArgs e)
        {
            if(!initialized || updating || !CanEditPlan || e.EditAction!=DataGridEditAction.Commit)return;
            var row=e.Row.Item as AchievementView;
            var box=e.EditingElement as TextBox;
            if(row==null || box==null || !row.Record.Included)return;
            string column=Convert.ToString(e.Column.Header);
            if(column!="Espera nueva ✎" && column!="Momento nuevo ✎") return;
            var a=row.Record;
            string value=box.Text;
            if(String.IsNullOrWhiteSpace(value) || value.Trim()=="—") a.ManualGapSeconds=null;
            else if(!LegacyTools.TryParseDuration(value,out double seconds))
            {
                MessageBox.Show(this,"Formato de tiempo no reconocido. Ejemplos: 00:30:00, 3d 22:24:20, 90m, 1h 15m.","Tiempo manual",MessageBoxButton.OK,MessageBoxImage.Warning);
            }
            else if(column=="Espera nueva ✎")
            {
                a.ManualGapSeconds=ReferenceEquals(a,order.FirstOrDefault(x=>x.Included))?null:Math.Max(0,seconds);
            }
            else
            {
                var previous=order.TakeWhile(x=>!ReferenceEquals(x,a)).LastOrDefault(x=>x.Included);
                var schedule=LegacyPlanner.Calculate(order,TargetDuration(),AutomaticGapLimit());
                double previousOffset=previous!=null && schedule.TryGetValue(previous,out var item)?item.OffsetSeconds:0;
                double manual=seconds-previousOffset;
                if(manual<0) MessageBox.Show(this,"Ese momento queda antes que el logro anterior.","Tiempo manual",MessageBoxButton.OK,MessageBoxImage.Warning);
                else a.ManualGapSeconds=previous==null?null:manual;
            }
            Dispatcher.BeginInvoke(new Action(RefreshPlan),System.Windows.Threading.DispatcherPriority.Background);
        }
        void CustomOrderChanged(object sender,RoutedEventArgs e)
        {
            if(!initialized || updating || !CanEditPlan)return;
            if(CustomOrder.IsChecked!=true) {RestoreChronological();RefreshPlan();}
            UpdateButtons();
        }
        void MoveUp(object sender,RoutedEventArgs e)=>MoveSelected(-1);
        void MoveDown(object sender,RoutedEventArgs e)=>MoveSelected(1);
        void MoveSelected(int dir)
        {
            if(CustomOrder.IsChecked!=true || !CanEditPlan)return;
            var a=(AchievementsGrid.SelectedItem as AchievementView)?.Record;
            int i=a==null?-1:order.IndexOf(a), next=i+dir;
            if(i<0||next<0||next>=order.Count)return;
            order.RemoveAt(i);order.Insert(next,a);RefreshPlan();
        }
        void RestoreChronological()
        {
            // OrderBy es estable cuando varios logros tienen exactamente el mismo timestamp.
            var chronological=order.OrderBy(a=>a.OriginalUnix).ToList();
            order.Clear();order.AddRange(chronological);
        }
        void RestoreOrder(object sender,RoutedEventArgs e) {if(!CanEditPlan)return;RestoreChronological();RefreshPlan();}
        void ClearTimings(object sender,RoutedEventArgs e) {if(!CanEditPlan)return;foreach(var a in order)a.ManualGapSeconds=null;RefreshPlan();}
        static T Ancestor<T>(DependencyObject x) where T:DependencyObject
        {
            while(x!=null && x is not T)
                x=x is FrameworkContentElement content ? content.Parent :
                    x is Visual || x is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(x) : LogicalTreeHelper.GetParent(x);
            return x as T;
        }
        void AchievementMouseDown(object sender,MouseButtonEventArgs e)
        {
            dragRow=null;
            var origin=e.OriginalSource as DependencyObject;
            var cell=Ancestor<DataGridCell>(origin);
            // No iniciar arrastre al marcar casillas, editar tiempos o usar barras.
            if(cell==null || cell.Column.DisplayIndex==0 || !cell.Column.IsReadOnly || Ancestor<TextBox>(origin)!=null)return;
            dragStart=e.GetPosition(AchievementsGrid);
            dragRow=Ancestor<DataGridRow>(origin)?.Item as AchievementView;
        }
        void AchievementMouseMove(object sender,MouseEventArgs e)
        {
            if(!CanEditPlan || e.LeftButton!=MouseButtonState.Pressed || dragRow==null)return;
            Point position=e.GetPosition(AchievementsGrid);
            if(Math.Abs(position.X-dragStart.X)<SystemParameters.MinimumHorizontalDragDistance && Math.Abs(position.Y-dragStart.Y)<SystemParameters.MinimumVerticalDragDistance)return;
            var source=dragRow;dragRow=null;
            CustomOrder.IsChecked=true;
            AchievementsGrid.CommitEdit(DataGridEditingUnit.Row,true);
            DragDrop.DoDragDrop(AchievementsGrid,source.Record,DragDropEffects.Move);
        }
        void AchievementDragOver(object sender,DragEventArgs e)
        {
            e.Handled=true;
            e.Effects=DragDropEffects.None;
            if(!CanEditPlan || !e.Data.GetDataPresent(typeof(AchievementRecord)))return;
            e.Effects=DragDropEffects.Move;
            var scroll=FindScrollViewer(AchievementsGrid);
            var position=e.GetPosition(AchievementsGrid);
            if(scroll!=null)
            {
                if(position.Y<55)scroll.LineUp();
                else if(position.Y>AchievementsGrid.ActualHeight-30)scroll.LineDown();
            }
        }
        static ScrollViewer FindScrollViewer(DependencyObject parent)
        {
            for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++)
            {
                var child=VisualTreeHelper.GetChild(parent,i);
                if(child is ScrollViewer scroll)return scroll;
                var nested=FindScrollViewer(child);
                if(nested!=null)return nested;
            }
            return null;
        }
        void AchievementDrop(object sender,DragEventArgs e)
        {
            e.Handled=true;
            if(!CanEditPlan)return;
            var from=e.Data.GetData(typeof(AchievementRecord)) as AchievementRecord;
            var row=Ancestor<DataGridRow>(e.OriginalSource as DependencyObject);
            var to=row?.Item as AchievementView;
            if(from==null || to==null || ReferenceEquals(from,to.Record))return;
            int index=order.IndexOf(from), dest=order.IndexOf(to.Record);
            if(index<0||dest<0)return;
            // La mitad superior/inferior determina antes/después de la fila destino.
            int insertion=dest+(e.GetPosition(row).Y>=row.ActualHeight/2?1:0);
            if(index<insertion)insertion--;
            order.RemoveAt(index);order.Insert(insertion,from);
            CustomOrder.IsChecked=true;
            RefreshPlan();
            AchievementsGrid.SelectedItem=achievementRows.FirstOrDefault(a=>ReferenceEquals(a.Record,from));
            AchievementsGrid.ScrollIntoView(AchievementsGrid.SelectedItem);
            e.Effects=DragDropEffects.Move;
        }
        async void QuerySteamClick(object sender,RoutedEventArgs e)
        {
            if(!CanQueryPlan || currentGame.IsDemo)return;
            var game=currentGame;
            string cli=SamPath.Text.Trim();
            if(samMode!=SamCliMode.Modern || !File.Exists(cli))
            {
                MessageBox.Show(this,"Seleccioná y validá el CLI moderno de SamRewritten en Escaneo.","Steam",MessageBoxButton.OK,MessageBoxImage.Information);return;
            }
            querying=true;UpdateButtons();
            var replayer=activeReplayer;
            SetStatus("Consultando Steam: "+game.GameName+"…");
            try
            {
                var r=await Task.Run(()=>replayer!=null ? replayer.QueryOtherGame(cli,game.AppId) : SamRunner.List(cli,SamCliMode.Modern,game.AppId));
                if(SamRunner.Failed(r))throw new Exception(r.StdErr+"\n"+r.StdOut);
                var states=SamRunner.States(r.StdOut);
                if(states.Count==0)throw new InvalidDataException("SAM no devolvió estados de logros; se conserva el contador anterior.");
                var libraryRow=games.FirstOrDefault(g=>ReferenceEquals(g.Game,game));
                if(libraryRow!=null)libraryRow.Count=AchievementCounts.Format(game,states);
                // Si se navegó mientras llegaba la respuesta, actualizar el borrador correcto.
                var target=ReferenceEquals(currentGame,game)?order:browsedPlans.TryGetValue(game,out var cached)?cached.Records:null;
                int omitted=target==null?0:SamRunner.ApplySteamStates(target,states,true);
                if(ReferenceEquals(currentGame,game))RefreshPlan();
                SetStatus(game.GameName+": "+omitted+" logros ya desbloqueados desmarcados.");
                Log("Consulta SAM ["+game.AppId+"]: "+omitted+" logros seleccionados desmarcados.");
                PersistLibrary();
            }
            catch(Exception ex){Log("Error consultando Steam: "+ex.Message);MessageBox.Show(this,ex.Message,"SamRewritten",MessageBoxButton.OK,MessageBoxImage.Warning);}
            finally {querying=false;UpdateButtons();}
        }
        void OpenSamClick(object sender,RoutedEventArgs e)
        {
            if(checkingUpdates || currentGame==null || currentGame.IsDemo)return;
            string cli=SamPath.Text.Trim(),folder=Path.GetDirectoryName(cli),gui=null;
            try {if(Directory.Exists(folder)) gui=Directory.GetFiles(folder,"*.exe").FirstOrDefault(f=>Path.GetFileName(f).Contains("samrewritten",StringComparison.OrdinalIgnoreCase) && !Path.GetFileName(f).Contains("cli",StringComparison.OrdinalIgnoreCase));}catch{}
            if(gui==null) {MessageBox.Show(this,"No encontré la interfaz gráfica de SamRewritten junto al CLI. Abrí SAM y buscá el AppID "+currentGame.AppId,"Abrir SAM",MessageBoxButton.OK,MessageBoxImage.Information);return;}
            try {Process.Start(new ProcessStartInfo{FileName=gui,Arguments="--auto-open="+currentGame.AppId,UseShellExecute=true});}
            catch(Exception ex){MessageBox.Show(this,ex.Message,"SAM",MessageBoxButton.OK,MessageBoxImage.Warning);}
        }
        async void StartReplayClick(object sender,RoutedEventArgs e)
        {
            if(checkingUpdates||running||scanning||querying||preparing||currentGame==null || currentGame.IsDemo || !currentGame.HasLocalHistory || !GapLimitValid())return;
            RefreshLibraryAvailability();
            if(currentGame.RequiresAccountRefresh || !currentGame.HasSnapshot || !currentGame.HasLocalHistory)
            {MessageBox.Show(this,"Actualizá la biblioteca antes de ejecutar: falta un snapshot válido o cambió la cuenta Steam.","Biblioteca",MessageBoxButton.OK,MessageBoxImage.Information);UpdateButtons();return;}
            if(!CommitPlanEdits())return;
            preparing=true;UpdateButtons();
            try
            {
                var selected=order.Where(x=>x.Included).ToList();
                if(selected.Count==0) {MessageBox.Show(this,"No hay logros seleccionados.","Migración",MessageBoxButton.OK,MessageBoxImage.Warning);return;}
                if(!await ValidateSamAsync(false)) {MessageBox.Show(this,"Seleccioná y validá el ejecutable CLI de SamRewritten.","SAM",MessageBoxButton.OK,MessageBoxImage.Warning);return;}
                if(Process.GetProcessesByName("steam").Length==0) {MessageBox.Show(this,"Steam no parece estar abierto. Abrilo e iniciá sesión.","Steam",MessageBoxButton.OK,MessageBoxImage.Warning);return;}
                var schedule=LegacyPlanner.Calculate(order,TargetDuration(),AutomaticGapLimit());
                if(schedule.Count==0)return;
                var plan=schedule.Values.OrderBy(x=>x.OffsetSeconds).ToList();
                double last=schedule.Values.Max(x=>x.OffsetSeconds);
                string summary="Juego: "+currentGame.GameName+" ["+currentGame.AppId+"]\nLogros: "+selected.Count+"\nHistorial calendario original: "+LegacyTools.FormatSpan(LegacyPlanner.OriginalSpan(order))+"\nDuración automática elegida: "+LegacyTools.FormatSpan((long)TargetDuration())+"\nDuración efectiva: "+LegacyTools.FormatSpan((long)Math.Round(last))+"\nLímite por espera automática: "+(AutomaticGapLimit() is double limit?LegacyTools.FormatSpan((long)Math.Round(limit)):"desactivado")+"\nOrden: "+(CustomOrder.IsChecked==true?"personalizado":"original")+"\nBloquear antes: "+(LockFirst.IsChecked==true?"sí":"no")+"\nRegistrar tiempo de juego: "+(IdleDuringMigration.IsChecked==true?"sí":"no")+"\n\nEsto modificará los logros de Steam. ¿Continuar?";
                if(MessageBox.Show(this,summary,"Confirmar migración",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;
                string cli=SamPath.Text.Trim();uint appId=currentGame.AppId;SamCliMode mode=samMode;
                bool locked=LockFirst.IsChecked==true, idle=IdleDuringMigration.IsChecked==true;
                int delay=InitialSeconds();
                string name=currentGame.GameName;
                SetStatus("Verificando logros con SAM…");
                var verify=await Task.Run(()=>SamRunner.List(cli,mode,appId));
                if(currentGame==null || currentGame.AppId!=appId) {SetStatus("El juego seleccionado cambió durante la validación. Prepará el plan de nuevo.");return;}
                if(SamRunner.Failed(verify)) {MessageBox.Show(this,"SAM no pudo leer los logros:\n"+verify.StdErr+"\n"+verify.StdOut,"SAM",MessageBoxButton.OK,MessageBoxImage.Error);return;}
                // En modo normal, volver a comprobar antes de ejecutar: el estado pudo cambiar
                // desde la última consulta. Si cambió el plan, pedir que se revise de nuevo.
                if(mode==SamCliMode.Modern)
                {
                    try
                    {
                        var latest=SamRunner.States(verify.StdOut);
                        SamRunner.ValidateSelectedIds(order,latest);
                        int omitted=locked?0:SamRunner.ApplySteamStates(order,latest,true);
                        if(omitted>0)
                        {
                            RefreshPlan();GoTo("Plan");
                            SetStatus(omitted+" logros ya desbloqueados fueron desmarcados. Revisá el plan e iniciá nuevamente.");
                            Log(GlobalStatus.Text);
                            return;
                        }
                    }
                    catch(Exception ex)
                    {
                        MessageBox.Show(this,"No se pudo verificar el esquema/estado actual de los logros. No se inició la migración.\n"+ex.Message,"Steam",MessageBoxButton.OK,MessageBoxImage.Warning);
                        return;
                    }
                }
                ExecutionGame.Text=name;
                executingGame=currentGame;activeSchedule=schedule;
                preparing=false;
                running=true;replayCancellation=new CancellationTokenSource();
                ReplayProgress.Maximum=plan.Count;ReplayProgress.Value=0;ReplayProgressText.Text="0 / "+plan.Count+" logros";
                GoTo("Ejecucion");UpdateButtons();
                Log("=== Inicio de migración: "+name+" ===");
                var replayer=new LegacyReplayer(new ReplayEvents
                {
                    Log=Log,
                    Status=s=>Dispatcher.BeginInvoke(new Action(()=>{ReplayStatus.Text=s;SetStatus(s);})),
                    Progress=(done,total,title)=>Dispatcher.BeginInvoke(new Action(()=>{ReplayProgress.Maximum=total;ReplayProgress.Value=done;ReplayProgressText.Text=done+" / "+total+" logros · Último: "+title;}))
                });
                try
                {
                    activeReplayer=replayer;
                    await replayer.Run(plan,cli,mode,appId,name,locked,idle,delay,replayCancellation.Token);
                    ReplayStatus.Text="Migración completada.";SetStatus("Migración completada.");
                }
                catch(OperationCanceledException){Log("=== Migración cancelada por el usuario ===");ReplayStatus.Text="Cancelado.";SetStatus("Migración cancelada.");}
                catch(Exception ex){Log("ERROR: "+ex);ReplayStatus.Text="ERROR: "+ex.Message;MessageBox.Show(this,ex.Message,"Error de migración",MessageBoxButton.OK,MessageBoxImage.Error);}
                finally {running=false;activeReplayer=null;executingGame=null;activeSchedule=null;replayCancellation.Dispose();replayCancellation=null;UpdateButtons();}
            }
            finally {preparing=false;UpdateButtons();}
        }
        void CancelReplayClick(object sender,RoutedEventArgs e)
        {
            if(!running)return;
            replayCancellation?.Cancel();CancelReplayButton.IsEnabled=false;ReplayStatus.Text="Cancelando…";
        }
        void CopyLogs(object sender,RoutedEventArgs e) {Clipboard.SetText(LogBox.Text);SetStatus("Logs copiados al portapapeles.");}
        void UpdateButtons()
        {
            if(!initialized)return;
            bool available=currentGame!=null && currentGame.HasLocalHistory;
            bool live=available && currentGame.HasSnapshot && !currentGame.IsDemo && !currentGame.RequiresAccountRefresh;
            ScanButton.IsEnabled=!checkingUpdates && !scanning && !running && !querying && !preparing;
            CancelScanButton.IsEnabled=scanning;
            RefreshLibraryButton.IsEnabled=ScanButton.IsEnabled;
            RemoveLibraryButton.IsEnabled=ScanButton.IsEnabled && currentGame!=null;
            UpdateButton.IsEnabled=!checkingUpdates && !running && !scanning && !preparing && !querying;
            ImportIniButton.IsEnabled=ScanIniDefaults.IsEnabled=ImportGoldbergButton.IsEnabled=ScanGoldbergDefaults.IsEnabled=ScanButton.IsEnabled;
            DefinitionTools.Visibility=available && GoldbergHistory.NeedsDefinitions(currentGame)?Visibility.Visible:Visibility.Collapsed;
            FindDefinitionsButton.IsEnabled=CanEditPlan && !querying;
            StartButton.IsEnabled=!checkingUpdates && live && !running && !scanning && !querying && !preparing && order.Any(x=>x.Included) && GapLimitValid();
            CancelReplayButton.IsEnabled=running;
            QueryButton.IsEnabled=live && CanQueryPlan && samMode==SamCliMode.Modern;
            OpenSamButton.IsEnabled=!checkingUpdates && live && !running && !preparing && !querying && samMode==SamCliMode.Modern;
            UpButton.IsEnabled=DownButton.IsEnabled=CanEditPlan && CustomOrder.IsChecked==true;
            AchievementsGrid.IsEnabled=available;
            AchievementsGrid.IsReadOnly=!CanEditPlan;
            foreach(var control in new Control[]{DurationHours,DurationMinutes,DurationSeconds,InitialDelay,LockFirst,IdleDuringMigration,CustomOrder,LimitLongGaps}) control.IsEnabled=CanEditPlan;
            MaxGapMinutes.IsEnabled=CanEditPlan && LimitLongGaps.IsChecked==true;
            GamesGrid.IsEnabled=!scanning && !preparing;
            SteamPath.IsEnabled=SamPath.IsEnabled=ValidateSamButton.IsEnabled=BrowseSamButton.IsEnabled=BrowseSteamButton.IsEnabled=!checkingUpdates && !running && !scanning && !querying && !preparing;
            RestoreOrderButton.IsEnabled=ClearTimingsButton.IsEnabled=RestoreOriginalButton.IsEnabled=CanEditPlan;
            OriginChoice.IsEnabled=CanEditPlan;
            SavePlanButton.IsEnabled=CanEditPlan && GapLimitValid();
            LoadPlanButton.IsEnabled=CanEditPlan && planStore.Exists(currentGame);
            EditingStatus.Text=running?"En ejecución: "+executingGame?.GameName+(ReferenceEquals(currentGame,executingGame)?" · Este plan está bloqueado.":" · Podés preparar este otro juego."):"Editando borrador. Guardar plan conserva los cambios al cerrar.";
            NavScan.IsEnabled=NavLibrary.IsEnabled=NavPlan.IsEnabled=NavExecution.IsEnabled=NavLogs.IsEnabled=true;
        }
        void WindowClosing(object sender,CancelEventArgs e)
        {
            if(running || preparing || querying)
            {
                e.Cancel=true;
                MessageBox.Show(this,"Hay una operación en curso. Si es una migración, cancelala primero; si es una consulta, esperá a que termine.","Migración activa",MessageBoxButton.OK,MessageBoxImage.Information);
                GoTo("Ejecucion");return;
            }
            updateCancellation?.Cancel();scanCancellation?.Cancel();SavePaths();
        }
    }
}
