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

using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.TypeSystem;
using ScintillaNET;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml.Linq;

namespace SB_Prime
{
    public class SBStyle
    {
        public int style;
        public Regex regex;

        public SBStyle(int style, Regex regex)
        {
            this.style = style;
            this.regex = regex;
        }
    }

    public class SBLexer : IDisposable
    {
        private SBDocument sbDocument;
        private Scintilla textArea;

        private int STYLE_SPACE = 0;
        private int STYLE_COMMENT = 1;
        private int STYLE_STRING = 2;
        private int STYLE_OPERATOR = 3;
        private int STYLE_KEYWORD = 4;
        private int STYLE_OBJECT = 5;
        private int STYLE_METHOD = 6;
        private int STYLE_VARIABLE = 7;
        private int STYLE_SUBROUTINE = 8;
        private int STYLE_LABEL = 9;
        private int STYLE_LITERAL = 10;

        List<SBStyle> styles = new List<SBStyle>();
        int LastLineCount = 0;
        bool isDirty = false;
        int numFileComments = 0;
        int maxStylingCount = 1000;
        bool isFormatting = false;
        string lastObject = "";
        int AutoCMode = 0;
        string AutoCData = "";
        string spaces = "";
        Timer AutoCTimer;
        public SBObjects sbObjects = new SBObjects();
        public int toolTipPosition = 0;
        int theme = 0;
        string keywords = "Sub|EndSub|For|To|Step|EndFor|If|Then|Else|ElseIf|EndIf|While|EndWhile|Goto";

        Regex keyword1 = new Regex("^[\\W](IF|SUB|WHILE|FOR)[\\W]");
        Regex keyword2 = new Regex("^[\\W](ENDSUB|ENDFOR|ENDIF|ENDWHILE)[\\W]");
        Regex keyword3 = new Regex("^[\\W](ELSE|ELSEIF)[\\W]");

        public SBLexer(SBDocument sbDocument, Scintilla textArea)
        {
            this.sbDocument = sbDocument;
            this.textArea = textArea;

            if (SBInterop.Variant == SBInterop.eVariant.SmallVisualBasic)
            {
                keywords = "";
                for (int i = 0; i < SBObjects.keywords.Count; i++)
                {
                    if (i > 0) keywords += "|";
                    keywords += SBObjects.keywords[i].name;
                }
                if (keywords.IndexOf("Function") < 0) keywords += "|Function";
                if (keywords.IndexOf("EndFunction") < 0) keywords += "|EndFunction";
                if (keywords.IndexOf("Return") < 0) keywords += "|Return";
                keyword1 = new Regex("^[\\W](IF|SUB|WHILE|FOR|FUNCTION)[\\W]");
                keyword2 = new Regex("^[\\W](ENDSUB|ENDFOR|NEXT|ENDIF|ENDWHILE|WEND|ENDFUNCTION)[\\W]");
                keyword3 = new Regex("^[\\W](ELSE|ELSEIF)[\\W]");
            }

            if (false)
            {
                // Configuring the default style with properties
                // we have common to every lexer style saves time.
                textArea.StyleResetDefault();
                textArea.Styles[Style.Default].Font = "Consolas";
                textArea.Styles[Style.Default].Size = 40;
                textArea.StyleClearAll();

                // Configure the CPP (C#) lexer styles
                textArea.Styles[Style.Cpp.Default].ForeColor = Color.Silver;
                textArea.Styles[Style.Cpp.Comment].ForeColor = Color.FromArgb(0, 128, 0); // Green
                textArea.Styles[Style.Cpp.CommentLine].ForeColor = Color.FromArgb(0, 128, 0); // Green
                textArea.Styles[Style.Cpp.CommentLineDoc].ForeColor = Color.FromArgb(128, 128, 128); // Gray
                textArea.Styles[Style.Cpp.Number].ForeColor = Color.Olive;
                textArea.Styles[Style.Cpp.Word].ForeColor = Color.Blue;
                textArea.Styles[Style.Cpp.Word2].ForeColor = Color.Blue;
                textArea.Styles[Style.Cpp.String].ForeColor = Color.FromArgb(163, 21, 21); // Red
                textArea.Styles[Style.Cpp.Character].ForeColor = Color.FromArgb(163, 21, 21); // Red
                textArea.Styles[Style.Cpp.Verbatim].ForeColor = Color.FromArgb(163, 21, 21); // Red
                textArea.Styles[Style.Cpp.StringEol].BackColor = Color.Pink;
                textArea.Styles[Style.Cpp.Operator].ForeColor = Color.Purple;
                textArea.Styles[Style.Cpp.Preprocessor].ForeColor = Color.Maroon;
                textArea.Lexer = Lexer.Cpp;

                // Set the keywords
                textArea.SetKeywords(0, "abstract as base break case catch checked continue default delegate do else event explicit extern false finally fixed for foreach goto if implicit in interface internal is lock namespace new null object operator out override params private protected public readonly ref return sealed sizeof stackalloc switch this throw true try typeof unchecked unsafe using virtual while");
                textArea.SetKeywords(1, "bool byte char class const decimal double enum float int long sbyte short static string struct uint ulong ushort void");
            }

            // STYLING
            InitSyntaxColoring();
            InitAutoComplete();

            // EVENTS
            textArea.InsertCheck += (this.OnInsertCheck);
            textArea.Insert += (this.OnInsert);
            textArea.Delete += (this.OnDelete);
            textArea.CharAdded += (this.OnCharAdded);
            textArea.StyleNeeded += OnStyleNeeded;
            textArea.TextChanged += OnTextChanged;
            textArea.MouseDwellTime = 100;
            textArea.DwellStart += OnDwellStart;
            textArea.DwellEnd += OnDwellEnd;
            textArea.AutoCSelection += OnAutoCSelection;
            textArea.AutoCCompleted += OnAutoCCompleted;
            textArea.UpdateUI += OnUpdateUI;

            AutoCTimer = new Timer();
            AutoCTimer.Enabled = false;
            AutoCTimer.Interval = 100;
            AutoCTimer.Tick += new EventHandler(AutoCTimerCallback);
        }

