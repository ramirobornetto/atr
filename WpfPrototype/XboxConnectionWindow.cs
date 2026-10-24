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
        readonly TextBox client=new TextBox{Margin=new Thickness(8),MaxLength=36};
        readonly TextBlock status=new TextBlock{Margin=new Thickness(8),TextWrapping=TextWrapping.Wrap};
        readonly TextBox code=new TextBox{Margin=new Thickness(8),IsReadOnly=true,FontSize=23,Visibility=Visibility.Collapsed};
        readonly Button login=new Button{Content="Conectar cuenta Microsoft"},logout=new Button{Content="Desconectar"},cancel=new Button{Content="Cancelar conexión"};
        readonly Button open=new Button{Content="Abrir Microsoft"};
        CancellationTokenSource cancellation;string verification;
        public XboxConnectionWindow(XboxConnection connection)
        {
            this.connection=connection;Title="Conectar Xbox · ATR";Width=620;Height=620;MinHeight=500;MinWidth=500;WindowStartupLocation=WindowStartupLocation.CenterOwner;
            Background=(System.Windows.Media.Brush)Application.Current.FindResource("Bg");Foreground=System.Windows.Media.Brushes.White;
            var panel=new StackPanel{Margin=new Thickness(20)};Content=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
            panel.Children.Add(new TextBlock{Text="Xbox · perfil y logros",FontSize=22,Margin=new Thickness(8)});
            panel.Children.Add(new TextBlock{Text="Conectá tu cuenta en la página oficial de Microsoft. ATR solo consulta tu historial: no desbloquea logros en Xbox ni cambia tus partidas. No necesita Playnite.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(8)});
            panel.Children.Add(new TextBlock{Text="Client ID público de ATR (configuración inicial)",Margin=new Thickness(8,12,8,0)});panel.Children.Add(client);
            panel.Children.Add(new TextBlock{Text="Lo obtiene el responsable de ATR al registrar una aplicación Microsoft para cuentas personales y habilitar los flujos de cliente público. No es tu usuario, contraseña ni un Client Secret. Consultá XBOX.md incluido en el ZIP.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(8)});
            var buttons=new WrapPanel();buttons.Children.Add(login);buttons.Children.Add(logout);buttons.Children.Add(cancel);panel.Children.Add(buttons);panel.Children.Add(status);panel.Children.Add(code);panel.Children.Add(open);
            open.Visibility=Visibility.Collapsed;cancel.IsEnabled=false;
            try{client.Text=connection.ClientId;status.Text=connection.Connected?"Sesión guardada para este usuario de Windows.":"Sin cuenta conectada.";}catch(Exception ex){status.Text=ex.Message;}
            login.Click+=async(s,e)=>
            {
                try
                {
                    string id=XboxProfile.ClientId(client.Text.Trim());connection.Configure(id);
                    cancellation=new CancellationTokenSource();SetBusy(true);status.Text="Solicitando código a Microsoft…";
                    using var api=new XboxProfile();var device=await api.BeginAsync(id,cancellation.Token);
                    code.Text=device.UserCode;code.Visibility=open.Visibility=Visibility.Visible;verification=device.VerificationUri;
                    status.Text="Abrí "+verification+" e ingresá este código. Esperando tu inicio de sesión…";
                    var oauth=await api.WaitAsync(id,device,cancellation.Token);
                    var session=await api.AuthorizeAsync(oauth.AccessToken,cancellation.Token);
                    connection.SaveToken(oauth.RefreshToken);status.Text="Cuenta Xbox conectada. Activá «Xbox (perfil)» y escaneá la biblioteca.";
                    code.Text="";code.Visibility=open.Visibility=Visibility.Collapsed;
                }
                catch(OperationCanceledException){status.Text="Conexión cancelada.";}
                catch(Exception ex){status.Text=ex.Message;}
                finally{cancellation?.Dispose();cancellation=null;code.Text="";code.Visibility=open.Visibility=Visibility.Collapsed;SetBusy(false);}
            };
            open.Click+=(s,e)=>{try{Process.Start(new ProcessStartInfo(verification){UseShellExecute=true});}catch(Exception){status.Text="No se pudo abrir el navegador. Abrí manualmente "+verification+" e ingresá el código.";}};
            cancel.Click+=(s,e)=>cancellation?.Cancel();logout.Click+=(s,e)=>{try{connection.Disconnect();status.Text="Sesión local eliminada. Los historiales y planes se conservan.";}catch(Exception ex){status.Text=ex.Message;}};
            Closing+=(s,e)=>{if(cancellation!=null){e.Cancel=true;cancellation.Cancel();}};
        }
        void SetBusy(bool busy){client.IsEnabled=login.IsEnabled=logout.IsEnabled=!busy;cancel.IsEnabled=busy;}
    }
}
