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
using System.Runtime.Versioning;
using System.Windows;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Metadata;
using System.Threading;
using System.Reflection.PortableExecutable;
using System.Collections;

namespace SB_Prime
{
    public class SBInterop
    {
        public const int CurrentVersion = 10;

        object Service = null;
        MethodInfo SaveProgram = null;
        MethodInfo PublishProgramDetails = null;
        MethodInfo GetProgramDetails = null;
        MethodInfo SubmitRating = null;
        MethodInfo LoadProgram = null;
        MethodInfo CompileProgram = null;
        Type CompilerType = null;
        MethodInfo CompileMethod = null;
        object Compiler = null;
        List<Assembly> assemblies = new List<Assembly>();

        List<string> extensions = new List<string>();
#if DEBUG
        bool overwriteSBDebug = true;
#else
        bool overwriteSBDebug = false;
#endif
        public static string Language = "";
        public static int Version = 0;

        public enum eVariant { SmallBasic, SmallVisualBasic }
        public static eVariant Variant = eVariant.SmallBasic;

        public SBInterop()
        {
            if (MainWindow.InstallDir == "")
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

            if (!Directory.Exists(MainWindow.InstallDir+"\\lib"))
            {
                MessageBox.Show(Properties.Strings.String57 + "\n\n" + MainWindow.InstallDir + "\\lib", "SB-Prime", MessageBoxButton.OK, MessageBoxImage.Information);
                string command = "mkdir \"" + MainWindow.InstallDir + "\\lib" + "\"";
                UACcommand(command);
            }

            LoadSB();
            LoadExtensions(MainWindow.loadExtensions);
            LoadCompiler();
            CompileExtension(Properties.Resources.SBClient, "SBDebugger", overwriteSBDebug);
        }

        private void LoadSB()
        {
            try
            {
                Assembly assembly = null;
                if (File.Exists(MainWindow.InstallDir + "\\SB.exe"))
                {
                    Variant = eVariant.SmallBasic;
                    assembly = Assembly.LoadFrom(MainWindow.InstallDir + "\\SB.exe");
                }
                else if (File.Exists(MainWindow.InstallDir + "\\sVB.exe"))
                {
                    Variant = eVariant.SmallVisualBasic;
                    assembly = Assembly.LoadFrom(MainWindow.InstallDir + "\\sVB.exe");
                }
                if (null == assembly)
                {
                    MainWindow.Errors.Add(new Error("Cannot find SB.exe or sVB.exe"));
                    return;
                }
                Type ServiceType = assembly.GetType("Microsoft." + Variant.ToString() + ".com.smallbasic.Service");
                ConstructorInfo ctor = ServiceType.GetConstructor(Type.EmptyTypes);
                Service = ctor.Invoke(null);
                SaveProgram = ServiceType.GetMethod("SaveProgram");
                PublishProgramDetails = ServiceType.GetMethod("PublishProgramDetails");
                GetProgramDetails = ServiceType.GetMethod("GetProgramDetails");
                SubmitRating = ServiceType.GetMethod("SubmitRating");
                LoadProgram = ServiceType.GetMethod("LoadProgram");
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
                Assembly assembly = null;
                Assembly.LoadFrom(MainWindow.InstallDir + "\\StringResources.dll");

                if (Variant == eVariant.SmallBasic)
                {
                    //assembly = Assembly.LoadFrom("C:\\Users\\steve\\Documents\\Visual Studio 2022\\Projects\\SmallBasicCompiler\\Test\\bin\\Debug\\SmallBasicCompiler.exe");
                    assembly = Assembly.LoadFrom(MainWindow.InstallDir + "\\SmallBasicCompiler.exe");
                    CompilerType = assembly.GetType("Microsoft." + Variant.ToString() + ".Compiler");
                    Compiler = Activator.CreateInstance(CompilerType);
                    CompileMethod = CompilerType.GetMethod("Compile", new Type[] { typeof(TextReader) });

                    try
                    {
                        MethodInfo methodInfo = CompilerType.GetMethod("AddAssemblyTypesToList", BindingFlags.NonPublic | BindingFlags.Instance);
                        FieldInfo fieldInfo = CompilerType.GetField("_libraryFiles", BindingFlags.NonPublic | BindingFlags.Instance);
                        List<string> _libraryFiles = (List<string>)fieldInfo.GetValue(Compiler);
                        foreach (Assembly _assembly in assemblies)
                        {
                            methodInfo.Invoke(Compiler, new object[] { _assembly });
                            _libraryFiles.Add(_assembly.Location);
                        }
                        fieldInfo.SetValue(Compiler, _libraryFiles);
                    }
                    catch
                    {

                    }
                }
                else if (Variant == eVariant.SmallVisualBasic)
                {
                    assembly = Assembly.LoadFrom(MainWindow.InstallDir + "\\sVBCompiler.exe");
                    CompilerType = assembly.GetType("Microsoft." + Variant.ToString() + ".Compiler");
                    Compiler = Activator.CreateInstance(CompilerType);
                    CompileMethod = CompilerType.GetMethod("Compile", new Type[] { typeof(TextReader), typeof(bool) });
                }
                if (null == assembly)
                {
                    MainWindow.Errors.Add(new Error("Cannot find SmallBasicCompiler.exe or sVBCompiler.exe"));
                    return;
                }
            }
            catch (Exception ex)
            {
                MainWindow.Errors.Add(new Error("Load LanguageService.dll : " + ex.Message));
            }
        }