        private void OnUpdateUI(object sender, UpdateUIEventArgs e)
        {
            if ((e.Change & UpdateChange.Selection) > 0)
            {
                if (MainWindow.highlightAll) sbDocument.searchManager.HighLight(textArea.SelectedText);
            }
        }

        public bool IsDirty
        {
            get { return isDirty; }
            set { isDirty = value; }
        }

        public int NumFileComments
        {
            get { return numFileComments; }
            set { numFileComments = value; }
        }

        public int Theme
        {
            set { theme = value; InitSyntaxColoring(); }
        }

        public void Format()
        {
            isFormatting = true;

            System.Windows.Input.Cursor cursor = System.Windows.Input.Mouse.OverrideCursor;
            System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

            for (int i = 0; i < textArea.Lines.Count; i++)
            {
                string text = textArea.Lines[i].Text.TrimEnd();
                if (i < textArea.Lines.Count - 1) text += "\r\n";
                if (text != textArea.Lines[i].Text)
                {
                    textArea.SetTargetRange(textArea.Lines[i].Position, textArea.Lines[i].EndPosition);
                    textArea.ReplaceTarget(text);
                }
            }

            for (int i = textArea.Lines.Count - 1; i > 0; i--)
            {
                if (string.IsNullOrWhiteSpace(textArea.Lines[i].Text) && string.IsNullOrWhiteSpace(textArea.Lines[i - 1].Text))
                {
                    textArea.SetTargetRange(textArea.Lines[i - 1].Position, textArea.Lines[i - 1].EndPosition);
                    textArea.ReplaceTarget("");
                }
            }

            int foldBase = textArea.Lines[0].FoldLevel;
            int fold = foldBase;

            for (int lineCur = 0; lineCur < textArea.Lines.Count; lineCur++)
            {
                textArea.Lines[lineCur].FoldLevel = fold;
                string text = textArea.Lines[lineCur].Text.Trim().ToUpperInvariant();
                if (keyword1.Match((' ' + text + ' ').ToUpperInvariant()).Value.Length > 0)
                {
                    fold++;
                    textArea.Lines[lineCur].FoldLevelFlags = FoldLevelFlags.Header;
                }
                else if (keyword2.Match((' ' + text + ' ').ToUpperInvariant()).Value.Length > 0)
                {
                    fold--;
                    textArea.Lines[lineCur].FoldLevel--;
                    textArea.Lines[lineCur].FoldLevelFlags = FoldLevelFlags.White;
                    if (fold < foldBase) fold = foldBase;
                }
                else if (keyword3.Match((' ' + text + ' ').ToUpperInvariant()).Value.Length > 0)
                {
                    textArea.Lines[lineCur].FoldLevel--;
                    textArea.Lines[lineCur].FoldLevelFlags = FoldLevelFlags.White;
                    if (fold < foldBase) fold = foldBase;
                }

                int foldCur = textArea.Lines[lineCur].FoldLevel - foldBase;

                string indents = "";
                for (int i = 0; i < foldCur; i++) indents += spaces;

                int iStart = textArea.Lines[lineCur].Position;
                string lineText = textArea.Lines[lineCur].Text;
                int iLen = 0;
                while (iLen < lineText.Length && char.IsWhiteSpace(lineText[iLen]) && lineText[iLen] != '\r' && lineText[iLen] != '\n') iLen++;
                textArea.SetTargetRange(iStart, iStart + iLen);
                textArea.ReplaceTarget(indents);
            }

            foreach (string keyword in keywords.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int pos = 0;
                Match match = Regex.Match(' ' + textArea.Text.Substring(pos).ToUpperInvariant() + ' ', "[\\W](" + keyword.ToUpperInvariant() + ")[\\W]");
                while (match.Success)
                {
                    int start = Math.Max(0, pos + match.Index);
                    int len = match.Length - 2;
                    int style = sbDocument.TextArea.GetStyleAt(start);

                    if (style == STYLE_KEYWORD)
                    {
                        sbDocument.TextArea.SetTargetRange(start, start + len);
                        sbDocument.TextArea.ReplaceTarget(keyword);
                    }

                    pos += match.Index + len;
                    if (pos >= textArea.Text.Length) break;
                    match = Regex.Match(' ' + textArea.Text.Substring(pos).ToUpperInvariant() + ' ', "[\\W](" + keyword.ToUpperInvariant() + ")[\\W]");
                }
            }

            ResetVariables();
            isFormatting = false;
            isDirty = true;
            System.Windows.Input.Mouse.OverrideCursor = cursor;
        }

