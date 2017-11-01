using ScintillaNET;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Controls;
using System.Windows.Forms;

namespace SB_IDE
{
    public class SBDocument
    {
        private Scintilla textArea;
        private SBLexer lexer;
        private string filepath = "";
        public TabItem Tab;
        public SBDebug debug = null;
        public Process Proc = null;
        public SearchManager searchManager = new SearchManager();

        // Text Area Colors
        private int BACK_MARGIN_COLOR = 0xF8F8F8;
        private int FORE_MARGIN_COLOR = 0x5C5C5C;
        private int BACK_BOOKMARK_COLOR = 0x5050A0;
        private int FORE_BOOKMARK_COLOR = 0x5050A0;
        private int BACK_BREAKPOINT_COLOR = 0xFF003B;
        private int FORE_BREAKPOINT_COLOR = 0xFF003B;
        private int SELECT_COLOR = 0xCCDDFF;

        public SBDocument()
        {
            // BASIC CONFIG
            textArea = new Scintilla();
            lexer = new SBLexer(this, textArea);
            textArea.Dock = DockStyle.Fill;

            // INITIAL VIEW CONFIG
            textArea.WrapMode = WrapMode.None;
            textArea.IndentationGuides = IndentView.LookBoth;
            textArea.ScrollWidth = 0;

            // STYLING
            InitColors();

            // NUMBER MARGIN
            InitNumberMargin();

            // BOOKMARK AND BREAKPOINT MARGINS
            InitBookmarkMargin();

            // CODE FOLDING MARGIN
            InitCodeFolding();

            // DRAG DROP
            InitDragDropFile();

            // INIT HOTKEYS
            InitHotkeys();

            // SEARCH
            searchManager.TextArea = textArea;
        }

        public Scintilla TextArea
        {
            get { return textArea; }
        }

        public SBLexer Lexer
        {
            get { return lexer; }
        }

