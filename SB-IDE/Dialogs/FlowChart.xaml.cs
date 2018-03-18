using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
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
        private Rectangle highlight;
        private CodeLine lastHighlight = null;
        private Color background;
        private Color foreground;
        private int highLightSize = 16;

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

            background = MainWindow.IntToColor(MainWindow.CHART_BACK_COLOR);
            foreground = MainWindow.IntToColor(MainWindow.CHART_FORE_COLOR);
            if (MainWindow.theme == 1)
            {
                background = MainWindow.IntToColor(MainWindow.CHART_FORE_COLOR);
                foreground = MainWindow.IntToColor(MainWindow.CHART_BACK_COLOR);
            }

            canvas.RenderTransform = new TransformGroup();
            scaleTransform.CenterX = 0;
            scaleTransform.CenterY = 0;
            canvas.RenderTransform = new TransformGroup();
            canvas.RenderTransform = new TransformGroup();
            ((TransformGroup)canvas.RenderTransform).Children.Add(scaleTransform);
            grid.Background = new SolidColorBrush(background);

            highlight = new Rectangle()
            {
                Width = highLightSize + width,
                Height = highLightSize + height,
                Fill = new SolidColorBrush(Colors.Transparent),
                Stroke = new SolidColorBrush(MainWindow.IntToColor(MainWindow.CHART_HIGHLIGHT_COLOR)),
                StrokeThickness = 4,
                RadiusX = 5,
                RadiusY = 5,
            };
            highlight.Visibility = Visibility.Hidden;

            Display();
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
                //scaleView = 1;
                //scaleTransform.ScaleX = 1.0;
                //scaleTransform.ScaleY = 1.0;
                lastHighlight = null;
                highlight.Visibility = Visibility.Hidden;

                canvas.Children.Add(highlight);
                Canvas.SetZIndex(highlight, -1);

                Parse();

                maxrow = 0;
                maxcol = 0;
                foreach (CodeLine codeLine in codeLines)
                {
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
                            Stroke = new SolidColorBrush(foreground),
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
                                Stroke = new SolidColorBrush(foreground),
                                StrokeThickness = 2,
                            };
                            canvas.Children.Add(connect2);
                            testCol--;
                        }

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
                            Foreground = new SolidColorBrush(foreground),
                            Text = "True",
                        };
                        condition.Measure(new Size(double.MaxValue, double.MaxValue));
                        canvas.Children.Add(condition);
                        Canvas.SetLeft(condition, borderSpace + (width + widthSpace) * col + width / 2 + 2);
                        Canvas.SetTop(condition, borderSpace + heightSpace * row + height + 2);

                        condition = new TextBlock()
                        {
                            Foreground = new SolidColorBrush(foreground),
                            Text = "False",
                        };
                        condition.Measure(new Size(double.MaxValue, double.MaxValue));
                        canvas.Children.Add(condition);
                        Canvas.SetLeft(condition, borderSpace + (width + widthSpace) * col + width + 2);
                        Canvas.SetTop(condition, borderSpace + heightSpace * row + height / 2 - condition.DesiredSize.Height - 2);
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

                    Color color;
                    switch (codeLine.block)
                    {
                        case eBlock.IF:
                        case eBlock.ELSE:
                        case eBlock.ELSEIF:
                            color = MainWindow.IntToColor(MainWindow.CHART_CONDITION_COLOR);
                            break;
                        case eBlock.START:
                        case eBlock.SUB:
                            color = MainWindow.IntToColor(MainWindow.CHART_START_COLOR);
                            break;
                        case eBlock.GOTO:
                        case eBlock.LABEL:
                        case eBlock.CALL:
                            color = MainWindow.IntToColor(MainWindow.CHART_CALL_COLOR);
                            break;
                        case eBlock.FOR:
                        case eBlock.ENDFOR:
                            color = MainWindow.IntToColor(MainWindow.CHART_FOR_COLOR);
                            break;
                        case eBlock.WHILE:
                        case eBlock.ENDWHILE:
                            color = MainWindow.IntToColor(MainWindow.CHART_WHILE_COLOR);
                            break;
                        default:
                            color = MainWindow.IntToColor(MainWindow.CHART_STATEMENT_COLOR);
                            break;
                    }
                    //Brush fill = new SolidColorBrush(color);
                    double shade = 0.4;
                    Color light = new Color() { A = 255, R = (byte)((1 - shade) * color.R + shade * 255), G = (byte)((1 - shade) * color.G + shade * 255), B = (byte)((1 - shade) * color.B + shade * 255) };
                    Color dark = new Color() { A = 255, R = (byte)((1 - shade) * color.R), G = (byte)((1 - shade) * color.G), B = (byte)((1 - shade) * color.B) };
                    Brush fill = new LinearGradientBrush(
                        new GradientStopCollection() {
                                    new GradientStop(light, 0),
                                    new GradientStop(dark, 1),
                        }, 90);

                    CodeShape border = new CodeShape()
                    {
                        Width = width,
                        Height = height,
                    };
                    border.Init(codeLine, fill);
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
                OnError();
            }
            Mouse.OverrideCursor = cursor;
        }

        public void HighlightLine(int line)
        {
            if (sbDocument != mainWindow.GetActiveDocument()) return;
            foreach (CodeLine codeLine in codeLines)
            {
                if (codeLine.lines.Contains(line))
                {
                    Highlight(codeLine);
                    break;
                }
            }
        }

        private void Highlight(CodeLine codeLine)
        {
            lastHighlight = codeLine;
            highlight.Visibility = Visibility.Visible;
            Canvas.SetLeft(highlight, borderSpace + (width + widthSpace) * codeLine.col + - highLightSize / 2);
            Canvas.SetTop(highlight, borderSpace + heightSpace * codeLine.row - highLightSize / 2);

            CodeShape borderRoot = codeLine.border;
            Point start = new Point(scrollViewer.HorizontalOffset, scrollViewer.VerticalOffset);
            Point end = borderRoot.TranslatePoint(new Point(0, 0), canvas);
            end.X = (end.X - widthSpace) * scaleView;
            end.Y = (end.Y - heightSpace) * scaleView;
            scrollStep = 0;
            timer = new Timer(_timer, new Point[] { start, end }, 0, 10);
        }

        private void codeClick(object sender, MouseButtonEventArgs e)
        {
            if (null == sender) return;
            CodeShape border = (CodeShape)sender;
            CodeLine codeLine = (CodeLine)border.Tag;
            if (null != codeLine.rootLine && e.RightButton == MouseButtonState.Pressed)
            {
                codeLine = codeLine.rootLine;
            }
            Highlight(codeLine);
        }

        private void _timer(object state)
        {
            Point[] data = (Point[])state;
            Dispatcher.Invoke(() =>
            {
                scrollStep += 0.1;
                scrollStep = Math.Min(1, scrollStep);
                scrollViewer.ScrollToHorizontalOffset((1 - scrollStep) * data[0].X + scrollStep * data[1].X);
                scrollViewer.ScrollToVerticalOffset((1 - scrollStep) * data[0].Y + scrollStep * data[1].Y);
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
                    Stroke = new SolidColorBrush(foreground),
                    StrokeThickness = 2,
                };
                canvas.Children.Add(connect);

                connect = new Line()
                {
                    X1 = borderSpace + (width + widthSpace) * col2 + width / 2,
                    X2 = borderSpace + (width + widthSpace) * col2 + width / 2,
                    Y1 = borderSpace + heightSpace * row1 + height / 2,
                    Y2 = borderSpace + heightSpace * row1,
                    Stroke = new SolidColorBrush(foreground),
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
                    Stroke = new SolidColorBrush(foreground),
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
                Stroke = new SolidColorBrush(foreground),
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
                Stroke = new SolidColorBrush(foreground),
                StrokeThickness = 2,
            };
            canvas.Children.Add(connect);

            connect = new Line()
            {
                X1 = borderSpace + (width + widthSpace) * col - space,
                X2 = borderSpace + (width + widthSpace) * col - space,
                Y1 = borderSpace + heightSpace * row1 + height / 2,
                Y2 = borderSpace + heightSpace * row2 + height / 2,
                Stroke = new SolidColorBrush(foreground),
                StrokeThickness = 2,
            };
            canvas.Children.Add(connect);

            connect = new Line()
            {
                X1 = borderSpace + (width + widthSpace) * col - space,
                X2 = borderSpace + (width + widthSpace) * col,
                Y1 = borderSpace + heightSpace * row2 + height / 2,
                Y2 = borderSpace + heightSpace * row2 + height / 2,
                Stroke = new SolidColorBrush(foreground),
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
            Stack<CodeLine> sSub = new Stack<CodeLine>();
            Stack<CodeLine> sIf = new Stack<CodeLine>();
            Stack<CodeLine> sFor = new Stack<CodeLine>();
            Stack<CodeLine> sWhile = new Stack<CodeLine>();
            Dictionary<string, CodeLine> labels = new Dictionary<string, CodeLine>();

            codeLines.Add(new CodeLine(-1, "START", eBlock.START));
            for (int i = 0; i < sbDocument.TextArea.Lines.Count; i++)
            {
                string line = Clean(sbDocument.TextArea.Lines[i].Text);
                if (line.Length == 0) continue;
                string lineLower = line.ToLower();

                if (Regex.Match(lineLower, "^(sub)[\\W]").Success)
                {
                    codeLines.Add(new CodeLine(i, line, eBlock.SUB));
                    sSub.Push(codeLines.Last());
                }
                else if (lineLower == "endsub")
                {
                    codeLines.Add(new CodeLine(i, line, eBlock.ENDSUB, sSub.Pop()));
                    codeLines.Last().rootLine.rootLine = codeLines.Last();
                }
                else if (Regex.Match(lineLower, "^(if)[\\W]").Success)
                {
                    codeLines.Add(new CodeLine(i, line, eBlock.IF));
                    sIf.Push(codeLines.Last());
                }
                else if (lineLower == "else")
                {
                    codeLines.Add(new CodeLine(i, line, eBlock.ELSE, sIf.Peek()));
                    sIf.Peek().hasElse = true;
                }
                else if (Regex.Match(lineLower, "^(elseif)[\\W]").Success)
                {
                    codeLines.Add(new CodeLine(i, line, eBlock.ELSEIF, sIf.Peek()));
                    sIf.Peek().hasElse = true;
                }
                else if (lineLower == "endif")
                {
                    if (!sIf.Peek().hasElse)
                    {
                        codeLines.Add(new CodeLine(-1, "Else", eBlock.ELSE, sIf.Peek()));
                    }
                    codeLines.Add(new CodeLine(i, line, eBlock.ENDIF, sIf.Pop()));
                    codeLines.Last().rootLine.rootLine = codeLines.Last();
                }
                else if (Regex.Match(lineLower, "^(for)[\\W]").Success)
                {
                    codeLines.Add(new CodeLine(i, line, eBlock.FOR));
                    sFor.Push(codeLines.Last());
                }
                else if (lineLower == "endfor")
                {
                    codeLines.Add(new CodeLine(i, line, eBlock.ENDFOR, sFor.Pop()));
                    codeLines.Last().rootLine.rootLine = codeLines.Last();
                }
                else if (Regex.Match(lineLower, "^(while)[\\W]").Success)
                {
                    codeLines.Add(new CodeLine(i, line, eBlock.WHILE));
                    sWhile.Push(codeLines.Last());
                }
                else if (lineLower == "endwhile")
                {
                    codeLines.Add(new CodeLine(i, line, eBlock.ENDWHILE, sWhile.Pop()));
                    codeLines.Last().rootLine.rootLine = codeLines.Last();
                }
                else if (Regex.Match(lineLower, "^(goto)[\\W]").Success)
                {
                    codeLines.Add(new CodeLine(i, line, eBlock.GOTO));
                }
                else if (Regex.Match(lineLower, "^[a-z_][\\w]*[ ]*[:]").Success)
                {
                    codeLines.Add(new CodeLine(i, line, eBlock.LABEL));
                    labels[lineLower.Substring(0, lineLower.Length - 1).Trim()] = codeLines.Last();
                }
                else if (Regex.Match(lineLower, "^[a-z_][\\w]*[ ]*[()]").Success)
                {
                    codeLines.Add(new CodeLine(i, line, eBlock.CALL));
                }
                else
                {
                    if (groupStatements && codeLines.Last().block == eBlock.STATEMENT)
                    {
                        codeLines.Last().code += "\n" + line;
                        codeLines.Last().lines.Add(i);
                    }
                    else
                    {
                        codeLines.Add(new CodeLine(i, line, eBlock.STATEMENT));
                    }
                }
            }
            codeLines.Add(new CodeLine(-1, "END", eBlock.END, codeLines[0]));
            codeLines[0].rootLine = codeLines.Last();

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
            if (scrollViewer.ComputedHorizontalScrollBarVisibility == Visibility.Collapsed && scrollViewer.ComputedVerticalScrollBarVisibility == Visibility.Collapsed) return;
            Zoom(0.8);
        }

        private void buttonZoomOut_Click(object sender, RoutedEventArgs e)
        {
            Zoom(1.25);
        }

        private void Zoom(double scale)
        {
            try
            {
                scaleView = Math.Min(1.0, scaleTransform.ScaleX * scale);
                scale = scaleView / scaleTransform.ScaleX;

                Point start = new Point(scrollViewer.HorizontalOffset, scrollViewer.VerticalOffset);
                Point end = new Point(scrollViewer.HorizontalOffset * scale, scrollViewer.VerticalOffset * scale);
                Point scaleX = new Point(scaleTransform.ScaleX, scaleTransform.ScaleX * scale);
                Point scaleY = new Point(scaleTransform.ScaleY, scaleTransform.ScaleY * scale);
                Point scaleW = new Point(canvas.Width, canvas.Width * scale);
                Point scaleH = new Point(canvas.Height, canvas.Height * scale);
                scrollStep = 0;
                timer = new Timer(_timer2, new Point[] { start, end, scaleX, scaleY, scaleW, scaleH }, 0, 10);
            }
            catch (Exception ex)
            {
                MainWindow.Errors.Add(new Error("Flow Chart : " + ex.Message));
                OnError();
            }
        }

        private void _timer2(object state)
        {
            Point[] data = (Point[])state;
            Dispatcher.Invoke(() =>
            {
                scrollStep += 0.1;
                scrollStep = Math.Min(1, scrollStep);
                scrollViewer.ScrollToHorizontalOffset((1 - scrollStep) * data[0].X + scrollStep * data[1].X);
                scrollViewer.ScrollToVerticalOffset((1 - scrollStep) * data[0].Y + scrollStep * data[1].Y);
                scaleTransform.ScaleX = (1 - scrollStep) * data[2].X + scrollStep * data[2].Y;
                scaleTransform.ScaleY = (1 - scrollStep) * data[3].X + scrollStep * data[3].Y;
                canvas.Width = (1 - scrollStep) * data[4].X + scrollStep * data[4].Y;
                canvas.Height = (1 - scrollStep) * data[5].X + scrollStep * data[5].Y;
                if (scrollStep >= 1) timer.Dispose();
            });
        }

        private void OnError()
        {
            MessageBox.Show("Flow Chart failed, probably because of code error - check program compiles.", "SB-IDE", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void buttonUpdate_Click(object sender, RoutedEventArgs e)
        {
            Display();
        }

        private void buttonStart_Click(object sender, RoutedEventArgs e)
        {
            if (codeLines.Count > 0) Highlight(codeLines[0]);
        }

        private void buttonEnd_Click(object sender, RoutedEventArgs e)
        {
            if (codeLines.Count > 0) Highlight(codeLines[codeLines.Count - 1]);
        }

        private void buttonSyncFrom_Click(object sender, RoutedEventArgs e)
        {
            if (sbDocument != mainWindow.GetActiveDocument()) return;
            int iLIne;
            if (sbDocument.TextArea.CurrentLine >= sbDocument.TextArea.FirstVisibleLine && sbDocument.TextArea.CurrentLine < sbDocument.TextArea.FirstVisibleLine + sbDocument.TextArea.LinesOnScreen)
            {
                iLIne = sbDocument.TextArea.CurrentLine;
                foreach (CodeLine codeLine in codeLines)
                {
                    if (codeLine.lines.Contains(iLIne))
                    {
                        Highlight(codeLine);
                        return;
                    }
                }
            }
            iLIne = sbDocument.TextArea.FirstVisibleLine;
            foreach (CodeLine codeLine in codeLines)
            {
                if (codeLine.lines[0] >= iLIne)
                {
                    Highlight(codeLine);
                    return;
                }
            }
        }

        private void buttonSyncTo_Click(object sender, RoutedEventArgs e)
        {
            if (sbDocument != mainWindow.GetActiveDocument()) return;
            int iLine = -1;
            if (null != lastHighlight && lastHighlight.lines.Count > 0) iLine = lastHighlight.lines[0];
            if (iLine < 0 && null != lastHighlight && null != lastHighlight.rootLine && lastHighlight.rootLine.lines.Count > 0) iLine = lastHighlight.rootLine.lines[0];
            if (iLine >= 0 && iLine < sbDocument.TextArea.Lines.Count)
            {
                ScintillaNET.Line line = sbDocument.TextArea.Lines[iLine];
                sbDocument.TextArea.ClearSelections();
                sbDocument.TextArea.SelectionStart = line.Position;
                sbDocument.TextArea.SelectionEnd = line.EndPosition;
                sbDocument.TextArea.FirstVisibleLine = iLine;
            }
        }

        private void buttonInfo_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Left click an item to select it\n\n" +
                "Right click an item to select its parent (EndFor, EndWhile, EndIf, Else or ElseIf) or select its link (Goto or Sub)\n\n" +
                "A selected item will be moved near the top of the view\n\n" +
                "During a debug session, the current paused line will be selected\n\n" +
                "Colors can be set in IDE Colors, and the Dark Theme is applied if selected",
                "SB-IDE", MessageBoxButton.OK, MessageBoxImage.Information);
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
        public CodeShape border = null;
        public int linkDist = 0;
        public List<int> lines = new List<int>();

        public CodeLine(int line, string code, eBlock block, CodeLine rootLine = null)
        {
            lines.Add(line);
            this.code = code;
            this.block = block;
            this.rootLine = rootLine;
        }
    }

    class CodeShape : Grid
    {
        public void Init(CodeLine codeLine, Brush fill)
        {
            Tag = codeLine;
            ToolTip = codeLine.code;

            Color stroke = MainWindow.IntToColor(MainWindow.theme == 0 ? MainWindow.CHART_FORE_COLOR : MainWindow.CHART_BACK_COLOR);
            switch (codeLine.block)
            {
                case eBlock.IF:
                case eBlock.ELSE:
                case eBlock.ELSEIF:
                    Polygon polygon = new Polygon();
                    polygon.Points.Add(new Point(0, Height / 2));
                    polygon.Points.Add(new Point(Width / 2, 0));
                    polygon.Points.Add(new Point(Width, Height / 2));
                    polygon.Points.Add(new Point(Width / 2, Height));
                    polygon.Fill = fill;
                    polygon.Stroke = new SolidColorBrush(stroke);
                    polygon.StrokeThickness = 1;
                    Children.Add(polygon);
                    break;
                case eBlock.START:
                case eBlock.SUB:
                case eBlock.LABEL:
                    Ellipse ellipse = new Ellipse();
                    ellipse.Width = Width;
                    ellipse.Height = Height;
                    ellipse.Fill = fill;
                    ellipse.Stroke = new SolidColorBrush(stroke);
                    ellipse.StrokeThickness = 1;
                    Children.Add(ellipse);
                    break;
                default:
                    Rectangle rectangle = new Rectangle();
                    rectangle.Width = Width;
                    rectangle.Height = Height;
                    rectangle.Fill = fill;
                    rectangle.RadiusX = 5;
                    rectangle.RadiusY = 5;
                    rectangle.Stroke = new SolidColorBrush(stroke);
                    rectangle.StrokeThickness = 1;
                    Children.Add(rectangle);
                    break;
            }
            Effect = new DropShadowEffect
            {
                Color = stroke,
                Direction = -45,
                ShadowDepth = 4,
                BlurRadius = 5,
                Opacity = 0.5,
            };

            TextBlock textBlock = new TextBlock()
            {
                Foreground = new SolidColorBrush(MainWindow.IntToColor(MainWindow.CHART_CODE_COLOR)),
                Text = codeLine.code,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                //FontFamily = new FontFamily("Consolas"),
                Margin = new Thickness(2),
            };
            Children.Add(textBlock);
        }
    }
}