        private void ResetVariables()
        {
            sbObjects.variables.Clear();
            textArea.StartStyling(0);
        }

        private void InitSyntaxColoring()
        {
            Color foreColor = IntToColor(MainWindow.FORE_COLOR);
            Color backColor = IntToColor(MainWindow.BACK_COLOR);
            if (theme == 1)
            {
                foreColor = IntToColor(MainWindow.BACK_COLOR);
                backColor = IntToColor(MainWindow.FORE_COLOR);
            }

            // Configure the default style
            maxStylingCount = MainWindow.maxStylingCount;
            textArea.StyleResetDefault();
            //textArea.Styles[Style.CallTip].Font = "Consolas";
            //textArea.Styles[Style.CallTip].Size = 20;
            textArea.Styles[Style.Default].Font = MainWindow.lexerFont;
            textArea.Styles[Style.Default].Size = 10;
            textArea.Styles[Style.Default].BackColor = backColor;
            textArea.Styles[Style.Default].ForeColor = foreColor;
            textArea.CaretForeColor = foreColor;
            textArea.TabWidth = MainWindow.indentSpaces;
            spaces = "";
            for (int i = 0; i < textArea.TabWidth; i++) spaces += " ";
            textArea.StyleClearAll();

            textArea.Styles[Style.LineNumber].ForeColor = IntToColor(MainWindow.FORE_MARGIN_COLOR);
            textArea.Styles[Style.LineNumber].BackColor = IntToColor(MainWindow.BACK_MARGIN_COLOR);
            textArea.Styles[Style.IndentGuide].ForeColor = IntToColor(MainWindow.FORE_FOLDING_COLOR);
            textArea.Styles[Style.IndentGuide].BackColor = IntToColor(MainWindow.BACK_FOLDING_COLOR);

            textArea.Styles[STYLE_SPACE].ForeColor = foreColor;
            textArea.Styles[STYLE_COMMENT].ForeColor = IntToColor(MainWindow.COMMENT_COLOR);
            textArea.Styles[STYLE_STRING].ForeColor = IntToColor(MainWindow.STRING_COLOR);
            textArea.Styles[STYLE_OPERATOR].ForeColor = IntToColor(MainWindow.OPERATOR_COLOR);
            textArea.Styles[STYLE_KEYWORD].ForeColor = IntToColor(MainWindow.KEYWORD_COLOR);
            textArea.Styles[STYLE_OBJECT].ForeColor = IntToColor(MainWindow.OBJECT_COLOR);
            textArea.Styles[STYLE_METHOD].ForeColor = IntToColor(MainWindow.METHOD_COLOR);
            textArea.Styles[STYLE_SUBROUTINE].ForeColor = foreColor;
            textArea.Styles[STYLE_LABEL].ForeColor = foreColor;
            textArea.Styles[STYLE_VARIABLE].ForeColor = foreColor;
            textArea.Styles[STYLE_LITERAL].ForeColor = IntToColor(MainWindow.LITERAL_COLOR);

            textArea.Styles[STYLE_COMMENT].Italic = true;
            textArea.Styles[STYLE_KEYWORD].Bold = true;

            styles.Clear();
            string word = "[" + MainWindow.exRegex + "A-Za-z_][" + MainWindow.exRegex + "A-Za-z_0-9]*";
            styles.Add(new SBStyle(STYLE_COMMENT, new Regex("^[\'].*")));
            styles.Add(new SBStyle(STYLE_STRING, new Regex("^[\"][^\"\\n]*[\"\\n]")));
            styles.Add(new SBStyle(STYLE_OPERATOR, new Regex("^[\\+|\\-|*|/|<|>|=]|^( AND | OR )")));
            styles.Add(new SBStyle(STYLE_SPACE, new Regex("^[\\s]")));
            styles.Add(new SBStyle(STYLE_KEYWORD, new Regex("^[\\W]("+keywords.ToUpperInvariant()+")[\\W]")));
            styles.Add(new SBStyle(STYLE_OBJECT, new Regex("^" + word + "[\\.]" + word)));
            styles.Add(new SBStyle(STYLE_SUBROUTINE, new Regex("^" + word + "[ ]*[(]")));
            styles.Add(new SBStyle(STYLE_LABEL, new Regex("^" + word + "[ ]*[:]")));
            styles.Add(new SBStyle(STYLE_VARIABLE, new Regex("^" + word + "[\\W]")));
            styles.Add(new SBStyle(STYLE_LITERAL, new Regex("^[-?\\d*\\.?\\d*]")));

            textArea.LexerName = "";
            //textArea.Lexer = Lexer.Container;

            const int SCI_CALLTIPSETBACK = 2205;
            const int SCI_CALLTIPSETFORE = 2206;
            textArea.DirectMessage(SCI_CALLTIPSETBACK, new IntPtr(ColorTranslator.ToWin32(SBDocument.IntToColor(MainWindow.BACK_CALLTIP_COLOR))), IntPtr.Zero);
            textArea.DirectMessage(SCI_CALLTIPSETFORE, new IntPtr(ColorTranslator.ToWin32(SBDocument.IntToColor(MainWindow.FORE_CALLTIP_COLOR))), IntPtr.Zero);
            textArea.CallTipSetForeHlt(SBDocument.IntToColor(MainWindow.HIGHLIGHT_CALLTIP_COLOR));
        }

