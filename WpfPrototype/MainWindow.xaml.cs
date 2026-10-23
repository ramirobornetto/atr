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
    public sealed class GameRow
    {
        public string GameName { get; set; }
        public uint AppId { get; set; }
        public string Source { get; set; }
        public int Count { get; set; }
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
        GameEntry currentGame;
        SamCliMode samMode=SamCliMode.Unknown;
        CancellationTokenSource scanCancellation;
        CancellationTokenSource replayCancellation;
        bool scanning;
        bool running;
        bool initialized;
        bool updating;
        int targetSeconds;
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
            Log("WPF v0.6.0 iniciada. Motor derivado de v0.5.3. No se ha modificado Steam.");
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
                b.Foreground=new SolidColorBrush(selected ? Color.FromRgb(62,232,234) : Colors.White);
            }
        }
        void BrowseSteam(object sender,RoutedEventArgs e)
        {
            // WPF usa el diálogo nativo de Windows sin agregar paquetes externos.
            var dlg=new Microsoft.Win32.OpenFolderDialog {Title="Seleccioná la carpeta raíz de Steam",InitialDirectory=Directory.Exists(SteamPath.Text)?SteamPath.Text:Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)};
            if(dlg.ShowDialog(this)==true) {SteamPath.Text=dlg.FolderName;SavePaths();}
        }
        void BrowseSam(object sender,RoutedEventArgs e)
        {
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
            if(scanning || running) return;
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
            games.Clear();achievementRows.Clear();order.Clear();currentGame=null;
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
                foreach(var g in found) games.Add(new GameRow{GameName=g.GameName,AppId=g.AppId,Source="Steam local",Count=g.Achievements.Count,Snapshot=g.HasSnapshot?"Protegido ✓":"—",Game=g});
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
            if(!initialized || running)return;
            currentGame=(GamesGrid.SelectedItem as GameRow)?.Game;
            order.Clear();achievementRows.Clear();
            if(currentGame!=null)
            {
                foreach(var a in currentGame.Achievements.OrderBy(x=>x.OriginalUnix)) order.Add(a.Clone());
                HeaderGame.Text=currentGame.GameName;
                HeaderGameInfo.Text="AppID "+currentGame.AppId+" · "+order.Count+" logros · "+(currentGame.HasSnapshot?"Snapshot protegido":"Sin snapshot");
                ExecutionGame.Text=currentGame.GameName;
                CustomOrder.IsChecked=false;
                SetDuration(LegacyPlanner.OriginalSpan(order));
                Log("Cargado "+currentGame.GameName+" ["+currentGame.AppId+"]: "+order.Count+" logros.");
                RefreshPlan();
            }
            else
            {
                HeaderGame.Text="Sin juego seleccionado";HeaderGameInfo.Text="Elegí un historial";ExecutionGame.Text="No hay ningún plan preparado";
            }
            UpdateButtons();
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
        int InitialSeconds() => int.TryParse(InitialDelay.Text,out int delay)?Math.Clamp(delay,0,3600):10;
        void DurationChanged(object sender,TextChangedEventArgs e) {if(initialized && !updating && !running)RefreshPlan();}
        void IncludedChanged()
        {
            if(!initialized || updating || running)return;
            // En la versión 0.5.3 al cambiar selección se ajusta la duración automática al intervalo histórico seleccionado.
            SetDuration(LegacyPlanner.OriginalSpan(order));
            Dispatcher.BeginInvoke(new Action(RefreshPlan));
        }
        void RefreshPlan()
        {
            if(!initialized || updating)return;
            updating=true;
            var selectedRecord=(AchievementsGrid.SelectedItem as AchievementView)?.Record;
            var schedule=LegacyPlanner.Calculate(order,TargetDuration());
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
                    r.TargetTime=DateTime.Now.AddSeconds(InitialSeconds()+item.OffsetSeconds).ToString("dd/MM HH:mm:ss");
                }
                else {r.NewGap="—";r.TargetOffset="—";r.TargetTime="—";}
                achievementRows.Add(r);
                if(ReferenceEquals(a,selectedRecord)) AchievementsGrid.SelectedItem=r;
            }
            SelectedCount.Text="Seleccionados: "+index+" / "+order.Count;
            OriginalDuration.Text="Original: "+LegacyTools.FormatSpan(LegacyPlanner.OriginalSpan(order));
            double last=schedule.Count>0?schedule.Values.Max(x=>x.OffsetSeconds):0;
            EffectiveDuration.Text="Efectiva: "+LegacyTools.FormatSpan((long)Math.Round(last));
            ExecutionSummary.Text=index+" logros · "+LegacyTools.FormatSpan((long)Math.Round(last))+" · espera inicial "+InitialSeconds()+"s";
            ReplayProgressText.Text=running ? ReplayProgressText.Text : "0 / "+index+" logros";
            updating=false;
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
            if(!initialized || updating || running || e.EditAction!=DataGridEditAction.Commit)return;
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
                var schedule=LegacyPlanner.Calculate(order,TargetDuration());
                double previousOffset=previous!=null && schedule.TryGetValue(previous,out var item)?item.OffsetSeconds:0;
                double manual=seconds-previousOffset;
                if(manual<0) MessageBox.Show(this,"Ese momento queda antes que el logro anterior.","Tiempo manual",MessageBoxButton.OK,MessageBoxImage.Warning);
                else a.ManualGapSeconds=previous==null?null:manual;
            }
            Dispatcher.BeginInvoke(new Action(RefreshPlan),System.Windows.Threading.DispatcherPriority.Background);
        }
        void CustomOrderChanged(object sender,RoutedEventArgs e)
        {
            if(!initialized || updating || running)return;
            if(CustomOrder.IsChecked!=true) {RestoreChronological();RefreshPlan();}
            UpdateButtons();
        }
        void MoveUp(object sender,RoutedEventArgs e)=>MoveSelected(-1);
        void MoveDown(object sender,RoutedEventArgs e)=>MoveSelected(1);
        void MoveSelected(int dir)
        {
            if(CustomOrder.IsChecked!=true || running)return;
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
        void RestoreOrder(object sender,RoutedEventArgs e) {if(running)return;RestoreChronological();RefreshPlan();}
        void ClearTimings(object sender,RoutedEventArgs e) {if(running)return;foreach(var a in order)a.ManualGapSeconds=null;RefreshPlan();}
        static T Ancestor<T>(DependencyObject x) where T:DependencyObject
        {
            while(x!=null && x is not T) x=VisualTreeHelper.GetParent(x);
            return x as T;
        }
        void AchievementMouseDown(object sender,MouseButtonEventArgs e)
        {
            dragStart=e.GetPosition(AchievementsGrid);
            dragRow=Ancestor<DataGridRow>(e.OriginalSource as DependencyObject)?.Item as AchievementView;
        }
        void AchievementMouseMove(object sender,MouseEventArgs e)
        {
            if(running || CustomOrder.IsChecked!=true || e.LeftButton!=MouseButtonState.Pressed || dragRow==null)return;
            Point position=e.GetPosition(AchievementsGrid);
            if(Math.Abs(position.X-dragStart.X)<SystemParameters.MinimumHorizontalDragDistance && Math.Abs(position.Y-dragStart.Y)<SystemParameters.MinimumVerticalDragDistance)return;
            var source=dragRow;dragRow=null;
            DragDrop.DoDragDrop(AchievementsGrid,source,DragDropEffects.Move);
        }
        void AchievementDrop(object sender,DragEventArgs e)
        {
            if(running || CustomOrder.IsChecked!=true)return;
            var from=e.Data.GetData(typeof(AchievementView)) as AchievementView;
            var to=Ancestor<DataGridRow>(e.OriginalSource as DependencyObject)?.Item as AchievementView;
            if(from==null || to==null || ReferenceEquals(from,to))return;
            int index=order.IndexOf(from.Record), dest=order.IndexOf(to.Record);
            if(index<0||dest<0)return;
            order.RemoveAt(index);order.Insert(dest,from.Record);RefreshPlan();
        }
        async void QuerySteamClick(object sender,RoutedEventArgs e)
        {
            if(currentGame==null||running)return;
            if(!await ValidateSamAsync(false) || samMode!=SamCliMode.Modern)
            {
                MessageBox.Show(this,"Consultar logros requiere el CLI moderno de SamRewritten.","Steam",MessageBoxButton.OK,MessageBoxImage.Information);return;
            }
            string cli=SamPath.Text.Trim();uint appId=currentGame.AppId;
            QueryButton.IsEnabled=false;SetStatus("Consultando logros actuales de Steam…");
            try
            {
                var r=await Task.Run(()=>SamRunner.RunProcess(cli,"list-achievements "+appId,90000));
                if(SamRunner.Failed(r)) throw new Exception(r.StdErr+"\n"+r.StdOut);
                var states=SamRunner.States(r.StdOut);
                if(currentGame!=null && currentGame.AppId==appId)
                {
                    foreach(var a in order) a.SteamState=states.TryGetValue(a.ApiId,out var state)?state:"No encontrado";
                    RefreshPlan();
                }
                SetStatus("Estado de Steam actualizado.");Log("Consulta SAM completada ["+appId+"].");
            }catch(Exception ex){Log("Error consultando Steam: "+ex.Message);MessageBox.Show(this,ex.Message,"SamRewritten",MessageBoxButton.OK,MessageBoxImage.Warning);}
            finally {UpdateButtons();}
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
            if(running||scanning||currentGame==null || !currentGame.HasLocalHistory)return;
            var selected=order.Where(x=>x.Included).ToList();
            if(selected.Count==0) {MessageBox.Show(this,"No hay logros seleccionados.","Migración",MessageBoxButton.OK,MessageBoxImage.Warning);return;}
            if(!await ValidateSamAsync(false)) {MessageBox.Show(this,"Seleccioná y validá el ejecutable CLI de SamRewritten.","SAM",MessageBoxButton.OK,MessageBoxImage.Warning);return;}
            if(Process.GetProcessesByName("steam").Length==0) {MessageBox.Show(this,"Steam no parece estar abierto. Abrilo e iniciá sesión.","Steam",MessageBoxButton.OK,MessageBoxImage.Warning);return;}
            var schedule=LegacyPlanner.Calculate(order,TargetDuration());
            if(schedule.Count==0)return;
            var plan=schedule.Values.OrderBy(x=>x.OffsetSeconds).ToList();
            double last=schedule.Values.Max(x=>x.OffsetSeconds);
            string summary="Juego: "+currentGame.GameName+" ["+currentGame.AppId+"]\nLogros: "+selected.Count+"\nHistorial calendario original: "+LegacyTools.FormatSpan(LegacyPlanner.OriginalSpan(order))+"\nDuración automática elegida: "+LegacyTools.FormatSpan((long)TargetDuration())+"\nDuración efectiva: "+LegacyTools.FormatSpan((long)Math.Round(last))+"\nOrden: "+(CustomOrder.IsChecked==true?"personalizado":"original")+"\nBloquear antes: "+(LockFirst.IsChecked==true?"sí":"no")+"\nRegistrar tiempo de juego: "+(IdleDuringMigration.IsChecked==true?"sí":"no")+"\n\nEsto modificará los logros de Steam. ¿Continuar?";
            if(MessageBox.Show(this,summary,"Confirmar migración",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;
            string cli=SamPath.Text.Trim();uint appId=currentGame.AppId;SamCliMode mode=samMode;
            bool locked=LockFirst.IsChecked==true, idle=IdleDuringMigration.IsChecked==true;
            int delay=InitialSeconds();
            string name=currentGame.GameName;
            SetStatus("Verificando logros con SAM…");
            var verify=await Task.Run(()=>SamRunner.List(cli,mode,appId));
            if(currentGame==null || currentGame.AppId!=appId) {SetStatus("El juego seleccionado cambió durante la validación. Prepará el plan de nuevo.");return;}
            if(SamRunner.Failed(verify)) {MessageBox.Show(this,"SAM no pudo leer los logros:\n"+verify.StdErr+"\n"+verify.StdOut,"SAM",MessageBoxButton.OK,MessageBoxImage.Error);return;}
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
                await replayer.Run(plan,cli,mode,appId,name,locked,idle,delay,replayCancellation.Token);
                ReplayStatus.Text="Migración completada.";SetStatus("Migración completada.");
            }
            catch(OperationCanceledException){Log("=== Migración cancelada por el usuario ===");ReplayStatus.Text="Cancelado.";SetStatus("Migración cancelada.");}
            catch(Exception ex){Log("ERROR: "+ex);ReplayStatus.Text="ERROR: "+ex.Message;MessageBox.Show(this,ex.Message,"Error de migración",MessageBoxButton.OK,MessageBoxImage.Error);}
            finally {running=false;replayCancellation.Dispose();replayCancellation=null;UpdateButtons();}
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
            ScanButton.IsEnabled=!scanning && !running;
            CancelScanButton.IsEnabled=scanning;
            StartButton.IsEnabled=available && !running && !scanning && order.Any(x=>x.Included);
            CancelReplayButton.IsEnabled=running;
            QueryButton.IsEnabled=available && !running && samMode==SamCliMode.Modern;
            OpenSamButton.IsEnabled=available && !running && samMode==SamCliMode.Modern;
            UpButton.IsEnabled=DownButton.IsEnabled=available && !running && CustomOrder.IsChecked==true;
            AchievementsGrid.IsEnabled=available && !running;
            foreach(var control in new Control[]{DurationHours,DurationMinutes,DurationSeconds,InitialDelay,LockFirst,IdleDuringMigration,CustomOrder}) control.IsEnabled=available && !running;
            GamesGrid.IsEnabled=!running;
            SteamPath.IsEnabled=SamPath.IsEnabled=!running && !scanning;
            NavScan.IsEnabled=NavLibrary.IsEnabled=NavPlan.IsEnabled=NavExecution.IsEnabled=NavLogs.IsEnabled=true;
        }
        void WindowClosing(object sender,CancelEventArgs e)
        {
            if(running)
            {
                e.Cancel=true;
                MessageBox.Show(this,"Hay una migración en curso. Cancelala primero para detener SAM idle de forma segura.","Migración activa",MessageBoxButton.OK,MessageBoxImage.Information);
                GoTo("Ejecucion");return;
            }
            scanCancellation?.Cancel();SavePaths();
        }
    }
}
