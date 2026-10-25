using System;
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
        readonly TextBlock status=new TextBlock{FontStyle=FontStyles.Italic,Margin=new Thickness(8),TextWrapping=TextWrapping.Wrap};
        readonly Button login=new Button{Content=UiText.Text("Iniciar sesión en Epic")},disconnect=new Button{Content=UiText.Text("Desconectar")},cancel=new Button{Content=UiText.Text("Cancelar")};
        public bool ConnectedNow {get;private set;}
        public bool DisconnectedNow {get;private set;}
        CancellationTokenSource cancellation;
        public EpicConnectionWindow(EpicConnection connection)
        {
            this.connection=connection;
            disconnect.Style=(Style)Application.Current.FindResource("DangerButton");cancel.Style=(Style)Application.Current.FindResource("DangerButton");
            Title=UiText.Text("Conectar Epic Games · ATR");Width=680;Height=490;MinWidth=520;MinHeight=400;WindowStartupLocation=WindowStartupLocation.CenterOwner;
            Background=(System.Windows.Media.Brush)Application.Current.FindResource("Bg");Foreground=System.Windows.Media.Brushes.White;
            var panel=new StackPanel{Margin=new Thickness(20)};Content=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
            panel.Children.Add(new TextBlock{Text=UiText.Text("Epic Games · perfil y logros"),FontSize=22,Margin=new Thickness(8)});
            panel.Children.Add(new TextBlock{FontStyle=FontStyles.Italic,Text=UiText.Text("Ingresá en el sitio oficial de Epic dentro de la ventana de conexión. ATR completará la vinculación automáticamente. No modifica tus logros ni tus partidas en Epic."),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(8)});
            var buttons=new WrapPanel();buttons.Children.Add(login);buttons.Children.Add(disconnect);buttons.Children.Add(cancel);panel.Children.Add(buttons);panel.Children.Add(status);cancel.IsEnabled=false;
            try{status.Text=connection.Connected?UiText.Text("Cuenta conectada: ")+(connection.Read().DisplayName??"Epic")+UiText.Text(". Podés escanear Epic."):UiText.Text("Sin cuenta conectada.");}catch{status.Text=UiText.Text("La sesión guardada no está disponible. Iniciá sesión nuevamente.");}
            login.Click+=async(s,e)=>
            {
                try
                {
                    var browser=new EpicBrowserWindow{Owner=this};
                    if(browser.ShowDialog()==true)await Connect(browser.TakeCode());
                    else if(browser.StartupError!=null)status.Text=browser.StartupError;
                }
                catch{status.Text=UiText.Text("No se pudo abrir el navegador integrado. Comprobá que Microsoft Edge WebView2 Runtime esté instalado y actualizado.");}
            };
            disconnect.Click+=(s,e)=>{try{connection.Disconnect();ConnectedNow=false;DisconnectedNow=true;status.Text=UiText.Text("Cuenta desconectada de ATR. Los historiales y planes se conservan.");}catch(Exception ex){status.Text=ex.Message;}};
            cancel.Click+=(s,e)=>cancellation?.Cancel();Closing+=(s,e)=>{if(cancellation!=null){e.Cancel=true;cancellation.Cancel();}};
        }
        async Task Connect(string value)
        {
            if(cancellation!=null)return;
            try
            {
                value=EpicProfile.AuthorizationCode(value);cancellation=new CancellationTokenSource();Busy(true);status.Text=UiText.Text("Conectando con Epic…");
                using var api=new EpicProfile();var session=await api.LoginAsync(value,cancellation.Token);
                connection.Save(session);ConnectedNow=true;DisconnectedNow=false;status.Text=UiText.Text("Conectado como ")+(session.DisplayName??session.AccountId)+UiText.Text(". Al cerrar esta ventana quedará seleccionada para el próximo escaneo.");
            }
            catch(OperationCanceledException){status.Text=UiText.Text("Conexión cancelada.");}
            catch(Exception ex){status.Text=ex.Message;}
            finally{cancellation?.Dispose();cancellation=null;Busy(false);}
        }
        void Busy(bool busy){login.IsEnabled=disconnect.IsEnabled=!busy;cancel.IsEnabled=busy;}
    }
}