        private void InitAutoComplete()
        {
            textArea.AutoCIgnoreCase = true;
            textArea.AutoCMaxHeight = 10;
            textArea.AutoCMaxWidth = 0;
            textArea.AutoCOrder = Order.Presorted;
            //textArea.AutoCSetFillUps("(");
            //textArea.AutoCStops("\t");
            textArea.AutoCAutoHide = !MainWindow.keywordContains;
            textArea.AutoCCancelAtStart = true;
            textArea.AutoCChooseSingle = false;
            textArea.AutoCDropRestOfWord = false;
            textArea.RegisterRgbaImage(0, new Bitmap(Properties.Resources.IntellisenseKeyword, new Size(24, 24)));
            textArea.RegisterRgbaImage(1, new Bitmap(Properties.Resources.IntellisenseObject, new Size(24, 24)));
            textArea.RegisterRgbaImage(2, new Bitmap(Properties.Resources.IntellisenseMethod, new Size(24, 24)));
            textArea.RegisterRgbaImage(3, new Bitmap(Properties.Resources.IntellisenseProperty, new Size(24, 24)));
            textArea.RegisterRgbaImage(4, new Bitmap(Properties.Resources.IntellisenseEvent, new Size(24, 24)));
            textArea.RegisterRgbaImage(5, new Bitmap(Properties.Resources.IntellisenseVariable, new Size(24, 24)));
            textArea.RegisterRgbaImage(6, new Bitmap(Properties.Resources.IntellisenseSubroutine, new Size(24, 24)));
            textArea.RegisterRgbaImage(7, new Bitmap(Properties.Resources.IntellisenseLabel, new Size(24, 24)));
        }

