using ScintillaNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Drawing2D;

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
            menu.Items.Add(new ToolStripMenuItem("Format Program", null, (s, ea) => sbDocument.Lexer.Format()));
            menu.Items.Add(new ToolStripMenuItem("Add to Debug Watch Ctrl+W", null, (s, ea) => sbDocument.AddWatch()) { Enabled = textArea.SelectedText.Length > 0 });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(menuColors);
            menu.Items.Add(menuFonts);
        }

        private void Insert(object sender, EventArgs e)
        {
            ToolStripMenuItem menuItem = (ToolStripMenuItem)sender;
            textArea.InsertText(textArea.CurrentPosition, "\"" + menuItem.Text + "\"");
            textArea.CurrentPosition += 2 + menuItem.Text.Length;
            textArea.ClearSelections();
        }

        private ToolStripMenuItem SetColors()
        {
            Array colorsArray = Enum.GetValues(typeof(KnownColor));
            KnownColor[] allColors = new KnownColor[colorsArray.Length];
            Array.Copy(colorsArray, allColors, colorsArray.Length);

            ToolStripMenuItem menuItem = new ToolStripMenuItem("Insert Color");

            for (int i = 0; i < allColors.Length; i++)
            {
                if (allColors[i] < KnownColor.Transparent || allColors[i] >= KnownColor.ButtonFace) continue;
                String name = allColors[i].ToString();
                Bitmap bmp = new Bitmap(50, 50);
                Graphics g = Graphics.FromImage(bmp);
                g.Clear(Color.FromName(allColors[i].ToString()));
                menuItem.DropDownItems.Add(new ToolStripMenuItem(name, bmp, Insert));
            }

            return menuItem;
        }

        private ToolStripMenuItem SetFonts()
        {
            ToolStripMenuItem menuItem = new ToolStripMenuItem("Insert Font");

            int i = 0;
            foreach (FontFamily font in FontFamily.Families)
            {
                //if (font.Name == "Cambria Math") continue;
                //if (font.Name == "Gabriola") continue;
                //if (font.Name == "Jokerman") continue;

                int size = 24;
                Bitmap bmp = new Bitmap(4*size, 3*size/2);
                Graphics g = Graphics.FromImage(bmp);
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawString("Basic", new Font(font, size, FontStyle.Regular, GraphicsUnit.Pixel), Brushes.Black, 1, 1);

                ToolStripMenuItem item = new ToolStripMenuItem(font.Name, bmp, Insert);
                item.ImageScaling = ToolStripItemImageScaling.None;
                //item.Font = new Font(item.Font.FontFamily, 24, FontStyle.Regular, GraphicsUnit.Point);
                menuItem.DropDownItems.Add(item);
                i++;
            }

            return menuItem;
        }
    }
}
