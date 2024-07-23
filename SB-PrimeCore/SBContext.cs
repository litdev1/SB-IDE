//The following Copyright applies to SB-Prime for Small Basic and files in the namespace SB_Prime. 
//Copyright (C) <2020> litdev@hotmail.co.uk 
//This file is part of SB-Prime for Small Basic. 

//SB-Prime for Small Basic is free software: you can redistribute it and/or modify 
//it under the terms of the GNU General Public License as published by 
//the Free Software Foundation, either version 3 of the License, or 
//(at your option) any later version. 

//SB-Prime for Small Basic is distributed in the hope that it will be useful, 
//but WITHOUT ANY WARRANTY; without even the implied warranty of 
//MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the 
//GNU General Public License for more details.  

//You should have received a copy of the GNU General Public License 
//along with SB-Prime for Small Basic.  If not, see <http://www.gnu.org/licenses/>. 

using ScintillaNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Drawing2D;
using SB_Prime.Dialogs;
using System.Reflection;
using System.IO;
using System.Diagnostics;
using System.ComponentModel;

namespace SB_Prime
{
    public class SBContext
    {
        private SBDocument sbDocument;
        private Scintilla textArea;
        private ToolStripMenuItem menuColors;
        private ToolStripMenuItem menuFonts;

        public SBContext(SBDocument sbDocument)
        {
            this.sbDocument = sbDocument;
            textArea = sbDocument.TextArea;

            menuColors = SetColors();
            menuFonts = SetFonts();
        }

        public void SetMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Closed += new ToolStripDropDownClosedEventHandler(OnClosed);
            textArea.ContextMenuStrip = menu;