        private void OnInsertCheck(object sender, InsertCheckEventArgs e)
        {
        }

        private void OnInsert(object sender, ModificationEventArgs e)
        {
        }

        private void OnDelete(object sender, ModificationEventArgs e)
        {
        }

        private void OnCharAdded(object sender, CharAddedEventArgs e)
        {
            // Auto Indent
            if (e.Char == '\n')
            {
                int foldBase = textArea.Lines[0].FoldLevel;
                int lineCur = textArea.CurrentLine;
                int foldCur = textArea.Lines[lineCur].FoldLevel - foldBase;
                int foldPrev = textArea.Lines[lineCur - 1].FoldLevel - foldBase;

                string indents = "";
                for (int i = 0; i < foldCur; i++) indents += spaces;
                textArea.AddText(indents);

                indents = "";
                for (int i = 0; i < foldPrev; i++) indents += spaces;
                int iStart = textArea.Lines[lineCur - 1].Position;
                string linePrev = textArea.Lines[lineCur - 1].Text;
                int iLen = 0;
                while (iLen < linePrev.Length && char.IsWhiteSpace(linePrev[iLen]) && linePrev[iLen] != '\r' && linePrev[iLen] != '\n') iLen++;
                textArea.SetTargetRange(iStart, iStart + iLen);
                textArea.ReplaceTarget(indents);
            }

            // Display the autocompletion list
            int currentPos = textArea.CurrentPosition;
            int wordStartPos = textArea.WordStartPosition(currentPos, true);
            int style = textArea.GetStyleAt(currentPos-2);
            string currentWord = textArea.GetWordFromPosition(wordStartPos);
            int lenEntered = currentPos - wordStartPos;
            textArea.AutoCSetFillUps("");
            textArea.AutoCStops("");
            textArea.AutoCAutoHide = !MainWindow.keywordContains;

            if (style == STYLE_COMMENT || style == STYLE_STRING) return;

            if (wordStartPos > 1 && textArea.GetCharAt(wordStartPos - 1) == '.') //method
            {
                textArea.AutoCSetFillUps("(");
                textArea.AutoCStops(" ");
                currentPos = wordStartPos - 2;
                wordStartPos = textArea.WordStartPosition(currentPos, true);
                lastObject = textArea.GetWordFromPosition(wordStartPos);
                AutoCData = sbObjects.GetMembers(lastObject, currentWord).Trim();
                textArea.AutoCShow(lenEntered, AutoCData);
                textArea.AutoCSelect(currentWord);
                AutoCMode = 2;
                AutoCTimer.Enabled = true;
            }
            else if (lenEntered > 0)
            {
                textArea.AutoCSetFillUps(".");
                textArea.AutoCStops(" ");
                AutoCData = (sbObjects.GetKeywords(currentWord) + sbObjects.GetObjects(currentWord) + sbObjects.GetSubroutines(currentWord) + sbObjects.GetLabels(currentWord) + sbObjects.GetVariables(currentWord)).Trim();
                textArea.AutoCShow(lenEntered, AutoCData);
                textArea.AutoCSelect(currentWord);
                lastObject = "";
                AutoCMode = 1;
                AutoCTimer.Enabled = true;
            }
        }

