using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SB_IDE.Dialogs
{
    /// <summary>
    /// Interaction logic for FlowChart.xaml
    /// </summary>
    public partial class FlowChart : Window
    {
        public static bool Active = false;
        public static FlowChart THIS;
        private MainWindow mainWindow;
        private SBDocument sbDocument;
        private List<CodeLine> codeLines = new List<CodeLine>();
        List<string> subs = new List<string>();
        private bool groupStatements = true;
        private double width = 150;
        private double height = 50;
        private double widthSpace = 50;
        private double heightSpace = 100;
        private double borderSpace = 50;
        private int maxrow;
        private int maxcol;
        private Duration animationDuration = new Duration(new TimeSpan(5000000));
        private ScaleTransform scaleTransform = new ScaleTransform();
        private double scaleView = 1;
        private Timer timer;
        private double scrollStep;

        public FlowChart(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;
            THIS = this;

            InitializeComponent();

            FontSize = 12 + MainWindow.zoom;
            Topmost = true;

            Height = SystemParameters.PrimaryScreenHeight - 40;
            Left = SystemParameters.PrimaryScreenWidth - Width - 20;
            Top = (SystemParameters.PrimaryScreenHeight - Height) / 2;

            canvas.RenderTransform = new TransformGroup();
            scaleTransform.CenterX = 0;
            scaleTransform.CenterY = 0;
            canvas.RenderTransform = new TransformGroup();
            canvas.RenderTransform = new TransformGroup();
            ((TransformGroup)canvas.RenderTransform).Children.Add(scaleTransform);

            Display();
        }

        private void Zoom(double scale)
        {
            scaleView = Math.Min(1.0, scaleTransform.ScaleX * scale);
            scale = scaleView / scaleTransform.ScaleX;

            DoubleAnimation scaleAnimaton = new DoubleAnimation();
            scaleAnimaton.Duration = animationDuration;
            scaleAnimaton.From = scaleTransform.ScaleX;
            scaleAnimaton.To = scaleTransform.ScaleX * scale;
            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimaton);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimaton);

            DoubleAnimation canvasWidthAnimaton = new DoubleAnimation();
            canvasWidthAnimaton.Duration = animationDuration;
            canvasWidthAnimaton.From = canvas.Width;
            canvasWidthAnimaton.To = canvas.Width * scale;
            canvas.BeginAnimation(Canvas.WidthProperty, canvasWidthAnimaton);

            DoubleAnimation canvasHeightAnimaton = new DoubleAnimation();
            canvasHeightAnimaton.Duration = animationDuration;
            canvasHeightAnimaton.From = canvas.Height;
            canvasHeightAnimaton.To = canvas.Height * scale;
            canvas.BeginAnimation(Canvas.HeightProperty, canvasHeightAnimaton);
        }

        public void Display()
        {
            Cursor cursor = Mouse.OverrideCursor;
            Mouse.OverrideCursor = Cursors.Wait;

            try
            {
                canvas.Children.Clear();
                codeLines.Clear();
                subs.Clear();
                sbDocument = mainWindow.GetActiveDocument();
                scaleView = 1;
                scaleTransform.ScaleX = 1.0;
                scaleTransform.ScaleY = 1.0;

                Parse();

                PathFigure pathFigure = new PathFigure();
                pathFigure.StartPoint = new Point(0, height / 2);
                pathFigure.Segments.Add(new LineSegment() { Point = new Point(width / 2, 0) });
                pathFigure.Segments.Add(new LineSegment() { Point = new Point(width, height / 2) });
                pathFigure.Segments.Add(new LineSegment() { Point = new Point(width / 2, height) });
                PathGeometry pathGeometry = new PathGeometry();
                pathGeometry.Figures = new PathFigureCollection();
                pathGeometry.Figures.Add(pathFigure);

                maxrow = 0;
                maxcol = 0;
                foreach (CodeLine codeLine in codeLines)
                {
                    Brush background;
                    switch (codeLine.block)
                    {
                        case eBlock.IF:
                        case eBlock.ELSE:
                        case eBlock.ELSEIF:
                            background = new SolidColorBrush(Colors.Red);
                            break;
                        case eBlock.START:
                        case eBlock.SUB:
                            background = new SolidColorBrush(Colors.Green);
                            break;
                        case eBlock.GOTO:
                        case eBlock.LABEL:
                        case eBlock.CALL:
                            background = new SolidColorBrush(Colors.Orange);
                            break;
                        case eBlock.FOR:
                        case eBlock.ENDFOR:
                            background = new SolidColorBrush(Colors.DarkCyan);
                            break;
                        case eBlock.WHILE:
                        case eBlock.ENDWHILE:
                            background = new SolidColorBrush(Colors.DeepPink);
                            break;
                        default:
                            background = new SolidColorBrush(Colors.Blue);
                            break;
                    }

                    Geometry geometry;
                    switch (codeLine.block)
                    {
                        case eBlock.IF:
                        case eBlock.ELSE:
                        case eBlock.ELSEIF:
                            geometry = pathGeometry;
                            break;
                        case eBlock.START:
                        case eBlock.SUB:
                        case eBlock.LABEL:
                            geometry = new EllipseGeometry(new Point(width / 2, height / 2), width / 2, height / 2);
                            break;
                        default:
                            geometry = new RectangleGeometry(new Rect(0, 0, width, height));
                            break;
                    }

                    int row = codeLine.row;
                    int col = codeLine.col;

                    if (codeLine.block != eBlock.START && codeLine.block != eBlock.SUB && codeLine.block != eBlock.ELSE && codeLine.block != eBlock.ELSEIF)
                    {
                        ConnectEndIf(row, col, 0, col);
                    }

                    if (codeLine.block == eBlock.ELSE || codeLine.block == eBlock.ELSEIF)
                    {
                        Line connect = new Line()
                        {
                            X1 = borderSpace + (width + widthSpace) * col + 2,
                            X2 = borderSpace + (width + widthSpace) * col - widthSpace - 2,
                            Y1 = borderSpace + heightSpace * row + height / 2,
                            Y2 = borderSpace + heightSpace * row + height / 2,
                            Stroke = new SolidColorBrush(Colors.Black),
                            StrokeThickness = 2,
                        };
                        canvas.Children.Add(connect);

                        int testCol = col - 1;
                        while (null == HasSymbol(row, testCol) && testCol > 0)
                        {
                            Line connect2 = new Line()
                            {
                                X1 = borderSpace + (width + widthSpace) * (testCol + 1) + 2,
                                X2 = borderSpace + (width + widthSpace) * testCol - widthSpace - 2,
                                Y1 = borderSpace + heightSpace * row + height / 2,
                                Y2 = borderSpace + heightSpace * row + height / 2,
                                Stroke = new SolidColorBrush(Colors.Black),
                                StrokeThickness = 2,
                            };
                            canvas.Children.Add(connect2);
                            testCol--;
                        }

                        TextBlock condition = new TextBlock()
                        {
                            Foreground = new SolidColorBrush(Colors.Black),
                            Text = "False",
                        };
                        canvas.Children.Add(condition);
                        Canvas.SetLeft(condition, borderSpace + (width + widthSpace) * col - widthSpace);
                        Canvas.SetTop(condition, borderSpace + heightSpace * row + height / 2 + 5);

                        ImageSource arrow = MainWindow.ImageSourceFromBitmap(Properties.Resources.Arrow);
                        Image img = new Image()
                        {
                            Width = 24,
                            Height = 24,
                            Source = arrow,
                        };
                        RotateTransform rotateTransform = new RotateTransform();
                        rotateTransform.CenterX = 12;
                        rotateTransform.CenterY = 12;
                        rotateTransform.Angle = 180;
                        img.RenderTransform = new TransformGroup();
                        ((TransformGroup)img.RenderTransform).Children.Add(rotateTransform);
                        canvas.Children.Add(img);
                        Canvas.SetLeft(img, borderSpace + (width + widthSpace) * col - 22);
                        Canvas.SetTop(img, borderSpace + heightSpace * row + height / 2 - 11);
                        Canvas.SetZIndex(img, 1);
                    }

                    if (codeLine.block == eBlock.IF || codeLine.block == eBlock.ELSEIF)
                    {
                        TextBlock condition = new TextBlock()
                        {
                            Foreground = new SolidColorBrush(Colors.Black),
                            Text = "True",
                        };
                        canvas.Children.Add(condition);
                        Canvas.SetLeft(condition, borderSpace + (width + widthSpace) * col + width / 2 + 5);
                        Canvas.SetTop(condition, borderSpace + heightSpace * row + height / 2 + 30);
                    }

                    if (codeLine.block == eBlock.ENDIF)
                    {
                        int rowIf = codeLine.rootLine.row;
                        for (int colIf = 0; colIf <= maxcol; colIf++)
                        {
                            CodeLine cl = HasSymbol(rowIf, colIf);
                            if (null != cl && cl.rootLine == codeLine.rootLine)
                            {
                                ConnectEndIf(codeLine.row, codeLine.col, rowIf, colIf);
                            }
                        }
                    }

                    if (codeLine.block == eBlock.ENDFOR || codeLine.block == eBlock.ENDWHILE || codeLine.block == eBlock.GOTO)
                    {
                        ConnectLoop(codeLine.row, codeLine.rootLine.row, codeLine.col);
                    }

                    TextBlock tb = new TextBlock()
                    {
                        Foreground = new SolidColorBrush(Colors.White),
                        Text = codeLine.code,
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center,
                    };
                    Border border = new Border()
                    {
                        Background = background,
                        Child = tb,
                        Width = width,
                        Height = height,
                        Clip = geometry,
                        ToolTip = codeLine.code,
                        Tag = codeLine,
                        //BorderBrush = new SolidColorBrush(Colors.Black),
                        //BorderThickness = new Thickness(2),
                        CornerRadius = new CornerRadius(5),
                    };
                    codeLine.border = border;
                    border.MouseDown += new MouseButtonEventHandler(codeClick);

                    canvas.Children.Add(border);
                    Canvas.SetLeft(border, borderSpace + (width + widthSpace) * col);
                    Canvas.SetTop(border, borderSpace + heightSpace * row++);
                    maxcol = Math.Max(maxcol, col);
                    maxrow = Math.Max(maxrow, row);
                }

                canvas.Width = -widthSpace + 2 * borderSpace + (width + widthSpace) * (maxcol + 1);
                canvas.Height = borderSpace + heightSpace * maxrow;
            }
            catch (Exception ex)
            {
                MainWindow.Errors.Add(new Error("Flow Chart : " + ex.Message));
            }
            Mouse.OverrideCursor = cursor;
        }

        private void codeClick(object sender, MouseButtonEventArgs e)
        {
            Border border = (Border)sender;
            CodeLine codeLine = (CodeLine)border.Tag;
            if (null != codeLine.rootLine)
            {
                codeLine = codeLine.rootLine;
            }
            if (null != codeLine.border)
            {
                Border borderRoot = codeLine.border;
                Point start = new Point(scrollViewer.HorizontalOffset, scrollViewer.VerticalOffset);
                Point end = borderRoot.TranslatePoint(new Point(0, 0), canvas);

                scrollStep = 0;
                timer = new Timer(_timer, new Point[] { start, end }, 0, 10);
            }
        }

        private void _timer(object state)
        {
            Point[] data = (Point[])state;
            Dispatcher.Invoke(() =>
            {
                double x = (1 - scrollStep) * data[0].X + scrollStep * (data[1].X - widthSpace) * scaleView;
                double y = (1 - scrollStep) * data[0].Y + scrollStep * (data[1].Y - heightSpace) * scaleView;
                scrollStep += 0.1;
                scrollStep = Math.Min(1, scrollStep);
                scrollViewer.ScrollToHorizontalOffset(x);
                scrollViewer.ScrollToVerticalOffset(y);
                if (scrollStep >= 1) timer.Dispose();
            });
        }

        // Connect right and up from row1,col1 to row2,col2
        private void ConnectEndIf(int row1, int col1, int row2, int col2)
        {
            Line connect;
            ImageSource arrow = MainWindow.ImageSourceFromBitmap(Properties.Resources.Arrow);
            Image img;

            if (col2 > col1)
            {
                connect = new Line()
                {
                    X1 = borderSpace + (width + widthSpace) * col1 + width,
                    X2 = borderSpace + (width + widthSpace) * col2 + width / 2,
                    Y1 = borderSpace + heightSpace * row1 + height / 2,
                    Y2 = borderSpace + heightSpace * row1 + height / 2,
                    Stroke = new SolidColorBrush(Colors.Black),
                    StrokeThickness = 2,
                };
                canvas.Children.Add(connect);

                connect = new Line()
                {
                    X1 = borderSpace + (width + widthSpace) * col2 + width / 2,
                    X2 = borderSpace + (width + widthSpace) * col2 + width / 2,
                    Y1 = borderSpace + heightSpace * row1 + height / 2,
                    Y2 = borderSpace + heightSpace * row1,
                    Stroke = new SolidColorBrush(Colors.Black),
                    StrokeThickness = 2,
                };
                canvas.Children.Add(connect);

                img = new Image()
                {
                    Width = 24,
                    Height = 24,
                    Source = arrow
                };
                canvas.Children.Add(img);
                Canvas.SetLeft(img, borderSpace + (width + widthSpace) * col1 + width - 3);
                Canvas.SetTop(img, borderSpace + heightSpace * row1 + height / 2 - 13);
                Canvas.SetZIndex(img, 1);
            }

            int rowUp = row1 - 1;
            while (rowUp >= 0 && null == HasSymbol(rowUp, col2))
            {
                connect = new Line()
                {
                    X1 = borderSpace + (width + widthSpace) * col2 + width / 2,
                    X2 = borderSpace + (width + widthSpace) * col2 + width / 2,
                    Y1 = borderSpace + heightSpace * (rowUp + 1) + height / 2,
                    Y2 = borderSpace + heightSpace * rowUp,
                    Stroke = new SolidColorBrush(Colors.Black),
                    StrokeThickness = 2,
                };
                canvas.Children.Add(connect);
                rowUp--;
            }

            connect = new Line()
            {
                X1 = borderSpace + (width + widthSpace) * col2 + width / 2,
                X2 = borderSpace + (width + widthSpace) * col2 + width / 2,
                Y1 = borderSpace + heightSpace * (rowUp + 1),
                Y2 = borderSpace + heightSpace * rowUp + height,
                Stroke = new SolidColorBrush(Colors.Black),
                StrokeThickness = 2,
            };
            canvas.Children.Add(connect);

            img = new Image()
            {
                Width = 24,
                Height = 24,
                Source = arrow,
            };
            RotateTransform rotateTransform = new RotateTransform();
            rotateTransform.CenterX = 12;
            rotateTransform.CenterY = 12;
            rotateTransform.Angle = -90;
            img.RenderTransform = new TransformGroup();
            ((TransformGroup)img.RenderTransform).Children.Add(rotateTransform);
            canvas.Children.Add(img);
            Canvas.SetLeft(img, borderSpace + (width + widthSpace) * col1 + width / 2 - 13);
            Canvas.SetTop(img, borderSpace + heightSpace * row1 - 24);
            Canvas.SetZIndex(img, 1);
        }

        // Connect left and up from row1 to row2 on col
        private void ConnectLoop(int row1, int row2, int col)
        {
            Line connect;
            ImageSource arrow = MainWindow.ImageSourceFromBitmap(Properties.Resources.Arrow);
            Image img;
            int space = 26;

            List<CodeLine> working = new List<CodeLine>();
            for (int row = row2; row <= row1; row++)
            {
                CodeLine codeLine = HasSymbol(row, col);
                if (null != codeLine)
                {
                    working.Add(codeLine);
                    space = Math.Max(space, codeLine.linkDist + 4);
                }
            }
            space = Math.Min(space, (int)widthSpace - 4);
            foreach (CodeLine codeLine in working)
            {
                codeLine.linkDist = space;
            }

            connect = new Line()
            {
                X1 = borderSpace + (width + widthSpace) * col,
                X2 = borderSpace + (width + widthSpace) * col - space,
                Y1 = borderSpace + heightSpace * row1 + height / 2,
                Y2 = borderSpace + heightSpace * row1 + height / 2,
                Stroke = new SolidColorBrush(Colors.Black),
                StrokeThickness = 2,
            };
            canvas.Children.Add(connect);

            connect = new Line()
            {
                X1 = borderSpace + (width + widthSpace) * col - space,
                X2 = borderSpace + (width + widthSpace) * col - space,
                Y1 = borderSpace + heightSpace * row1 + height / 2,
                Y2 = borderSpace + heightSpace * row2 + height / 2,
                Stroke = new SolidColorBrush(Colors.Black),
                StrokeThickness = 2,
            };
            canvas.Children.Add(connect);

            connect = new Line()
            {
                X1 = borderSpace + (width + widthSpace) * col - space,
                X2 = borderSpace + (width + widthSpace) * col,
                Y1 = borderSpace + heightSpace * row2 + height / 2,
                Y2 = borderSpace + heightSpace * row2 + height / 2,
                Stroke = new SolidColorBrush(Colors.Black),
                StrokeThickness = 2,
            };
            canvas.Children.Add(connect);

            img = new Image()
            {
                Width = 24,
                Height = 24,
                Source = arrow,
            };
            RotateTransform rotateTransform = new RotateTransform();
            rotateTransform.CenterX = 12;
            rotateTransform.CenterY = 12;
            rotateTransform.Angle = 180;
            img.RenderTransform = new TransformGroup();
            ((TransformGroup)img.RenderTransform).Children.Add(rotateTransform);
            canvas.Children.Add(img);
            Canvas.SetLeft(img, borderSpace + (width + widthSpace) * col - 24);
            Canvas.SetTop(img, borderSpace + heightSpace * row2 + height / 2 - 11);
            Canvas.SetZIndex(img, 1);
        }

        private void Parse()
        {
            string[] lines = sbDocument.TextArea.Text.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            Stack<CodeLine> sSub = new Stack<CodeLine>();
            Stack<CodeLine> sIf = new Stack<CodeLine>();
            Stack<CodeLine> sFor = new Stack<CodeLine>();
            Stack<CodeLine> sWhile = new Stack<CodeLine>();
            Dictionary<string, CodeLine> labels = new Dictionary<string, CodeLine>();

            codeLines.Add(new CodeLine("START", eBlock.START));
            for (int i = 0; i < lines.Length; i++)
            {
                string line = Clean(lines[i]);
                if (line.Length == 0) continue;
                string lineLower = line.ToLower();

                if (Regex.Match(lineLower, "^(sub)[\\W]").Success)
                {
                    codeLines.Add(new CodeLine(line, eBlock.SUB));
                    sSub.Push(codeLines.Last());
                }
                else if (lineLower == "endsub")
                {
                    codeLines.Add(new CodeLine(line, eBlock.ENDSUB, sSub.Pop()));
                }
                else if (Regex.Match(lineLower, "^(if)[\\W]").Success)
                {
                    codeLines.Add(new CodeLine(line, eBlock.IF));
                    sIf.Push(codeLines.Last());
                }
                else if (lineLower == "else")
                {
                    codeLines.Add(new CodeLine(line, eBlock.ELSE, sIf.Peek()));
                    sIf.Peek().hasElse = true;
                }
                else if (Regex.Match(lineLower, "^(elseif)[\\W]").Success)
                {
                    codeLines.Add(new CodeLine(line, eBlock.ELSEIF, sIf.Peek()));
                    sIf.Peek().hasElse = true;
                }
                else if (lineLower == "endif")
                {
                    if (!sIf.Peek().hasElse)
                    {
                        codeLines.Add(new CodeLine("Else", eBlock.ELSE, sIf.Peek()));
                    }
                    codeLines.Add(new CodeLine(line, eBlock.ENDIF, sIf.Pop()));
                }
                else if (Regex.Match(lineLower, "^(for)[\\W]").Success)
                {
                    codeLines.Add(new CodeLine(line, eBlock.FOR));
                    sFor.Push(codeLines.Last());
                }
                else if (lineLower == "endfor")
                {
                    codeLines.Add(new CodeLine(line, eBlock.ENDFOR, sFor.Pop()));
                }
                else if (Regex.Match(lineLower, "^(while)[\\W]").Success)
                {
                    codeLines.Add(new CodeLine(line, eBlock.WHILE));
                    sWhile.Push(codeLines.Last());
                }
                else if (lineLower == "endwhile")
                {
                    codeLines.Add(new CodeLine(line, eBlock.ENDWHILE, sWhile.Pop()));
                }
                else if (Regex.Match(lineLower, "^(goto)[\\W]").Success)
                {
                    codeLines.Add(new CodeLine(line, eBlock.GOTO));
                }
                else if (Regex.Match(lineLower, "^[a-z_][\\w]*[ ]*[:]").Success)
                {
                    codeLines.Add(new CodeLine(line, eBlock.LABEL));
                    labels[lineLower.Substring(0, lineLower.Length - 1).Trim()] = codeLines.Last();
                }
                else if (Regex.Match(lineLower, "^[a-z_][\\w]*[ ]*[()]").Success)
                {
                    codeLines.Add(new CodeLine(line, eBlock.CALL));
                }
                else
                {
                    if (groupStatements && codeLines.Last().block == eBlock.STATEMENT)
                    {
                        codeLines.Last().code += "\n" + line;
                    }
                    else
                    {
                        codeLines.Add(new CodeLine(line, eBlock.STATEMENT));
                    }
                }
            }
            codeLines.Add(new CodeLine("END", eBlock.END));

            // Set parents for GOTO
            foreach (KeyValuePair<string, CodeLine> kvp in labels)
            {
                foreach (CodeLine codeLine in codeLines)
                {
                    if (codeLine.block == eBlock.GOTO && codeLine.code.ToLower().EndsWith(kvp.Key))
                    {
                        codeLine.rootLine = kvp.Value;
                    }
                }
            }

            // Identify subroutines
            string sub = "";
            foreach (CodeLine codeLine in codeLines)
            {
                if (codeLine.block == eBlock.SUB)
                {
                    sub = codeLine.code.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Last().ToLower();
                    subs.Add(sub);
                    foreach (CodeLine codeLineCall in codeLines)
                    {
                        if (codeLineCall.block == eBlock.CALL && codeLineCall.code.ToLower().StartsWith(sub))
                        {
                            codeLineCall.rootLine = codeLine;
                        }
                    }
                }
                codeLine.sub = sub;
                if (codeLine.block == eBlock.ENDSUB) sub = "";
            }

            // Set row and col locations
            int col = 0;
            int row = 0;
            int nextCol = 0;
            int ifLevel = 0;
            maxrow = 0;
            maxcol = 0;
            for (int iSub = -1; iSub < subs.Count; iSub++)
            {
                foreach (CodeLine codeLine in codeLines)
                {
                    if (iSub < 0 && codeLine.sub != "") continue;
                    if (iSub >= 0 && codeLine.sub != subs[iSub]) continue;

                    if (codeLine.block == eBlock.IF)
                    {
                        if (ifLevel == 0)
                        {
                            nextCol = 0;
                            maxcol = 0;
                        }
                        codeLine.nextCol = col;
                        row = maxrow;
                        row = GetRow(row, col);
                        ifLevel++;
                    }
                    else if (codeLine.block == eBlock.ELSE || codeLine.block == eBlock.ELSEIF)
                    {
                        nextCol++;
                        codeLine.rootLine.nextCol = nextCol;
                        col = codeLine.rootLine.nextCol;
                        row = codeLine.rootLine.row;
                    }
                    else if (codeLine.block == eBlock.ENDIF)
                    {
                        col = codeLine.rootLine.col;
                        row = maxrow;
                        row = GetRow(row, col, maxcol);
                        ifLevel--;
                    }
                    else
                    {
                        row = maxrow;
                        row = GetRow(row, col);
                    }
                    codeLine.row = row;
                    codeLine.col = col;
                    if (codeLine.block == eBlock.ENDIF && ifLevel == 0)
                    {
                        IndentConditions(codeLines.IndexOf(codeLine.rootLine), codeLines.IndexOf(codeLine));
                    }
                    row++;
                    maxrow = Math.Max(maxrow, row);
                    maxcol = Math.Max(maxcol, col);
                }
            }
        }

        private void IndentConditions(int start, int end)
        {
            List<CodeLine> working = new List<CodeLine>();
            for (int i = start; i <= end; i++)
            {
                working.Add(codeLines[i]);
            }

            bool exists;
            bool indentFound = true;
            while (indentFound)
            {
                indentFound = false;
                foreach (CodeLine codeLine in working)
                {
                    if (codeLine.block == eBlock.ENDIF)
                    {
                        int rowFirst = codeLine.rootLine.row;
                        int colFirst = codeLine.rootLine.col;
                        int rowLast = codeLine.row;
                        for (int colCheck = colFirst + 1; colCheck <= maxcol; colCheck++)
                        {
                            exists = false;
                            foreach (CodeLine codeLineExist in working)
                            {
                                if (codeLineExist.row == rowFirst && codeLineExist.col == colCheck && codeLineExist.rootLine == codeLine.rootLine)
                                {
                                    exists = true;
                                    break;
                                }
                            }
                            if (!exists) continue;
                            bool canIndent = true;
                            for (int rowCheck = rowFirst; rowCheck <= rowLast; rowCheck++)
                            {
                                foreach (CodeLine codeLineExist in working)
                                {
                                    if (codeLineExist.row == rowCheck && codeLineExist.col == colCheck - 1)
                                    {
                                        canIndent = false;
                                        break;
                                    }
                                }
                                if (!canIndent) break;
                            }
                            if (canIndent)
                            {
                                indentFound = true;
                                for (int rowCheck = rowFirst; rowCheck <= rowLast; rowCheck++)
                                {
                                    foreach (CodeLine codeLineExist in working)
                                    {
                                        if (codeLineExist.row == rowCheck && codeLineExist.col == colCheck)
                                        {
                                            codeLineExist.col--;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private int GetRow(int row, int col, int maxCol = -1)
        {
            int newRow = row;
            bool found = false;
            while (!found && newRow > 0)
            {
                found = found || null != HasSymbol(newRow - 1, col);
                for (int _col = col; _col <= maxCol; _col++)
                {
                    found = found || null != HasSymbol(newRow - 1, _col);
                }
                if (!found) newRow--;
            }
            return newRow;
        }

        private CodeLine HasSymbol(int row, int col)
        {
            foreach (CodeLine codeLine in codeLines)
            {
                if (codeLine.row == row && codeLine.col == col)
                {
                    return codeLine;
                }
            }
            return null;
        }

        private string Clean(string line)
        {
            string result = "";
            bool inString = false;
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == '"') inString = !inString;
                else if (!inString && line[i] == '\'') break;
                result += line[i];
            }
            return result.Trim();
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

        private void buttonZoomIn_Click(object sender, RoutedEventArgs e)
        {
            Zoom(0.8);
        }

        private void buttonZoomOut_Click(object sender, RoutedEventArgs e)
        {
            Zoom(1.25);
        }

        private void buttonUpdate_Click(object sender, RoutedEventArgs e)
        {
            Display();
        }
    }

    enum eBlock { START, END, CALL, SUB, ENDSUB, IF, ELSE, ELSEIF, ENDIF, FOR, ENDFOR, WHILE, ENDWHILE, GOTO, LABEL, STATEMENT };

    class CodeLine
    {
        public string code;
        public eBlock block;
        public CodeLine rootLine;
        public string sub;
        public int row = -1;
        public int col = -1;
        public int nextCol;
        public bool hasElse = false;
        public Border border = null;
        public int linkDist = 0;

        public CodeLine(string code, eBlock block, CodeLine rootLine = null)
        {
            this.code = code;
            this.block = block;
            this.rootLine = rootLine;
        }
    }
}
