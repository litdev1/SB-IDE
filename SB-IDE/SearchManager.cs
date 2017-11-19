using ScintillaNET;
using System.Text.RegularExpressions;
using System.Windows.Controls;

namespace SB_IDE
{
    public class SearchManager
    {
		public Scintilla TextArea;

        public void Find(bool next, string search)
        {
            HighLight(MainWindow.highlightAll ? search : "");

            if (search.Length > 0)
            {
				if (next)
                {
					// SEARCH FOR THE NEXT OCCURANCE

                    // Search the document from the caret onwards
                    TextArea.TargetStart = TextArea.CurrentPosition;
                    TextArea.TargetEnd = TextArea.TextLength;
                    TextArea.SearchFlags = SearchFlags.None;

                    // Search, and if not found..
                    if (TextArea.SearchInTarget(search) == -1)
                    {
                        // Search again from top
                        TextArea.TargetStart = 0;
                        TextArea.TargetEnd = TextArea.TextLength;

                        // Search, and if not found..
                        if (TextArea.SearchInTarget(search) == -1)
                        {
                            // clear selection and exit
                            TextArea.ClearSelections();
                            return;
                        }
                    }
                }
                else
                {
                    // SEARCH FOR THE PREVIOUS OCCURANCE

                    // Search the document from the caret backwards
                    TextArea.TargetStart = TextArea.CurrentPosition - 1;
                    TextArea.TargetEnd = 0;
                    TextArea.SearchFlags = SearchFlags.None;

                    // Search, and if not found..
                    if (TextArea.SearchInTarget(search) == -1)
                    {
                        // Search again from top
                        TextArea.TargetStart = TextArea.TextLength;
                        TextArea.TargetEnd = 0;

                        // Search, and if not found..
                        if (TextArea.SearchInTarget(search) == -1)
                        {
                            // clear selection and exit
                            TextArea.ClearSelections();
                            return;
                        }
                    }
                }

				// Select the occurance
				TextArea.SetSelection(TextArea.TargetEnd, TextArea.TargetStart);
				TextArea.ScrollCaret();
			}
		}

        public void HighLight(string search)
        {
            if (search.Length > 0)
            {
                TextArea.Indicators[0].ForeColor = SBDocument.IntToColor(MainWindow.FIND_HIGHLIGHT_COLOR);
                TextArea.Indicators[0].Style = IndicatorStyle.RoundBox;
                TextArea.TargetStart = 0;
                TextArea.TargetEnd = TextArea.TextLength;

                RegexOptions caseSensitive = RegexOptions.IgnoreCase;
                MatchCollection matches = Regex.Matches(TextArea.Text, search, caseSensitive);
                foreach (Match match in matches)
                {
                    TextArea.IndicatorFillRange(match.Index, match.Length);
                }
            }
            else
            {
                TextArea.IndicatorClearRange(0, TextArea.TextLength);
            }
        }
    }
}
