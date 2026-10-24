using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
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
        readonly StackPanel manual=new StackPanel{Visibility=Visibility.Collapsed};
        readonly Button login=new Button{Content="Iniciar sesión en Epic"},external=new Button{Content="Usar navegador externo"},disconnect=new Button{Content="Desconectar"},cancel=new Button{Content="Cancelar"},paste=new Button{Content="Pegar código y conectar"};
        public bool ConnectedNow {get;private set;}
        public bool DisconnectedNow {get;private set;}
        CancellationTokenSource cancellation;
        public EpicConnectionWindow(EpicConnection connection)
        {
            this.connection=connection;Title="Conectar Epic Games · ATR";Width=680;Height=490;MinWidth=520;MinHeight=400;WindowStartupLocation=WindowStartupLocation.CenterOwner;
            Background=(System.Windows.Media.Brush)Application.Current.FindResource("Bg");Foreground=System.Windows.Media.Brushes.White;
            var panel=new StackPanel{Margin=new Thickness(20)};Content=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
            panel.Children.Add(new TextBlock{Text="Epic Games · perfil y logros",FontSize=22,Margin=new Thickness(8)});
            panel.Children.Add(new TextBlock{Text="Ingresá en el sitio oficial de Epic dentro de la ventana de conexión. ATR completará la vinculación automáticamente. No modifica tus logros ni tus partidas en Epic.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(8)});
            var buttons=new WrapPanel();buttons.Children.Add(login);buttons.Children.Add(external);buttons.Children.Add(disconnect);buttons.Children.Add(cancel);panel.Children.Add(buttons);panel.Children.Add(status);panel.Children.Add(manual);
            manual.Children.Add(new TextBlock{Text="Alternativa con navegador externo: ingresá en Epic, obtené el código temporal y pegalo para conectar. Podés pegar el JSON completo que contiene authorizationCode.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(8)});
            var get=new Button{Content="Obtener código en Epic"};get.Click+=(s,e)=>Open(EpicProfile.CodeUrl);manual.Children.Add(get);manual.Children.Add(code);
            var connect=new Button{Content="Conectar con el código"};var finish=new WrapPanel();finish.Children.Add(paste);finish.Children.Add(connect);manual.Children.Add(finish);cancel.IsEnabled=false;
            try{status.Text=connection.Connected?"Cuenta conectada: "+(connection.Read().DisplayName??"Epic")+". Podés escanear Epic.":"Sin cuenta conectada.";}catch{status.Text="La sesión guardada no está disponible. Iniciá sesión nuevamente.";}
            login.Click+=async(s,e)=>
            {
                try
                {
                    var browser=new EpicBrowserWindow{Owner=this};
                    if(browser.ShowDialog()==true)await Connect(browser.TakeCode());
                    else if(browser.UseExternal)External();
                }
                catch{External();}
            };
            external.Click+=(s,e)=>External();connect.Click+=async(s,e)=>await Connect(code.Password);
            paste.Click+=async(s,e)=>{try{if(Clipboard.ContainsText())await Connect(Clipboard.GetText());else status.Text="Copiá primero el código que muestra Epic.";}catch{status.Text="No se pudo leer el portapapeles. Pegá el código manualmente.";}};
            disconnect.Click+=(s,e)=>{try{connection.Disconnect();ConnectedNow=false;DisconnectedNow=true;status.Text="Cuenta desconectada de ATR. Los historiales y planes se conservan.";}catch(Exception ex){status.Text=ex.Message;}};
            cancel.Click+=(s,e)=>cancellation?.Cancel();Closing+=(s,e)=>{if(cancellation!=null){e.Cancel=true;cancellation.Cancel();}};
        }
        async Task Connect(string value)
        {
            if(cancellation!=null)return;
            try
            {
                value=EpicProfile.AuthorizationCode(value);cancellation=new CancellationTokenSource();Busy(true);status.Text="Conectando con Epic…";
                using var api=new EpicProfile();var session=await api.LoginAsync(value,cancellation.Token);
                connection.Save(session);ConnectedNow=true;DisconnectedNow=false;manual.Visibility=Visibility.Collapsed;status.Text="Conectado como "+(session.DisplayName??session.AccountId)+". Al cerrar esta ventana quedará seleccionada para el próximo escaneo.";
            }
            catch(OperationCanceledException){status.Text="Conexión cancelada.";}
            catch(Exception ex){status.Text=ex.Message;}
            finally{code.Clear();cancellation?.Dispose();cancellation=null;Busy(false);}
        }
        void External(){manual.Visibility=Visibility.Visible;status.Text="Usá tu navegador para ingresar y obtener el código de conexión.";Open(EpicProfile.LoginUrl+"?redirectUrl="+Uri.EscapeDataString(EpicProfile.CodeUrl));}
        void Busy(bool busy){code.IsEnabled=manual.IsEnabled=login.IsEnabled=external.IsEnabled=disconnect.IsEnabled=!busy;cancel.IsEnabled=busy;}
        void Open(string url){try{Process.Start(new ProcessStartInfo(url){UseShellExecute=true});}catch{status.Text="No se pudo abrir el navegador. Usá la alternativa manual desde el sitio de Epic Games.";}}
    }
}
