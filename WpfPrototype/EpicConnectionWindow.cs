using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using AchievementTimelineReplay;

namespace AchievementTimelineReplay.Wpf
{
    internal sealed class EpicConnectionWindow : Window
    {
        readonly EpicConnection connection;
        readonly PasswordBox code=new PasswordBox{Margin=new Thickness(8),MaxLength=8192};
        readonly TextBlock status=new TextBlock{Margin=new Thickness(8),TextWrapping=TextWrapping.Wrap};
        readonly Button connect=new Button{Content="Conectar"},disconnect=new Button{Content="Desconectar"},cancel=new Button{Content="Cancelar"};
        CancellationTokenSource cancellation;
        public EpicConnectionWindow(EpicConnection connection)
        {
            this.connection=connection;Title="Conectar Epic Games · ATR";Width=680;Height=570;MinWidth=520;MinHeight=460;WindowStartupLocation=WindowStartupLocation.CenterOwner;
            Background=(System.Windows.Media.Brush)Application.Current.FindResource("Bg");Foreground=System.Windows.Media.Brushes.White;
            var panel=new StackPanel{Margin=new Thickness(20)};Content=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
            panel.Children.Add(new TextBlock{Text="Epic Games · perfil y logros",FontSize=22,Margin=new Thickness(8)});
            panel.Children.Add(new TextBlock{Text="ATR consulta tu biblioteca y tus logros mediante un flujo compatible con Epic Games Launcher. La contraseña y la verificación se ingresan en el sitio oficial de Epic, nunca en ATR. Esta conexión es independiente de PA y no modifica logros en Epic.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(8)});
            var browser=new WrapPanel();panel.Children.Add(browser);var login=new Button{Content="1. Iniciar sesión en Epic"};var authorization=new Button{Content="2. Obtener código"};browser.Children.Add(login);browser.Children.Add(authorization);
            login.Click+=(s,e)=>Open(EpicProfile.LoginUrl);authorization.Click+=(s,e)=>Open(EpicProfile.CodeUrl);
            panel.Children.Add(new TextBlock{Text="Después de iniciar sesión, abrí Obtener código. Epic mostrará una respuesta: copiá el valor authorizationCode y pegalo abajo. También podés pegar el JSON que contiene ese campo. El código es temporal y de un solo uso; no lo compartas.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(8)});
            panel.Children.Add(code);var buttons=new WrapPanel();buttons.Children.Add(connect);buttons.Children.Add(disconnect);buttons.Children.Add(cancel);panel.Children.Add(buttons);panel.Children.Add(status);
            cancel.IsEnabled=false;
            try{status.Text=connection.Connected?"Sesión local guardada: "+(connection.Read().DisplayName??"cuenta Epic")+". Desconectar conserva historiales y planes.":"Sin cuenta conectada.";}catch(Exception ex){status.Text=ex.Message;}
            connect.Click+=async(s,e)=>
            {
                try
                {
                    string value=EpicProfile.AuthorizationCode(code.Password);cancellation=new CancellationTokenSource();Busy(true);status.Text="Conectando con Epic…";
                    using var api=new EpicProfile();var session=await api.LoginAsync(value,cancellation.Token);
                    connection.Save(session);status.Text="Conectado como "+(session.DisplayName??session.AccountId)+". Comprobá que sea tu cuenta, activá Epic (perfil) y escaneá la biblioteca.";
                }
                catch(OperationCanceledException){status.Text="Conexión cancelada.";}
                catch(Exception ex){status.Text=ex.Message;}
                finally{code.Clear();cancellation?.Dispose();cancellation=null;Busy(false);}
            };
            disconnect.Click+=(s,e)=>{try{connection.Disconnect();status.Text="Sesión local eliminada. Los historiales y planes se conservan.";}catch(Exception ex){status.Text=ex.Message;}};
            cancel.Click+=(s,e)=>cancellation?.Cancel();Closing+=(s,e)=>{if(cancellation!=null){e.Cancel=true;cancellation.Cancel();}};
        }
        void Busy(bool busy){code.IsEnabled=connect.IsEnabled=disconnect.IsEnabled=!busy;cancel.IsEnabled=busy;}
        void Open(string url){try{Process.Start(new ProcessStartInfo(url){UseShellExecute=true});}catch(Exception){status.Text="Abrí esta dirección en tu navegador: "+url;}}
    }
}
