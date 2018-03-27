//The following Copyright applies to SB-IDE for Small Basic and files in the namespace SB_IDE. 
//Copyright (C) <2017> litdev@hotmail.co.uk 
//This file is part of SB-IDE for Small Basic. 

//SB-IDE for Small Basic is free software: you can redistribute it and/or modify 
//it under the terms of the GNU General Public License as published by 
//the Free Software Foundation, either version 3 of the License, or 
//(at your option) any later version. 

//SB-IDE for Small Basic is distributed in the hope that it will be useful, 
//but WITHOUT ANY WARRANTY; without even the implied warranty of 
//MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the 
//GNU General Public License for more details.  

//You should have received a copy of the GNU General Public License 
//along with SB-IDE for Small Basic.  If not, see <http://www.gnu.org/licenses/>. 

using ScintillaNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Drawing2D;
using SB_IDE.Dialogs;
using System.Reflection;
using System.IO;
using System.Diagnostics;

namespace SB_IDE
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
            textArea.ContextMenuStrip = menu;

            menu.Items.Add(new ToolStripMenuItem("Undo Ctrl+Z", null, (s, ea) => textArea.Undo()) { Enabled = textArea.CanUndo });
            menu.Items.Add(new ToolStripMenuItem("Redo Ctrl+Y", null, (s, ea) => textArea.Redo()) { Enabled = textArea.CanRedo });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Cut Ctrl+X", null, (s, ea) => textArea.Cut()) { Enabled = textArea.SelectedText.Length > 0 });
            menu.Items.Add(new ToolStripMenuItem("Copy Ctrl+C", null, (s, ea) => textArea.Copy()) { Enabled = textArea.SelectedText.Length > 0 });
            menu.Items.Add(new ToolStripMenuItem("Paste Ctrl+V", null, (s, ea) => textArea.Paste()) { Enabled = textArea.CanPaste });
            menu.Items.Add(new ToolStripMenuItem("Delete", null, (s, ea) => textArea.DeleteRange(textArea.SelectionStart, textArea.SelectionEnd - textArea.SelectionStart)) { Enabled = textArea.SelectedText.Length > 0 });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Select All Ctrl+A", null, (s, ea) => textArea.SelectAll()));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Find Ctrl+F", null, OpenFindDialog) { Enabled = null != sbDocument.Tab });
            menu.Items.Add(new ToolStripMenuItem("Find and Replace Ctrl+H", null, OpenReplaceDialog) { Enabled = null != sbDocument.Tab });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Comment Selected Lines", null, (s, ea) => sbDocument.Comment(true)));
            menu.Items.Add(new ToolStripMenuItem("Un-Comment Selected Lines", null, (s, ea) => sbDocument.Comment(false)));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Collapse Folding", null, (s, ea) => sbDocument.FoldAll(FoldAction.Contract)));
            menu.Items.Add(new ToolStripMenuItem("Expand Folding", null, (s, ea) => sbDocument.FoldAll(FoldAction.Expand)));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(menuColors);
            menu.Items.Add(menuFonts);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Copy Selection to Clipboard as HTML text", null, CopyToHtml) { Enabled = textArea.SelectedText.Length > 0 });
            menu.Items.Add(new ToolStripMenuItem("Copy Selection to Clipboard as HTML", null, (s, ea) => textArea.CopyAllowLine(CopyFormat.Html)) { Enabled = textArea.SelectedText.Length > 0 });
            menu.Items.Add(new ToolStripMenuItem("Copy Selection to Clipboard as RTF", null, (s, ea) => textArea.CopyAllowLine(CopyFormat.Rtf)) { Enabled = textArea.SelectedText.Length > 0 });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Open Containing Folder", null, OpenContainingFolder) { Enabled = null != sbDocument.Tab && File.Exists(((TabHeader)sbDocument.Tab.Header).FilePath) });
            menu.Items.Add(new ToolStripMenuItem("Add to Debug Watch Ctrl+W", null, (s, ea) => sbDocument.AddWatch()) { Enabled = textArea.SelectedText.Length > 0 });
            menu.Items.Add(new ToolStripMenuItem("Display Flow Chart", null, OpenFlowChart) { Enabled = null != sbDocument.Tab });
            menu.Items.Add(new ToolStripMenuItem("Format Program", null, (s, ea) => sbDocument.Lexer.Format()));
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
                string path = Path.GetDirectoryName(((TabHeader)sbDocument.Tab.Header).FilePath);
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
            ToolStripMenuItem menuItem = new ToolStripMenuItem("Insert Color");

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
            ToolStripMenuItem menuItem = new ToolStripMenuItem("Insert Font");

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
