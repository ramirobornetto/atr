using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using AchievementTimelineReplay;

namespace AchievementTimelineReplay.Wpf
{
    public sealed class UiTranslation : IValueConverter
    {
        public static readonly UiTranslation Instance=new UiTranslation();
        public object Convert(object value,Type type,object parameter,CultureInfo culture)=>value is string text?UiText.Text(text):value;
        public object ConvertBack(object value,Type type,object parameter,CultureInfo culture)=>throw new NotSupportedException();
    }
}
