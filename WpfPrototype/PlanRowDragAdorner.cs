using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AchievementTimelineReplay.Wpf
{
    // Snapshot the row before it is recycled by virtualization. Only the preview moves; it cannot intercept input.
    internal sealed class PlanRowDragAdorner : Adorner
    {
        readonly BitmapSource snapshot;
        readonly double width,height,header;
        double top;
        public double RowHeight=>height;
        public PlanRowDragAdorner(DataGrid grid,DataGridRow row):base(grid)
        {
            IsHitTestVisible=false;Opacity=0.96;
            height=Math.Max(1,row.ActualHeight);header=grid.ColumnHeaderHeight;
            width=Math.Max(1,Math.Min(row.ActualWidth,grid.ActualWidth-SystemParameters.VerticalScrollBarWidth));
            double left=Math.Max(0,-row.TranslatePoint(new Point(),grid).X);
            var visual=new DrawingVisual();
            using(var drawing=visual.RenderOpen())
                drawing.DrawRectangle(new VisualBrush(row){ViewboxUnits=BrushMappingMode.Absolute,Viewbox=new Rect(left,0,width,height),Stretch=Stretch.Fill},null,new Rect(0,0,width,height));
            var dpi=VisualTreeHelper.GetDpi(grid);
            var bitmap=new RenderTargetBitmap((int)Math.Ceiling(width*dpi.DpiScaleX),(int)Math.Ceiling(height*dpi.DpiScaleY),dpi.PixelsPerInchX,dpi.PixelsPerInchY,PixelFormats.Pbgra32);
            bitmap.Render(visual);bitmap.Freeze();snapshot=bitmap;
        }
        public void MoveTo(double cursorY,double grabOffset)
        {
            top=Math.Clamp(cursorY-grabOffset,header,Math.Max(header,AdornedElement.RenderSize.Height-height));InvalidateVisual();
        }
        protected override void OnRender(DrawingContext drawing)
        {
            var bounds=new Rect(1,top,width-1,height);
            drawing.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(245,12,24,27)),null,bounds,3,3);
            drawing.DrawImage(snapshot,bounds);
            drawing.DrawRoundedRectangle(null,new Pen(new SolidColorBrush(Color.FromRgb(112,248,240)),1.5),bounds,3,3);
        }
    }
}
