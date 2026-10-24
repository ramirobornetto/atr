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
        public string SourceCode=>Source.ApiId;
        public string TargetCode=>target?.ApiId??"";
        public string Description=>Source.Description;
        SteamDefinition target;bool confirmed;
        public bool ExactSuggestion;
        public bool NoEquivalent;
        public string MatchReason {get;set;}
        public SteamDefinition Target{get=>target;set{NoEquivalent=value!=null && String.IsNullOrEmpty(value.ApiId);target=String.IsNullOrEmpty(value?.ApiId)?null:value;confirmed=false;ExactSuggestion=false;MatchReason="Elección manual";Changed();}}
        public bool Confirmed{get=>confirmed;set{confirmed=value && target!=null;Changed();}}
        public string TargetDescription=>target?.Description??"";
        public string Status=>target==null?(NoEquivalent?"Sin equivalente":"Sin sugerencia"):confirmed?"Confirmado":"Por revisar";
        public event PropertyChangedEventHandler PropertyChanged;
        void Changed(){PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(nameof(Target)));PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(nameof(Confirmed)));PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(nameof(Status)));PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(nameof(TargetDescription)));PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(nameof(TargetCode)));PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(nameof(MatchReason)));}
    }
    internal sealed class PlatformMappingWindow : Window
    {
        readonly GameEntry source;readonly string directory;readonly Func<uint,Task<List<SteamDefinition>>> load;
        readonly Func<uint,string> targetName;readonly Func<string,Task<List<SteamGameMatch>>> search;
        readonly TextBox searchText=new TextBox{Width=270,MaxLength=200,Margin=new Thickness(8)};
        readonly ComboBox searchResults=new ComboBox{Width=430,Margin=new Thickness(8)};
        readonly Button searchButton=new Button{Content="Buscar juego en Steam"},confirmExact=new Button{Content="Confirmar coincidencias exactas"},confirmSelected=new Button{Content="Confirmar seleccionadas"};
        readonly ComboBox language=new ComboBox{Width=125,Margin=new Thickness(8)};
        readonly TextBox appId=new TextBox{Width=130,Margin=new Thickness(8)};
        readonly TextBlock info=new TextBlock{FontStyle=FontStyles.Italic,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(8)};
        readonly CheckBox edition=new CheckBox{Content="Confirmo que el AppID corresponde al juego y edición correctos en Steam",Margin=new Thickness(8)};
        readonly ObservableCollection<MappingRow> rows=new ObservableCollection<MappingRow>();
        readonly DataGridTemplateColumn targetColumn=new DataGridTemplateColumn{Header="Equivalente Steam",Width=new DataGridLength(3,DataGridLengthUnitType.Star),MinWidth=160};
        readonly DataGrid grid=new DataGrid{AutoGenerateColumns=false,CanUserAddRows=false,Margin=new Thickness(8),SelectionUnit=DataGridSelectionUnit.FullRow};
        readonly Button fetch=new Button{Content="Cargar logros Steam"},save=new Button{Content="Guardar correspondencias"},prepare=new Button{Content="Preparar plan Steam"};
        readonly Dictionary<uint,MappingFile> drafts=new Dictionary<uint,MappingFile>();
        readonly CheckBox confirmAll=new CheckBox{Content="Confirmar",IsThreeState=true,Margin=new Thickness(0),ToolTip="Marcar o desmarcar todas las equivalencias válidas. Sin equivalente o con destino repetido quedan pendientes."};
        bool bulkConfirm,allConfirmed;
        string loadedTargetName;
        List<SteamDefinition> definitions=new List<SteamDefinition>();uint loadedId;bool busy;
        public GameEntry Result;
        public PlatformMappingWindow(GameEntry source,string directory,Func<uint,Task<List<SteamDefinition>>> load,Func<uint,string> targetName,Func<string,Task<List<SteamGameMatch>>> search)
        {
            this.source=source;this.directory=directory;this.load=load;this.targetName=targetName;this.search=search;
            Title="Correspondencias · "+source.Platform+" → Steam · "+source.GameName;Width=1220;Height=780;MinWidth=850;MinHeight=560;WindowStartupLocation=WindowStartupLocation.CenterOwner;Background=(System.Windows.Media.Brush)Application.Current.FindResource("Bg");Foreground=System.Windows.Media.Brushes.White;
            // Appearance.xaml supplies the shared control sizes; local properties can override them.
            var layout=new DockPanel{Margin=new Thickness(12)};Content=layout;
            var header=new StackPanel();DockPanel.SetDock(header,Dock.Top);layout.Children.Add(header);
            header.Children.Add(new TextBlock{Text="Origen: "+source.GameName+" · "+source.Platform+" · "+source.Achievements.Count+" desbloqueados",FontSize=19,Margin=new Thickness(8),TextWrapping=TextWrapping.Wrap});
            header.Children.Add(new TextBlock{FontStyle=FontStyles.Italic,Text="Buscá el juego de Steam por nombre, elegí la edición y revisá las sugerencias. Las sugerencias por nombre o descripción no se confirman automáticamente. Solo pasarán al plan los logros confirmados; no se escribe en la plataforma de origen.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(8)});
            searchText.Text=source.GameName;var searchBar=new WrapPanel();searchBar.Children.Add(searchText);searchBar.Children.Add(searchButton);searchBar.Children.Add(searchResults);header.Children.Add(searchBar);
            language.Items.Add(new ComboBoxItem{Content="Español",Tag="spanish"});language.Items.Add(new ComboBoxItem{Content="English",Tag="english"});language.SelectedIndex=System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName=="es"?0:1;
            var input=new WrapPanel();input.Children.Add(new TextBlock{Text="AppID Steam:",VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(8)});input.Children.Add(appId);input.Children.Add(new TextBlock{Text="Idioma:",VerticalAlignment=VerticalAlignment.Center});input.Children.Add(language);input.Children.Add(fetch);header.Children.Add(input);header.Children.Add(info);header.Children.Add(edition);
            var footer=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right};DockPanel.SetDock(footer,Dock.Bottom);footer.Children.Add(confirmExact);footer.Children.Add(confirmSelected);footer.Children.Add(save);footer.Children.Add(prepare);var cancel=new Button{Content="Cerrar"};cancel.Click+=(s,e)=>Close();footer.Children.Add(cancel);layout.Children.Add(footer);layout.Children.Add(grid);
            DataGridTextColumn TextColumn(string header,string path,DataGridLength width)
            {
                var style=new Style(typeof(TextBlock),(Style)Application.Current.FindResource(typeof(TextBlock)));
                style.Setters.Add(new Setter(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis));
                style.Setters.Add(new Setter(FrameworkElement.ToolTipProperty,new Binding(path)));
                return new DataGridTextColumn{Header=header,Binding=new Binding(path),Width=width,IsReadOnly=true,ElementStyle=style};
            }
            grid.Columns.Add(TextColumn("Logro de origen",nameof(MappingRow.Name),new DataGridLength(2,DataGridLengthUnitType.Star)));
            grid.Columns.Add(TextColumn("Código de origen",nameof(MappingRow.SourceCode),150));
            grid.Columns.Add(TextColumn("Descripción de origen",nameof(MappingRow.Description),new DataGridLength(2,DataGridLengthUnitType.Star)));
            grid.Columns.Add(targetColumn);
            grid.Columns.Add(TextColumn("Código Steam",nameof(MappingRow.TargetCode),160));
            grid.Columns.Add(TextColumn("Descripción Steam",nameof(MappingRow.TargetDescription),new DataGridLength(2,DataGridLengthUnitType.Star)));
            var check=new FrameworkElementFactory(typeof(CheckBox));check.SetBinding(CheckBox.IsCheckedProperty,new Binding(nameof(MappingRow.Confirmed)){Mode=BindingMode.TwoWay,UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged});check.SetValue(CheckBox.HorizontalAlignmentProperty,HorizontalAlignment.Center);
            grid.Columns.Add(new DataGridTemplateColumn{Header=confirmAll,Width=118,CanUserSort=false,CellTemplate=new DataTemplate{VisualTree=check}});
            grid.Columns.Add(TextColumn("Estado",nameof(MappingRow.Status),115));
            grid.Columns.Add(TextColumn("Motivo de la sugerencia",nameof(MappingRow.MatchReason),200));grid.ItemsSource=rows;
            foreach(var column in grid.Columns.Where(c=>c.Width.IsStar))column.MinWidth=160;
            rows.CollectionChanged+=(s,e)=>UpdateConfirmAll();
            confirmAll.Click+=(s,e)=>
            {
                if(busy || loadedId==0)return;
                grid.CommitEdit(DataGridEditingUnit.Cell,true);grid.CommitEdit(DataGridEditingUnit.Row,true);
                bool mark=!allConfirmed;var eligible=PlatformMapping.UniqueTargets(rows.Select(r=>r.Target?.ApiId));bulkConfirm=true;
                try{foreach(var row in rows)row.Confirmed=mark && row.Target!=null && eligible.Contains(row.Target.ApiId);}
                finally{bulkConfirm=false;UpdateConfirmAll();}
                info.Text=mark?rows.Count(r=>r.Confirmed)+" correspondencias confirmadas. Las filas sin equivalente o con destinos repetidos siguen pendientes.":"Se desmarcaron todas las correspondencias.";
            };
            fetch.Click+=async(s,e)=>await Fetch();save.Click+=(s,e)=>Save(false);prepare.Click+=(s,e)=>Save(true);Closing+=(s,e)=>{if(busy)e.Cancel=true;};
            appId.TextChanged+=(s,e)=>{if(loadedId!=0){RememberDraft();loadedId=0;edition.IsChecked=false;save.IsEnabled=prepare.IsEnabled=confirmExact.IsEnabled=confirmSelected.IsEnabled=false;rows.Clear();info.Text="Cambió el AppID: cargá nuevamente los logros del destino.";}};
            searchButton.Click+=async(s,e)=>await Search();
            searchResults.SelectionChanged+=(s,e)=>{if(searchResults.SelectedItem is SteamGameMatch game)appId.Text=game.AppId.ToString();};
            confirmExact.Click+=(s,e)=>Confirm(rows.Where(r=>r.ExactSuggestion));confirmSelected.Click+=(s,e)=>Confirm(grid.SelectedItems.Cast<MappingRow>());
            language.SelectionChanged+=(s,e)=>{if(!busy && loadedId!=0){RememberDraft();loadedId=0;rows.Clear();edition.IsChecked=false;save.IsEnabled=prepare.IsEnabled=false;info.Text="Cambió el idioma: cargá los logros nuevamente.";}};
            Loaded+=async(s,e)=>await Search();
            save.IsEnabled=prepare.IsEnabled=confirmExact.IsEnabled=confirmSelected.IsEnabled=false;UpdateConfirmAll();
        }
        void UpdateConfirmAll()
        {
            if(bulkConfirm)return;
            if(busy || loadedId==0){confirmAll.IsChecked=false;confirmAll.IsEnabled=false;allConfirmed=false;return;}
            var eligible=PlatformMapping.UniqueTargets(rows.Select(r=>r.Target?.ApiId));
            var valid=rows.Where(r=>r.Target!=null && eligible.Contains(r.Target.ApiId)).ToList();
            allConfirmed=valid.Count>0 && valid.All(r=>r.Confirmed);
            confirmAll.IsChecked=allConfirmed?true:rows.Any(r=>r.Confirmed)?(bool?)null:false;
            confirmAll.IsEnabled=valid.Count>0 || rows.Any(r=>r.Confirmed);
        }
        void RememberDraft()
        {if(loadedId!=0 && rows.Count>0)drafts[loadedId]=PlatformMapping.Capture(source,loadedId,rows.Select(r=>new AchievementMapping{SourceId=r.Source.ApiId,TargetId=r.Target?.ApiId,Confirmed=r.Confirmed,NoEquivalent=r.NoEquivalent}));}
        async Task Search()
        {
            if(busy)return;RememberDraft();loadedId=0;rows.Clear();edition.IsChecked=false;save.IsEnabled=prepare.IsEnabled=confirmExact.IsEnabled=confirmSelected.IsEnabled=false;busy=true;searchButton.IsEnabled=fetch.IsEnabled=appId.IsEnabled=searchResults.IsEnabled=searchText.IsEnabled=language.IsEnabled=false;info.Text="Buscando coincidencias por nombre…";
            try
            {
                var matches=await search(searchText.Text.Trim());searchResults.ItemsSource=matches;
                var exact=matches.Where(m=>String.Equals(m.Name.Trim(),searchText.Text.Trim(),StringComparison.OrdinalIgnoreCase)).ToList();
                if(exact.Count==1)searchResults.SelectedItem=exact[0];
                info.Text=matches.Count==0?"Sin coincidencias disponibles. Podés cambiar el nombre o introducir un AppID manualmente.":matches.Count+" coincidencias. Elegí la edición correcta y cargá sus logros. La búsqueda no verifica propiedad.";
            }
            catch(Exception ex){info.Text="No se pudo buscar en Steam: "+ex.Message+". El AppID manual sigue disponible.";}
            finally{busy=false;UpdateConfirmAll();searchButton.IsEnabled=fetch.IsEnabled=appId.IsEnabled=searchResults.IsEnabled=searchText.IsEnabled=language.IsEnabled=true;}
        }
        void Confirm(IEnumerable<MappingRow> selected)
        {
            if(busy || loadedId==0)return;
            var ids=PlatformMapping.UniqueTargets(rows.Select(r=>r.Target?.ApiId));var unique=rows.Where(r=>r.Target!=null && ids.Contains(r.Target.ApiId)).ToHashSet();int count=0;
            foreach(var row in selected.ToList())if(unique.Contains(row)){row.Confirmed=true;count++;}
            info.Text=count+" correspondencias confirmadas. Los destinos repetidos o filas sin sugerencia no se confirmaron.";
        }
        async Task Fetch()
        {
            if(!UInt32.TryParse(appId.Text.Trim(),out uint id) || id==0){info.Text="Ingresá un AppID de Steam mayor que cero.";return;}
            RememberDraft();busy=true;searchButton.IsEnabled=searchText.IsEnabled=searchResults.IsEnabled=language.IsEnabled=confirmExact.IsEnabled=confirmSelected.IsEnabled=false;grid.IsEnabled=false;info.Text="Cargando esquema de Steam…";fetch.IsEnabled=save.IsEnabled=prepare.IsEnabled=false;appId.IsEnabled=false;edition.IsChecked=false;
            try
            {
                var result=await load(id);if(result.Count==0)throw new InvalidDataException("No se encontraron definiciones para ese AppID.");
                string localizationNote="";
                if(!source.Achievements.All(a=>AchievementMatching.Suggest(a,result).Exact))
                {
                    info.Text="Buscando textos localizados para comparar los logros…";
                    try{int count=await SteamDiscovery.LocalizeAsync(id,result,(string)((ComboBoxItem)language.SelectedItem).Tag,Path.Combine(LegacyTools.DataDirectory,"SteamAchievementTexts"));localizationNote=count>0?" · "+count+" textos localizados":"";}
                    catch(Exception ex){localizationNote=" · Textos públicos no disponibles; se conservan las sugerencias locales/es-en.";System.Diagnostics.Debug.WriteLine(ex.Message);}
                }
                definitions=result;loadedId=id;rows.Clear();var saved=drafts.TryGetValue(id,out var draft)?draft:PlatformMapping.Load(directory,source,id);
                foreach(var achievement in source.Achievements)
                {
                    var row=new MappingRow{Source=achievement};var old=saved?.Entries.FirstOrDefault(e=>String.Equals(e.SourceId,achievement.ApiId,StringComparison.OrdinalIgnoreCase));
                    var suggestion=AchievementMatching.Suggest(achievement,definitions);
                    var previous=old?.TargetId==null?null:definitions.FirstOrDefault(d=>String.Equals(d.ApiId,old.TargetId,StringComparison.OrdinalIgnoreCase));
                    row.Target=old?.NoEquivalent==true?null:previous??suggestion.Target;row.NoEquivalent=old?.NoEquivalent==true;
                    row.ExactSuggestion=suggestion.Exact && row.Target==suggestion.Target;row.MatchReason=old?.NoEquivalent==true?"Marcado sin equivalente":previous!=null?"Correspondencia guardada":suggestion.Reason;
                    row.Confirmed=old?.Confirmed==true && row.Target!=null && row.Target.ApiId==old.TargetId;row.PropertyChanged+=(s,e)=>{if(e.PropertyName==nameof(MappingRow.Target) || e.PropertyName==nameof(MappingRow.Confirmed))UpdateConfirmAll();};rows.Add(row);
                }
                var combo=new FrameworkElementFactory(typeof(ComboBox));combo.SetValue(ComboBox.ItemsSourceProperty,new[]{new SteamDefinition{Name="— Sin equivalente —"}}.Concat(definitions.OrderBy(d=>d.Name,StringComparer.CurrentCultureIgnoreCase).ThenBy(d=>d.ApiId,StringComparer.OrdinalIgnoreCase)).ToList());combo.SetValue(ComboBox.IsTextSearchEnabledProperty,true);combo.SetBinding(ComboBox.SelectedItemProperty,new Binding(nameof(MappingRow.Target)){Mode=BindingMode.TwoWay,UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged});
                combo.SetResourceReference(FrameworkElement.HeightProperty,"TableControlHeight");combo.SetValue(FrameworkElement.MinHeightProperty,0d);combo.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);combo.SetValue(Control.PaddingProperty,new Thickness(5,0,5,0));combo.SetValue(FrameworkElement.MarginProperty,new Thickness(0));
                targetColumn.CellTemplate=new DataTemplate{VisualTree=combo};
                string title=searchResults.SelectedItem is SteamGameMatch match && match.AppId==id?match.Name:targetName(id);loadedTargetName=title;
                info.Text="Destino: "+title+" · AppID "+id+" · "+definitions.Count+" logros · "+rows.Count(r=>r.Target!=null)+"/"+rows.Count+" sugerencias"+localizationNote+". Revisá edición, DLC y requisitos antes de confirmar.";save.IsEnabled=prepare.IsEnabled=confirmExact.IsEnabled=confirmSelected.IsEnabled=true;
            }
            catch(Exception ex){info.Text=ex.Message;definitions.Clear();loadedId=0;}
            finally{busy=false;UpdateConfirmAll();grid.IsEnabled=true;searchButton.IsEnabled=searchText.IsEnabled=searchResults.IsEnabled=language.IsEnabled=fetch.IsEnabled=appId.IsEnabled=true;}
        }
        void Save(bool build)
        {
            if(busy || loadedId==0)return;
            grid.CommitEdit(DataGridEditingUnit.Cell,true);grid.CommitEdit(DataGridEditingUnit.Row,true);
            try
            {
                if(build && edition.IsChecked!=true)throw new InvalidDataException("Confirmá el juego y la edición de Steam antes de preparar el plan.");
                var file=PlatformMapping.Capture(source,loadedId,rows.Select(r=>new AchievementMapping{SourceId=r.Source.ApiId,TargetId=r.Target?.ApiId,Confirmed=r.Confirmed,NoEquivalent=r.NoEquivalent}));
                if(build)Result=PlatformMapping.Build(source,file,definitions,loadedTargetName??targetName(loadedId));
                PlatformMapping.Save(directory,source,file);
                if(build){DialogResult=true;}else info.Text="Correspondencias guardadas. Las filas sin confirmar no se incluirán en el plan Steam.";
            }
            catch(Exception ex){MessageBox.Show(this,ex.Message,"Correspondencias",MessageBoxButton.OK,MessageBoxImage.Information);}
        }
    }
}
