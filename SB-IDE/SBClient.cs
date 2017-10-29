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
        private static Thread currentThread = null;
        private static List<int> lineBreaks = new List<int>();

        private static string[] separators = new string[] { "\0" };
        private static bool bStep = false;
        private static bool bStepOver = false;
        private static bool bStepOut = false;
        private static bool ignoreBP = false;
        private static int stackLevel = 0;

        private static Type PrimitiveType = typeof(Primitive);
        private static MethodInfo GetArrayValue = PrimitiveType.GetMethod("GetArrayValue", BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase);
        private static MethodInfo SetArrayValue = PrimitiveType.GetMethod("SetArrayValue", BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase);

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
            if (bStep || (!ignoreBP && lineBreaks.Contains(line)))
            {
                Send("BREAK " + line);
                bStep = false;
                //currentThread = applicationThread == Thread.CurrentThread ? null : Thread.CurrentThread;
                //if (null != currentThread) currentThread.Suspend();
                applicationThread.Suspend();
            }
            else if (bStepOut)
            {
                int curStackLevel = GetStackLevel();
                if (curStackLevel > 0 && curStackLevel < stackLevel)
                {
                    Send("BREAK " + line);
                    bStepOut = false;
                    stackLevel = 0;
                    //currentThread = applicationThread == Thread.CurrentThread ? null : Thread.CurrentThread;
                    //if (null != currentThread) currentThread.Suspend();
                    applicationThread.Suspend();
                }
            }
            else if (bStepOver)
            {
                int curStackLevel = GetStackLevel();
                if (curStackLevel > 0 && curStackLevel <= stackLevel)
                {
                    Send("BREAK " + line);
                    bStepOver = false;
                    stackLevel = 0;
                    //currentThread = applicationThread == Thread.CurrentThread ? null : Thread.CurrentThread;
                    //if (null != currentThread) currentThread.Suspend();
                    applicationThread.Suspend();
                }
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
                            string message = messages[i].Trim();
                            //GraphicsWindow.Title = message;
                            if (message.Length > 0)
                            {
                                if (message.ToUpper().StartsWith("PAUSE"))
                                {
                                    if (applicationThread.ThreadState != System.Threading.ThreadState.Suspended)
                                    {
                                        bStep = true;
                                    }
                                }
                                else if (message.ToUpper().StartsWith("RESUME"))
                                {
                                    if (applicationThread.ThreadState == System.Threading.ThreadState.Suspended)
                                    {
                                        applicationThread.Resume();
                                        if (null != currentThread && currentThread.ThreadState == System.Threading.ThreadState.Suspended) currentThread.Resume();
                                    }
                                }
                                else if (message.ToUpper().StartsWith("ADDBREAK"))
                                {
                                    int line = -1;
                                    int.TryParse(message.Substring(8), out line);
                                    if (line >= 0) lineBreaks.Add(line);
                                }
                                else if (message.ToUpper().StartsWith("REMOVEBREAK"))
                                {
                                    int line = -1;
                                    int.TryParse(message.Substring(11), out line);
                                    if (line >= 0) lineBreaks.Remove(line);
                                }
                                else if (message.ToUpper().StartsWith("REMOVEALLBREAKS"))
                                {
                                    lineBreaks.Clear();
                                }
                                else if (message.ToUpper().StartsWith("STEPOUT"))
                                {
                                    if (applicationThread.ThreadState == System.Threading.ThreadState.Suspended)
                                    {
                                        stackLevel = GetStackLevel();
                                        if (stackLevel > 0) bStepOut = true;
                                        applicationThread.Resume();
                                        if (null != currentThread && currentThread.ThreadState == System.Threading.ThreadState.Suspended) currentThread.Resume();
                                    }
                                }
                                else if (message.ToUpper().StartsWith("STEPOVER"))
                                {
                                    if (applicationThread.ThreadState == System.Threading.ThreadState.Suspended)
                                    {
                                        stackLevel = GetStackLevel();
                                        if (stackLevel > 0) bStepOver = true;
                                        applicationThread.Resume();
                                        if (null != currentThread && currentThread.ThreadState == System.Threading.ThreadState.Suspended) currentThread.Resume();
                                    }
                                }
                                else if (message.ToUpper().StartsWith("STEP"))
                                {
                                    bStep = true;
                                    if (applicationThread.ThreadState == System.Threading.ThreadState.Suspended)
                                    {
                                        applicationThread.Resume();
                                        if (null != currentThread && currentThread.ThreadState == System.Threading.ThreadState.Suspended) currentThread.Resume();
                                    }
                                }
                                else if (message.ToUpper().StartsWith("IGNORE"))
                                {
                                    bool.TryParse(message.Substring(6), out ignoreBP);
                                }
                                else if (message.ToUpper().StartsWith("GETVALUE"))
                                {
                                    if (applicationThread.ThreadState == System.Threading.ThreadState.Suspended)
                                    {
                                        string var = message.Substring(8).Trim();
                                        Send("VALUE " + var + " " + GetValue(var));
                                    }
                                }
                                else if (message.ToUpper().StartsWith("GETHOVER"))
                                {
                                    if (applicationThread.ThreadState == System.Threading.ThreadState.Suspended)
                                    {
                                        string var = message.Substring(8).Trim();
                                        Send("HOVER " + var + " " + GetValue(var));
                                    }
                                }
                                else if (message.ToUpper().StartsWith("SETVALUE"))
                                {
                                    if (applicationThread.ThreadState == System.Threading.ThreadState.Suspended)
                                    {
                                        message = message.Substring(8).Trim();
                                        int pos = message.IndexOf(' ');
                                        string var = message.Substring(0, pos).Trim();
                                        string value = message.Substring(pos).Trim();
                                        string result = SetValue(var, value);
                                        if (result != "") Send("VALUE " + var + " " + result);
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
                if (applicationThread.ThreadState != System.Threading.ThreadState.Suspended) return "";
                StackTrace stackTrace = new StackTrace(applicationThread, false);
                StackFrame frame = stackTrace.GetFrame(stackTrace.FrameCount - 1);
                MethodBase method = frame.GetMethod();
                Type type = method.DeclaringType;
                //Primitive result = (Primitive)type.GetField(var, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.IgnoreCase).GetValue(null);
                string[] data = var.Split(new char[] { '[', ']' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 1; i < data.Length; i++)
                {
                    try
                    {
                        Primitive key = (Primitive)type.GetField(data[i], BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.IgnoreCase).GetValue(null);
                        if (null != key) data[i] = key;
                    }
                    catch
                    {

                    }
                }
                Primitive result = (Primitive)type.GetField(data[0], BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.IgnoreCase).GetValue(null);
                for (int i = 1; i < data.Length; i++)
                {
                    result = result[(Primitive)data[i]];
                }
                return result;
            }
            catch (Exception ex)
            {
                return "";
                return ex.Message;
            }
        }

        private static string SetValue(string var, string value)
        {
            try
            {
                if (applicationThread.ThreadState != System.Threading.ThreadState.Suspended) return "";
                StackTrace stackTrace = new StackTrace(applicationThread, false);
                StackFrame frame = stackTrace.GetFrame(stackTrace.FrameCount - 1);
                MethodBase method = frame.GetMethod();
                Type type = method.DeclaringType;
                //type.GetField(var, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.IgnoreCase).SetValue(null, (Primitive)value);
                string[] data = var.Split(new char[] { '[', ']' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 1; i < data.Length; i++)
                {
                    try
                    {
                        Primitive key = (Primitive)type.GetField(data[i], BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.IgnoreCase).GetValue(null);
                        if (null != key) data[i] = key;
                    }
                    catch
                    {

                    }
                }
                Primitive result = (Primitive)type.GetField(data[0], BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.IgnoreCase).GetValue(null);

                Primitive[] working = new Primitive[data.Length];
                working[0] = result;
                for (int i = 1; i < data.Length; i++)
                {
                    working[i] = working[i - 1][data[i]];
                }
                working[data.Length - 1] = value;
                for (int i = data.Length - 2; i >= 0; i--)
                {
                    working[i] = (Primitive)SetArrayValue.Invoke(null, new object[] { working[i + 1], working[i], (Primitive)data[i + 1] });
                }

                result = working[0];
                type.GetField(data[0], BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.IgnoreCase).SetValue(null, result);
                return "";
            }
            catch (Exception ex)
            {
                return "";
                return ex.Message;
            }
        }

        private static int GetStackLevel()
        {
            int result = 0;
            try
            {
                StackTrace stackTrace = new StackTrace(applicationThread, false);
                for (int i = 0; i < stackTrace.FrameCount; i++)
                {
                    StackFrame frame = stackTrace.GetFrame(i);
                    MethodBase method = frame.GetMethod();
                    if (method.DeclaringType.Name == "_SmallBasicProgram") result++;
                }
                return result;
            }
            catch
            {
                return result;
            }
        }

        private static List<string> GetStack()
        {
            try
            {
                List<string> result = new List<string>();
                StackTrace stackTrace = new StackTrace(applicationThread, false);
                for (int i = 0; i < stackTrace.FrameCount; i++)
                {
                    StackFrame frame = stackTrace.GetFrame(i);
                    MethodBase method = frame.GetMethod();
                    if (method.DeclaringType.Name == "_SmallBasicProgram") result.Add(method.Name);
                }
                return result;
            }
            catch
            {
                return null;
            }
        }
    }
}