﻿//The following Copyright applies to SB-Prime for Small Basic and files in the namespace SB_Prime. 
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

using SB_Prime.Dialogs;
using ScintillaNET;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace SB_Prime
{
    public class SBDebug : IDisposable
    {
        SBInterop sbInterop;
        SBDocument sbDocument;
        MainWindow mainWindow;
        bool debug = false;

        Process process;
        string tempExe;
        IPAddress ip;
        int port = 100;
        TcpListener tcpListener = null;
        TcpClient tcpServer = null;
        object lockSend = new object();
        Queue<string> messageQueue = new Queue<string>();
        Timer threadTimer;
        bool paused = false;
        int maxValueLen = 500;

        public List<string> CallStack = new List<string>();
        public Dictionary<string, string> Variables = new Dictionary<string, string>();

        public bool IsDebug()
        {
            return debug;
        }

        public SBDebug(MainWindow mainWindow, SBInterop sbInterop, SBDocument sbDocument, bool debug)
        {
            this.mainWindow = mainWindow;
            this.sbInterop = sbInterop;
            this.sbDocument = sbDocument;
            this.debug = debug;

            threadTimer = new Timer(new TimerCallback(ThreadTimerCallback));
            threadTimer.Change(100, 100);

            ip = GetIP();
        }

        public void Compile()
        {
            try
            {
                string tempCode = Path.GetTempFileName();
                File.Delete(tempCode);
                FileFilter.WriteAllText(tempCode, sbDocument.TextArea.Text);

                if (debug)
                {
                    string tempDebug = Instrument(tempCode);
                    File.Delete(tempCode);
                    tempExe = sbInterop.Compile(tempDebug, debug);
                    File.Delete(tempDebug);
                    if (sbDocument.Filepath != "")
                    {
                        string runExe = Path.GetDirectoryName(sbDocument.Filepath) + "\\" + Path.GetFileNameWithoutExtension(sbDocument.Filepath) + "_debug.exe";
                        File.Copy(tempExe, runExe, true);
                        File.Delete(tempExe);
                        tempExe = Path.GetFullPath(runExe);
                        sbInterop.CopyExtensions(Path.GetDirectoryName(tempExe));
                    }
                }
                else
                {
                    tempExe = sbInterop.Compile(tempCode, debug);
                    File.Delete(tempCode);
                    if (sbDocument.Filepath != "")
                    {
                        string runExe = Path.ChangeExtension(sbDocument.Filepath, ".exe");
                        File.Copy(tempExe, runExe, true);
                        File.Delete(tempExe);
                        tempExe = Path.GetFullPath(runExe);
                        sbInterop.CopyExtensions(Path.GetDirectoryName(tempExe));
                    }
                }
            }
            catch (Exception ex)
            {
                MainWindow.Errors.Add(new Error("Compile : " + ex.Message));
                tempExe = "";
            }
        }

        public Process Run(bool bContinue, bool debug)
        {
            try
            {
                if (tempExe == "")
                {
                    MainWindow.Errors.Add(new Error("Run : " + Properties.Strings.String67));
                    sbDocument.Proc = null;
                    if (null == sbDocument.debug) return null;
                    sbDocument.debug.Dispose();
                    sbDocument.debug = null;
                    return null;
                }
                process = sbInterop.Run(tempExe);
                if (debug)
                {
                    tcpListener = new TcpListener(ip, port);
                    tcpListener.Start();
                    tcpServer = tcpListener.AcceptTcpClient();
                    Thread.Sleep(100);
                    if (null == tcpServer || !tcpServer.Connected) Thread.Sleep(1000);
                    if (null == tcpServer || !tcpServer.Connected) return null;
                    SetBreakPoints();
                    SetWatches();
                    if (bContinue) Resume();
                    else Step();
                    Thread worker = new Thread(new ThreadStart(Listen));
                    worker.Start();
                }
                MainWindow.Errors.Add(new Error("Run : " + Properties.Strings.String68 + " " + process.Id));
                return process;
            }
            catch (Exception ex)
            {
                MainWindow.Errors.Add(new Error("Run : " + ex.Message));
                return null;
            }
        }

        public void Step()
        {
            try
            {
                sbDocument.TextArea.ClearSelections();
                if (paused) SetWatches();
                paused = false;
                Send("STEP");
            }
            catch (Exception ex)
            {
                MainWindow.Errors.Add(new Error("Step : " + ex.Message));
            }
        }

        public void StepOver()
        {
            try
            {
                sbDocument.TextArea.ClearSelections();
                if (paused) SetWatches();
                paused = false;
                Send("STEPOVER");
            }
            catch (Exception ex)
            {
                MainWindow.Errors.Add(new Error("StepOver : " + ex.Message));
            }
        }

        public void StepOut()
        {
            try
            {
                sbDocument.TextArea.ClearSelections();
                if (paused) SetWatches();
                paused = false;
                Send("STEPOUT");
            }
            catch (Exception ex)
            {
                MainWindow.Errors.Add(new Error("StepOut : " + ex.Message));
            }
        }

        public void Pause()
        {
            try
            {
                Send("PAUSE");
            }
            catch (Exception ex)
            {
                MainWindow.Errors.Add(new Error("Pause : " + ex.Message));
            }
        }

        public void Resume()
        {
            try
            {
                sbDocument.TextArea.ClearSelections();
                if (paused) SetWatches();
                paused = false;
                Send("RESUME");
            }
            catch (Exception ex)
            {
                MainWindow.Errors.Add(new Error("Resume : " + ex.Message));
            }
        }

        public void ClearBP()
        {
            try
            {
                Send("REMOVEALLBREAKS");
            }
            catch (Exception ex)
            {
                MainWindow.Errors.Add(new Error("Resume : " + ex.Message));
            }
        }

        public void SetBreakPoint(int line, bool set)
        {
            try
            {
                if (set) Send("ADDBREAK " + line);
                else Send("REMOVEBREAK " + line);
            }
            catch (Exception ex)
            {
                MainWindow.Errors.Add(new Error("Resume : " + ex.Message));
            }
        }

        public void Ignore(bool state)
        {
            Send("IGNORE "+ state);
        }

        public bool IsRunning()
        {
            if (null == process) return false;
            return !process.HasExited;
        }

        public bool IsPaused()
        {
            return paused;
        }

        public void GetValue(string var)
        {
            Send("GETVALUE " + var);
        }

        public void GetHover(string var)
        {
            Send("GETHOVER " + var);
        }

        public void SetValue(string var, string value)
        {
            Send("SETVALUE " + var + " " + value);
        }

        public void ClearConditions()
        {
            Send("CLEARWATCHES");
        }

        public void SetCondition(DebugData data)
        {
            Send("SETWATCH " + data.Variable + "?" + data.LessThan + "?" + data.GreaterThan + "?" + data.Equal + "?" + data.Changes);
        }

        public void GetStack()
        {
            Send("GETSTACK");
        }

        public void GetVariables()
        {
            Send("GETVARIABLES");
        }

        private void ThreadTimerCallback(object state)
        {
            try
            {
                if (mainWindow.CheckAccess())
                {
                    DoMessages();
                }
                else
                {
                    mainWindow.Dispatcher.Invoke(() =>
                    {
                        DoMessages();
                    });
                }
            }
            catch
            {
            }
        }

        private void SetBreakPoints()
        {
            const uint mask = (1 << SBDocument.BREAKPOINT_MARKER);
            Send("REMOVEALLBREAKS");
            foreach (Line line in sbDocument.TextArea.Lines)
            {
                if ((line.MarkerGet() & mask) > 0)
                {
                    Send("ADDBREAK " + line.Index);
                }
            }
            Send("IGNORE " + MainWindow.ignoreBP); 
        }

        private void SetWatches()
        {
            ClearConditions();
            foreach (DebugData data in mainWindow.debugData)
            {
                SetCondition(data);
            }
        }

        private void Listen()
        {
            NetworkStream networkStream;
            byte[] bytes;
            string[] messages;
            string[] separators = new string[] { "\0" };

            while (!process.HasExited)
            {
                try
                {
                    if (tcpServer.Connected)
                    {
                        bytes = new byte[tcpServer.ReceiveBufferSize];
                        networkStream = tcpServer.GetStream();
                        networkStream.Read(bytes, 0, bytes.Length);
                        string dataFromClient = Encoding.UTF8.GetString(bytes);
                        messages = dataFromClient.Split(separators, StringSplitOptions.RemoveEmptyEntries);
                        for (int i = 0; i < messages.Length; i++)
                        {
                            string message = messages[i];
                            if (message.Length > 0)
                            {
                                messageQueue.Enqueue(message);
                            }
                        }
                    }
                    else
                    {
                        Thread.Sleep(10);
                    }
                }
                catch
                {
                }
            }
            try
            {
                if (null != tcpServer) tcpServer.Close();
                if (null != tcpListener) tcpListener.Stop();
                tcpServer = null;
                tcpListener = null;
                //if (!process.HasExited)
                //{
                //    process.Kill();
                //}
                //if (null != sbDocument.Proc)
                //{
                //    sbDocument.ClearHighlights();
                //    MainWindow.Errors.Add(new Error("Run : " + Properties.Strings.String71 + " " + sbDocument.Proc.Id));
                //}
                if (sbDocument.Filepath == "" || debug) File.Delete(tempExe);
                sbDocument.Proc = null;
                //if (null == sbDocument.debug) return;
                //sbDocument.debug.Dispose();
                sbDocument.debug = null;
            }
            catch
            {
            }
        }

        private void DoMessages()
        {
            while (messageQueue.Count > 0)
            {
                string message = messageQueue.Dequeue();
                if (message.ToUpper().StartsWith("BREAK"))
                {
                    int iLine = -1;
                    message = message.Substring(5);
                    string[] data = message.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (data.Length > 0) int.TryParse(data[0], out iLine);
                    if (iLine >= 0)
                    {
                        Line line = sbDocument.TextArea.Lines[iLine];
                        sbDocument.TextArea.CurrentPosition = line.Position;
                        sbDocument.TextArea.ScrollCaret();
                        sbDocument.TextArea.ClearSelections();

                        sbDocument.ClearHighlights();
                        sbDocument.HighlightLine(line);
                        if (FlowChart.Active)
                        {
                            FlowChart.THIS.HighlightLine(iLine);
                        }
                    }
                    if (data.Length > 1)
                    {
                        mainWindow.dataGridDebug.SelectedCells.Clear();
                        foreach (DebugData item in mainWindow.debugData)
                        {
                            if (item.Variable.ToUpperInvariant() == data[1])
                            {
                                mainWindow.dataGridDebug.ScrollIntoView(item);
                                mainWindow.dataGridDebug.CurrentCell = new DataGridCellInfo(item, mainWindow.dataGridDebug.Columns[1]);
                                mainWindow.dataGridDebug.SelectedCells.Add(mainWindow.dataGridDebug.CurrentCell);
                            }
                        }
                    }
                    paused = true;
                }
                else if (message.ToUpper().StartsWith("VALUE"))
                {
                    message = message.Substring(5).Trim();
                    int pos = message.IndexOf(' ');
                    string key = pos > 0 ? message.Substring(0, pos).Trim() : message;
                    string value = pos > 0 ? message.Substring(pos).Trim() : "";
                    for (int i = 0; i < mainWindow.debugData.Count; i++)
                    {
                        DebugData data = mainWindow.debugData[i];
                        if (data.Variable.ToUpperInvariant() == key.ToUpperInvariant())
                        {
                            if (value.Length > maxValueLen) value = value.Substring(0, maxValueLen) + " ...";
                            data.Value = value;
                            //mainWindow.debugData.RemoveAt(i);
                            //mainWindow.debugData.Insert(i, data);
                            break;
                        }
                    }
                }
                else if (message.ToUpper().StartsWith("HOVER"))
                {
                    message = message.Substring(5).Trim();
                    int pos = message.IndexOf(' ');
                    string key = pos > 0 ? message.Substring(0, pos).Trim() : message;
                    string value = pos > 0 ? message.Substring(pos).Trim() : "";
                    if (value.Length > maxValueLen) value = value.Substring(0, maxValueLen) + " ...";
                    sbDocument.TextArea.CallTipShow(sbDocument.Lexer.toolTipPosition, value);

                    sbDocument.TextArea.CallTipSetHlt(0, value.Length);
                }
                else if (message.ToUpper().StartsWith("STACKLEVEL"))
                {
                    message = message.Substring(11).Trim();
                }
                else if (message.ToUpper().StartsWith("STACK"))
                {
                    message = message.Substring(6).Trim();
                    CallStack = message.Split(new char[] { '?' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                    if (null != mainWindow.stackVariables) mainWindow.stackVariables.Update();
                }
                else if (message.ToUpper().StartsWith("VARIABLES"))
                {
                    message = message.Substring(10).Trim();
                    string[] data = message.Split(new char[] { '?' }, StringSplitOptions.RemoveEmptyEntries);
                    Variables.Clear();
                    foreach (string pair in data)
                    {
                        int pos = pair.IndexOf(',');
                        Variables[pair.Substring(0, pos).Trim(new char[] { '[', ']', ' ' })] = pair.Substring(pos + 1).Trim(new char[] { '[', ']', ' ' });
                    }
                    if (null != mainWindow.stackVariables) mainWindow.stackVariables.Update();
                }
                else if (message.ToUpper().StartsWith("DEBUG"))
                {
                    message = message.Substring(6).Trim();
#if DEBUG
                    MainWindow.Errors.Add(new Error(message));
#endif
                }
            }
        }

        private void Send(string message)
        {
            lock (lockSend)
            {
                if (null == tcpServer || !tcpServer.Connected) return;
                Byte[] bytes = Encoding.UTF8.GetBytes(message + '\0');
                NetworkStream networkStream = tcpServer.GetStream();
                networkStream.Write(bytes, 0, bytes.Length);
            }
        }

        private string Instrument(string fileName)
        {
            try
            {
                if (!File.Exists(fileName)) return "";

                string[] lines = FileFilter.ReadAllLines(fileName);
                List<string> output = new List<string>();
                output.Add("SBDebug.Start()");
                for (int i = 0; i < lines.Length; i++)
                {
                    output.Add("SBDebug.Break(" + i.ToString() + ")");
                    output.Add(lines[i]);
                }
                string fileOutput = Path.GetTempFileName();
                File.Delete(fileOutput);
                FileFilter.WriteAllLines(fileOutput, output);
                return fileOutput;
            }
            catch (Exception ex)
            {
                MainWindow.Errors.Add(new Error("Instrument : " + ex.Message));
                return "";
            }
        }

        private IPAddress GetIP()
        {
            return IPAddress.Loopback;
            //foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            //{
            //    if (ni.OperationalStatus == OperationalStatus.Up && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            //    {
            //        foreach (UnicastIPAddressInformation ua in ni.GetIPProperties().UnicastAddresses)
            //        {
            //            if (ua.Address.AddressFamily == AddressFamily.InterNetwork)
            //            {
            //                return ua.Address;
            //            }
            //        }
            //    }
            //}
            //return IPAddress.None;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // free managed resources
                try
                {
                    threadTimer.Dispose();
                    if (null != tcpServer) tcpServer.Close();
                    if (null != tcpListener) tcpListener.Stop();
                    tcpServer = null;
                    tcpListener = null;
                    int id = (null == sbDocument.Proc) ? -1 : sbDocument.Proc.Id;
                    if (!process.HasExited) process.Kill();
                    if (null != sbDocument.Proc)
                    {
                        sbDocument.ClearHighlights();
                        MainWindow.Errors.Add(new Error("Run : " + Properties.Strings.String61 + " " + id));
                        sbDocument.Proc = null;
                    }
                    if (sbDocument.Filepath == "" || debug) File.Delete(tempExe);
                }
                catch
                {
                }
            }
            // free native resources if there are any.
        }
    }
}
