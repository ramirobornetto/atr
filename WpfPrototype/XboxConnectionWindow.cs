using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using AchievementTimelineReplay;

namespace AchievementTimelineReplay.Wpf
{
    internal sealed class XboxConnectionWindow : Window
    {
        readonly XboxConnection connection;
        readonly TextBlock status=new TextBlock{FontStyle=FontStyles.Italic,Margin=new Thickness(8),TextWrapping=TextWrapping.Wrap};
        readonly TextBox code=new TextBox{Margin=new Thickness(8),IsReadOnly=true,FontSize=23,Visibility=Visibility.Collapsed};
        readonly Button login=new Button{Content="Conectar cuenta Microsoft"},logout=new Button{Content="Desconectar"},cancel=new Button{Content="Cancelar conexión"};
        readonly Button open=new Button{Content="Abrir Microsoft"},copy=new Button{Content="Copiar código"};
        public bool ConnectedNow {get;private set;}
        public bool DisconnectedNow {get;private set;}
        CancellationTokenSource cancellation;string verification;
        public XboxConnectionWindow(XboxConnection connection)
        {
            this.connection=connection;
            logout.Style=(Style)Application.Current.FindResource("DangerButton");cancel.Style=(Style)Application.Current.FindResource("DangerButton");
            Title="Conectar Xbox · ATR";Width=620;Height=620;MinHeight=500;MinWidth=500;WindowStartupLocation=WindowStartupLocation.CenterOwner;
            Background=(System.Windows.Media.Brush)Application.Current.FindResource("Bg");Foreground=System.Windows.Media.Brushes.White;
            var panel=new StackPanel{Margin=new Thickness(20)};Content=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
            panel.Children.Add(new TextBlock{Text="Xbox · perfil y logros",FontSize=22,Margin=new Thickness(8)});
            panel.Children.Add(new TextBlock{FontStyle=FontStyles.Italic,Text="Conectá tu cuenta en la página oficial de Microsoft. ATR solo consulta tu historial: no desbloquea logros en Xbox ni cambia tus partidas. No necesita Playnite.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(8)});
            var buttons=new WrapPanel();buttons.Children.Add(login);buttons.Children.Add(logout);buttons.Children.Add(cancel);panel.Children.Add(buttons);panel.Children.Add(status);panel.Children.Add(code);panel.Children.Add(open);panel.Children.Add(copy);
            open.Visibility=copy.Visibility=Visibility.Collapsed;cancel.IsEnabled=false;
            try{status.Text=connection.Connected?"Sesión guardada para este usuario de Windows.":"Sin cuenta conectada.";}catch(Exception ex){status.Text=ex.Message;}
            login.Click+=async(s,e)=>
            {
                try
                {
                    string id=connection.ClientId;connection.Configure(id);
                    cancellation=new CancellationTokenSource();SetBusy(true);status.Text="Solicitando código a Microsoft…";
                    using var api=new XboxProfile();var device=await api.BeginAsync(id,cancellation.Token);
                    code.Text=device.UserCode;code.Visibility=open.Visibility=copy.Visibility=Visibility.Visible;verification=device.VerificationUri;
                    status.Text="Ingresá este código en Microsoft. Esperando tu inicio de sesión…";
                    try{Process.Start(new ProcessStartInfo(verification){UseShellExecute=true});}catch{status.Text="Pulsá Abrir Microsoft e ingresá el código para continuar.";}
                    var oauth=await api.WaitAsync(id,device,cancellation.Token);
                    var session=await api.AuthorizeAsync(oauth.AccessToken,cancellation.Token);
                    connection.SaveToken(oauth.RefreshToken);ConnectedNow=true;DisconnectedNow=false;status.Text="Cuenta Xbox conectada. Al cerrar esta ventana quedará seleccionada para el próximo escaneo.";
                    code.Text="";code.Visibility=open.Visibility=copy.Visibility=Visibility.Collapsed;
                }
                catch(OperationCanceledException){status.Text="Conexión cancelada.";}
                catch(Exception ex){status.Text=ex.Message;}
                finally{cancellation?.Dispose();cancellation=null;code.Text="";code.Visibility=open.Visibility=copy.Visibility=Visibility.Collapsed;SetBusy(false);}
            };
            open.Click+=(s,e)=>{try{Process.Start(new ProcessStartInfo(verification){UseShellExecute=true});}catch(Exception){status.Text="No se pudo abrir el navegador. Abrí manualmente "+verification+" e ingresá el código.";}};
            copy.Click+=(s,e)=>{try{Clipboard.SetText(code.Text);status.Text="Código copiado. Pegalo en la página de Microsoft.";}catch{status.Text="No se pudo copiar. Ingresá el código que aparece arriba.";}};
            cancel.Click+=(s,e)=>cancellation?.Cancel();logout.Click+=(s,e)=>{try{connection.Disconnect();ConnectedNow=false;DisconnectedNow=true;status.Text="Sesión local eliminada. Los historiales y planes se conservan.";}catch(Exception ex){status.Text=ex.Message;}};
            Closing+=(s,e)=>{if(cancellation!=null){e.Cancel=true;cancellation.Cancel();}};
        }
        void SetBusy(bool busy){login.IsEnabled=logout.IsEnabled=!busy;cancel.IsEnabled=busy;}
    }
}
