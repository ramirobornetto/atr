using System.Text;
using System.Windows;
using System.Linq;
using System.Text.Json;
using AchievementTimelineReplay;
namespace AchievementTimelineReplay.Wpf
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            UiText.Initialize();
            if(e.Args.Length==2 && e.Args[0]=="--atr-update")
            {
                StartupUri=null;
                UpdateProgram.Main(new[]{e.Args[1]});
                Shutdown();return;
            }
            var assembly=typeof(App).Assembly;
            using var stream=assembly.GetManifestResourceStream(assembly.GetManifestResourceNames().Single(n=>n.EndsWith(".Localization.Resources.json")));
            foreach(var entry in JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string,string>>(stream))
                Resources[entry.Key]=UiText.Text(entry.Value);
            Resources["UiTranslation"]=UiTranslation.Instance;
            base.OnStartup(e);
        }
        public App()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            // Metadata covers implicit styles, local styles and controls built in C#.
            System.Windows.Controls.ToolTipService.InitialShowDelayProperty.OverrideMetadata(typeof(FrameworkElement),new FrameworkPropertyMetadata(300));
            System.Windows.Controls.ToolTipService.InitialShowDelayProperty.OverrideMetadata(typeof(FrameworkContentElement),new FrameworkPropertyMetadata(300));
            System.Windows.Controls.ToolTipService.BetweenShowDelayProperty.OverrideMetadata(typeof(FrameworkElement),new FrameworkPropertyMetadata(0));
            System.Windows.Controls.ToolTipService.BetweenShowDelayProperty.OverrideMetadata(typeof(FrameworkContentElement),new FrameworkPropertyMetadata(0));
        }
    }
}
