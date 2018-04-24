using System.Net.Sockets;
using Microsoft.SmallBasic.Library;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

[assembly: AssemblyVersion("1.0.0.0")]
namespace SBDebugger
{
    [SmallBasicType, HideFromIntellisense]
    public static class SBDebug
    {
        private static TcpClient tcpClient = null;
        private static object lockSend = new object();
        private static Thread applicationThread;
        private static List<int> lineBreaks = new List<int>();

        private static string[] separators = new string[] { "\0" };
        private static bool bStep = false;
        private static bool ignoreBP = false;

        [HideFromIntellisense]
        public static void Start()
        {
            applicationThread = Thread.CurrentThread;
            tcpClient = new TcpClient(GetIP().ToString(), 100);
            Thread thread = new Thread(new ThreadStart(Listen));
            thread.Start();
            applicationThread.Suspend();
        }

        [HideFromIntellisense]
        public static void Break(Primitive line)
        {
            if (!ignoreBP  && (bStep || lineBreaks.Contains(line)))
            {
                Send("BREAK " + line);
                bStep = false;
                applicationThread.Suspend();
            }
        }

        private static IPAddress GetIP()
        {
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus == OperationalStatus.Up && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                {
                    foreach (UnicastIPAddressInformation ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            return ua.Address;
                        }
                    }
                }
            }
            return IPAddress.None;
        }

        private static void Listen()
        {
            try
            {
                NetworkStream networkStream = tcpClient.GetStream();
                byte[] bytes;
                string[] messages;

                while (true)
                {
                    if (tcpClient.Connected)
                    {
                        bytes = new byte[tcpClient.ReceiveBufferSize];
                        networkStream.Read(bytes, 0, bytes.Length);
                        string dataFromClient = Encoding.UTF8.GetString(bytes);
                        messages = dataFromClient.Split(separators, StringSplitOptions.RemoveEmptyEntries);
                        for (int i = 0; i < messages.Length; i++)
                        {
                            string message = messages[i].ToUpper();
                            if (message.Length > 0)
                            {
                                if (message.StartsWith("PAUSE") && applicationThread.ThreadState == System.Threading.ThreadState.Running) applicationThread.Suspend();
                                else if (message.StartsWith("RESUME") && applicationThread.ThreadState == System.Threading.ThreadState.Suspended) applicationThread.Resume();
                                else if (message.StartsWith("ADDBREAK"))
                                {
                                    int line = -1;
                                    int.TryParse(message.Substring(8), out line);
                                    if (line >= 0) lineBreaks.Add(line);
                                }
                                else if (message.StartsWith("REMOVEBREAK"))
                                {
                                    int line = -1;
                                    int.TryParse(message.Substring(11), out line);
                                    if (line >= 0) lineBreaks.Remove(line);
                                }
                                else if (message.StartsWith("REMOVEALLBREAKS"))
                                {
                                    lineBreaks.Clear();
                                }
                                else if (message.StartsWith("STEP"))
                                {
                                    bStep = true;
                                    if (applicationThread.ThreadState == System.Threading.ThreadState.Suspended) applicationThread.Resume();
                                }
                                else if (message.StartsWith("IGNORE"))
                                {
                                    bool.TryParse(message.Substring(6), out ignoreBP);
                                }
                                else if (message.StartsWith("GETVALUE"))
                                {
                                    if (applicationThread.ThreadState == System.Threading.ThreadState.Suspended)
                                    {
                                        string var = message.Substring(8).Trim();
                                        Send("VALUE " + var + " " + GetValue(var));
                                    }
                                }
                                else if (message.StartsWith("GETHOVER"))
                                {
                                    if (applicationThread.ThreadState == System.Threading.ThreadState.Suspended)
                                    {
                                        string var = message.Substring(8).Trim();
                                        Send("HOVER " + var + " " + GetValue(var));
                                    }
                                }
                                else if (message.StartsWith("SETVALUE"))
                                {
                                    if (applicationThread.ThreadState == System.Threading.ThreadState.Suspended)
                                    {
                                        message = message.Substring(8).Trim();
                                        int pos = message.IndexOf(' ');
                                        string var = message.Substring(0, pos).Trim();
                                        string value = message.Substring(pos).Trim();
                                        SetValue(var, value);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        Thread.Sleep(10);
                    }
                }
            }
            catch
            {
            }
        }

        private static void Send(string message)
        {
            lock (lockSend)
            {
                if (!tcpClient.Connected) return;
                Byte[] bytes = Encoding.UTF8.GetBytes(message + '\0');
                NetworkStream networkStream = tcpClient.GetStream();
                networkStream.Write(bytes, 0, bytes.Length);
            }
        }

        private static Primitive GetValue(string var)
        {
            try
            {
                StackTrace stackTrace = new StackTrace(applicationThread, false);
                StackFrame frame = stackTrace.GetFrame(stackTrace.FrameCount - 1);
                MethodBase method = frame.GetMethod();
                Type type = method.DeclaringType;
                //string[] data = var.Split(new char[] { '[', ']' });
                Primitive result = (Primitive)type.GetField(var, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.IgnoreCase).GetValue(null);
                return result;
            }
            catch
            {
                return "";
            }
        }

        private static void SetValue(string var, string value)
        {
            try
            {
                StackTrace stackTrace = new StackTrace(applicationThread, false);
                StackFrame frame = stackTrace.GetFrame(stackTrace.FrameCount - 1);
                MethodBase method = frame.GetMethod();
                Type type = method.DeclaringType;
                type.GetField(var, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.IgnoreCase).SetValue(null, (Primitive)value);
            }
            catch
            {
            }
        }
    }
}