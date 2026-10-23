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
        public string Snapshot { get; set; }
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
        bool querying;
        bool preparing;
        LegacyReplayer activeReplayer;
        readonly OriginStore originStore=new OriginStore(Path.Combine(LegacyTools.DataDirectory,"Origins"));
        readonly PlanStore planStore=new PlanStore(Path.Combine(LegacyTools.DataDirectory,"Plans"));
        bool CanEditPlan => currentGame!=null && !scanning && !preparing && (!running || !ReferenceEquals(currentGame,executingGame));
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
            AchievementsGrid.CanUserSortColumns=false;
            var settings=LegacyTools.ReadSettings();
            SteamPath.Text=settings.TryGetValue("steam",out var steam) && !String.IsNullOrWhiteSpace(steam) ? steam : LegacyTools.DetectSteam();
            SamPath.Text=settings.TryGetValue("sam",out var sam) && !String.IsNullOrWhiteSpace(sam) ? sam : LegacyTools.DetectSam();
            initialized=true;
            GoTo("Escaneo");
            UpdateButtons();
            Log("WPF v0.6.5 iniciada. Motor derivado de v0.5.3. No se ha modificado Steam.");
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
        async void ScanClick(object sender,RoutedEventArgs e)
        {
            if(scanning || running || querying || preparing) return;
            SavePaths();
            string steam=SteamPath.Text.Trim();
            string cli=SamPath.Text.Trim();
            if(!Directory.Exists(Path.Combine(steam,"appcache","stats")))
            {
                MessageBox.Show(this,"No encontré Steam\\appcache\\stats en la carpeta seleccionada.","Escaneo",MessageBoxButton.OK,MessageBoxImage.Warning);return;
            }
            scanning=true;
            scanCancellation=new CancellationTokenSource();
            var token=scanCancellation.Token;
            ScanProgress.IsIndeterminate=true;
            ScanResult.Text="Iniciando escaneo…";
            games.Clear();achievementRows.Clear();order.Clear();browsedPlans.Clear();currentGame=null;
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
                var found=await Task.Run(()=>scanner.Scan(steam,cli,samMode,progress,token),token);
                token.ThrowIfCancellationRequested();
                foreach(var g in found) games.Add(new GameRow{GameName=g.GameName,AppId=g.AppId,Source="UserGameStats (.bin)",Origin=ReadLibraryOrigin(g),Count=AchievementCounts.Format(g),Snapshot=g.HasSnapshot?"Protegido ✓":"—",Game=g});
                LibraryCount.Text=games.Count+" juegos";
                ScanResult.Text="Escaneo terminado: "+games.Count+" historiales aprovechables.";
                SetStatus(ScanResult.Text);
                Log(ScanResult.Text);
                ScanProgress.IsIndeterminate=false;ScanProgress.Maximum=1;ScanProgress.Value=1;
                GoTo("Biblioteca");
                if(games.Count>0) GamesGrid.SelectedIndex=0;
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
            finally {scanning=false;scanCancellation.Dispose();scanCancellation=null;ScanProgress.IsIndeterminate=false;UpdateButtons();}
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
            if(currentGame!=null)
                browsedPlans[currentGame]=new BrowsedPlan
                {
                    Records=order.ToList(),
                    Parameters=ReadPlanParameters(),Selected=(AchievementsGrid.SelectedItem as AchievementView)?.Record
                };
            currentGame=next;
            order.Clear();achievementRows.Clear();
            if(currentGame!=null)
            {
                bool saved=browsedPlans.TryGetValue(currentGame,out var previous);
                if(saved)order.AddRange(previous.Records);
                else foreach(var a in currentGame.Achievements.OrderBy(x=>x.OriginalUnix))order.Add(a.Clone());
                HeaderGame.Text=currentGame.GameName;
                HeaderGameInfo.Text="AppID "+currentGame.AppId+" · "+order.Count+" logros · "+(currentGame.HasSnapshot?"Snapshot protegido":"Sin snapshot");
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
                HeaderGame.Text="Sin juego seleccionado";HeaderGameInfo.Text="Elegí un historial";
                if(!running)ExecutionGame.Text="No hay ningún plan preparado";
                SelectOrigin("Sin clasificar");OriginStatus.Text="";
            }
            UpdateButtons();
        }
        string ReadLibraryOrigin(GameEntry game)
        {
            try {return originStore.Load(game)??"Sin clasificar";}
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
                SelectOrigin(origin??"Sin clasificar");
                OriginStatus.Text=origin==null?"Sin origen asignado.":"Origen manual guardado para esta cuenta y juego.";
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
            DetailSource.Text="Fuente: "+(r==null?"—":"Steam local");
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
            if(!CanQueryPlan)return;
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
            }
            catch(Exception ex){Log("Error consultando Steam: "+ex.Message);MessageBox.Show(this,ex.Message,"SamRewritten",MessageBoxButton.OK,MessageBoxImage.Warning);}
            finally {querying=false;UpdateButtons();}
        }
        void OpenSamClick(object sender,RoutedEventArgs e)
        {
            if(currentGame==null)return;
            string cli=SamPath.Text.Trim(),folder=Path.GetDirectoryName(cli),gui=null;
            try {if(Directory.Exists(folder)) gui=Directory.GetFiles(folder,"*.exe").FirstOrDefault(f=>Path.GetFileName(f).Contains("samrewritten",StringComparison.OrdinalIgnoreCase) && !Path.GetFileName(f).Contains("cli",StringComparison.OrdinalIgnoreCase));}catch{}
            if(gui==null) {MessageBox.Show(this,"No encontré la interfaz gráfica de SamRewritten junto al CLI. Abrí SAM y buscá el AppID "+currentGame.AppId,"Abrir SAM",MessageBoxButton.OK,MessageBoxImage.Information);return;}
            try {Process.Start(new ProcessStartInfo{FileName=gui,Arguments="--auto-open="+currentGame.AppId,UseShellExecute=true});}
            catch(Exception ex){MessageBox.Show(this,ex.Message,"SAM",MessageBoxButton.OK,MessageBoxImage.Warning);}
        }
        async void StartReplayClick(object sender,RoutedEventArgs e)
        {
            if(running||scanning||querying||preparing||currentGame==null || !currentGame.HasLocalHistory || !GapLimitValid())return;
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
                if(mode==SamCliMode.Modern && !locked)
                {
                    try
                    {
                        var latest=SamRunner.States(verify.StdOut);
                        if(latest.Count==0)throw new InvalidDataException("SAM no devolvió estados utilizables.");
                        int omitted=SamRunner.ApplySteamStates(order,latest,true);
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
                        MessageBox.Show(this,"No se pudo verificar qué logros ya están desbloqueados. No se inició la migración.\n"+ex.Message,"Steam",MessageBoxButton.OK,MessageBoxImage.Warning);
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
            ScanButton.IsEnabled=!scanning && !running && !querying && !preparing;
            CancelScanButton.IsEnabled=scanning;
            StartButton.IsEnabled=available && !running && !scanning && !querying && !preparing && order.Any(x=>x.Included) && GapLimitValid();
            CancelReplayButton.IsEnabled=running;
            QueryButton.IsEnabled=CanQueryPlan && samMode==SamCliMode.Modern;
            OpenSamButton.IsEnabled=available && !running && !preparing && !querying && samMode==SamCliMode.Modern;
            UpButton.IsEnabled=DownButton.IsEnabled=CanEditPlan && CustomOrder.IsChecked==true;
            AchievementsGrid.IsEnabled=available;
            AchievementsGrid.IsReadOnly=!CanEditPlan;
            foreach(var control in new Control[]{DurationHours,DurationMinutes,DurationSeconds,InitialDelay,LockFirst,IdleDuringMigration,CustomOrder,LimitLongGaps}) control.IsEnabled=CanEditPlan;
            MaxGapMinutes.IsEnabled=CanEditPlan && LimitLongGaps.IsChecked==true;
            GamesGrid.IsEnabled=!scanning && !preparing;
            SteamPath.IsEnabled=SamPath.IsEnabled=ValidateSamButton.IsEnabled=BrowseSamButton.IsEnabled=BrowseSteamButton.IsEnabled=!running && !scanning && !querying && !preparing;
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
            scanCancellation?.Cancel();SavePaths();
        }
    }
}
