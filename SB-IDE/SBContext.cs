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
            menu.Items.Add(new ToolStripMenuItem("Collapse Folding", null, (s, ea) => sbDocument.FoldAll(FoldAction.Contract)));
            menu.Items.Add(new ToolStripMenuItem("Expand Folding", null, (s, ea) => sbDocument.FoldAll(FoldAction.Expand)));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(menuColors);
            menu.Items.Add(menuFonts);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Add to Debug Watch Ctrl+W", null, (s, ea) => sbDocument.AddWatch()) { Enabled = textArea.SelectedText.Length > 0 });
            menu.Items.Add(new ToolStripMenuItem("Format Program", null, (s, ea) => sbDocument.Lexer.Format()));
        }

        private void Insert(object sender, EventArgs e)
        {
            ToolStripMenuItem menuItem = (ToolStripMenuItem)sender;
            textArea.ReplaceSelection("\"" + menuItem.Text + "\"");
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
                item.ImageScaling = ToolStripItemImageScaling.None;

                menuItem.DropDownItems.Add(item);
            }

            return menuItem;
        }

        private ToolStripMenuItem SetFonts()
        {
            ToolStripMenuItem menuItem = new ToolStripMenuItem("Insert Font");

            foreach (FontFamily font in FontFamily.Families)
            {
                //if (font.Name == "Cambria Math") continue;
                //if (font.Name == "Gabriola") continue;
                //if (font.Name == "Jokerman") continue;

                int size = 24;
                Bitmap bmp = new Bitmap(4*size, 5*size/4);
                Graphics g = Graphics.FromImage(bmp);
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawString("Basic", new Font(font, size, FontStyle.Regular, GraphicsUnit.Pixel), Brushes.Black, 1, 1);

                ToolStripMenuItem item = new ToolStripMenuItem(font.Name, bmp, Insert);
                item.ImageScaling = ToolStripItemImageScaling.None;
                //item.Font = new Font(item.Font.FontFamily, 24, FontStyle.Regular, GraphicsUnit.Point);

                menuItem.DropDownItems.Add(item);
            }

            return menuItem;
        }
    }
}
