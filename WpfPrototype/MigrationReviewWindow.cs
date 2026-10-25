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
            Title=UiText.Text("Revisar migración · ")+game.GameName;Width=1000;Height=670;MinWidth=760;MinHeight=530;WindowStartupLocation=WindowStartupLocation.CenterOwner;
            Background=(System.Windows.Media.Brush)Application.Current.FindResource("Bg");Foreground=System.Windows.Media.Brushes.White;
            var layout=new DockPanel{Margin=new Thickness(20)};Content=layout;
            var header=new StackPanel{Margin=new Thickness(0,0,0,12)};DockPanel.SetDock(header,Dock.Top);layout.Children.Add(header);
            header.Children.Add(new TextBlock{Text=game.GameName+UiText.Text(" · Steam AppID ")+game.AppId,FontSize=22,FontWeight=FontWeights.Bold,TextWrapping=TextWrapping.Wrap});
            header.Children.Add(new TextBlock{Text=UiText.Text("Historial: ")+HistoryGroup.Label(game)+UiText.Text("\nArchivo: ")+game.StatsPath+UiText.Text("\nCuenta Steam detectada: ")+(account==0?UiText.Text("no identificada"):account.ToString()),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,10)});
            double duration=preview.Schedule.Count==0?0:System.Linq.Enumerable.Max(preview.Schedule.Values,s=>s.OffsetSeconds);
            header.Children.Add(new TextBlock{Text=UiText.Text("Seleccionados: ")+preview.Selected+UiText.Text(" · Ya desbloqueados (se omiten): ")+preview.Already+UiText.Text(" · Por aplicar: ")+preview.Schedule.Count+UiText.Text(" · No encontrados: ")+preview.Missing+UiText.Text("\nDuración restante del plan: ")+LegacyTools.FormatSpan((long)Math.Round(duration))+UiText.Text(" + ")+parameters.InitialDelaySeconds+UiText.Text(" s de espera inicial."),TextWrapping=TextWrapping.Wrap,FontWeight=FontWeights.SemiBold});
            header.Children.Add(new TextBlock{FontStyle=FontStyles.Italic,Text=UiText.Text("Reparación (bloquear antes): ")+(parameters.Repair?UiText.Text("SÍ: los logros del plan se bloquearán y desbloquearán nuevamente."):UiText.Text("no"))+UiText.Text("\nRegistrar tiempo de juego: ")+(parameters.Idle?UiText.Text("sí"):UiText.Text("no"))+(!preview.Verified?UiText.Text("\nCLI legacy: no se pudo verificar qué logros ya están desbloqueados. Para omitirlos y reanudar con verificación, usá el CLI moderno."):""),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,0)});
            var footer=new StackPanel{Margin=new Thickness(0,12,0,0)};DockPanel.SetDock(footer,Dock.Bottom);layout.Children.Add(footer);
            footer.Children.Add(new TextBlock{FontStyle=FontStyles.Italic,Text=preview.Missing>0?UiText.Text("Hay códigos ausentes en Steam. Volvé al plan y revisá el juego, la edición y las correspondencias."):preview.Schedule.Count==0?UiText.Text("Todos los seleccionados ya están desbloqueados. No se realizarán cambios."):UiText.Text("Se modificarán los logros de la cuenta Steam iniciada. Las fechas históricas no se trasladan: los nuevos desbloqueos tendrán la fecha actual."),TextWrapping=TextWrapping.Wrap});
            var buttons=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right};footer.Children.Add(buttons);
            var back=new Button{Content=UiText.Text("Volver al plan")};back.Click+=(s,e)=>{DialogResult=false;};buttons.Children.Add(back);
            var confirm=new Button{Content=preview.Schedule.Count==0?UiText.Text("Cerrar resumen"):UiText.Text("Iniciar migración"),IsEnabled=preview.Missing==0};confirm.Click+=(s,e)=>{DialogResult=true;};buttons.Children.Add(confirm);
            var grid=new DataGrid{AutoGenerateColumns=false,IsReadOnly=true,CanUserAddRows=false,ItemsSource=preview.Items};layout.Children.Add(grid);
            grid.Columns.Add(new DataGridTextColumn{Header=UiText.Text("Logro"),Binding=new Binding(nameof(MigrationItem.Name)),Width=new DataGridLength(2,DataGridLengthUnitType.Star)});
            grid.Columns.Add(new DataGridTextColumn{Header=UiText.Text("Código Steam"),Binding=new Binding(nameof(MigrationItem.ApiId)),Width=170});
            grid.Columns.Add(new DataGridTextColumn{Header=UiText.Text("Acción"),Binding=new Binding(nameof(MigrationItem.Status)){Converter=UiTranslation.Instance},Width=150});
            grid.Columns.Add(new DataGridTextColumn{Header=UiText.Text("Detalle"),Binding=new Binding(nameof(MigrationItem.Detail)){Converter=UiTranslation.Instance},Width=new DataGridLength(2,DataGridLengthUnitType.Star)});
        }
    }
}
