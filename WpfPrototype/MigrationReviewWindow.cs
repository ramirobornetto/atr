using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using AchievementTimelineReplay;

namespace AchievementTimelineReplay.Wpf
{
    internal sealed class MigrationReviewWindow : Window
    {
        public MigrationReviewWindow(GameEntry game,MigrationPreview preview,PlanParameters parameters,uint account)
        {
            Title="Revisar migración · "+game.GameName;Width=1000;Height=670;MinWidth=760;MinHeight=530;WindowStartupLocation=WindowStartupLocation.CenterOwner;
            Background=(System.Windows.Media.Brush)Application.Current.FindResource("Bg");Foreground=System.Windows.Media.Brushes.White;
            var layout=new DockPanel{Margin=new Thickness(20)};Content=layout;
            var header=new StackPanel{Margin=new Thickness(0,0,0,12)};DockPanel.SetDock(header,Dock.Top);layout.Children.Add(header);
            header.Children.Add(new TextBlock{Text=game.GameName+" · Steam AppID "+game.AppId,FontSize=22,FontWeight=FontWeights.Bold,TextWrapping=TextWrapping.Wrap});
            header.Children.Add(new TextBlock{Text="Historial: "+HistoryGroup.Label(game)+"\nArchivo: "+game.StatsPath+"\nCuenta Steam detectada: "+(account==0?"no identificada":account.ToString()),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,10)});
            double duration=preview.Schedule.Count==0?0:System.Linq.Enumerable.Max(preview.Schedule.Values,s=>s.OffsetSeconds);
            header.Children.Add(new TextBlock{Text="Seleccionados: "+preview.Selected+" · Ya desbloqueados (se omiten): "+preview.Already+" · Por aplicar: "+preview.Schedule.Count+" · No encontrados: "+preview.Missing+"\nDuración restante del plan: "+LegacyTools.FormatSpan((long)Math.Round(duration))+" + "+parameters.InitialDelaySeconds+" s de espera inicial.",TextWrapping=TextWrapping.Wrap,FontWeight=FontWeights.SemiBold});
            header.Children.Add(new TextBlock{FontStyle=FontStyles.Italic,Text="Reparación (bloquear antes): "+(parameters.Repair?"SÍ: los logros del plan se bloquearán y desbloquearán nuevamente.":"no")+"\nRegistrar tiempo de juego: "+(parameters.Idle?"sí":"no")+(!preview.Verified?"\nCLI legacy: no se pudo verificar qué logros ya están desbloqueados. Para omitirlos y reanudar con verificación, usá el CLI moderno.":""),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,0)});
            var footer=new StackPanel{Margin=new Thickness(0,12,0,0)};DockPanel.SetDock(footer,Dock.Bottom);layout.Children.Add(footer);
            footer.Children.Add(new TextBlock{FontStyle=FontStyles.Italic,Text=preview.Missing>0?"Hay códigos ausentes en Steam. Volvé al plan y revisá el juego, la edición y las correspondencias.":preview.Schedule.Count==0?"Todos los seleccionados ya están desbloqueados. No se realizarán cambios.":"Se modificarán los logros de la cuenta Steam iniciada. Las fechas históricas no se trasladan: los nuevos desbloqueos tendrán la fecha actual.",TextWrapping=TextWrapping.Wrap});
            var buttons=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right};footer.Children.Add(buttons);
            var back=new Button{Content="Volver al plan"};back.Click+=(s,e)=>{DialogResult=false;};buttons.Children.Add(back);
            var confirm=new Button{Content=preview.Schedule.Count==0?"Cerrar resumen":"Iniciar migración",IsEnabled=preview.Missing==0};confirm.Click+=(s,e)=>{DialogResult=true;};buttons.Children.Add(confirm);
            var grid=new DataGrid{AutoGenerateColumns=false,IsReadOnly=true,CanUserAddRows=false,ItemsSource=preview.Items};layout.Children.Add(grid);
            grid.Columns.Add(new DataGridTextColumn{Header="Logro",Binding=new Binding(nameof(MigrationItem.Name)),Width=new DataGridLength(2,DataGridLengthUnitType.Star)});
            grid.Columns.Add(new DataGridTextColumn{Header="Código Steam",Binding=new Binding(nameof(MigrationItem.ApiId)),Width=170});
            grid.Columns.Add(new DataGridTextColumn{Header="Acción",Binding=new Binding(nameof(MigrationItem.Status)),Width=150});
            grid.Columns.Add(new DataGridTextColumn{Header="Detalle",Binding=new Binding(nameof(MigrationItem.Detail)),Width=new DataGridLength(2,DataGridLengthUnitType.Star)});
        }
    }
}
