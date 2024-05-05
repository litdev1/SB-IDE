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
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Controls;
using System.Windows.Forms;

namespace SB_Prime
{
    public class SBDocument : IDisposable
    {
        private Scintilla textArea;
        private SBLexer lexer;
        private SBContext sbContext;
        private string filepath = "";
        public TabItem Tab;
        public SBLayout Layout;
        public SBDebug debug = null;
        public Process Proc = null;
        public SearchManager searchManager = new SearchManager();
        public LineStack lineStack = new LineStack();

        public SBDocument()
        {
            // BASIC CONFIG
            textArea = new Scintilla();
            lexer = new SBLexer(this, textArea);
            textArea.Dock = DockStyle.Fill;
            sbContext = new SBContext(this);
            textArea.UsePopup(false);

            // INITIAL VIEW CONFIG
            textArea.WrapMode = WrapMode.None;
            textArea.IndentationGuides = IndentView.LookBoth;
            textArea.ScrollWidth = 1;
            textArea.WhitespaceSize = 2;

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
            System.Windows.Input.Cursor cursor = System.Windows.Input.Mouse.OverrideCursor;
            System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

            try
            {
                if (File.Exists(path))
                {
                    textArea.Text = FileFilter.ReadAllText(path);
                    //string spaces = "";
                    //for (int i = 0; i < textArea.TabWidth; i++) spaces += " ";
                    //textArea.Text = textArea.Text.Replace(spaces, "\t");
                    lexer.IsDirty = false;
                    filepath = path;
                    if (Properties.Settings.Default.MRU.Contains(filepath)) Properties.Settings.Default.MRU.Remove(filepath);
                    Properties.Settings.Default.MRU.Insert(0, filepath);

                    List<int> lines = new List<int>();
                    MainWindow.breakpoints.TryGetValue(path, out lines);
                    if (null != lines)
                    {
                        foreach (int iLine in lines)
                        {
                            if (iLine >= 0 && iLine < textArea.Lines.Count) textArea.Lines[iLine].MarkerAdd(BREAKPOINT_MARKER);
                        }
                    }
                    MainWindow.bookmarks.TryGetValue(path, out lines);
                    if (null != lines)
                    {
                        foreach (int iLine in lines)
                        {
                            if (iLine >= 0 && iLine < textArea.Lines.Count) textArea.Lines[iLine].MarkerAdd(BOOKMARK_MARKER);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MainWindow.Errors.Add(new Error("Load File : " + ex.Message));
            }

            System.Windows.Input.Mouse.OverrideCursor = cursor;
        }

        public void LoadDataFromText(string program)
        {
            System.Windows.Input.Cursor cursor = System.Windows.Input.Mouse.OverrideCursor;
            System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

            try
            {
                textArea.Text = program;
                //string spaces = "";
                //for (int i = 0; i < textArea.TabWidth; i++) spaces += " ";
                //textArea.Text = textArea.Text.Replace(spaces, "\t");
                lexer.IsDirty = true;
            }
            catch (Exception ex)
            {
                MainWindow.Errors.Add(new Error("Load Text : " + ex.Message));
            }

            System.Windows.Input.Mouse.OverrideCursor = cursor;
        }

        public void SaveDataToFile(string path = "")
        {
            try
            {
                if (path == "") path = filepath;
                if (Directory.Exists(Path.GetDirectoryName(path)))
                {
                    FileFilter.WriteAllText(path, textArea.Text);
                    filepath = path;
                    lexer.IsDirty = false;
                    SetMarks();
                }
            }
            catch (Exception ex)
            {
                MainWindow.Errors.Add(new Error("Save File : " + ex.Message));
            }
        }

        public void SetMarks()
        {
            try
            {
                if (Directory.Exists(Path.GetDirectoryName(filepath)))
                {
                    const uint maskBP = (1 << BREAKPOINT_MARKER);
                    const uint maskBM = (1 << BOOKMARK_MARKER);
                    List<int> BPs = new List<int>();
                    List<int> BMs = new List<int>();
                    foreach (Line line in textArea.Lines)
                    {
                        if ((line.MarkerGet() & maskBP) > 0) BPs.Add(line.Index);
                        if ((line.MarkerGet() & maskBM) > 0) BMs.Add(line.Index);
                    }
                    if (BPs.Count > 0) MainWindow.breakpoints[filepath] = BPs;
                    else if (MainWindow.breakpoints.ContainsKey(filepath)) MainWindow.breakpoints.Remove(filepath);
                    if (BMs.Count > 0) MainWindow.bookmarks[filepath] = BMs;
                    else if (MainWindow.bookmarks.ContainsKey(filepath)) MainWindow.bookmarks.Remove(filepath);
                }
            }
            catch
            {

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

        public void ClearHighlights()
        {
            foreach (Line line in textArea.Lines)
            {
                line.MarkerDelete(HIGHLIGHT_MARKER);
            }
        }

        public void HighlightLine(Line line)
        {
            Marker marker = textArea.Markers[SBDocument.HIGHLIGHT_MARKER];
            marker.Symbol = MarkerSymbol.Background;
            marker.SetBackColor(IntToColor(MainWindow.DEBUG_HIGHLIGHT_COLOR));
            line.MarkerAdd(HIGHLIGHT_MARKER);
        }

        private void InitColors()
        {
            textArea.SetSelectionBackColor(true, IntToColor(MainWindow.SELECT_COLOR));
        }

        private void InitHotkeys()
        {
            // register the hotkeys with the document
            HotKeyManager.AddHotKey(textArea, ZoomIn, Keys.Oemplus, true);
            HotKeyManager.AddHotKey(textArea, ZoomOut, Keys.OemMinus, true);
            HotKeyManager.AddHotKey(textArea, ZoomDefault, Keys.D0, true);
            HotKeyManager.AddHotKey(textArea, AddWatch, Keys.W, true, true);
            HotKeyManager.AddHotKey(textArea, SelectWord, Keys.W, true);
            HotKeyManager.AddHotKey(textArea, TopOfView, Keys.End, true, false, true);
            HotKeyManager.AddHotKey(textArea, ClearSelection, Keys.Escape);
            HotKeyManager.AddHotKey(textArea, GoBackwards, Keys.B, true);
            HotKeyManager.AddHotKey(textArea, GoForwards, Keys.B, true, true);
            HotKeyManager.AddHotKey(textArea, Comment, Keys.OemQuestion, true);
            HotKeyManager.AddHotKey(textArea, CommentAt, Keys.OemQuestion, true, true);

            // remove conflicting hotkeys from scintilla
            textArea.ClearCmdKey(Keys.Control | Keys.N);
            textArea.ClearCmdKey(Keys.Control | Keys.O);
            textArea.ClearCmdKey(Keys.Control | Keys.S);
            textArea.ClearCmdKey(Keys.Control | Keys.Shift | Keys.S);
            textArea.ClearCmdKey(Keys.Control | Keys.Shift | Keys.O);
            textArea.ClearCmdKey(Keys.Control | Keys.F);
            textArea.ClearCmdKey(Keys.Control | Keys.H);
            textArea.ClearCmdKey(Keys.Control | Keys.Shift | Keys.W);
            textArea.ClearCmdKey(Keys.Control | Keys.W);
            textArea.ClearCmdKey(Keys.Control | Keys.Alt | Keys.End);
            textArea.ClearCmdKey(Keys.Escape);
            textArea.ClearCmdKey(Keys.Control | Keys.B);
            textArea.ClearCmdKey(Keys.Control | Keys.Shift | Keys.B);
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
        public const int HIGHLIGHT_MARKER = 0;
        public const int DELETED_MARKER = 4;
        public const int INSERTED_MARKER = 5;

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
            var nums = textArea.Margins[NUMBER_MARGIN];
            nums.Width = MainWindow.showNumberMargin ? 50 : 0;
            nums.Type = MarginType.Number;
            nums.Sensitive = true;
            nums.Mask = 0;

            textArea.MarginClick += TextArea_MarginClick;
            textArea.MouseDown += TextArea_MouseDown;
        }

        private void InitBookmarkMargin()
        {
            var margin = textArea.Margins[BOOKMARK_MARGIN];
            margin.Width = 15;
            margin.Sensitive = true;
            margin.Type = MarginType.Symbol;
            margin.Mask = (1 << BOOKMARK_MARGIN);

            var marker = textArea.Markers[BOOKMARK_MARGIN];
            marker.Symbol = MarkerSymbol.Bookmark;
            marker.SetBackColor(IntToColor(MainWindow.BACK_BOOKMARK_COLOR));
            marker.SetForeColor(IntToColor(MainWindow.FORE_BOOKMARK_COLOR));
            marker.SetAlpha(100);

            margin = textArea.Margins[BREAKPOINT_MARGIN];
            margin.Width = 20;
            margin.Sensitive = true;
            margin.Type = MarginType.Symbol;
            margin.Mask = (1 << BREAKPOINT_MARKER);

            marker = textArea.Markers[BREAKPOINT_MARKER];
            marker.Symbol = MarkerSymbol.Circle;
            marker.SetBackColor(IntToColor(MainWindow.BACK_BREAKPOINT_COLOR));
            marker.SetForeColor(IntToColor(MainWindow.FORE_BREAKPOINT_COLOR));
            marker.SetAlpha(100);
        }

        private void InitCodeFolding()
        {
            textArea.SetFoldMarginColor(true, IntToColor(MainWindow.BACK_FOLDING_COLOR));
            textArea.SetFoldMarginHighlightColor(true, IntToColor(MainWindow.BACK_FOLDING_COLOR));

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
                textArea.Markers[i].SetForeColor(IntToColor(MainWindow.BACK_FOLDING_COLOR)); // styles for [+] and [-]
                textArea.Markers[i].SetBackColor(IntToColor(MainWindow.FORE_FOLDING_COLOR)); // styles for [+] and [-]
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

        private void TextArea_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                lineStack.bActive = true;
                sbContext.SetMenu();
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
                        MainWindow.THIS.SetTabHeaderStyle(Tab);
                    }
                }
            };
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
            System.Windows.Input.Cursor cursor = System.Windows.Input.Mouse.OverrideCursor;
            System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

            textArea.Paste();

            System.Windows.Input.Mouse.OverrideCursor = cursor;
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

        public void CommentAt()
        {
            string next = textArea.GetTextRange(textArea.CurrentPosition, 1);
            string previous = textArea.CurrentPosition == 0 ? "" : textArea.GetTextRange(textArea.CurrentPosition - 1, 1);
            if (next == "'")
            {
                textArea.SetTargetRange(textArea.CurrentPosition, textArea.CurrentPosition + 1);
                textArea.ReplaceTarget("");
            }
            else if (previous == "'")
            {
                textArea.SetTargetRange(textArea.CurrentPosition - 1, textArea.CurrentPosition);
                textArea.ReplaceTarget("");
            }
            else
            {
                textArea.InsertText(textArea.CurrentPosition, "'");
            }

            lexer.IsDirty = true;
        }

        public void Comment()
        {
            int lineA = textArea.LineFromPosition(textArea.SelectionStart);
            int lineB = textArea.LineFromPosition(textArea.SelectionEnd);
            if (lineB < lineA)
            {
                int iTemp = lineA;
                lineA = lineB;
                lineB = iTemp;
            }
            if (lineB > lineA && textArea.SelectionEnd == textArea.Lines[lineB - 1].EndPosition) lineB--;
            int iStart = textArea.Lines[lineA].Position;
            int iEnd = textArea.Lines[lineB].EndPosition;

            string selected = "";
            for (int i = lineA; i <= lineB; i++)
            {
                Line line = textArea.Lines[i];
                string text = line.Text;
                int pos = text.TakeWhile(c => char.IsWhiteSpace(c)).Count();
                if (pos < text.Length)
                {
                    if (text[pos] != '\'')
                    {
                        text = text.Insert(pos, "'");
                    }
                    else if (text[pos] == '\'')
                    {
                        text = text.Remove(pos, 1);
                    }
                }
                selected += text;
            }

            textArea.SetTargetRange(iStart, iEnd);
            textArea.ReplaceTarget(selected);
            textArea.CurrentPosition = textArea.Lines[lineA].Position + 1;
            lexer.IsDirty = true;
        }

        public void Comment(bool bComment)
        {
            int lineA = textArea.LineFromPosition(textArea.SelectionStart);
            int lineB = textArea.LineFromPosition(textArea.SelectionEnd);
            if (lineB < lineA)
            {
                int iTemp = lineA;
                lineA = lineB;
                lineB = iTemp;
            }
            if (lineB > lineA && textArea.SelectionEnd == textArea.Lines[lineB-1].EndPosition) lineB--;
            int iStart = textArea.Lines[lineA].Position;
            int iEnd = textArea.Lines[lineB].EndPosition;

            string selected = "";
            for (int i = lineA; i <= lineB; i++)
            {
                Line line = textArea.Lines[i];
                string text = line.Text;
                int pos = text.TakeWhile(c => char.IsWhiteSpace(c)).Count();
                if (pos < text.Length)
                {
                    if (bComment && text[pos] != '\'')
                    {
                        text = text.Insert(pos, "'");
                    }
                    else if (!bComment && text[pos] == '\'')
                    {
                        text = text.Remove(pos, 1);
                    }
                }
                selected += text;
            }

            textArea.SetTargetRange(iStart, iEnd);
            textArea.ReplaceTarget(selected);
            //textArea.SetStyle(iStart, iEnd);
            lexer.IsDirty = true;
        }

        public void UnCommentFile()
        {
            System.Windows.Input.Cursor cursor = System.Windows.Input.Mouse.OverrideCursor;
            System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

            string search = "' The following line could be harmful and has been automatically commented.";

            TextArea.TargetStart = 0;
            TextArea.TargetEnd = TextArea.TextLength;
            TextArea.SearchFlags = MainWindow.searchFlags;
            while (TextArea.SearchInTarget(search) != -1)
            {
                // Uncomment File command
                int iLine = textArea.LineFromPosition(textArea.TargetStart);
                Line line = textArea.Lines[iLine];
                int iStart = line.Position;
                int iEnd = line.EndPosition;
                textArea.SetTargetRange(iStart, iEnd);
                textArea.ReplaceTarget("");
                lexer.IsDirty = true;
                if (iLine < textArea.Lines.Count)
                {
                    line = textArea.Lines[iLine];
                    string text = line.Text;
                    if (!text.StartsWith(search)) //Not for a repeated line
                    {
                        iStart = line.Position;
                        iEnd = line.EndPosition;
                        int pos = text.TakeWhile(c => char.IsWhiteSpace(c)).Count();
                        if (pos < text.Length - 1 && text[pos] == '\'')
                        {
                            text = text.Remove(pos, 1);
                            textArea.SetTargetRange(iStart, iEnd);
                            textArea.ReplaceTarget(text);
                        }
                    }
                }

                // Search the remainder of the document
                iEnd = textArea.TargetEnd;
                textArea.TargetStart = iEnd;
                if (textArea.TargetStart != iEnd) break; //No idea why this is necessary sometimes
                textArea.TargetEnd = textArea.TextLength;
            }

            System.Windows.Input.Mouse.OverrideCursor = cursor;
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

        public void AddWatch()
        {
            MainWindow.MarkedForWatch.Enqueue(textArea.SelectedText);
        }

        public void SelectWord()
        {
            textArea.SelectionStart = textArea.WordStartPosition(textArea.CurrentPosition, true);
            textArea.SelectionEnd = textArea.WordEndPosition(textArea.CurrentPosition, true);
        }

        public void TopOfView()
        {
            textArea.FirstVisibleLine = textArea.CurrentLine;
        }

        public void ClearSelection()
        {
            textArea.ClearSelections();
        }

        public void GoBackwards()
        {
            int iLine = lineStack.GetBackwards();
            if (iLine < 0 || iLine >= textArea.Lines.Count) return;
            textArea.GotoPosition(textArea.Lines[iLine].Position);
            SelectLine();
        }

        public void GoForwards()
        {
            int iLine = lineStack.GetForwards();
            if (iLine < 0 || iLine >= textArea.Lines.Count) return;
            textArea.GotoPosition(textArea.Lines[iLine].Position);
            SelectLine();
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

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                textArea.Dispose();
                lexer.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }

    public class LineStack
    {
        public int MaxItems { get; set; }

        public List<int> backwards = new List<int>();
        public List<int> forwards = new List<int>();
        public bool bActive = false;

        public LineStack()
        {
            MaxItems = 100;
        }

        public void PushBackwards(int iLine)
        {
            if (bActive) return;
            if (backwards.Count > 0 && iLine == backwards[backwards.Count - 1]) return;
            backwards.Add(iLine);
            while (backwards.Count > MaxItems) backwards.RemoveAt(0);
        }

        public void PushForwards(int iLine)
        {
            if (forwards.Count > 0 && iLine == forwards[0]) return;
            forwards.Insert(0, iLine);
            while (forwards.Count > MaxItems) forwards.RemoveAt(forwards.Count - 1);
        }

        public int GetBackwards()
        {
            if (backwards.Count < 2) return -1;
            PushForwards(backwards[backwards.Count - 1]);
            backwards.RemoveAt(backwards.Count - 1);
            return backwards[backwards.Count - 1];
        }

        public int GetForwards()
        {
            if (forwards.Count == 0) return -1;
            int iLine = forwards[0];
            forwards.RemoveAt(0);
            return iLine;
        }
    }
}
