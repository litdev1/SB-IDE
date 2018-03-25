using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SB_IDE.Dialogs
{
    /// <summary>
    /// Interaction logic for ShapesEditor.xaml
    /// </summary>
    public partial class ShapesEditor : Window
    {
        public static bool Active = false;
        public static ShapesEditor THIS;
        private MainWindow mainWindow;
        private SBDocument sbDocument;
        private VisualContainer visualContainer;
        private DrawingGroup drawingGroup;
        private List<PropertyData> properties;
        private List<PropertyData> modifiers;

        private Shape currentShape = null;
        private Shape lastShape = null;
        private FrameworkElement currentElt = null;

        private Point startGlobal;
        private Point startLocal;
        private double startWidth;
        private double startHeight;

        private List<string> names;
        private Brush background;
        private Brush brush;
        private Pen pen;
        private FontFamily fontFamily;
        private FontStyle fontStyle;
        private double fontSize;
        private FontWeight fontWeight;

        private double fixDec = 0.01;
        private int snap = 10;
        private string mode = "SEL";

        public ShapesEditor(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;
            THIS = this;

            InitializeComponent();

            FontSize = 12 + MainWindow.zoom;
            Topmost = true;

            Height = SystemParameters.PrimaryScreenHeight - 40;
            Left = SystemParameters.PrimaryScreenWidth - Width - 20;
            Top = (SystemParameters.PrimaryScreenHeight - Height) / 2;

            Resources["GridBrushBackground"] = new SolidColorBrush(MainWindow.IntToColor(MainWindow.BACKGROUND_COLOR));
            Resources["GridBrushForeground"] = new SolidColorBrush(MainWindow.IntToColor(MainWindow.FOREGROUND_COLOR));
            Resources["SplitterBrush"] = new SolidColorBrush(MainWindow.IntToColor(MainWindow.SPLITTER_COLOR));

            canvas.Width = double.Parse(textBoxWidth.Text);
            canvas.Height = double.Parse(textBoxHeight.Text);
            canvas.MouseMove += new MouseEventHandler(canvasMouseMove);
            canvas.MouseUp += new MouseButtonEventHandler(canvasMouseUp);
            names = new List<string>();
            background = canvas.Background;
            brush = Brushes.SlateBlue;
            pen = new Pen(Brushes.Black, 2);
            fontFamily = new FontFamily("Tahoma");
            fontStyle = FontStyles.Normal;
            fontSize = 12.0;
            fontWeight = FontWeights.Bold;

            drawingGroup = new DrawingGroup();
            visualContainer = new VisualContainer(drawingGroup);
            visualGrid.Children.Add(visualContainer);

            textBoxSnap.Text = snap.ToString();
            SetSnap();

            properties = new List<PropertyData>();
            dataGridProperties.ItemsSource = properties;

            modifiers = new List<PropertyData>();
            dataGridModifiers.ItemsSource = modifiers;

            WindowsFormsHost host = new WindowsFormsHost()
            {
                Margin = new Thickness(10),
            };
            System.Windows.Forms.Panel panel = new System.Windows.Forms.Panel();
            sbDocument = new SBDocument();
            panel.Contains(sbDocument.TextArea);
            host.Child = sbDocument.TextArea;
            codeGrid.Children.Add(host);

            Display();
        }

        public void Display()
        {
        }

        public void SetStart(Point start)
        {
            startGlobal = start;
            startLocal = Snap(currentShape.shape.TranslatePoint(new Point(Shape.HandleShort, Shape.HandleShort), canvas));
            currentElt.Measure(new Size(double.MaxValue, double.MaxValue));
            startWidth = currentElt.DesiredSize.Width;
            startHeight = currentElt.DesiredSize.Height;
            currentElt.Width = startWidth;
            currentElt.Height = startHeight;
        }

        private void UpdateView()
        {
            if (null != currentShape)
            {
                try
                {
                    RotateTransform rotateTransform = new RotateTransform();
                    rotateTransform.CenterX = currentElt.ActualWidth / 2.0;
                    rotateTransform.CenterY = currentElt.ActualHeight / 2.0;
                    rotateTransform.Angle = double.Parse(currentShape.modifiers["Angle"]);
                    currentElt.RenderTransform = new TransformGroup();
                    ((TransformGroup)currentElt.RenderTransform).Children.Add(rotateTransform);
                    currentElt.Opacity = double.Parse(currentShape.modifiers["Opacity"]) / 100.0;
                }
                catch
                {

                }
            }

            canvas.UpdateLayout();
            ShowProperties();
            ShowModifiers();
            ShowCode();
        }

        private void Delete()
        {
            if (null == currentShape) return;

            canvas.Children.Remove(currentShape.grid);

            currentElt = null;
            currentShape = null;
            lastShape = null;
            mode = "SEL";
            UpdateView();
        }

        private void DeleteAll()
        {
            canvas.Children.Clear();
            names.Clear();

            currentElt = null;
            currentShape = null;
            lastShape = null;
            mode = "SEL";
            UpdateView();
        }

        private void eltPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (null != lastShape) lastShape.ShowHandles(false);
            currentElt = (FrameworkElement)sender;
            currentShape = (Shape)currentElt.Tag;
            currentShape.ShowHandles(true);
            lastShape = currentShape;

            if (null != e)
            {
                mode = mode == "SEL" ? currentElt.Name : "SEL";
                Cursor = Cursors.Hand;
                SetStart(e.GetPosition(canvas));
            }

            UpdateView();
        }

        private void canvasMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (null == currentShape) return;

            mode = "SEL";
            Cursor = Cursors.Arrow;

            UpdateView();
        }

        private void canvasMouseMove(object sender, MouseEventArgs e)
        {
            if (null == currentShape) return;
            if (mode == "SEL") return;

            Point position = e.GetPosition(canvas);
            Vector change = Snap(position - startGlobal);

            switch (mode)
            {
                case "TL":
                    if (startWidth - change.X > 0 && startHeight - change.Y > 0)
                    {
                        if (currentElt.GetType() == typeof(Polygon))
                        {
                            UpdatePolygon(-change.X, -change.Y);
                        }
                        else if (currentElt.GetType() == typeof(Line))
                        {
                            UpdateLine(-change.X, -change.Y);
                        }
                        currentElt.Width = startWidth - change.X;
                        currentElt.Height = startHeight - change.Y;
                        Canvas.SetLeft(currentShape.shape, startLocal.X + change.X - Shape.HandleShort);
                        Canvas.SetTop(currentShape.shape, startLocal.Y + change.Y - Shape.HandleShort);
                    }
                    break;
                case "TR":
                    if (startWidth + change.X > 0 && startHeight - change.Y > 0)
                    {
                        if (currentElt.GetType() == typeof(Polygon))
                        {
                            UpdatePolygon(change.X, -change.Y);
                        }
                        else if (currentElt.GetType() == typeof(Line))
                        {
                            UpdateLine(change.X, -change.Y);
                        }
                        currentElt.Width = startWidth + change.X;
                        currentElt.Height = startHeight - change.Y;
                        Canvas.SetTop(currentShape.shape, startLocal.Y + change.Y - Shape.HandleShort);
                    }
                    break;
                case "BL":
                    if (startWidth - change.X > 0 && startHeight + change.Y > 0)
                    {
                        if (currentElt.GetType() == typeof(Polygon))
                        {
                            UpdatePolygon(-change.X, change.Y);
                        }
                        else if (currentElt.GetType() == typeof(Line))
                        {
                            UpdateLine(-change.X, change.Y);
                        }
                        currentElt.Width = startWidth - change.X;
                        currentElt.Height = startHeight + change.Y;
                        Canvas.SetLeft(currentShape.shape, startLocal.X + change.X - Shape.HandleShort);
                    }
                    break;
                case "BR":
                    if (startWidth + change.X > 0 && startHeight + change.Y > 0)
                    {
                        if (currentElt.GetType() == typeof(Polygon))
                        {
                            UpdatePolygon(change.X, change.Y);
                        }
                        else if (currentElt.GetType() == typeof(Line))
                        {
                            UpdateLine(change.X, change.Y);
                        }
                        currentElt.Width = startWidth + change.X;
                        currentElt.Height = startHeight + change.Y;
                    }
                    break;
                case "L":
                    if (startWidth - change.X > 0)
                    {
                        if (currentElt.GetType() == typeof(Polygon))
                        {
                            UpdatePolygon(-change.X, 0);
                        }
                        else if (currentElt.GetType() == typeof(Line))
                        {
                            UpdateLine(-change.X, 0);
                        }
                        currentElt.Width = Math.Max(0, startWidth - change.X);
                        Canvas.SetLeft(currentShape.shape, startLocal.X + change.X - Shape.HandleShort);
                    }
                    break;
                case "R":
                    if (startWidth + change.X > 0)
                    {
                        if (currentElt.GetType() == typeof(Polygon))
                        {
                            UpdatePolygon(change.X, 0);
                        }
                        else if (currentElt.GetType() == typeof(Line))
                        {
                            UpdateLine(change.X, 0);
                        }
                        currentElt.Width = Math.Max(0, startWidth + change.X);
                    }
                    break;
                case "T":
                    if (startHeight - change.Y > 0)
                    {
                        if (currentElt.GetType() == typeof(Polygon))
                        {
                            UpdatePolygon(0, -change.Y);
                        }
                        else if (currentElt.GetType() == typeof(Line))
                        {
                            UpdateLine(0, -change.Y);
                        }
                        currentElt.Height = Math.Max(0, startHeight - change.Y);
                        Canvas.SetTop(currentShape.shape, startLocal.Y + change.Y - Shape.HandleShort);
                    }
                    break;
                case "B":
                    if (startHeight + change.Y > 0)
                    {
                        if (currentElt.GetType() == typeof(Polygon))
                        {
                            UpdatePolygon(0, change.Y);
                        }
                        else if (currentElt.GetType() == typeof(Line))
                        {
                            UpdateLine(0, change.Y);
                        }
                        currentElt.Height = Math.Max(0, startHeight + change.Y);
                    }
                    break;
                default:
                    Canvas.SetLeft(currentShape.shape, startLocal.X + change.X - Shape.HandleShort);
                    Canvas.SetTop(currentShape.shape, startLocal.Y + change.Y - Shape.HandleShort);
                    break;
            }
        }

        private void UpdatePolygon(double changeX, double changeY)
        {
            Polygon polygon = (Polygon)currentElt;
            double scaleX = (startWidth + changeX) / currentElt.Width;
            double scaleY = (startHeight + changeY) / currentElt.Height;
            for (int i = 0; i < polygon.Points.Count; i++)
            {
                polygon.Points[i] = new Point(polygon.Points[i].X * scaleX, polygon.Points[i].Y * scaleY);
            }
        }

        private void UpdateLine(double changeX, double changeY)
        {
            Line line = (Line)currentElt;
            double scaleX = (startWidth + changeX) / currentElt.Width;
            double scaleY = (startHeight + changeY) / currentElt.Height;
            line.X1 *= scaleX;
            line.Y1 *= scaleY;
            line.X2 *= scaleX;
            line.Y2 *= scaleY;
        }

        private Point Snap(Point point)
        {
            if (snap < 1) return point;
            point.X = snap * Math.Round(point.X / snap);
            point.Y = snap * Math.Round(point.Y / snap);
            return point;
        }

        private Vector Snap(Vector vector)
        {
            if (snap < 1) return vector;
            vector.X = snap * Math.Round(vector.X / snap);
            vector.Y = snap * Math.Round(vector.Y / snap);
            return vector;
        }

        private void SetSnap()
        {
            DrawingContext dc = drawingGroup.Open();
            Color color = ((SolidColorBrush)background).Color;
            color = Color.FromArgb(255, (byte)(255 - color.R), (byte)(255 - color.G), (byte)(255 - color.B));
            if (snap >= 5)
            {
                for (int i = snap; i < canvas.Width; i += snap)
                {
                    for (int j = snap; j < canvas.Height; j += snap)
                    {
                        dc.DrawRectangle(null, new Pen(new SolidColorBrush(color), 0.5), new Rect(i, j, 0.5, 0.5));
                    }
                }
            }
            dc.Close();
        }

        private string GetName(string label)
        {
            int i = 1;
            while (names.Contains(label + i)) i++;
            names.Add(label + i);
            return names.Last();
        }

        private string ColorName(Brush brush)
        {
            return ColorName(((SolidColorBrush)brush).Color);
        }

        private string ColorName(Color color)
        {
            Type colorsType = typeof(Colors);
            PropertyInfo[] colorsTypePropertyInfos = colorsType.GetProperties(BindingFlags.Public | BindingFlags.Static);

            foreach (PropertyInfo colorsTypePropertyInfo in colorsTypePropertyInfos)
            {
                string colorName = colorsTypePropertyInfo.Name;
                if ((Color)ColorConverter.ConvertFromString(colorName) == color) return colorName;
            }
            return color.ToString();
        }

        private void ShowProperties()
        {
            try
            {
                properties.Clear();
                if (null != currentShape)
                {
                    properties.Add(new PropertyData() { Property = "Name", Value = currentElt.Name });
                    if (currentElt.GetType() == typeof(Rectangle))
                    {
                        Rectangle shape = (Rectangle)currentElt;
                        properties.Add(new PropertyData() { Property = "Width", Value = shape.Width.ToString() });
                        properties.Add(new PropertyData() { Property = "Height", Value = shape.Height.ToString() });
                        properties.Add(new PropertyData() { Property = "Fill", Value = ColorName(shape.Fill) });
                        properties.Add(new PropertyData() { Property = "Stroke", Value = ColorName(shape.Stroke) });
                        properties.Add(new PropertyData() { Property = "StrokeThickness", Value = shape.StrokeThickness.ToString() });
                    }
                    else if (currentElt.GetType() == typeof(Ellipse))
                    {
                        Ellipse shape = (Ellipse)currentElt;
                        properties.Add(new PropertyData() { Property = "Width", Value = shape.Width.ToString() });
                        properties.Add(new PropertyData() { Property = "Height", Value = shape.Height.ToString() });
                        properties.Add(new PropertyData() { Property = "Fill", Value = ColorName(shape.Fill) });
                        properties.Add(new PropertyData() { Property = "Stroke", Value = ColorName(shape.Stroke) });
                        properties.Add(new PropertyData() { Property = "StrokeThickness", Value = shape.StrokeThickness.ToString() });
                    }
                    else if (currentElt.GetType() == typeof(Polygon))
                    {
                        Polygon shape = (Polygon)currentElt;
                        for (int i = 0; i < shape.Points.Count; i++)
                        {
                            properties.Add(new PropertyData() { Property = "X" + (i + 1).ToString(), Value = shape.Points[i].X.ToString() });
                            properties.Add(new PropertyData() { Property = "Y" + (i + 1).ToString(), Value = shape.Points[i].Y.ToString() });
                        }
                        properties.Add(new PropertyData() { Property = "Fill", Value = ColorName(shape.Fill) });
                        properties.Add(new PropertyData() { Property = "Stroke", Value = ColorName(shape.Stroke) });
                        properties.Add(new PropertyData() { Property = "StrokeThickness", Value = shape.StrokeThickness.ToString() });
                    }
                    else if (currentElt.GetType() == typeof(Line))
                    {
                        Line shape = (Line)currentElt;
                        properties.Add(new PropertyData() { Property = "X1", Value = shape.X1.ToString() });
                        properties.Add(new PropertyData() { Property = "Y1", Value = shape.Y1.ToString() });
                        properties.Add(new PropertyData() { Property = "X2", Value = shape.X2.ToString() });
                        properties.Add(new PropertyData() { Property = "Y2", Value = shape.Y2.ToString() });
                        properties.Add(new PropertyData() { Property = "Stroke", Value = ColorName(shape.Stroke) });
                        properties.Add(new PropertyData() { Property = "StrokeThickness", Value = shape.StrokeThickness.ToString() });
                    }
                    else if (currentElt.GetType() == typeof(TextBlock))
                    {
                        TextBlock shape = (TextBlock)currentElt;
                        properties.Add(new PropertyData() { Property = "Text", Value = shape.Text });
                        properties.Add(new PropertyData() { Property = "Foreground", Value = shape.Foreground.ToString() });
                        properties.Add(new PropertyData() { Property = "FontFamily", Value = shape.FontFamily.ToString() });
                        properties.Add(new PropertyData() { Property = "FontStyle", Value = shape.FontStyle.ToString() });
                        properties.Add(new PropertyData() { Property = "FontSize", Value = shape.FontSize.ToString() });
                        properties.Add(new PropertyData() { Property = "FontWeight", Value = shape.FontWeight.ToString() });
                    }
                    else if (currentElt.GetType() == typeof(Image))
                    {
                        Image shape = (Image)currentElt;
                        properties.Add(new PropertyData() { Property = "Source", Value = shape.Source.ToString() });
                    }
                    else if (currentElt.GetType() == typeof(Button))
                    {
                        Button shape = (Button)currentElt;
                        properties.Add(new PropertyData() { Property = "Content", Value = shape.Content.ToString() });
                        properties.Add(new PropertyData() { Property = "Foreground", Value = ColorName(shape.Foreground) });
                        properties.Add(new PropertyData() { Property = "FontFamily", Value = shape.FontFamily.ToString() });
                        properties.Add(new PropertyData() { Property = "FontStyle", Value = shape.FontStyle.ToString() });
                        properties.Add(new PropertyData() { Property = "FontSize", Value = shape.FontSize.ToString() });
                        properties.Add(new PropertyData() { Property = "FontWeight", Value = shape.FontWeight.ToString() });
                    }
                    else if (currentElt.GetType() == typeof(TextBox))
                    {
                        TextBox shape = (TextBox)currentElt;
                        properties.Add(new PropertyData() { Property = "Text", Value = shape.Text.ToString() });
                        properties.Add(new PropertyData() { Property = "Foreground", Value = ColorName(shape.Foreground) });
                        properties.Add(new PropertyData() { Property = "FontFamily", Value = shape.FontFamily.ToString() });
                        properties.Add(new PropertyData() { Property = "FontStyle", Value = shape.FontStyle.ToString() });
                        properties.Add(new PropertyData() { Property = "FontSize", Value = shape.FontSize.ToString() });
                        properties.Add(new PropertyData() { Property = "FontWeight", Value = shape.FontWeight.ToString() });
                    }
                }
                dataGridProperties.Items.Refresh();
            }
            catch
            {

            }
        }

        private void ShowModifiers()
        {
            try
            {
                modifiers.Clear();
                if (null != currentShape)
                {
                    if (currentElt.GetType() == typeof(TextBlock) || currentElt.GetType() == typeof(Image) || currentElt.GetType() == typeof(Button) || currentElt.GetType() == typeof(TextBox))
                    {
                        currentElt.Measure(new Size(double.MaxValue, double.MaxValue));
                        currentShape.modifiers["Width"] = currentElt.DesiredSize.Width.ToString();
                        currentShape.modifiers["Height"] = currentElt.DesiredSize.Height.ToString();
                    }
                    Point point = currentShape.shape.TranslatePoint(new Point(0, 0), canvas);
                    currentShape.modifiers["Left"] = (point.X + Shape.HandleShort).ToString();
                    currentShape.modifiers["Top"] = (point.Y + Shape.HandleShort).ToString();
                    if (!currentShape.modifiers.ContainsKey("Angle")) currentShape.modifiers["Angle"] = "0";
                    if (!currentShape.modifiers.ContainsKey("Opacity")) currentShape.modifiers["Opacity"] = "100";

                    foreach (KeyValuePair<string, string> kvp in currentShape.modifiers)
                    {
                        modifiers.Add(new PropertyData() { Property = kvp.Key, Value = kvp.Value });
                    }
                }
                dataGridModifiers.Items.Refresh();
            }
            catch
            {

            }
        }

        private void ShowCode()
        {
            sbDocument.TextArea.Text = "";
            sbDocument.TextArea.Text += "Init()\n\nSub Init\n\n";

            Brush _brush = brush;
            Pen _pen = pen;
            FontFamily _fontFamily = fontFamily;
            FontStyle _fontStyle = fontStyle;
            double _fontSize = fontSize;
            FontWeight _fontWeight = fontWeight;

            foreach (FrameworkElement child in canvas.Children)
            {
                if (child.GetType() == typeof(Grid))
                {
                    Grid grid = (Grid)child;
                    foreach (FrameworkElement elt in grid.Children)
                    {
                        if (null != elt.Tag)
                        {
                            Shape shape = (Shape)elt.Tag;

                            if (elt.GetType() == typeof(Rectangle))
                            {
                                Rectangle obj = (Rectangle)elt;
                                if (obj.Fill.ToString() != _brush.ToString())
                                {
                                    _brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(obj.Fill.ToString()));
                                    sbDocument.TextArea.Text += "GraphicsWindow.BrushColor = \"" + ColorName(_brush) + "\"\n";
                                }
                                if (obj.Stroke.ToString() != _pen.Brush.ToString())
                                {
                                    _pen.Brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(obj.Stroke.ToString()));
                                    sbDocument.TextArea.Text += "GraphicsWindow.PenColor = \"" + ColorName(_pen.Brush) + "\"\n";
                                }
                                if (obj.StrokeThickness.ToString() != _pen.Thickness.ToString())
                                {
                                    _pen.Thickness = obj.StrokeThickness;
                                    sbDocument.TextArea.Text += "GraphicsWindow.PenWidth = " + _pen.Thickness.ToString() + "\n";
                                }
                                sbDocument.TextArea.Text += obj.Name + " = Shapes.AddRectangle(" + Fix(obj.Width) + "," + Fix(obj.Height) + ")\n";
                                sbDocument.TextArea.Text += "Shapes.Move(" + obj.Name + "," + Fix(shape.modifiers["Left"]) + "," + Fix(shape.modifiers["Top"]) + ")\n";
                                if (shape.modifiers["Opacity"] != "100") sbDocument.TextArea.Text += "Shapes.SetOpacity(" + obj.Name + "," + Fix(shape.modifiers["Opacity"]) + ")\n";
                                if (shape.modifiers["Angle"] != "0") sbDocument.TextArea.Text += "Shapes.Rotate(" + obj.Name + "," + Fix(shape.modifiers["Angle"]) + ")\n";
                                sbDocument.TextArea.Text += "\n";
                            }
                            else if (elt.GetType() == typeof(Ellipse))
                            {
                                Ellipse obj = (Ellipse)elt;
                                if (obj.Fill.ToString() != _brush.ToString())
                                {
                                    _brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(obj.Fill.ToString()));
                                    sbDocument.TextArea.Text += "GraphicsWindow.BrushColor = \"" + ColorName(_brush) + "\"\n";
                                }
                                if (obj.Stroke.ToString() != _pen.Brush.ToString())
                                {
                                    _pen.Brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(obj.Stroke.ToString()));
                                    sbDocument.TextArea.Text += "GraphicsWindow.PenColor = \"" + ColorName(_pen.Brush) + "\"\n";
                                }
                                if (obj.StrokeThickness.ToString() != _pen.Thickness.ToString())
                                {
                                    _pen.Thickness = obj.StrokeThickness;
                                    sbDocument.TextArea.Text += "GraphicsWindow.PenWidth = " + _pen.Thickness.ToString() + "\n";
                                }
                                sbDocument.TextArea.Text += obj.Name + " = Shapes.AddEllipse(" + Fix(obj.Width) + "," + Fix(obj.Height) + ")\n";
                                sbDocument.TextArea.Text += "Shapes.Move(" + obj.Name + "," + Fix(shape.modifiers["Left"]) + "," + Fix(shape.modifiers["Top"]) + ")\n";
                                if (shape.modifiers["Opacity"] != "100") sbDocument.TextArea.Text += "Shapes.SetOpacity(" + obj.Name + "," + Fix(shape.modifiers["Opacity"]) + ")\n";
                                if (shape.modifiers["Angle"] != "0") sbDocument.TextArea.Text += "Shapes.Rotate(" + obj.Name + "," + (shape.modifiers["Angle"]) + ")\n";
                                sbDocument.TextArea.Text += "\n";
                            }
                            else if (elt.GetType() == typeof(Polygon))
                            {
                                Polygon obj = (Polygon)elt;
                                if (obj.Fill.ToString() != _brush.ToString())
                                {
                                    _brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(obj.Fill.ToString()));
                                    sbDocument.TextArea.Text += "GraphicsWindow.BrushColor = \"" + ColorName(_brush) + "\"\n";
                                }
                                if (obj.Stroke.ToString() != _pen.Brush.ToString())
                                {
                                    _pen.Brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(obj.Stroke.ToString()));
                                    sbDocument.TextArea.Text += "GraphicsWindow.PenColor = \"" + ColorName(_pen.Brush) + "\"\n";
                                }
                                if (obj.StrokeThickness.ToString() != _pen.Thickness.ToString())
                                {
                                    _pen.Thickness = obj.StrokeThickness;
                                    sbDocument.TextArea.Text += "GraphicsWindow.PenWidth = " + _pen.Thickness.ToString() + "\n";
                                }
                                if (obj.Points.Count == 3)
                                {
                                    sbDocument.TextArea.Text += obj.Name + " = Shapes.AddTriangle(" + Fix(obj.Points[0].X) + "," + Fix(obj.Points[0].Y) + "," + Fix(obj.Points[1].X) + "," + Fix(obj.Points[1].Y) + "," + Fix(obj.Points[2].X) + "," + Fix(obj.Points[2].Y) + ")\n";
                                }
                                sbDocument.TextArea.Text += "Shapes.Move(" + obj.Name + "," + Fix(shape.modifiers["Left"]) + "," + Fix(shape.modifiers["Top"]) + ")\n";
                                if (shape.modifiers["Opacity"] != "100") sbDocument.TextArea.Text += "Shapes.SetOpacity(" + obj.Name + "," + Fix(shape.modifiers["Opacity"]) + ")\n";
                                if (shape.modifiers["Angle"] != "0") sbDocument.TextArea.Text += "Shapes.Rotate(" + obj.Name + "," + Fix(shape.modifiers["Angle"]) + ")\n";
                                sbDocument.TextArea.Text += "\n";
                            }
                            else if (elt.GetType() == typeof(Line))
                            {
                                Line obj = (Line)elt;
                                if (obj.Stroke.ToString() != _pen.Brush.ToString())
                                {
                                    _pen.Brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(obj.Stroke.ToString()));
                                    sbDocument.TextArea.Text += "GraphicsWindow.PenColor = \"" + ColorName(_pen.Brush) + "\"\n";
                                }
                                if (obj.StrokeThickness.ToString() != _pen.Thickness.ToString())
                                {
                                    _pen.Thickness = obj.StrokeThickness;
                                    sbDocument.TextArea.Text += "GraphicsWindow.PenWidth = " + _pen.Thickness.ToString() + "\n";
                                }
                                sbDocument.TextArea.Text += obj.Name + " = Shapes.AddLine(" + Fix(obj.X1) + "," + Fix(obj.Y1) + "," + Fix(obj.X2) + "," + Fix(obj.Y2) + ")\n";
                                sbDocument.TextArea.Text += "Shapes.Move(" + obj.Name + "," + Fix(shape.modifiers["Left"]) + "," + Fix(shape.modifiers["Top"]) + ")\n";
                                if (shape.modifiers["Opacity"] != "100") sbDocument.TextArea.Text += "Shapes.SetOpacity(" + obj.Name + "," + Fix(shape.modifiers["Opacity"]) + ")\n";
                                if (shape.modifiers["Angle"] != "0") sbDocument.TextArea.Text += "Shapes.Rotate(" + obj.Name + "," + Fix(shape.modifiers["Angle"]) + ")\n";
                                sbDocument.TextArea.Text += "\n";
                            }
                            else if (elt.GetType() == typeof(TextBlock))
                            {
                                TextBlock obj = (TextBlock)elt;
                                if (obj.Foreground.ToString() != _brush.ToString())
                                {
                                    _brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(obj.Foreground.ToString()));
                                    sbDocument.TextArea.Text += "GraphicsWindow.BrushColor = \"" + ColorName(_brush) + "\"\n";
                                }
                                if (obj.FontFamily.ToString() != _fontFamily.ToString())
                                {
                                    _fontFamily = new FontFamily(obj.FontFamily.ToString());
                                    sbDocument.TextArea.Text += "GraphicsWindow.FontName = \"" + _fontFamily.ToString() + "\"\n";
                                }
                                if (obj.FontStyle.ToString() != _fontStyle.ToString())
                                {
                                    _fontStyle = obj.FontStyle;
                                    sbDocument.TextArea.Text += "GraphicsWindow.FontItalic = \"" + (_fontStyle == FontStyles.Italic ? "True" : "False") + "\"\n";
                                }
                                if (obj.FontSize.ToString() != _fontSize.ToString())
                                {
                                    _fontSize = obj.FontSize;
                                    sbDocument.TextArea.Text += "GraphicsWindow.FontSize = " + _fontSize + "\n";
                                }
                                if (obj.FontWeight.ToString() != _fontWeight.ToString())
                                {
                                    _fontWeight = obj.FontWeight;
                                    sbDocument.TextArea.Text += "GraphicsWindow.FontBold = \"" + (_fontWeight == FontWeights.Bold ? "True" : "False") + "\"\n";
                                }
                                sbDocument.TextArea.Text += obj.Name + " = Shapes.AddText(\"" + obj.Text + "\")\n";
                                sbDocument.TextArea.Text += "Controls.SetSize(" + obj.Name + "," + Fix(shape.modifiers["Width"]) + "," + Fix(shape.modifiers["Height"]) + ")\n";
                                sbDocument.TextArea.Text += "Shapes.Move(" + obj.Name + "," + Fix(shape.modifiers["Left"]) + "," + Fix(shape.modifiers["Top"]) + ")\n";
                                if (shape.modifiers["Opacity"] != "100") sbDocument.TextArea.Text += "Shapes.SetOpacity(" + obj.Name + "," + Fix(shape.modifiers["Opacity"]) + ")\n";
                                if (shape.modifiers["Angle"] != "0") sbDocument.TextArea.Text += "Shapes.Rotate(" + obj.Name + "," + Fix(shape.modifiers["Angle"]) + ")\n";
                                sbDocument.TextArea.Text += "\n";
                            }
                            else if (elt.GetType() == typeof(Image))
                            {
                                Image obj = (Image)elt;
                                sbDocument.TextArea.Text += obj.Name + " = Shapes.AddImage(\"" + obj.Source.ToString() + "\")\n";
                                sbDocument.TextArea.Text += "Controls.SetSize(" + obj.Name + "," + Fix(shape.modifiers["Width"]) + "," + Fix(shape.modifiers["Height"]) + ")\n";
                                sbDocument.TextArea.Text += "Shapes.Move(" + obj.Name + "," + Fix(shape.modifiers["Left"]) + "," + Fix(shape.modifiers["Top"]) + ")\n";
                                if (shape.modifiers["Opacity"] != "100") sbDocument.TextArea.Text += "Shapes.SetOpacity(" + obj.Name + "," + Fix(shape.modifiers["Opacity"]) + ")\n";
                                if (shape.modifiers["Angle"] != "0") sbDocument.TextArea.Text += "Shapes.Rotate(" + obj.Name + "," + Fix(shape.modifiers["Angle"]) + ")\n";
                                sbDocument.TextArea.Text += "\n";
                            }
                            else if (elt.GetType() == typeof(Button))
                            {
                                Button obj = (Button)elt;
                                if (obj.Foreground.ToString() != _brush.ToString())
                                {
                                    _brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(obj.Foreground.ToString()));
                                    sbDocument.TextArea.Text += "GraphicsWindow.BrushColor = \"" + ColorName(_brush) + "\"\n";
                                }
                                if (obj.FontFamily.ToString() != _fontFamily.ToString())
                                {
                                    _fontFamily = new FontFamily(obj.FontFamily.ToString());
                                    sbDocument.TextArea.Text += "GraphicsWindow.FontName = \"" + _fontFamily.ToString() + "\"\n";
                                }
                                if (obj.FontStyle.ToString() != _fontStyle.ToString())
                                {
                                    _fontStyle = obj.FontStyle;
                                    sbDocument.TextArea.Text += "GraphicsWindow.FontItalic = \"" + (_fontStyle == FontStyles.Italic ? "True" : "False") + "\"\n";
                                }
                                if (obj.FontSize.ToString() != _fontSize.ToString())
                                {
                                    _fontSize = obj.FontSize;
                                    sbDocument.TextArea.Text += "GraphicsWindow.FontSize = " + _fontSize + "\n";
                                }
                                if (obj.FontWeight.ToString() != _fontWeight.ToString())
                                {
                                    _fontWeight = obj.FontWeight;
                                    sbDocument.TextArea.Text += "GraphicsWindow.FontBold = \"" + (_fontWeight == FontWeights.Bold ? "True" : "False") + "\"\n";
                                }
                                sbDocument.TextArea.Text += obj.Name + " = Controls.AddButton(\"" + obj.Content + "\"," + Fix(shape.modifiers["Left"]) + "," + Fix(shape.modifiers["Top"]) + ")\n";
                                sbDocument.TextArea.Text += "Controls.SetSize(" + obj.Name + "," + Fix(shape.modifiers["Width"]) + "," + Fix(shape.modifiers["Height"]) + ")\n";
                                if (shape.modifiers["Opacity"] != "100") sbDocument.TextArea.Text += "Shapes.SetOpacity(" + obj.Name + "," + Fix(shape.modifiers["Opacity"]) + ")\n";
                                if (shape.modifiers["Angle"] != "0") sbDocument.TextArea.Text += "Shapes.Rotate(" + obj.Name + "," + Fix(shape.modifiers["Angle"]) + ")\n";
                                sbDocument.TextArea.Text += "\n";
                            }
                            else if (elt.GetType() == typeof(TextBox))
                            {
                                TextBox obj = (TextBox)elt;
                                if (obj.Foreground.ToString() != _brush.ToString())
                                {
                                    _brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(obj.Foreground.ToString()));
                                    sbDocument.TextArea.Text += "GraphicsWindow.BrushColor = \"" + ColorName(_brush) + "\"\n";
                                }
                                if (obj.FontFamily.ToString() != _fontFamily.ToString())
                                {
                                    _fontFamily = new FontFamily(obj.FontFamily.ToString());
                                    sbDocument.TextArea.Text += "GraphicsWindow.FontName = \"" + _fontFamily.ToString() + "\"\n";
                                }
                                if (obj.FontStyle.ToString() != _fontStyle.ToString())
                                {
                                    _fontStyle = obj.FontStyle;
                                    sbDocument.TextArea.Text += "GraphicsWindow.FontItalic = \"" + (_fontStyle == FontStyles.Italic ? "True" : "False") + "\"\n";
                                }
                                if (obj.FontSize.ToString() != _fontSize.ToString())
                                {
                                    _fontSize = obj.FontSize;
                                    sbDocument.TextArea.Text += "GraphicsWindow.FontSize = " + _fontSize + "\n";
                                }
                                if (obj.FontWeight.ToString() != _fontWeight.ToString())
                                {
                                    _fontWeight = obj.FontWeight;
                                    sbDocument.TextArea.Text += "GraphicsWindow.FontBold = \"" + (_fontWeight == FontWeights.Bold ? "True" : "False") + "\"\n";
                                }
                                if (obj.AcceptsReturn)
                                {
                                    sbDocument.TextArea.Text += obj.Name + " = Controls.AddMultiLineTextBox(" + Fix(shape.modifiers["Left"]) + "," + Fix(shape.modifiers["Top"]) + ")\n";
                                }
                                else
                                {
                                    sbDocument.TextArea.Text += obj.Name + " = Controls.AddTextBox(" + Fix(shape.modifiers["Left"]) + "," + Fix(shape.modifiers["Top"]) + ")\n";
                                }
                                sbDocument.TextArea.Text += "Controls.SetTextBoxText(" + obj.Name + ",\"" + obj.Text + "\")\n";
                                sbDocument.TextArea.Text += "Controls.SetSize(" + obj.Name + "," + Fix(shape.modifiers["Width"]) + "," + Fix(shape.modifiers["Height"]) + ")\n";
                                if (shape.modifiers["Opacity"] != "100") sbDocument.TextArea.Text += "Shapes.SetOpacity(" + obj.Name + "," + Fix(shape.modifiers["Opacity"]) + ")\n";
                                if (shape.modifiers["Angle"] != "0") sbDocument.TextArea.Text += "Shapes.Rotate(" + obj.Name + "," + Fix(shape.modifiers["Angle"]) + ")\n";
                                sbDocument.TextArea.Text += "\n";
                            }
                        }
                    }
                }
            }

            sbDocument.TextArea.Text += "EndSub\n";
            sbDocument.Lexer.Format();
        }

        private string Fix(string value)
        {
            return (fixDec * Math.Round(double.Parse(value) / fixDec)).ToString();
        }

        private string Fix(double value)
        {
            return (fixDec * Math.Round(value / fixDec)).ToString();
        }

        private void ReadCode()
        {
            try
            {
                Brush _brush = brush;
                Pen _pen = pen;
                FontFamily _fontFamily = fontFamily;
                FontStyle _fontStyle = fontStyle;
                double _fontSize = fontSize;
                FontWeight _fontWeight = fontWeight;

                FrameworkElement elt = null;
                Shape shape = null;
                string name;
                double[] value = new double[10];

                foreach (ScintillaNET.Line line in sbDocument.TextArea.Lines)
                {
                    string code = line.Text.ToLower().Trim();
                    if (code.Contains("shapes.addrectangle"))
                    {
                        string[] parts = code.Split(new char[] { '(', ')', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        double.TryParse(parts[1], out value[0]);
                        double.TryParse(parts[2], out value[1]);
                        name = GetName("Rectangle");
                        elt = new Rectangle()
                        {
                            Name = name,
                            Width = value[0],
                            Height = value[1],
                            Fill = _brush,
                            Stroke = _pen.Brush,
                            StrokeThickness = _pen.Thickness,
                        };
                        elt.PreviewMouseDown += new MouseButtonEventHandler(eltPreviewMouseDown);
                        shape = new Shape(elt);
                        canvas.Children.Add(shape.shape);
                        Canvas.SetLeft(shape.shape, -Shape.HandleShort);
                        Canvas.SetTop(shape.shape, -Shape.HandleShort);
                    }
                    else if (code.Contains("shapes.addellipse"))
                    {
                        string[] parts = code.Split(new char[] { '(', ')', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        double.TryParse(parts[1], out value[0]);
                        double.TryParse(parts[2], out value[1]);
                        name = GetName("Ellipse");
                        elt = new Ellipse()
                        {
                            Name = name,
                            Width = value[0],
                            Height = value[1],
                            Fill = _brush,
                            Stroke = _pen.Brush,
                            StrokeThickness = _pen.Thickness,
                        };
                        elt.PreviewMouseDown += new MouseButtonEventHandler(eltPreviewMouseDown);
                        shape = new Shape(elt);
                        canvas.Children.Add(shape.shape);
                        Canvas.SetLeft(shape.shape, -Shape.HandleShort);
                        Canvas.SetTop(shape.shape, -Shape.HandleShort);
                    }
                    else if (code.Contains("shapes.addtriangle"))
                    {
                        string[] parts = code.Split(new char[] { '(', ')', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        double.TryParse(parts[1], out value[0]);
                        double.TryParse(parts[2], out value[1]);
                        double.TryParse(parts[3], out value[2]);
                        double.TryParse(parts[4], out value[3]);
                        double.TryParse(parts[5], out value[4]);
                        double.TryParse(parts[6], out value[5]);
                        name = GetName("Triangle");
                        elt = new Polygon()
                        {
                            Name = name,
                            Points = new PointCollection() { new Point(value[0], value[1]), new Point(value[2], value[3]), new Point(value[4], value[5]) },
                            Fill = _brush,
                            Stroke = _pen.Brush,
                            StrokeThickness = _pen.Thickness,
                        };
                        elt.PreviewMouseDown += new MouseButtonEventHandler(eltPreviewMouseDown);
                        shape = new Shape(elt);
                        canvas.Children.Add(shape.shape);
                        Canvas.SetLeft(shape.shape, -Shape.HandleShort);
                        Canvas.SetTop(shape.shape, -Shape.HandleShort);
                    }
                    else if (code.Contains("shapes.addline"))
                    {
                        string[] parts = code.Split(new char[] { '(', ')', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        double.TryParse(parts[1], out value[0]);
                        double.TryParse(parts[2], out value[1]);
                        double.TryParse(parts[3], out value[2]);
                        double.TryParse(parts[4], out value[3]);
                        name = GetName("Line");
                        elt = new Line()
                        {
                            Name = name,
                            X1 = value[0],
                            Y1 = value[1],
                            X2 = value[2],
                            Y2 = value[3],
                            Stroke = _pen.Brush,
                            StrokeThickness = _pen.Thickness,
                        };
                        elt.PreviewMouseDown += new MouseButtonEventHandler(eltPreviewMouseDown);
                        shape = new Shape(elt);
                        canvas.Children.Add(shape.shape);
                        Canvas.SetLeft(shape.shape, -Shape.HandleShort);
                        Canvas.SetTop(shape.shape, -Shape.HandleShort);
                    }
                    else if (code.Contains("shapes.addtext"))
                    {
                        string[] parts = code.Split(new char[] { '(', ')', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        name = GetName("Text");
                        elt = new TextBlock()
                        {
                            Name = name,
                            Text = parts[1].Replace("\"", ""),
                            Foreground = _brush,
                            FontFamily = _fontFamily,
                            FontStyle = _fontStyle,
                            FontSize = _fontSize,
                            FontWeight = _fontWeight,
                        };
                        elt.PreviewMouseDown += new MouseButtonEventHandler(eltPreviewMouseDown);
                        shape = new Shape(elt);
                        canvas.Children.Add(shape.shape);
                        Canvas.SetLeft(shape.shape, -Shape.HandleShort);
                        Canvas.SetTop(shape.shape, -Shape.HandleShort);
                    }
                    else if (code.Contains("shapes.addimage"))
                    {
                        string[] parts = code.Split(new char[] { '(', ')', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        name = GetName("Image");
                        ImageSource bm;
                        try
                        {
                            bm = new BitmapImage(new Uri(parts[1].Replace("\"", "")));
                        }
                        catch (Exception ex)
                        {
                            bm = MainWindow.ImageSourceFromBitmap(Properties.Resources.No_image);
                        }
                        elt = new Image()
                        {
                            Name = name,
                            Width = bm.Width,
                            Height = bm.Height,
                            Source = bm,
                            Stretch = Stretch.Fill,
                        };
                        elt.PreviewMouseDown += new MouseButtonEventHandler(eltPreviewMouseDown);
                        shape = new Shape(elt);
                        canvas.Children.Add(shape.shape);
                        Canvas.SetLeft(shape.shape, -Shape.HandleShort);
                        Canvas.SetTop(shape.shape, -Shape.HandleShort);
                    }
                    else if (code.Contains("controls.addbutton"))
                    {
                        string[] parts = code.Split(new char[] { '(', ')', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        double.TryParse(parts[2], out value[0]);
                        double.TryParse(parts[3], out value[1]);
                        name = GetName("Button");
                        elt = new Button()
                        {
                            Name = name,
                            Content = parts[1].Replace("\"", ""),
                            Foreground = _brush,
                            FontFamily = _fontFamily,
                            FontStyle = _fontStyle,
                            FontSize = _fontSize,
                            FontWeight = _fontWeight,
                            Padding = new Thickness(4.0),
                        };
                        elt.PreviewMouseDown += new MouseButtonEventHandler(eltPreviewMouseDown);
                        shape = new Shape(elt);
                        canvas.Children.Add(shape.shape);
                        Canvas.SetLeft(shape.shape, value[0] - Shape.HandleShort);
                        Canvas.SetTop(shape.shape, value[1] - Shape.HandleShort);
                    }
                    else if (code.Contains("controls.addtextbox"))
                    {
                        string[] parts = code.Split(new char[] { '(', ')', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        double.TryParse(parts[1], out value[0]);
                        double.TryParse(parts[2], out value[1]);
                        name = GetName("TextBox");
                        elt = new TextBox()
                        {
                            Name = name,
                            Width = 160,
                            Text = name,
                            Foreground = _brush,
                            FontFamily = _fontFamily,
                            FontStyle = _fontStyle,
                            FontSize = _fontSize,
                            FontWeight = _fontWeight,
                            Padding = new Thickness(2.0),
                        };
                        elt.PreviewMouseDown += new MouseButtonEventHandler(eltPreviewMouseDown);
                        shape = new Shape(elt);
                        canvas.Children.Add(shape.shape);
                        Canvas.SetLeft(shape.shape, value[0] - Shape.HandleShort);
                        Canvas.SetTop(shape.shape, value[1] - Shape.HandleShort);
                    }
                    else if (code.Contains("controls.addmultilinetextbox"))
                    {
                        string[] parts = code.Split(new char[] { '(', ')', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        double.TryParse(parts[1], out value[0]);
                        double.TryParse(parts[2], out value[1]);
                        name = GetName("MultiLineTextBox");
                        elt = new TextBox()
                        {
                            Name = name,
                            Width = 200,
                            Height = 80,
                            Text = name,
                            Foreground = _brush,
                            FontFamily = _fontFamily,
                            FontStyle = _fontStyle,
                            FontSize = _fontSize,
                            FontWeight = _fontWeight,
                            Padding = new Thickness(2.0),
                            AcceptsReturn = true,
                            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                        };
                        elt.PreviewMouseDown += new MouseButtonEventHandler(eltPreviewMouseDown);
                        shape = new Shape(elt);
                        canvas.Children.Add(shape.shape);
                        Canvas.SetLeft(shape.shape, value[0] - Shape.HandleShort);
                        Canvas.SetTop(shape.shape, value[1] - Shape.HandleShort);
                    }
                    else if (code.Contains("graphicswindow.brushcolor"))
                    {
                        string[] parts = code.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
                        _brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(parts[1].Replace("\"", "")));
                    }
                    else if (code.Contains("graphicswindow.pencolor"))
                    {
                        string[] parts = code.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
                        _pen.Brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(parts[1].Replace("\"", "")));
                    }
                    else if (code.Contains("graphicswindow.penwidth"))
                    {
                        string[] parts = code.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
                        double.TryParse(parts[1], out value[0]);
                        _pen.Thickness = value[0];
                    }
                    else if (code.Contains("graphicswindow.fontname"))
                    {
                        string[] parts = code.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
                        _fontFamily = new FontFamily(parts[1].Replace("\"", ""));
                    }
                    else if (code.Contains("graphicswindow.fontitalic"))
                    {
                        string[] parts = code.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
                        _fontStyle = parts[1].Replace("\"", "") == "true" ? FontStyles.Italic : FontStyles.Normal;
                    }
                    else if (code.Contains("graphicswindow.fontbold"))
                    {
                        string[] parts = code.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
                        _fontWeight = parts[1].Replace("\"", "") == "true" ? FontWeights.Bold : FontWeights.Normal;
                    }
                    else if (code.Contains("graphicswindow.fontsize"))
                    {
                        string[] parts = code.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
                        double.TryParse(parts[1], out value[0]);
                        _fontSize = value[0];
                    }
                    else if (code.Contains("shapes.move"))
                    {
                        string[] parts = code.Split(new char[] { '(', ')', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        shape.modifiers["Left"] = parts[2];
                        shape.modifiers["Top"] = parts[3];
                        Canvas.SetLeft(shape.shape, double.Parse(parts[2]) - Shape.HandleShort);
                        Canvas.SetTop(shape.shape, double.Parse(parts[3]) - Shape.HandleShort);
                    }
                    else if (code.Contains("controls.setsize"))
                    {
                        string[] parts = code.Split(new char[] { '(', ')', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        shape.modifiers["Width"] = parts[2];
                        shape.modifiers["Height"] = parts[3];
                        shape.elt.Width = double.Parse(parts[2]);
                        shape.elt.Height = double.Parse(parts[3]);
                    }
                    else if (code.Contains("shapes.setopacity"))
                    {
                        string[] parts = code.Split(new char[] { '(', ')', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        shape.modifiers["Opacity"] = parts[2];
                        shape.elt.Opacity = double.Parse(parts[2]) / 100.0;
                    }
                    else if (code.Contains("shapes.rotate"))
                    {
                        string[] parts = code.Split(new char[] { '(', ')', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        shape.modifiers["Angle"] = parts[2];
                        RotateTransform rotateTransform = new RotateTransform();
                        shape.elt.Measure(new Size(double.MaxValue, double.MaxValue));
                        rotateTransform.CenterX = shape.elt.DesiredSize.Width / 2.0;
                        rotateTransform.CenterY = shape.elt.DesiredSize.Height / 2.0;
                        rotateTransform.Angle = double.Parse(parts[2]);
                        shape.elt.RenderTransform = new TransformGroup();
                        ((TransformGroup)shape.elt.RenderTransform).Children.Add(rotateTransform);
                    }
                    if (null != shape)
                    {
                        if (shape.elt.GetType() == typeof(TextBlock) || shape.elt.GetType() == typeof(Image) || shape.elt.GetType() == typeof(Button) || shape.elt.GetType() == typeof(TextBox))
                        {
                            shape.elt.Measure(new Size(double.MaxValue, double.MaxValue));
                            if (!shape.modifiers.ContainsKey("Width")) shape.modifiers["Width"] = shape.elt.DesiredSize.Width.ToString();
                            if (!shape.modifiers.ContainsKey("Height")) shape.modifiers["Height"] = shape.elt.DesiredSize.Height.ToString();
                        }
                        if (!shape.modifiers.ContainsKey("Left")) shape.modifiers["Left"] = "0";
                        if (!shape.modifiers.ContainsKey("Top")) shape.modifiers["Top"] = "0";
                        if (!shape.modifiers.ContainsKey("Angle")) shape.modifiers["Angle"] = "0";
                        if (!shape.modifiers.ContainsKey("Opacity")) shape.modifiers["Opacity"] = "100";
                    }
                }
                canvas.UpdateLayout();
            }
            catch
            {

            }
        }

        private class Shape
        {
            public static int HandleShort = 5;

            public Grid grid;
            public FrameworkElement shape;
            public FrameworkElement elt;
            public Dictionary<string, string> modifiers = new Dictionary<string, string>();

            private int handleLong = 2 * HandleShort;
            private Rectangle handleTL = null;
            private Rectangle handleTR = null;
            private Rectangle handleBL = null;
            private Rectangle handleBR = null;
            private Rectangle handleL = null;
            private Rectangle handleR = null;
            private Rectangle handleT = null;
            private Rectangle handleB = null;

            public Shape(FrameworkElement elt)
            {
                elt.Tag = this;
                elt.Cursor = Cursors.Hand;
                elt.MouseDown += new MouseButtonEventHandler(OnMouseDown);
                this.elt = elt;

                grid = new Grid();
                grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(HandleShort) });
                grid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(HandleShort) });
                grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(HandleShort) });
                grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(HandleShort) });

                grid.Children.Add(elt);
                Grid.SetRow(elt, 1);
                Grid.SetColumn(elt, 1);

                handleTL = GetHandle(HandleShort, HandleShort, "TL");
                grid.Children.Add(handleTL);
                Grid.SetRow(handleTL, 0);
                Grid.SetColumn(handleTL, 0);

                handleTR = GetHandle(HandleShort, HandleShort, "TR");
                grid.Children.Add(handleTR);
                Grid.SetRow(handleTR, 0);
                Grid.SetColumn(handleTR, 2);

                handleBL = GetHandle(HandleShort, HandleShort, "BL");
                grid.Children.Add(handleBL);
                Grid.SetRow(handleBL, 2);
                Grid.SetColumn(handleBL, 0);

                handleBR = GetHandle(HandleShort, HandleShort, "BR");
                grid.Children.Add(handleBR);
                Grid.SetRow(handleBR, 2);
                Grid.SetColumn(handleBR, 2);

                handleL = GetHandle(HandleShort, handleLong, "L");
                grid.Children.Add(handleL);
                Grid.SetRow(handleL, 1);
                Grid.SetColumn(handleL, 0);

                handleR = GetHandle(HandleShort, handleLong, "R");
                grid.Children.Add(handleR);
                Grid.SetRow(handleR, 1);
                Grid.SetColumn(handleR, 2);

                handleT = GetHandle(handleLong, HandleShort, "T");
                grid.Children.Add(handleT);
                Grid.SetRow(handleT, 0);
                Grid.SetColumn(handleT, 1);

                handleB = GetHandle(handleLong, HandleShort, "B");
                grid.Children.Add(handleB);
                Grid.SetRow(handleB, 2);
                Grid.SetColumn(handleB, 1);

                ShowHandles(false);
                shape = grid;
            }

            public void ShowHandles(bool bSet)
            {
                if (null != handleTL) handleTL.Visibility = bSet ? Visibility.Visible : Visibility.Hidden;
                if (null != handleTR) handleTR.Visibility = bSet ? Visibility.Visible : Visibility.Hidden;
                if (null != handleBL) handleBL.Visibility = bSet ? Visibility.Visible : Visibility.Hidden;
                if (null != handleBR) handleBR.Visibility = bSet ? Visibility.Visible : Visibility.Hidden;
                if (null != handleL) handleL.Visibility = bSet ? Visibility.Visible : Visibility.Hidden;
                if (null != handleR) handleR.Visibility = bSet ? Visibility.Visible : Visibility.Hidden;
                if (null != handleT) handleT.Visibility = bSet ? Visibility.Visible : Visibility.Hidden;
                if (null != handleB) handleB.Visibility = bSet ? Visibility.Visible : Visibility.Hidden;
            }

            private Rectangle GetHandle(int width, int height, string name)
            {
                Color color = ((SolidColorBrush)THIS.background).Color;
                color = Color.FromArgb(255, (byte)(255 - color.R), (byte)(255 - color.G), (byte)(255 - color.B));
                Rectangle handle = new Rectangle()
                {
                    Name = name,
                    Width = width,
                    Height = height,
                    Fill = Brushes.Transparent,
                    Stroke = new SolidColorBrush(color),
                    StrokeThickness = 1,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Cursor = Cursors.Cross,
                };
                handle.MouseDown += new MouseButtonEventHandler(OnMouseDown);
                return handle;
            }

            private void OnMouseDown(object sender, MouseButtonEventArgs e)
            {
                FrameworkElement elt = (FrameworkElement)sender;
                THIS.mode = elt.Name;
                THIS.Cursor = Cursors.Cross;
                THIS.SetStart(e.GetPosition(THIS.canvas));
            }
        }

        private class VisualContainer : FrameworkElement
        {
            private Drawing drawing;

            public VisualContainer(Drawing drawing)
            {
                this.drawing = drawing;
            }

            protected override void OnRender(DrawingContext drawingContext)
            {
                drawingContext.DrawDrawing(drawing);
            }
        }

        private class PropertyData
        {
            public string Property { get; set; }
            public string Value { get; set; }
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            Topmost = true;
            Activate();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Active = true;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Active = false;
        }

        private void AddShape_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ListBoxItem item = (ListBoxItem)sender;
            string label = item.Content.ToString();
            string name = GetName(label);

            FrameworkElement elt = null;
            switch (label)
            {
                case "Rectangle":
                    elt = new Rectangle()
                    {
                        Name = name,
                        Width = 100,
                        Height = 100,
                        Fill = brush,
                        Stroke = pen.Brush,
                        StrokeThickness = pen.Thickness,
                    };
                    break;
                case "Ellipse":
                    elt = new Ellipse()
                    {
                        Name = name,
                        Width = 100,
                        Height = 100,
                        Fill = brush,
                        Stroke = pen.Brush,
                        StrokeThickness = pen.Thickness,
                    };
                    break;
                case "Triangle":
                    elt = new Polygon()
                    {
                        Name = name,
                        Points = new PointCollection() { new Point(0, 100), new Point(100, 100), new Point(50, 0) },
                        Fill = brush,
                        Stroke = pen.Brush,
                        StrokeThickness = pen.Thickness,
                    };
                    break;
                case "Line":
                    elt = new Line()
                    {
                        Name = name,
                        X1 = 0,
                        Y1 = 0,
                        X2 = 100,
                        Y2 = 100,
                        Stroke = pen.Brush,
                        StrokeThickness = pen.Thickness,
                    };
                    break;
                case "Text":
                    elt = new TextBlock()
                    {
                        Name = name,
                        Text = label,
                        Foreground = brush,
                        FontFamily = fontFamily,
                        FontStyle = fontStyle,
                        FontSize = fontSize,
                        FontWeight = fontWeight,
                    };
                    break;
                case "Image":
                    ImageSource bm = MainWindow.ImageSourceFromBitmap(Properties.Resources.No_image);
                    elt = new Image()
                    {
                        Name = name,
                        Width = bm.Width,
                        Height = bm.Height,
                        Source = bm,
                        Stretch = Stretch.Fill,
                    };
                    break;
                case "Button":
                    elt = new Button()
                    {
                        Name = name,
                        Content = label,
                        Foreground = brush,
                        FontFamily = fontFamily,
                        FontStyle = fontStyle,
                        FontSize = fontSize,
                        FontWeight = fontWeight,
                        Padding = new Thickness(4.0),
                    };
                    break;
                case "TextBox":
                    elt = new TextBox()
                    {
                        Name = name,
                        Width = 160,
                        Text = label,
                        Foreground = brush,
                        FontFamily = fontFamily,
                        FontStyle = fontStyle,
                        FontSize = fontSize,
                        FontWeight = fontWeight,
                        Padding = new Thickness(2.0),
                    };
                    break;
                case "MultiLineTextBox":
                    elt = new TextBox()
                    {
                        Name = name,
                        Width = 200,
                        Height = 80,
                        Text = label,
                        Foreground = brush,
                        FontFamily = fontFamily,
                        FontStyle = fontStyle,
                        FontSize = fontSize,
                        FontWeight = fontWeight,
                        Padding = new Thickness(2.0),
                        AcceptsReturn = true,
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    };
                    break;
            }

            elt.PreviewMouseDown += new MouseButtonEventHandler(eltPreviewMouseDown);
            Shape shape = new Shape(elt);
            canvas.Children.Add(shape.shape);
            Canvas.SetLeft(shape.shape, 100 - Shape.HandleShort);
            Canvas.SetTop(shape.shape, 100 - Shape.HandleShort);

            eltPreviewMouseDown(elt, null);
        }

        private void dataGridProperties_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            try
            {
                PropertyData property = (PropertyData)e.Row.Item;
                TextBox tb = (TextBox)e.EditingElement;

                if (currentElt.GetType() == typeof(Rectangle))
                {
                    Rectangle shape = (Rectangle)currentElt;
                    switch (property.Property)
                    {
                        case "Name":
                            shape.Name = tb.Text;
                            break;
                        case "Width":
                            shape.Width = double.Parse(tb.Text);
                            break;
                        case "Height":
                            shape.Height = double.Parse(tb.Text);
                            break;
                        case "Fill":
                            shape.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(tb.Text));
                            break;
                        case "Stroke":
                            shape.Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString(tb.Text));
                            break;
                        case "StrokeThickness":
                            shape.StrokeThickness = double.Parse(tb.Text);
                            break;
                    }
                }
                else if (currentElt.GetType() == typeof(Ellipse))
                {
                    Ellipse shape = (Ellipse)currentElt;
                    switch (property.Property)
                    {
                        case "Name":
                            shape.Name = tb.Text;
                            break;
                        case "Width":
                            shape.Width = double.Parse(tb.Text);
                            break;
                        case "Height":
                            shape.Height = double.Parse(tb.Text);
                            break;
                        case "Fill":
                            shape.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(tb.Text));
                            break;
                        case "Stroke":
                            shape.Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString(tb.Text));
                            break;
                        case "StrokeThickness":
                            shape.StrokeThickness = double.Parse(tb.Text);
                            break;
                    }
                }
                else if (currentElt.GetType() == typeof(Polygon))
                {
                    Polygon shape = (Polygon)currentElt;
                    switch (property.Property)
                    {
                        case "Name":
                            shape.Name = tb.Text;
                            break;
                        case "Fill":
                            shape.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(tb.Text));
                            break;
                        case "Stroke":
                            shape.Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString(tb.Text));
                            break;
                        case "StrokeThickness":
                            shape.StrokeThickness = double.Parse(tb.Text);
                            break;
                        default:
                            int i = int.Parse(property.Property.Substring(1)) - 1;
                            if (property.Property.StartsWith("X")) shape.Points[i] = new Point(double.Parse(tb.Text), shape.Points[i].Y);
                            else if (property.Property.StartsWith("Y")) shape.Points[i] = new Point(shape.Points[i].X, double.Parse(tb.Text));
                            break;

                    }
                }
                else if (currentElt.GetType() == typeof(Line))
                {
                    Line shape = (Line)currentElt;
                    switch (property.Property)
                    {
                        case "Name":
                            shape.Name = tb.Text;
                            break;
                        case "X1":
                            shape.X1 = double.Parse(tb.Text);
                            break;
                        case "Y1":
                            shape.Y1 = double.Parse(tb.Text);
                            break;
                        case "X2":
                            shape.X2 = double.Parse(tb.Text);
                            break;
                        case "Y2":
                            shape.Y2 = double.Parse(tb.Text);
                            break;
                        case "Stroke":
                            shape.Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString(tb.Text));
                            break;
                        case "StrokeThickness":
                            shape.StrokeThickness = double.Parse(tb.Text);
                            break;
                    }
                }
                else if (currentElt.GetType() == typeof(TextBlock))
                {
                    TextBlock shape = (TextBlock)currentElt;
                    switch (property.Property)
                    {
                        case "Name":
                            shape.Name = tb.Text;
                            break;
                        case "Text":
                            shape.Text = tb.Text;
                            break;
                        case "Foreground":
                            shape.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(tb.Text));
                            break;
                        case "FontFamily":
                            shape.FontFamily = new FontFamily(tb.Text);
                            break;
                        case "FontStyle":
                            shape.FontStyle = tb.Text.ToLower() == "italic" ? FontStyles.Italic : FontStyles.Normal;
                            break;
                        case "FontSize":
                            shape.FontSize = double.Parse(tb.Text);
                            break;
                        case "FontWeight":
                            shape.FontWeight = tb.Text.ToLower() == "bold" ? FontWeights.Bold : FontWeights.Normal;
                            break;
                    }
                }
                else if (currentElt.GetType() == typeof(Image))
                {
                    Image shape = (Image)currentElt;
                    switch (property.Property)
                    {
                        case "Name":
                            shape.Name = tb.Text;
                            break;
                        case "Width":
                            shape.Width = double.Parse(tb.Text);
                            break;
                        case "Height":
                            shape.Height = double.Parse(tb.Text);
                            break;
                        case "Source":
                            shape.Source = new BitmapImage(new Uri(tb.Text));
                            break;
                    }
                }
                else if (currentElt.GetType() == typeof(Button))
                {
                    Button shape = (Button)currentElt;
                    switch (property.Property)
                    {
                        case "Name":
                            shape.Name = tb.Text;
                            break;
                        case "Content":
                            shape.Content = tb.Text;
                            break;
                        case "Foreground":
                            shape.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(tb.Text));
                            break;
                        case "FontFamily":
                            shape.FontFamily = new FontFamily(tb.Text);
                            break;
                        case "FontStyle":
                            shape.FontStyle = tb.Text.ToLower() == "italic" ? FontStyles.Italic : FontStyles.Normal;
                            break;
                        case "FontSize":
                            shape.FontSize = double.Parse(tb.Text);
                            break;
                        case "FontWeight":
                            shape.FontWeight = tb.Text.ToLower() == "bold" ? FontWeights.Bold : FontWeights.Normal;
                            break;
                    }
                }
                else if (currentElt.GetType() == typeof(TextBox))
                {
                    TextBox shape = (TextBox)currentElt;
                    switch (property.Property)
                    {
                        case "Name":
                            shape.Name = tb.Text;
                            break;
                        case "Text":
                            shape.Text = tb.Text;
                            break;
                        case "Foreground":
                            shape.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(tb.Text));
                            break;
                        case "FontFamily":
                            shape.FontFamily = new FontFamily(tb.Text);
                            break;
                        case "FontStyle":
                            shape.FontStyle = tb.Text.ToLower() == "italic" ? FontStyles.Italic : FontStyles.Normal;
                            break;
                        case "FontSize":
                            shape.FontSize = double.Parse(tb.Text);
                            break;
                        case "FontWeight":
                            shape.FontWeight = tb.Text.ToLower() == "bold" ? FontWeights.Bold : FontWeights.Normal;
                            break;
                    }
                }
                canvas.UpdateLayout();
                ShowCode();
            }
            catch
            {

            }
        }

        private void dataGridModifiers_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            try
            {
                PropertyData property = (PropertyData)e.Row.Item;
                TextBox tb = (TextBox)e.EditingElement;

                switch (property.Property)
                {
                    case "Width":
                        currentShape.modifiers["Width"] = tb.Text;
                        currentElt.Width = double.Parse(tb.Text);
                        break;
                    case "Height":
                        currentShape.modifiers["Height"] = tb.Text;
                        currentElt.Height = double.Parse(tb.Text);
                        break;
                    case "Left":
                        currentShape.modifiers["Left"] = tb.Text;
                        Canvas.SetLeft(currentShape.shape, double.Parse(tb.Text) - Shape.HandleShort);
                        break;
                    case "Top":
                        currentShape.modifiers["Top"] = tb.Text;
                        Canvas.SetTop(currentShape.shape, double.Parse(tb.Text) - Shape.HandleShort);
                        break;
                    case "Angle":
                        currentShape.modifiers["Angle"] = tb.Text;
                        RotateTransform rotateTransform = new RotateTransform();
                        rotateTransform.CenterX = currentElt.ActualWidth / 2.0;
                        rotateTransform.CenterY = currentElt.ActualHeight / 2.0;
                        rotateTransform.Angle = double.Parse(tb.Text);
                        currentElt.RenderTransform = new TransformGroup();
                        ((TransformGroup)currentElt.RenderTransform).Children.Add(rotateTransform);
                        break;
                    case "Opacity":
                        currentShape.modifiers["Opacity"] = tb.Text;
                        currentElt.Opacity = double.Parse(tb.Text) / 100.0;
                        break;
                }
                canvas.UpdateLayout();
                ShowCode();
            }
            catch
            {

            }
        }

        private void buttonDelete_Click(object sender, RoutedEventArgs e)
        {
            Delete();
        }

        private void textBoxSnap_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                int.TryParse(textBoxSnap.Text, out snap);
                SetSnap();
            }
            catch
            {

            }
        }

        private void buttonImport_Click(object sender, RoutedEventArgs e)
        {
            ReadCode();
        }

        private void buttonDeleteAll_Click(object sender, RoutedEventArgs e)
        {
            DeleteAll();
        }

        private void textBoxWidth_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                double width = canvas.Width;
                double.TryParse(textBoxWidth.Text, out width);
                canvas.Width = width;
                SetSnap();
            }
            catch
            {

            }
        }

        private void textBoxHeight_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                double height = canvas.Height;
                double.TryParse(textBoxHeight.Text, out height);
                canvas.Height = height;
                SetSnap();
            }
            catch
            {

            }
        }
    }
}
