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
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
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
        string source;
        public string Source {get=>Group==null?UiText.Text(SourceSelection.DisplayText(source)?.Replace("Automático: ","")):(Group.Manual?UiText.Text("Manual: "):"")+HistoryGroup.Label(Group.Selected)+" · "+Group.Sources.Count+UiText.Text(" fuentes");set{source=value;PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(nameof(Source)));}}
        string origin;
        public string Origin
        {
            get => UiText.Text(CanonicalOrigin);
            set {if(origin!=value){origin=value;PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(nameof(Origin)));}}
        }
        internal string CanonicalOrigin=>SourceSelection.DisplayOrigin(Game!=null && PlatformMapping.IsMapped(Game)?Game.MappedFromPlatform+" → Steam":origin);
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
        internal HistoryGroup Group;
        public string SourceDetails => SourceSelection.DisplayText(Group?.Details??Source);
        public string DisplayGameId => Game.SteamTarget?Game.AppId.ToString():Game.ExternalGameId;
    }
    public sealed class AchievementView : INotifyPropertyChanged
    {
        internal AchievementRecord Record;
        readonly Action changed;
        internal AchievementView(AchievementRecord a, Action onChange) { Record=a; changed=onChange; }
        string orderNumber;
        public string Order {get=>orderNumber;set{orderNumber=value;OnPropertyChanged(nameof(Order));}}
        bool isDragging;
        public bool IsDragging {get=>isDragging;set{isDragging=value;OnPropertyChanged(nameof(IsDragging));}}
        public string DisplayName => Record.DisplayName;
        public string Description => Record.Description;
        public string OriginalDate => Record.OriginalUnix>0?LegacyTools.UnixToLocal(Record.OriginalUnix).ToString("dd/MM/yyyy HH:mm:ss"):UiText.Text("Sin fecha");
        public string OriginalGap { get; set; }
        public string NewGap { get; set; }
        public string TargetOffset { get; set; }
        public string TargetTime { get; set; }
        public string SteamState => UiText.Text(Record.SteamState);
        internal void RefreshSteamState()=>OnPropertyChanged(nameof(SteamState));
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
        readonly List<GameEntry> allHistories=new List<GameEntry>();
        Dictionary<string,string> historyChoices=new Dictionary<string,string>();
        bool rebuildingGroups,loadingHistoryChoice,deferGroups;
        string shownBaseline;
        string planPinnedSourceKey;
        string sourceChangeNotice,sourceNoticeKey;
        static string HistoryChoicesPath=>Path.Combine(LegacyTools.DataDirectory,"history-choices.json");
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
        SourceSelection sourceOptions=new SourceSelection();
        readonly Dictionary<string,CheckBox> scanSourceChecks=new Dictionary<string,CheckBox>();
        readonly Dictionary<string,ToggleButton> originToggles=new Dictionary<string,ToggleButton>();
        static string SourceOptionsPath => Path.Combine(LegacyTools.DataDirectory,"source-options.json");
        DateTimeOffset? lastLibraryScan;
        string lastLibrarySummary="";
        readonly MigrationRunStore migrationRuns=new MigrationRunStore(Path.Combine(LegacyTools.DataDirectory,"Runs"));
        MigrationRun shownRun;
        readonly PlanStore planStore=new PlanStore(Path.Combine(LegacyTools.DataDirectory,"Plans"));
        bool CanEditPlan => currentGame!=null && currentGame.HasLocalHistory && !scanning && !preparing && !checkingUpdates && (!running || !ReferenceEquals(currentGame,executingGame));
        bool CanQueryPlan => CanEditPlan && !querying && (!running || currentGame.AppId!=executingGame?.AppId);
        bool scanning;
        bool running;
        bool initialized;
        bool updating;
        string activePage="Escaneo";
        Point dragStart;
        AchievementView dragRow;
        PlanReorderSession planDrag;
        GameEntry planDragGame;
        PlanRowDragAdorner dragPreview;
        AdornerLayer dragLayer;
        Point dragPosition;
        double dragGrabY;
        bool dragInside;
        long lastDragScroll;

        public MainWindow()
        {
            InitializeComponent();
            string installedVersion=typeof(MainWindow).Assembly.GetName().Version.ToString(3);
            Title="Achievement Timeline Replay v"+installedVersion;
            ApplicationVersionLabel.Text="v"+installedVersion;
#if ATR_BRIDGE
            Loaded+=(sender,args)=>MessageBox.Show(this,UiText.Text("Actualización de transición instalada. Pulsá «Actualizar ATR» una vez más para instalar el paquete compacto."),UiText.Text("ATR · Actualizaciones"),MessageBoxButton.OK,MessageBoxImage.Information);
#endif
            InterfaceLanguageChoice.SelectedValue=UiText.Language;
            GamesGrid.ItemsSource=games;
            AchievementsGrid.ItemsSource=achievementRows;
            GamesGrid.CanUserSortColumns=true;
            GamesGrid.Columns[0].SortDirection=ListSortDirection.Ascending;
            CollectionViewSource.GetDefaultView(games).SortDescriptions.Add(new SortDescription(nameof(GameRow.GameName),ListSortDirection.Ascending));
            AchievementsGrid.CanUserSortColumns=false;
            var settings=LegacyTools.ReadSettings();
            SteamPath.Text=settings.TryGetValue("steam",out var steam) && !String.IsNullOrWhiteSpace(steam) ? steam : LegacyTools.DetectSteam();
            string savedSam=settings.TryGetValue("sam",out var sam) && !String.IsNullOrWhiteSpace(sam) ? sam : LegacyTools.DetectSam();
            SamPath.Text=String.IsNullOrWhiteSpace(savedSam)?"":Path.GetDirectoryName(savedSam);
            recoverUpdateDrafts=File.Exists(UpdateReadyPath);
            try{sourceOptions=SourceSelection.Load(SourceOptionsPath);}catch(Exception ex){Log(UiText.Text("Preferencias de fuentes: ")+ex.Message);}
            try{historyChoices=HistoryGroups.LoadChoices(HistoryChoicesPath);}catch(Exception ex){Log(UiText.Text("Selección de historiales: ")+ex.Message);}
            BuildSourceControls();
            initialized=true;UpdateConnectionNotice();
            games.CollectionChanged+=(sender,args)=>ApplyLibraryFilter();
            ApplyLibraryFilter();
            GoTo("Escaneo");
            UpdateButtons();
            Log(UiText.Text("WPF v0.19.1 iniciada. Motor derivado de v0.5.3. No se ha modificado Steam.").Replace("0.19.1",installedVersion));
            LoadSavedLibrary();
            if(!String.IsNullOrWhiteSpace(SamPath.Text)) _=ValidateSamAsync(false);
        }
        bool CanRestartForLanguage=>!running && !scanning && !preparing && !querying && !checkingUpdates && planDrag==null;
        void InterfaceLanguageChanged(object sender,SelectionChangedEventArgs e)
        {
            if(!initialized || InterfaceLanguageChoice.SelectedValue is not string language)return;
            try
            {
                UiText.SavePreference(language);
                if(language==UiText.Language){SetStatus(UiText.Text("El idioma seleccionado ya está activo."));return;}
                SetStatus(UiText.Text("Idioma guardado. Reiniciá ATR para aplicarlo."));
                var prompt=new LanguageRestartWindow(()=>CanRestartForLanguage){Owner=this};
                if(prompt.ShowDialog()==true)RestartForLanguage();
            }
            catch(Exception ex)
            {
                SetStatus(UiText.Text("No se pudo aplicar el cambio de idioma: ")+ex.Message);
                MessageBox.Show(this,UiText.Text("No se pudo aplicar el cambio de idioma: ")+ex.Message,UiText.Text("Idioma de la interfaz"),MessageBoxButton.OK,MessageBoxImage.Warning);
            }
        }
        void OpenGitHub(object sender,RoutedEventArgs e)
        {
            try { Process.Start(new ProcessStartInfo("https://github.com/ramirobornetto/atr"){UseShellExecute=true}); }
            catch(Exception ex) { MessageBox.Show(this,UiText.Text("No se pudo abrir GitHub: ")+ex.Message,"GitHub",MessageBoxButton.OK,MessageBoxImage.Information); }
        }
        void OpenCafecito(object sender,RoutedEventArgs e)
        {
            try { Process.Start(new ProcessStartInfo("https://cafecito.app/ramirobornetto"){UseShellExecute=true}); }
            catch(Exception ex) { MessageBox.Show(this,UiText.Text("No se pudo abrir Cafecito: ")+ex.Message,"Cafecito",MessageBoxButton.OK,MessageBoxImage.Information); }
        }
        void RestartForLanguage()
        {
            if(!CanRestartForLanguage)return;
            // Reuse recoverable drafts; explicitly saved plans are never overwritten.
            SaveUpdateDrafts(true);SavePaths();
            Directory.CreateDirectory(Path.GetDirectoryName(UpdateReadyPath));File.WriteAllText(UpdateReadyPath,"1");
            string executable=Environment.ProcessPath;
            using var process=Process.Start(new ProcessStartInfo{FileName=executable,WorkingDirectory=Path.GetDirectoryName(executable),UseShellExecute=true})??throw new IOException(UiText.Text("No se pudo reiniciar ATR."));
            Close();
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
            GlobalStatus.Text=(status??"").Split('\n')[0].TrimEnd('\r');
        }
        void SavePaths()
        {
            if(initialized) LegacyTools.WriteSettings(SteamPath.Text.Trim(),SamExecutablePath);
        }
        void Navigate(object sender,RoutedEventArgs e) => GoTo(Convert.ToString(((Button)sender).Tag));
        void GoTo(string screen)
        {
            activePage=screen;
            if(screen=="Biblioteca")planPinnedSourceKey=null;
            if(initialized && screen=="Biblioteca"){RefreshLibraryAvailability();UpdateButtons();}
            EscaneoPanel.Visibility=screen=="Escaneo" ? Visibility.Visible : Visibility.Collapsed;
            BibliotecaPanel.Visibility=screen=="Biblioteca" ? Visibility.Visible : Visibility.Collapsed;
            PlanPanel.Visibility=screen=="Plan" ? Visibility.Visible : Visibility.Collapsed;
            ExecutionPanel.Visibility=screen=="Ejecucion" ? Visibility.Visible : Visibility.Collapsed;
            LogsPanel.Visibility=screen=="Logs" ? Visibility.Visible : Visibility.Collapsed;
            HeaderGameDetails.Visibility=screen=="Biblioteca" || screen=="Plan" || screen=="Ejecucion" ? Visibility.Visible : Visibility.Collapsed;
            foreach(var b in new[]{NavScan,NavLibrary,NavPlan,NavExecution,NavLogs})
            {
                bool selected=Convert.ToString(b.Tag)==screen;
                b.SetValue(System.Windows.Controls.Primitives.Selector.IsSelectedProperty,selected);
                b.ClearValue(Control.ForegroundProperty);
            }
        }
        void BrowseSteam(object sender,RoutedEventArgs e)
        {
            if(running || preparing || querying)return;
            // WPF usa el diálogo nativo de Windows sin agregar paquetes externos.
            var dlg=new Microsoft.Win32.OpenFolderDialog {Title=UiText.Text("Seleccioná la carpeta raíz de Steam"),InitialDirectory=Directory.Exists(SteamPath.Text)?SteamPath.Text:Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)};
            if(dlg.ShowDialog(this)==true) {SteamPath.Text=dlg.FolderName;SavePaths();}
        }
        string SamExecutablePath
        {
            get
            {
                string value=SamPath.Text.Trim();
                // Retain compatibility with previously saved or manually pasted executable paths.
                return String.IsNullOrWhiteSpace(value)?"":value.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)?value:Path.Combine(value,"samrewritten-cli.exe");
            }
        }
        void BrowseSam(object sender,RoutedEventArgs e)
        {
            if(running || preparing || querying)return;
            var dlg=new Microsoft.Win32.OpenFolderDialog {Title=UiText.Text("Seleccioná la carpeta de SAM Rewritten"),InitialDirectory=Directory.Exists(SamPath.Text)?SamPath.Text:Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)};
            if(dlg.ShowDialog(this)==true) {SamPath.Text=dlg.FolderName;SavePaths();_=ValidateSamAsync(true);}
        }
        async void ValidateSamClick(object sender,RoutedEventArgs e) {await ValidateSamAsync(true);}
        async Task<bool> ValidateSamAsync(bool notify)
        {
            SavePaths();
            string cli=SamExecutablePath;
            if(!File.Exists(cli))
            {
                samMode=SamCliMode.Unknown;SamInfo.Text=UiText.Text("SAM: no encontrado");
                if(notify) MessageBox.Show(this,UiText.Text("No se encontró samrewritten-cli.exe en la carpeta indicada. Elegí la carpeta que contiene ese archivo."),UiText.Text("SamRewritten"),MessageBoxButton.OK,MessageBoxImage.Warning);
                UpdateButtons();return false;
            }
            SamInfo.Text=UiText.Text("Validando SamRewritten…");
            try {samMode=await Task.Run(()=>SamRunner.DetectMode(cli));}
            catch(Exception ex) {samMode=SamCliMode.Unknown;Log(UiText.Text("SAM: ")+ex.Message);}
            SamInfo.Text=samMode switch {SamCliMode.Modern=>UiText.Text("SAM: CLI moderno"),SamCliMode.Legacy=>UiText.Text("SAM: CLI legacy"),_=>UiText.Text("SAM: formato no reconocido")};
            UpdateButtons();
            if(notify) MessageBox.Show(this,samMode==SamCliMode.Unknown?UiText.Text("No pude reconocer la interfaz CLI de SAM."):UiText.Text("SamRewritten validado correctamente (")+samMode+").",UiText.Text("SAM"),MessageBoxButton.OK,samMode==SamCliMode.Unknown?MessageBoxImage.Warning:MessageBoxImage.Information);
            return samMode!=SamCliMode.Unknown;
        }
        GameRow LibraryRow(GameEntry game) => new GameRow{Game=game,GameName=game.GameName,AppId=game.AppId,Source=game.SourceLabel,Origin=ReadLibraryOrigin(game),Count=game.HasLocalHistory?AchievementCounts.Format(game):"— / "+game.TotalAchievements,Snapshot=game.HasSnapshot?UiText.Text("Protegido ✓"):UiText.Text("No disponible")};
        GameRow AdoptImportedGame(GameEntry game)
        {
            var existing=allHistories.FirstOrDefault(g=>g.StorageKey==game.StorageKey || (PlayniteHistory.IsPA(game) && PlayniteHistory.IsPA(g) && !g.PAGroupKey.Contains("|source:") && g.PAProvider==game.PAProvider && g.StorageKey==game.AlternatePlanKey));
            SavedPlan draft=null;
            if(existing!=null)
            {
                if(ReferenceEquals(currentGame,existing) && existing.HasLocalHistory)draft=PlanStore.Capture(existing,order,ReadPlanParameters());
                else if(browsedPlans.TryGetValue(existing,out var previous))draft=PlanStore.Capture(existing,previous.Records,previous.Parameters);
                allHistories.Remove(existing);browsedPlans.Remove(existing);
            }
            allHistories.Add(game);
            if(draft!=null)browsedPlans[game]=new BrowsedPlan{Records=PlanStore.Restore(game,draft).Records,Parameters=draft.Parameters};
            if(ReferenceEquals(currentGame,existing)){currentGame=null;order.Clear();achievementRows.Clear();}
            if(!deferGroups)RebuildHistoryGroups(game.StorageKey);
            return games.FirstOrDefault(r=>r.Group.Sources.Any(g=>g.StorageKey==game.StorageKey));
        }
        void RebuildHistoryGroups(string preferred=null)
        {
            preferred??=currentGame?.StorageKey;
            if(currentGame?.HasLocalHistory==true)browsedPlans[currentGame]=new BrowsedPlan{Records=order.ToList(),Parameters=ReadPlanParameters()};
            rebuildingGroups=true;
            try
            {
                var groups=HistoryGroups.Build(allHistories,historyChoices);games.Clear();
                foreach(var group in groups)
                {
                    if(group.Selected.AlternatePlanKey==null && !File.Exists(planStore.FilePath(group.Selected)))group.Selected.AlternatePlanKey=group.Sources.FirstOrDefault(g=>g!=group.Selected && planStore.Exists(g))?.StorageKey;
                    var row=LibraryRow(group.Selected);row.Group=group;row.Source=group.Summary;games.Add(row);
                }
                var changed=games.FirstOrDefault(r=>r.Group.Sources.Any(g=>g.StorageKey==preferred || g.AlternatePlanKey==preferred));
                sourceNoticeKey=changed?.Game.StorageKey;
                sourceChangeNotice=changed!=null && !changed.Group.Manual && changed.Game.StorageKey!=preferred?UiText.Text("El modo automático eligió ")+HistoryGroup.Label(changed.Game)+UiText.Text(". Los borradores y planes anteriores se conservan por fuente."):null;
                currentGame=null;order.Clear();achievementRows.Clear();
                GamesGrid.SelectedItem=GamesGrid.Items.OfType<GameRow>().FirstOrDefault(r=>r.Group.Sources.Any(g=>g.StorageKey==preferred))??GamesGrid.Items.OfType<GameRow>().FirstOrDefault();
            }
            finally{rebuildingGroups=false;}
            ApplyLibraryFilter();GameChanged(GamesGrid,null);
        }
        void PopulateHistoryChoices()
        {
            loadingHistoryChoice=true;
            try
            {
                HistorySourceChoice.Items.Clear();var row=games.FirstOrDefault(r=>r.Group.Sources.Contains(currentGame));if(row==null)return;
                HistorySourceChoice.Items.Add(new ComboBoxItem{Content=UiText.Text("Automático — más desbloqueados"),Tag="auto"});
                foreach(var source in row.Group.Sources)HistorySourceChoice.Items.Add(new ComboBoxItem{Content=HistoryGroup.Label(source)+UiText.Text(" · ")+source.Achievements.Count+"/"+source.TotalAchievements,Tag=source.StorageKey,ToolTip=source.StatsPath});
                string selected=row.Group.Manual?currentGame.StorageKey:"auto";
                HistorySourceChoice.SelectedItem=HistorySourceChoice.Items.Cast<ComboBoxItem>().FirstOrDefault(i=>(string)i.Tag==selected);
            }
            finally{loadingHistoryChoice=false;}
        }
        void HistorySourceChanged(object sender,SelectionChangedEventArgs e)
        {
            if(!initialized || loadingHistoryChoice || rebuildingGroups || currentGame==null || scanning || preparing || checkingUpdates || (running && ReferenceEquals(currentGame,executingGame)) || querying || HistorySourceChoice.SelectedItem is not ComboBoxItem choice)return;
            if(currentGame.HasLocalHistory && (!CommitPlanEdits() || !GapLimitValid())){PopulateHistoryChoices();return;}
            var row=games.FirstOrDefault(r=>r.Group.Sources.Contains(currentGame));if(row==null)return;
            string signature=PlanStore.Signature(PlanStore.Capture(currentGame,order,ReadPlanParameters()));
            if(currentGame.HasLocalHistory && (signature!=shownBaseline || planStore.Exists(currentGame)) && MessageBox.Show(this,UiText.Text("Cambiará el historial mostrado. El borrador actual se conserva para esta fuente y los planes guardados no se sobrescriben. ¿Cambiar de fuente?"),UiText.Text("Historial de origen"),MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes){PopulateHistoryChoices();return;}
            foreach(var source in row.Group.Sources)historyChoices[source.StorageKey]=(string)choice.Tag;
            try{HistoryGroups.SaveChoices(HistoryChoicesPath,historyChoices);}catch(Exception ex){Log(UiText.Text("No se pudo guardar la selección de fuente: ")+ex.Message);}
            planPinnedSourceKey=currentGame.StorageKey;RebuildHistoryGroups(currentGame.StorageKey);PersistLibrary();
        }
        void RefreshLibraryAvailability()
        {
            uint account=LegacyScanner.RecentAccount(SteamPath.Text.Trim());
            foreach(var row in games)
            {
                var game=row.Game;game.SourceMissing=!File.Exists(game.StatsPath);
                game.RequiresAccountRefresh=LibraryStore.NeedsAccountRefresh(game,account);
                game.HasSnapshot=game.HasLocalHistory && File.Exists(libraryStore.SnapshotPath(game));
                row.SourceStatus=!String.IsNullOrEmpty(game.CatalogIssue)?game.CatalogIssue:!game.HasSnapshot?UiText.Text("Sin snapshot"):game.RequiresAccountRefresh?UiText.Text("Otra cuenta · actualizar"):game.SourceMissing?UiText.Text("Archivo no encontrado"):game.RetainedFromCatalog?UiText.Text("Guardado · sin reimportar"):UiText.Text("Disponible");
                row.Snapshot=game.HasSnapshot?UiText.Text("Protegido ✓"):UiText.Text("No disponible");
            }
            ApplyLibraryFilter();
        }
        void LoadSavedLibrary()
        {
            var saved=libraryStore.Load();foreach(var warning in saved.Warnings)Log(UiText.Text("Biblioteca guardada: ")+warning);
            if(saved.Catalog==null)return;
            lastLibraryScan=saved.Catalog.LastScanAtUtc;lastLibrarySummary=saved.Catalog.Summary??"";
            foreach(var game in saved.Games){OriginDetector.Apply(game,SteamPath.Text.Trim());allHistories.Add(game);}
            RebuildHistoryGroups();
            RefreshLibraryAvailability();LibraryStamp.Text=UiText.Text("Catálogo guardado: ")+saved.Catalog.SavedAtUtc.LocalDateTime.ToString("dd/MM/yyyy HH:mm")+UiText.Text(" · Último escaneo: ")+(lastLibraryScan?.LocalDateTime.ToString("dd/MM/yyyy HH:mm")??"solo importaciones manuales")+(saved.UsedBackup?UiText.Text(" · Recuperado de copia anterior"):"");
            ScanResult.Text=UiText.Text("Biblioteca guardada cargada sin escanear. Usá Actualizar biblioteca para revisar los archivos actuales.")+(String.IsNullOrEmpty(lastLibrarySummary)?"":UiText.Text("\nResumen del último escaneo guardado:\n")+lastLibrarySummary);
            SetStatus(ScanResult.Text);Log(ScanResult.Text);
            if(games.Count>0){GoTo("Biblioteca");GamesGrid.SelectedIndex=0;GameChanged(GamesGrid,null);}
        }
        bool PersistLibrary()
        {
            try
            {
                libraryStore.Save(allHistories,SteamPath.Text.Trim(),lastLibraryScan,lastLibrarySummary);
                RefreshLibraryAvailability();LibraryStamp.Text=UiText.Text("Catálogo guardado · Último escaneo: ")+(lastLibraryScan?.LocalDateTime.ToString("dd/MM/yyyy HH:mm")??"solo importaciones manuales");return true;
            }
            catch(Exception ex){Log(UiText.Text("No se pudo guardar la biblioteca: ")+ex.Message);SetStatus(UiText.Text("Biblioteca cargada, pero no se pudo guardar el catálogo. Revisá Logs."));return false;}
        }
        string ScanSourceDirectory(string source)
        {
            string roaming=Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            switch(source)
            {
                case "Steam": return Path.Combine(SteamPath.Text.Trim(),"appcache","stats");
                case "SteamIdra/LuaTools": return Path.Combine(SteamPath.Text.Trim(),"appcache","stats")+Environment.NewLine+Path.Combine(SteamPath.Text.Trim(),"config","stplug-in");
                case "Goldberg": return Path.Combine(roaming,"Goldberg SteamEmu Saves");
                case "GSE": return Path.Combine(roaming,"GSE Saves");
                case "CODEX": case "RUNE": return String.Join(Environment.NewLine,IniHistory.DefaultRoots().Where(p=>String.Equals(Path.GetFileName(p),source,StringComparison.OrdinalIgnoreCase)));
                case "PA": return Path.Combine(roaming,"Playnite","ExtensionsData");
                case "Xbox (perfil)": case "Epic (perfil)": return UiText.Text("Consulta en línea; sin directorio local de escaneo.");
                default: return UiText.Text("Sin directorio predeterminado; solo archivos importados manualmente.");
            }
        }
        void BuildSourceControls()
        {
            foreach(string source in SourceSelection.LibraryFilterSources)
                OriginChoice.Items.Add(new ComboBoxItem{Content=UiText.Text(SourceSelection.DisplayOrigin(source)),Tag=source});
            int sourceIndex=0,sourceRows=(SourceSelection.ScanSources.Length+1)/2;
            for(int row=0;row<sourceRows;row++)ScanSourcesPanel.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
            foreach(string source in SourceSelection.ScanSources)
            {
                var check=new CheckBox{Content=UiText.Text(SourceSelection.DisplayOrigin(source)),IsChecked=sourceOptions.Scans(source),Margin=new Thickness(0,3,8,3),Tag=source};
                Grid.SetColumn(check,sourceIndex/sourceRows);Grid.SetRow(check,sourceIndex%sourceRows);sourceIndex++;
                check.Checked+=ScanSourceChanged;check.Unchecked+=ScanSourceChanged;scanSourceChecks[source]=check;ScanSourcesPanel.Children.Add(check);
                check.ToolTip=ScanSourceDirectory(source);
                check.ToolTipOpening+=(s,e)=>check.ToolTip=ScanSourceDirectory(source);
            }
            foreach(string source in SourceSelection.LibraryFilterSources)
            {
                var toggle=new ToggleButton{Tag=source,IsChecked=sourceOptions.Shows(source),Style=(Style)FindResource("OriginToggle"),ToolTip=UiText.Text("Mostrar u ocultar ")+source+UiText.Text(". No escanea ni elimina juegos.")};
                toggle.Checked+=LibraryOriginChanged;toggle.Unchecked+=LibraryOriginChanged;originToggles[source]=toggle;LibraryOriginsPanel.Children.Add(toggle);
            }
        }
        void SaveSourceOptions()
        {
            try{sourceOptions.Save(SourceOptionsPath);}catch(Exception ex){Log(UiText.Text("No se pudieron guardar las opciones de origen: ")+ex.Message);}
        }
        void ScanSourceChanged(object sender,RoutedEventArgs e)
        {
            if(!initialized)return;
            var check=(CheckBox)sender;sourceOptions.Scan[(string)check.Tag]=check.IsChecked==true;SaveSourceOptions();UpdateConnectionNotice();
        }
        void LibraryOriginChanged(object sender,RoutedEventArgs e)
        {
            if(!initialized)return;
            var toggle=(ToggleButton)sender;sourceOptions.Visible[(string)toggle.Tag]=toggle.IsChecked==true;SaveSourceOptions();ApplyLibraryFilter();
        }
        void ApplyLibraryFilter()
        {
            if(!initialized)return;
            var view=CollectionViewSource.GetDefaultView(games);string query=LibrarySearch.Text.Trim();
            view.Filter=item=>item is GameRow row && ((planPinnedSourceKey!=null && row.Group.Sources.Any(g=>g.StorageKey==planPinnedSourceKey)) || (sourceOptions.Shows(row.CanonicalOrigin) && (query.Length==0 || row.Group.Sources.Any(g=>g.GameName.IndexOf(query,StringComparison.CurrentCultureIgnoreCase)>=0) || row.DisplayGameId.Contains(query,StringComparison.OrdinalIgnoreCase))));
            int shown=view.Cast<object>().Count();LibraryCount.Text=UiText.Text("Mostrando ")+shown+UiText.Text(" de ")+games.Count;
            foreach(var pair in originToggles)pair.Value.Content=UiText.Text(SourceSelection.DisplayOrigin(pair.Key))+UiText.Text(" · ")+games.Count(r=>SourceSelection.LibraryCategory(r.CanonicalOrigin)==pair.Key);
            LibraryEmptyMessage.Visibility=shown==0?Visibility.Visible:Visibility.Collapsed;
            LibraryEmptyMessage.Text=games.Count==0?UiText.Text("La biblioteca está vacía. Incluí los orígenes deseados desde Escaneo o importá un historial."):!SourceSelection.LibrarySources.Any(sourceOptions.Shows)?UiText.Text("Todos los orígenes están ocultos. Activá uno o más filtros para mostrar sus juegos."):UiText.Text("No hay juegos que coincidan con los orígenes y la búsqueda actuales. Podés incluir otros orígenes desde Escaneo.");
        }
        void ClearLibraryClick(object sender,RoutedEventArgs e)
        {
            if(checkingUpdates || scanning || running || preparing || querying)return;
            if(MessageBox.Show(this,UiText.Text("¿Borrar toda la biblioteca guardada? Se eliminarán el catálogo y su respaldo; deberás escanear o importar de nuevo. No borra archivos de logros, snapshots, planes guardados ni orígenes personalizados. Los borradores sin guardar se descartarán."),UiText.Text("Borrar biblioteca"),MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;
            try
            {
                libraryStore.Clear();allHistories.Clear();games.Clear();order.Clear();achievementRows.Clear();browsedPlans.Clear();savedSignatures.Clear();currentGame=null;
                lastLibraryScan=null;lastLibrarySummary="";HistorySourceChoice.Items.Clear();HistorySelectionNotice.Visibility=Visibility.Collapsed;GameChanged(GamesGrid,null);HeaderGame.Text=UiText.Text("Sin juego seleccionado");HeaderGameInfo.Text=UiText.Text("Elegí un historial");PlanHistoryPath.Text="";UnknownDatesNotice.Visibility=Visibility.Collapsed;
                LibraryStamp.Text=UiText.Text("Biblioteca borrada. Escaneá o importá nuevamente para obtener los juegos.");
                ApplyLibraryFilter();UpdateButtons();SetStatus(LibraryStamp.Text);Log(LibraryStamp.Text);
            }
            catch(Exception ex){MessageBox.Show(this,UiText.Text("No se pudo borrar el catálogo: ")+ex.Message,UiText.Text("Biblioteca"),MessageBoxButton.OK,MessageBoxImage.Warning);}
        }
        void RemoveLibraryClick(object sender,RoutedEventArgs e)
        {
            if(checkingUpdates || scanning || running || preparing || querying || GamesGrid.SelectedItem is not GameRow row)return;
            if(MessageBox.Show(this,UiText.Text("¿Quitar este juego y sus fuentes de la biblioteca guardada? No borra archivos de origen, snapshots, planes ni etiquetas. Puede reaparecer al escanear si su archivo sigue en una ruta conocida."),UiText.Text("Quitar de biblioteca"),MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
            foreach(var source in row.Group.Sources){allHistories.Remove(source);browsedPlans.Remove(source);savedSignatures.Remove(source);}
            currentGame=null;order.Clear();RebuildHistoryGroups();PersistLibrary();UpdateButtons();
        }
        List<string> MissingConnections(SourceSelection selection)=>selection.MissingConnections(new XboxConnection(Path.Combine(LegacyTools.DataDirectory,"Xbox")).Connected,new EpicConnection(Path.Combine(LegacyTools.DataDirectory,"Epic")).Connected);
        void UpdateConnectionNotice()
        {
            var missing=MissingConnections(sourceOptions);
            SourceConnectionNotice.Text=String.Join(Environment.NewLine,missing.Select(key=>key=="Epic (perfil)"?UiText.Text("Epic no está conectado. Usá Conectar Epic para incluirlo en el escaneo."):UiText.Text("Xbox Live no está conectado. Usá Conectar Xbox para incluirlo en el escaneo.")));
            ConnectionNotice.Visibility=missing.Count==0?Visibility.Collapsed:Visibility.Visible;
        }
        async void ScanClick(object sender,RoutedEventArgs e)
        {
            if(checkingUpdates || scanning || running || querying || preparing) return;
            SavePaths();
            string steam=SteamPath.Text.Trim();
            string cli=SamExecutablePath;
            var selection=new SourceSelection{Scan=new Dictionary<string,bool>(sourceOptions.Scan),PAPaths=new List<string>(sourceOptions.PAPaths)};
            UpdateConnectionNotice();var missing=MissingConnections(selection);
            foreach(string key in missing){selection.Scan[key]=false;Log(SourceSelection.DisplayOrigin(key)+UiText.Text(": fuente omitida; conectá la cuenta para escanearla."));}
            if(missing.Count>0 && !SourceSelection.ScanSources.Any(selection.Scans)){ScanProgress.Value=0;ScanActivity.Visibility=Visibility.Collapsed;SetStatus(UiText.Text("Conectá una cuenta para escanear las fuentes seleccionadas."));GoTo("Escaneo");return;}
            bool scanPA=selection.Scans("PA");
            bool scanGoldberg=selection.Scans("Goldberg") || selection.Scans("GSE");
            bool scanIni=selection.Scans("CODEX") || selection.Scans("RUNE") || selection.Scans("CODEX/RUNE compatible");
            bool scanSteam=(selection.Scans("Steam") || selection.Scans("SteamIdra/LuaTools")) && Directory.Exists(Path.Combine(steam,"appcache","stats"));
            if(!SourceSelection.ScanSources.Any(selection.Scans)){MessageBox.Show(this,UiText.Text("Activá al menos un origen en Escaneo."),UiText.Text("Escaneo"),MessageBoxButton.OK,MessageBoxImage.Information);return;}
            if(currentGame!=null && (!CommitPlanEdits() || !GapLimitValid()))return;
            var knownGames=allHistories.ToList();string previousSelection=currentGame?.StorageKey;
            var drafts=new Dictionary<string,SavedPlan>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach(var pair in browsedPlans.Where(p=>p.Key.HasLocalHistory))drafts[pair.Key.StorageKey]=PlanStore.Capture(pair.Key,pair.Value.Records,pair.Value.Parameters);
                if(currentGame?.HasLocalHistory==true)drafts[currentGame.StorageKey]=PlanStore.Capture(currentGame,order,ReadPlanParameters());
            }
            catch(Exception ex){MessageBox.Show(this,UiText.Text("No se pudo conservar un borrador antes de actualizar: ")+ex.Message,UiText.Text("Biblioteca"),MessageBoxButton.OK,MessageBoxImage.Information);return;}
            scanning=true;
            scanCancellation=new CancellationTokenSource();
            var token=scanCancellation.Token;
            ScanProgress.IsIndeterminate=true;
            ScanProgress.Value=0;
            ScanResult.Text=UiText.Text("Iniciando escaneo…");ScanActivity.Text=ScanResult.Text;ScanActivity.Visibility=Visibility.Visible;
            UpdateButtons();
            SetStatus(UiText.Text("Escaneando historiales locales…"));
            Log(UiText.Text("=== Inicio de escaneo ==="));
            bool scanProgressFinished=false;
            var progress=new Progress<ScanStatus>(p=>
            {
                if(scanProgressFinished)return;
                ScanProgress.IsIndeterminate=p.Indeterminate || p.Total==0;
                if(p.Total>0) {ScanProgress.Maximum=p.Total;ScanProgress.Value=Math.Min(p.Current,p.Total);}
                ScanResult.Text=p.Message;ScanActivity.Text=p.Message;ScanActivity.Visibility=Visibility.Visible;
                SetStatus(p.Message);
            });
            var phaseNames=new List<string>();
            if(scanSteam)phaseNames.Add("Steam");if(scanGoldberg)phaseNames.Add("Goldberg / GSE");if(scanIni)phaseNames.Add("CODEX / RUNE");if(scanPA)phaseNames.Add("Playnite Achievements");if(selection.Scans("Xbox (perfil)"))phaseNames.Add("Xbox Live");if(selection.Scans("Epic (perfil)"))phaseNames.Add("Epic Games");
            phaseNames.Add("Consolidación");phaseNames.Add("Guardar biblioteca");
            var phases=new ScanPhases(phaseNames,progress);
            try
            {
                var scanner=new LegacyScanner(Log);
                var goldbergSummary=new ScanSummary();
                var iniSummary=new ScanSummary();
                var epicSummary=new RemoteScanSummary();var xboxSummary=new RemoteScanSummary();int paErrors=0;
                var found=await Task.Run(()=>
                {
                    var all=new List<GameEntry>();
                    if(scanSteam){phases.Begin("Steam");all.AddRange(scanner.Scan(steam,cli,samMode,new ImmediateProgress<ScanStatus>(phases.Report),token,id=>selection.AllowsSteam(id,steam)));}
                    if(scanGoldberg)
                    {
                        phases.Begin("Goldberg / GSE");
                        foreach(var root in GoldbergHistory.DefaultRoots().Where(root=>Directory.Exists(root) && selection.Scans(Path.GetFileName(root)=="GSE Saves"?"GSE":"Goldberg")))
                            all.AddRange(GoldbergHistory.Scan(root,steam,Path.Combine(LegacyTools.DataDirectory,"Snapshots"),Log,token,goldbergSummary));
                    }
                    if(scanIni)
                    {
                        phases.Begin("CODEX / RUNE");
                        foreach(var root in IniHistory.DefaultRoots().Where(root=>Directory.Exists(root) && selection.Scans(Path.GetFileName(root).ToUpperInvariant())))
                            all.AddRange(IniHistory.Scan(root,steam,Path.Combine(LegacyTools.DataDirectory,"Snapshots"),Log,token,iniSummary));
                    }
                    if(scanPA)
                    {
                        phases.Begin("Playnite Achievements");
                        var paths=PlayniteHistory.FindDatabases().Concat(selection.PAPaths).Concat(knownGames.Where(PlayniteHistory.IsPA).Select(g=>g.StatsPath)).Distinct(StringComparer.OrdinalIgnoreCase).Where(File.Exists);
                        foreach(var path in paths)
                        {
                            token.ThrowIfCancellationRequested();
                            try
                            {
                                var imported=PlayniteHistory.Load(path,Log,token,true).Where(g=>selection.Allows(g,steam)).ToList();
                                foreach(var fresh in imported)
                                {
                                    var old=knownGames.FirstOrDefault(g=>PlayniteHistory.IsPA(g) && !g.PAGroupKey.Contains("|source:") && g.Platform==fresh.Platform && g.AppId==fresh.AppId && g.PAProvider==fresh.PAProvider && g.PAGroupKey.Split('|')[0]==fresh.PAGroupKey.Split('|')[0]);
                                    if(old!=null)fresh.AlternatePlanKey=old.StorageKey;
                                }
                                all.AddRange(imported);
                            }
                            catch(OperationCanceledException){throw;}
                            catch(Exception ex){paErrors++;Log(UiText.Text("Playnite Achievements: ")+ex.Message);}
                        }
                    }
                    if(selection.Scans("Xbox (perfil)"))
                    {
                        phases.Begin("Xbox Live");
                        try
                        {
                            using var api=new XboxProfile();var connection=new XboxConnection(Path.Combine(LegacyTools.DataDirectory,"Xbox"));
                            var session=connection.SessionAsync(api,token).GetAwaiter().GetResult();
                            var titles=api.TitlesAsync(session,token).GetAwaiter().GetResult();int completed=0;
                            foreach(var title in titles)
                            {
                                token.ThrowIfCancellationRequested();
                                phases.Report(new ScanStatus{Current=++completed,Total=titles.Count,Message=UiText.Text("Xbox: ")+completed+"/"+titles.Count+UiText.Text(" · ")+title.Name});
                                try
                                {
                                    var history=api.HistoryAsync(session,title,Path.Combine(LegacyTools.DataDirectory,"Xbox","Histories"),token).GetAwaiter().GetResult();
                                    if(!XboxProfile.ShouldInclude(history)){xboxSummary.Record(history.TotalAchievements==0?RemoteScanKind.NoCatalog:RemoteScanKind.NoUnlocks);Log(UiText.Text("Xbox Live · ")+title.Name+UiText.Text(": omitido, ")+(history.TotalAchievements==0?UiText.Text("sin catálogo disponible."):UiText.Text("sin desbloqueos.")));continue;}
                                    XboxProfile.SaveHistory(history,Path.Combine(LegacyTools.DataDirectory,"Snapshots"));all.Add(history);xboxSummary.Record(RemoteScanKind.Recovered);
                                }
                                catch(OperationCanceledException){throw;}
                                catch(XboxServiceException ex){xboxSummary.Record(RemoteScanKind.Error);xboxSummary.StopReason=ex.Message;Log(UiText.Text("Xbox: ")+ex.Message+UiText.Text(" Se detiene la consulta; se conservan los historiales anteriores."));break;}
                                catch(Exception ex){xboxSummary.Record(RemoteScanKind.Error);Log(UiText.Text("Xbox · ")+title.Name+UiText.Text(": ")+ex.Message+UiText.Text(" Se conserva el historial anterior, si existe."));}
                            }
                        }
                        catch(OperationCanceledException){throw;}
                        catch(Exception ex){xboxSummary.FailPhase(ex.Message);Log(UiText.Text("Xbox: ")+ex.Message+UiText.Text(" Los historiales guardados se conservan."));}
                    }
                    if(selection.Scans("Epic (perfil)"))
                    {
                        phases.Begin("Epic Games");
                        try
                        {
                            using var api=new EpicProfile();var connection=new EpicConnection(Path.Combine(LegacyTools.DataDirectory,"Epic"));
                            var session=connection.SessionAsync(api,token).GetAwaiter().GetResult();
                            api.RenewSession=ct=>connection.SessionAsync(api,ct);
                            var assets=api.GamesAsync(session,token).GetAwaiter().GetResult();int completed=0;
                            foreach(var asset in assets)
                            {
                                token.ThrowIfCancellationRequested();phases.Report(new ScanStatus{Current=++completed,Total=assets.Count,Message=UiText.Text("Epic: ")+completed+"/"+assets.Count+UiText.Text(" · ")+(asset.Title??asset.AppName)});
                                try
                                {
                                    var lookup=api.LookupAsync(session,asset,Path.Combine(LegacyTools.DataDirectory,"Epic","Histories"),Log,token).GetAwaiter().GetResult();
                                    if(lookup.Kind!=RemoteScanKind.Recovered)
                                    {
                                        epicSummary.Record(lookup.Kind);
                                        string reason=lookup.Kind==RemoteScanKind.NoCatalog?UiText.Text("sin catálogo disponible"):lookup.Kind==RemoteScanKind.NoProgress?UiText.Text("sin progreso disponible (no equivale a cero desbloqueos)"):UiText.Text("sin desbloqueos");
                                        Log(UiText.Text("Epic · ")+(asset.Title??asset.AppName)+UiText.Text(": omitido, ")+reason+".");continue;
                                    }
                                    var history=lookup.Game;
                                    if(all.Any(g=>g.StorageKey==history.StorageKey)){epicSummary.Record(RemoteScanKind.Duplicate);continue;}
                                    EpicProfile.SaveHistory(history,Path.Combine(LegacyTools.DataDirectory,"Snapshots"));all.Add(history);epicSummary.Record(RemoteScanKind.Recovered);
                                }
                                catch(OperationCanceledException){throw;}
                                catch(EpicServiceException ex) when(ex.StatusCode==403){epicSummary.Record(RemoteScanKind.Error);Log(UiText.Text("Epic · ")+(asset.Title??asset.AppName)+UiText.Text(": ")+ex.Message+UiText.Text(" Se continúa con otros títulos."));}
                                catch(EpicServiceException ex){epicSummary.Record(RemoteScanKind.Error);epicSummary.StopReason=ex.Message;Log(UiText.Text("Epic: ")+ex.Message+UiText.Text(" Se detiene la consulta; se conservan los historiales anteriores."));break;}
                                catch(Exception ex){epicSummary.Record(RemoteScanKind.Error);Log(UiText.Text("Epic · ")+(asset.Title??asset.AppName)+UiText.Text(": ")+ex.Message+UiText.Text(" Se conserva el historial anterior, si existe."));}
                            }
                        }
                        catch(OperationCanceledException){throw;}
                        catch(Exception ex){epicSummary.FailPhase(ex.Message);Log(UiText.Text("Epic: ")+ex.Message+UiText.Text(" Los historiales guardados se conservan."));}
                    }
                    phases.Begin("Consolidación");
                    // Compare PA against current native progress, preserving its first snapshot separately.
                    foreach(var direct in all.Where(g=>!PlayniteHistory.IsPA(g) && !XboxProfile.IsXbox(g) && !EpicProfile.IsEpic(g) && !PlatformMapping.IsMapped(g) && !g.UsesCurrentSnapshot).ToList())
                    {
                        var paMatches=all.Where(g=>PlayniteHistory.IsPA(g) && g.AppId==direct.AppId).ToList();if(paMatches.Count==0)continue;
                        try
                        {
                            string schema=Path.Combine(steam,"appcache","stats","UserGameStatsSchema_"+direct.AppId+".bin");
                            var fresh=String.IsNullOrEmpty(direct.HistoryId)?HistoryLoader.Load(direct.StatsPath,schema,direct.AccountId,direct.AppId):direct.HistoryId.StartsWith("goldberg_",StringComparison.Ordinal)?GoldbergHistory.Load(direct.StatsPath,direct.AppId,File.Exists(direct.SchemaPath)?direct.SchemaPath:null,schema,Log):IniHistory.Load(direct.StatsPath,direct.AppId,schema,Log);
                            bool continuation=direct.Achievements.Any(a=>fresh.Achievements.Any(b=>String.Equals(a.ApiId,b.ApiId,StringComparison.OrdinalIgnoreCase) && a.OriginalUnix==b.OriginalUnix)) && direct.Achievements.All(a=>!fresh.Achievements.Any(b=>String.Equals(a.ApiId,b.ApiId,StringComparison.OrdinalIgnoreCase) && a.OriginalUnix!=b.OriginalUnix));
                            if(continuation && fresh.Achievements.Count>direct.Achievements.Count && paMatches.Any(pa=>PlayniteHistory.SameHistory(fresh,pa)))
                            {fresh.GameName=direct.GameName;OriginDetector.Apply(fresh,steam);if(String.IsNullOrEmpty(fresh.HistoryId)){fresh.UsesCurrentSnapshot=true;PlayniteHistory.ProtectCurrent(fresh,Path.Combine(LegacyTools.DataDirectory,"Snapshots"));}else HistorySnapshots.ProtectLatest(fresh,Path.Combine(LegacyTools.DataDirectory,"Snapshots"));all[all.IndexOf(direct)]=fresh;Log(UiText.Text("Se actualizó el historial directo para compararlo con Playnite Achievements; el snapshot original se conserva."));}
                        }
                        catch(Exception ex){Log(UiText.Text("Comparación de progreso directo con Playnite Achievements: ")+ex.Message);}
                    }
                    var seen=new HashSet<string>(all.Select(g=>g.StorageKey),StringComparer.OrdinalIgnoreCase);
                    foreach(var known in knownGames.Where(g=>!String.IsNullOrEmpty(g.HistoryId) && !PlayniteHistory.IsPA(g) && !XboxProfile.IsXbox(g) && !EpicProfile.IsEpic(g) && !PlatformMapping.IsMapped(g)))
                    {
                        token.ThrowIfCancellationRequested();if(seen.Contains(known.StorageKey) || !selection.Allows(known,steam) || !File.Exists(known.StatsPath))continue;
                        try
                        {
                            string schema=Path.Combine(steam,"appcache","stats","UserGameStatsSchema_"+known.AppId+".bin");
                            var refreshed=known.HistoryId.StartsWith("goldberg_",StringComparison.Ordinal)?GoldbergHistory.Load(known.StatsPath,known.AppId,File.Exists(known.SchemaPath)?known.SchemaPath:null,schema,Log):IniHistory.Load(known.StatsPath,known.AppId,schema,Log);
                            refreshed=HistorySnapshots.Protect(refreshed,Path.Combine(LegacyTools.DataDirectory,"Snapshots"));all.Add(refreshed);seen.Add(refreshed.StorageKey);
                        }
                        catch(Exception ex){Log(UiText.Text("No se pudo reimportar la fuente recordada ")+known.StatsPath+UiText.Text(": ")+ex.Message);}
                    }
                    foreach(var refreshed in all.Where(g=>g.HistoryId?.StartsWith("goldberg_",StringComparison.Ordinal)==true))
                    {
                        var known=knownGames.FirstOrDefault(g=>g.StorageKey==refreshed.StorageKey);
                        if(known==null)continue;
                        if(GoldbergHistory.NeedsDefinitions(refreshed) && File.Exists(known.SchemaPath))
                            try{GoldbergHistory.ApplyDefinitions(refreshed,known.SchemaPath);}catch(Exception ex){Log(UiText.Text("Definición recordada no disponible: ")+ex.Message);}
                        var labels=known.Achievements.ToDictionary(a=>a.ApiId,StringComparer.OrdinalIgnoreCase);
                        foreach(var achievement in refreshed.Achievements)
                            if(labels.TryGetValue(achievement.ApiId,out var label))
                            {
                                if(achievement.DisplayName==achievement.ApiId || String.IsNullOrWhiteSpace(achievement.DisplayName))achievement.DisplayName=label.DisplayName;
                                if(String.IsNullOrEmpty(achievement.Description))achievement.Description=label.Description;
                            }
                    }
                    foreach(var known in knownGames)
                    {
                        if(!XboxProfile.ShouldInclude(known) || !EpicProfile.ShouldInclude(known))continue;
                        if(PlayniteHistory.IsPA(known) && all.Any(g=>g.AlternatePlanKey==known.StorageKey))continue;
                        if(LibraryStore.ShouldRetainAfterScan(known,selection.Allows(known,steam)) && seen.Add(known.StorageKey)){known.RetainedFromCatalog=true;all.Add(known);}
                    }
                    GameNames.Resolve(all.Where(g=>!String.IsNullOrEmpty(g.HistoryId) && !PlayniteHistory.IsPA(g) && !XboxProfile.IsXbox(g) && !EpicProfile.IsEpic(g) && !PlatformMapping.IsMapped(g) && !g.RetainedFromCatalog),Log,token);
                    foreach(var game in all.Where(PlayniteHistory.IsPA))
                    {
                        game.AlternatePlanKey??=knownGames.FirstOrDefault(g=>g.StorageKey==game.StorageKey)?.AlternatePlanKey;
                        if(!game.RetainedFromCatalog)PlayniteHistory.ProtectCurrent(game,Path.Combine(LegacyTools.DataDirectory,"Snapshots"));
                    }
                    return all.DistinctBy(g=>g.StorageKey).OrderBy(g=>g.GameName,StringComparer.CurrentCultureIgnoreCase).ToList();
                },token);
                token.ThrowIfCancellationRequested();
                phases.Begin("Guardar biblioteca");
                rebuildingGroups=true;games.Clear();rebuildingGroups=false;allHistories.Clear();achievementRows.Clear();order.Clear();browsedPlans.Clear();savedSignatures.Clear();currentGame=null;
                foreach(var g in found)
                {
                    if(g.HasLocalHistory && (drafts.TryGetValue(g.StorageKey,out var draft) || (g.AlternatePlanKey!=null && drafts.TryGetValue(g.AlternatePlanKey,out draft))))
                        try{browsedPlans[g]=new BrowsedPlan{Records=PlanStore.Restore(g,draft).Records,Parameters=draft.Parameters};}catch(Exception ex){Log(UiText.Text("Borrador no restaurado: ")+ex.Message);}
                    allHistories.Add(g);
                }
                RebuildHistoryGroups(previousSelection);
                ApplyLibraryFilter();
                bool scanIssues=epicSummary.HasErrors || xboxSummary.HasErrors || paErrors>0 || scanner.Summary.ReadErrors+scanner.Summary.SnapshotErrors+goldbergSummary.ReadErrors+goldbergSummary.SnapshotErrors+iniSummary.ReadErrors+iniSummary.SnapshotErrors>0;
                ScanResult.Text=(scanIssues?UiText.Text("Actualización terminada con incidencias: "):UiText.Text("Actualización terminada: "))+games.Count+UiText.Text(" juegos/perfiles · ")+allHistories.Count+UiText.Text(" fuentes (")+found.Count(g=>g.RetainedFromCatalog)+UiText.Text(" conservados del catálogo).\n")+(scanSteam?scanner.Summary.Format("Steam"):UiText.Text("Steam/BIN: desactivado o appcache/stats no disponible"))+(scanGoldberg?"\n"+goldbergSummary.Format("Goldberg/GBE"):"")+(scanIni?"\n"+iniSummary.Format("CODEX/RUNE"):"")+(selection.Scans("Xbox (perfil)")?"\n"+xboxSummary.Format("Xbox Live"):"")+(selection.Scans("Epic (perfil)")?"\n"+epicSummary.Format("Epic"):"")+(scanPA?UiText.Text("\nPlaynite Achievements: ")+found.Count(PlayniteHistory.IsPA)+UiText.Text(" historiales en biblioteca · ")+paErrors+UiText.Text(" errores de lectura. Los desbloqueos sin fecha se conservan."):"");
                SetStatus(UiText.Text("Escaneo finalizado")+(scanIssues?UiText.Text(" con incidencias"):"")+UiText.Text(" · ")+games.Count+UiText.Text(" juegos en biblioteca.")+(scanIssues?UiText.Text(" Revisá Logs."):""));
                Log(ScanResult.Text);
                lastLibraryScan=DateTimeOffset.UtcNow;lastLibrarySummary=ScanResult.Text;
                bool persisted=PersistLibrary();scanProgressFinished=true;
                ScanProgress.IsIndeterminate=false;ScanProgress.Maximum=1000;if(persisted){phases.Complete();ScanProgress.Value=1000;}
                else {ScanResult.Text+=UiText.Text("\nNo se pudo guardar el catálogo. Revisá Logs.");SetStatus(UiText.Text("No se pudo guardar la biblioteca. Revisá Logs."));}
                GoTo("Biblioteca");
                GamesGrid.SelectedItem=GamesGrid.Items.OfType<GameRow>().FirstOrDefault(r=>r.Group.Sources.Any(g=>g.StorageKey==previousSelection || g.AlternatePlanKey==previousSelection))??GamesGrid.Items.OfType<GameRow>().FirstOrDefault();
            }
            catch(OperationCanceledException)
            {
                ScanResult.Text=UiText.Text("Escaneo cancelado.");SetStatus(ScanResult.Text);Log(ScanResult.Text);
            }
            catch(Exception ex)
            {
                ScanResult.Text=UiText.Text("ERROR: ")+ex.Message;Log(ex.ToString());SetStatus(ScanResult.Text);
                MessageBox.Show(this,ex.Message,UiText.Text("Escaneo"),MessageBoxButton.OK,MessageBoxImage.Error);
            }
            finally {ScanActivity.Visibility=Visibility.Collapsed;scanProgressFinished=true;scanning=false;scanCancellation.Dispose();scanCancellation=null;ScanProgress.IsIndeterminate=false;GameChanged(GamesGrid,null);UpdateButtons();}
        }
        async void ImportGoldbergClick(object sender,RoutedEventArgs e)
        {
            if(checkingUpdates || scanning || running || querying || preparing)return;
            var dialog=new OpenFileDialog{Title=UiText.Text("Seleccioná el estado Goldberg: <AppID>/achievements.json (no el de steam_settings)"),Filter=UiText.Text("Estado Goldberg (achievements.json)|achievements.json|JSON (*.json)|*.json")};
            var existing=GoldbergHistory.DefaultRoots().FirstOrDefault(Directory.Exists);
            if(existing!=null)dialog.InitialDirectory=existing;
            if(dialog.ShowDialog(this)!=true)return;
            string folder=Path.GetDirectoryName(dialog.FileName);
            if(!UInt32.TryParse(Path.GetFileName(folder),out uint appId) || appId==0)
            {MessageBox.Show(this,UiText.Text("El archivo de estado debe estar dentro de una carpeta con el AppID del juego como nombre. No selecciones la definición de steam_settings."),UiText.Text("Goldberg"),MessageBoxButton.OK,MessageBoxImage.Information);return;}
            string definitions=Path.Combine(folder,"steam_settings","achievements.json");
            if(!File.Exists(definitions))definitions=null;
            string statePath=dialog.FileName,steam=SteamPath.Text.Trim();
            scanning=true;scanCancellation=new CancellationTokenSource();var token=scanCancellation.Token;UpdateButtons();
            ScanProgress.IsIndeterminate=true;SetStatus(UiText.Text("Importando Goldberg…"));
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
                ScanResult.Text=game.IsDemo?UiText.Text("Demostración cargada. Podés editar y guardar planes; Steam y SAM están bloqueados para esta muestra."):UiText.Text("Goldberg importado. Solo se añadieron logros desbloqueados con fecha válida. Revisá el log para omisiones.");
                ApplyLibraryFilter();SetStatus(ScanResult.Text);Log(ScanResult.Text);PersistLibrary();GoTo("Plan");
            }
            catch(OperationCanceledException){SetStatus(UiText.Text("Importación Goldberg cancelada."));}
            catch(Exception ex){Log(ex.ToString());MessageBox.Show(this,ex.Message,UiText.Text("Goldberg"),MessageBoxButton.OK,MessageBoxImage.Warning);}
            finally{scanning=false;scanCancellation.Dispose();scanCancellation=null;ScanProgress.IsIndeterminate=false;UpdateButtons();}
        }
        void FindDefinitionsClick(object sender,RoutedEventArgs e)
        {
            if(!CanEditPlan || querying || !GoldbergHistory.NeedsDefinitions(currentGame) || !CommitPlanEdits())return;
            var dialog=new OpenFileDialog{Title=UiText.Text("Elegí steam_settings/achievements.json del mismo juego"),Filter=UiText.Text("Definición Goldberg (achievements.json)|achievements.json|JSON (*.json)|*.json")};
            if(dialog.ShowDialog(this)!=true)return;
            try
            {
                GoldbergHistory.ApplyDefinitions(currentGame,dialog.FileName);
                var metadata=currentGame.Achievements.ToDictionary(a=>a.ApiId,StringComparer.OrdinalIgnoreCase);
                foreach(var record in order)
                    if(metadata.TryGetValue(record.ApiId,out var source)){record.DisplayName=source.DisplayName;record.Description=source.Description;}
                var row=games.First(r=>ReferenceEquals(r.Game,currentGame));row.Count=AchievementCounts.Format(currentGame);
                RefreshPlan();PersistLibrary();UpdateButtons();
                SetStatus(GoldbergHistory.NeedsDefinitions(currentGame)?UiText.Text("Definición cargada. Algunos IDs siguen sin nombre en ese archivo."):UiText.Text("Nombres y descripciones cargados; el orden y los tiempos del plan se conservaron."));
            }
            catch(Exception ex){MessageBox.Show(this,ex.Message,UiText.Text("Definición de nombres"),MessageBoxButton.OK,MessageBoxImage.Information);}
        }
        void ConnectEpicClick(object sender,RoutedEventArgs e)
        {
            if(checkingUpdates || scanning || running || querying || preparing)return;
            var dialog=new EpicConnectionWindow(new EpicConnection(Path.Combine(LegacyTools.DataDirectory,"Epic"))){Owner=this};dialog.ShowDialog();
            if(dialog.DisconnectedNow)scanSourceChecks["Epic (perfil)"].IsChecked=false;
            if(dialog.ConnectedNow){scanSourceChecks["Epic (perfil)"].IsChecked=true;SetStatus(UiText.Text("Epic conectado. Podés iniciar el escaneo."));}
            UpdateConnectionNotice();
        }
        void ConnectXboxClick(object sender,RoutedEventArgs e)
        {
            if(checkingUpdates || scanning || running || querying || preparing)return;
            var dialog=new XboxConnectionWindow(new XboxConnection(Path.Combine(LegacyTools.DataDirectory,"Xbox"))){Owner=this};
            dialog.ShowDialog();
            if(dialog.DisconnectedNow)scanSourceChecks["Xbox (perfil)"].IsChecked=false;
            if(dialog.ConnectedNow){scanSourceChecks["Xbox (perfil)"].IsChecked=true;SetStatus(UiText.Text("Xbox conectado. Podés iniciar el escaneo."));}
            UpdateConnectionNotice();
        }
        async void ImportPAClick(object sender,RoutedEventArgs e)
        {
            if(checkingUpdates || scanning || running || querying || preparing)return;
            if(currentGame!=null && (!CommitPlanEdits() || !GapLimitValid()))return;
            var dialog=new OpenFileDialog{Title=UiText.Text("Elegí achievement_cache.db de Playnite Achievements (Playnite Achievements)"),Filter=UiText.Text("Caché Playnite Achievements (achievement_cache.db)|achievement_cache.db")};
            var defaultPath=PlayniteHistory.FindDatabases().FirstOrDefault();if(defaultPath!=null)dialog.InitialDirectory=Path.GetDirectoryName(defaultPath);
            if(dialog.ShowDialog(this)!=true)return;
            scanning=true;scanCancellation=new CancellationTokenSource();var token=scanCancellation.Token;UpdateButtons();ScanProgress.IsIndeterminate=true;
            try
            {
                var imported=await Task.Run(()=>PlayniteHistory.Load(dialog.FileName,Log,token,true),token);token.ThrowIfCancellationRequested();
                deferGroups=true;
                foreach(var game in imported)
                {
                    var old=allHistories.FirstOrDefault(g=>PlayniteHistory.IsPA(g) && !g.PAGroupKey.Contains("|source:") && g.PAProvider==game.PAProvider && g.Platform==game.Platform && g.AppId==game.AppId && g.PAGroupKey.Split('|')[0]==game.PAGroupKey.Split('|')[0]);
                    if(old!=null)game.AlternatePlanKey=old.StorageKey;
                }
                try{foreach(var game in imported){PlayniteHistory.ProtectCurrent(game,Path.Combine(LegacyTools.DataDirectory,"Snapshots"));AdoptImportedGame(game);}}
                finally{deferGroups=false;}
                if(!sourceOptions.PAPaths.Contains(dialog.FileName,StringComparer.OrdinalIgnoreCase) && sourceOptions.PAPaths.Count<64){sourceOptions.PAPaths.Add(dialog.FileName);SaveSourceOptions();}
                RebuildHistoryGroups();
                ScanResult.Text=UiText.Text("Playnite Achievements: ")+imported.Count+UiText.Text(" historiales importados. Los logros sin fecha se conservaron. Actualizar biblioteca compara también las fuentes directas.");
                PersistLibrary();SetStatus(ScanResult.Text);Log(ScanResult.Text);GoTo("Biblioteca");
            }
            catch(OperationCanceledException){SetStatus(UiText.Text("Importación Playnite Achievements cancelada."));}
            catch(Exception ex){Log(ex.ToString());MessageBox.Show(this,ex.Message,UiText.Text("Playnite Achievements"),MessageBoxButton.OK,MessageBoxImage.Information);}
            finally{scanning=false;scanCancellation.Dispose();scanCancellation=null;ScanProgress.IsIndeterminate=false;GameChanged(GamesGrid,null);UpdateButtons();}
        }
        void MapPlatformClick(object sender,RoutedEventArgs e)
        {
            if(!CanEditPlan || querying || currentGame.SteamTarget || !CommitPlanEdits())return;
            var source=currentGame;var parameters=ReadPlanParameters();
            var names=new Dictionary<uint,string>();querying=true;UpdateButtons();
            try
            {
                async Task<List<SteamDefinition>> Load(uint appId)
                {
                    var known=allHistories.FirstOrDefault(g=>g.SteamTarget && g.AppId==appId);if(known!=null)names[appId]=known.GameName;
                    var databases=SteamDefinitionSources.Databases(allHistories,sourceOptions.PAPaths.Concat(PlayniteHistory.FindDatabases()));
                    string schema=Path.Combine(SteamPath.Text.Trim(),"appcache","stats","UserGameStatsSchema_"+appId+".bin");
                    return await Task.Run(()=>
                    {
                        var local=SteamDefinitionSources.Load(schema,appId,databases,Log);
                        if(!String.IsNullOrWhiteSpace(local.Name))names[appId]=local.Name;
                        if(local.Definitions.Count>0)return local.Definitions;
                        string cli=SamPath.Dispatcher.Invoke(()=>SamExecutablePath);
                        if(samMode!=SamCliMode.Modern)throw new InvalidDataException(UiText.Text("No hay esquema local ni caché Playnite Achievements del destino. Validá el CLI moderno de SAM para consultarlo."));
                        var result=activeReplayer!=null?activeReplayer.QueryOtherGame(cli,appId):SamRunner.List(cli,SamCliMode.Modern,appId);
                        if(SamRunner.Failed(result))throw new InvalidDataException(UiText.Text("SAM rechazó la consulta del destino: ")+result.StdErr+" "+result.StdOut);
                        return PlatformMapping.FromSam(result.StdOut);
                    });
                }
                async Task<List<SteamGameMatch>> Search(string term)
                {
                    var paths=SteamDefinitionSources.Databases(allHistories,sourceOptions.PAPaths.Concat(PlayniteHistory.FindDatabases()));
                    var local=await Task.Run(()=>SteamDiscovery.LocalGames(paths,allHistories,term));
                    try
                    {
                        var online=await SteamDiscovery.SearchAsync(term);
                        return local.Concat(online).DistinctBy(g=>g.AppId).OrderByDescending(g=>String.Equals(g.Name,term,StringComparison.OrdinalIgnoreCase)).ThenBy(g=>g.Name,StringComparer.CurrentCultureIgnoreCase).ToList();
                    }
                    catch(Exception ex){Log(UiText.Text("Búsqueda pública Steam: ")+ex.Message);if(local.Count>0)return local;throw;}
                }
                var window=new PlatformMappingWindow(source.WithRecords(order),Path.Combine(LegacyTools.DataDirectory,"Mappings"),Load,id=>names.TryGetValue(id,out var title)?title:"AppID "+id+UiText.Text(" (verificá el nombre y edición en Steam)"),Search){Owner=this};
                if(window.ShowDialog()==true && window.Result!=null)
                {
                    var mapped=window.Result;bool existingMapped=allHistories.Any(g=>g.StorageKey==mapped.StorageKey);PlayniteHistory.ProtectCurrent(mapped,Path.Combine(LegacyTools.DataDirectory,"Snapshots"));
                    AdoptImportedGame(mapped);if(!existingMapped)browsedPlans[mapped]=new BrowsedPlan{Records=mapped.Achievements.Select(a=>a.Clone()).ToList(),Parameters=parameters};
                    var destinationGroup=games.First(r=>r.Group.Sources.Contains(mapped)).Group;
                    foreach(var entry in destinationGroup.Sources)historyChoices[entry.StorageKey]=mapped.StorageKey;
                    HistoryGroups.SaveChoices(HistoryChoicesPath,historyChoices);planPinnedSourceKey=mapped.StorageKey;RebuildHistoryGroups(mapped.StorageKey);
                    PersistLibrary();SetStatus(UiText.Text("Plan Steam preparado con equivalencias confirmadas. Revisá tiempos y Consultar Steam antes de iniciar."));GoTo("Plan");
                }
            }
            catch(Exception ex){Log(ex.ToString());MessageBox.Show(this,ex.Message,UiText.Text("Correspondencias"),MessageBoxButton.OK,MessageBoxImage.Information);}
            finally{querying=false;UpdateButtons();}
        }
        async void ImportIniClick(object sender,RoutedEventArgs e)
        {
            if(checkingUpdates || scanning || running || querying || preparing)return;
            var dialog=new OpenFileDialog{Title=UiText.Text("Seleccioná <AppID>/achievements.ini o <AppID>/remote/achievements.ini"),Filter=UiText.Text("Estado CODEX/RUNE (achievements.ini)|achievements.ini|INI (*.ini)|*.ini")};
            var root=IniHistory.DefaultRoots().FirstOrDefault(Directory.Exists);if(root!=null)dialog.InitialDirectory=root;
            if(dialog.ShowDialog(this)!=true)return;
            uint appId=IniHistory.AppIdFromPath(dialog.FileName);
            if(appId==0){MessageBox.Show(this,UiText.Text("Elegí un archivo dentro de la carpeta numérica del AppID, o de su subcarpeta remote."),UiText.Text("CODEX/RUNE"),MessageBoxButton.OK,MessageBoxImage.Information);return;}
            string path=dialog.FileName,steam=SteamPath.Text.Trim();
            scanning=true;scanCancellation=new CancellationTokenSource();var token=scanCancellation.Token;UpdateButtons();ScanProgress.IsIndeterminate=true;
            SetStatus(UiText.Text("Importando CODEX/RUNE…"));
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
                ScanResult.Text=game.IsDemo?UiText.Text("Muestra CODEX/RUNE cargada. Las acciones de Steam están bloqueadas."):UiText.Text("INI importado. Solo se añadieron desbloqueos con fecha válida; revisá Logs para omisiones.");
                ApplyLibraryFilter();SetStatus(ScanResult.Text);Log(ScanResult.Text);PersistLibrary();GoTo("Plan");
            }
            catch(OperationCanceledException){SetStatus(UiText.Text("Importación INI cancelada."));}
            catch(Exception ex){Log(ex.ToString());MessageBox.Show(this,ex.Message,UiText.Text("CODEX/RUNE"),MessageBoxButton.OK,MessageBoxImage.Warning);}
            finally{scanning=false;scanCancellation.Dispose();scanCancellation=null;ScanProgress.IsIndeterminate=false;UpdateButtons();}
        }
        void SaveUpdateDrafts(bool forRestart=false)
        {
            if(currentGame?.HasLocalHistory==true)
            {
                if(!CommitPlanEdits() || !GapLimitValid())throw new InvalidDataException(forRestart?UiText.Text("Corregí el plan actual antes de reiniciar para conservar el borrador."):UiText.Text("Corregí el plan actual antes de actualizar para poder conservar el borrador."));
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
            UpdateButton.Content=UiText.Text("Buscando…");UpdateButtons();bool hadReady=File.Exists(UpdateReadyPath),installerStarted=false;
            try
            {
                var info=await UpdateChecker.CheckAsync(token);token.ThrowIfCancellationRequested();
                var current=typeof(MainWindow).Assembly.GetName().Version;
                if(!info.IsNewerThan(current))
                {MessageBox.Show(this,UiText.Text("Ya tenés una versión igual o más nueva que la publicada (")+info.Version+").",UiText.Text("ATR · Actualizaciones"),MessageBoxButton.OK,MessageBoxImage.Information);return;}
                string work=Path.Combine(LegacyTools.DataDirectory,"Updates",info.Version+"_"+Guid.NewGuid().ToString("N"));
                string package=Path.Combine(work,"update.zip");
                var progress=new Progress<int>(percent=>UpdateButton.Content=UiText.Text("Descargando ")+percent+UiText.Text(" %"));
                await AutomaticUpdate.DownloadAsync(info,package,progress,token);token.ThrowIfCancellationRequested();
                UpdateButton.Content=UiText.Text("Preparando instalación…");
                SaveUpdateDrafts();SavePaths();Directory.CreateDirectory(Path.GetDirectoryName(UpdateReadyPath));File.WriteAllText(UpdateReadyPath,"1");
                var start=await Task.Run(()=>AutomaticUpdate.PrepareInstaller(info,package,Path.GetDirectoryName(Environment.ProcessPath),work,token),token);token.ThrowIfCancellationRequested();
                using var installer=Process.Start(start)??throw new IOException(UiText.Text("No se pudo iniciar el instalador de actualización."));installerStarted=true;
                Close();
            }
            catch(OperationCanceledException){if(!token.IsCancellationRequested)MessageBox.Show(this,UiText.Text("Se agotó el tiempo de consulta o descarga. Reintentá más tarde."),UiText.Text("Actualizaciones"),MessageBoxButton.OK,MessageBoxImage.Information);}
            catch(Exception ex){Log(UiText.Text("Actualización no iniciada: ")+ex.Message);MessageBox.Show(this,UiText.Text("No se pudo iniciar la actualización. La instalación actual se conserva.\n")+ex.Message,UiText.Text("Actualizaciones"),MessageBoxButton.OK,MessageBoxImage.Information);}
            finally
            {
                if(!installerStarted && !hadReady)try{File.Delete(UpdateReadyPath);}catch{}
                checkingUpdates=false;updateCancellation.Dispose();updateCancellation=null;
                if(IsLoaded){UpdateButton.Content=UiText.Text("Actualizar ATR");UpdateButtons();}
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
                    if(!Directory.Exists(directory))throw new DirectoryNotFoundException(UiText.Text("El archivo y su carpeta ya no existen: ")+path);
                    start.ArgumentList.Add(directory);
                }
                Process.Start(start);e.Handled=true;
            }
            catch(Exception ex){MessageBox.Show(this,ex.Message,UiText.Text("Abrir ubicación"),MessageBoxButton.OK,MessageBoxImage.Information);}
        }
        void CancelScanClick(object sender,RoutedEventArgs e) {scanCancellation?.Cancel();CancelScanButton.IsEnabled=false;ScanResult.Text=UiText.Text("Cancelando escaneo…");}
        void LibraryFilterChanged(object sender,TextChangedEventArgs e)
        {
            ApplyLibraryFilter();
        }
        void GameChanged(object sender,SelectionChangedEventArgs e)
        {
            if(!initialized || rebuildingGroups || scanning || preparing)return;
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
                        Log(UiText.Text("Borrador recuperado al iniciar: ")+currentGame.GameName+UiText.Text(". El plan guardado explícitamente se conserva por separado."));
                    }
                    catch(Exception ex){Log(UiText.Text("No se pudo recuperar el borrador de actualización: ")+ex.Message);}
                }
                bool saved=browsedPlans.TryGetValue(currentGame,out var previous);
                if(saved)order.AddRange(previous.Records);
                else foreach(var a in currentGame.Achievements.OrderBy(x=>x.OriginalUnix>0?x.OriginalUnix:Int64.MaxValue))order.Add(a.Clone());
                HeaderGame.Text=currentGame.GameName;
                HistorySelectionNotice.Text=sourceNoticeKey==currentGame.StorageKey?sourceChangeNotice??"":"";HistorySelectionNotice.Visibility=String.IsNullOrEmpty(HistorySelectionNotice.Text)?Visibility.Collapsed:Visibility.Visible;
                PlanHistoryPath.Text=currentGame.StatsPath;
                int unknownDates=order.Count(a=>a.OriginalUnix==0);
                UnknownDatesNotice.Visibility=unknownDates>0?Visibility.Visible:Visibility.Collapsed;
                UnknownDatesNotice.Text=unknownDates+UiText.Text(" logros sin fecha. El plan sugiere 60 s por intervalo desconocido; podés modificarlo.");
                HeaderGameInfo.Text=(currentGame.IsDemo?UiText.Text("DEMO · "):"")+(currentGame.SteamTarget?UiText.Text("AppID ")+currentGame.AppId:currentGame.Platform+UiText.Text(" · ID ")+currentGame.ExternalGameId)+UiText.Text(" · ")+order.Count+UiText.Text(" logros · ")+(currentGame.HasSnapshot?UiText.Text("Snapshot protegido"):UiText.Text("Sin snapshot"))+(order.Any(a=>a.OriginalUnix==0)?UiText.Text(" · ")+order.Count(a=>a.OriginalUnix==0)+UiText.Text(" sin fecha"):"");
                if(!running)ExecutionGame.Text=currentGame.GameName;
                ApplyPlanParameters(saved?previous.Parameters:new PlanParameters{DurationSeconds=LegacyPlanner.OriginalSpan(order)});
                Log(UiText.Text("Cargado ")+currentGame.GameName+UiText.Text(" [")+currentGame.AppId+UiText.Text("]: ")+order.Count+UiText.Text(" logros."));
                RefreshPlan();
                if(!running && currentGame.SteamTarget && migrationRuns.Exists(currentGame))try{ShowMigrationReport(migrationRuns.Load(currentGame));}catch(Exception ex){Log(UiText.Text("Informe anterior no disponible: ")+ex.Message);}
                LoadOrigin();PopulateHistoryChoices();shownBaseline=PlanStore.Signature(PlanStore.Capture(currentGame,order,ReadPlanParameters()));

                if(saved && previous.Selected!=null)
                {
                    AchievementsGrid.SelectedItem=achievementRows.FirstOrDefault(a=>ReferenceEquals(a.Record,previous.Selected));
                    AchievementsGrid.ScrollIntoView(AchievementsGrid.SelectedItem);
                }
            }
            else
            {
                HistorySelectionNotice.Visibility=Visibility.Collapsed;UnknownDatesNotice.Visibility=Visibility.Collapsed;PlanHistoryPath.Text="";HeaderGame.Text=UiText.Text("Sin juego seleccionado");HeaderGameInfo.Text=UiText.Text("Elegí un historial");
                if(!running)ExecutionGame.Text=UiText.Text("No hay ningún plan preparado");
                SelectOrigin("Sin clasificar");OriginStatus.Text="";
            }
            UpdateButtons();
        }
        string ReadLibraryOrigin(GameEntry game)
        {
            try {return originStore.Load(game)??game.DetectedOrigin??"Sin clasificar";}
            catch(Exception ex){Log(UiText.Text("No se pudo leer el origen de ")+game.AppId+UiText.Text(": ")+ex.Message);return "Sin clasificar";}
        }
        void SelectOrigin(string label)
        {
            label=SourceSelection.LibraryCategory(label);
            var item=OriginChoice.Items.Cast<ComboBoxItem>().FirstOrDefault(i=>Convert.ToString(i.Tag)==label);
            if(item==null)item=OriginChoice.Items.Cast<ComboBoxItem>().First(i=>Convert.ToString(i.Tag)=="Otro");
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
                OriginStatus.Visibility=Visibility.Collapsed;
                OriginStatus.Text=origin==null?(currentGame.DetectedOrigin==null?UiText.Text("Sin origen asignado."):currentGame.OriginEvidence??"Origen detectado; podés corregirlo manualmente."):UiText.Text("Origen manual guardado para esta cuenta y juego.");
            }
            catch(Exception ex){SelectOrigin("Sin clasificar");OriginStatus.Visibility=Visibility.Visible;OriginStatus.Text=UiText.Text("No se pudo leer el origen: ")+ex.Message;}
        }
        void OriginChanged(object sender,SelectionChangedEventArgs e)
        {
            if(!initialized || selectingOrigin || !CanEditPlan)return;
            try
            {
                string label=Convert.ToString((OriginChoice.SelectedItem as ComboBoxItem)?.Tag)??"Sin clasificar";
                if(label=="Xbox Live")label="Xbox";
                if(label=="Playnite Achievements")label="PA";
                originStore.Save(currentGame,label=="Sin clasificar"?null:label);
                LoadOrigin();
                var row=games.FirstOrDefault(g=>ReferenceEquals(g.Game,currentGame));
                if(row!=null)row.Origin=ReadLibraryOrigin(currentGame);
                SetStatus(UiText.Text("Origen actualizado para ")+currentGame.GameName+UiText.Text(". No modifica archivos de logros ni Steam."));
                if(activePage=="Plan")planPinnedSourceKey=currentGame.StorageKey;ApplyLibraryFilter();
            }
            catch(Exception ex){LoadOrigin();MessageBox.Show(this,ex.Message,UiText.Text("No se pudo guardar el origen"),MessageBoxButton.OK,MessageBoxImage.Warning);}
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
            if(currentGame==null){PlanSaveStatus.Text=UiText.Text("Sin juego seleccionado.");return;}
            try
            {
                if(!planStore.Exists(currentGame)){PlanSaveStatus.Text=UiText.Text("Sin plan guardado en disco. Guardá para conservar los cambios.");return;}
                if(!savedSignatures.TryGetValue(currentGame,out var signature))
                    savedSignatures[currentGame]=signature=PlanStore.Signature(planStore.Load(currentGame));
                PlanSaveStatus.Text=PlanStore.Signature(PlanStore.Capture(currentGame,order,ReadPlanParameters()))==signature
                    ? UiText.Text("El borrador coincide con el plan guardado en disco.")
                    : UiText.Text("El borrador difiere del guardado. Guardá los cambios o usá Cargar guardado.");
            }
            catch(Exception ex){PlanSaveStatus.Text=UiText.Text("No se pudo comprobar el plan guardado: ")+ex.Message;}
        }
        bool CommitPlanEdits() => AchievementsGrid.CommitEdit(DataGridEditingUnit.Cell,true) && AchievementsGrid.CommitEdit(DataGridEditingUnit.Row,true);
        void SavePlanClick(object sender,RoutedEventArgs e)
        {
            if(!CanEditPlan || !GapLimitValid() || !CommitPlanEdits())return;
            try
            {
                var saved=PlanStore.Capture(currentGame,order,ReadPlanParameters());
                planStore.Save(currentGame,saved);savedSignatures[currentGame]=PlanStore.Signature(saved);
                PlanSaveStatus.Text=UiText.Text("Plan guardado en disco: ")+DateTime.Now.ToString("dd/MM HH:mm:ss");
                SetStatus(UiText.Text("Plan de ")+currentGame.GameName+UiText.Text(" guardado. No modifica Steam ni el historial original."));
            }
            catch(Exception ex){MessageBox.Show(this,ex.Message,UiText.Text("No se pudo guardar el plan"),MessageBoxButton.OK,MessageBoxImage.Warning);}
        }
        void LoadPlanClick(object sender,RoutedEventArgs e)
        {
            if(!CanEditPlan)return;
            if(MessageBox.Show(this,UiText.Text("Cargar el plan guardado reemplazará los cambios de este borrador. No modifica Steam. ¿Continuar?"),UiText.Text("Cargar plan"),MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
            try
            {
                var saved=planStore.Load(currentGame);
                var restored=PlanStore.Restore(currentGame,saved);
                savedSignatures[currentGame]=PlanStore.Signature(saved);
                order.Clear();order.AddRange(restored.Records);ApplyPlanParameters(saved.Parameters);RefreshPlan();
                PlanSaveStatus.Text=UiText.Text("Plan cargado. Consultá Steam antes de ejecutarlo.");
                if(restored.Added>0 || restored.Missing>0)
                    MessageBox.Show(this,UiText.Format($"El historial cambió: {restored.Missing} logros guardados ya no están y {restored.Added} son nuevos. Los nuevos se agregaron desmarcados. Revisá el plan."),UiText.Text("Plan actualizado"),MessageBoxButton.OK,MessageBoxImage.Information);
            }
            catch(Exception ex){MessageBox.Show(this,ex.Message,UiText.Text("No se pudo cargar el plan"),MessageBoxButton.OK,MessageBoxImage.Warning);}
        }
        void RestoreOriginalClick(object sender,RoutedEventArgs e)
        {
            if(!CanEditPlan)return;
            if(MessageBox.Show(this,UiText.Text("Restaurar el historial importado descarta los cambios de este borrador: selección, orden, tiempos y opciones. El plan guardado en disco y Steam no cambian. ¿Continuar?"),UiText.Text("Restaurar original"),MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
            order.Clear();
            foreach(var source in currentGame.Achievements.OrderBy(a=>a.OriginalUnix>0?a.OriginalUnix:Int64.MaxValue))
            {
                var a=source.Clone();a.Included=true;a.ManualGapSeconds=null;order.Add(a);
            }
            ApplyPlanParameters(new PlanParameters{DurationSeconds=LegacyPlanner.OriginalSpan(order)});
            RefreshPlan();PlanSaveStatus.Text=UiText.Text("Historial original restaurado en el borrador. El guardado en disco no cambió.");
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
            if(!initialized || updating || planDrag!=null)return;
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
            SelectedCount.Text=UiText.Text("Seleccionados: ")+index+UiText.Text(" / ")+order.Count;
            OriginalDuration.Text=(order.Any(a=>a.OriginalUnix==0)?UiText.Text("Base sugerida (fechas incompletas): "):UiText.Text("Original: "))+LegacyTools.FormatSpan(LegacyPlanner.OriginalSpan(order));
            double last=schedule.Count>0?schedule.Values.Max(x=>x.OffsetSeconds):0;
            EffectiveDuration.Text=UiText.Text("Efectiva: ")+LegacyTools.FormatSpan((long)Math.Round(last));
            GapLimitValidation.Text=GapLimitValid()?UiText.Text("Solo esperas automáticas; los tiempos manuales tienen prioridad."):UiText.Text("Ingresá un máximo mayor que 0 y hasta 525600 minutos.");
            if(!running)ExecutionSummary.Text=index+UiText.Text(" logros · ")+LegacyTools.FormatSpan((long)Math.Round(last))+UiText.Text(" · espera inicial ")+InitialSeconds()+UiText.Text("s");
            ReplayProgressText.Text=running ? ReplayProgressText.Text : UiText.Text("0 / ")+index+UiText.Text(" logros");
            updating=false;
            UpdatePlanSaveStatus();
            UpdateButtons();
        }
        void AchievementChanged(object sender,SelectionChangedEventArgs e)
        {
            var r=AchievementsGrid.SelectedItem as AchievementView;
            DetailTitle.Text=r?.DisplayName??UiText.Text("Seleccioná un logro");
            DetailDescription.Text=r?.Description??"";
            DetailTime.Text=r==null?"":UiText.Text("Fecha original: ")+r.OriginalDate;
            DetailSource.Text=UiText.Text("Fuente: ")+(r==null?UiText.Text("—"):currentGame.SourceLabel);
            DetailSteam.Text=UiText.Text("Steam: ")+(r?.SteamState??"—");
            DetailOrder.Text=UiText.Text("Orden: ")+(r?.Order??"—")+UiText.Text(" de ")+order.Count(a=>a.Included);
        }
        void AchievementCellEditEnding(object sender,DataGridCellEditEndingEventArgs e)
        {
            if(!initialized || updating || !CanEditPlan || e.EditAction!=DataGridEditAction.Commit)return;
            var row=e.Row.Item as AchievementView;
            var box=e.EditingElement as TextBox;
            if(row==null || box==null || !row.Record.Included)return;
            string column=e.Column.SortMemberPath;
            if(column!=nameof(AchievementView.NewGap) && column!=nameof(AchievementView.TargetOffset)) return;
            var a=row.Record;
            string value=box.Text;
            if(String.IsNullOrWhiteSpace(value) || value.Trim()=="—") a.ManualGapSeconds=null;
            else if(!LegacyTools.TryParseDuration(value,out double seconds))
            {
                MessageBox.Show(this,UiText.Text("Formato de tiempo no reconocido. Ejemplos: 00:30:00, 3d 22:24:20, 90m, 1h 15m."),UiText.Text("Tiempo manual"),MessageBoxButton.OK,MessageBoxImage.Warning);
            }
            else if(column==nameof(AchievementView.NewGap))
            {
                a.ManualGapSeconds=ReferenceEquals(a,order.FirstOrDefault(x=>x.Included))?null:Math.Max(0,seconds);
            }
            else
            {
                var previous=order.TakeWhile(x=>!ReferenceEquals(x,a)).LastOrDefault(x=>x.Included);
                var schedule=LegacyPlanner.Calculate(order,TargetDuration(),AutomaticGapLimit());
                double previousOffset=previous!=null && schedule.TryGetValue(previous,out var item)?item.OffsetSeconds:0;
                double manual=seconds-previousOffset;
                if(manual<0) MessageBox.Show(this,UiText.Text("Ese momento queda antes que el logro anterior."),UiText.Text("Tiempo manual"),MessageBoxButton.OK,MessageBoxImage.Warning);
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
            var chronological=order.OrderBy(a=>a.OriginalUnix>0?a.OriginalUnix:Int64.MaxValue).ToList();
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
            if(!CanEditPlan || querying || e.LeftButton!=MouseButtonState.Pressed || dragRow==null || planDrag!=null)return;
            Point position=e.GetPosition(AchievementsGrid);
            if(Math.Abs(position.X-dragStart.X)<SystemParameters.MinimumHorizontalDragDistance && Math.Abs(position.Y-dragStart.Y)<SystemParameters.MinimumVerticalDragDistance)return;
            var source=dragRow;dragRow=null;e.Handled=true;
            if(!AchievementsGrid.CommitEdit(DataGridEditingUnit.Row,true))return;
            var row=AchievementsGrid.ItemContainerGenerator.ContainerFromItem(source) as DataGridRow;
            dragLayer=AdornerLayer.GetAdornerLayer(AchievementsGrid);
            if(row==null || dragLayer==null)return;
            var session=new PlanReorderSession(order,source.Record,CustomOrder.IsChecked==true);
            var game=currentGame;
            dragGrabY=Math.Clamp(dragStart.Y-row.TranslatePoint(new Point(),AchievementsGrid).Y,0,row.ActualHeight);
            dragPreview=new PlanRowDragAdorner(AchievementsGrid,row);
            planDrag=session;planDragGame=game;dragPosition=position;dragInside=true;
            source.IsDragging=true;AchievementsGrid.SelectedItem=source;
            var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(80)};
            timer.Tick+=(s,args)=>{if(dragInside && planDrag!=null)PreviewPlanDrag(dragPosition);};
            bool accepted=false;
            try
            {
                dragLayer.Add(dragPreview);dragPreview.MoveTo(position.Y,dragGrabY);
                CustomOrder.IsChecked=true;timer.Start();
                accepted=DragDrop.DoDragDrop(AchievementsGrid,source.Record,DragDropEffects.Move)==DragDropEffects.Move;
            }
            finally
            {
                if(!accepted && ReferenceEquals(currentGame,game))
                {
                    session.Restore(order);
                    updating=true;try{CustomOrder.IsChecked=session.WasCustom;}finally{updating=false;}
                }
                timer.Stop();source.IsDragging=false;
                dragLayer.Remove(dragPreview);dragPreview=null;dragLayer=null;planDrag=null;planDragGame=null;dragInside=false;Mouse.UpdateCursor();
                if(ReferenceEquals(currentGame,game))
                {
                    RefreshPlan();
                    AchievementsGrid.SelectedItem=achievementRows.FirstOrDefault(a=>ReferenceEquals(a.Record,source.Record));
                }
            }
        }
        void AchievementDragFeedback(object sender,GiveFeedbackEventArgs e)
        {
            if(planDrag==null)return;
            e.UseDefaultCursors=false;Mouse.SetCursor(Cursors.SizeNS);e.Handled=true;
        }
        void AchievementDragContinue(object sender,QueryContinueDragEventArgs e)
        {
            if(planDrag!=null && (e.EscapePressed || !ReferenceEquals(currentGame,planDragGame) || activePage!="Plan" || !CanEditPlan || querying))
            {e.Action=DragAction.Cancel;e.Handled=true;}
        }
        bool OwnPlanDrag(DragEventArgs e)=>planDrag!=null && ReferenceEquals(currentGame,planDragGame) && activePage=="Plan" && CanEditPlan && !querying && e.Data.GetDataPresent(typeof(AchievementRecord)) && ReferenceEquals(e.Data.GetData(typeof(AchievementRecord)),planDrag.Moving);
        void AchievementDragOver(object sender,DragEventArgs e)
        {
            e.Handled=true;e.Effects=DragDropEffects.None;
            if(!OwnPlanDrag(e))return;
            dragPosition=e.GetPosition(AchievementsGrid);dragInside=true;
            dragPreview.Visibility=Visibility.Visible;
            e.Effects=DragDropEffects.Move;PreviewPlanDrag(dragPosition);
        }
        void AchievementDragLeave(object sender,DragEventArgs e)
        {
            if(planDrag==null)return;
            var point=e.GetPosition(AchievementsGrid);
            dragInside=point.X>=0 && point.X<AchievementsGrid.ActualWidth && point.Y>=0 && point.Y<AchievementsGrid.ActualHeight;
            dragPreview.Visibility=dragInside?Visibility.Visible:Visibility.Hidden;
        }
        void PreviewPlanDrag(Point position)
        {
            if(planDrag==null || !ReferenceEquals(currentGame,planDragGame) || activePage!="Plan" || !CanEditPlan || querying)return;
            dragPreview.MoveTo(position.Y,dragGrabY);
            var scroll=FindScrollViewer(AchievementsGrid);
            if(scroll!=null && Environment.TickCount64-lastDragScroll>=75)
            {
                if(position.Y<AchievementsGrid.ColumnHeaderHeight+24){scroll.LineUp();lastDragScroll=Environment.TickCount64;}
                else if(position.Y>AchievementsGrid.ActualHeight-30){scroll.LineDown();lastDragScroll=Environment.TickCount64;}
            }
            AchievementsGrid.UpdateLayout();
            double y=Math.Clamp(position.Y-dragGrabY+dragPreview.RowHeight/2,AchievementsGrid.ColumnHeaderHeight+1,Math.Max(AchievementsGrid.ColumnHeaderHeight+1,AchievementsGrid.ActualHeight-20));
            double x=Math.Clamp(position.X,2,Math.Max(2,AchievementsGrid.ActualWidth-20));
            var row=Ancestor<DataGridRow>(AchievementsGrid.InputHitTest(new Point(x,y)) as DependencyObject);
            var hovered=row?.Item as AchievementView;
            bool below=hovered!=null && y-row.TranslatePoint(new Point(),AchievementsGrid).Y>=row.ActualHeight/2;
            if(hovered==null && achievementRows.Count>0)
            {
                var last=AchievementsGrid.ItemContainerGenerator.ContainerFromIndex(achievementRows.Count-1) as DataGridRow;
                if(last!=null && y>=last.TranslatePoint(new Point(),AchievementsGrid).Y+last.ActualHeight)
                {hovered=last.Item as AchievementView;below=true;}
            }
            if(hovered==null)return;
            var moving=achievementRows.FirstOrDefault(a=>ReferenceEquals(a.Record,planDrag.Moving));
            if(moving==null || !planDrag.MoveOver(order,hovered.Record,below))return;
            int oldIndex=achievementRows.IndexOf(moving),newIndex=order.IndexOf(planDrag.Moving);
            achievementRows.Move(oldIndex,newIndex);
            int number=0;foreach(var view in achievementRows)view.Order=view.Included?(++number).ToString():"—";
            AchievementsGrid.SelectedItem=moving;AchievementChanged(AchievementsGrid,null);
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
            e.Handled=true;e.Effects=DragDropEffects.None;
            if(!OwnPlanDrag(e))return;
            var position=e.GetPosition(AchievementsGrid);
            if(position.Y<AchievementsGrid.ColumnHeaderHeight)return;
            PreviewPlanDrag(position);e.Effects=DragDropEffects.Move;
        }
        async void QuerySteamClick(object sender,RoutedEventArgs e)
        {
            if(!CanQueryPlan || currentGame.IsDemo || !currentGame.SteamTarget)return;
            var game=currentGame;
            string cli=SamExecutablePath;
            if(samMode!=SamCliMode.Modern || !File.Exists(cli))
            {
                MessageBox.Show(this,UiText.Text("Seleccioná y validá el CLI moderno de SamRewritten en Escaneo."),UiText.Text("Steam"),MessageBoxButton.OK,MessageBoxImage.Information);return;
            }
            querying=true;UpdateButtons();
            var replayer=activeReplayer;
            SetStatus(UiText.Text("Consultando Steam: ")+game.GameName+UiText.Text("…"));
            try
            {
                var r=await Task.Run(()=>replayer!=null ? replayer.QueryOtherGame(cli,game.AppId) : SamRunner.List(cli,SamCliMode.Modern,game.AppId));
                if(SamRunner.Failed(r))throw new Exception(r.StdErr+"\n"+r.StdOut);
                var states=SamRunner.States(r.StdOut);
                if(states.Count==0)throw new InvalidDataException(UiText.Text("SAM no devolvió estados de logros; se conserva el contador anterior."));
                var libraryRow=games.FirstOrDefault(g=>ReferenceEquals(g.Game,game));
                if(libraryRow!=null)libraryRow.Count=AchievementCounts.Format(game,states);
                // Si se navegó mientras llegaba la respuesta, actualizar el borrador correcto.
                var target=ReferenceEquals(currentGame,game)?order:browsedPlans.TryGetValue(game,out var cached)?cached.Records:null;
                int omitted=target==null?0:SamRunner.ApplySteamStates(target,states,true);
                if(ReferenceEquals(currentGame,game))RefreshPlan();
                SetStatus(game.GameName+UiText.Text(": ")+omitted+UiText.Text(" logros ya desbloqueados desmarcados."));
                Log(UiText.Text("Consulta SAM [")+game.AppId+UiText.Text("]: ")+omitted+UiText.Text(" logros seleccionados desmarcados."));
                PersistLibrary();
            }
            catch(Exception ex){Log(UiText.Text("Error consultando Steam: ")+ex.Message);MessageBox.Show(this,ex.Message,UiText.Text("SamRewritten"),MessageBoxButton.OK,MessageBoxImage.Warning);}
            finally {querying=false;UpdateButtons();}
        }
        void OpenSamClick(object sender,RoutedEventArgs e)
        {
            if(checkingUpdates || currentGame==null || currentGame.IsDemo || !currentGame.SteamTarget)return;
            string cli=SamExecutablePath,folder=Path.GetDirectoryName(cli),gui=null;
            try {if(Directory.Exists(folder)) gui=Directory.GetFiles(folder,"*.exe").FirstOrDefault(f=>Path.GetFileName(f).Contains("samrewritten",StringComparison.OrdinalIgnoreCase) && !Path.GetFileName(f).Contains("cli",StringComparison.OrdinalIgnoreCase));}catch{}
            if(gui==null) {MessageBox.Show(this,UiText.Text("No encontré la interfaz gráfica de SamRewritten junto al CLI. Abrí SAM y buscá el AppID ")+currentGame.AppId,UiText.Text("Abrir SAM"),MessageBoxButton.OK,MessageBoxImage.Information);return;}
            try {Process.Start(new ProcessStartInfo{FileName=gui,Arguments="--auto-open="+currentGame.AppId,UseShellExecute=true});}
            catch(Exception ex){MessageBox.Show(this,ex.Message,UiText.Text("SAM"),MessageBoxButton.OK,MessageBoxImage.Warning);}
        }
        void ShowMigrationReport(MigrationRun report)
        {
            shownRun=report;ReportTitle.Text=UiText.Text("Última migración: ")+report.Plan.GameName+UiText.Text(" · AppID ")+report.Plan.AppId;
            ReportSummary.Text=UiText.Text(report.Outcome)+UiText.Text(" · ")+report.Summary+(String.IsNullOrEmpty(report.Note)?"":"\n"+report.Note);
            MigrationResults.ItemsSource=null;MigrationResults.ItemsSource=report.Items;
            CopyReportButton.IsEnabled=true;
        }
        void CopyMigrationReport(object sender,RoutedEventArgs e)
        {
            if(shownRun!=null){Clipboard.SetText(shownRun.Report());SetStatus(UiText.Text("Informe copiado."));}
        }
        void ResumeMigrationClick(object sender,RoutedEventArgs e)
        {
            if(running || !CanEditPlan || !currentGame.SteamTarget || querying || !CommitPlanEdits())return;
            try
            {
                if(samMode!=SamCliMode.Modern)throw new InvalidOperationException(UiText.Text("Para reanudar con verificación usá el CLI moderno de SAM."));
                var report=migrationRuns.Load(currentGame);
                uint account=LegacyScanner.RecentAccount(SteamPath.Text.Trim());
                report.EnsureResumeAccount(account);
                if(MessageBox.Show(this,UiText.Text("Se cargará el plan de la última migración de este historial. Steam se consultará nuevamente y se omitirán los logros ya desbloqueados. El modo reparación se desactivará para no volver a bloquearlos. Los planes guardados explícitamente se conservan. ¿Cargar y revisar?"),UiText.Text("Reanudar migración"),MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
                var restored=PlanStore.Restore(currentGame,report.Plan);
                if(restored.Missing>0)throw new InvalidDataException(UiText.Text("Faltan ")+restored.Missing+UiText.Text(" logros del plan original en el historial actual. Actualizá la biblioteca antes de reanudar."));
                report.Plan.Parameters.Repair=false;order.Clear();order.AddRange(restored.Records);
                ApplyPlanParameters(report.Plan.Parameters);RefreshPlan();ShowMigrationReport(report);GoTo("Ejecucion");
                SetStatus(UiText.Text("Plan recuperado. Iniciar migración consultará Steam y mostrará los pendientes antes de ejecutar."));
            }
            catch(Exception ex){MessageBox.Show(this,ex.Message,UiText.Text("Reanudar migración"),MessageBoxButton.OK,MessageBoxImage.Information);}
        }
        async void StartReplayClick(object sender,RoutedEventArgs e)
        {
            if(checkingUpdates||running||scanning||querying||preparing||currentGame==null || currentGame.IsDemo || !currentGame.SteamTarget || !currentGame.HasLocalHistory || !GapLimitValid())return;
            RefreshLibraryAvailability();
            if(currentGame.RequiresAccountRefresh || !currentGame.HasSnapshot || !currentGame.HasLocalHistory)
            {MessageBox.Show(this,UiText.Text("Actualizá la biblioteca antes de ejecutar: falta un snapshot válido o cambió la cuenta Steam."),UiText.Text("Biblioteca"),MessageBoxButton.OK,MessageBoxImage.Information);UpdateButtons();return;}
            if(!CommitPlanEdits())return;
            var game=currentGame;preparing=true;UpdateButtons();
            try
            {
                if(!order.Any(a=>a.Included)){MessageBox.Show(this,UiText.Text("No hay logros seleccionados."),UiText.Text("Migración"),MessageBoxButton.OK,MessageBoxImage.Information);return;}
                if(!await ValidateSamAsync(false))throw new InvalidOperationException(UiText.Text("Seleccioná y validá el ejecutable CLI de SamRewritten."));
                if(Process.GetProcessesByName("steam").Length==0)throw new InvalidOperationException(UiText.Text("Steam no parece estar abierto. Abrilo e iniciá sesión."));
                string cli=SamExecutablePath;uint appId=game.AppId;var mode=samMode;
                var parameters=ReadPlanParameters();uint account=LegacyScanner.RecentAccount(SteamPath.Text.Trim());
                async Task<Dictionary<string,string>> ReadStates()
                {
                    var response=await Task.Run(()=>SamRunner.List(cli,mode,appId));
                    if(SamRunner.Failed(response))throw new InvalidOperationException(UiText.Text("SAM no pudo leer el destino: ")+response.StdErr+" "+response.StdOut);
                    return mode==SamCliMode.Modern?SamRunner.States(response.StdOut):null;
                }
                SetStatus(UiText.Text("Consultando Steam para preparar el resumen…"));
                var states=await ReadStates();MigrationPreview preview;
                while(true)
                {
                    if(!ReferenceEquals(currentGame,game))throw new InvalidOperationException(UiText.Text("Cambió el historial seleccionado. Revisá el plan de nuevo."));
                    preview=MigrationPreview.Build(order,parameters,states);
                    if(states!=null){SamRunner.ApplySteamStates(order,states,false);foreach(var view in achievementRows)view.RefreshSteamState();}
                    var review=new MigrationReviewWindow(game,preview,parameters,account){Owner=this};
                    if(review.ShowDialog()!=true){GoTo("Plan");return;}
                    if(preview.Missing>0)return;
                    if(preview.Schedule.Count==0)break;
                    if(mode!=SamCliMode.Modern)break;
                    var latest=await ReadStates();var rechecked=MigrationPreview.Build(order,parameters,latest);
                    if(preview.PendingIdentity==rechecked.PendingIdentity){states=latest;break;}
                    states=latest;SetStatus(UiText.Text("Cambió el estado de Steam mientras revisabas. Se actualizó el resumen."));
                }
                if(account!=0 && LegacyScanner.RecentAccount(SteamPath.Text.Trim())!=account)throw new InvalidOperationException(UiText.Text("Cambió la cuenta Steam durante la revisión. Volvé a preparar la migración."));
                var run=new MigrationRun{SourceKey=game.StorageKey,SourceLabel=HistoryGroup.Label(game),SourcePath=game.StatsPath,SteamAccount=account,Plan=PlanStore.Capture(game,order,parameters),Items=preview.Items,Outcome=preview.Schedule.Count==0?"Sin cambios":"En curso"};
                migrationRuns.Save(game,run);ShowMigrationReport(run);
                if(preview.Schedule.Count==0){Log(run.Report());GoTo("Ejecucion");SetStatus(UiText.Text("Todos los logros seleccionados ya están desbloqueados. Informe guardado sin realizar cambios."));return;}
                var schedule=preview.Schedule;var plan=schedule.Values.OrderBy(i=>i.OffsetSeconds).ToList();
                ExecutionGame.Text=game.GameName;ExecutionSummary.Text=plan.Count+UiText.Text(" por aplicar · ")+preview.Already+UiText.Text(" ya desbloqueados omitidos · AppID ")+appId;
                executingGame=game;activeSchedule=schedule;preparing=false;running=true;replayCancellation=new CancellationTokenSource();
                ReplayProgress.Maximum=plan.Count;ReplayProgress.Value=0;ReplayProgressText.Text=UiText.Text("0 / ")+plan.Count+UiText.Text(" logros");
                GoTo("Ejecucion");UpdateButtons();Log(UiText.Text("=== Inicio de migración: ")+game.GameName+UiText.Text(" · AppID ")+appId+UiText.Text(" ==="));
                var replayer=new LegacyReplayer(new ReplayEvents
                {
                    Log=Log,
                    Achievement=(achievement,state)=>{run.Action(achievement.ApiId,state);migrationRuns.Save(game,run);},
                    Status=status=>Dispatcher.BeginInvoke(new Action(()=>{ReplayStatus.Text=status;SetStatus(status);})),
                    Progress=(done,total,title)=>Dispatcher.BeginInvoke(new Action(()=>{ReplayProgress.Maximum=total;ReplayProgress.Value=done;ReplayProgressText.Text=done+UiText.Text(" / ")+total+UiText.Text(" logros · Último: ")+title;}))
                });
                try
                {
                    activeReplayer=replayer;
                    await replayer.Run(plan,cli,mode,appId,game.GameName,parameters.Repair,parameters.Idle,parameters.InitialDelaySeconds,replayCancellation.Token);
                    run.Outcome="Finalizada";
                }
                catch(OperationCanceledException){run.Outcome="Cancelada";Log(UiText.Text("=== Migración cancelada por el usuario ==="));}
                catch(Exception ex){run.Outcome="Detenida por error";run.Note=ex.Message;Log(UiText.Text("ERROR: ")+ex);}
                finally
                {
                    try
                    {
                        if(mode==SamCliMode.Modern)
                        {
                            SetStatus(UiText.Text("Verificando el resultado final en Steam…"));
                            var result=await Task.Run(()=>replayer.QueryOtherGame(cli,appId));
                            if(SamRunner.Failed(result))throw new InvalidOperationException(UiText.Text("SAM rechazó la consulta final."));
                            var finalStates=SamRunner.States(result.StdOut);if(finalStates.Count==0)throw new InvalidDataException(UiText.Text("SAM devolvió una consulta final vacía."));
                            run.Reconcile(finalStates);
                            var target=ReferenceEquals(currentGame,game)?order:browsedPlans.TryGetValue(game,out var draft)?draft.Records:null;
                            if(target!=null)SamRunner.ApplySteamStates(target,finalStates,false);
                            if(ReferenceEquals(currentGame,game))foreach(var view in achievementRows)view.RefreshSteamState();
                            var row=games.FirstOrDefault(r=>ReferenceEquals(r.Game,game));if(row!=null)row.Count=AchievementCounts.Format(game,finalStates);
                        }
                    }
                    catch(Exception ex){run.Note=(run.Note??"")+UiText.Text("\nNo se pudo verificar todo el resultado final: ")+ex.Message+UiText.Text(" Al reanudar se consultará Steam nuevamente.");}
                    if(run.Outcome=="Finalizada" && run.Items.Any(i=>i.Status=="Fallido"))run.Outcome="Finalizada con errores";
                    else if(run.Outcome=="Finalizada" && run.Items.Any(i=>i.Status=="Aplicado sin verificar" || i.Status=="En curso"))run.Outcome="Finalizada sin verificación completa";
                    foreach(var item in run.Items.Where(i=>i.Status=="En curso")){item.Status="Sin verificar";item.Detail=UiText.Text("La operación se interrumpió; consultar Steam antes de repetir.");}
                    try{migrationRuns.Save(game,run);}catch(Exception ex){run.Note+=UiText.Text("\nNo se pudo guardar el informe: ")+ex.Message;Log(run.Note);}
                    ShowMigrationReport(run);Log(run.Report());ReplayStatus.Text=run.Outcome;SetStatus(run.Outcome+UiText.Text(" · ")+run.Summary);
                    running=false;activeReplayer=null;executingGame=null;activeSchedule=null;replayCancellation.Dispose();replayCancellation=null;UpdateButtons();
                }
            }
            catch(Exception ex){Log(UiText.Text("No se inició la migración: ")+ex.Message);MessageBox.Show(this,ex.Message,UiText.Text("Migración"),MessageBoxButton.OK,MessageBoxImage.Warning);}
            finally{preparing=false;UpdateButtons();}
        }
        void CancelReplayClick(object sender,RoutedEventArgs e)
        {
            if(!running)return;
            replayCancellation?.Cancel();CancelReplayButton.IsEnabled=false;ReplayStatus.Text=UiText.Text("Cancelando…");
        }
        void CopyLogs(object sender,RoutedEventArgs e) {Clipboard.SetText(LogBox.Text);SetStatus(UiText.Text("Logs copiados al portapapeles."));}
        void UpdateButtons()
        {
            if(!initialized)return;
            bool available=currentGame!=null && currentGame.HasLocalHistory;
            bool live=available && currentGame.SteamTarget && currentGame.HasSnapshot && !currentGame.IsDemo && !currentGame.RequiresAccountRefresh;
            ScanButton.IsEnabled=!checkingUpdates && !scanning && !running && !querying && !preparing;
            CancelScanButton.IsEnabled=scanning;
            RefreshLibraryButton.IsEnabled=ScanButton.IsEnabled;
            RemoveLibraryButton.IsEnabled=ScanButton.IsEnabled && currentGame!=null;
            UpdateButton.IsEnabled=!checkingUpdates && !running && !scanning && !preparing && !querying;
            ConnectEpicButton.IsEnabled=ConnectXboxButton.IsEnabled=ImportPAButton.IsEnabled=ImportIniButton.IsEnabled=ImportGoldbergButton.IsEnabled=ScanButton.IsEnabled;
            foreach(var check in scanSourceChecks.Values)check.IsEnabled=ScanButton.IsEnabled;
            ClearLibraryButton.IsEnabled=ScanButton.IsEnabled && (games.Count>0 || File.Exists(libraryStore.FilePath) || File.Exists(libraryStore.BackupPath));
            DefinitionTools.Visibility=available && GoldbergHistory.NeedsDefinitions(currentGame)?Visibility.Visible:Visibility.Collapsed;
            FindDefinitionsButton.IsEnabled=CanEditPlan && !querying;
            StartButton.IsEnabled=!checkingUpdates && live && !running && !scanning && !querying && !preparing && order.Any(x=>x.Included) && GapLimitValid();
            CancelReplayButton.IsEnabled=running;
            ResumeMigrationButton.IsEnabled=!running && CanEditPlan && currentGame.SteamTarget && !querying && migrationRuns.Exists(currentGame);
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
            OriginChoice.IsEnabled=CanEditPlan;HistorySourceChoice.IsEnabled=currentGame!=null && !scanning && !preparing && !checkingUpdates && !querying && (!running || !ReferenceEquals(currentGame,executingGame));
            MapPlatformButton.IsEnabled=CanEditPlan && !currentGame.SteamTarget && !querying;MapPlatformButton.Visibility=available && !currentGame.SteamTarget?Visibility.Visible:Visibility.Collapsed;
            SavePlanButton.IsEnabled=CanEditPlan && GapLimitValid();
            LoadPlanButton.IsEnabled=CanEditPlan && planStore.Exists(currentGame);
            EditingStatus.Visibility=running?Visibility.Visible:Visibility.Collapsed;
            EditingStatus.Text=running?UiText.Text("En ejecución: ")+executingGame?.GameName+(ReferenceEquals(currentGame,executingGame)?UiText.Text(" · Este plan está bloqueado."):UiText.Text(" · Podés preparar este otro juego.")):UiText.Text("Editando borrador. Guardar plan conserva los cambios al cerrar.");
            NavScan.IsEnabled=NavLibrary.IsEnabled=NavPlan.IsEnabled=NavExecution.IsEnabled=NavLogs.IsEnabled=true;
        }
        void WindowClosing(object sender,CancelEventArgs e)
        {
            if(running || preparing || querying)
            {
                e.Cancel=true;
                MessageBox.Show(this,UiText.Text("Hay una operación en curso. Si es una migración, cancelala primero; si es una consulta, esperá a que termine."),UiText.Text("Migración activa"),MessageBoxButton.OK,MessageBoxImage.Information);
                GoTo("Ejecucion");return;
            }
            updateCancellation?.Cancel();scanCancellation?.Cancel();SavePaths();
        }
    }
}
