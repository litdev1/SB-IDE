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
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace SB_IDE
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

    public class SBLexer
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

        public int Theme
        {
            set { theme = value; InitSyntaxColoring(); }
        }

        public void Format()
        {
            System.Windows.Input.Cursor cursor = System.Windows.Input.Mouse.OverrideCursor;
            System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

            int foldBase = textArea.Lines[0].FoldLevel;
            int fold = foldBase;

            for (int lineCur = 0; lineCur < textArea.Lines.Count; lineCur++)
            {
                textArea.Lines[lineCur].FoldLevel = fold;
                string text = textArea.Lines[lineCur].Text.Trim().ToUpper();
                if (keyword1.Match((' ' + text + ' ').ToUpper()).Value.Length > 0)
                {
                    fold++;
                    textArea.Lines[lineCur].FoldLevelFlags = FoldLevelFlags.Header;
                }
                else if (keyword2.Match((' ' + text + ' ').ToUpper()).Value.Length > 0)
                {
                    fold--;
                    textArea.Lines[lineCur].FoldLevel--;
                    textArea.Lines[lineCur].FoldLevelFlags = FoldLevelFlags.White;
                    if (fold < foldBase) fold = foldBase;
                }
                else if (keyword3.Match((' ' + text + ' ').ToUpper()).Value.Length > 0)
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
                Match match = Regex.Match(' ' + textArea.Text.Substring(pos).ToUpper() + ' ', "[\\W](" + keyword.ToUpper() + ")[\\W]");
                while (match.Success)
                {
                    int start = Math.Max(0, pos + match.Index);
                    int len = match.Length - 2;

                    sbDocument.TextArea.SetTargetRange(start, start+len);
                    sbDocument.TextArea.ReplaceTarget(keyword);

                    pos += match.Index + len;
                    if (pos >= textArea.Text.Length) break;
                    match = Regex.Match(' ' + textArea.Text.Substring(pos).ToUpper() + ' ', "[\\W](" + keyword.ToUpper() + ")[\\W]");
                }
            }

            isDirty = true;
            System.Windows.Input.Mouse.OverrideCursor = cursor;
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
            textArea.StyleResetDefault();
            textArea.Styles[Style.Default].Font = "Consolas";
            textArea.Styles[Style.Default].Size = 10;
            textArea.Styles[Style.Default].BackColor = backColor;
            textArea.Styles[Style.Default].ForeColor = foreColor;
            textArea.CaretForeColor = foreColor;
            textArea.TabWidth = 2;
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

            styles.Add(new SBStyle(STYLE_SPACE, new Regex("^[\\s+]")));
            styles.Add(new SBStyle(STYLE_COMMENT, new Regex("^[\'].*")));
            styles.Add(new SBStyle(STYLE_STRING, new Regex("^[\"][^\"\\n?]*[\"\\n]")));
            styles.Add(new SBStyle(STYLE_OPERATOR, new Regex("^[\\+|-|*|/|<|>|=]|^(AND|OR)")));
            styles.Add(new SBStyle(STYLE_KEYWORD, new Regex("^[\\W]("+keywords.ToUpper()+")[\\W]")));
            styles.Add(new SBStyle(STYLE_OBJECT, new Regex("^[A-Za-z_][\\w]*[\\.][A-Za-z_][\\w]*")));
            styles.Add(new SBStyle(STYLE_SUBROUTINE, new Regex("^[A-Za-z_][\\w]*[(]")));
            styles.Add(new SBStyle(STYLE_LABEL, new Regex("^[A-Za-z_][\\w]*[ ]*[:]")));
            styles.Add(new SBStyle(STYLE_VARIABLE, new Regex("^[A-Za-z_][\\w]*[\\W]")));
            styles.Add(new SBStyle(STYLE_LITERAL, new Regex("^[-?\\d*\\.?\\d*]")));

            // Configure the lexer styles
            textArea.Lexer = Lexer.Container;
        }

        private void InitAutoComplete()
        {
            textArea.AutoCIgnoreCase = true;
            textArea.AutoCMaxHeight = 10;
            textArea.AutoCMaxWidth = 0;
            textArea.AutoCOrder = Order.Presorted;
            //textArea.AutoCSetFillUps("(");
            //textArea.AutoCStops("\t");
            textArea.AutoCAutoHide = true;
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
            string currentWord = textArea.GetWordFromPosition(wordStartPos);
            int lenEntered = currentPos - wordStartPos;
            textArea.AutoCSetFillUps("");
            textArea.AutoCStops("");

            if (wordStartPos > 1 && textArea.GetCharAt(wordStartPos - 1) == '.') //method
            {
                textArea.AutoCSetFillUps("(");
                textArea.AutoCStops(" ");
                currentPos = wordStartPos - 2;
                wordStartPos = textArea.WordStartPosition(currentPos, true);
                lastObject = textArea.GetWordFromPosition(wordStartPos);
                AutoCData = sbObjects.GetMembers(lastObject, currentWord).Trim();
                textArea.AutoCShow(lenEntered, AutoCData);
                AutoCMode = 2;
                AutoCTimer.Enabled = true;
            }
            else if (lenEntered > 0)
            {
                textArea.AutoCSetFillUps(".");
                textArea.AutoCStops(" ");
                AutoCData = (sbObjects.GetKeywords(currentWord) + sbObjects.GetObjects(currentWord) + sbObjects.GetSubroutines(currentWord) + sbObjects.GetLabels(currentWord) + sbObjects.GetVariables(currentWord)).Trim();
                textArea.AutoCShow(lenEntered, AutoCData);
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

        private void SetStyle(int startPos, int endPos)
        {
            int line = textArea.LineFromPosition(startPos);
            startPos = textArea.Lines[line].Position;

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
                        value = style.regex.Match((' ' + text + ' ').ToUpper()).Value;
                        length = value.Length - 2;
                    }
                    else if (style.style == STYLE_OPERATOR)
                    {
                        value = style.regex.Match(text.ToUpper()).Value;
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
                                    !sbObjects.labels.Contains(variable, StringComparer.OrdinalIgnoreCase) &&
                                    textArea.CurrentLine != line)
                                {
                                    sbObjects.variables.Add(variable);
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
            if (textArea.Lines.Count != LastLineCount)
            {
                int position = textArea.CurrentPosition;
                int foldBase = textArea.Lines[0].FoldLevel;
                int fold = foldBase;
                for (int i = 0; i < textArea.Lines.Count; i++)
                {
                    textArea.Lines[i].FoldLevel = fold;
                    string text = textArea.Lines[i].Text.Trim().ToUpper();
                    if (keyword1.Match(('\n' + text + '\n').ToUpper()).Value.Length > 0)
                    {
                        fold++;
                        textArea.Lines[i].FoldLevelFlags = FoldLevelFlags.Header;
                    }
                    else if (keyword2.Match(('\n' + text + '\n').ToUpper()).Value.Length > 0)
                    {
                        fold--;
                        textArea.Lines[i].FoldLevel--;
                        textArea.Lines[i].FoldLevelFlags = FoldLevelFlags.White;
                        if (fold < foldBase) fold = foldBase;
                    }
                    else if (keyword2.Match(('\n' + text + '\n').ToUpper()).Value.Length > 0)
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
            textArea.Margins[SBDocument.NUMBER_MARGIN].Width = Math.Max(50, 10 * (int)Math.Log10(textArea.Lines.Count));
        }

        private void OnDwellStart(object sender, DwellEventArgs e)
        {
            int currentPos = e.Position;
            int wordStartPos = textArea.WordStartPosition(currentPos, true);
            string currentWord = textArea.GetWordFromPosition(wordStartPos);
            string lastWord = "";
            if (textArea.GetCharAt(wordStartPos-1) == '.')
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
            if (currentWord != "" && sbObjects.GetObjects(currentWord) != "")
            {
                showObjectData(currentWord);
             }
            else if (lastWord != "" && currentWord != "" && sbObjects.GetMembers(lastWord, currentWord) != "")
            {
                showMethodData(lastWord, currentWord);
            }
            else if (currentWord != "" && sbObjects.GetKeywords(currentWord) != "")
            {
                showObjectData(currentWord);
            }
        }

        private void showObjectData(string currentWord)
        {
            foreach (Member member in SBObjects.keywords)
            {
                if (member.name.ToUpper() == currentWord.ToUpper())
                {
                    MainWindow.showObject = null;
                    MainWindow.showMember = member;
                    //sbDocument.TextArea.CallTipShow(e.Position, member.summary);
                    //sbDocument.TextArea.CallTipSetHlt(0, member.summary.Length);
                    break;
                }
            }
            foreach (SBObject obj in SBObjects.objects)
            {
                if (obj.name.ToUpper() == currentWord.ToUpper())
                {
                    MainWindow.showObject = obj;
                    MainWindow.showMember = null;
                    //sbDocument.TextArea.CallTipShow(sbDocument.TextArea.CurrentPosition, obj.summary);
                    //sbDocument.TextArea.CallTipSetHlt(0, obj.summary.Length);
                    break;
                }
            }
        }

        private void showMethodData(string lastWord, string currentWord)
        {
            foreach (SBObject obj in SBObjects.objects)
            {
                if (obj.name.ToUpper() == lastWord.ToUpper())
                {
                    foreach (Member member in obj.members)
                    {
                        if (member.name.ToUpper() == currentWord.ToUpper())
                        {
                            MainWindow.showObject = null;
                            MainWindow.showMember = member;
                            //sbDocument.TextArea.CallTipShow(e.Position, member.summary);
                            //sbDocument.TextArea.CallTipSetHlt(0, member.summary.Length);
                            break;
                        }
                    }
                    break;
                }
            }
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
    }
}
