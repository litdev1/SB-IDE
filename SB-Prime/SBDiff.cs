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

using my.utils;
using ScintillaNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace SB_Prime
{
    static class SBDiff
    {
        public static bool bShowDiff = false;
        private static Timer timer = new Timer(_timer, null, 1000, 1000);
        private static bool bRefresh = false;
        private static TabControl tabConstrol1 = MainWindow.THIS.tabControlSB1;
        private static TabControl tabConstrol2 = MainWindow.THIS.tabControlSB2;

        private static void _timer(object state)
        {
            try
            {
                MainWindow.THIS.Dispatcher.Invoke(() =>
                {
                    if (bRefresh) ClearDiff();
                    if (bShowDiff)
                    {
                        SetDiff();
                        bRefresh = true;
                    }
                    else
                    {
                        bRefresh = false;
                    }
                });
            }
            catch
            {

            }
        }

        public static void UpdateDiff()
        {
            bShowDiff = !bShowDiff;
        }

        private static void SetDiff()
        {
            TabItem item1 = (TabItem)tabConstrol1.Items[tabConstrol1.SelectedIndex];
            SBDocument doc1 = (SBDocument)item1.Tag;
            TabItem item2 = (TabItem)tabConstrol2.Items[tabConstrol2.SelectedIndex];
            SBDocument doc2 = (SBDocument)item2.Tag;

            Diff.Item[] items = Diff.DiffText(doc1.TextArea.Text, doc2.TextArea.Text, true, true, true);

            foreach (Diff.Item item in items)
            {
                Marker marker1 = doc1.TextArea.Markers[SBDocument.DELETED_MARKER];
                marker1.Symbol = MarkerSymbol.Background;
                marker1.SetBackColor(SBDocument.IntToColor(MainWindow.DELETED_HIGHLIGHT_COLOR));
                for (int i = item.StartA; i < item.StartA + item.deletedA; i++)
                {
                    doc1.TextArea.Lines[i].MarkerAdd(SBDocument.DELETED_MARKER);
                }
                Marker marker2 = doc2.TextArea.Markers[SBDocument.INSERTED_MARKER];
                marker2.Symbol = MarkerSymbol.Background;
                marker2.SetBackColor(SBDocument.IntToColor(MainWindow.INSERTED_HIGHLIGHT_COLOR));
                for (int i = item.StartB; i < item.StartB + item.insertedB; i++)
                {
                    doc2.TextArea.Lines[i].MarkerAdd(SBDocument.INSERTED_MARKER);
                }
            }
        }

        private static void ClearDiff()
        {
            foreach (TabItem item in tabConstrol1.Items)
            {
                SBDocument doc = (SBDocument)item.Tag;
                foreach (Line line in doc.TextArea.Lines)
                {
                    line.MarkerDelete(SBDocument.DELETED_MARKER);
                    line.MarkerDelete(SBDocument.INSERTED_MARKER);
                }
            }

            foreach (TabItem item in tabConstrol2.Items)
            {
                SBDocument doc = (SBDocument)item.Tag;
                foreach (Line line in doc.TextArea.Lines)
                {
                    line.MarkerDelete(SBDocument.DELETED_MARKER);
                    line.MarkerDelete(SBDocument.INSERTED_MARKER);
                }
            }
        }
    }
}