        private void OnStyleNeeded(object sender, StyleNeededEventArgs e)
        {
            int startPos = textArea.GetEndStyled();
            int endPos = e.Position;
            SetStyle(startPos, endPos);
        }

        //This is a big performance hit for large programs
        private void SetStyle(int startPos, int endPos)
        {
            //Limit to maxStylingCount characters for performance reasons
            endPos = Math.Min(endPos, startPos + maxStylingCount);

            int line = textArea.LineFromPosition(startPos);
            startPos = textArea.Lines[line].Position;
            endPos = Math.Max(endPos, startPos + textArea.Lines[line].Length);

            string text = textArea.GetTextRange(startPos, endPos - startPos + 1);
            string value;
            int length;

            while (text != "")
            {
                line = textArea.LineFromPosition(startPos);
                length = 0;
                foreach (SBStyle style in styles)
                {
                    if (style.style == STYLE_KEYWORD)
                    {
                        value = style.regex.Match((' ' + text + ' ').ToUpperInvariant()).Value;
                        length = value.Length - 2;
                    }
                    else if (style.style == STYLE_OPERATOR)
                    {
                        value = style.regex.Match(text.ToUpperInvariant()).Value;
                        length = value.Length;
                    }
                    else
                    {
                        value = style.regex.Match(text).Value;
                        length = value.Length;
                    }
                    if (length > 0)
                    {
                        if (style.style == STYLE_OBJECT)
                        {
                            length = value.IndexOf('.');
                            textArea.StartStyling(startPos);
                            textArea.SetStyling(length, STYLE_OBJECT);
                            startPos += length;
                            text = text.Substring(length);

                            value = value.Substring(length);
                            length = value.Length;

                            textArea.StartStyling(startPos);
                            textArea.SetStyling(length, STYLE_METHOD);
                            startPos += length;
                            text = text.Substring(length);
                        }
                        else
                        {
                            if (style.style == STYLE_VARIABLE)
                            {
                                length--;
                                string variable = text.Substring(0, length).Trim();
                                if (!sbObjects.variables.Contains(variable, StringComparer.OrdinalIgnoreCase) &&
                                    !sbObjects.subroutines.Contains(variable, StringComparer.OrdinalIgnoreCase) &&
                                    !sbObjects.labels.Contains(variable, StringComparer.OrdinalIgnoreCase))
                                {
                                    if (textArea.CurrentLine != line || (line == 0 && textArea.Lines.Count() > 0)) //First line on file open
                                    {
                                        sbObjects.variables.Add(variable);
                                    }
                                    else if (MainWindow.parseLineVariables)
                                    {
                                        string regex = style.regex.ToString().Replace("[\\W]", "[\\s]*[=]");
                                        if (Regex.Match(text, regex).Success)
                                        {
                                            sbObjects.variables.Add(variable);
                                        }
                                    }
                                }
                            }
                            else if (style.style == STYLE_SUBROUTINE)
                            {
                                length--;
                                string subroutine = text.Substring(0, length).Trim();
                                sbObjects.variables.RemoveAll(n => n.Equals(subroutine, StringComparison.OrdinalIgnoreCase));
                                if (!sbObjects.subroutines.Contains(subroutine, StringComparer.OrdinalIgnoreCase))
                                {
                                    sbObjects.subroutines.Add(subroutine);
                                }
                            }
                            else if (style.style == STYLE_LABEL)
                            {
                                length--;
                                string label = text.Substring(0, length).Trim();
                                sbObjects.variables.RemoveAll(n => n.Equals(label, StringComparison.OrdinalIgnoreCase));
                                if (!sbObjects.labels.Contains(label, StringComparer.OrdinalIgnoreCase))
                                {
                                    sbObjects.labels.Add(label);
                                }
                            }

                            textArea.StartStyling(startPos);
                            textArea.SetStyling(length, style.style);
                            startPos += length;
                            text = text.Substring(length);
                        }
                        break;
                    }
                }

                if (length == 0)
                {
                    startPos++;
                    text = text.Substring(1);
                }
            }
        }

