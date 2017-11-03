using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace SB_IDE
{
    public class SBInterop
    {
        public const int CurrentVersion = 6;

        object Service = null;
        MethodInfo SaveProgram = null;
        MethodInfo PublishProgramDetails = null;
        MethodInfo LoadProgram = null;
        MethodInfo CompileProgram = null;
        MethodInfo CompileVB = null;
        List<string> extensions = new List<string>();
#if DEBUG
        bool overwriteSBDebug = true;
#else
        bool overwriteSBDebug = false;
#endif
        public static string Language = "";
        public static int Version = 0;

        public SBInterop()
        {
            if (MainWindow.InstallDir == "")
            {
                MainWindow.InstallDir = Settings.GetValue("SBINSTALLATIONPATH");
                if (null == MainWindow.InstallDir || !Directory.Exists(MainWindow.InstallDir))
                {
                    if (IntPtr.Size == 8)
                    {
                        MainWindow.InstallDir = "C:\\Program Files (x86)\\Microsoft\\Small Basic";
                    }
                    else
                    {
                        MainWindow.InstallDir = "C:\\Program Files\\Microsoft\\Small Basic";
                    }
                }
            }

            LoadSB();
            LoadCompiler();
            CompileExtension(Properties.Resources.SBClient, "SBDebugger", overwriteSBDebug);
            LoadExtensions();
        }

        private void LoadSB()
        {
            try
            {
                Assembly assembly = Assembly.LoadFrom(MainWindow.InstallDir + "\\SB.exe");
                Type SBType = assembly.GetType("Microsoft.SmallBasic.com.smallbasic.Service");
                ConstructorInfo ctor = SBType.GetConstructor(Type.EmptyTypes);
                Service = ctor.Invoke(null);
                SaveProgram = SBType.GetMethod("SaveProgram");
                PublishProgramDetails = SBType.GetMethod("PublishProgramDetails");
                LoadProgram = SBType.GetMethod("LoadProgram");
            }
            catch (Exception ex)
            {
                MainWindow.Errors.Add(new Error("Load SB.exe : " + ex.Message));
            }
        }

        private void LoadCompiler()
        {
            try
            {
                Assembly assembly = Assembly.LoadFrom(MainWindow.InstallDir + "\\SmallBasicCompiler.exe");
                Type CompilerType = assembly.GetType("Microsoft.SmallBasic.Compiler");

                assembly = Assembly.LoadFrom(MainWindow.InstallDir + "\\LanguageService.dll");
                Type CSType = assembly.GetType("Microsoft.SmallBasic.LanguageService.CompilerService");
                MethodInfo[] methods = CSType.GetMethods();
                foreach (MethodInfo method in methods)
                {
                    if (method.Name == "Compile" && method.ReturnType == typeof(Boolean))
                    {
                        CompileProgram = method;
                    }
                    else if (method.Name == "Compile" && method.ReturnType == CompilerType)
                    {
                        CompileVB = method;
                    }
                }
            }
            catch (Exception ex)
            {
                MainWindow.Errors.Add(new Error("Load LanguageService.dll : " + ex.Message));
            }
        }

        private void LoadExtensions()
        {
            try
            {
                extensions.Clear();
                extensions.Add("\\SmallBasicLibrary");
                string path = MainWindow.InstallDir + "\\lib\\";
                string[] files = Directory.GetFiles(path, "*.dll");
                foreach (string file in files)
                {
                    if (file.EndsWith("SBDebugger.dll") || File.Exists(path + Path.GetFileNameWithoutExtension(file) + ".xml") || File.Exists(path + Path.GetFileNameWithoutExtension(file) + "."+Language+".xml"))
                    {
                        extensions.Add("\\lib\\"+ Path.GetFileNameWithoutExtension(file));
                    }
                }

                Assembly assembly = Assembly.LoadFrom(MainWindow.InstallDir + extensions[0] + ".dll");
                Type SmallBasicTypeAttribute = assembly.GetType("Microsoft.SmallBasic.Library.SmallBasicTypeAttribute");
                Type HideFromIntellisenseAttribute = assembly.GetType("Microsoft.SmallBasic.Library.HideFromIntellisenseAttribute");
                Type Primitive = assembly.GetType("Microsoft.SmallBasic.Library.Primitive");

                foreach (string extension in extensions)
                {
                    XmlDocument doc = new XmlDocument();
                    SBObject obj = null;
                    if (File.Exists(MainWindow.InstallDir + extension + "." + Language + ".xml"))
                    {
                        doc.Load(MainWindow.InstallDir + extension + "." + Language + ".xml");
                    }
                    else
                    {
                        doc.Load(MainWindow.InstallDir + extension + ".xml");
                    }

                    try
                    {
                        assembly = Assembly.LoadFrom(MainWindow.InstallDir + extension + ".dll");
                        Type[] types = assembly.GetTypes();
                        foreach (Type type in types)
                        {
                            if (type.IsPublic && type.IsDefined(SmallBasicTypeAttribute, false))
                            {
                                foreach (XmlNode xmlNode in doc.SelectNodes("/doc/members/member"))
                                {
                                    if (xmlNode.Attributes["name"].InnerText.Contains(type.FullName))
                                    {
                                        if (xmlNode.Attributes["name"].InnerText.StartsWith("T:"))
                                        {
                                            obj = new SBObject();
                                            SBObjects.objects.Add(obj);
                                            obj.name = type.Name;
                                            XmlNode node1 = xmlNode.FirstChild;
                                            if (node1.Name == "summary") obj.summary = node1.InnerText.Trim();
                                            continue;
                                        }

                                        MemberInfo[] memberInfos = type.GetMembers();
                                        foreach (MemberInfo memberInfo in memberInfos)
                                        {
                                            if (memberInfo.Name.StartsWith("add_") || memberInfo.Name.StartsWith("set_") || memberInfo.Name.StartsWith("get_")) continue;

                                            //MethodInfo methodInfo = (MethodInfo)memberInfo;
                                            string[] parts = memberInfo.ToString().Split(' ');
                                            string fullName = memberInfo.DeclaringType.FullName + ".";
                                            for (int i = 1; i < parts.Length; i++) fullName += parts[i];
                                            string xmlName = xmlNode.Attributes["name"].InnerText;
                                            if (!xmlName.EndsWith(")")) xmlName += "()";
                                            if (!xmlName.Contains(fullName)) continue;

                                            if (memberInfo.MemberType == MemberTypes.Method)
                                            {
                                                MethodInfo methodInfo = (MethodInfo)memberInfo;
                                                if (!methodInfo.IsPublic || !methodInfo.IsStatic || methodInfo.IsDefined(HideFromIntellisenseAttribute, false)) continue;
                                                if (methodInfo.ReturnType != Primitive && methodInfo.ReturnType != typeof(void)) continue;
                                                foreach (ParameterInfo parameterInfo in methodInfo.GetParameters())
                                                {
                                                    if (parameterInfo.GetType() != Primitive) continue;
                                                }
                                            }
                                            else if (memberInfo.MemberType == MemberTypes.Property)
                                            {
                                                if (((PropertyInfo)memberInfo).IsDefined(HideFromIntellisenseAttribute, false)) continue;
                                            }
                                            else if (memberInfo.MemberType == MemberTypes.Event)
                                            {
                                                if (((EventInfo)memberInfo).IsDefined(HideFromIntellisenseAttribute, false)) continue;
                                            }
                                            else
                                            {
                                                continue;
                                            }

                                            Member member = new Member();
                                            obj.members.Add(member);
                                            member.name = memberInfo.Name;
                                            member.type = memberInfo.MemberType;

                                            foreach (XmlNode node in xmlNode.ChildNodes)
                                            {
                                                switch (node.Name)
                                                {
                                                    case "summary":
                                                        member.summary = node.InnerText.Trim();
                                                        break;
                                                    case "param":
                                                        member.arguments[node.Attributes["name"].Value] = node.InnerText.Trim();
                                                        break;
                                                    case "returns":
                                                        member.returns = node.InnerText.Trim();
                                                        break;
                                                    case "example":
                                                        member.other[node.Name] = node.InnerText.Trim();
                                                        break;
                                                    case "remarks":
                                                        member.arguments[node.Name] = node.InnerText.Trim();
                                                        break;
                                                    case "value":
                                                        member.arguments[node.Name] = node.InnerText.Trim();
                                                        break;
                                                    default:
                                                        break;
                                                }
                                            }
                                            break;
                                        }
                                    }
                                }
                            }
                            if (null != obj) obj.members.Sort();
                        }
                    }
                    catch (Exception ex)
                    {
                        //MainWindow.Errors.Add("Load Extensions : " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                if (!ex.Message.Contains("SBDebugger"))
                {
                    MainWindow.Errors.Add(new Error("Load Extensions : " + ex.Message));
                }
            }
        }

        public string Publish(string program)
        {
            try
            { 
                string key = (string)SaveProgram.Invoke(Service, new object[] { "", program, "SBProgram" });
                if (key.ToUpper() != "ERROR")
                {
                    string result = (string)PublishProgramDetails.Invoke(Service, new object[] { key, "", "", "Miscellaneous" });
                }
                return key;
            }
            catch (Exception ex)
            {
                MainWindow.Errors.Add(new Error("Publish : " + ex.Message));
                return "";
            }
        }

        public string Import(string key)
        {
            try
            {
                string program = (string)LoadProgram.Invoke(Service, new object[] { key.Trim() });
                return program;
            }
            catch (Exception ex)
            {
                MainWindow.Errors.Add(new Error("Publish : " + ex.Message));
                return "";
            }
        }

        public void CompileExtension(string cs, string name, bool bOverwrite = false)
        {
            string tempPath = Path.GetDirectoryName(Path.GetTempFileName()) + "\\SBDebugger.dll";
            try
            {
                string extPath = MainWindow.InstallDir + "\\lib\\" + name + ".dll";
                string sblPath = MainWindow.InstallDir + "\\SmallBasicLibrary.dll";
                File.Delete(tempPath);

                if (File.Exists(extPath) && !bOverwrite)
                {
                    if (Version >= CurrentVersion) return;
                }

                MainWindow.Help();

                Assembly sblAssembly = Assembly.LoadFile(sblPath);
                var provider_options = new Dictionary<string, string>();
                provider_options["CompilerVersion"] = sblAssembly.ImageRuntimeVersion.Substring(0, 4);
                var provider = new Microsoft.CSharp.CSharpCodeProvider(provider_options);
                var compiler_params = new System.CodeDom.Compiler.CompilerParameters();
                compiler_params.OutputAssembly = tempPath;
                compiler_params.GenerateExecutable = false;
                compiler_params.ReferencedAssemblies.Add("System.dll");
                compiler_params.ReferencedAssemblies.Add("System.Net.Http.dll");
                compiler_params.ReferencedAssemblies.Add(sblPath);
                var results = provider.CompileAssemblyFromSource(compiler_params, cs);
                if (results.Errors.Count > 0)
                {
                    MainWindow.Errors.Add(new Error("Compile Extension : Compilation errors"));
                    for (int i = 0; i < results.Errors.Count; i++)
                    {
                        if (!results.Errors[i].IsWarning) MainWindow.Errors.Add(new Error("Compile Extension : "+ results.Errors[i].ErrorText));
                    }
                }

                try
                {
                    // Attempt to get a list of security permissions from the folder. 
                    // This will raise an exception if the path is read only or do not have access to view the permissions. 
                    System.Security.AccessControl.DirectorySecurity ds = Directory.GetAccessControl(MainWindow.InstallDir + "\\lib\\");
                    File.Copy(tempPath, extPath, true);
                }
                catch (UnauthorizedAccessException)
                {
                    string command = "del \"" + extPath + "\"";
                    command += " & move /Y \"" + tempPath + "\" \"" + extPath + "\"";
                    UACcommand(command);
                }

                Version = CurrentVersion;
            }
            catch (Exception ex)
            {
                MainWindow.Errors.Add(new Error("Compile Extension : " + ex.Message));
            }

            try
            {
                File.Delete(tempPath);
            }
            catch
            {

            }
        }

        public void CopyExtensions(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    foreach (string extension in extensions)
                    {
                        string from = MainWindow.InstallDir + extension + ".dll";
                        string to = path + "\\" + Path.GetFileNameWithoutExtension(extension) + ".dll";
                        File.Copy(from, to, true);
                    }
                }
            }
            catch (Exception ex)
            {
                MainWindow.Errors.Add(new Error("Copy Extensions : " + ex.Message));
            }
        }

        public string Compile(string fileName, bool debug)
        {
            try
            {
                string compiler = MainWindow.InstallDir + "\\SmallBasicCompiler.exe";
                Process process = new Process();
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = compiler;
                psi.Arguments = "\"" + Path.GetFileName(fileName) + "\"";
                psi.WorkingDirectory = Path.GetDirectoryName(fileName);
                psi.RedirectStandardOutput = true;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                process.StartInfo = psi;
                process.Start();
                string result = process.StandardOutput.ReadToEnd();

                string source = File.ReadAllText(fileName);
                string output = Path.ChangeExtension(fileName, ".exe");
                List<string> errors = new List<string>();

                if (!result.Contains("0 errors"))
                {
                    bool ok = (bool)CompileProgram.Invoke(null, new object[] { source, output, errors });
                    MainWindow.Errors.Add(new Error("Compile : " + "Errors were found"));
                    foreach (string error in errors)
                    {
                        string[] bits = error.Split(new char[] { ',', ':' });
                        int row = 0, col  = 0;
                        if (bits.Length > 2)
                        {
                            int.TryParse(bits[0], out row);
                            int.TryParse(bits[1], out col);
                            if (debug) row = (row - 1) / 2;
                            string message = "Compile : (row=" + row + ",col=" + col + ") ";
                            for (int i = 2; i < bits.Length; i++) message += bits[i];
                            MainWindow.Errors.Add(new Error(message) { Row = row, Col = col });
                        }
                        else
                        {
                            MainWindow.Errors.Add(new Error("Compile : " + error));
                        }
                        MainWindow.CompileError = true;
                    }
                    return "";
                }

                if (File.Exists(output) && DateTime.Now - File.GetLastWriteTime(output) < TimeSpan.FromMilliseconds(1000))
                {
                    MainWindow.Errors.Add(new Error("Compile : " + "0 Errors"));
                    return output;
                }
                MainWindow.Errors.Add(new Error("Compile : " + "Failed to create exe"));
                return "";
            }
            catch (Exception ex)
            {
                MainWindow.Errors.Add(new Error("Compile : " + ex.Message));
                return "";
            }
        }

        public string Graduate(string fileName, string projectName, string projectPath)
        {
            try
            {
                Assembly assembly = Assembly.LoadFrom(MainWindow.InstallDir + "\\SmallBasicCompiler.exe");
                Type CompilerType = assembly.GetType("Microsoft.SmallBasic.Compiler");

                List<string> errors = new List<string>();
                string source = File.ReadAllText(fileName);
                var Compiler = CompileVB.Invoke(null, new object[] { source, errors });
                if (errors.Count > 0)
                {
                    MainWindow.Errors.Add(new Error("Graduate : Compilation errors"));
                    for (int i = 0; i < errors.Count; i++)
                    {
                        MainWindow.Errors.Add(new Error("Graduate : " + errors[i]));
                    }
                    return "";
                }

                Type VisualBasicExporterType = assembly.GetType("Microsoft.SmallBasic.VisualBasicExporter");
                ConstructorInfo ctor = VisualBasicExporterType.GetConstructor(new Type[] { CompilerType });
                var VisualBasicExporter = ctor.Invoke(new object[] { Compiler });
                MethodInfo ExportToVisualBasicProject = VisualBasicExporterType.GetMethod("ExportToVisualBasicProject");

                return (string)ExportToVisualBasicProject.Invoke(VisualBasicExporter, new object[] { projectName, projectPath });
            }
            catch (Exception ex)
            {
                MainWindow.Errors.Add(new Error("Graduate : " + ex.Message));
                return "";
            }
        }

        public Process Run(string fileName)
        {
            try
            {
                Process process = new Process();
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = fileName;
                psi.WorkingDirectory = Path.GetDirectoryName(fileName);
                process.StartInfo = psi;
                process.Start();
                return process;
            }
            catch (Exception ex)
            {
                MainWindow.Errors.Add(new Error("Run : " + ex.Message));
                return null;
            }
        }

        public void UACcommand(string command)
        {
            var psi = new ProcessStartInfo();
            psi.UseShellExecute = true;
            psi.FileName = @"C:\Windows\System32\cmd.exe";
            psi.Arguments = "/c " + command;
            psi.Verb = "runas";
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            Process proc = new Process();
            proc.StartInfo = psi;

            try
            {
                proc.Start();
                proc.WaitForExit();
            }
            catch (Exception ex)
            {
                MainWindow.Errors.Add(new Error("UACcommand : " + ex.Message));
            }
        }
    }
}
