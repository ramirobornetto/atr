using System.Text;
using System.Windows;
namespace AchievementTimelineReplay.Wpf
{
    public partial class App : Application
    {
        public App() { Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); }
    }
}