        private void OnTextChanged(object sender, EventArgs e)
        {
            if (isFormatting) return;

            if (textArea.Lines.Count != LastLineCount)
            {
                int position = textArea.CurrentPosition;
                int foldBase = textArea.Lines[0].FoldLevel;
                int fold = foldBase;
                for (int i = 0; i < textArea.Lines.Count; i++)
                {
                    textArea.Lines[i].FoldLevel = fold;
                    string text = textArea.Lines[i].Text.Trim().ToUpperInvariant();
                    if (keyword1.Match(('\n' + text + '\n').ToUpperInvariant()).Value.Length > 0)
                    {
                        fold++;
                        textArea.Lines[i].FoldLevelFlags = FoldLevelFlags.Header;
                    }
                    else if (keyword2.Match(('\n' + text + '\n').ToUpperInvariant()).Value.Length > 0)
                    {
                        fold--;
                        textArea.Lines[i].FoldLevel--;
                        textArea.Lines[i].FoldLevelFlags = FoldLevelFlags.White;
                        if (fold < foldBase) fold = foldBase;
                    }
                    else if (keyword3.Match(('\n' + text + '\n').ToUpperInvariant()).Value.Length > 0)
                    {
                        textArea.Lines[i].FoldLevel--;
                        textArea.Lines[i].FoldLevelFlags = FoldLevelFlags.White;
                        if (fold < foldBase) fold = foldBase;
                    }
                }
                textArea.CurrentPosition = position;
                LastLineCount = textArea.Lines.Count;
            }
            isDirty = true;
            textArea.Margins[SBDocument.NUMBER_MARGIN].Width = MainWindow.showNumberMargin ? Math.Max(50, 10 * (int)Math.Log10(textArea.Lines.Count)) : 0;
        }

        private void OnDwellStart(object sender, DwellEventArgs e)
        {
            int currentPos = e.Position;
            if (currentPos < 0) return;
            int wordStartPos = textArea.WordStartPosition(currentPos, true);
            string currentWord = textArea.GetWordFromPosition(wordStartPos);
            string lastWord = "";
            bool isObject = textArea.GetCharAt(wordStartPos + currentWord.Length) == '.';
            if (textArea.GetCharAt(wordStartPos - 1) == '.')
            {
                lastWord = textArea.GetWordFromPosition(textArea.WordStartPosition(wordStartPos - 2, true));
            }
            if (currentPos > wordStartPos)
            {
                lastObject = textArea.GetWordFromPosition(wordStartPos);
            }
            if (currentWord != "" && sbObjects.GetVariables(currentWord) != "")
            {
                if (null != sbDocument.debug && sbDocument.debug.IsPaused())
                {
                    toolTipPosition = currentPos;
                    sbDocument.debug.GetHover(currentWord);
                }
            }
            if (isObject && currentWord != "" && sbObjects.GetObjects(currentWord) != "")
            {
                showObjectData(currentWord, e.Position);
            }
            else if (lastWord != "" && currentWord != "" && sbObjects.GetMembers(lastWord, currentWord) != "")
            {
                showMethodData(lastWord, currentWord, e.Position);
            }
            else if (currentWord != "" && sbObjects.GetKeywords(currentWord) != "")
            {
                showObjectData(currentWord, e.Position);
            }
        }

