using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace AchievementTimelineReplay.Wpf
{
    internal sealed class MappingRow : INotifyPropertyChanged
    {
        public AchievementRecord Source;
        public string Name=>Source.DisplayName;
        public string Description=>Source.Description;
        public string Date=>Source.OriginalUnix>0?LegacyTools.UnixToLocal(Source.OriginalUnix).ToString("dd/MM/yyyy HH:mm:ss"):"Sin fecha";
        SteamDefinition target;bool confirmed;
        public SteamDefinition Target{get=>target;set{target=String.IsNullOrEmpty(value?.ApiId)?null:value;confirmed=false;Changed();}}
        public bool Confirmed{get=>confirmed;set{confirmed=value && target!=null;Changed();}}
        public string TargetDescription=>target?.Description??"";
        public string Status=>target==null?"Sin equivalente":confirmed?"Confirmado":"Por revisar";
        public event PropertyChangedEventHandler PropertyChanged;
        void Changed(){PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(nameof(Target)));PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(nameof(Confirmed)));PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(nameof(Status)));PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(nameof(TargetDescription)));}
    }
    internal sealed class PlatformMappingWindow : Window
    {
        readonly GameEntry source;readonly string directory;readonly Func<uint,Task<List<SteamDefinition>>> load;
        readonly Func<uint,string> targetName;
        readonly TextBox appId=new TextBox{Width=130,Margin=new Thickness(8)};
        readonly TextBlock info=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(8)};
        readonly CheckBox edition=new CheckBox{Content="Confirmo que el AppID corresponde al juego y edición correctos en Steam",Margin=new Thickness(8)};
        readonly ObservableCollection<MappingRow> rows=new ObservableCollection<MappingRow>();
        readonly DataGrid grid=new DataGrid{AutoGenerateColumns=false,CanUserAddRows=false,Margin=new Thickness(8),SelectionUnit=DataGridSelectionUnit.FullRow};
        readonly Button fetch=new Button{Content="Cargar logros Steam"},save=new Button{Content="Guardar correspondencias"},prepare=new Button{Content="Preparar plan Steam"};
        List<SteamDefinition> definitions=new List<SteamDefinition>();uint loadedId;bool busy;
        public GameEntry Result;
        public PlatformMappingWindow(GameEntry source,string directory,Func<uint,Task<List<SteamDefinition>>> load,Func<uint,string> targetName)
        {
            this.source=source;this.directory=directory;this.load=load;this.targetName=targetName;
            Title="Correspondencias · "+source.Platform+" → Steam · "+source.GameName;Width=1220;Height=780;MinWidth=850;MinHeight=560;WindowStartupLocation=WindowStartupLocation.CenterOwner;Background=(System.Windows.Media.Brush)Application.Current.FindResource("Bg");Foreground=System.Windows.Media.Brushes.White;
            var layout=new DockPanel{Margin=new Thickness(12)};Content=layout;
            var header=new StackPanel();DockPanel.SetDock(header,Dock.Top);layout.Children.Add(header);
            header.Children.Add(new TextBlock{Text="Origen: "+source.GameName+" · "+source.Platform+" · "+source.Achievements.Count+" desbloqueados",FontSize=19,Margin=new Thickness(8),TextWrapping=TextWrapping.Wrap});
            header.Children.Add(new TextBlock{Text="Elegí el AppID de Steam y revisá cada equivalencia. Las sugerencias por nombre o descripción no se confirman automáticamente. Solo pasarán al plan los logros confirmados; no se escribe en la plataforma de origen.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(8)});
            var input=new WrapPanel();input.Children.Add(new TextBlock{Text="AppID Steam:",VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(8)});input.Children.Add(appId);input.Children.Add(fetch);header.Children.Add(input);header.Children.Add(info);header.Children.Add(edition);
            var footer=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right};DockPanel.SetDock(footer,Dock.Bottom);footer.Children.Add(save);footer.Children.Add(prepare);var cancel=new Button{Content="Cerrar"};cancel.Click+=(s,e)=>Close();footer.Children.Add(cancel);layout.Children.Add(footer);layout.Children.Add(grid);
            grid.Columns.Add(new DataGridTextColumn{Header="Logro de origen",Binding=new Binding(nameof(MappingRow.Name)),Width=new DataGridLength(2,DataGridLengthUnitType.Star),IsReadOnly=true});
            grid.Columns.Add(new DataGridTextColumn{Header="Descripción de origen",Binding=new Binding(nameof(MappingRow.Description)),Width=new DataGridLength(2,DataGridLengthUnitType.Star),IsReadOnly=true});
            grid.Columns.Add(new DataGridTextColumn{Header="Fecha",Binding=new Binding(nameof(MappingRow.Date)),Width=145,IsReadOnly=true});
            grid.Columns.Add(new DataGridTemplateColumn{Header="Equivalente Steam",Width=new DataGridLength(3,DataGridLengthUnitType.Star)});
            grid.Columns.Add(new DataGridTemplateColumn{Header="Confirmar",Width=85});
            var check=new FrameworkElementFactory(typeof(CheckBox));check.SetBinding(CheckBox.IsCheckedProperty,new Binding(nameof(MappingRow.Confirmed)){Mode=BindingMode.TwoWay,UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged});check.SetValue(CheckBox.HorizontalAlignmentProperty,HorizontalAlignment.Center);((DataGridTemplateColumn)grid.Columns[4]).CellTemplate=new DataTemplate{VisualTree=check};
            grid.Columns.Add(new DataGridTextColumn{Header="Estado",Binding=new Binding(nameof(MappingRow.Status)),Width=115,IsReadOnly=true});grid.Columns.Insert(4,new DataGridTextColumn{Header="Descripción Steam",Binding=new Binding(nameof(MappingRow.TargetDescription)),Width=new DataGridLength(2,DataGridLengthUnitType.Star),IsReadOnly=true});grid.ItemsSource=rows;
            fetch.Click+=async(s,e)=>await Fetch();save.Click+=(s,e)=>Save(false);prepare.Click+=(s,e)=>Save(true);Closing+=(s,e)=>{if(busy)e.Cancel=true;};
            appId.TextChanged+=(s,e)=>{if(!busy && loadedId!=0){loadedId=0;edition.IsChecked=false;save.IsEnabled=prepare.IsEnabled=false;rows.Clear();info.Text="Cambió el AppID: cargá nuevamente los logros del destino.";}};
            save.IsEnabled=prepare.IsEnabled=false;
        }
        async Task Fetch()
        {
            if(!UInt32.TryParse(appId.Text.Trim(),out uint id) || id==0){info.Text="Ingresá un AppID de Steam mayor que cero.";return;}
            busy=true;grid.IsEnabled=false;info.Text="Cargando esquema de Steam…";fetch.IsEnabled=save.IsEnabled=prepare.IsEnabled=false;appId.IsEnabled=false;edition.IsChecked=false;
            try
            {
                var result=await load(id);if(result.Count==0)throw new InvalidDataException("No se encontraron definiciones para ese AppID.");
                definitions=result;loadedId=id;rows.Clear();var saved=PlatformMapping.Load(directory,source,id);
                foreach(var achievement in source.Achievements)
                {
                    var row=new MappingRow{Source=achievement};var old=saved?.Entries.FirstOrDefault(e=>String.Equals(e.SourceId,achievement.ApiId,StringComparison.OrdinalIgnoreCase));
                    row.Target=old==null?PlatformMapping.Suggest(achievement,definitions):definitions.FirstOrDefault(d=>String.Equals(d.ApiId,old.TargetId,StringComparison.OrdinalIgnoreCase));
                    row.Confirmed=old?.Confirmed==true && row.Target!=null;rows.Add(row);
                }
                var combo=new FrameworkElementFactory(typeof(ComboBox));combo.SetValue(ComboBox.ItemsSourceProperty,new[]{new SteamDefinition{Name="— Sin equivalente —"}}.Concat(definitions).ToList());combo.SetValue(ComboBox.IsTextSearchEnabledProperty,true);combo.SetBinding(ComboBox.SelectedItemProperty,new Binding(nameof(MappingRow.Target)){Mode=BindingMode.TwoWay,UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged});combo.SetValue(ComboBox.ForegroundProperty,System.Windows.Media.Brushes.Black);combo.SetValue(ComboBox.BackgroundProperty,System.Windows.Media.Brushes.White);
                ((DataGridTemplateColumn)grid.Columns[3]).CellTemplate=new DataTemplate{VisualTree=combo};
                info.Text="Destino: "+targetName(id)+" · AppID "+id+" · "+definitions.Count+" logros. Revisá edición, DLC y requisitos; podés dejar filas sin confirmar.";save.IsEnabled=prepare.IsEnabled=true;
            }
            catch(Exception ex){info.Text=ex.Message;definitions.Clear();loadedId=0;}
            finally{busy=false;grid.IsEnabled=true;fetch.IsEnabled=appId.IsEnabled=true;}
        }
        void Save(bool build)
        {
            if(busy || loadedId==0)return;
            grid.CommitEdit(DataGridEditingUnit.Cell,true);grid.CommitEdit(DataGridEditingUnit.Row,true);
            try
            {
                if(build && edition.IsChecked!=true)throw new InvalidDataException("Confirmá el juego y la edición de Steam antes de preparar el plan.");
                var file=PlatformMapping.Capture(source,loadedId,rows.Select(r=>new AchievementMapping{SourceId=r.Source.ApiId,TargetId=r.Target?.ApiId,Confirmed=r.Confirmed}));
                if(build)Result=PlatformMapping.Build(source,file,definitions,targetName(loadedId));
                PlatformMapping.Save(directory,source,file);
                if(build){DialogResult=true;}else info.Text="Correspondencias guardadas. Las filas sin confirmar no se incluirán en el plan Steam.";
            }
            catch(Exception ex){MessageBox.Show(this,ex.Message,"Correspondencias",MessageBoxButton.OK,MessageBoxImage.Information);}
        }
    }
}
