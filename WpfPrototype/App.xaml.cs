using System.Text;
using System.Windows;
namespace AchievementTimelineReplay.Wpf
{
    public partial class App : Application
    {
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