        private void showObjectData(string currentWord, int Position = -1)
        {
            foreach (Member member in SBObjects.keywords)
            {
                if (member.name.ToUpperInvariant() == currentWord.ToUpperInvariant())
                {
                    MainWindow.showObject = null;
                    MainWindow.showMember = member;
                    if (Position >= 0 && !MainWindow.THIS.canvasInfo.IsVisible) sbDocument.TextArea.CallTipShow(Position, CallTipFormat(member.summary));
                    //sbDocument.TextArea.CallTipSetHlt(0, member.summary.Length);
                    break;
                }
            }
            foreach (SBObject obj in SBObjects.objects)
            {
                string objName = "";
                if (!FileFilter.EnableAliases || !FileFilter.Aliases.TryGetValue(obj.name, out objName))
                {
                    objName = obj.name;
                }
                if (objName.ToUpperInvariant() == currentWord.ToUpperInvariant())
                {
                    MainWindow.showObject = obj;
                    MainWindow.showMember = null;
                    if (Position >= 0 && !MainWindow.THIS.canvasInfo.IsVisible) sbDocument.TextArea.CallTipShow(Position, CallTipFormat(obj.summary));
                    //sbDocument.TextArea.CallTipSetHlt(0, obj.summary.Length);
                    break;
                }
            }
        }

        private void showMethodData(string lastWord, string currentWord, int Position = -1)
        {
            foreach (SBObject obj in SBObjects.objects)
            {
                string objName = "";
                if (!FileFilter.EnableAliases || !FileFilter.Aliases.TryGetValue(obj.name, out objName))
                {
                    objName = obj.name;
                }
                if (objName.ToUpperInvariant() == lastWord.ToUpperInvariant())
                {
                    foreach (Member member in obj.members)
                    {
                        string memberName = "";
                        if (!FileFilter.EnableAliases || !FileFilter.Aliases.TryGetValue(member.name, out memberName))
                        {
                            memberName = member.name;
                        }
                        if (memberName.ToUpperInvariant() == currentWord.ToUpperInvariant())
                        {
                            MainWindow.showObject = null;
                            MainWindow.showMember = member;
                            if (Position >= 0 && !MainWindow.THIS.canvasInfo.IsVisible)
                            {
                                string name = "";
                                switch (member.type)
                                {
                                    case MemberTypes.Custom:
                                        name = memberName;
                                        break;
                                    case MemberTypes.Method:
                                        name = memberName;
                                        if (member.arguments.Count > 0)
                                        {
                                            name += "(";
                                            for (int i = 0; i < member.arguments.Count; i++)
                                            {
                                                name += member.arguments.Keys.ElementAt(i);
                                                if (i < member.arguments.Count - 1) name += ',';
                                            }
                                            name += ")";
                                        }
                                        else
                                        {
                                            name += "()";
                                        }
                                        break;
                                    case MemberTypes.Property:
                                        name = memberName;
                                        break;
                                    case MemberTypes.Event:
                                        name = memberName;
                                        break;
                                }
                                sbDocument.TextArea.CallTipShow(Position, name + "\n" + CallTipFormat(member.summary));
                                sbDocument.TextArea.CallTipSetHlt(0, name.Length);
                            }
                            break;
                        }
                    }
                    break;
                }
            }
        }

        private string CallTipFormat(string text)
        {
            if (text.Contains('\n')) return text;
            string result = "";
            string[] words = text.Split(new char[] { ' ' }, StringSplitOptions.None);
            string line = "";
            foreach (string word in words)
            {
                line += word + ' ';
                if (line.Length > 50)
                {
                    result += line + '\n';
                    line = "";
                }
            }
            result += line;
            return result.Trim();
        }

        private void OnDwellEnd(object sender, DwellEventArgs e)
        {
            textArea.CallTipCancel();
        }

        private void AutoCTimerCallback(object sender, EventArgs e)
        {
            try
            {
                if (AutoCMode == 0) return;
                int index = textArea.AutoCCurrent;
                if (index < 0) return;
                string value = AutoCData.Split(' ')[index];
                value = value.Substring(0, value.IndexOf('?'));
                if (AutoCMode == 1) showObjectData(value);
                else if (AutoCMode == 2) showMethodData(lastObject, value);
            }
            catch
            {

            }
        }

        private void OnAutoCSelection(object sender, AutoCSelectionEventArgs e)
        {
            AutoCTimer.Enabled = false;
            AutoCMode = 0;
        }

        private void OnAutoCCompleted(object sender, AutoCSelectionEventArgs e)
        {
            AutoCTimer.Enabled = false;
            AutoCMode = 0;
        }

        private static Color IntToColor(int rgb)
        {
            return Color.FromArgb(255, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                AutoCTimer.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