        public void LoadDataFromFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    textArea.Text = File.ReadAllText(path);
                    string spaces = "";
                    for (int i = 0; i < textArea.TabWidth; i++) spaces += " ";
                    textArea.Text = textArea.Text.Replace(spaces, "\t");
                    lexer.IsDirty = false;
                    filepath = path;
                    if (Properties.Settings.Default.MRU.Contains(filepath)) Properties.Settings.Default.MRU.Remove(filepath);
                    Properties.Settings.Default.MRU.Insert(0, filepath);
                }
            }
            catch (Exception ex)
            {
                MainWindow.Errors.Add(new Error("Load File : " + ex.Message));
            }
        }

        public void LoadDataFromText(string program)
        {
            try
            {
                textArea.Text = program;
                string spaces = "";
                for (int i = 0; i < textArea.TabWidth; i++) spaces += " ";
                textArea.Text = textArea.Text.Replace(spaces, "\t");
                lexer.IsDirty = true;
            }
            catch (Exception ex)
            {
                MainWindow.Errors.Add(new Error("Load Text : " + ex.Message));
            }
        }

        public void SaveDataToFile(string path = "")
        {
            try
            {
                if (path == "") path = filepath;
                if (Directory.Exists(Path.GetDirectoryName(path)))
                {
                    File.WriteAllText(path, textArea.Text);
                    filepath = path;
                    lexer.IsDirty = false;
                }
            }
            catch (Exception ex)
            {
                MainWindow.Errors.Add(new Error("Save File : " + ex.Message));
            }
        }

        public bool IsDirty
        {
            get { return lexer.IsDirty; }
        }

        public string Filepath
        {
            get { return filepath; }
        }

        private void InitColors()
        {
            textArea.SetSelectionBackColor(true, IntToColor(SELECT_COLOR));
        }

        private void InitHotkeys()
        {
            // register the hotkeys with the document
            HotKeyManager.AddHotKey(textArea, Uppercase, Keys.U, true);
            HotKeyManager.AddHotKey(textArea, Lowercase, Keys.L, true);
            HotKeyManager.AddHotKey(textArea, ZoomIn, Keys.Oemplus, true);
            HotKeyManager.AddHotKey(textArea, ZoomOut, Keys.OemMinus, true);
            HotKeyManager.AddHotKey(textArea, ZoomDefault, Keys.D0, true);

            // remove conflicting hotkeys from scintilla
            textArea.ClearCmdKey(Keys.Control | Keys.F);
            textArea.ClearCmdKey(Keys.Control | Keys.R);
            textArea.ClearCmdKey(Keys.Control | Keys.H);
            textArea.ClearCmdKey(Keys.Control | Keys.L);
            textArea.ClearCmdKey(Keys.Control | Keys.U);
        }

        #region Numbers, Bookmarks, Code Folding

        /// <summary>
        /// change this to whatever margin you want the line numbers to show in
        /// </summary>
        public const int NUMBER_MARGIN = 2;

        /// <summary>
        /// change this to whatever margin you want the bookmarks/breakpoints to show in
        /// </summary>
        public const int BOOKMARK_MARGIN = 3;
        public const int BOOKMARK_MARKER = 3;
        public const int BREAKPOINT_MARGIN = 1;
        public const int BREAKPOINT_MARKER = 1;

        /// <summary>
        /// change this to whatever margin you want the code folding tree (+/-) to show in
        /// </summary>
        private const int FOLDING_MARGIN = 4;

        /// <summary>
        /// set this true to show circular buttons for code folding (the [+] and [-] buttons on the margin)
        /// </summary>
        private const bool CODEFOLDING_CIRCULAR = true;

        private void InitNumberMargin()
        {
            textArea.Styles[Style.LineNumber].ForeColor = IntToColor(FORE_MARGIN_COLOR);
            textArea.Styles[Style.LineNumber].BackColor = IntToColor(BACK_MARGIN_COLOR);
            textArea.Styles[Style.IndentGuide].ForeColor = IntToColor(FORE_MARGIN_COLOR);
            textArea.Styles[Style.IndentGuide].BackColor = IntToColor(BACK_MARGIN_COLOR);

            var nums = textArea.Margins[NUMBER_MARGIN];
            nums.Width = 50;
            nums.Type = MarginType.Number;
            nums.Sensitive = true;
            nums.Mask = 0;

            textArea.MarginClick += TextArea_MarginClick;
        }

        private void InitBookmarkMargin()
        {
            textArea.SetFoldMarginColor(true, IntToColor(BACK_MARGIN_COLOR));

            var margin = textArea.Margins[BOOKMARK_MARGIN];
            margin.Width = 15;
            margin.Sensitive = true;
            margin.Type = MarginType.Symbol;
            margin.Mask = (1 << BOOKMARK_MARGIN);

            var marker = textArea.Markers[BOOKMARK_MARGIN];
            marker.Symbol = MarkerSymbol.Bookmark;
            marker.SetBackColor(IntToColor(BACK_BOOKMARK_COLOR));
            marker.SetForeColor(IntToColor(FORE_BOOKMARK_COLOR));
            marker.SetAlpha(100);

            margin = textArea.Margins[BREAKPOINT_MARGIN];
            margin.Width = 20;
            margin.Sensitive = true;
            margin.Type = MarginType.Symbol;
            margin.Mask = (1 << BREAKPOINT_MARKER);

            marker = textArea.Markers[BREAKPOINT_MARKER];
            marker.Symbol = MarkerSymbol.Circle;
            marker.SetBackColor(IntToColor(BACK_BREAKPOINT_COLOR));
            marker.SetForeColor(IntToColor(FORE_BREAKPOINT_COLOR));
            marker.SetAlpha(100);
        }

        private void InitCodeFolding()
        {
            textArea.SetFoldMarginColor(true, IntToColor(BACK_MARGIN_COLOR));
            textArea.SetFoldMarginHighlightColor(true, IntToColor(BACK_MARGIN_COLOR));

            // Enable code folding
            textArea.SetProperty("fold", "1");
            textArea.SetProperty("fold.compact", "1");

            // Configure a margin to display folding symbols
            textArea.Margins[FOLDING_MARGIN].Type = MarginType.Symbol;
            textArea.Margins[FOLDING_MARGIN].Mask = Marker.MaskFolders;
            textArea.Margins[FOLDING_MARGIN].Sensitive = true;
            textArea.Margins[FOLDING_MARGIN].Width = 20;

            // Set colors for all folding markers
            for (int i = 25; i <= 31; i++)
            {
                textArea.Markers[i].SetForeColor(IntToColor(BACK_MARGIN_COLOR)); // styles for [+] and [-]
                textArea.Markers[i].SetBackColor(IntToColor(FORE_MARGIN_COLOR)); // styles for [+] and [-]
            }

            // Configure folding markers with respective symbols
            textArea.Markers[Marker.Folder].Symbol = CODEFOLDING_CIRCULAR ? MarkerSymbol.CirclePlus : MarkerSymbol.BoxPlus;
            textArea.Markers[Marker.FolderOpen].Symbol = CODEFOLDING_CIRCULAR ? MarkerSymbol.CircleMinus : MarkerSymbol.BoxMinus;
            textArea.Markers[Marker.FolderEnd].Symbol = CODEFOLDING_CIRCULAR ? MarkerSymbol.CirclePlusConnected : MarkerSymbol.BoxPlusConnected;
            textArea.Markers[Marker.FolderMidTail].Symbol = MarkerSymbol.TCorner;
            textArea.Markers[Marker.FolderOpenMid].Symbol = CODEFOLDING_CIRCULAR ? MarkerSymbol.CircleMinusConnected : MarkerSymbol.BoxMinusConnected;
            textArea.Markers[Marker.FolderSub].Symbol = MarkerSymbol.VLine;
            textArea.Markers[Marker.FolderTail].Symbol = MarkerSymbol.LCorner;

            // Enable automatic folding
            textArea.AutomaticFold = (AutomaticFold.Show | AutomaticFold.Click | AutomaticFold.Change);
        }

        private void TextArea_MarginClick(object sender, MarginClickEventArgs e)
        {
            if (e.Margin == BOOKMARK_MARGIN)
            {
                // Do we have a marker for this line?
                var line = textArea.Lines[textArea.LineFromPosition(e.Position)];
                ToggleBM(line);
            }
            else if (e.Margin == BREAKPOINT_MARGIN)
            {
                // Do we have a marker for this line?
                var line = textArea.Lines[textArea.LineFromPosition(e.Position)];
                ToggleBP(line);
            }
        }

        public void ToggleBP(Line line)
        {
            const uint mask = (1 << BREAKPOINT_MARKER);
            if ((line.MarkerGet() & mask) > 0)
            {
                // Remove existing breakpoint
                line.MarkerDelete(BREAKPOINT_MARKER);
                if (null != debug) debug.SetBreakPoint(line.Index, false);
            }
            else
            {
                // Add breakpoint
                line.MarkerAdd(BREAKPOINT_MARKER);
                if (null != debug) debug.SetBreakPoint(line.Index, true);
            }
        }

        public void ToggleBM(Line line)
        {
            const uint mask = (1 << BOOKMARK_MARKER);
            if ((line.MarkerGet() & mask) > 0)
            {
                // Remove existing bookmark
                line.MarkerDelete(BOOKMARK_MARKER);
            }
            else
            {
                // Add bookmark
                line.MarkerAdd(BOOKMARK_MARKER);
            }
        }

        #endregion

        #region Drag & Drop File

        public void InitDragDropFile()
        {
            textArea.AllowDrop = true;
            textArea.DragEnter += delegate (object sender, DragEventArgs e)
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                    e.Effect = DragDropEffects.Copy;
                else
                    e.Effect = DragDropEffects.None;
            };
            textArea.DragDrop += delegate (object sender, DragEventArgs e)
            {
                // get file drop
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    Array a = (Array)e.Data.GetData(DataFormats.FileDrop);
                    if (a != null)
                    {
                        string path = a.GetValue(0).ToString();

                        LoadDataFromFile(path);
                        Tab.Header = new TabHeader(path);
                    }
                }
            };
        }

        #endregion

        #region Uppercase / Lowercase

        public void Lowercase()
        {
            // save the selection
            int start = textArea.SelectionStart;
            int end = textArea.SelectionEnd;

            // modify the selected text
            textArea.ReplaceSelection(textArea.GetTextRange(start, end - start).ToLower());

            // preserve the original selection
            textArea.SetSelection(start, end);
        }

        public void Uppercase()
        {
            // save the selection
            int start = textArea.SelectionStart;
            int end = textArea.SelectionEnd;

            // modify the selected text
            textArea.ReplaceSelection(textArea.GetTextRange(start, end - start).ToUpper());

            // preserve the original selection
            textArea.SetSelection(start, end);
        }

        #endregion

        #region Selection Copy Cut Paste

        public void Cut()
        {
            textArea.Cut();
        }

        public void Copy()
        {
            textArea.Copy();
        }

        public void Paste()
        {
            textArea.Paste();
        }

        public void Delete()
        {
            textArea.DeleteRange(textArea.SelectionStart , textArea.SelectionEnd - textArea.SelectionStart);
        }

        public void Undo()
        {
            textArea.Undo();
        }

        public void Redo()
        {
            textArea.Redo();
        }

        public void SelectAll()
        {
            textArea.SelectAll();
        }

        public void SelectLine()
        {
            Line line = textArea.Lines[textArea.CurrentLine];
            textArea.SetSelection(line.Position, line.Position + line.Length);
            textArea.ScrollCaret();
        }

        public void SelectLine(int iLine)
        {
            Line line = textArea.Lines[iLine];
            textArea.SetSelection(line.Position, line.Position + line.Length);
            textArea.ScrollCaret();
        }

        public void SetEmptySelection()
        {
            textArea.SetEmptySelection(0);
        }

        #endregion

        #region Wrap and Fold

        public WrapMode WrapMode
        {
            set { textArea.WrapMode = value; }
        }

        public IndentView IndentationGuides
        {
            set { textArea.IndentationGuides = value; }
        }

        public WhitespaceMode ViewWhitespace
        {
            set { textArea.ViewWhitespace = value; }
        }

        public int Theme
        {
            set { lexer.Theme = value; }
        }

        public void FoldAll(FoldAction foldAction)
        {
            textArea.FoldAll(foldAction);
        }

        #endregion

        #region Indent / Outdent

        public void Indent()
        {
            // we use this hack to send "Shift+Tab" to scintilla, since there is no known API to indent,
            // although the indentation function exists. Pressing TAB with the editor focused confirms this.
            GenerateKeystrokes("{TAB}");
        }

        public void Outdent()
        {
            // we use this hack to send "Shift+Tab" to scintilla, since there is no known API to outdent,
            // although the indentation function exists. Pressing Shift+Tab with the editor focused confirms this.
            GenerateKeystrokes("+{TAB}");
        }

        private void GenerateKeystrokes(string keys)
        {
            HotKeyManager.Enable = false;
            textArea.Focus();
            SendKeys.Send(keys);
            HotKeyManager.Enable = true;
        }

        #endregion

        #region Zoom

        public void ZoomIn()
        {
            textArea.ZoomIn();
        }

        public void ZoomOut()
        {
            textArea.ZoomOut();
        }

        public void ZoomDefault()
        {
            textArea.Zoom = 0;
        }

        #endregion

        #region Navigation

        public void NavBack()
        {
            const uint mask = (1 << BOOKMARK_MARKER);
            for (int iLine = textArea.CurrentLine - 1; iLine >= 0; iLine--)
            {
                Line line = textArea.Lines[iLine];
                if ((line.MarkerGet() & mask) > 0)
                {
                    textArea.SetEmptySelection(line.Position);
                    textArea.ScrollCaret();
                    break;
                }
            }
        }

        public void NavForward()
        {
            const uint mask = (1 << BOOKMARK_MARKER);
            for (int iLine = textArea.CurrentLine + 1; iLine < textArea.Lines.Count; iLine++)
            {
                Line line = textArea.Lines[iLine];
                if ((line.MarkerGet() & mask) > 0)
                {
                    textArea.SetEmptySelection(line.Position);
                    textArea.ScrollCaret();
                    break;
                }
            }
        }

        #endregion

        #region Utils

        public static Color IntToColor(int rgb)
        {
            return Color.FromArgb(255, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
        }

        #endregion
    }
}