            menu.Items.Add(new ToolStripMenuItem(Properties.Strings.String89, null, (s, ea) => textArea.Undo()) { Enabled = textArea.CanUndo });
            menu.Items.Add(new ToolStripMenuItem(Properties.Strings.String90, null, (s, ea) => textArea.Redo()) { Enabled = textArea.CanRedo });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem(Properties.Strings.String91, null, (s, ea) => textArea.Cut()) { Enabled = textArea.SelectedText.Length > 0 });
            menu.Items.Add(new ToolStripMenuItem(Properties.Strings.String92, null, (s, ea) => textArea.Copy()) { Enabled = textArea.SelectedText.Length > 0 });
            menu.Items.Add(new ToolStripMenuItem(Properties.Strings.String93, null, (s, ea) => textArea.Paste()) { Enabled = textArea.CanPaste });
            menu.Items.Add(new ToolStripMenuItem(Properties.Strings.String94, null, (s, ea) => textArea.DeleteRange(textArea.SelectionStart, textArea.SelectionEnd - textArea.SelectionStart)) { Enabled = textArea.SelectedText.Length > 0 });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem(Properties.Strings.String95, null, (s, ea) => textArea.SelectAll()));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem(Properties.Strings.String96, null, OpenFindDialog) { Enabled = null != sbDocument.Layout });
            menu.Items.Add(new ToolStripMenuItem(Properties.Strings.String97, null, OpenReplaceDialog) { Enabled = null != sbDocument.Layout });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem(Properties.Strings.String98, null, (s, ea) => sbDocument.Comment(true)));
            menu.Items.Add(new ToolStripMenuItem(Properties.Strings.String99, null, (s, ea) => sbDocument.Comment(false)));
            menu.Items.Add(new ToolStripMenuItem(Properties.Strings.String100, null, (s, ea) => sbDocument.UnCommentFile()));
            menu.Items.Add(new ToolStripMenuItem(Properties.Strings.String183, null, (s, ea) => sbDocument.CommentAt()));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem(Properties.Strings.String101, null, (s, ea) => sbDocument.FoldAll(FoldAction.Contract)));
            menu.Items.Add(new ToolStripMenuItem(Properties.Strings.String102, null, (s, ea) => sbDocument.FoldAll(FoldAction.Expand)));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem(Properties.Strings.String103, null, (s, ea) => sbDocument.GoBackwards()) { Enabled = sbDocument.lineStack.backwards.Count > 1 });
            menu.Items.Add(new ToolStripMenuItem(Properties.Strings.String104, null, (s, ea) => sbDocument.GoForwards()) { Enabled = sbDocument.lineStack.forwards.Count > 0 });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(menuColors);
            menu.Items.Add(menuFonts);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem(Properties.Strings.String105, null, CopyToHtml) { Enabled = textArea.SelectedText.Length > 0 });
            menu.Items.Add(new ToolStripMenuItem(Properties.Strings.String106, null, (s, ea) => textArea.CopyAllowLine(CopyFormat.Html)) { Enabled = textArea.SelectedText.Length > 0 });
            menu.Items.Add(new ToolStripMenuItem(Properties.Strings.String107, null, (s, ea) => textArea.CopyAllowLine(CopyFormat.Rtf)) { Enabled = textArea.SelectedText.Length > 0 });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem(Properties.Strings.String184, null, (s, ea) => FirstCompare()));
            menu.Items.Add(new ToolStripMenuItem(Properties.Strings.String185, null, (s, ea) => SecondCompare()));
            menu.Items.Add(new ToolStripMenuItem(Properties.Strings.String186, null, (s, ea) => EndCompare()));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem(Properties.Strings.String108, null, OpenContainingFolder) { Enabled = null != sbDocument.Layout && File.Exists(sbDocument.Layout.FilePath) });
            menu.Items.Add(new ToolStripMenuItem(Properties.Strings.String109, null, (s, ea) => sbDocument.AddWatch()) { Enabled = textArea.SelectedText.Length > 0 });
            menu.Items.Add(new ToolStripMenuItem(Properties.Strings.String110, null, OpenFlowChart) { Enabled = null != sbDocument.Layout });
            menu.Items.Add(new ToolStripMenuItem(Properties.Strings.String111, null, (s, ea) => sbDocument.Lexer.Format()));
        }

        private void FirstCompare()
        {
            SBDiff.doc1 = sbDocument;
        }

        private void SecondCompare()
        {
            SBDiff.doc2 = sbDocument;
        }

        private void EndCompare()
        {
            SBDiff.bEndDiff = true;
        }

        private void OnClosed(object sender, ToolStripDropDownClosedEventArgs e)
        {
            sbDocument.lineStack.bActive = false;
        }

        private void OpenFlowChart(object sender, EventArgs e)
        {
            try
            {
                if (FlowChart.Active)
                {
                    FlowChart.THIS.Display();
                    FlowChart.THIS.Activate();
                    if (FlowChart.THIS.WindowState == System.Windows.WindowState.Minimized)
                        FlowChart.THIS.WindowState = System.Windows.WindowState.Normal;
                    return;
                }

                FlowChart fc = new FlowChart(MainWindow.THIS);
                fc.Show();
            }
            catch
            {

            }
        }

        private void OpenContainingFolder(object sender, EventArgs e)
        {
            try
            {
                string path = Path.GetDirectoryName(sbDocument.Layout.FilePath);
                Process.Start("explorer.exe", "\"" + path + "\"");
            }
            catch
            {

            }
        }

        private void OpenFindDialog(object sender, EventArgs e)
        {
            if (textArea.SelectedText != "") MainWindow.THIS.tbFind.Text = textArea.SelectedText;
            MainWindow.THIS.tbFind.Focus();
            MainWindow.THIS.tbFind.SelectAll();
            MainWindow.THIS.FindNext();
        }

        private void OpenReplaceDialog(object sender, EventArgs e)
        {
            if (FindAndReplace.Active) return;

            FindAndReplace far = new FindAndReplace(MainWindow.THIS);
            far.Show();
        }

        private void CopyToHtml(object sender, EventArgs e)
        {
            string html = sbDocument.TextArea.GetTextRangeAsHtml(textArea.SelectionStart, textArea.SelectedText.Length);
            Clipboard.SetText(html);
        }

        private void Insert(object sender, EventArgs e)
        {
            ToolStripMenuItem menuItem = (ToolStripMenuItem)sender;
            string value = MainWindow.hexColors && null != menuItem.Tag ? menuItem.Tag.ToString() : menuItem.Text;
            if (MainWindow.quoteInserts)
                textArea.ReplaceSelection("\"" + value + "\"");
            else
                textArea.ReplaceSelection(value);
            textArea.SelectionStart = textArea.CurrentPosition;
            textArea.SelectionEnd = textArea.CurrentPosition;
        }

        private ToolStripMenuItem SetColors()
        {
            ToolStripMenuItem menuItem = new ToolStripMenuItem(Properties.Strings.String112);

            Type colorsType = typeof(System.Windows.Media.Colors);
            PropertyInfo[] colorsTypePropertyInfos = colorsType.GetProperties(BindingFlags.Public | BindingFlags.Static);

            Dictionary<string, float> colors = new Dictionary<string, float>();
            foreach (PropertyInfo colorsTypePropertyInfo in colorsTypePropertyInfos)
            {
                string colorName = colorsTypePropertyInfo.Name;
                colors[colorName] = Color.FromName(colorName).GetHue();
            }

            foreach (KeyValuePair<string, float> color in colors.OrderBy(kvp => kvp.Value))
            {
                Bitmap bmp = new Bitmap(24, 24);
                Graphics g = Graphics.FromImage(bmp);
                g.Clear(Color.FromName(color.Key));
                ToolStripMenuItem item = new ToolStripMenuItem(color.Key, bmp, Insert);
                item.Tag = System.Windows.Media.ColorConverter.ConvertFromString(color.Key).ToString();
                item.ImageScaling = ToolStripItemImageScaling.None;

                menuItem.DropDownItems.Add(item);
            }

            return menuItem;
        }

        private ToolStripMenuItem SetFonts()
        {
            ToolStripMenuItem menuItem = new ToolStripMenuItem(Properties.Strings.String113);

            List<string> fonts = new List<string>();
            foreach (System.Windows.Media.FontFamily font in System.Windows.Media.Fonts.SystemFontFamilies) //WPF fonts
            {
                fonts.Add(font.FamilyNames.Values.First());
            }
            fonts.Sort();

            foreach (string fontName in fonts)
            {
                Bitmap bmp = null;
                try
                {
                    int size = 20;
                    //bmp = new Bitmap(4 * size, 5 * size / 4);
                    //Graphics g = Graphics.FromImage(bmp);
                    //g.SmoothingMode = SmoothingMode.HighQuality;
                    //g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    //g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    //g.DrawString("Basic", new Font(new FontFamily(fontName), size, FontStyle.Regular, GraphicsUnit.Pixel), Brushes.Black, 1, 1);

                    System.Windows.Media.DrawingVisual dv = new System.Windows.Media.DrawingVisual();
                    using (System.Windows.Media.DrawingContext dc = dv.RenderOpen())
                    {
                        dc.DrawRectangle(System.Windows.Media.Brushes.White, null, new System.Windows.Rect(0, 0, 4 * size, 5 * size / 4));
                        dc.DrawText(new System.Windows.Media.FormattedText("Basic", System.Globalization.CultureInfo.InvariantCulture,
                            System.Windows.FlowDirection.LeftToRight, new System.Windows.Media.Typeface(fontName), size,
                            System.Windows.Media.Brushes.Black), new System.Windows.Point(1, 1));
                    }
                    System.Windows.Media.Imaging.RenderTargetBitmap rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(4 * size, 5 * size / 4, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                    rtb.Render(dv);
                    rtb.Freeze();
                    MemoryStream stream = new MemoryStream();
                    System.Windows.Media.Imaging.BitmapEncoder encoder = new System.Windows.Media.Imaging.BmpBitmapEncoder();
                    encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                    encoder.Save(stream);
                    bmp = new Bitmap(stream);
                }
                catch
                {

                }

                ToolStripMenuItem item = new ToolStripMenuItem(fontName, bmp, Insert);
                item.ImageScaling = ToolStripItemImageScaling.None;

                menuItem.DropDownItems.Add(item);
            }
            
            return menuItem;
        }
    }
}