        private void LoadExtensions(bool bLoadExtensions)
        {
            try
            {
                extensions.Clear();
                SBObjects.objects.Clear();
                extensions.Add("\\" + Variant.ToString() + "Library");
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
                Type SmallBasicTypeAttribute = assembly.GetType("Microsoft." + Variant.ToString() + ".Library." + Variant.ToString() + "TypeAttribute");
                Type HideFromIntellisenseAttribute = assembly.GetType("Microsoft." + Variant.ToString() + ".Library.HideFromIntellisenseAttribute");
                Type Primitive = assembly.GetType("Microsoft." + Variant.ToString() + ".Library.Primitive");

                try
                {
                    string tempPath = Path.GetTempPath();
                    string[] strings = Directory.GetFiles(tempPath, "*.sbprime");
                    foreach (string file in strings)
                    {
                        File.Delete(file);
                    }
                }
                catch
                {

                }

                foreach (string extension in extensions)
                {
                    if (extension.Contains("SBDebugger")) continue;
                    if (!bLoadExtensions && !extension.Contains(Variant.ToString() + "Library")) continue;

                    XmlDocument doc = new XmlDocument();
                    SBObject obj = null;
                    if (File.Exists(MainWindow.InstallDir + extension + "." + Language + ".xml"))
                    {
                        doc.Load(MainWindow.InstallDir + extension + "." + Language + ".xml");
                    }
                    else if (null != Properties.Strings.Culture && File.Exists(MainWindow.InstallDir + extension + "." + Properties.Strings.Culture.TwoLetterISOLanguageName + ".xml"))
                    {
                        doc.Load(MainWindow.InstallDir + extension + "." + Properties.Strings.Culture.TwoLetterISOLanguageName + ".xml");
                    }
                    else if (null != Properties.Strings.Culture && File.Exists(MainWindow.InstallDir + extension + "." + Properties.Strings.Culture.Name + ".xml"))
                    {
                        doc.Load(MainWindow.InstallDir + extension + "." + Properties.Strings.Culture.Name + ".xml");
                    }
                    else
                    {
                        doc.Load(MainWindow.InstallDir + extension + ".xml");
                    }

                    if (extension.Contains(Variant.ToString() + "Library"))
                    {
                        foreach (XmlNode xmlNode in doc.SelectNodes("/doc/members/member"))
                        {
                            if (xmlNode.Attributes["name"].InnerText.StartsWith("M:Microsoft." + Variant.ToString() + ".Library.Keywords."))
                            {
                                Member member = new Member();
                                SBObjects.keywords.Add(member);
                                member.name = xmlNode.Attributes["name"].InnerText.Substring(40);
                                int pos = member.name.IndexOf('.');
                                if (pos >= 0) member.name = member.name.Substring(pos + 1);
                                member.type = MemberTypes.Custom;
                                member.summary = "";

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
                            }
                        }
                    }

                    try
                    {
                        string tempFile = System.IO.Path.GetTempFileName();
                        File.Delete(tempFile);
                        tempFile = Path.ChangeExtension(tempFile, "sbprime");
                        File.Copy(MainWindow.InstallDir + extension + ".dll", tempFile);
                        //tempFile = MainWindow.InstallDir + extension + ".dll";
                        assembly = Assembly.LoadFrom(tempFile);
                        Type[] types = assembly.GetTypes();
                        foreach (Type type in types)
                        {
                            if (type.IsPublic && type.IsDefined(SmallBasicTypeAttribute, false) && !type.IsDefined(HideFromIntellisenseAttribute, false))
                            {
                                assemblies.Add(assembly);
                                obj = new SBObject();
                                SBObjects.objects.Add(obj);
                                obj.extension = extension.Split('\\').Last();
                                obj.name = type.Name;

                                MemberInfo[] memberInfos = type.GetMembers();
                                foreach (MemberInfo memberInfo in memberInfos)
                                {
                                    if (memberInfo.Name.StartsWith("add_") || memberInfo.Name.StartsWith("set_") || memberInfo.Name.StartsWith("get_")) continue;

                                    if (memberInfo.MemberType == MemberTypes.Method)
                                    {
                                        MethodInfo methodInfo = (MethodInfo)memberInfo;
                                        if (!methodInfo.IsPublic || !methodInfo.IsStatic || methodInfo.IsDefined(HideFromIntellisenseAttribute, false)) continue;
                                        if (methodInfo.ReturnType != Primitive && methodInfo.ReturnType != typeof(void)) continue;
                                        bool parmOK = true;
                                        foreach (ParameterInfo parameterInfo in methodInfo.GetParameters())
                                        {
                                            if (parameterInfo.ParameterType != Primitive) parmOK = false;
                                        }
                                        if (!parmOK) continue;
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
                                    member.summary = "";

                                    string[] array = memberInfo.ToString().Split(new char[] { ' ' });
                                    string dllName = memberInfo.DeclaringType.FullName + ".";
                                    for (int i = 1; i < array.Length; i++) dllName += array[i];
                                    if (dllName.EndsWith("()")) dllName = dllName.Substring(0, dllName.Length - 2);

                                    XmlNode node1 = null;
                                    foreach (XmlNode xmlNode in doc.SelectNodes("/doc/members/member"))
                                    {
                                        if (xmlNode.Attributes["name"].InnerText == "T:" + type.FullName)
                                        {
                                            node1 = xmlNode.FirstChild;
                                            if (node1.Name == "summary") obj.summary = node1.InnerText.Trim();
                                        }
                                    }
                                    foreach (XmlNode xmlNode in doc.SelectNodes("/doc/members/member"))
                                    {
                                        if (xmlNode.Attributes["name"].InnerText == "T:" + type.FullName) continue;
                                        else if (null != node1 && xmlNode.Attributes["name"].InnerText.EndsWith(dllName))
                                        {
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
                                        }
                                        else if (null != node1 && !xmlNode.Attributes["name"].InnerText.Contains(type.FullName))
                                        {
                                            //break;
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
                MainWindow.Errors.Add(new Error("Load Extensions : " + ex.Message));
            }
            SBObjects.objects.Sort();
        }

        public string Publish(string program, string baseID)
        {
            try
            { 
                string key = (string)SaveProgram.Invoke(Service, new object[] { "", FileFilter.Write(program), baseID });
                return key;
            }
            catch (Exception ex)
            {
                MainWindow.Errors.Add(new Error("Publish : " + ex.Message));
                return "error";
            }
        }

        public void SetDetails(string key, string title, string description, string category)
        {
            try
            {
                if (!string.IsNullOrEmpty(title) || !string.IsNullOrEmpty(description) || !string.IsNullOrEmpty(category))
                {
                    PublishProgramDetails.Invoke(Service, new object[] { key.Trim(), title, description, category });
                }
            }
            catch (Exception ex)
            {
                MainWindow.Errors.Add(new Error("SetDetails : " + ex.Message));
            }
        }

        public string Import(string key)
        {
            try
            {
                string program = (string)LoadProgram.Invoke(Service, new object[] { key.Trim() });
                return FileFilter.Read(program);
            }
            catch (Exception ex)
            {
                MainWindow.Errors.Add(new Error("Import : " + ex.Message));
                return "error";
            }
        }

        public object GetDetails(string key)
        {
            try
            {
                return GetProgramDetails.Invoke(Service, new object[] { key.Trim() });
            }
            catch (Exception ex)
            {
                MainWindow.Errors.Add(new Error("GetDetails : " + ex.Message));
                return null;
            }
        }

        public object SetRating(string key, double rating)
        {
            try
            {
                return SubmitRating.Invoke(Service, new object[] { key.Trim(), rating });
            }
            catch (Exception ex)
            {
                MainWindow.Errors.Add(new Error("SetRating : " + ex.Message));
                return null;
            }
        }

        public void CompileExtension(string cs, string name, bool bOverwrite = false)
        {
            if (Variant != eVariant.SmallBasic)
            {
                cs = cs.Replace(eVariant.SmallBasic.ToString(), Variant.ToString());
            }
            string tempPath = Path.GetTempFileName();
            File.Delete(tempPath);
            tempPath = Path.GetDirectoryName(tempPath) + "\\SBDebugger.dll";
            try
            {
                string extPath = MainWindow.InstallDir + "\\lib\\" + name + ".dll";
                string sblPath = MainWindow.InstallDir + "\\" + Variant.ToString() + "Library.dll";

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
                compiler_params.ReferencedAssemblies.Add("System.Runtime.InteropServices.dll");
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
                string source = File.ReadAllText(fileName);
                string output = Path.ChangeExtension(fileName, ".exe");
                if (File.Exists(output)) File.Delete(output);

                string compiler = MainWindow.InstallDir;
                if (Variant == eVariant.SmallBasic)
                {
                    compiler += "\\SmallBasicCompiler.exe";
                }
                else if (Variant == eVariant.SmallVisualBasic)
                {
                    compiler += "\\sVBCompiler.exe";
                }
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

                List<string> errors = new List<string>();

                if (!result.Contains("0 errors"))
                {
                    try
                    {
                        try
                        {
                            IList _errors = null;
                            if (Variant == eVariant.SmallBasic)
                            {
                                TextReader _source = new StringReader(source);
                                _errors = (IList)CompileMethod.Invoke(Compiler, new object[] { _source });
                            }
                            else if (Variant == eVariant.SmallVisualBasic)
                            {
                                TextReader _source = new StringReader(source);
                                _errors = (IList)CompileMethod.Invoke(Compiler, new object[] { _source, false });
                            }

                            foreach (var error in _errors)
                            {
                                errors.Add(error.ToString());
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message);
                            bool ok = (bool)CompileProgram.Invoke(null, new object[] { source, output, errors }); //doesn't find extensions
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message);
                        bool ok = (bool)CompileProgram.Invoke(null, new object[] { source, output, errors }); //doesn't find extensions
                    }
                    MainWindow.Errors.Add(new Error("Compile : " + "Errors were found"));
                    foreach (string error in errors)
                    {
                        string[] bits = error.Split(new char[] { ',', ':' });
                        int row = 0, col  = 0;
                        if (bits.Length > 2)
                        {
                            int.TryParse(bits[0], out row);
                            int.TryParse(bits[1], out col);
                            row++;
                            col++;
                            if (debug) row = (row + 1) / 2;
                            string message = "Compile : (row=" + row + ",col=" + col + ") ";
                            for (int i = 2; i < bits.Length; i++) message += bits[i];
                            MainWindow.Errors.Add(new Error(message) { Row = row, Col = col, Level = 1});
                        }
                        else
                        {
                            MainWindow.Errors.Add(new Error("Compile : " + error));
                        }
                        MainWindow.CompileError = true;
                    }
                    return "";
                }

                string pdb = Path.ChangeExtension(output, ".pdb");
                if (File.Exists(pdb))
                {
                    File.Delete(pdb);
                }

                if (File.Exists(output))
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
                List<string> errors = new List<string>();
                string source = File.ReadAllText(fileName);
                IList _errors = null;
                if (Variant == eVariant.SmallBasic)
                {
                    TextReader _source = new StringReader(source);
                    _errors = (IList)CompileMethod.Invoke(Compiler, new object[] { _source });
                }
                else if (Variant == eVariant.SmallVisualBasic)
                {
                    TextReader _source = new StringReader(source);
                    _errors = (IList)CompileMethod.Invoke(Compiler, new object[] { _source, false });
                }

                foreach (var error in _errors)
                {
                    errors.Add(error.ToString());
                }

                if (errors.Count > 0)
                {
                    MainWindow.Errors.Add(new Error("Graduate : Compilation errors"));
                    for (int i = 0; i < errors.Count; i++)
                    {
                        MainWindow.Errors.Add(new Error("Graduate : " + errors[i]));
                    }
                    return "";
                }

                Assembly assembly = null;
                if (Variant == eVariant.SmallBasic)
                {
                    assembly = Assembly.LoadFrom(MainWindow.InstallDir + "\\SmallBasicCompiler.exe");
                }
                else if (Variant == eVariant.SmallVisualBasic)
                {
                    assembly = Assembly.LoadFrom(MainWindow.InstallDir + "\\sVBCompiler.exe");
                    MainWindow.Errors.Add(new Error("Graduate : " + "Not supported for sVB"));
                    return "";
                }
                if (null == assembly)
                {
                    MainWindow.Errors.Add(new Error("Cannot find SmallBasicCompiler.exe or sVBCompiler.exe"));
                    return "";
                }
                Type VisualBasicExporterType = assembly.GetType("Microsoft." + Variant.ToString() + ".VisualBasicExporter");
                ConstructorInfo ctor = VisualBasicExporterType.GetConstructor(new Type[] { CompilerType });
                var VisualBasicExporter = ctor.Invoke(new object[] { Compiler });
                MethodInfo ExportToVisualBasicProject = VisualBasicExporterType.GetMethod("ExportToVisualBasicProject");

                string result = (string)ExportToVisualBasicProject.Invoke(VisualBasicExporter, new object[] { projectName, projectPath });

                // vbproj bugs
                string runtime;
                try
                {
                    string sblPath = MainWindow.InstallDir + "\\" + Variant.ToString() + "Library.dll";
                    Assembly sblAssembly = Assembly.LoadFile(sblPath);
                    TargetFrameworkAttribute attrib = (TargetFrameworkAttribute)sblAssembly.GetCustomAttribute(typeof(TargetFrameworkAttribute));
                    runtime = attrib.FrameworkName.Substring(attrib.FrameworkName.Length - 4);
                }
                catch
                {
                    runtime = "v4.5";
                }

                string vbproj = FileFilter.ReadAllText(result);
                vbproj = vbproj.Replace("<HintPath>$(programfiles)\\ (x86)\\Microsoft\\Small Basic\\SmallBasicLibrary.dll</HintPath>", "<HintPath>$(programfiles) (x86)\\Microsoft\\Small Basic\\SmallBasicLibrary.dll</HintPath>");
                vbproj = vbproj.Replace("<TargetFrameworkVersion>v3.5</TargetFrameworkVersion>", "<TargetFrameworkVersion>" + runtime + "</TargetFrameworkVersion>");
                //vcproj = vcproj.Replace("<Project ToolsVersion=\"3.5\" DefaultTargets=\"Build\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">", "<Project ToolsVersion=\"15.0\" DefaultTargets=\"Build\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">");
                FileFilter.WriteAllText(result, vbproj);

                return result;
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

        public bool Decomple(string targetProj, string source, bool bConsole)
        {
            targetProj = Environment.ExpandEnvironmentVariables(targetProj);
            source = Environment.ExpandEnvironmentVariables(source);
            string targetDirectory = Path.GetDirectoryName(targetProj);
            string targetName = Path.GetFileNameWithoutExtension(targetProj);
            bool bSuccess = false;

            try
            {
                using (FileStream fs = new FileStream(source, FileMode.Open, FileAccess.Read))
                {
                    WholeProjectDecompiler decompiler = new WholeProjectDecompiler();
                    PEFile moduleDefinition = new PEFile(source, fs, PEStreamOptions.PrefetchEntireImage);
                    CancellationToken cancellationToken = default(CancellationToken);
                    UniversalAssemblyResolver universalAssemblyResolver = new UniversalAssemblyResolver(source, false, moduleDefinition.Reader.DetectTargetFrameworkId(), PEStreamOptions.PrefetchEntireImage);
                    decompiler.AssemblyResolver = universalAssemblyResolver;
                    decompiler.DecompileProject(moduleDefinition, targetDirectory, cancellationToken);

                    // Set runtime to SB runtime
                    // Update case target name
                    // Copy and rename paths for dlls
                    string runtime;
                    try
                    {
                        string sblPath = MainWindow.InstallDir + "\\SmallBasicLibrary.dll";
                        Assembly sblAssembly = Assembly.LoadFile(sblPath);
                        TargetFrameworkAttribute attrib = (TargetFrameworkAttribute)sblAssembly.GetCustomAttribute(typeof(TargetFrameworkAttribute));
                        runtime = attrib.FrameworkName.Substring(attrib.FrameworkName.Length - 4);
                    }
                    catch
                    {
                        runtime = "v4.5";
                    }

                    string result = targetDirectory + "\\" + moduleDefinition.Name + ".csproj";
                    XmlDocument doc = new XmlDocument();
                    doc.Load(result);
                    XmlNodeList elemList = doc.GetElementsByTagName("AssemblyName");
                    if (elemList.Count == 1 && elemList[0].InnerText == moduleDefinition.Name) elemList[0].InnerText = targetName;
                    elemList = doc.GetElementsByTagName("TargetFrameworkVersion");
                    if (elemList.Count == 1) elemList[0].InnerText = runtime;
                    elemList = doc.GetElementsByTagName("OutputType");
                    if (bConsole && elemList.Count == 1 && elemList[0].InnerText == "WinExe") elemList[0].InnerText = "Exe";
                    elemList = doc.GetElementsByTagName("HintPath");
                    foreach (XmlNode xmlNode in elemList)
                    {
                        if (xmlNode.InnerText.EndsWith(".dll"))
                        {
                            string dll = targetDirectory + "\\" + Path.GetFileName(xmlNode.InnerText);
                            File.Copy(xmlNode.InnerText, dll);
                            xmlNode.InnerText = dll;
                        }
                    }
                    File.Delete(result);
                    doc.Save(targetProj);

                    //Set Main
                    string projectFile = targetDirectory + "\\_SmallBasicProgram.cs";
                    if (File.Exists(projectFile))
                    {
                        string prog = File.ReadAllText(projectFile);
                        prog = prog.Replace("static void _Main()", "static void Main()");
                        if (bConsole)
                        {
                            prog = prog.Replace("SmallBasicApplication.BeginProgram();", "SmallBasicApplication.BeginProgram();\r\n\t\t//Initialise and hide TextWindow for Console App\r\n\t\tTextWindow.Show();\r\n\t\tTextWindow.Hide();");
                        }
                        File.WriteAllText(projectFile, prog);
                    }
                }

                MainWindow.Errors.Add(new Error("Decompile : " + Properties.Strings.String69 + " " + targetProj));
                bSuccess = true;
            }
            catch (Exception ex)
            {
                MainWindow.Errors.Add(new Error("Decomple : " + ex.Message));
                bSuccess = false;
            }
            //Start VS or open containing folder
            //if (File.Exists(targetProj)) Process.Start(targetProj);
            //else if (Directory.Exists(targetDirectory)) Process.Start("explorer.exe", "\"" + targetDirectory + "\"");
            if (Directory.Exists(targetDirectory)) Process.Start("explorer.exe", "\"" + targetDirectory + "\"");
            return bSuccess;
        }
    }
}
