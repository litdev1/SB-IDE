using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
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
        private ContextMenu contextMenu;
        private ScaleTransform scaleTransform;

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
        private double scale = 1;
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
            canvas.PreviewMouseLeftButtonUp += new MouseButtonEventHandler(canvasPreviewLeftMouseLeftButtonUp);
            canvas.PreviewMouseRightButtonDown += new MouseButtonEventHandler(canvasPreviewMouseRightButtonDown);

            scaleTransform = new ScaleTransform();
            scaleTransform.CenterX = 0;
            scaleTransform.CenterY = 0;
            visualGrid.RenderTransform = new TransformGroup();
            ((TransformGroup)visualGrid.RenderTransform).Children.Add(scaleTransform);

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

            contextMenu = new ContextMenu();
            canvas.ContextMenu = contextMenu;

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

            canvas.Children.Remove(currentShape.shape);

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

        private void eltPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
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
                e.Handled = true;
            }

            UpdateView();
        }

        private void canvasPreviewLeftMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (null == currentShape) return;

            mode = "SEL";
            Cursor = Cursors.Arrow;
            e.Handled = true;

            UpdateView();
        }

        private void canvasPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            contextMenu.Items.Clear();

            MenuItem itemShape = new MenuItem();
            itemShape.Header = "Select Shape";
            contextMenu.Items.Add(itemShape);

            MenuItem item;
            foreach (FrameworkElement child in canvas.Children)
            {
                if (child.GetType() == typeof(Grid))
                {
                    Grid shape = (Grid)child;
                    item = new MenuItem();
                    itemShape.Items.Add(item);
                    FrameworkElement elt = (FrameworkElement)shape.Children[0];
                    item.Header = elt.Name;
                    item.Click += new RoutedEventHandler(SelectShapeClick);
                    item.Tag = elt;
                }
            }
        }

        private void SelectShapeClick(object sender, RoutedEventArgs e)
        {
            try
            {
                MenuItem item = (MenuItem)sender;
                FrameworkElement elt = (FrameworkElement)item.Tag;
                mode = "SEL";
                eltPreviewMouseLeftButtonDown(elt, null);
            }
            catch
            {

            }
        }

        private void canvasMouseMove(object sender, MouseEventArgs e)
        {
            if (null == currentShape) return;
            if (mode == "SEL") return;
            if (e.LeftButton == MouseButtonState.Released) return;

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
            currentShape.modifiers["Width"] = (currentElt.Width).ToString();
            currentShape.modifiers["Height"] = (currentElt.Height).ToString();
            currentShape.modifiers["Left"] = (Canvas.GetLeft(currentShape.shape) + Shape.HandleShort).ToString();
            currentShape.modifiers["Top"] = (Canvas.GetTop(currentShape.shape) + Shape.HandleShort).ToString();
            canvas.UpdateLayout();
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

        private string GetName(string label, string assign)
        {
            int pos = assign.IndexOf("=");
            if (pos > 0)
            {
                assign = assign.Substring(0, pos).Trim();
                if (names.Contains(assign)) return GetName(assign);
                names.Add(assign);
                return assign;
            }
            return GetName(label);
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
                    properties.Add(new PropertyData() { Property = "Name", Value = currentElt.Name, Visible = Visibility.Hidden });
                    if (currentElt.GetType() == typeof(Rectangle))
                    {
                        Rectangle shape = (Rectangle)currentElt;
                        properties.Add(new PropertyData() { Property = "Fill", Value = ColorName(shape.Fill), Visible = Visibility.Visible });
                        properties.Add(new PropertyData() { Property = "Stroke", Value = ColorName(shape.Stroke), Visible = Visibility.Visible });
                        properties.Add(new PropertyData() { Property = "StrokeThickness", Value = shape.StrokeThickness.ToString(), Visible = Visibility.Hidden });
                    }
                    else if (currentElt.GetType() == typeof(Ellipse))
                    {
                        Ellipse shape = (Ellipse)currentElt;
                        properties.Add(new PropertyData() { Property = "Fill", Value = ColorName(shape.Fill), Visible = Visibility.Visible });
                        properties.Add(new PropertyData() { Property = "Stroke", Value = ColorName(shape.Stroke), Visible = Visibility.Visible });
                        properties.Add(new PropertyData() { Property = "StrokeThickness", Value = shape.StrokeThickness.ToString(), Visible = Visibility.Hidden });
                    }
                    else if (currentElt.GetType() == typeof(Polygon))
                    {
                        Polygon shape = (Polygon)currentElt;
                        for (int i = 0; i < shape.Points.Count; i++)
                        {
                            properties.Add(new PropertyData() { Property = "X" + (i + 1).ToString(), Value = shape.Points[i].X.ToString(), Visible = Visibility.Hidden });
                            properties.Add(new PropertyData() { Property = "Y" + (i + 1).ToString(), Value = shape.Points[i].Y.ToString(), Visible = Visibility.Hidden });
                        }
                        properties.Add(new PropertyData() { Property = "Fill", Value = ColorName(shape.Fill), Visible = Visibility.Visible });
                        properties.Add(new PropertyData() { Property = "Stroke", Value = ColorName(shape.Stroke), Visible = Visibility.Visible });
                        properties.Add(new PropertyData() { Property = "StrokeThickness", Value = shape.StrokeThickness.ToString(), Visible = Visibility.Hidden });
                    }
                    else if (currentElt.GetType() == typeof(Line))
                    {
                        Line shape = (Line)currentElt;
                        properties.Add(new PropertyData() { Property = "X1", Value = shape.X1.ToString(), Visible = Visibility.Hidden });
                        properties.Add(new PropertyData() { Property = "Y1", Value = shape.Y1.ToString(), Visible = Visibility.Hidden });
                        properties.Add(new PropertyData() { Property = "X2", Value = shape.X2.ToString(), Visible = Visibility.Hidden });
                        properties.Add(new PropertyData() { Property = "Y2", Value = shape.Y2.ToString(), Visible = Visibility.Hidden });
                        properties.Add(new PropertyData() { Property = "Stroke", Value = ColorName(shape.Stroke), Visible = Visibility.Visible });
                        properties.Add(new PropertyData() { Property = "StrokeThickness", Value = shape.StrokeThickness.ToString(), Visible = Visibility.Hidden });
                    }
                    else if (currentElt.GetType() == typeof(TextBlock))
                    {
                        TextBlock shape = (TextBlock)currentElt;
                        properties.Add(new PropertyData() { Property = "Text", Value = shape.Text, Visible = Visibility.Hidden });
                        properties.Add(new PropertyData() { Property = "Foreground", Value = ColorName(shape.Foreground), Visible = Visibility.Visible });
                        properties.Add(new PropertyData() { Property = "FontFamily", Value = shape.FontFamily.ToString(), Visible = Visibility.Visible });
                        properties.Add(new PropertyData() { Property = "FontStyle", Value = shape.FontStyle.ToString(), Visible = Visibility.Visible });
                        properties.Add(new PropertyData() { Property = "FontSize", Value = shape.FontSize.ToString(), Visible = Visibility.Visible });
                        properties.Add(new PropertyData() { Property = "FontWeight", Value = shape.FontWeight.ToString(), Visible = Visibility.Visible });
                    }
                    else if (currentElt.GetType() == typeof(Image))
                    {
                        Image shape = (Image)currentElt;
                        properties.Add(new PropertyData() { Property = "Source", Value = shape.Source.ToString(), Visible = Visibility.Visible });
                    }
                    else if (currentElt.GetType() == typeof(Button))
                    {
                        Button shape = (Button)currentElt;
                        properties.Add(new PropertyData() { Property = "Content", Value = shape.Content.ToString(), Visible = Visibility.Hidden });
                        properties.Add(new PropertyData() { Property = "Foreground", Value = ColorName(shape.Foreground), Visible = Visibility.Visible });
                        properties.Add(new PropertyData() { Property = "FontFamily", Value = shape.FontFamily.ToString(), Visible = Visibility.Visible });
                        properties.Add(new PropertyData() { Property = "FontStyle", Value = shape.FontStyle.ToString(), Visible = Visibility.Visible });
                        properties.Add(new PropertyData() { Property = "FontSize", Value = shape.FontSize.ToString(), Visible = Visibility.Visible });
                        properties.Add(new PropertyData() { Property = "FontWeight", Value = shape.FontWeight.ToString(), Visible = Visibility.Visible });
                    }
                    else if (currentElt.GetType() == typeof(TextBox))
                    {
                        TextBox shape = (TextBox)currentElt;
                        properties.Add(new PropertyData() { Property = "Text", Value = shape.Text.ToString(), Visible = Visibility.Hidden });
                        properties.Add(new PropertyData() { Property = "Foreground", Value = ColorName(shape.Foreground), Visible = Visibility.Visible });
                        properties.Add(new PropertyData() { Property = "FontFamily", Value = shape.FontFamily.ToString(), Visible = Visibility.Visible });
                        properties.Add(new PropertyData() { Property = "FontStyle", Value = shape.FontStyle.ToString(), Visible = Visibility.Visible });
                        properties.Add(new PropertyData() { Property = "FontSize", Value = shape.FontSize.ToString(), Visible = Visibility.Visible });
                        properties.Add(new PropertyData() { Property = "FontWeight", Value = shape.FontWeight.ToString(), Visible = Visibility.Visible });
                    }
                    else if (currentElt.GetType() == typeof(WebBrowser))
                    {
                        WebBrowser shape = (WebBrowser)currentElt;
                        properties.Add(new PropertyData() { Property = "Url", Value = shape.Source.ToString(), Visible = Visibility.Hidden });
                    }
                    else if (currentElt.GetType() == typeof(CheckBox))
                    {
                        CheckBox shape = (CheckBox)currentElt;
                        properties.Add(new PropertyData() { Property = "Content", Value = shape.Content.ToString(), Visible = Visibility.Hidden });
                    }
                    else if (currentElt.GetType() == typeof(ComboBox))
                    {
                        ComboBox shape = (ComboBox)currentElt;
                        string list = "";
                        int i = 1;
                        foreach (ComboBoxItem item in shape.Items)
                        {
                            list += (i++).ToString() + "=" + item.Content.ToString() + ";";
                        }
                        properties.Add(new PropertyData() { Property = "List", Value = list, Visible = Visibility.Hidden });
                        properties.Add(new PropertyData() { Property = "DropDownHeight", Value = shape.MaxDropDownHeight.ToString(), Visible = Visibility.Hidden });
                    }
                    else if (currentElt.GetType() == typeof(WindowsFormsHost))
                    {
                        WindowsFormsHost shape = (WindowsFormsHost)currentElt;
                        System.Windows.Forms.DataGridView dataView = (System.Windows.Forms.DataGridView)shape.Child;
                        string headings = "";
                        int i = 1;
                        foreach (System.Windows.Forms.DataGridViewColumn col in dataView.Columns)
                        {
                            headings += (i++).ToString() + "=" + col.HeaderText + ";";
                        }
                        properties.Add(new PropertyData() { Property = "Headings", Value = headings, Visible = Visibility.Hidden });
                    }
                    else if (currentElt.GetType() == typeof(DocumentViewer))
                    {
                        DocumentViewer shape = (DocumentViewer)currentElt;
                    }
                    else if (currentElt.GetType() == typeof(ListView))
                    {
                        ListView shape = (ListView)currentElt;
                        GridView gridView = (GridView)shape.View;
                        string headings = "";
                        int i = 1;
                        foreach (GridViewColumn col in gridView.Columns)
                        {
                            headings += (i++).ToString() + "=" + ((GridViewColumnHeader)col.Header).Content.ToString() + ";";
                        }
                        properties.Add(new PropertyData() { Property = "Headings", Value = headings, Visible = Visibility.Hidden });
                    }
                    else if (currentElt.GetType() == typeof(MediaElement))
                    {
                        MediaElement shape = (MediaElement)currentElt;
                    }
                    else if (currentElt.GetType() == typeof(Menu))
                    {
                        Menu shape = (Menu)currentElt;
                        string menuList = "";
                        string iconList = "";
                        string checkList = "";
                        string separator = "-";
                        foreach (MenuItem menuItem in shape.Items)
                        {
                            GetMenuLists(menuItem, ref menuList, ref iconList, ref checkList, ref separator);
                        }
                        properties.Add(new PropertyData() { Property = "MenuList", Value = menuList, Visible = Visibility.Hidden });
                        properties.Add(new PropertyData() { Property = "IconList", Value = iconList, Visible = Visibility.Hidden });
                        properties.Add(new PropertyData() { Property = "CheckList", Value = checkList, Visible = Visibility.Hidden });
                    }
                    else if (currentElt.GetType() == typeof(PasswordBox))
                    {
                        PasswordBox shape = (PasswordBox)currentElt;
                        properties.Add(new PropertyData() { Property = "MaxLength", Value = shape.MaxLength.ToString(), Visible = Visibility.Hidden });
                    }
                    else if (currentElt.GetType() == typeof(ProgressBar))
                    {
                        ProgressBar shape = (ProgressBar)currentElt;
                        properties.Add(new PropertyData() { Property = "Orientation", Value = shape.Orientation.ToString(), Visible = Visibility.Hidden });
                    }
                    else if (currentElt.GetType() == typeof(RadioButton))
                    {
                        RadioButton shape = (RadioButton)currentElt;
                        properties.Add(new PropertyData() { Property = "Content", Value = shape.Content.ToString(), Visible = Visibility.Hidden });
                        properties.Add(new PropertyData() { Property = "GroupName", Value = shape.GroupName, Visible = Visibility.Hidden });
                    }
                    else if (currentElt.GetType() == typeof(RichTextBox))
                    {
                        RichTextBox shape = (RichTextBox)currentElt;
                    }
                    else if (currentElt.GetType() == typeof(Slider))
                    {
                        Slider shape = (Slider)currentElt;
                        properties.Add(new PropertyData() { Property = "Orientation", Value = shape.Orientation.ToString(), Visible = Visibility.Hidden });
                    }
                    else if (currentElt.GetType() == typeof(TreeView))
                    {
                        TreeView shape = (TreeView)currentElt;
                        string tree = "";
                        int i = 1;
                        foreach (TreeViewItem item in shape.Items)
                        {
                            GetTreeList(item, ref i, 0, ref tree);
                        }
                        properties.Add(new PropertyData() { Property = "Tree", Value = tree, Visible = Visibility.Hidden });
                    }
                }
                dataGridProperties.Items.Refresh();
            }
            catch (Exception ex)
            {

            }
        }

        private void GetMenuLists(object obj, ref string menuList, ref string iconList, ref string checkList, ref string separator)
        {
            if (obj.GetType() == typeof(Separator))
            {
                Separator data = (Separator)obj;
                menuList += separator + "=" + data.Name + ";";
                separator += "-";
            }
            else
            {
                MenuItem menuItem = (MenuItem)obj;
                menuList += menuItem.Header + "=" + menuItem.Name + ";";
                if (null != menuItem.Icon) iconList += menuItem.Name + "=" + ((Image)menuItem.Icon).Source.ToString() + ";";
                if (menuItem.IsCheckable) checkList += menuItem.Name + "=" + menuItem.IsChecked + ";";
                foreach (object item in menuItem.Items)
                {
                    GetMenuLists(item, ref menuList, ref iconList, ref checkList, ref separator);
                }
            }
        }

        private void GetTreeList(TreeViewItem item, ref int i, int parent, ref string tree)
        {
            int j = i;
            string array = parent + "\\=" + item.Header.ToString() + "\\;";
            tree += (i++).ToString() + "=" + array + ";";
            if (item.Items.Count > 0)
            {
                foreach (TreeViewItem _item in item.Items)
                {
                    GetTreeList(_item, ref i, j, ref tree);
                }
            }
        }

        private void ShowModifiers()
        {
            try
            {
                modifiers.Clear();
                if (null != currentShape)
                {
                    currentElt.Measure(new Size(double.MaxValue, double.MaxValue));
                    if (!currentShape.modifiers.ContainsKey("Width")) currentShape.modifiers["Width"] = currentElt.DesiredSize.Width.ToString();
                    if (!currentShape.modifiers.ContainsKey("Height")) currentShape.modifiers["Height"] = currentElt.DesiredSize.Height.ToString();
                    Point point = currentShape.shape.TranslatePoint(new Point(0, 0), canvas);
                    if (!currentShape.modifiers.ContainsKey("Left")) currentShape.modifiers["Left"] = (point.X + Shape.HandleShort).ToString();
                    if (!currentShape.modifiers.ContainsKey("Top")) currentShape.modifiers["Top"] = (point.Y + Shape.HandleShort).ToString();
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

            Brush _brush = new SolidColorBrush(((SolidColorBrush)brush).Color);
            Pen _pen = new Pen(new SolidColorBrush(((SolidColorBrush)pen.Brush).Color), pen.Thickness);
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
                                sbDocument.TextArea.Text += "Shapes.Move(" + obj.Name + "," + Fix(shape.modifiers["Left"]) + "," + Fix(shape.modifiers["Top"]) + ")\n";
                                if (shape.modifiers["Opacity"] != "100") sbDocument.TextArea.Text += "Shapes.SetOpacity(" + obj.Name + "," + Fix(shape.modifiers["Opacity"]) + ")\n";
                                if (shape.modifiers["Angle"] != "0") sbDocument.TextArea.Text += "Shapes.Rotate(" + obj.Name + "," + Fix(shape.modifiers["Angle"]) + ")\n";
                                sbDocument.TextArea.Text += "\n";
                            }
                            else if (elt.GetType() == typeof(WebBrowser))
                            {
                                WebBrowser obj = (WebBrowser)elt;
                                string url = "";
                                if (null != obj.Source) url = obj.Source.ToString();
                                sbDocument.TextArea.Text += obj.Name + " = LDControls.AddBrowser(" + Fix(shape.modifiers["Width"]) + "," + Fix(shape.modifiers["Height"]) + ",\"" + url + "\")\n";
                                sbDocument.TextArea.Text += "Shapes.Move(" + obj.Name + "," + Fix(shape.modifiers["Left"]) + "," + Fix(shape.modifiers["Top"]) + ")\n";
                                if (shape.modifiers["Opacity"] != "100") sbDocument.TextArea.Text += "Shapes.SetOpacity(" + obj.Name + "," + Fix(shape.modifiers["Opacity"]) + ")\n";
                                if (shape.modifiers["Angle"] != "0") sbDocument.TextArea.Text += "Shapes.Rotate(" + obj.Name + "," + Fix(shape.modifiers["Angle"]) + ")\n";
                                sbDocument.TextArea.Text += "\n";
                            }
                            else if (elt.GetType() == typeof(CheckBox))
                            {
                                CheckBox obj = (CheckBox)elt;
                                sbDocument.TextArea.Text += obj.Name + " = LDControls.AddCheckBox(\"" + obj.Content.ToString() + "\")\n";
                                sbDocument.TextArea.Text += "Controls.SetSize(" + obj.Name + "," + Fix(shape.modifiers["Width"]) + "," + Fix(shape.modifiers["Height"]) + ")\n";
                                sbDocument.TextArea.Text += "Shapes.Move(" + obj.Name + "," + Fix(shape.modifiers["Left"]) + "," + Fix(shape.modifiers["Top"]) + ")\n";
                                if (shape.modifiers["Opacity"] != "100") sbDocument.TextArea.Text += "Shapes.SetOpacity(" + obj.Name + "," + Fix(shape.modifiers["Opacity"]) + ")\n";
                                if (shape.modifiers["Angle"] != "0") sbDocument.TextArea.Text += "Shapes.Rotate(" + obj.Name + "," + Fix(shape.modifiers["Angle"]) + ")\n";
                                sbDocument.TextArea.Text += "\n";
                            }
                            else if (elt.GetType() == typeof(ComboBox))
                            {
                                ComboBox obj = (ComboBox)elt;
                                string list = "";
                                int i = 1;
                                foreach (ComboBoxItem item in obj.Items)
                                {
                                    list += (i++).ToString() + "=" + item.Content.ToString() + ";";
                                }
                                sbDocument.TextArea.Text += obj.Name + " = LDControls.AddComboBox(\"" + list + "\"," + Fix(shape.modifiers["Width"]) + "," + obj.MaxDropDownHeight.ToString() + ")\n";
                                sbDocument.TextArea.Text += "Controls.SetSize(" + obj.Name + "," + Fix(shape.modifiers["Width"]) + "," + Fix(shape.modifiers["Height"]) + ")\n";
                                sbDocument.TextArea.Text += "Shapes.Move(" + obj.Name + "," + Fix(shape.modifiers["Left"]) + "," + Fix(shape.modifiers["Top"]) + ")\n";
                                if (shape.modifiers["Opacity"] != "100") sbDocument.TextArea.Text += "Shapes.SetOpacity(" + obj.Name + "," + Fix(shape.modifiers["Opacity"]) + ")\n";
                                if (shape.modifiers["Angle"] != "0") sbDocument.TextArea.Text += "Shapes.Rotate(" + obj.Name + "," + Fix(shape.modifiers["Angle"]) + ")\n";
                                sbDocument.TextArea.Text += "\n";
                            }
                            else if (elt.GetType() == typeof(WindowsFormsHost))
                            {
                                WindowsFormsHost obj = (WindowsFormsHost)elt;
                                System.Windows.Forms.DataGridView dataView = (System.Windows.Forms.DataGridView)obj.Child;
                                string headings = "";
                                int i = 1;
                                foreach (System.Windows.Forms.DataGridViewColumn col in dataView.Columns)
                                {
                                    headings += (i++).ToString() + "=" + col.HeaderText + ";";
                                }
                                sbDocument.TextArea.Text += obj.Name + " = LDControls.AddDataView(" + Fix(shape.modifiers["Width"]) + "," + Fix(shape.modifiers["Height"]) + ",\"" + headings + "\")\n";
                                sbDocument.TextArea.Text += "Shapes.Move(" + obj.Name + "," + Fix(shape.modifiers["Left"]) + "," + Fix(shape.modifiers["Top"]) + ")\n";
                                if (shape.modifiers["Opacity"] != "100") sbDocument.TextArea.Text += "Shapes.SetOpacity(" + obj.Name + "," + Fix(shape.modifiers["Opacity"]) + ")\n";
                                if (shape.modifiers["Angle"] != "0") sbDocument.TextArea.Text += "Shapes.Rotate(" + obj.Name + "," + Fix(shape.modifiers["Angle"]) + ")\n";
                                sbDocument.TextArea.Text += "\n";
                            }
                            else if (elt.GetType() == typeof(DocumentViewer))
                            {
                                DocumentViewer obj = (DocumentViewer)elt;
                                sbDocument.TextArea.Text += obj.Name + " = LDControls.AddDocumentViewer(" + Fix(shape.modifiers["Width"]) + "," + Fix(shape.modifiers["Height"]) + ")\n";
                                sbDocument.TextArea.Text += "Shapes.Move(" + obj.Name + "," + Fix(shape.modifiers["Left"]) + "," + Fix(shape.modifiers["Top"]) + ")\n";
                                if (shape.modifiers["Opacity"] != "100") sbDocument.TextArea.Text += "Shapes.SetOpacity(" + obj.Name + "," + Fix(shape.modifiers["Opacity"]) + ")\n";
                                if (shape.modifiers["Angle"] != "0") sbDocument.TextArea.Text += "Shapes.Rotate(" + obj.Name + "," + Fix(shape.modifiers["Angle"]) + ")\n";
                                sbDocument.TextArea.Text += "\n";
                            }
                            else if (elt.GetType() == typeof(ListView))
                            {
                                ListView obj = (ListView)elt;
                                GridView gridView = (GridView)obj.View;
                                string headings = "";
                                int i = 1;
                                foreach (GridViewColumn col in gridView.Columns)
                                {
                                    headings += (i++).ToString() + "=" + ((GridViewColumnHeader)col.Header).Content.ToString() + ";";
                                }
                                sbDocument.TextArea.Text += obj.Name + " = LDControls.AddListView(" + Fix(shape.modifiers["Width"]) + "," + Fix(shape.modifiers["Height"]) + ",\"" + headings + "\")\n";
                                sbDocument.TextArea.Text += "Shapes.Move(" + obj.Name + "," + Fix(shape.modifiers["Left"]) + "," + Fix(shape.modifiers["Top"]) + ")\n";
                                if (shape.modifiers["Opacity"] != "100") sbDocument.TextArea.Text += "Shapes.SetOpacity(" + obj.Name + "," + Fix(shape.modifiers["Opacity"]) + ")\n";
                                if (shape.modifiers["Angle"] != "0") sbDocument.TextArea.Text += "Shapes.Rotate(" + obj.Name + "," + Fix(shape.modifiers["Angle"]) + ")\n";
                                sbDocument.TextArea.Text += "\n";
                            }
                            else if (elt.GetType() == typeof(MediaPlayer))
                            {
                                MediaElement obj = (MediaElement)elt;
                                sbDocument.TextArea.Text += obj.Name + " = LDControls.AddMediaPlayer(" + Fix(shape.modifiers["Width"]) + "," + Fix(shape.modifiers["Height"]) + ")\n";
                                sbDocument.TextArea.Text += "Shapes.Move(" + obj.Name + "," + Fix(shape.modifiers["Left"]) + "," + Fix(shape.modifiers["Top"]) + ")\n";
                                if (shape.modifiers["Opacity"] != "100") sbDocument.TextArea.Text += "Shapes.SetOpacity(" + obj.Name + "," + Fix(shape.modifiers["Opacity"]) + ")\n";
                                if (shape.modifiers["Angle"] != "0") sbDocument.TextArea.Text += "Shapes.Rotate(" + obj.Name + "," + Fix(shape.modifiers["Angle"]) + ")\n";
                                sbDocument.TextArea.Text += "\n";
                            }
                            else if (elt.GetType() == typeof(Menu))
                            {
                                Menu obj = (Menu)elt;
                                string menuList = "";
                                string iconList = "";
                                string checkList = "";
                                string separator = "-";
                                foreach (MenuItem menuItem in obj.Items)
                                {
                                    GetMenuLists(menuItem, ref menuList, ref iconList, ref checkList, ref separator);
                                }
                                sbDocument.TextArea.Text += obj.Name + " = LDControls.AddMenu(" + Fix(shape.modifiers["Width"]) + "," + Fix(shape.modifiers["Height"]) + ",\"" + menuList + "\",\"" + iconList + "\",\"" + checkList + "\")\n";
                                sbDocument.TextArea.Text += "Shapes.Move(" + obj.Name + "," + Fix(shape.modifiers["Left"]) + "," + Fix(shape.modifiers["Top"]) + ")\n";
                                if (shape.modifiers["Opacity"] != "100") sbDocument.TextArea.Text += "Shapes.SetOpacity(" + obj.Name + "," + Fix(shape.modifiers["Opacity"]) + ")\n";
                                if (shape.modifiers["Angle"] != "0") sbDocument.TextArea.Text += "Shapes.Rotate(" + obj.Name + "," + Fix(shape.modifiers["Angle"]) + ")\n";
                                sbDocument.TextArea.Text += "\n";
                            }
                            else if (elt.GetType() == typeof(PasswordBox))
                            {
                                PasswordBox obj = (PasswordBox)elt;
                                sbDocument.TextArea.Text += obj.Name + " = LDControls.AddPasswordBox(" + Fix(shape.modifiers["Width"]) + "," + Fix(shape.modifiers["Height"]) + "," + obj.MaxLength.ToString() + ")\n";
                                sbDocument.TextArea.Text += "Shapes.Move(" + obj.Name + "," + Fix(shape.modifiers["Left"]) + "," + Fix(shape.modifiers["Top"]) + ")\n";
                                if (shape.modifiers["Opacity"] != "100") sbDocument.TextArea.Text += "Shapes.SetOpacity(" + obj.Name + "," + Fix(shape.modifiers["Opacity"]) + ")\n";
                                if (shape.modifiers["Angle"] != "0") sbDocument.TextArea.Text += "Shapes.Rotate(" + obj.Name + "," + Fix(shape.modifiers["Angle"]) + ")\n";
                                sbDocument.TextArea.Text += "\n";
                            }
                            else if (elt.GetType() == typeof(ProgressBar))
                            {
                                ProgressBar obj = (ProgressBar)elt;
                                sbDocument.TextArea.Text += obj.Name + " = LDControls.AddProgressBar(" + Fix(shape.modifiers["Width"]) + "," + Fix(shape.modifiers["Height"]) + ",\"" + obj.Orientation.ToString() + "\")\n";
                                sbDocument.TextArea.Text += "Shapes.Move(" + obj.Name + "," + Fix(shape.modifiers["Left"]) + "," + Fix(shape.modifiers["Top"]) + ")\n";
                                if (shape.modifiers["Opacity"] != "100") sbDocument.TextArea.Text += "Shapes.SetOpacity(" + obj.Name + "," + Fix(shape.modifiers["Opacity"]) + ")\n";
                                if (shape.modifiers["Angle"] != "0") sbDocument.TextArea.Text += "Shapes.Rotate(" + obj.Name + "," + Fix(shape.modifiers["Angle"]) + ")\n";
                                sbDocument.TextArea.Text += "\n";
                            }
                            else if (elt.GetType() == typeof(RadioButton))
                            {
                                RadioButton obj = (RadioButton)elt;
                                sbDocument.TextArea.Text += obj.Name + " = LDControls.AddRadioButton(\"" + obj.Content.ToString() + "\",\"" + obj.GroupName.ToString() + "\")\n";
                                sbDocument.TextArea.Text += "Controls.SetSize(" + obj.Name + "," + Fix(shape.modifiers["Width"]) + "," + Fix(shape.modifiers["Height"]) + ")\n";
                                sbDocument.TextArea.Text += "Shapes.Move(" + obj.Name + "," + Fix(shape.modifiers["Left"]) + "," + Fix(shape.modifiers["Top"]) + ")\n";
                                if (shape.modifiers["Opacity"] != "100") sbDocument.TextArea.Text += "Shapes.SetOpacity(" + obj.Name + "," + Fix(shape.modifiers["Opacity"]) + ")\n";
                                if (shape.modifiers["Angle"] != "0") sbDocument.TextArea.Text += "Shapes.Rotate(" + obj.Name + "," + Fix(shape.modifiers["Angle"]) + ")\n";
                                sbDocument.TextArea.Text += "\n";
                            }
                            else if (elt.GetType() == typeof(RichTextBox))
                            {
                                RichTextBox obj = (RichTextBox)elt;
                                sbDocument.TextArea.Text += obj.Name + " = LDControls.AddRichTextBox(" + Fix(shape.modifiers["Width"]) + "," + Fix(shape.modifiers["Height"]) + ")\n";
                                sbDocument.TextArea.Text += "Shapes.Move(" + obj.Name + "," + Fix(shape.modifiers["Left"]) + "," + Fix(shape.modifiers["Top"]) + ")\n";
                                if (shape.modifiers["Opacity"] != "100") sbDocument.TextArea.Text += "Shapes.SetOpacity(" + obj.Name + "," + Fix(shape.modifiers["Opacity"]) + ")\n";
                                if (shape.modifiers["Angle"] != "0") sbDocument.TextArea.Text += "Shapes.Rotate(" + obj.Name + "," + Fix(shape.modifiers["Angle"]) + ")\n";
                                sbDocument.TextArea.Text += "\n";
                            }
                            else if (elt.GetType() == typeof(Slider))
                            {
                                Slider obj = (Slider)elt;
                                sbDocument.TextArea.Text += obj.Name + " = LDControls.AddSlider(" + Fix(shape.modifiers["Width"]) + "," + Fix(shape.modifiers["Height"]) + ",\"" + obj.Orientation.ToString() + "\")\n";
                                sbDocument.TextArea.Text += "Shapes.Move(" + obj.Name + "," + Fix(shape.modifiers["Left"]) + "," + Fix(shape.modifiers["Top"]) + ")\n";
                                if (shape.modifiers["Opacity"] != "100") sbDocument.TextArea.Text += "Shapes.SetOpacity(" + obj.Name + "," + Fix(shape.modifiers["Opacity"]) + ")\n";
                                if (shape.modifiers["Angle"] != "0") sbDocument.TextArea.Text += "Shapes.Rotate(" + obj.Name + "," + Fix(shape.modifiers["Angle"]) + ")\n";
                                sbDocument.TextArea.Text += "\n";
                            }
                            else if (elt.GetType() == typeof(TreeView))
                            {
                                TreeView obj = (TreeView)elt;
                                string tree = "";
                                int i = 1;
                                foreach (TreeViewItem item in obj.Items)
                                {
                                    GetTreeList(item, ref i, 0, ref tree);
                                }
                                sbDocument.TextArea.Text += obj.Name + " = LDControls.AddTreeView(\"" + tree + "\"," + Fix(shape.modifiers["Width"]) + "," + Fix(shape.modifiers["Height"]) + ")\n";
                                sbDocument.TextArea.Text += "Shapes.Move(" + obj.Name + "," + Fix(shape.modifiers["Left"]) + "," + Fix(shape.modifiers["Top"]) + ")\n";
                                if (shape.modifiers["Opacity"] != "100") sbDocument.TextArea.Text += "Shapes.SetOpacity(" + obj.Name + "," + Fix(shape.modifiers["Opacity"]) + ")\n";
                                if (shape.modifiers["Angle"] != "0") sbDocument.TextArea.Text += "Shapes.Rotate(" + obj.Name + "," + Fix(shape.modifiers["Angle"]) + ")\n";
                                sbDocument.TextArea.Text += "\n";
                            }
                            //TODO
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
                Brush _brush = new SolidColorBrush(((SolidColorBrush)brush).Color);
                Pen _pen = new Pen(new SolidColorBrush(((SolidColorBrush)pen.Brush).Color), pen.Thickness);
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
                    string code = line.Text.Trim();
                    string codeLower = code.ToLower();
                    if (codeLower.Contains("shapes.addrectangle"))
                    {
                        string[] parts = code.Split(new char[] { '(', ')', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        double.TryParse(parts[1], out value[0]);
                        double.TryParse(parts[2], out value[1]);
                        name = GetName("Rectangle", parts[0]);
                        elt = new Rectangle()
                        {
                            Name = name,
                            Width = value[0],
                            Height = value[1],
                            Fill = _brush,
                            Stroke = _pen.Brush,
                            StrokeThickness = _pen.Thickness,
                        };
                        elt.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(eltPreviewMouseLeftButtonDown);
                        shape = new Shape(elt);
                        canvas.Children.Add(shape.shape);
                    }
                    else if (codeLower.Contains("shapes.addellipse"))
                    {
                        string[] parts = code.Split(new char[] { '(', ')', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        double.TryParse(parts[1], out value[0]);
                        double.TryParse(parts[2], out value[1]);
                        name = GetName("Ellipse", parts[0]);
                        elt = new Ellipse()
                        {
                            Name = name,
                            Width = value[0],
                            Height = value[1],
                            Fill = _brush,
                            Stroke = _pen.Brush,
                            StrokeThickness = _pen.Thickness,
                        };
                        elt.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(eltPreviewMouseLeftButtonDown);
                        shape = new Shape(elt);
                        canvas.Children.Add(shape.shape);
                    }
                    else if (codeLower.Contains("shapes.addtriangle"))
                    {
                        string[] parts = code.Split(new char[] { '(', ')', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        double.TryParse(parts[1], out value[0]);
                        double.TryParse(parts[2], out value[1]);
                        double.TryParse(parts[3], out value[2]);
                        double.TryParse(parts[4], out value[3]);
                        double.TryParse(parts[5], out value[4]);
                        double.TryParse(parts[6], out value[5]);
                        name = GetName("Triangle", parts[0]);
                        elt = new Polygon()
                        {
                            Name = name,
                            Points = new PointCollection() { new Point(value[0], value[1]), new Point(value[2], value[3]), new Point(value[4], value[5]) },
                            Fill = _brush,
                            Stroke = _pen.Brush,
                            StrokeThickness = _pen.Thickness,
                        };
                        elt.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(eltPreviewMouseLeftButtonDown);
                        shape = new Shape(elt);
                        canvas.Children.Add(shape.shape);
                    }
                    else if (codeLower.Contains("shapes.addline"))
                    {
                        string[] parts = code.Split(new char[] { '(', ')', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        double.TryParse(parts[1], out value[0]);
                        double.TryParse(parts[2], out value[1]);
                        double.TryParse(parts[3], out value[2]);
                        double.TryParse(parts[4], out value[3]);
                        name = GetName("Line", parts[0]);
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
                        elt.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(eltPreviewMouseLeftButtonDown);
                        shape = new Shape(elt);
                        canvas.Children.Add(shape.shape);
                    }
                    else if (codeLower.Contains("shapes.addtext"))
                    {
                        string[] parts = code.Split(new char[] { '(', ')', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        name = GetName("Text", parts[0]);
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
                        elt.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(eltPreviewMouseLeftButtonDown);
                        shape = new Shape(elt);
                        canvas.Children.Add(shape.shape);
                    }
                    else if (codeLower.Contains("shapes.addimage"))
                    {
                        string[] parts = code.Split(new char[] { '(', ')', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        name = GetName("Image", parts[0]);
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
                            //Stretch = Stretch.Fill,
                        };
                        elt.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(eltPreviewMouseLeftButtonDown);
                        shape = new Shape(elt);
                        canvas.Children.Add(shape.shape);
                    }
                    else if (codeLower.Contains("controls.addbutton"))
                    {
                        string[] parts = code.Split(new char[] { '(', ')', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        name = GetName("Button", parts[0]);
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
                        elt.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(eltPreviewMouseLeftButtonDown);
                        shape = new Shape(elt);
                        canvas.Children.Add(shape.shape);
                        shape.modifiers["Left"] = parts[2];
                        shape.modifiers["Top"] = parts[3];
                    }
                    else if (codeLower.Contains("controls.addtextbox"))
                    {
                        string[] parts = code.Split(new char[] { '(', ')', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        name = GetName("TextBox", parts[0]);
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
                        elt.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(eltPreviewMouseLeftButtonDown);
                        shape = new Shape(elt);
                        canvas.Children.Add(shape.shape);
                        shape.modifiers["Left"] = parts[1];
                        shape.modifiers["Top"] = parts[2];
                    }
                    else if (codeLower.Contains("controls.addmultilinetextbox"))
                    {
                        string[] parts = code.Split(new char[] { '(', ')', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        name = GetName("MultiLineTextBox", parts[0]);
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
                        elt.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(eltPreviewMouseLeftButtonDown);
                        shape = new Shape(elt);
                        canvas.Children.Add(shape.shape);
                        shape.modifiers["Left"] = parts[1];
                        shape.modifiers["Top"] = parts[2];
                    }
                    else if (codeLower.Contains("ldcontrols.addbrowser"))
                    {
                        string[] parts = code.Split(new char[] { '(', ')', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        name = GetName("Browser", parts[0]);
                        double.TryParse(parts[1], out value[0]);
                        double.TryParse(parts[2], out value[1]);
                        WebBrowser webBrowser = new WebBrowser()
                        {
                            Name = name,
                            Width = value[0],
                            Height = value[1],
                        };
                        try
                        {
                            webBrowser.Navigate(new Uri(parts[3].Replace("\"", "")));
                        }
                        catch
                        {

                        }
                        elt = webBrowser;
                        elt.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(eltPreviewMouseLeftButtonDown);
                        shape = new Shape(elt);
                        canvas.Children.Add(shape.shape);
                    }
                    else if (codeLower.Contains("ldcontrols.addcheckbox"))
                    {
                        string[] parts = code.Split(new char[] { '(', ')', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        name = GetName("CheckBox", parts[0]);
                        elt = new CheckBox()
                        {
                            Name = name,
                            Content = parts[1].Replace("\"", ""),
                        };
                        elt.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(eltPreviewMouseLeftButtonDown);
                        shape = new Shape(elt);
                        canvas.Children.Add(shape.shape);
                    }
                    else if (codeLower.Contains("ldcontrols.addcombobox"))
                    {
                        string[] parts = code.Split(new char[] { '(', ')', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        double.TryParse(parts[2], out value[0]);
                        double.TryParse(parts[3], out value[1]);
                        name = GetName("ComboBox", parts[0]);
                        elt = new ComboBox()
                        {
                            Name = name,
                            Width = value[0],
                            MaxDropDownHeight = value[1],
                        };
                        currentElt = elt;
                        UpdateProperty(new PropertyData() { Property = "List" }, parts[1].Replace("\"", ""));
                        elt.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(eltPreviewMouseLeftButtonDown);
                        shape = new Shape(elt);
                        canvas.Children.Add(shape.shape);
                    }
                    else if (codeLower.Contains("ldcontrols.adddataview"))
                    {
                        string[] parts = code.Split(new char[] { '(', ')', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        double.TryParse(parts[1], out value[0]);
                        double.TryParse(parts[2], out value[1]);
                        name = GetName("DataView", parts[0]);
                        WindowsFormsHost windowsFormsHost = new WindowsFormsHost()
                        {
                            Name = name,
                            Width = value[0],
                            Height = value[1],
                        };
                        System.Windows.Forms.DataGridView dataView = new System.Windows.Forms.DataGridView();
                        dataView.AutoResizeColumns(System.Windows.Forms.DataGridViewAutoSizeColumnsMode.AllCells);
                        dataView.RowHeadersWidthSizeMode = System.Windows.Forms.DataGridViewRowHeadersWidthSizeMode.AutoSizeToAllHeaders;
                        windowsFormsHost.Child = dataView;
                        elt = windowsFormsHost;
                        currentElt = elt;
                        UpdateProperty(new PropertyData() { Property = "Headings" }, parts[3].Replace("\"", ""));
                        elt.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(eltPreviewMouseLeftButtonDown);
                        shape = new Shape(elt);
                        canvas.Children.Add(shape.shape);
                    }
                    else if (codeLower.Contains("ldcontrols.adddocumentviewer"))
                    {
                        string[] parts = code.Split(new char[] { '(', ')', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        double.TryParse(parts[1], out value[0]);
                        double.TryParse(parts[2], out value[1]);
                        name = GetName("DocumentViewer", parts[0]);
                        elt = new DocumentViewer()
                        {
                            Name = name,
                            Width = value[0],
                            Height = value[1],
                        };
                        elt.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(eltPreviewMouseLeftButtonDown);
                        shape = new Shape(elt);
                        canvas.Children.Add(shape.shape);
                    }
                    else if (codeLower.Contains("ldcontrols.addlistview"))
                    {
                        string[] parts = code.Split(new char[] { '(', ')', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        double.TryParse(parts[1], out value[0]);
                        double.TryParse(parts[2], out value[1]);
                        name = GetName("ListView", parts[0]);
                        ListView listView = new ListView()
                        {
                            Name = name,
                            Width = value[0],
                            Height = value[1],
                        };
                        GridView gridView = new GridView();
                        listView.View = gridView;
                        Style style = new Style(typeof(ListViewItem));
                        style.Setters.Add(new Setter(ListViewItem.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
                        listView.ItemContainerStyle = style;
                        elt = listView;
                        currentElt = elt;
                        UpdateProperty(new PropertyData() { Property = "Headings" }, parts[3].Replace("\"", ""));
                        elt.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(eltPreviewMouseLeftButtonDown);
                        shape = new Shape(elt);
                        canvas.Children.Add(shape.shape);
                    }
                    else if (codeLower.Contains("ldcontrols.addmediaplayer"))
                    {
                        string[] parts = code.Split(new char[] { '(', ')', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        double.TryParse(parts[1], out value[0]);
                        double.TryParse(parts[2], out value[1]);
                        name = GetName("MediaPlayer", parts[0]);
                        elt = new MediaElement()
                        {
                            Name = name,
                            Width = value[0],
                            Height = value[1],
                        };
                        elt.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(eltPreviewMouseLeftButtonDown);
                        shape = new Shape(elt);
                        canvas.Children.Add(shape.shape);
                    }
                    else if (codeLower.Contains("ldcontrols.addmenu"))
                    {
                        string[] parts = code.Split(new char[] { '(', ')', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        double.TryParse(parts[1], out value[0]);
                        double.TryParse(parts[2], out value[1]);
                        name = GetName("Menu", parts[0]);
                        elt = new Menu()
                        {
                            Name = name,
                            Width = value[0],
                            Height = value[1],
                        };
                        currentElt = elt;
                        UpdateProperty(new PropertyData() { Property = "MenuList" }, parts[3].Replace("\"", ""));
                        UpdateProperty(new PropertyData() { Property = "IconList" }, parts[4].Replace("\"", ""));
                        UpdateProperty(new PropertyData() { Property = "CheckList" }, parts[5].Replace("\"", ""));
                        elt.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(eltPreviewMouseLeftButtonDown);
                        shape = new Shape(elt);
                        canvas.Children.Add(shape.shape);
                    }
                    else if (codeLower.Contains("ldcontrols.addpasswordbox"))
                    {
                        string[] parts = code.Split(new char[] { '(', ')', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        double.TryParse(parts[1], out value[0]);
                        double.TryParse(parts[2], out value[1]);
                        name = GetName("PasswordBox", parts[0]);
                        elt = new PasswordBox()
                        {
                            Name = name,
                            Width = value[0],
                            Height = value[1],
                        };
                        currentElt = elt;
                        UpdateProperty(new PropertyData() { Property = "MaxLength" }, parts[3].Replace("\"", ""));
                        elt.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(eltPreviewMouseLeftButtonDown);
                        shape = new Shape(elt);
                        canvas.Children.Add(shape.shape);
                    }
                    else if (codeLower.Contains("ldcontrols.addprogressbar"))
                    {
                        string[] parts = code.Split(new char[] { '(', ')', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        double.TryParse(parts[1], out value[0]);
                        double.TryParse(parts[2], out value[1]);
                        name = GetName("ProgressBar", parts[0]);
                        elt = new ProgressBar()
                        {
                            Name = name,
                            Width = value[0],
                            Height = value[1],
                        };
                        currentElt = elt;
                        UpdateProperty(new PropertyData() { Property = "Orientation" }, parts[3].Replace("\"", ""));
                        elt.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(eltPreviewMouseLeftButtonDown);
                        shape = new Shape(elt);
                        canvas.Children.Add(shape.shape);
                    }
                    else if (codeLower.Contains("ldcontrols.addradiobutton"))
                    {
                        string[] parts = code.Split(new char[] { '(', ')', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        name = GetName("RadioButton", parts[0]);
                        elt = new RadioButton()
                        {
                            Name = name,
                            Content = parts[1].Replace("\"", ""),
                            GroupName = parts[2].Replace("\"", ""),
                        };
                        elt.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(eltPreviewMouseLeftButtonDown);
                        shape = new Shape(elt);
                        canvas.Children.Add(shape.shape);
                    }
                    else if (codeLower.Contains("ldcontrols.addrichtextbox"))
                    {
                        string[] parts = code.Split(new char[] { '(', ')', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        double.TryParse(parts[1], out value[0]);
                        double.TryParse(parts[2], out value[1]);
                        name = GetName("RichTextBox", parts[0]);
                        elt = new RichTextBox()
                        {
                            Name = name,
                            Width = value[0],
                            Height = value[1],
                            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                        };
                        elt.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(eltPreviewMouseLeftButtonDown);
                        shape = new Shape(elt);
                        canvas.Children.Add(shape.shape);
                    }
                    else if (codeLower.Contains("ldcontrols.addslider"))
                    {
                        string[] parts = code.Split(new char[] { '(', ')', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        double.TryParse(parts[1], out value[0]);
                        double.TryParse(parts[2], out value[1]);
                        name = GetName("Slider", parts[0]);
                        elt = new Slider()
                        {
                            Name = name,
                            Width = value[0],
                            Height = value[1],
                        };
                        currentElt = elt;
                        UpdateProperty(new PropertyData() { Property = "Orientation" }, parts[3].Replace("\"", ""));
                        elt.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(eltPreviewMouseLeftButtonDown);
                        shape = new Shape(elt);
                        canvas.Children.Add(shape.shape);
                    }
                    else if (codeLower.Contains("ldcontrols.addtreeview"))
                    {
                        string[] parts = code.Split(new char[] { '(', ')', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        double.TryParse(parts[2], out value[0]);
                        double.TryParse(parts[3], out value[1]);
                        name = GetName("TreeView", parts[0]);
                        elt = new TreeView()
                        {
                            Name = name,
                            Width = value[0],
                            Height = value[1],
                        };
                        currentElt = elt;
                        UpdateProperty(new PropertyData() { Property = "Tree" }, parts[1].Replace("\"", ""));
                        elt.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(eltPreviewMouseLeftButtonDown);
                        shape = new Shape(elt);
                        canvas.Children.Add(shape.shape);
                    }
                    else if (codeLower.Contains("graphicswindow.brushcolor"))
                    {
                        string[] parts = code.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
                        _brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(parts[1].Replace("\"", "")));
                    }
                    else if (codeLower.Contains("graphicswindow.pencolor"))
                    {
                        string[] parts = code.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
                        _pen.Brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(parts[1].Replace("\"", "")));
                    }
                    else if (codeLower.Contains("graphicswindow.penwidth"))
                    {
                        string[] parts = code.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
                        double.TryParse(parts[1], out value[0]);
                        _pen.Thickness = value[0];
                    }
                    else if (codeLower.Contains("graphicswindow.fontname"))
                    {
                        string[] parts = code.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
                        _fontFamily = new FontFamily(parts[1].Replace("\"", ""));
                    }
                    else if (codeLower.Contains("graphicswindow.fontitalic"))
                    {
                        string[] parts = code.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
                        _fontStyle = parts[1].Replace("\"", "") == "true" ? FontStyles.Italic : FontStyles.Normal;
                    }
                    else if (codeLower.Contains("graphicswindow.fontbold"))
                    {
                        string[] parts = code.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
                        _fontWeight = parts[1].Replace("\"", "") == "true" ? FontWeights.Bold : FontWeights.Normal;
                    }
                    else if (codeLower.Contains("graphicswindow.fontsize"))
                    {
                        string[] parts = code.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
                        double.TryParse(parts[1], out value[0]);
                        _fontSize = value[0];
                    }
                    else if (codeLower.Contains("shapes.move"))
                    {
                        string[] parts = code.Split(new char[] { '(', ')', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        shape.modifiers["Left"] = parts[2];
                        shape.modifiers["Top"] = parts[3];
                    }
                    else if (codeLower.Contains("controls.setsize"))
                    {
                        string[] parts = code.Split(new char[] { '(', ')', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        shape.modifiers["Width"] = parts[2];
                        shape.modifiers["Height"] = parts[3];
                        shape.elt.Width = double.Parse(parts[2]);
                        shape.elt.Height = double.Parse(parts[3]);
                    }
                    else if (codeLower.Contains("shapes.setopacity"))
                    {
                        string[] parts = code.Split(new char[] { '(', ')', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        shape.modifiers["Opacity"] = parts[2];
                        shape.elt.Opacity = double.Parse(parts[2]) / 100.0;
                    }
                    else if (codeLower.Contains("shapes.rotate"))
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
                        shape.elt.Measure(new Size(double.MaxValue, double.MaxValue));
                        if (!shape.modifiers.ContainsKey("Width")) shape.modifiers["Width"] = shape.elt.DesiredSize.Width.ToString();
                        if (!shape.modifiers.ContainsKey("Height")) shape.modifiers["Height"] = shape.elt.DesiredSize.Height.ToString();
                        if (!shape.modifiers.ContainsKey("Left")) shape.modifiers["Left"] = "0";
                        if (!shape.modifiers.ContainsKey("Top")) shape.modifiers["Top"] = "0";
                        if (!shape.modifiers.ContainsKey("Angle")) shape.modifiers["Angle"] = "0";
                        if (!shape.modifiers.ContainsKey("Opacity")) shape.modifiers["Opacity"] = "100";
                        Canvas.SetLeft(shape.shape, double.Parse(shape.modifiers["Left"]) - Shape.HandleShort);
                        Canvas.SetTop(shape.shape, double.Parse(shape.modifiers["Top"]) - Shape.HandleShort);
                    }
                }
                canvas.UpdateLayout();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Shapes Editor failed to import some shapes.", "SB-IDE", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private class Shape
        {
            public static int HandleShort = 5;

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

                Grid grid = new Grid();
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
            public Visibility Visible { get; set; }
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
                        //Stretch = Stretch.Fill,
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
                case "Browser":
                    WebBrowser webBrowser = new WebBrowser()
                    {
                        Name = name,
                        Width = 100,
                        Height = 100,
                    };
                    //TODO
                    webBrowser.Navigate(new Uri("http://www.smallbasic.com"));
                    elt = webBrowser;
                    break;
                case "CheckBox":
                    elt = new CheckBox()
                    {
                        Name = name,
                        Content = label,
                    };
                    break;
                case "ComboBox":
                    ComboBox comboBox = new ComboBox()
                    {
                        Name = name,
                        Width = 100,
                        MaxDropDownHeight = 100,
                    };
                    ComboBoxItem comboBoxItem = new ComboBoxItem();
                    comboBoxItem.Content = "Item1";
                    comboBox.Items.Add(comboBoxItem);
                    elt = comboBox;
                    break;
                case "DataView":
                    WindowsFormsHost windowsFormsHost = new WindowsFormsHost()
                    {
                        Name = name,
                        Width = 100,
                        Height = 100,
                    };
                    System.Windows.Forms.DataGridView dataView = new System.Windows.Forms.DataGridView();
                    dataView.AutoResizeColumns(System.Windows.Forms.DataGridViewAutoSizeColumnsMode.AllCells);
                    dataView.RowHeadersWidthSizeMode = System.Windows.Forms.DataGridViewRowHeadersWidthSizeMode.AutoSizeToAllHeaders;
                    dataView.Columns.Add("1", "Heading1");
                    windowsFormsHost.Child = dataView;
                    elt = windowsFormsHost;
                    //TODO
                    break;
                case "DocumentViewer":
                    elt = new DocumentViewer()
                    {
                        Name = name,
                        Width = 100,
                        Height = 100,
                    };
                    break;
                case "ListView":
                    ListView listView = new ListView()
                    {
                        Name = name,
                        Width = 100,
                        Height = 100,
                    };
                    GridView gridView = new GridView();
                    listView.View = gridView;
                    Style style = new Style(typeof(ListViewItem));
                    style.Setters.Add(new Setter(ListViewItem.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
                    listView.ItemContainerStyle = style;
                    GridViewColumn col = new GridViewColumn();
                    GridViewColumnHeader header = new GridViewColumnHeader();
                    header.Content = "Heading1";
                    col.Header = header;
                    col.Width = Double.NaN;
                    gridView.Columns.Add(col);
                    elt = listView;
                    break;
                case "MediaPlayer":
                    elt = new MediaElement()
                    {
                        Name = name,
                        Width = 100,
                        Height = 100,
                    };
                    //TODO
                    break;
                case "Menu":
                    Menu menu = new Menu()
                    {
                        Name = name,
                    };
                    MenuItem menuItem = new MenuItem();
                    menuItem.Header = "Header1";
                    menuItem.Name = "Main";
                    menu.Items.Add(menuItem);
                    elt = menu;
                    break;
                case "PasswordBox":
                    elt = new PasswordBox()
                    {
                        Name = name,
                        MaxLength = 100,
                        Width = 100,
                    };
                    break;
                case "ProgressBar":
                    elt = new ProgressBar()
                    {
                        Name = name,
                        Width = 100,
                        Orientation = Orientation.Horizontal,
                    };
                    break;
                case "RadioButton":
                    elt = new RadioButton()
                    {
                        Name = name,
                        Content = label,
                        GroupName = "Group1",
                    };
                    break;
                case "RichTextBox":
                    elt = new RichTextBox()
                    {
                        Name = name,
                        Width = 100,
                        Height = 100,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    };
                    break;
                case "Slider":
                    elt = new Slider()
                    {
                        Name = name,
                        Width = 100,
                        Orientation = Orientation.Horizontal,
                    };
                    break;
                case "TreeView":
                    TreeView treeView = new TreeView()
                    {
                        Name = name,
                        Width = 100,
                        Height = 100,
                    };
                    TreeViewItem treeViewItem = new TreeViewItem();
                    treeViewItem.Header = "Item1";
                    treeView.Items.Add(treeViewItem);
                    elt = treeView;
                    break;
            }

            elt.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(eltPreviewMouseLeftButtonDown);
            Shape shape = new Shape(elt);
            canvas.Children.Add(shape.shape);
            Canvas.SetLeft(shape.shape, 100 - Shape.HandleShort);
            Canvas.SetTop(shape.shape, 100 - Shape.HandleShort);

            eltPreviewMouseLeftButtonDown(elt, null);
        }

        private void dataGridProperties_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            try
            {
                PropertyData property = (PropertyData)e.Row.Item;
                TextBox tb = (TextBox)e.EditingElement;
                UpdateProperty(property, tb.Text);
                ShowCode();
            }
            catch
            {

            }
        }

        private void UpdateProperty(PropertyData property, string value)
        {
            try
            {
                if (currentElt.GetType() == typeof(Rectangle))
                {
                    Rectangle shape = (Rectangle)currentElt;
                    switch (property.Property)
                    {
                        case "Name":
                            shape.Name = value;
                            break;
                        case "Fill":
                            shape.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
                            break;
                        case "Stroke":
                            shape.Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
                            break;
                        case "StrokeThickness":
                            shape.StrokeThickness = double.Parse(value);
                            break;
                    }
                }
                else if (currentElt.GetType() == typeof(Ellipse))
                {
                    Ellipse shape = (Ellipse)currentElt;
                    switch (property.Property)
                    {
                        case "Name":
                            shape.Name = value;
                            break;
                        case "Fill":
                            shape.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
                            break;
                        case "Stroke":
                            shape.Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
                            break;
                        case "StrokeThickness":
                            shape.StrokeThickness = double.Parse(value);
                            break;
                    }
                }
                else if (currentElt.GetType() == typeof(Polygon))
                {
                    Polygon shape = (Polygon)currentElt;
                    switch (property.Property)
                    {
                        case "Name":
                            shape.Name = value;
                            break;
                        case "Fill":
                            shape.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
                            break;
                        case "Stroke":
                            shape.Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
                            break;
                        case "StrokeThickness":
                            shape.StrokeThickness = double.Parse(value);
                            break;
                        default:
                            int i = int.Parse(property.Property.Substring(1)) - 1;
                            if (property.Property.StartsWith("X")) shape.Points[i] = new Point(double.Parse(value), shape.Points[i].Y);
                            else if (property.Property.StartsWith("Y")) shape.Points[i] = new Point(shape.Points[i].X, double.Parse(value));
                            break;

                    }
                }
                else if (currentElt.GetType() == typeof(Line))
                {
                    Line shape = (Line)currentElt;
                    switch (property.Property)
                    {
                        case "Name":
                            shape.Name = value;
                            break;
                        case "X1":
                            shape.X1 = double.Parse(value);
                            break;
                        case "Y1":
                            shape.Y1 = double.Parse(value);
                            break;
                        case "X2":
                            shape.X2 = double.Parse(value);
                            break;
                        case "Y2":
                            shape.Y2 = double.Parse(value);
                            break;
                        case "Stroke":
                            shape.Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
                            break;
                        case "StrokeThickness":
                            shape.StrokeThickness = double.Parse(value);
                            break;
                    }
                }
                else if (currentElt.GetType() == typeof(TextBlock))
                {
                    TextBlock shape = (TextBlock)currentElt;
                    switch (property.Property)
                    {
                        case "Name":
                            shape.Name = value;
                            break;
                        case "Text":
                            shape.Text = value;
                            break;
                        case "Foreground":
                            shape.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
                            break;
                        case "FontFamily":
                            shape.FontFamily = new FontFamily(value);
                            break;
                        case "FontStyle":
                            shape.FontStyle = value.ToLower() == "italic" ? FontStyles.Italic : FontStyles.Normal;
                            break;
                        case "FontSize":
                            shape.FontSize = double.Parse(value);
                            break;
                        case "FontWeight":
                            shape.FontWeight = value.ToLower() == "bold" ? FontWeights.Bold : FontWeights.Normal;
                            break;
                    }
                }
                else if (currentElt.GetType() == typeof(Image))
                {
                    Image shape = (Image)currentElt;
                    switch (property.Property)
                    {
                        case "Name":
                            shape.Name = value;
                            break;
                        case "Source":
                            shape.Source = new BitmapImage(new Uri(value));
                            break;
                    }
                }
                else if (currentElt.GetType() == typeof(Button))
                {
                    Button shape = (Button)currentElt;
                    switch (property.Property)
                    {
                        case "Name":
                            shape.Name = value;
                            break;
                        case "Content":
                            shape.Content = value;
                            break;
                        case "Foreground":
                            shape.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
                            break;
                        case "FontFamily":
                            shape.FontFamily = new FontFamily(value);
                            break;
                        case "FontStyle":
                            shape.FontStyle = value.ToLower() == "italic" ? FontStyles.Italic : FontStyles.Normal;
                            break;
                        case "FontSize":
                            shape.FontSize = double.Parse(value);
                            break;
                        case "FontWeight":
                            shape.FontWeight = value.ToLower() == "bold" ? FontWeights.Bold : FontWeights.Normal;
                            break;
                    }
                }
                else if (currentElt.GetType() == typeof(TextBox))
                {
                    TextBox shape = (TextBox)currentElt;
                    switch (property.Property)
                    {
                        case "Name":
                            shape.Name = value;
                            break;
                        case "Text":
                            shape.Text = value;
                            break;
                        case "Foreground":
                            shape.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
                            break;
                        case "FontFamily":
                            shape.FontFamily = new FontFamily(value);
                            break;
                        case "FontStyle":
                            shape.FontStyle = value.ToLower() == "italic" ? FontStyles.Italic : FontStyles.Normal;
                            break;
                        case "FontSize":
                            shape.FontSize = double.Parse(value);
                            break;
                        case "FontWeight":
                            shape.FontWeight = value.ToLower() == "bold" ? FontWeights.Bold : FontWeights.Normal;
                            break;
                    }
                }
                else if (currentElt.GetType() == typeof(WebBrowser))
                {
                    WebBrowser shape = (WebBrowser)currentElt;
                    switch (property.Property)
                    {
                        case "Url":
                            shape.Navigate(new Uri(value));
                            break;
                    }
                }
                else if (currentElt.GetType() == typeof(CheckBox))
                {
                    CheckBox shape = (CheckBox)currentElt;
                    switch (property.Property)
                    {
                        case "Content":
                            shape.Content = value;
                            break;
                    }
                }
                else if (currentElt.GetType() == typeof(ComboBox))
                {
                    ComboBox shape = (ComboBox)currentElt;
                    switch (property.Property)
                    {
                        case "List":
                            string[] list = value.Split(new char[] { '=', ';' }, StringSplitOptions.RemoveEmptyEntries);
                            shape.Items.Clear();
                            for (int i = 1; i < list.Length; i += 2)
                            {
                                ComboBoxItem comboBoxItem = new ComboBoxItem();
                                comboBoxItem.Content = list[i];
                                shape.Items.Add(comboBoxItem);
                            }
                            break;
                        case "DropDownHeight":
                            shape.MaxDropDownHeight = double.Parse(value);
                            break;
                    }
                }
                else if (currentElt.GetType() == typeof(WindowsFormsHost))
                {
                    WindowsFormsHost shape = (WindowsFormsHost)currentElt;
                    System.Windows.Forms.DataGridView dataView = (System.Windows.Forms.DataGridView)shape.Child;
                    switch (property.Property)
                    {
                        case "Headings":
                            string[] headings = value.Split(new char[] { '=', ';' }, StringSplitOptions.RemoveEmptyEntries);
                            dataView.Columns.Clear();
                            for (int i = 1; i < headings.Length; i += 2)
                            {
                                dataView.Columns.Add(i.ToString(), headings[i]);
                            }
                            break;
                    }
                }
                else if (currentElt.GetType() == typeof(DocumentViewer))
                {
                    DocumentViewer shape = (DocumentViewer)currentElt;
                }
                else if (currentElt.GetType() == typeof(ListView))
                {
                    ListView shape = (ListView)currentElt;
                    GridView gridView = (GridView)shape.View;
                    switch (property.Property)
                    {
                        case "Headings":
                            string[] headings = value.Split(new char[] { '=', ';' }, StringSplitOptions.RemoveEmptyEntries);
                            gridView.Columns.Clear();
                            for (int i = 1; i < headings.Length; i += 2)
                            {
                                GridViewColumn col = new GridViewColumn();
                                GridViewColumnHeader header = new GridViewColumnHeader();
                                header.Content = headings[i];
                                col.Header = header;
                                col.Width = Double.NaN;
                                gridView.Columns.Add(col);
                            }
                            break;
                    }
                }
                else if (currentElt.GetType() == typeof(MediaElement))
                {
                    MediaElement shape = (MediaElement)currentElt;
                }
                else if (currentElt.GetType() == typeof(Menu))
                {
                    Menu shape = (Menu)currentElt;
                    switch (property.Property)
                    {
                        case "MenuList":
                            string[] listMenu = value.Split(new char[] { '=', ';' }, StringSplitOptions.RemoveEmptyEntries);
                            shape.Items.Clear();
                            for (int i = 0; i < listMenu.Length; i += 2)
                            {
                                string header = listMenu[i];
                                string parent = listMenu[i + 1];
                                MenuItem menuItem = new MenuItem();
                                menuItem.Header = header;
                                menuItem.Name = parent;
                                if (parent.ToLower() == "main")
                                {
                                    shape.Items.Add(menuItem);
                                }
                                else
                                {
                                    if (header.StartsWith("-"))
                                    {
                                        findMenuItem(shape.Items, parent).Items.Add(new Separator() { Name = parent });
                                    }
                                    else
                                    {
                                        findMenuItem(shape.Items, parent).Items.Add(menuItem);
                                    }
                                }
                            }
                            break;
                    }
                }
                else if (currentElt.GetType() == typeof(PasswordBox))
                {
                    PasswordBox shape = (PasswordBox)currentElt;
                    switch (property.Property)
                    {
                        case "MaxLength":
                            shape.MaxLength = int.Parse(value);
                            break;
                    }
                }
                else if (currentElt.GetType() == typeof(ProgressBar))
                {
                    ProgressBar shape = (ProgressBar)currentElt;
                    switch (property.Property)
                    {
                        case "Orientation":
                            shape.Orientation = value.ToLower() == "vertical" ? Orientation.Vertical : Orientation.Horizontal;
                            break;
                    }
                }
                else if (currentElt.GetType() == typeof(RadioButton))
                {
                    RadioButton shape = (RadioButton)currentElt;
                    switch (property.Property)
                    {
                        case "Title":
                            shape.Content = value;
                            break;
                        case "Group":
                            shape.GroupName = value;
                            break;
                    }
                }
                else if (currentElt.GetType() == typeof(RichTextBox))
                {
                    RichTextBox shape = (RichTextBox)currentElt;
                }
                else if (currentElt.GetType() == typeof(Slider))
                {
                    Slider shape = (Slider)currentElt;
                    switch (property.Property)
                    {
                        case "Orientation":
                            shape.Orientation = value.ToLower() == "vertical" ? Orientation.Vertical : Orientation.Horizontal;
                            break;
                    }
                }
                else if (currentElt.GetType() == typeof(TreeView))
                {
                    TreeView shape = (TreeView)currentElt;
                    switch (property.Property)
                    {
                        case "Tree":
                            string[] items = value.Split(new char[] { '=', ';', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                            shape.Items.Clear();
                            for (int i = 0; i < items.Length; i += 3)
                            {
                                if (items[i + 1] == "0")
                                {
                                    TreeViewItem treeViewItem = new TreeViewItem();
                                    treeViewItem.Header = items[i + 2];
                                    treeViewItem.Name = "Index" + items[i];
                                    shape.Items.Add(treeViewItem);
                                }
                                else
                                {
                                    TreeViewItem treeViewItem = new TreeViewItem();
                                    treeViewItem.Header = items[i + 2];
                                    treeViewItem.Name = "Index" + items[i];
                                    findTreeItem(shape.Items, "Index" + items[i + 1]).Items.Add(treeViewItem);
                                }
                            }
                            break;
                    }
                }
                canvas.UpdateLayout();
            }
            catch
            {

            }
        }

        private static TreeViewItem findTreeItem(ItemCollection items, string name)
        {
            foreach (TreeViewItem i in items)
            {
                TreeViewItem children = findTreeItem(i.Items, name);
                if (null != children) return children;
                if (i.Name == name) return i;
            }
            return null;
        }

        private static MenuItem findMenuItem(ItemCollection items, string parent)
        {
            parent = parent.ToLower();
            foreach (Object i in items)
            {
                if (i.GetType() == typeof(MenuItem))
                {
                    MenuItem item = (MenuItem)i;
                    MenuItem children = findMenuItem(item.Items, parent);
                    if (null != children) return children;
                    if (((string)item.Header).ToLower() == parent) return item;
                }
            }
            return null;
        }

        private void dataGridModifiers_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            try
            {
                PropertyData property = (PropertyData)e.Row.Item;
                TextBox tb = (TextBox)e.EditingElement;
                UpdateModifier(property, tb.Text);
                ShowCode();
            }
            catch
            {

            }
        }

        private void UpdateModifier(PropertyData property, string value)
        {
            try
            {
                switch (property.Property)
                {
                    case "Width":
                        currentShape.modifiers["Width"] = value;
                        currentElt.Width = double.Parse(value);
                        break;
                    case "Height":
                        currentShape.modifiers["Height"] = value;
                        currentElt.Height = double.Parse(value);
                        break;
                    case "Left":
                        currentShape.modifiers["Left"] = value;
                        Canvas.SetLeft(currentShape.shape, double.Parse(value) - Shape.HandleShort);
                        break;
                    case "Top":
                        currentShape.modifiers["Top"] = value;
                        Canvas.SetTop(currentShape.shape, double.Parse(value) - Shape.HandleShort);
                        break;
                    case "Angle":
                        currentShape.modifiers["Angle"] = value;
                        RotateTransform rotateTransform = new RotateTransform();
                        rotateTransform.CenterX = currentElt.ActualWidth / 2.0;
                        rotateTransform.CenterY = currentElt.ActualHeight / 2.0;
                        rotateTransform.Angle = double.Parse(value);
                        currentElt.RenderTransform = new TransformGroup();
                        ((TransformGroup)currentElt.RenderTransform).Children.Add(rotateTransform);
                        break;
                    case "Opacity":
                        currentShape.modifiers["Opacity"] = value;
                        currentElt.Opacity = double.Parse(value) / 100.0;
                        break;
                }
                canvas.UpdateLayout();
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

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                for (int i = 0; i < canvas.Children.Count; i++)
                {
                    FrameworkElement child = (FrameworkElement)canvas.Children[i];
                    if (child.GetType() == typeof(Grid))
                    {
                        Grid grid = (Grid)child;
                        if (grid.Children.Count > 0 && grid.Children[0].IsMouseDirectlyOver)
                        {
                            FrameworkElement elt = (FrameworkElement)grid.Children[0];
                            eltPreviewMouseLeftButtonDown(elt, null);
                            Delete();
                            break;
                        }
                    }
                }
            }
        }

        private void sliderScale_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                scale = Math.Pow(4.0, sliderScale.Value / 10.0);
                scaleTransform.ScaleX = scale;
                scaleTransform.ScaleY = scale;
                visualGrid.Width = canvas.Width * scale;
                visualGrid.Height = canvas.Height * scale;
                scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset * scale);
                scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset * scale);
            }
            catch
            {

            }
        }

        private void dataGridPropertySet(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            try
            {
                PropertyData property = (PropertyData)button.Tag;
                if (null != property)
                {
                    if (property.Property == "Fill" || property.Property == "Stroke" || property.Property == "Foreground")
                    {
                        System.Windows.Forms.ColorDialog cd = new System.Windows.Forms.ColorDialog();
                        Color color = (Color)ColorConverter.ConvertFromString(property.Value);
                        cd.Color = System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);
                        cd.AnyColor = true;
                        cd.SolidColorOnly = true;
                        cd.FullOpen = true;
                        if (cd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                        {
                            property.Value = ColorName(Color.FromArgb(cd.Color.A, cd.Color.R, cd.Color.G, cd.Color.B));
                            UpdateProperty(property, property.Value);
                            dataGridProperties.Items.Refresh();
                        }
                    }
                    else if (property.Property == "FontFamily" || property.Property == "FontStyle" || property.Property == "FontWeight" || property.Property == "FontSize")
                    {
                        string _fontFamily = fontFamily.FamilyNames.Values.First();
                        string _fontStyle = fontStyle.ToString();
                        string _fontWeight = fontWeight.ToString();
                        string _fontSize = fontSize.ToString();
                        foreach (PropertyData data in properties)
                        {
                            if (data.Property == "FontFamily") _fontFamily = data.Value;
                            else if (data.Property == "FontStyle") _fontStyle = data.Value;
                            else if (data.Property == "FontWeight") _fontWeight = data.Value;
                            else if (data.Property == "FontSize") _fontSize = data.Value;
                        }
                        System.Windows.Forms.FontDialog fd = new System.Windows.Forms.FontDialog();
                        System.Drawing.FontStyle style = System.Drawing.FontStyle.Regular;
                        if (_fontWeight.ToLower() == "bold") style |= System.Drawing.FontStyle.Bold;
                        if (_fontStyle.ToLower() == "italic") style |= System.Drawing.FontStyle.Italic;
                        fd.Font = new System.Drawing.Font(_fontFamily, float.Parse(_fontSize), style);
                        fd.ShowColor = false;
                        fd.ShowEffects = false;
                        if (fd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                        {
                            foreach (PropertyData data in properties)
                            {
                                if (data.Property == "FontFamily") data.Value = fd.Font.Name;
                                else if (data.Property == "FontStyle") data.Value = fd.Font.Italic ? "Italic" : "Normal";
                                else if (data.Property == "FontWeight") data.Value = fd.Font.Bold ? "Bold" : "Normal";
                                else if (data.Property == "FontSize") data.Value = fd.Font.Size.ToString();
                                UpdateProperty(data, data.Value);
                            }
                            dataGridProperties.Items.Refresh();
                        }
                    }
                    else if (property.Property == "Source")
                    {
                        System.Windows.Forms.OpenFileDialog fd = new System.Windows.Forms.OpenFileDialog();
                        fd.Filter = "Png (*.png)|*.png|Jpj (*.jpg)|*.jpg|All files (*.*)|*.*";
                        fd.FilterIndex = 1;
                        fd.RestoreDirectory = true;
                        if (fd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                        {
                            property.Value = fd.FileName;
                            UpdateProperty(property, property.Value);
                            dataGridProperties.Items.Refresh();
                        }
                    }
                }
            }
            catch
            {

            }
        }
    }
}
