using ScintillaNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SB_IDE
{
    public class SBContext
    {
        private SBDocument sbDocument;
        private Scintilla textArea;

        public SBContext(SBDocument sbDocument)
        {
            this.sbDocument = sbDocument;
            textArea = sbDocument.TextArea;
        }

        public void SetMenu()
        { 
            ContextMenuStrip menu = new ContextMenuStrip();
            textArea.ContextMenuStrip = menu;

            menu.Items.Add(new ToolStripMenuItem("Undo Ctrl-Z", null, (s, ea) => textArea.Undo()) { Enabled = textArea.CanUndo });
            menu.Items.Add(new ToolStripMenuItem("Redo Ctrl-Y", null, (s, ea) => textArea.Redo()) { Enabled = textArea.CanRedo });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Cut Ctrl-X", null, (s, ea) => textArea.Cut()) { Enabled = textArea.SelectedText.Length > 0 });
            menu.Items.Add(new ToolStripMenuItem("Copy Ctrl-C", null, (s, ea) => textArea.Copy()) { Enabled = textArea.SelectedText.Length > 0 });
            menu.Items.Add(new ToolStripMenuItem("Paste Ctrl-V", null, (s, ea) => textArea.Paste()) { Enabled = textArea.CanPaste });
            menu.Items.Add(new ToolStripMenuItem("Delete", null, (s, ea) => textArea.DeleteRange(textArea.SelectionStart, textArea.SelectionEnd - textArea.SelectionStart)) { Enabled = textArea.SelectedText.Length > 0 });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Select All Ctrl-A", null, (s, ea) => textArea.SelectAll()));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Collapse Folding", null, (s, ea) => sbDocument.FoldAll(FoldAction.Contract)));
            menu.Items.Add(new ToolStripMenuItem("Expand Folding", null, (s, ea) => sbDocument.FoldAll(FoldAction.Expand)));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Format Program", null, (s, ea) => sbDocument.Lexer.Format()));
            menu.Items.Add(new ToolStripMenuItem("Add to Debug Watch Ctrl+W", null, (s, ea) => sbDocument.AddWatch()) { Enabled = textArea.SelectedText.Length > 0 });
        }
    }
}
