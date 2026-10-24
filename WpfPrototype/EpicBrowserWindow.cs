using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using AchievementTimelineReplay;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace AchievementTimelineReplay.Wpf
{
    internal sealed class EpicBrowserWindow : Window
    {
        readonly WebView2 browser=new WebView2();
        readonly TextBlock status=new TextBlock{Text="Abriendo Epic…",Margin=new Thickness(8)};
        bool closed,expectedCallback;
        string authorizationCode;
        public bool UseExternal {get;private set;}
        public string TakeCode(){string value=authorizationCode;authorizationCode=null;return value;}
        public EpicBrowserWindow()
        {
            Title="Iniciar sesión en Epic Games";Width=1000;Height=800;MinWidth=650;MinHeight=500;WindowStartupLocation=WindowStartupLocation.CenterOwner;
            Background=(System.Windows.Media.Brush)Application.Current.FindResource("Bg");Foreground=System.Windows.Media.Brushes.White;
            var layout=new DockPanel();Content=layout;DockPanel.SetDock(status,Dock.Top);layout.Children.Add(status);
            var buttons=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right};DockPanel.SetDock(buttons,Dock.Bottom);layout.Children.Add(buttons);
            var next=new Button{Content="Continuar después de ingresar"};next.Click+=(s,e)=>{if(browser.CoreWebView2!=null)browser.CoreWebView2.Navigate(EpicProfile.CodeUrl);};buttons.Children.Add(next);
            var external=new Button{Content="Usar navegador externo"};external.Click+=(s,e)=>{UseExternal=true;DialogResult=false;};buttons.Children.Add(external);layout.Children.Add(browser);
            Loaded+=async(s,e)=>
            {
                try
                {
                    var environment=await CoreWebView2Environment.CreateAsync(null,Path.Combine(LegacyTools.DataDirectory,"Epic","Browser"));
                    if(closed)return;await browser.EnsureCoreWebView2Async(environment);if(closed)return;
                    browser.CoreWebView2.Settings.AreDevToolsEnabled=false;browser.CoreWebView2.Settings.IsStatusBarEnabled=false;browser.CoreWebView2.Settings.IsWebMessageEnabled=false;
                    browser.CoreWebView2.CookieManager.DeleteAllCookies();
                    browser.CoreWebView2.NavigationStarting+=(sender,args)=>
                    {
                        string code=EpicLoginCapture.Callback(args.Uri,expectedCallback);
                        if(code!=null){args.Cancel=true;Complete(code);return;}
                        expectedCallback=EpicLoginCapture.CodeEndpoint(args.Uri);
                    };
                    browser.CoreWebView2.NewWindowRequested+=(sender,args)=>
                    {args.Handled=true;if(args.IsUserInitiated && Uri.TryCreate(args.Uri,UriKind.Absolute,out var target) && target.Scheme=="https")browser.CoreWebView2.Navigate(args.Uri);};
                    browser.CoreWebView2.WebResourceResponseReceived+=async(sender,args)=>
                    {
                        try
                        {
                            if(closed || !EpicLoginCapture.CodeEndpoint(args.Request.Uri) || args.Response.StatusCode!=200)return;
                            using var stream=await args.Response.GetContentAsync();if(stream==null || closed)return;
                            using var memory=new MemoryStream();var buffer=new byte[1024];int n;
                            while((n=await stream.ReadAsync(buffer))>0){if(memory.Length+n>8192)return;memory.Write(buffer,0,n);}
                            string code=EpicLoginCapture.ResponseCode(args.Request.Uri,Encoding.UTF8.GetString(memory.ToArray()));if(code!=null)Complete(code);
                        }
                        catch{ /* Login pages and non-code responses remain in the browser. */ }
                    };
                    browser.CoreWebView2.NavigationCompleted+=async(sender,args)=>
                    {
                        if(closed || !EpicLoginCapture.Trusted(browser.CoreWebView2.Source))return;
                        status.Text="Ingresá en Epic. La conexión se completará automáticamente; también podés pulsar Continuar.";
                        try{await browser.ExecuteScriptAsync("fetch("+JsonSerializer.Serialize(EpicProfile.CodeUrl)+",{credentials:'include',cache:'no-store'}).catch(()=>{}); void 0;");}catch{}
                    };
                    status.Text="La contraseña y la verificación se ingresan en el sitio de Epic.";
                    browser.CoreWebView2.Navigate(EpicProfile.LoginUrl+"?redirectUrl="+Uri.EscapeDataString(EpicProfile.CodeUrl));
                }
                catch{if(!closed){UseExternal=true;DialogResult=false;}}
            };
            Closed+=(s,e)=>{closed=true;browser.Dispose();};
        }
        void Complete(string code)
        {
            if(closed)return;authorizationCode=code;DialogResult=true;
        }
    }
}
