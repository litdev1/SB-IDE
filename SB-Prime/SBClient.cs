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
using System.Runtime.InteropServices;

[assembly: AssemblyVersion("1.0.0.0")]
namespace SBDebugger
{
    [SmallBasicType, HideFromIntellisense]
    public static class SBDebug
    {
        private static TcpClient tcpClient = null;
        private static object lockSend = new object();
        private static Thread applicationThread = null;
        private static Thread currentThread = null;
        private static Thread listenThread = null;
        private static int applicationId = -1;
        private static int listenId = -1;
        private static ProcessThreadCollection threadsInitial;
        private static List<ProcessThread> threadsCritical;
        private static List<int> lineBreaks = new List<int>();
        private static List<Watch> watches = new List<Watch>();
        private static MethodBase methodBase;
        private static bool paused = false;
        private static StackTrace stackTraceApplication = null;
        private static StackTrace stackTraceCurrent = null;

        private static string[] separators = new string[] { "\0" };
        private static bool bStep = false;
        private static bool bStepOver = false;
        private static bool bStepOut = false;
        private static bool ignoreBP = false;
        private static int stackLevel = 0;

        private static Type PrimitiveType = typeof(Primitive);
        private static MethodInfo GetArrayValue = PrimitiveType.GetMethod("GetArrayValue", BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase);
        private static MethodInfo SetArrayValue = PrimitiveType.GetMethod("SetArrayValue", BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr OpenThread(uint dwDesiredAccess, bool bInheritHandle, uint dwThreadId);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern int SuspendThread(IntPtr hThread);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern uint ResumeThread(IntPtr hThread);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern int GetCurrentThreadId();

        private enum ThreadAccess : uint
        {
            TERMINATE = (0x0001),
            SUSPEND_RESUME = (0x0002),
            GET_CONTEXT = (0x0008),
            SET_CONTEXT = (0x0010),
            SET_INFORMATION = (0x0020),
            QUERY_INFORMATION = (0x0040),
            SET_THREAD_TOKEN = (0x0080),
            IMPERSONATE = (0x0100),
            DIRECT_IMPERSONATION = (0x0200)
        }

        private static void ShowThreads(string info)
        {
            string ids = "";
            foreach (ProcessThread thread in Process.GetCurrentProcess().Threads)
            {
                ids += thread.Id;
                if (thread.ThreadState == System.Diagnostics.ThreadState.Wait) ids += "(" + thread.WaitReason.ToString() + ")";
                ids += ",";
            }
            Send("DEBUG " + info + " application=" + applicationId + " current=" + GetCurrentThreadId() + " listen=" + listenId + " : " + ids);
        }

        private static void PauseAll()
        {
            //Send("DEBUG " + "PauseAll");
            //ShowThreads("Pause");
            foreach (ProcessThread thread in Process.GetCurrentProcess().Threads)
            {
                bool bCritical = false;
                foreach (ProcessThread threadCritical in threadsCritical)
                {
                    if (thread.Id == threadCritical.Id)
                    {
                        bCritical = true;
                    }
                }
                if (bCritical) continue;
                if (thread.Id == listenId || thread.Id == GetCurrentThreadId()) continue;
                if (thread.ThreadState == System.Diagnostics.ThreadState.Wait)
                {
                    if (thread.WaitReason == ThreadWaitReason.UserRequest)
                    {
                        //Send("DEBUG " + "not suspend " + thread.Id + " : " + thread.WaitReason);
                        continue;
                    }
                }
                else
                {
                    //Send("DEBUG " + "not suspend " + thread.Id + " : " + thread.ThreadState);
                    continue;
                }
                IntPtr ptrOpenThread = OpenThread((uint)ThreadAccess.SUSPEND_RESUME, false, (uint)thread.Id);
                if (ptrOpenThread == null) continue;
                SuspendThread(ptrOpenThread);
                //Send("DEBUG " + "suspend " + thread.Id);
            }
        }

        private static void ResumeAll()
        {
            //Send("DEBUG " + "ResumeAll");
            //ShowThreads("Resume");
            foreach (ProcessThread thread in Process.GetCurrentProcess().Threads)
            {
                IntPtr ptrOpenThread = OpenThread((uint)ThreadAccess.SUSPEND_RESUME, false, (uint)thread.Id);
                if (ptrOpenThread == null) continue;
                ResumeThread(ptrOpenThread);
                //Send("DEBUG " + "resume " + thread.Id);
            }
        }

        private static void Pause()
        {
            if (applicationThread.ThreadState == System.Threading.ThreadState.Running) applicationThread.Suspend();
            //currentThread = applicationThread == Thread.CurrentThread ? null : Thread.CurrentThread;
            if (null != currentThread && currentThread.ThreadState == System.Threading.ThreadState.Running) currentThread.Suspend();
            PauseAll();
            paused = true;
        }

        private static void Resume()
        {
            if (applicationThread.ThreadState == System.Threading.ThreadState.Suspended) applicationThread.Resume();
            if (null != currentThread && currentThread.ThreadState == System.Threading.ThreadState.Suspended) currentThread.Resume();
            ResumeAll();
            paused = false;
        }

        [HideFromIntellisense]
        public static void Start()
        {
            threadsInitial = Process.GetCurrentProcess().Threads;
            string ids = "";
            foreach (ProcessThread thread in threadsInitial)
            {
                ids += thread.Id;
                if (thread.ThreadState == System.Diagnostics.ThreadState.Wait) ids += "(" + thread.WaitReason.ToString() + ")";
                ids += ",";
            }

            tcpClient = new TcpClient(GetIP().ToString(), 100);
            applicationThread = Thread.CurrentThread;
            applicationId = GetCurrentThreadId();

            //Send("DEBUG " + "Initial" + " application=" + applicationId + " current=" + GetCurrentThreadId() + " listen=" + listenId + " : " + ids);
            //ShowThreads("Start 1");

            listenThread = new Thread(new ThreadStart(Listen));
            listenThread.Start();
            Thread.Sleep(10);

            ProcessThreadCollection threads = Process.GetCurrentProcess().Threads;
            listenId = threads[threads.Count - 1].Id;
            //ShowThreads("Start 2");

            threadsCritical = new List<ProcessThread>();
            foreach (ProcessThread thread in threads)
            {
                bool bCritical = true;
                foreach (ProcessThread threadInitial in threadsInitial)
                {
                    if (thread.Id == threadInitial.Id) bCritical = false;
                }
                if (bCritical)
                {
                    threadsCritical.Add(thread);
                }
            }

            GetStackTrace();
            Pause();
        }

        [HideFromIntellisense]
        public static void Break(Primitive line)
        {
            //Send("DEBUG " + "Break " + line);
            DoBreak(line);
        }

        private static void GetStackTrace()
        {
            try
            {
                Thread _currentThread = applicationThread == Thread.CurrentThread ? null : Thread.CurrentThread;
                StackTrace _stackTraceApplication = new StackTrace(applicationThread, false);
                StackTrace _stackTraceCurrent = currentThread == null ? null : new StackTrace(currentThread, false);

                currentThread = _currentThread;
                stackTraceApplication = _stackTraceApplication;
                stackTraceCurrent = _stackTraceCurrent;
            }
            catch (Exception ex)
            {
            }
        }

        private static void DoBreak(int line)
        {
            if (bStep || (!ignoreBP && lineBreaks.Contains(line)))
            {
                Send("BREAK " + line);
                bStep = false;
                GetStackTrace();
                Pause();
            }
            else if (bStepOut)
            {
                GetStackTrace();
                int curStackLevel = GetStackLevel();
                if (curStackLevel > 0 && curStackLevel < stackLevel)
                {
                    Send("BREAK " + line);
                    bStepOut = false;
                    stackLevel = 0;
                    Pause();
                }
            }
            else if (bStepOver)
            {
                GetStackTrace();
                int curStackLevel = GetStackLevel();
                //Send("DEBUG " + "curStackLevel " + curStackLevel);
                if (curStackLevel > 0 && curStackLevel <= stackLevel)
                {
                    Send("BREAK " + line);
                    bStepOver = false;
                    stackLevel = 0;
                    Pause();
                }
            }
            else if (!ignoreBP && watches.Count > 0)
            {
                foreach (Watch watch in watches)
                {
                    if (watch.Compare(GetValue(watch.Variable)))
                    {
                        Send("BREAK " + line + " " + watch.Variable);
                        GetStackTrace();
                        Pause();
                        break;
                    }
                }
            }
        }

        private static IPAddress GetIP()
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
                                //Send("DEBUG " + "Listen " + message);
                                if (message.ToUpper().StartsWith("PAUSE"))
                                {
                                    bStep = true;
                                }
                                else if (message.ToUpper().StartsWith("RESUME"))
                                {
                                    Resume();
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
                                    stackLevel = GetStackLevel();
                                    if (stackLevel > 0) bStepOut = true;
                                    Resume();
                                }
                                else if (message.ToUpper().StartsWith("STEPOVER"))
                                {
                                    stackLevel = GetStackLevel();
                                    //Send("DEBUG " + "stackLevel " + stackLevel);
                                    if (stackLevel > 0) bStepOver = true;
                                    Resume();
                                }
                                else if (message.ToUpper().StartsWith("STEP"))
                                {
                                    bStep = true;
                                    Resume();
                                }
                                else if (message.ToUpper().StartsWith("IGNORE"))
                                {
                                    bool.TryParse(message.Substring(6), out ignoreBP);
                                }
                                else if (message.ToUpper().StartsWith("GETVALUE"))
                                {
                                    string var = message.Substring(8).Trim();
                                    Send("VALUE " + var + " " + GetValue(var));
                                }
                                else if (message.ToUpper().StartsWith("GETHOVER"))
                                {
                                    string var = message.Substring(8).Trim();
                                    Send("HOVER " + var + " " + GetValue(var));
                                }
                                else if (message.ToUpper().StartsWith("SETVALUE"))
                                {
                                    message = message.Substring(8).Trim();
                                    int pos = message.IndexOf(' ');
                                    string var = message.Substring(0, pos).Trim();
                                    string value = message.Substring(pos).Trim();
                                    string result = SetValue(var, value);
                                    if (result != "") Send("VALUE " + var + " " + result);
                                }
                                else if (message.ToUpper().StartsWith("GETSTACKLEVEL"))
                                {
                                    Send("STACKLEVEL " + GetStackLevel());
                                }
                                else if (message.ToUpper().StartsWith("GETSTACK"))
                                {
                                    Send("STACK " + string.Join("?", GetStack()));
                                }
                                else if (message.ToUpper().StartsWith("GETVARIABLES"))
                                {
                                    Send("VARIABLES " + string.Join("?", GetVariables()));
                                }
                                else if (message.ToUpper().StartsWith("CLEARWATCHES"))
                                {
                                    watches.Clear();
                                }
                                else if (message.ToUpper().StartsWith("SETWATCH"))
                                {
                                    message = message.Substring(8).Trim();
                                    message = message.ToUpper();
                                    string[] data = message.Split(new char[] { '?' });
                                    if (data.Length == 5)
                                    {
                                        Watch watch = new Watch();
                                        watches.Add(watch);
                                        watch.Variable = data[0].ToUpper();
                                        watch.LessThan = data[1].ToUpper();
                                        watch.GreaterThan = data[2].ToUpper();
                                        watch.Equal = data[3].ToUpper();
                                        if (data[4] == "TRUE")
                                        {
                                            watch.bChanges = true;
                                            watch.Changes = GetValue(data[0]).ToUpper();
                                        }
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
            catch (Exception ex)
            {
                Send("DEBUG "+ex.Message);
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

        private static string GetValue(string var)
        {
            try
            {
                MethodBase method = GetMethodBase();
                if (null == method)
                {
                    //Send("DEBUG " + "GetValue null method");
                    return "";
                }

                //Send("DEBUG " + "GetValue " + var);
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
                //Send("DEBUG " + "GetValue data " + result);
                for (int i = 1; i < data.Length; i++)
                {
                    result = result[(Primitive)data[i]];
                }
                return result;
            }
            catch (Exception ex)
            {
                //Send("DEBUG " + "GetValue " + ex.Message);
                return "";
            }
        }

        private static string SetValue(string var, string value)
        {
            try
            {
                MethodBase method = GetMethodBase();
                if (null == method) return "";

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
                //Send("DEBUG " + "SetValue " + ex.Message);
                return "";
            }
        }

        private static int GetStackLevel()
        {
            int result = 0;
            try
            {
                if (null != stackTraceApplication)
                {
                    for (int i = 0; i < stackTraceApplication.FrameCount; i++)
                    {
                        StackFrame frame = stackTraceApplication.GetFrame(i);
                        MethodBase method = frame.GetMethod();
                        if (method.DeclaringType.Name == "_SmallBasicProgram") result++;
                    }
                }
                if (null != stackTraceCurrent)
                {
                    for (int i = 0; i < stackTraceCurrent.FrameCount; i++)
                    {
                        StackFrame frame = stackTraceCurrent.GetFrame(i);
                        MethodBase method = frame.GetMethod();
                        if (method.DeclaringType.Name == "_SmallBasicProgram") result++;
                    }
                }
            }
            catch
            {
            }

            return result;
        }

        private static List<string> GetStack()
        {
            List<string> result = new List<string>();
            try
            {
                if (null != stackTraceCurrent)
                {
                    for (int i = 0; i < stackTraceCurrent.FrameCount; i++)
                    {
                        StackFrame frame = stackTraceCurrent.GetFrame(i);
                        MethodBase method = frame.GetMethod();
                        if (method.DeclaringType.Name == "_SmallBasicProgram") result.Add(method.Name);
                    }
                }
                if (null != stackTraceApplication)
                {
                    for (int i = 0; i < stackTraceApplication.FrameCount; i++)
                    {
                        StackFrame frame = stackTraceApplication.GetFrame(i);
                        MethodBase method = frame.GetMethod();
                        if (method.DeclaringType.Name == "_SmallBasicProgram") result.Add(method.Name);
                    }
                }
            }
            catch
            {
            }

            return result;
        }

        private static SortedDictionary<string, Primitive> GetVariables()
        {
            SortedDictionary<string, Primitive> result = new SortedDictionary<string, Primitive>();

            try
            {
                MethodBase method = GetMethodBase();
                if (null == method) return result;

                Type type = method.DeclaringType;
                FieldInfo[] fields = type.GetFields(BindingFlags.Static | BindingFlags.NonPublic);
                for (int i = 0; i < fields.Length; i++)
                {
                    result[fields[i].Name] = (Primitive)fields[i].GetValue(null);
                }
            }
            catch
            {
            }

            return result;
        }

        private static MethodBase GetMethodBase()
        {
            MethodBase method = null;
            try
            {
                if (null != stackTraceApplication)
                {
                    for (int i = 0; i < stackTraceApplication.FrameCount; i++)
                    {
                        StackFrame frame = stackTraceApplication.GetFrame(i);
                        method = frame.GetMethod();
                        if (method.DeclaringType.Name == "_SmallBasicProgram") break;
                    }
                }
                if (null != stackTraceCurrent)
                {
                    for (int i = 0; i < stackTraceCurrent.FrameCount; i++)
                    {
                        StackFrame frame = stackTraceCurrent.GetFrame(i);
                        method = frame.GetMethod();
                        if (method.DeclaringType.Name == "_SmallBasicProgram") break;
                    }
                }
            }
            catch (Exception ex)
            {
                //Send("DEBUG " + "GetMethodBase " + ex.Message);
            }

            if (method == null)
                method = methodBase;
            else
                methodBase = method;

            return method;
        }
    }

    class Watch
    {
        public string Variable = "";
        public string LessThan = "";
        public string GreaterThan = "";
        public string Equal = "";
        public string Changes = "";
        public bool bChanges = false;

        public bool Compare(string value)
        {
            Decimal val, Val;
            value = value.ToUpper();

            if (bChanges)
            {
                if (decimal.TryParse(value, out val) && decimal.TryParse(Changes, out Val))
                {
                    if (val != Val)
                    {
                        Changes = value;
                        return true;
                    }
                }
                else if (value.CompareTo(Changes) != 0)
                {
                    Changes = value;
                    return true;
                }
            }
            if (LessThan != "")
            {
                if (decimal.TryParse(value, out val) && decimal.TryParse(LessThan, out Val))
                {
                    if (val < Val) return true;
                }
                else if (value.CompareTo(LessThan) < 0)
                {
                    return true;
                }
            }
            if (GreaterThan != "")
            {
                if (decimal.TryParse(value, out val) && decimal.TryParse(GreaterThan, out Val))
                {
                    if (val > Val) return true;
                }
                else if (value.CompareTo(GreaterThan) > 0)
                {
                    return true;
                }
            }
            if (Equal != "")
            {
                if (decimal.TryParse(value, out val) && decimal.TryParse(Equal, out Val))
                {
                    if (val == Val) return true;
                }
                else if (value.CompareTo(Equal) == 0)
                {
                    return true;
                }
            }
            return false;
        }
    }
}