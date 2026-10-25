using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using AchievementTimelineReplay;

namespace AchievementTimelineReplay.Wpf
{
    internal sealed class LanguageRestartWindow : Window
    {
        public LanguageRestartWindow(Func<bool> canRestart)
        {
            Title=UiText.Text("Idioma de la interfaz");Width=510;SizeToContent=SizeToContent.Height;ResizeMode=ResizeMode.NoResize;WindowStartupLocation=WindowStartupLocation.CenterOwner;
            Background=(Brush)Application.Current.FindResource("Bg");Foreground=Brushes.White;
            var panel=new StackPanel{Margin=new Thickness(22)};Content=panel;
            panel.Children.Add(new TextBlock{Text=UiText.Text("Se necesita reiniciar ATR"),FontSize=19,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,12)});
            panel.Children.Add(new TextBlock{Text=UiText.Text("El idioma se guardó. Reiniciá la aplicación para que los cambios surtan efecto. Podés reiniciar ahora o hacerlo más tarde."),TextWrapping=TextWrapping.Wrap,FontStyle=FontStyles.Italic});
            var wait=new TextBlock{Text=UiText.Text("Hay una operación en curso. Podrás reiniciar cuando termine; también podés elegir Después."),TextWrapping=TextWrapping.Wrap,FontStyle=FontStyles.Italic,Margin=new Thickness(0,10,0,0),Foreground=(Brush)Application.Current.FindResource("Muted")};panel.Children.Add(wait);
            var actions=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,18,0,0)};panel.Children.Add(actions);
            var now=new Button{Content=UiText.Text("Reiniciar ahora"),Style=(Style)Application.Current.FindResource("AccentButton"),IsDefault=true};
            var later=new Button{Content=UiText.Text("Después"),IsCancel=true};now.Click+=(s,e)=>{if(canRestart())DialogResult=true;};later.Click+=(s,e)=>DialogResult=false;actions.Children.Add(now);actions.Children.Add(later);
            void Refresh(){now.IsEnabled=canRestart();wait.Visibility=now.IsEnabled?Visibility.Collapsed:Visibility.Visible;}
            var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(250)};timer.Tick+=(s,e)=>Refresh();Refresh();
            Loaded+=(s,e)=>timer.Start();Closed+=(s,e)=>timer.Stop();
        }
    }
}
