using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
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

        private List<Shape> selectedShapes = new List<Shape>();
        private Shape currentShape = null;
        private FrameworkElement currentElt = null;

        private Point startGlobal;
        private Point startLocal;
        private Point currentPosition;
        private double startWidth;
        private double startHeight;

        private List<string> names;
        private Brush background;
        private Brush foreground;
        private Brush brush;
        private Pen pen;
        private FontFamily fontFamily;
        private FontStyle fontStyle;
        private double fontSize;
        private FontWeight fontWeight;

        private double fixDec = 0.1;
        private int snap = 10;
        private double scale = 1;
        private string mode = "_SEL";
        private int _PT = -1;
        private Rectangle rubberZoom;

        public ShapesEditor(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;
            THIS = this;

            InitializeComponent();

            Cursor cursor = Mouse.OverrideCursor;
            Mouse.OverrideCursor = Cursors.Wait;

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
            canvas.MouseDown += new MouseButtonEventHandler(canvasMouseDown);
            canvas.PreviewMouseRightButtonDown += new MouseButtonEventHandler(canvasPreviewMouseRightButtonDown);

            scaleTransform = new ScaleTransform();
            scaleTransform.CenterX = 0;
            scaleTransform.CenterY = 0;
            visualGrid.RenderTransform = new TransformGroup();
            ((TransformGroup)visualGrid.RenderTransform).Children.Add(scaleTransform);

            names = new List<string>();
            background = canvas.Background;
            foreground = new SolidColorBrush();
            HighContrast();
            brush = Brushes.SlateBlue;
            pen = new Pen(Brushes.Black, 2);
            fontFamily = new FontFamily("Tahoma");
            fontStyle = FontStyles.Normal;
            fontSize = 12.0;
            fontWeight = FontWeights.Bold;

            drawingGroup = new DrawingGroup();
            visualContainer = new VisualContainer(drawingGroup);
            visualGrid.Children.Add(visualContainer);

            Shape.HandlePT = null;
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
            sbDocument.Lexer.Theme = MainWindow.theme;
            panel.Contains(sbDocument.TextArea);
            host.Child = sbDocument.TextArea;
            codeGrid.Children.Add(host);

            contextMenu = new ContextMenu();
            canvas.ContextMenu = contextMenu;

            ToolTipService.SetShowDuration(buttonImport, 10000);

            rubberZoom = new Rectangle()
            {
                Width = 0,
                Height = 0,
                Fill = Brushes.Transparent,
                Stroke = foreground,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection() { 5, 5 },
                Visibility = Visibility.Hidden,
            };
            canvas.Children.Add(rubberZoom);

            Mouse.OverrideCursor = cursor;
            Display();
        }

        public void Display()
        {
        }

        public void SetStart(Point start)
        {
            startGlobal = start;
            if (null == currentShape) return;
            startLocal = Snap(currentShape.shape.TranslatePoint(new Point(Shape.HandleShort, Shape.HandleShort), canvas));
            if (currentElt.GetType() != typeof(CheckBox) && currentElt.GetType() != typeof(RadioButton))
            {
                currentElt.Measure(new Size(double.MaxValue, double.MaxValue));
                startWidth = currentElt.DesiredSize.Width;
                startHeight = currentElt.DesiredSize.Height;
                currentElt.Width = startWidth;
                currentElt.Height = startHeight;
            }
        }

        private void UpdateView()
        {
            if (null != currentShape)
            {
                try
                {
                    RotateShape(currentShape);
                    UpdatePolygonHandles();
                    currentShape.ShowHandles(true); //polygon handles on rotation
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

        private void Delete(Shape shape)
        {
            if (null == shape) return;

            shape.ShowHandles(false);
            canvas.Children.Remove(shape.shape);
            selectedShapes.Remove(shape);
            if (selectedShapes.Count == 0)
            {
                currentElt = null;
                currentShape = null;
            }
            else
            {
                currentShape = selectedShapes.Last();
                currentElt = currentShape.elt;
            }
            mode = "_SEL";
            UpdateView();
        }

        private void DeleteAll()
        {
            canvas.Children.Clear();
            names.Clear();

            currentElt = null;
            currentShape = null;
            selectedShapes.Clear();
            Shape.HandlePT = null;
            mode = "_SEL";
            UpdateView();
        }

        private void eltPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!Keyboard.IsKeyDown(Key.LeftShift) && !Keyboard.IsKeyDown(Key.RightShift))
            {
                foreach (Shape shape in selectedShapes) shape.ShowHandles(false);
                selectedShapes.Clear();
            }
            currentElt = (FrameworkElement)sender;
            currentShape = (Shape)currentElt.Tag;
            UpdatePolygonHandles();
            currentShape.ShowHandles(true);
            if (!selectedShapes.Contains(currentShape)) selectedShapes.Add(currentShape);

            if (null != e)
            {
                mode = mode == "_SEL" ? currentElt.Name : "_SEL";
                Cursor = Cursors.Hand;
                SetStart(e.GetPosition(canvas));
                e.Handled = true;
            }

            UpdateView();
        }

        private void canvasPreviewLeftMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (rubberZoom.Visibility == Visibility.Visible)
            {
                SelectRubberZoom();
                rubberZoom.Visibility = Visibility.Hidden;
                return;
            }
            if (null == currentShape) return;
            currentElt.MinWidth = 0;
            currentElt.MinHeight = 0;
            UpdatePolygonSize(currentElt);

            mode = "_SEL";
            Cursor = Cursors.Arrow;
            e.Handled = true;

            UpdateView();
        }

        private void SelectRubberZoom()
        {
            if (!Keyboard.IsKeyDown(Key.LeftShift) && !Keyboard.IsKeyDown(Key.RightShift)) selectedShapes.Clear();
            Rect rubberBounds = new Rect(Canvas.GetLeft(rubberZoom), Canvas.GetTop(rubberZoom), rubberZoom.Width, rubberZoom.Height);

            foreach (FrameworkElement child in canvas.Children)
            {
                if (child.GetType() == typeof(Grid))
                {
                    Grid grid = (Grid)child;
                    Shape shape = (Shape)grid.Tag;
                    Point point1 = shape.elt.TranslatePoint(new Point(0, 0), canvas);
                    Point point2 = shape.elt.TranslatePoint(new Point(shape.elt.ActualWidth, shape.elt.ActualHeight), canvas);
                    Rect eltBounds = new Rect(point1, point2);
                    if (rubberBounds.Contains(eltBounds))
                    {
                        currentElt = shape.elt;
                        currentShape = shape;
                        currentShape.ShowHandles(true);
                        if (!selectedShapes.Contains(currentShape)) selectedShapes.Add(currentShape);
                    }
                }
            }
            UpdateView();
        }

        private void canvasPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            contextMenu.Items.Clear();

            MenuItem itemShape = new MenuItem();
            itemShape.Header = "Select Shape";
            itemShape.Icon = new Image() { Source = MainWindow.ImageSourceFromBitmap(Properties.Resources.Objects)};
            contextMenu.Items.Add(itemShape);

            MenuItem itemUnShape = new MenuItem();
            itemUnShape.Header = "Unselect Shape";
            itemUnShape.Icon = new Image() { Source = MainWindow.ImageSourceFromBitmap(Properties.Resources.UnObjects) };
            contextMenu.Items.Add(itemUnShape);

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

                    if (selectedShapes.Contains((Shape)shape.Tag))
                    {
                        item = new MenuItem();
                        itemUnShape.Items.Add(item);
                        item.Header = elt.Name;
                        item.Click += new RoutedEventHandler(UnSelectShapeClick);
                        item.Tag = elt;
                    }
                }
            }

            MenuItem itemCopyShapes = new MenuItem();
            itemCopyShapes.Header = "Copy Selected Shapes";
            itemCopyShapes.Icon = new Image() { Source = MainWindow.ImageSourceFromBitmap(Properties.Resources.CopyShapes) };
            itemCopyShapes.Click += new RoutedEventHandler(CopyShapes);
            contextMenu.Items.Add(itemCopyShapes);

            MenuItem itemBackground = new MenuItem();
            itemBackground.Header = "Background Color";
            itemBackground.Icon = new Image() { Source = MainWindow.ImageSourceFromBitmap(Properties.Resources.Color_palette) };
            itemBackground.Click += new RoutedEventHandler(SelectBackground);
            contextMenu.Items.Add(itemBackground);

            MenuItem itemSetCode = new MenuItem();
            itemSetCode.Header = "Paste Code to Current Document";
            itemSetCode.Icon = new Image() { Source = MainWindow.ImageSourceFromBitmap(Properties.Resources.Paste) };
            itemSetCode.Click += new RoutedEventHandler(SetNewCode);
            contextMenu.Items.Add(itemSetCode);

            MenuItem itemGetCode = new MenuItem();
            itemGetCode.Header = "Copy Code from Current Document";
            itemGetCode.Icon = new Image() { Source = MainWindow.ImageSourceFromBitmap(Properties.Resources.Copy) };
            itemGetCode.Click += new RoutedEventHandler(GetNewCode);
            contextMenu.Items.Add(itemGetCode);

            e.Handled = true;
        }

        private void CopyShapes(object sender, RoutedEventArgs e)
        {
            ShowCode(true);
            foreach (Shape shape in selectedShapes) shape.ShowHandles(false);
            selectedShapes.Clear();
            ReadCode(true);
            ShowCode();
            if (selectedShapes.Count == 0)
            {
                currentShape = null;
                currentElt = null;
            }
            else
            {
                currentShape = selectedShapes.Last();
                currentElt = currentShape.elt;
            }
            UpdateView();
        }

        private void SetNewCode(object sender, RoutedEventArgs e)
        {
            ShowCode();
            mainWindow.GetActiveDocument().LoadDataFromText(sbDocument.TextArea.Text);
        }

        private void GetNewCode(object sender, RoutedEventArgs e)
        {
            sbDocument.TextArea.Text = mainWindow.GetActiveDocument().TextArea.Text;
        }

        private void canvasMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!Keyboard.IsKeyDown(Key.LeftShift) && !Keyboard.IsKeyDown(Key.RightShift))
            {
                currentElt = null;
                foreach (Shape shape in selectedShapes) shape.ShowHandles(false);
                selectedShapes.Clear();
                currentShape = null;
            }
            mode = "_SEL";
            SetStart(e.GetPosition(canvas));
            UpdateView();
        }

        private void SelectBackground(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.ColorDialog cd = new System.Windows.Forms.ColorDialog();
            Color color = ((SolidColorBrush)background).Color;
            cd.Color = System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);
            cd.AnyColor = true;
            cd.SolidColorOnly = true;
            cd.FullOpen = true;
            if (cd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                background = new SolidColorBrush(Color.FromArgb(cd.Color.A, cd.Color.R, cd.Color.G, cd.Color.B));
                HighContrast();
                SetBackgound();
                ShowCode();
            }
        }

        private void SetBackgound()
        {
            canvas.Background = background;
            SetSnap();
            foreach (FrameworkElement child in canvas.Children)
            {
                if (child.GetType() == typeof(Grid))
                {
                    Grid grid = (Grid)child;
                    Shape shape = (Shape)grid.Tag;
                    shape.UpdateHandleColor();
                }
            }
        }

        private void SelectShapeClick(object sender, RoutedEventArgs e)
        {
            try
            {
                MenuItem item = (MenuItem)sender;
                FrameworkElement elt = (FrameworkElement)item.Tag;
                mode = "_SEL";
                eltPreviewMouseLeftButtonDown(elt, null);
            }
            catch
            {

            }
        }

        private void UnSelectShapeClick(object sender, RoutedEventArgs e)
        {
            try
            {
                MenuItem item = (MenuItem)sender;
                FrameworkElement elt = (FrameworkElement)item.Tag;
                Shape shape = (Shape)elt.Tag;
                mode = "_SEL";
                selectedShapes.Remove(shape);
                shape.ShowHandles(false);
                if (selectedShapes.Count == 0)
                {
                    currentElt = null;
                    currentShape = null;
                }
                else
                {
                    currentShape = selectedShapes.Last();
                    currentElt = currentShape.elt;
                }
                UpdateView();
            }
            catch
            {

            }
        }

        private void canvasMouseMove(object sender, MouseEventArgs e)
        {
            currentPosition = e.GetPosition(canvas);
            labelPosition.Content = "(" + Fix(currentPosition.X) + "," + Fix(currentPosition.Y) + ")";

            if (e.LeftButton == MouseButtonState.Released) return;
            else if (mode == "_SEL")
            {
                rubberZoom.Visibility = Visibility.Visible;
                rubberZoom.Width = Math.Abs(currentPosition.X - startGlobal.X);
                rubberZoom.Height = Math.Abs(currentPosition.Y - startGlobal.Y);
                Canvas.SetLeft(rubberZoom, Math.Min(currentPosition.X, startGlobal.X));
                Canvas.SetTop(rubberZoom, Math.Min(currentPosition.Y, startGlobal.Y));
                //canvas.UpdateLayout();
                return;
            }
            else if (null == currentShape) return;

            Vector change = Snap(currentPosition - startGlobal);

            switch (mode)
            {
                case "_TL":
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
                        if (currentElt.GetType() != typeof(CheckBox) && currentElt.GetType() != typeof(RadioButton))
                        {
                            currentElt.Width = startWidth - change.X;
                            currentElt.Height = startHeight - change.Y;
                            Canvas.SetLeft(currentShape.shape, startLocal.X + change.X - Shape.HandleShort);
                            Canvas.SetTop(currentShape.shape, startLocal.Y + change.Y - Shape.HandleShort);
                        }
                    }
                    break;
                case "_TR":
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
                        if (currentElt.GetType() != typeof(CheckBox) && currentElt.GetType() != typeof(RadioButton))
                        {
                            currentElt.Width = startWidth + change.X;
                            currentElt.Height = startHeight - change.Y;
                            Canvas.SetTop(currentShape.shape, startLocal.Y + change.Y - Shape.HandleShort);
                        }
                    }
                    break;
                case "_BL":
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
                        if (currentElt.GetType() != typeof(CheckBox) && currentElt.GetType() != typeof(RadioButton))
                        {
                            currentElt.Width = startWidth - change.X;
                            currentElt.Height = startHeight + change.Y;
                            Canvas.SetLeft(currentShape.shape, startLocal.X + change.X - Shape.HandleShort);
                        }
                    }
                    break;
                case "_BR":
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
                        if (currentElt.GetType() != typeof(CheckBox) && currentElt.GetType() != typeof(RadioButton))
                        {
                            currentElt.Width = startWidth + change.X;
                            currentElt.Height = startHeight + change.Y;
                        }
                    }
                    break;
                case "_L":
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
                        if (currentElt.GetType() != typeof(CheckBox) && currentElt.GetType() != typeof(RadioButton))
                        {
                            currentElt.Width = Math.Max(0, startWidth - change.X);
                            Canvas.SetLeft(currentShape.shape, startLocal.X + change.X - Shape.HandleShort);
                        }
                    }
                    break;
                case "_R":
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
                        if (currentElt.GetType() != typeof(CheckBox) && currentElt.GetType() != typeof(RadioButton))
                        {
                            currentElt.Width = Math.Max(0, startWidth + change.X);
                        }
                    }
                    break;
                case "_T":
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
                        if (currentElt.GetType() != typeof(CheckBox) && currentElt.GetType() != typeof(RadioButton))
                        {
                            currentElt.Height = Math.Max(0, startHeight - change.Y);
                            Canvas.SetTop(currentShape.shape, startLocal.Y + change.Y - Shape.HandleShort);
                        }
                    }
                    break;
                case "_B":
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
                        if (currentElt.GetType() != typeof(CheckBox) && currentElt.GetType() != typeof(RadioButton))
                        {
                            currentElt.Height = Math.Max(0, startHeight + change.Y);
                        }
                    }
                    break;
                case "_PT":
                    if (currentElt.GetType() == typeof(Polygon) && _PT >= 0 && _PT < ((Polygon)currentElt).Points.Count)
                    {
                        Polygon polygon = (Polygon)currentElt;
                        polygon.Points[_PT] = new Point(currentPosition.X - startLocal.X - Shape.HandleShort / 2.0, currentPosition.Y - startLocal.Y - Shape.HandleShort / 2.0);
                    }
                    else if (currentElt.GetType() == typeof(Line) && _PT >= 0 && _PT < 2)
                    {
                        Line line = (Line)currentElt;
                        if (_PT == 0)
                        {
                            line.X1 = currentPosition.X - startLocal.X - Shape.HandleShort / 2.0;
                            line.Y1 = currentPosition.Y - startLocal.Y - Shape.HandleShort / 2.0;
                        }
                        else
                        {
                            line.X2 = currentPosition.X - startLocal.X - Shape.HandleShort / 2.0;
                            line.Y2 = currentPosition.Y - startLocal.Y - Shape.HandleShort / 2.0;
                        }
                    }
                    break;
                default:
                    double dX = startLocal.X + change.X - Shape.HandleShort - Canvas.GetLeft(currentShape.shape);
                    double dY = startLocal.Y + change.Y - Shape.HandleShort - Canvas.GetTop(currentShape.shape);
                    foreach (Shape selectedShape in selectedShapes)
                    {
                        Canvas.SetLeft(selectedShape.shape, Canvas.GetLeft(selectedShape.shape) + dX);
                        Canvas.SetTop(selectedShape.shape, Canvas.GetTop(selectedShape.shape) + dY);
                    }
                    break;
            }
            if (currentElt.GetType() != typeof(CheckBox) && currentElt.GetType() != typeof(RadioButton))
            {
                currentShape.modifiers["Width"] = (currentElt.Width).ToString();
                currentShape.modifiers["Height"] = (currentElt.Height).ToString();
            }
            foreach (Shape selectedShape in selectedShapes)
            {
                selectedShape.modifiers["Left"] = (Canvas.GetLeft(selectedShape.shape) + Shape.HandleShort).ToString();
                selectedShape.modifiers["Top"] = (Canvas.GetTop(selectedShape.shape) + Shape.HandleShort).ToString();
            }
            UpdatePolygonHandles();

            canvas.UpdateLayout();
        }

        private void UpdatePolygonHandles()
        {
            if (null != currentElt)
            {
                if (currentElt.GetType() == typeof(Polygon))
                {
                    Polygon polygon = (Polygon)currentElt;
                    if (Shape.HandlePT.Count >= polygon.Points.Count)
                    {
                        for (int i = 0; i < polygon.Points.Count; i++)
                        {
                            Canvas.SetLeft(Shape.HandlePT[i], Canvas.GetLeft(currentShape.shape) + Shape.HandleShort + polygon.Points[i].X - Shape.HandleShort / 2.0);
                            Canvas.SetTop(Shape.HandlePT[i], Canvas.GetTop(currentShape.shape) + Shape.HandleShort + polygon.Points[i].Y - Shape.HandleShort / 2.0);
                        }
                    }
                }
                else if (currentElt.GetType() == typeof(Line))
                {
                    Line line = (Line)currentElt;
                    Canvas.SetLeft(Shape.HandlePT[0], Canvas.GetLeft(currentShape.shape) + Shape.HandleShort + line.X1 - Shape.HandleShort / 2.0);
                    Canvas.SetTop(Shape.HandlePT[0], Canvas.GetTop(currentShape.shape) + Shape.HandleShort + line.Y1 - Shape.HandleShort / 2.0);
                    Canvas.SetLeft(Shape.HandlePT[1], Canvas.GetLeft(currentShape.shape) + Shape.HandleShort + line.X2 - Shape.HandleShort / 2.0);
                    Canvas.SetTop(Shape.HandlePT[1], Canvas.GetTop(currentShape.shape) + Shape.HandleShort + line.Y2 - Shape.HandleShort / 2.0);
                }
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
            Pen _Pen = new Pen(foreground, 0.5);
            if (snap >= 5)
            {
                for (int i = snap; i < canvas.Width; i += snap)
                {
                    for (int j = snap; j < canvas.Height; j += snap)
                    {
                        dc.DrawRectangle(null, _Pen, new Rect(i, j, 0.5, 0.5));
                    }
                }
            }
            dc.Close();
        }

        private void HighContrast()
        {
            //return Color.FromArgb(255, (byte)(255 - color.R), (byte)(255 - color.G), (byte)(255 - color.B));
            //return Color.FromArgb(255, (byte)(color.R > 127 ? 0 : 255), (byte)(color.G > 127 ? 0 : 255), (byte)(color.B > 127 ? 0 : 255));
            Color color = ((SolidColorBrush)background).Color;
            ((SolidColorBrush)foreground).Color = (0.2126 * color.ScR + 0.7152 * color.ScG + 0.0722 * color.ScB) < 0.5 ? Colors.White : Colors.Black;
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
                        properties.Add(new PropertyData() { Property = "StrokeThickness", Value = Fix(shape.StrokeThickness).ToString(), Visible = Visibility.Hidden });
                    }
                    else if (currentElt.GetType() == typeof(Ellipse))
                    {
                        Ellipse shape = (Ellipse)currentElt;
                        properties.Add(new PropertyData() { Property = "Fill", Value = ColorName(shape.Fill), Visible = Visibility.Visible });
                        properties.Add(new PropertyData() { Property = "Stroke", Value = ColorName(shape.Stroke), Visible = Visibility.Visible });
                        properties.Add(new PropertyData() { Property = "StrokeThickness", Value = Fix(shape.StrokeThickness).ToString(), Visible = Visibility.Hidden });
                    }
                    else if (currentElt.GetType() == typeof(Polygon))
                    {
                        Polygon shape = (Polygon)currentElt;
                        properties.Add(new PropertyData() { Property = "Fill", Value = ColorName(shape.Fill), Visible = Visibility.Visible });
                        properties.Add(new PropertyData() { Property = "Stroke", Value = ColorName(shape.Stroke), Visible = Visibility.Visible });
                        properties.Add(new PropertyData() { Property = "StrokeThickness", Value = Fix(shape.StrokeThickness).ToString(), Visible = Visibility.Hidden });
                        for (int i = 0; i < shape.Points.Count; i++)
                        {
                            properties.Add(new PropertyData() { Property = "X" + (i + 1).ToString(), Value = Fix(shape.Points[i].X).ToString(), Visible = Visibility.Hidden });
                            properties.Add(new PropertyData() { Property = "Y" + (i + 1).ToString(), Value = Fix(shape.Points[i].Y).ToString(), Visible = Visibility.Hidden });
                        }
                    }
                    else if (currentElt.GetType() == typeof(Line))
                    {
                        Line shape = (Line)currentElt;
                        properties.Add(new PropertyData() { Property = "Stroke", Value = ColorName(shape.Stroke), Visible = Visibility.Visible });
                        properties.Add(new PropertyData() { Property = "StrokeThickness", Value = Fix(shape.StrokeThickness).ToString(), Visible = Visibility.Hidden });
                        properties.Add(new PropertyData() { Property = "X1", Value = Fix(shape.X1).ToString(), Visible = Visibility.Hidden });
                        properties.Add(new PropertyData() { Property = "Y1", Value = Fix(shape.Y1).ToString(), Visible = Visibility.Hidden });
                        properties.Add(new PropertyData() { Property = "X2", Value = Fix(shape.X2).ToString(), Visible = Visibility.Hidden });
                        properties.Add(new PropertyData() { Property = "Y2", Value = Fix(shape.Y2).ToString(), Visible = Visibility.Hidden });
                    }
                    else if (currentElt.GetType() == typeof(TextBlock))
                    {
                        TextBlock shape = (TextBlock)currentElt;
                        properties.Add(new PropertyData() { Property = "Text", Value = shape.Text, Visible = Visibility.Hidden });
                        properties.Add(new PropertyData() { Property = "Foreground", Value = ColorName(shape.Foreground), Visible = Visibility.Visible });
                        properties.Add(new PropertyData() { Property = "FontFamily", Value = shape.FontFamily.ToString(), Visible = Visibility.Visible });
                        properties.Add(new PropertyData() { Property = "FontStyle", Value = shape.FontStyle.ToString(), Visible = Visibility.Visible });
                        properties.Add(new PropertyData() { Property = "FontSize", Value = Fix(shape.FontSize).ToString(), Visible = Visibility.Visible });
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
                        GetControlProperties(shape);
                    }
                    else if (currentElt.GetType() == typeof(TextBox))
                    {
                        TextBox shape = (TextBox)currentElt;
                        properties.Add(new PropertyData() { Property = "Text", Value = shape.Text.ToString(), Visible = Visibility.Hidden });
                        GetControlProperties(shape);
                    }
                    else if (currentElt.GetType() == typeof(WebBrowser))
                    {
                        WebBrowser shape = (WebBrowser)currentElt;
                        properties.Add(new PropertyData() { Property = "Url", Value = null == shape.Source ? "" : shape.Source.ToString(), Visible = Visibility.Hidden });
                    }
                    else if (currentElt.GetType() == typeof(CheckBox))
                    {
                        CheckBox shape = (CheckBox)currentElt;
                        properties.Add(new PropertyData() { Property = "Content", Value = shape.Content.ToString(), Visible = Visibility.Hidden });
                        GetControlProperties(shape);
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
                        properties.Add(new PropertyData() { Property = "DropDownHeight", Value = Fix(shape.MaxDropDownHeight).ToString(), Visible = Visibility.Hidden });
                        GetControlProperties(shape);
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
                    else if (currentElt.GetType() == typeof(ListBox))
                    {
                        ListBox shape = (ListBox)currentElt;
                        string list = "";
                        int i = 1;
                        foreach (ListBoxItem item in shape.Items)
                        {
                            list += (i++).ToString() + "=" + item.Content.ToString() + ";";
                        }
                        properties.Add(new PropertyData() { Property = "List", Value = list, Visible = Visibility.Hidden });
                        GetControlProperties(shape);
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
                        GetControlProperties(shape);
                    }
                    else if (currentElt.GetType() == typeof(PasswordBox))
                    {
                        PasswordBox shape = (PasswordBox)currentElt;
                        properties.Add(new PropertyData() { Property = "MaxLength", Value = Fix(shape.MaxLength).ToString(), Visible = Visibility.Hidden });
                        GetControlProperties(shape);
                    }
                    else if (currentElt.GetType() == typeof(ProgressBar))
                    {
                        ProgressBar shape = (ProgressBar)currentElt;
                        properties.Add(new PropertyData() { Property = "Foreground", Value = ColorName(shape.Foreground), Visible = Visibility.Visible });
                        properties.Add(new PropertyData() { Property = "Orientation", Value = shape.Orientation.ToString()[0].ToString(), Visible = Visibility.Hidden });
                    }
                    else if (currentElt.GetType() == typeof(RadioButton))
                    {
                        RadioButton shape = (RadioButton)currentElt;
                        properties.Add(new PropertyData() { Property = "Content", Value = shape.Content.ToString(), Visible = Visibility.Hidden });
                        properties.Add(new PropertyData() { Property = "GroupName", Value = shape.GroupName, Visible = Visibility.Hidden });
                        GetControlProperties(shape);
                    }
                    else if (currentElt.GetType() == typeof(RichTextBox))
                    {
                        RichTextBox shape = (RichTextBox)currentElt;
                        GetControlProperties(shape);
                    }
                    else if (currentElt.GetType() == typeof(Slider))
                    {
                        Slider shape = (Slider)currentElt;
                        properties.Add(new PropertyData() { Property = "Orientation", Value = shape.Orientation.ToString()[0].ToString(), Visible = Visibility.Hidden });
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
                        GetControlProperties(shape);
                        properties.RemoveAt(2);
                    }
                }
                dataGridProperties.Items.Refresh();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Shapes Editor failed to set a property input.", "SB-IDE", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GetControlProperties(Control shape)
        {
            properties.Add(new PropertyData() { Property = "Foreground", Value = ColorName(shape.Foreground), Visible = Visibility.Visible });
            properties.Add(new PropertyData() { Property = "FontFamily", Value = shape.FontFamily.ToString(), Visible = Visibility.Visible });
            properties.Add(new PropertyData() { Property = "FontStyle", Value = shape.FontStyle.ToString(), Visible = Visibility.Visible });
            properties.Add(new PropertyData() { Property = "FontSize", Value = Fix(shape.FontSize).ToString(), Visible = Visibility.Visible });
            properties.Add(new PropertyData() { Property = "FontWeight", Value = shape.FontWeight.ToString(), Visible = Visibility.Visible });
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
                    if (currentElt.GetType() != typeof(CheckBox) && currentElt.GetType() != typeof(RadioButton))
                    {
                        currentElt.Measure(new Size(double.MaxValue, double.MaxValue));
                        currentShape.modifiers["Width"] = Fix(currentElt.DesiredSize.Width).ToString();
                        currentShape.modifiers["Height"] = Fix(currentElt.DesiredSize.Height).ToString();
                    }
                    Point point = currentShape.shape.TranslatePoint(new Point(0, 0), canvas);
                    currentShape.modifiers["Left"] = Fix(point.X + Shape.HandleShort).ToString();
                    currentShape.modifiers["Top"] = Fix(point.Y + Shape.HandleShort).ToString();
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

        private void ShowCode(bool bSelected = false)
        {
            try
            {
                sbDocument.TextArea.Text = "";
                sbDocument.TextArea.Text += "Init()\n\nSub Init\n\n";

                Brush _brush = new SolidColorBrush(((SolidColorBrush)brush).Color);
                Pen _pen = new Pen(new SolidColorBrush(((SolidColorBrush)pen.Brush).Color), pen.Thickness);
                FontFamily _fontFamily = fontFamily;
                FontStyle _fontStyle = fontStyle;
                double _fontSize = fontSize;
                FontWeight _fontWeight = fontWeight;

                if (background.ToString() != Brushes.White.ToString())
                {
                    sbDocument.TextArea.Text += "GraphicsWindow.BackgroundColor = \"" + ColorName(background) + "\"\n\n";
                }

                foreach (FrameworkElement child in canvas.Children)
                {
                    if (child.GetType() == typeof(Grid))
                    {
                        Grid grid = (Grid)child;
                        if (bSelected && !selectedShapes.Contains((Shape)grid.Tag)) continue;

                        foreach (FrameworkElement elt in grid.Children)
                        {
                            if (null != elt.Tag && !elt.Name.StartsWith("_"))
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
                                    else if (obj.Points.Count > 3)
                                    {
                                        string points = "";
                                        for (int i = 0; i < obj.Points.Count; i++)
                                        {
                                            string point = "1\\=" + Fix(obj.Points[i].X) + "\\;2\\=" + Fix(obj.Points[i].Y) + "\\;";
                                            points += (i + 1).ToString() + "=" + point + ";";
                                        }
                                        sbDocument.TextArea.Text += obj.Name + " = LDShapes.AddPolygon(\"" + points + "\")\n";
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
                                    SetControlPropertyCode(obj, ref _brush, ref _fontFamily, ref _fontStyle, ref _fontSize, ref _fontWeight);
                                    sbDocument.TextArea.Text += obj.Name + " = Controls.AddButton(\"" + obj.Content + "\"," + Fix(shape.modifiers["Left"]) + "," + Fix(shape.modifiers["Top"]) + ")\n";
                                    sbDocument.TextArea.Text += "Controls.SetSize(" + obj.Name + "," + Fix(shape.modifiers["Width"]) + "," + Fix(shape.modifiers["Height"]) + ")\n";
                                    if (shape.modifiers["Opacity"] != "100") sbDocument.TextArea.Text += "Shapes.SetOpacity(" + obj.Name + "," + Fix(shape.modifiers["Opacity"]) + ")\n";
                                    if (shape.modifiers["Angle"] != "0") sbDocument.TextArea.Text += "Shapes.Rotate(" + obj.Name + "," + Fix(shape.modifiers["Angle"]) + ")\n";
                                    sbDocument.TextArea.Text += "\n";
                                }
                                else if (elt.GetType() == typeof(TextBox))
                                {
                                    TextBox obj = (TextBox)elt;
                                    SetControlPropertyCode(obj, ref _brush, ref _fontFamily, ref _fontStyle, ref _fontSize, ref _fontWeight);
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
                                    SetControlPropertyCode(obj, ref _brush, ref _fontFamily, ref _fontStyle, ref _fontSize, ref _fontWeight);
                                    sbDocument.TextArea.Text += obj.Name + " = LDControls.AddCheckBox(\"" + obj.Content.ToString() + "\")\n";
                                    sbDocument.TextArea.Text += "Shapes.Move(" + obj.Name + "," + Fix(shape.modifiers["Left"]) + "," + Fix(shape.modifiers["Top"]) + ")\n";
                                    if (shape.modifiers["Opacity"] != "100") sbDocument.TextArea.Text += "Shapes.SetOpacity(" + obj.Name + "," + Fix(shape.modifiers["Opacity"]) + ")\n";
                                    if (shape.modifiers["Angle"] != "0") sbDocument.TextArea.Text += "Shapes.Rotate(" + obj.Name + "," + Fix(shape.modifiers["Angle"]) + ")\n";
                                    sbDocument.TextArea.Text += "\n";
                                }
                                else if (elt.GetType() == typeof(ComboBox))
                                {
                                    ComboBox obj = (ComboBox)elt;
                                    SetControlPropertyCode(obj, ref _brush, ref _fontFamily, ref _fontStyle, ref _fontSize, ref _fontWeight);
                                    string list = "";
                                    int i = 1;
                                    foreach (ComboBoxItem item in obj.Items)
                                    {
                                        list += (i++).ToString() + "=" + item.Content.ToString() + ";";
                                    }
                                    sbDocument.TextArea.Text += obj.Name + " = LDControls.AddComboBox(\"" + list + "\"," + Fix(shape.modifiers["Width"]) + "," + Fix(obj.MaxDropDownHeight) + ")\n";
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
                                else if (elt.GetType() == typeof(ListBox))
                                {
                                    ListBox obj = (ListBox)elt;
                                    SetControlPropertyCode(obj, ref _brush, ref _fontFamily, ref _fontStyle, ref _fontSize, ref _fontWeight);
                                    string list = "";
                                    int i = 1;
                                    foreach (ListBoxItem item in obj.Items)
                                    {
                                        list += (i++).ToString() + "=" + item.Content.ToString() + ";";
                                    }
                                    sbDocument.TextArea.Text += obj.Name + " = LDControls.AddListBox(\"" + list + "\"," + Fix(shape.modifiers["Width"]) + "," + Fix(shape.modifiers["Height"]) + ")\n";
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
                                else if (elt.GetType() == typeof(MediaElement))
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
                                    SetControlPropertyCode(obj, ref _brush, ref _fontFamily, ref _fontStyle, ref _fontSize, ref _fontWeight);
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
                                    SetControlPropertyCode(obj, ref _brush, ref _fontFamily, ref _fontStyle, ref _fontSize, ref _fontWeight);
                                    sbDocument.TextArea.Text += obj.Name + " = LDControls.AddPasswordBox(" + Fix(shape.modifiers["Width"]) + "," + Fix(shape.modifiers["Height"]) + "," + obj.MaxLength.ToString() + ")\n";
                                    sbDocument.TextArea.Text += "Shapes.Move(" + obj.Name + "," + Fix(shape.modifiers["Left"]) + "," + Fix(shape.modifiers["Top"]) + ")\n";
                                    if (shape.modifiers["Opacity"] != "100") sbDocument.TextArea.Text += "Shapes.SetOpacity(" + obj.Name + "," + Fix(shape.modifiers["Opacity"]) + ")\n";
                                    if (shape.modifiers["Angle"] != "0") sbDocument.TextArea.Text += "Shapes.Rotate(" + obj.Name + "," + Fix(shape.modifiers["Angle"]) + ")\n";
                                    sbDocument.TextArea.Text += "\n";
                                }
                                else if (elt.GetType() == typeof(ProgressBar))
                                {
                                    ProgressBar obj = (ProgressBar)elt;
                                    if (obj.Foreground.ToString() != _brush.ToString())
                                    {
                                        _brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(obj.Foreground.ToString()));
                                        sbDocument.TextArea.Text += "GraphicsWindow.BrushColor = \"" + ColorName(_brush) + "\"\n";
                                    }
                                    sbDocument.TextArea.Text += obj.Name + " = LDControls.AddProgressBar(" + Fix(shape.modifiers["Width"]) + "," + Fix(shape.modifiers["Height"]) + ",\"" + obj.Orientation.ToString()[0] + "\")\n";
                                    sbDocument.TextArea.Text += "Shapes.Move(" + obj.Name + "," + Fix(shape.modifiers["Left"]) + "," + Fix(shape.modifiers["Top"]) + ")\n";
                                    if (shape.modifiers["Opacity"] != "100") sbDocument.TextArea.Text += "Shapes.SetOpacity(" + obj.Name + "," + Fix(shape.modifiers["Opacity"]) + ")\n";
                                    if (shape.modifiers["Angle"] != "0") sbDocument.TextArea.Text += "Shapes.Rotate(" + obj.Name + "," + Fix(shape.modifiers["Angle"]) + ")\n";
                                    sbDocument.TextArea.Text += "\n";
                                }
                                else if (elt.GetType() == typeof(RadioButton))
                                {
                                    RadioButton obj = (RadioButton)elt;
                                    SetControlPropertyCode(obj, ref _brush, ref _fontFamily, ref _fontStyle, ref _fontSize, ref _fontWeight);
                                    sbDocument.TextArea.Text += obj.Name + " = LDControls.AddRadioButton(\"" + obj.Content.ToString() + "\",\"" + obj.GroupName.ToString() + "\")\n";
                                    sbDocument.TextArea.Text += "Shapes.Move(" + obj.Name + "," + Fix(shape.modifiers["Left"]) + "," + Fix(shape.modifiers["Top"]) + ")\n";
                                    if (shape.modifiers["Opacity"] != "100") sbDocument.TextArea.Text += "Shapes.SetOpacity(" + obj.Name + "," + Fix(shape.modifiers["Opacity"]) + ")\n";
                                    if (shape.modifiers["Angle"] != "0") sbDocument.TextArea.Text += "Shapes.Rotate(" + obj.Name + "," + Fix(shape.modifiers["Angle"]) + ")\n";
                                    sbDocument.TextArea.Text += "\n";
                                }
                                else if (elt.GetType() == typeof(RichTextBox))
                                {
                                    RichTextBox obj = (RichTextBox)elt;
                                    SetControlPropertyCode(obj, ref _brush, ref _fontFamily, ref _fontStyle, ref _fontSize, ref _fontWeight);
                                    sbDocument.TextArea.Text += obj.Name + " = LDControls.AddRichTextBox(" + Fix(shape.modifiers["Width"]) + "," + Fix(shape.modifiers["Height"]) + ")\n";
                                    sbDocument.TextArea.Text += "Shapes.Move(" + obj.Name + "," + Fix(shape.modifiers["Left"]) + "," + Fix(shape.modifiers["Top"]) + ")\n";
                                    if (shape.modifiers["Opacity"] != "100") sbDocument.TextArea.Text += "Shapes.SetOpacity(" + obj.Name + "," + Fix(shape.modifiers["Opacity"]) + ")\n";
                                    if (shape.modifiers["Angle"] != "0") sbDocument.TextArea.Text += "Shapes.Rotate(" + obj.Name + "," + Fix(shape.modifiers["Angle"]) + ")\n";
                                    sbDocument.TextArea.Text += "\n";
                                }
                                else if (elt.GetType() == typeof(Slider))
                                {
                                    Slider obj = (Slider)elt;
                                    sbDocument.TextArea.Text += obj.Name + " = LDControls.AddSlider(" + Fix(shape.modifiers["Width"]) + "," + Fix(shape.modifiers["Height"]) + ",\"" + obj.Orientation.ToString()[0] + "\")\n";
                                    sbDocument.TextArea.Text += "Shapes.Move(" + obj.Name + "," + Fix(shape.modifiers["Left"]) + "," + Fix(shape.modifiers["Top"]) + ")\n";
                                    if (shape.modifiers["Opacity"] != "100") sbDocument.TextArea.Text += "Shapes.SetOpacity(" + obj.Name + "," + Fix(shape.modifiers["Opacity"]) + ")\n";
                                    if (shape.modifiers["Angle"] != "0") sbDocument.TextArea.Text += "Shapes.Rotate(" + obj.Name + "," + Fix(shape.modifiers["Angle"]) + ")\n";
                                    sbDocument.TextArea.Text += "\n";
                                }
                                else if (elt.GetType() == typeof(TreeView))
                                {
                                    TreeView obj = (TreeView)elt;
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
                            }
                        }
                    }
                }

                sbDocument.TextArea.Text += "EndSub\n";
                sbDocument.Lexer.Format();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Shapes Editor failed to export some shapes to code.", "SB-IDE", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetControlPropertyCode(Control obj, ref Brush _brush, ref FontFamily _fontFamily, ref FontStyle _fontStyle, ref double _fontSize, ref FontWeight _fontWeight)
        {
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
        }

        private string Fix(string value)
        {
            return (fixDec * Math.Round(double.Parse(value) / fixDec)).ToString();
        }

        private string Fix(double value)
        {
            return (fixDec * Math.Round(value / fixDec)).ToString();
        }

        private void ReadCode(bool bSelect = false)
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
                    if (code.StartsWith("\'")) continue;
                    string codeLower = code.ToLower();
                    if (codeLower.Contains("graphicswindow.backgroundcolor"))
                    {
                        string[] parts = code.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
                        background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(parts[1].Replace("\"", "")));
                        HighContrast();
                        SetBackgound();
                    }
                    else if (codeLower.Contains("shapes.addrectangle"))
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
                        shape.modifiers["Left"] = Fix(parts[2]);
                        shape.modifiers["Top"] = Fix(parts[3]);
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
                        shape.modifiers["Left"] = Fix(parts[1]);
                        shape.modifiers["Top"] = Fix(parts[2]);
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
                        shape.modifiers["Left"] = Fix(parts[1]);
                        shape.modifiers["Top"] = Fix(parts[2]);
                    }
                    else if (codeLower.Contains("ldshapes.addpolygon"))
                    {
                        string[] parts = code.Split(new char[] { '(', ')', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        string[] values = parts[1].Replace("\"", "").Split(new char[] { '=', ';', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                        PointCollection points = new PointCollection();
                        double x = 0;
                        double y = 0;
                        for (int i = 0; i < values.Length; i = i+5)
                        {
                            double.TryParse(values[i + 2], out x);
                            double.TryParse(values[i + 4], out y);
                            points.Add(new Point(x, y));
                        }
                        name = GetName("Polygon", parts[0]);
                        elt = new Polygon()
                        {
                            Name = name,
                            Points = points,
                            Fill = _brush,
                            Stroke = _pen.Brush,
                            StrokeThickness = _pen.Thickness,
                        };
                        elt.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(eltPreviewMouseLeftButtonDown);
                        shape = new Shape(elt);
                        canvas.Children.Add(shape.shape);
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
                            Foreground = _brush,
                            FontFamily = _fontFamily,
                            FontStyle = _fontStyle,
                            FontSize = _fontSize,
                            FontWeight = _fontWeight,
                        };
                        UpdateProperty(elt, new PropertyData() { Property = "List" }, parts[1].Replace("\"", ""));
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
                        UpdateProperty(elt, new PropertyData() { Property = "Headings" }, parts[3].Replace("\"", ""));
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
                    else if (codeLower.Contains("ldcontrols.addlistbox"))
                    {
                        string[] parts = code.Split(new char[] { '(', ')', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        double.TryParse(parts[2], out value[0]);
                        double.TryParse(parts[3], out value[1]);
                        name = GetName("ListBox", parts[0]);
                        elt = new ListBox()
                        {
                            Name = name,
                            Width = value[0],
                            Height = value[1],
                            Foreground = _brush,
                            FontFamily = _fontFamily,
                            FontStyle = _fontStyle,
                            FontSize = _fontSize,
                            FontWeight = _fontWeight,
                        };
                        UpdateProperty(elt, new PropertyData() { Property = "List" }, parts[1].Replace("\"", ""));
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
                        UpdateProperty(elt, new PropertyData() { Property = "Headings" }, parts[3].Replace("\"", ""));
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
                            Foreground = _brush,
                            FontFamily = _fontFamily,
                            FontStyle = _fontStyle,
                            FontSize = _fontSize,
                            FontWeight = _fontWeight,
                        };
                        UpdateProperty(elt, new PropertyData() { Property = "MenuList" }, parts[3].Replace("\"", ""));
                        UpdateProperty(elt, new PropertyData() { Property = "IconList" }, parts[4].Replace("\"", ""));
                        UpdateProperty(elt, new PropertyData() { Property = "CheckList" }, parts[5].Replace("\"", ""));
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
                            Foreground = _brush,
                            FontFamily = _fontFamily,
                            FontStyle = _fontStyle,
                            FontSize = _fontSize,
                            FontWeight = _fontWeight,
                        };
                        UpdateProperty(elt, new PropertyData() { Property = "MaxLength" }, parts[3].Replace("\"", ""));
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
                            Foreground = _brush,
                            Value = 75,
                        };
                        UpdateProperty(elt, new PropertyData() { Property = "Orientation" }, parts[3].Replace("\"", ""));
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
                            Foreground = _brush,
                            FontFamily = _fontFamily,
                            FontStyle = _fontStyle,
                            FontSize = _fontSize,
                            FontWeight = _fontWeight,
                            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                        };
                        ((RichTextBox)elt).Document.Blocks.Clear();
                        ((RichTextBox)elt).Document.Blocks.Add(new Paragraph(new Run("RichTextBox")));
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
                            Maximum = 100,
                            Value = 75,
                        };
                        UpdateProperty(elt, new PropertyData() { Property = "Orientation" }, parts[3].Replace("\"", ""));
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
                            FontFamily = _fontFamily,
                            FontStyle = _fontStyle,
                            FontSize = _fontSize,
                            FontWeight = _fontWeight,
                        };
                        UpdateProperty(elt, new PropertyData() { Property = "Tree" }, parts[1].Replace("\"", ""));
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
                        shape.modifiers["Left"] = Fix(parts[2]);
                        shape.modifiers["Top"] = Fix(parts[3]);
                    }
                    else if (codeLower.Contains("controls.setsize"))
                    {
                        string[] parts = code.Split(new char[] { '(', ')', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        shape.modifiers["Width"] = Fix(parts[2]);
                        shape.modifiers["Height"] = Fix(parts[3]);
                        shape.elt.Width = double.Parse(parts[2]);
                        shape.elt.Height = double.Parse(parts[3]);
                    }
                    else if (codeLower.Contains("shapes.setopacity"))
                    {
                        string[] parts = code.Split(new char[] { '(', ')', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        shape.modifiers["Opacity"] = Fix(parts[2]);
                        shape.elt.Opacity = double.Parse(parts[2]) / 100.0;
                    }
                    else if (codeLower.Contains("shapes.rotate"))
                    {
                        string[] parts = code.Split(new char[] { '(', ')', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        shape.modifiers["Angle"] = Fix(parts[2]);
                        RotateShape(shape);
                    }
                    if (null != shape)
                    {
                        if (shape.elt.GetType() != typeof(CheckBox) && shape.elt.GetType() != typeof(RadioButton))
                        {
                            shape.elt.Measure(new Size(double.MaxValue, double.MaxValue));
                            if (!shape.modifiers.ContainsKey("Width")) shape.modifiers["Width"] = Fix(shape.elt.DesiredSize.Width).ToString();
                            if (!shape.modifiers.ContainsKey("Height")) shape.modifiers["Height"] = Fix(shape.elt.DesiredSize.Height).ToString();
                        }
                        if (!shape.modifiers.ContainsKey("Left")) shape.modifiers["Left"] = "0";
                        if (!shape.modifiers.ContainsKey("Top")) shape.modifiers["Top"] = "0";
                        if (!shape.modifiers.ContainsKey("Angle")) shape.modifiers["Angle"] = "0";
                        if (!shape.modifiers.ContainsKey("Opacity")) shape.modifiers["Opacity"] = "100";
                        Canvas.SetLeft(shape.shape, double.Parse(shape.modifiers["Left"]) - Shape.HandleShort);
                        Canvas.SetTop(shape.shape, double.Parse(shape.modifiers["Top"]) - Shape.HandleShort);
                        if (bSelect && !selectedShapes.Contains(shape))
                        {
                            selectedShapes.Add(shape);
                            shape.ShowHandles(true, false);
                        }
                    }
                }
                canvas.UpdateLayout();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Shapes Editor failed to import some shapes from code.", "SB-IDE", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private class Shape
        {
            public static int HandleShort = 6;

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
            private Image handleM = null;
            public static List<Ellipse> HandlePT = null;

            public Shape(FrameworkElement elt)
            {
                elt.Tag = this;
                elt.Cursor = Cursors.Hand;
                elt.MouseDown += new MouseButtonEventHandler(OnMouseDown);
                this.elt = elt;

                Grid grid = new Grid();
                grid.Tag = this;
                grid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

                grid.Children.Add(elt);
                Grid.SetRow(elt, 1);
                Grid.SetColumn(elt, 1);

                handleTL = GetHandle(HandleShort, HandleShort, "_TL");
                grid.Children.Add(handleTL);
                Grid.SetRow(handleTL, 0);
                Grid.SetColumn(handleTL, 0);

                handleTR = GetHandle(HandleShort, HandleShort, "_TR");
                grid.Children.Add(handleTR);
                Grid.SetRow(handleTR, 0);
                Grid.SetColumn(handleTR, 2);

                handleBL = GetHandle(HandleShort, HandleShort, "_BL");
                grid.Children.Add(handleBL);
                Grid.SetRow(handleBL, 2);
                Grid.SetColumn(handleBL, 0);

                handleBR = GetHandle(HandleShort, HandleShort, "_BR");
                grid.Children.Add(handleBR);
                Grid.SetRow(handleBR, 2);
                Grid.SetColumn(handleBR, 2);

                handleL = GetHandle(HandleShort, handleLong, "_L");
                grid.Children.Add(handleL);
                Grid.SetRow(handleL, 1);
                Grid.SetColumn(handleL, 0);

                handleR = GetHandle(HandleShort, handleLong, "_R");
                grid.Children.Add(handleR);
                Grid.SetRow(handleR, 1);
                Grid.SetColumn(handleR, 2);

                handleT = GetHandle(handleLong, HandleShort, "_T");
                grid.Children.Add(handleT);
                Grid.SetRow(handleT, 0);
                Grid.SetColumn(handleT, 1);

                handleB = GetHandle(handleLong, HandleShort, "_B");
                grid.Children.Add(handleB);
                Grid.SetRow(handleB, 2);
                Grid.SetColumn(handleB, 1);

                handleM = new Image()
                {
                    Name = "_M",
                    Tag = this,
                    Width = 2 * handleLong,
                    Height = 2 * handleLong,
                    Source = MainWindow.ImageSourceFromBitmap(Properties.Resources.Transform_move),
                    ToolTip = "Use Shift to select multiple shapes, to move or modify together",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Cursor = Cursors.Cross,
                };
                handleM.MouseDown += new MouseButtonEventHandler(OnMouseDown);
                grid.Children.Add(handleM);
                Canvas.SetZIndex(handleM, 1);
                Grid.SetRow(handleM, 3);
                Grid.SetColumn(handleM, 3);

                if (null == HandlePT)
                {
                    HandlePT = new List<Ellipse>();
                    for (int i = 0; i < PolygonSides.MaxSides; i++)
                    {
                        Ellipse pt = new Ellipse()
                        {
                            Name = "_PT",
                            Tag = i,
                            Width = HandleShort,
                            Height = HandleShort,
                            Fill = Brushes.Red,
                            Cursor = Cursors.Cross,
                            Stroke = THIS.foreground,
                            StrokeThickness = 1,
                            ToolTip = "P"+(i+1).ToString(),
                        };
                        pt.MouseDown += new MouseButtonEventHandler(OnMouseDown);
                        HandlePT.Add(pt);
                        THIS.canvas.Children.Add(pt);
                        Canvas.SetZIndex(pt, 2);
                    }
                }

                ShowHandles(false);
                shape = grid;
            }

            public void ShowHandles(bool bSet, bool bPT = true)
            {
                if (null != handleTL) handleTL.Visibility = bSet ? Visibility.Visible : Visibility.Hidden;
                if (null != handleTR) handleTR.Visibility = bSet ? Visibility.Visible : Visibility.Hidden;
                if (null != handleBL) handleBL.Visibility = bSet ? Visibility.Visible : Visibility.Hidden;
                if (null != handleBR) handleBR.Visibility = bSet ? Visibility.Visible : Visibility.Hidden;
                if (null != handleL) handleL.Visibility = bSet ? Visibility.Visible : Visibility.Hidden;
                if (null != handleR) handleR.Visibility = bSet ? Visibility.Visible : Visibility.Hidden;
                if (null != handleT) handleT.Visibility = bSet ? Visibility.Visible : Visibility.Hidden;
                if (null != handleB) handleB.Visibility = bSet ? Visibility.Visible : Visibility.Hidden;

                if (bSet)
                {
                    elt.MinWidth = 0;
                    elt.MinHeight = 0;
                }
                else if (elt.GetType() == typeof(Polygon) || elt.GetType() == typeof(Line)) //We loose grab handleM, but don't clip stroke
                {
                    Rect visualContentBounds = (Rect)GetPrivatePropertyValue(elt, "VisualContentBounds");
                    if (bPT && null != visualContentBounds && !visualContentBounds.IsEmpty)
                    {
                        elt.MinWidth = visualContentBounds.Width;
                        elt.MinHeight = visualContentBounds.Height;
                    }
                    else
                    {
                        elt.MinWidth = THIS.canvas.Width;
                        elt.MinHeight = THIS.canvas.Height;
                    }
                }
                if (!bPT) return;

                foreach (FrameworkElement pt in HandlePT)
                {
                    pt.Visibility = Visibility.Hidden;
                }

                if (bSet)
                {
                    if (elt.GetType() == typeof(Polygon))
                    {
                        Polygon polygon = (Polygon)elt;
                        if (modifiers.ContainsKey("Angle") && modifiers["Angle"] != "0")
                        {
                            for (int i = 0; i < polygon.Points.Count; i++)
                            {
                                HandlePT[i].Visibility = Visibility.Hidden;
                            }
                        }
                        else
                        {
                            for (int i = 0; i < polygon.Points.Count; i++)
                            {
                                HandlePT[i].Visibility = Visibility.Visible;
                            }
                        }
                    }
                    else if (elt.GetType() == typeof(Line))
                    {
                        Line line = (Line)elt;
                        if (modifiers.ContainsKey("Angle") && modifiers["Angle"] != "0")
                        {
                            HandlePT[0].Visibility = Visibility.Hidden;
                            HandlePT[1].Visibility = Visibility.Hidden;
                        }
                        else
                        {
                            HandlePT[0].Visibility = Visibility.Visible;
                            HandlePT[1].Visibility = Visibility.Visible;
                        }
                    }
                }
            }

            private object GetPrivatePropertyValue(object obj, string propName)
            {
                try
                {
                    Type t = obj.GetType();
                    PropertyInfo pi = t.GetProperty(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    return pi.GetValue(obj, null);
                }
                catch
                {
                    return null;
                }
            }

            public void UpdateHandleColor()
            {
                if (null != handleTL) handleTL.Stroke = THIS.foreground;
                if (null != handleTR) handleTR.Stroke = THIS.foreground;
                if (null != handleBL) handleBL.Stroke = THIS.foreground;
                if (null != handleBR) handleBR.Stroke = THIS.foreground;
                if (null != handleL) handleL.Stroke = THIS.foreground;
                if (null != handleR) handleR.Stroke = THIS.foreground;
                if (null != handleT) handleT.Stroke = THIS.foreground;
                if (null != handleB) handleB.Stroke = THIS.foreground;
            }

            private Rectangle GetHandle(int width, int height, string name)
            {
                Rectangle handle = new Rectangle()
                {
                    Name = name,
                    Tag = this,
                    Width = width,
                    Height = height,
                    Fill = Brushes.Transparent,
                    Stroke = THIS.foreground,
                    StrokeThickness = 1,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Cursor = Cursors.Cross,
                };
                Canvas.SetZIndex(handle, 1);
                handle.MouseDown += new MouseButtonEventHandler(OnMouseDown);
                return handle;
            }

            private void OnMouseDown(object sender, MouseButtonEventArgs e)
            {
                FrameworkElement elt = (FrameworkElement)sender;
                THIS.mode = elt.Name;
                THIS.Cursor = elt.Name == "_M" ? Cursors.Hand : Cursors.Cross;
                if (elt.Name == "_PT")
                {
                    THIS._PT = (int)elt.Tag;
                    if (null != THIS.currentShape)
                    {
                        THIS.currentElt.MinWidth = THIS.canvas.Width;
                        THIS.currentElt.MinHeight = THIS.canvas.Height;
                        THIS.currentShape.ShowHandles(false, false);
                    }
                }
                else if (elt.Name.StartsWith("_"))
                {
                    THIS.eltPreviewMouseLeftButtonDown(((Shape)elt.Tag).elt, null);
                }
                THIS.SetStart(e.GetPosition(THIS.canvas));
                e.Handled = true;
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
                case "Polygon":
                    // Get number of points
                    PolygonSides dlg = new PolygonSides();
                    dlg.ShowDialog();

                    int nPoint = PolygonSides.NumSides;
                    PointCollection points = new PointCollection();
                    for (int i = 0; i < nPoint; i++)
                    {
                        double angle = 2.0 * Math.PI * i / nPoint;
                        points.Add(new Point(50 - 50 * Math.Cos(angle), 50 + 50 * Math.Sin(angle)));
                    }
                    elt = new Polygon()
                    {
                        Name = name,
                        Points = points,
                        Fill = brush,
                        Stroke = pen.Brush,
                        StrokeThickness = pen.Thickness,
                    };
                    break;
                case "Browser":
                    WebBrowser webBrowser = new WebBrowser()
                    {
                        Name = name,
                        Width = 100,
                        Height = 100,
                    };
                    webBrowser.Navigate(new Uri("http://www.smallbasic.com"));
                    elt = webBrowser;
                    break;
                case "CheckBox":
                    elt = new CheckBox()
                    {
                        Name = name,
                        Content = label,
                        Foreground = brush,
                        FontFamily = fontFamily,
                        FontStyle = fontStyle,
                        FontSize = fontSize,
                        FontWeight = fontWeight,
                    };
                    break;
                case "ComboBox":
                    ComboBox comboBox = new ComboBox()
                    {
                        Name = name,
                        Width = 100,
                        MaxDropDownHeight = 100,
                        Foreground = brush,
                        FontFamily = fontFamily,
                        FontStyle = fontStyle,
                        FontSize = fontSize,
                        FontWeight = fontWeight,
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
                    break;
                case "DocumentViewer":
                    elt = new DocumentViewer()
                    {
                        Name = name,
                        Width = 100,
                        Height = 100,
                    };
                    break;
                case "ListBox":
                    ListBox listBox = new ListBox()
                    {
                        Name = name,
                        Width = 100,
                        Height = 100,
                        Foreground = brush,
                        FontFamily = fontFamily,
                        FontStyle = fontStyle,
                        FontSize = fontSize,
                        FontWeight = fontWeight,
                    };
                    ListBoxItem listBoxItem = new ListBoxItem();
                    listBoxItem.Content = "Item1";
                    listBox.Items.Add(listBoxItem);
                    elt = listBox;
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
                    break;
                case "Menu":
                    Menu menu = new Menu()
                    {
                        Name = name,
                        Foreground = brush,
                        FontFamily = fontFamily,
                        FontStyle = fontStyle,
                        FontSize = fontSize,
                        FontWeight = fontWeight,
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
                        Foreground = brush,
                        FontFamily = fontFamily,
                        FontStyle = fontStyle,
                        FontSize = fontSize,
                        FontWeight = fontWeight,
                    };
                    break;
                case "ProgressBar":
                    elt = new ProgressBar()
                    {
                        Name = name,
                        Width = 100,
                        Height = 20,
                        Foreground = brush,
                        Orientation = Orientation.Horizontal,
                        Value = 75,
                    };
                    break;
                case "RadioButton":
                    elt = new RadioButton()
                    {
                        Name = name,
                        Content = label,
                        GroupName = "Group1",
                        Foreground = brush,
                        FontFamily = fontFamily,
                        FontStyle = fontStyle,
                        FontSize = fontSize,
                        FontWeight = fontWeight,
                    };
                    break;
                case "RichTextBox":
                    elt = new RichTextBox()
                    {
                        Name = name,
                        Width = 100,
                        Height = 100,
                        Foreground = brush,
                        FontFamily = fontFamily,
                        FontStyle = fontStyle,
                        FontSize = fontSize,
                        FontWeight = fontWeight,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    };
                    ((RichTextBox)elt).Document.Blocks.Clear();
                    ((RichTextBox)elt).Document.Blocks.Add(new Paragraph(new Run("RichTextBox")));
                    break;
                case "Slider":
                    elt = new Slider()
                    {
                        Name = name,
                        Width = 100,
                        Height = 20,
                        Orientation = Orientation.Horizontal,
                        Maximum = 100,
                        Value = 75,
                    };
                    break;
                case "TreeView":
                    TreeView treeView = new TreeView()
                    {
                        Name = name,
                        Width = 100,
                        Height = 100,
                        FontFamily = fontFamily,
                        FontStyle = fontStyle,
                        FontSize = fontSize,
                        FontWeight = fontWeight,
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
                foreach (Shape selectedShape in selectedShapes)
                {
                    FrameworkElement selectedElt = selectedShape.elt;
                    UpdateProperty(selectedElt, property, value);
                }
            }
            catch
            {

            }
        }

        private void UpdateProperty(FrameworkElement selectedElt, PropertyData property, string value)
        {
            try
            {
                if (selectedElt.GetType() == typeof(Rectangle))
                {
                    Rectangle shape = (Rectangle)selectedElt;
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
                            shape.StrokeThickness = double.Parse(Fix(value));
                            break;
                    }
                }
                else if (selectedElt.GetType() == typeof(Ellipse))
                {
                    Ellipse shape = (Ellipse)selectedElt;
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
                            shape.StrokeThickness = double.Parse(Fix(value));
                            break;
                    }
                }
                else if (selectedElt.GetType() == typeof(Polygon))
                {
                    Polygon shape = (Polygon)selectedElt;
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
                            shape.StrokeThickness = double.Parse(Fix(value));
                            break;
                        default:
                            int i = int.Parse(property.Property.Substring(1)) - 1;
                            if (property.Property.StartsWith("X")) shape.Points[i] = new Point(double.Parse(Fix(value)), shape.Points[i].Y);
                            else if (property.Property.StartsWith("Y")) shape.Points[i] = new Point(shape.Points[i].X, double.Parse(Fix(value)));
                            break;
                    }
                    UpdatePolygonSize(shape);
                }
                else if (selectedElt.GetType() == typeof(Line))
                {
                    Line shape = (Line)selectedElt;
                    switch (property.Property)
                    {
                        case "Name":
                            shape.Name = value;
                            break;
                        case "X1":
                            shape.X1 = double.Parse(Fix(value));
                            break;
                        case "Y1":
                            shape.Y1 = double.Parse(Fix(value));
                            break;
                        case "X2":
                            shape.X2 = double.Parse(Fix(value));
                            break;
                        case "Y2":
                            shape.Y2 = double.Parse(Fix(value));
                            break;
                        case "Stroke":
                            shape.Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
                            break;
                        case "StrokeThickness":
                            shape.StrokeThickness = double.Parse(Fix(value));
                            break;
                    }
                }
                else if (selectedElt.GetType() == typeof(TextBlock))
                {
                    TextBlock shape = (TextBlock)selectedElt;
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
                            shape.FontSize = double.Parse(Fix(value));
                            break;
                        case "FontWeight":
                            shape.FontWeight = value.ToLower() == "bold" ? FontWeights.Bold : FontWeights.Normal;
                            break;
                    }
                }
                else if (selectedElt.GetType() == typeof(Image))
                {
                    Image shape = (Image)selectedElt;
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
                else if (selectedElt.GetType() == typeof(Button))
                {
                    Button shape = (Button)selectedElt;
                    switch (property.Property)
                    {
                        case "Name":
                            shape.Name = value;
                            break;
                        case "Content":
                            shape.Content = value;
                            break;
                        default:
                            SetControlProperties(shape, property, value);
                            break;
                    }
                }
                else if (selectedElt.GetType() == typeof(TextBox))
                {
                    TextBox shape = (TextBox)selectedElt;
                    switch (property.Property)
                    {
                        case "Name":
                            shape.Name = value;
                            break;
                        case "Text":
                            shape.Text = value;
                            break;
                        default:
                            SetControlProperties(shape, property, value);
                            break;
                    }
                }
                else if (selectedElt.GetType() == typeof(WebBrowser))
                {
                    WebBrowser shape = (WebBrowser)selectedElt;
                    switch (property.Property)
                    {
                        case "Url":
                            shape.Navigate(new Uri(value));
                            break;
                    }
                }
                else if (selectedElt.GetType() == typeof(CheckBox))
                {
                    CheckBox shape = (CheckBox)selectedElt;
                    switch (property.Property)
                    {
                        case "Content":
                            shape.Content = value;
                            break;
                        default:
                            SetControlProperties(shape, property, value);
                            break;
                    }
                }
                else if (selectedElt.GetType() == typeof(ComboBox))
                {
                    ComboBox shape = (ComboBox)selectedElt;
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
                            shape.MaxDropDownHeight = double.Parse(Fix(value));
                            break;
                        default:
                            SetControlProperties(shape, property, value);
                            break;
                    }
                }
                else if (selectedElt.GetType() == typeof(WindowsFormsHost))
                {
                    WindowsFormsHost shape = (WindowsFormsHost)selectedElt;
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
                else if (selectedElt.GetType() == typeof(DocumentViewer))
                {
                    DocumentViewer shape = (DocumentViewer)selectedElt;
                }
                else if (selectedElt.GetType() == typeof(ListBox))
                {
                    ListBox shape = (ListBox)selectedElt;
                    switch (property.Property)
                    {
                        case "List":
                            string[] list = value.Split(new char[] { '=', ';' }, StringSplitOptions.RemoveEmptyEntries);
                            shape.Items.Clear();
                            for (int i = 1; i < list.Length; i += 2)
                            {
                                ListBoxItem listBoxItem = new ListBoxItem();
                                listBoxItem.Content = list[i];
                                shape.Items.Add(listBoxItem);
                            }
                            break;
                        default:
                            SetControlProperties(shape, property, value);
                            break;
                    }
                }
                else if (selectedElt.GetType() == typeof(ListView))
                {
                    ListView shape = (ListView)selectedElt;
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
                else if (selectedElt.GetType() == typeof(MediaElement))
                {
                    MediaElement shape = (MediaElement)selectedElt;
                }
                else if (selectedElt.GetType() == typeof(Menu))
                {
                    Menu shape = (Menu)selectedElt;
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
                        default:
                            SetControlProperties(shape, property, value);
                            break;
                    }
                }
                else if (selectedElt.GetType() == typeof(PasswordBox))
                {
                    PasswordBox shape = (PasswordBox)selectedElt;
                    switch (property.Property)
                    {
                        case "MaxLength":
                            shape.MaxLength = int.Parse(Fix(value));
                            break;
                        default:
                            SetControlProperties(shape, property, value);
                            break;
                    }
                }
                else if (selectedElt.GetType() == typeof(ProgressBar))
                {
                    ProgressBar shape = (ProgressBar)selectedElt;
                    switch (property.Property)
                    {
                        case "Foreground":
                            shape.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
                            break;
                        case "Orientation":
                            shape.Orientation = value.ToLower()[0] == 'h' ? Orientation.Horizontal : Orientation.Vertical;
                            break;
                    }
                }
                else if (selectedElt.GetType() == typeof(RadioButton))
                {
                    RadioButton shape = (RadioButton)selectedElt;
                    switch (property.Property)
                    {
                        case "Title":
                            shape.Content = value;
                            break;
                        case "Group":
                            shape.GroupName = value;
                            break;
                        default:
                            SetControlProperties(shape, property, value);
                            break;
                    }
                }
                else if (selectedElt.GetType() == typeof(RichTextBox))
                {
                    RichTextBox shape = (RichTextBox)selectedElt;
                    SetControlProperties(shape, property, value);
                }
                else if (selectedElt.GetType() == typeof(Slider))
                {
                    Slider shape = (Slider)selectedElt;
                    switch (property.Property)
                    {
                        case "Orientation":
                            shape.Orientation = value.ToLower()[0] == 'h' ? Orientation.Horizontal : Orientation.Vertical;
                            break;
                    }
                }
                else if (selectedElt.GetType() == typeof(TreeView))
                {
                    TreeView shape = (TreeView)selectedElt;
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
                        default:
                            SetControlProperties(shape, property, value);
                            break;
                    }
                }
                canvas.UpdateLayout();
            }
            catch
            {

            }
        }

        private void UpdatePolygonSize(FrameworkElement shape)
        {
            if (shape.GetType() == typeof(Polygon) || shape.GetType() == typeof(Line))
            {
                double minX = double.MaxValue;
                double minY = double.MaxValue;
                double maxX = -double.MaxValue;
                double maxY = -double.MaxValue;
                if (shape.GetType() == typeof(Polygon))
                {
                    foreach (Point point in ((Polygon)shape).Points)
                    {
                        minX = Math.Min(minX, point.X);
                        minY = Math.Min(minY, point.Y);
                        maxX = Math.Max(maxX, point.X);
                        maxY = Math.Max(maxY, point.Y);
                    }
                    for (int i = 0; i < ((Polygon)shape).Points.Count; i++)
                    {
                        Point point = ((Polygon)shape).Points[i];
                        ((Polygon)shape).Points[i] = new Point(point.X - minX, point.Y - minY);
                    }
                }
                else if (shape.GetType() == typeof(Line))
                {
                    Line line = (Line)shape;
                    minX = Math.Min(line.X1, line.X2);
                    minY = Math.Min(line.Y1, line.Y2);
                    maxX = Math.Max(line.X1, line.X2);
                    maxY = Math.Max(line.Y1, line.Y2);
                    line.X1 -= minX;
                    line.Y1 -= minY;
                    line.X2 -= minX;
                    line.Y2 -= minY;
                }
                shape.Width = maxX - minX;
                shape.Height = maxY - minY;
                Shape parent = (Shape)shape.Tag;
                Canvas.SetLeft(parent.shape, Canvas.GetLeft(parent.shape) + minX);
                Canvas.SetTop(parent.shape, Canvas.GetTop(parent.shape) + minY);

                currentShape.modifiers["Width"] = Fix(shape.Width).ToString();
                currentShape.modifiers["Height"] = Fix(shape.Height).ToString();
                currentShape.modifiers["Left"] = Fix(Canvas.GetLeft(parent.shape) + Shape.HandleShort).ToString();
                currentShape.modifiers["Top"] = Fix(Canvas.GetTop(parent.shape) + Shape.HandleShort).ToString();
            }
        }

        private void SetControlProperties(Control shape, PropertyData property, string value)
        {
            switch (property.Property)
            {
                case "Foreground":
                    shape.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(property.Value));
                    break;
                case "FontFamily":
                    shape.FontFamily = new FontFamily(value);
                    break;
                case "FontStyle":
                    shape.FontStyle = value.ToLower() == "italic" ? FontStyles.Italic : FontStyles.Normal;
                    break;
                case "FontSize":
                    shape.FontSize = double.Parse(Fix(value));
                    break;
                case "FontWeight":
                    shape.FontWeight = value.ToLower() == "bold" ? FontWeights.Bold : FontWeights.Normal;
                    break;
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
                value = Fix(value);
                foreach (Shape selectedShape in selectedShapes)
                {
                    FrameworkElement selectedElt = selectedShape.elt;
                    switch (property.Property)
                    {
                        case "Width":
                            selectedShape.modifiers["Width"] = value;
                            if (selectedElt.GetType() == typeof(Polygon))
                            {
                                Polygon polygon = (Polygon)selectedShape.elt;
                                double scale = double.Parse(value) / selectedShape.elt.ActualWidth;
                                for (int i = 0; i < polygon.Points.Count; i++)
                                {
                                    polygon.Points[i] = new Point(polygon.Points[i].X * scale, polygon.Points[i].Y);
                                }
                            }
                            else if (selectedElt.GetType() == typeof(Line))
                            {
                                Line line = (Line)currentElt;
                                double scale = double.Parse(value) / selectedShape.elt.ActualWidth;
                                line.X1 *= scale;
                                line.X2 *= scale;
                            }
                            else
                            {
                                selectedElt.Width = double.Parse(value);
                            }
                            break;
                        case "Height":
                            selectedShape.modifiers["Height"] = value;
                            if (selectedElt.GetType() == typeof(Polygon))
                            {
                                Polygon polygon = (Polygon)selectedShape.elt;
                                double scale = double.Parse(value) / selectedShape.elt.ActualHeight;
                                for (int i = 0; i < polygon.Points.Count; i++)
                                {
                                    polygon.Points[i] = new Point(polygon.Points[i].X, polygon.Points[i].Y * scale);
                                }
                            }
                            else if (selectedElt.GetType() == typeof(Line))
                            {
                                Line line = (Line)currentElt;
                                double scale = double.Parse(value) / selectedShape.elt.ActualHeight;
                                line.Y1 *= scale;
                                line.Y2 *= scale;
                            }
                            else
                            {
                                selectedElt.Height = double.Parse(value);
                            }
                            break;
                        case "Left":
                            selectedShape.modifiers["Left"] = value;
                            Canvas.SetLeft(selectedShape.shape, double.Parse(value) - Shape.HandleShort);
                            break;
                        case "Top":
                            selectedShape.modifiers["Top"] = value;
                            Canvas.SetTop(selectedShape.shape, double.Parse(value) - Shape.HandleShort);
                            break;
                        case "Angle":
                            selectedShape.modifiers["Angle"] = value;
                            RotateShape(selectedShape);
                            UpdatePolygonHandles();
                            selectedShape.ShowHandles(true); //polygon handles on rotation
                            break;
                        case "Opacity":
                            selectedShape.modifiers["Opacity"] = value;
                            selectedElt.Opacity = double.Parse(value) / 100.0;
                            break;
                    }
                }
                UpdateView();
            }
            catch
            {

            }
        }

        private void RotateShape(Shape shape)
        {
            RotateTransform rotateTransform = new RotateTransform();
            shape.elt.Measure(new Size(double.MaxValue, double.MaxValue));
            rotateTransform.CenterX = shape.elt.DesiredSize.Width / 2.0;
            rotateTransform.CenterY = shape.elt.DesiredSize.Height / 2.0;
            rotateTransform.Angle = double.Parse(shape.modifiers["Angle"]);

            if (shape.elt.GetType() == typeof(Polygon))
            {
                Polygon polygon = (Polygon)shape.elt;
                for (int i = 0; i < polygon.Points.Count; i++)
                {
                    Point point = new Point(polygon.Points[i].X, polygon.Points[i].Y);
                    point = rotateTransform.Transform(point);
                    polygon.Points[i] = point;
                    //point = shape.elt.TranslatePoint(point, THIS.canvas);
                }
                shape.modifiers["Angle"] = "0";
                UpdatePolygonSize(shape.elt);
            }
            if (shape.elt.GetType() == typeof(Line))
            {
                Line line = (Line)shape.elt;
                Point point = new Point(line.X1, line.Y1);
                point = rotateTransform.Transform(point);
                line.X1 = point.X;
                line.Y1 = point.Y;
                point = new Point(line.X2, line.Y2);
                point = rotateTransform.Transform(point);
                line.X2 = point.X;
                line.Y2 = point.Y;
                shape.modifiers["Angle"] = "0";
                UpdatePolygonSize(shape.elt);
            }
            else
            {
                shape.elt.RenderTransform = new TransformGroup();
                ((TransformGroup)shape.elt.RenderTransform).Children.Add(rotateTransform);
            }
        }

        private void buttonDelete_Click(object sender, RoutedEventArgs e)
        {
            for (int i = selectedShapes.Count-1; i >= 0; i--)
            {
                Delete(selectedShapes[i]);
            }
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

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            int nudge = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift) ? snap : 1;
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
                            Delete((Shape)elt.Tag);
                            break;
                        }
                    }
                }
                e.Handled = true;
                UpdateView();
            }
            else if (e.Key == Key.Left)
            {
                Shape _currentShape = currentShape;
                foreach (Shape selectedShape in selectedShapes)
                {
                    Canvas.SetLeft(selectedShape.shape, Canvas.GetLeft(selectedShape.shape) - nudge);
                    selectedShape.modifiers["Left"] = (Canvas.GetLeft(selectedShape.shape) + Shape.HandleShort).ToString();
                    currentShape = selectedShape;
                    currentElt = currentShape.elt;
                    UpdatePolygonHandles();
                }
                if (null != _currentShape)
                {
                    currentShape = _currentShape;
                    currentElt = currentShape.elt;
                }
                e.Handled = true;
                UpdateView();
            }
            else if (e.Key == Key.Right)
            {
                Shape _currentShape = currentShape;
                foreach (Shape selectedShape in selectedShapes)
                {
                    Canvas.SetLeft(selectedShape.shape, Canvas.GetLeft(selectedShape.shape) + nudge);
                    selectedShape.modifiers["Left"] = (Canvas.GetLeft(selectedShape.shape) + Shape.HandleShort).ToString();
                    currentShape = selectedShape;
                    currentElt = currentShape.elt;
                    UpdatePolygonHandles();
                }
                if (null != _currentShape)
                {
                    currentShape = _currentShape;
                    currentElt = currentShape.elt;
                }
                e.Handled = true;
                UpdateView();
            }
            else if (e.Key == Key.Up)
            {
                Shape _currentShape = currentShape;
                foreach (Shape selectedShape in selectedShapes)
                {
                    Canvas.SetTop(selectedShape.shape, Canvas.GetTop(selectedShape.shape) - nudge);
                    selectedShape.modifiers["Top"] = (Canvas.GetTop(selectedShape.shape) + Shape.HandleShort).ToString();
                    currentShape = selectedShape;
                    currentElt = currentShape.elt;
                    UpdatePolygonHandles();
                }
                if (null != _currentShape)
                {
                    currentShape = _currentShape;
                    currentElt = currentShape.elt;
                }
                e.Handled = true;
                UpdateView();
            }
            else if (e.Key == Key.Down)
            {
                Shape _currentShape = currentShape;
                foreach (Shape selectedShape in selectedShapes)
                {
                    Canvas.SetTop(selectedShape.shape, Canvas.GetTop(selectedShape.shape) + nudge);
                    selectedShape.modifiers["Top"] = (Canvas.GetTop(selectedShape.shape) + Shape.HandleShort).ToString();
                    currentShape = selectedShape;
                    currentElt = currentShape.elt;
                    UpdatePolygonHandles();
                }
                if (null != _currentShape)
                {
                    currentShape = _currentShape;
                    currentElt = currentShape.elt;
                }
                e.Handled = true;
                UpdateView();
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
                    ShowCode();
                }
            }
            catch
            {

            }
        }

        private void buttonHelp_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Add shapes or controls by clicking shape name on left panel.\n\n" +
                "Select a shape by clicking it or blue move handle.\n" +
                "The properties of a selected shape are shown on the right panel, they can be changed by editing or using Set button.\n\n" +
                "Select multiple shapes by selecting a shape with Shift key held down or by selecting round a group of shapes (Shift to add to current selected shapes).\n" +
                "Deslect all shapes by left click on the backgound.\n\n" +
                "Move, resize (using corner handles) or change the properties of a shape (holding Shift to apply to multiple selected shapes).\n" +
                "Triangles and polygons are resized by moving individual corners.\n\n" +
                "Nudge selected shapes using arrow keys (hold Shift to move by snap distance).\n\n" +
                "Press Delete over a shape to delete it.\n\n" +
                "Right click for additional options, including copy and paste code to current document, or copy selected shapes (new copied shapes lie over the originals so can then be moved as a group using Shift key).\n\n" +
                "The current code is shown in the code window.  This can be edited and use \"Import From Code\" to add the current code to the current view.\n\n" +
                "Questions and suggestions welcome.",
                "SB-IDE", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
