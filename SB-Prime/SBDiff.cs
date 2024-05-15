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
        public static bool bEndDiff = false;
        public static SBDocument doc1 = null;
        public static SBDocument doc2 = null;

        private static bool bShowDiff = false;
        private static Timer timer = new Timer(_timer, null, 1000, 1000);
        private static bool bRefresh = false;

        private static void _timer(object state)
        {
            try
            {
                MainWindow.THIS.Dispatcher.Invoke(() =>
                {
                    if (bEndDiff)
                    {
                        ClearDiff();
                        doc1 = null;
                        doc2 = null;
                        bEndDiff = false;
                    }
                    if (bRefresh) ClearDiff();
                    if (null != doc1 && null == doc1.Layout.Content) doc1 = null;
                    if (null != doc2 && null == doc2.Layout.Content) doc2 = null;
                    bShowDiff = null != doc1 || null != doc2;
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
            if (null == doc1 || null == doc2 || doc1 == doc2) return;

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
            try
            {
                if (null != doc1)
                {
                    foreach (Line line in doc1.TextArea.Lines)
                    {
                        line.MarkerDelete(SBDocument.DELETED_MARKER);
                        line.MarkerDelete(SBDocument.INSERTED_MARKER);
                    }
                }
                if (null != doc2)
                {
                    foreach (Line line in doc2.TextArea.Lines)
                    {
                        line.MarkerDelete(SBDocument.DELETED_MARKER);
                        line.MarkerDelete(SBDocument.INSERTED_MARKER);
                    }
                }
            }
            catch
            {
                doc1 = null;
                doc2 = null;
            }
        }
    }
}
