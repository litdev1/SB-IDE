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

using ICSharpCode.Decompiler.TypeSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SB_Prime
{
    public class SBObjects
    {
        public static List<SBObject> objects = new List<SBObject>();
        public static List<Member> keywords = new List<Member>(); // { "Sub", "EndSub", "For", "To", "Step", "EndFor", "If", "Then", "Else", "ElseIf", "EndIf", "While", "EndWhile", "Goto" };
        public List<string> variables = new List<string>();
        public List<string> subroutines = new List<string>();
        public List<string> labels = new List<string>();

        public string GetKeywords(string input)
        {
            string result = "";
            List<string> data = new List<string>();
            foreach (Member member in keywords)
            {
                //if (member.name.IndexOf(input, StringComparison.OrdinalIgnoreCase) >= 0 || member.name.ToUpperInvariant().Contains(input.ToUpperInvariant()))
                if (member.name.StartsWith(input, StringComparison.OrdinalIgnoreCase) || member.name.ToUpperInvariant().StartsWith(input.ToUpperInvariant()))
                {
                    data.Add(member.name + "?0");
                }
            }
            data = data.Distinct().ToList();
            data.Sort();
            foreach (string value in data)
            {
                result += value + " ";
            }
            return result;
        }

        public string GetObjects(string input)
        {
            string result = "";
            List<string> data = new List<string>();
            foreach (SBObject label in objects)
            {
                string name = "";
                if (FileFilter.EnableAliases && FileFilter.Aliases.TryGetValue(label.name, out name))
                {
                    //if (name.IndexOf(input, StringComparison.OrdinalIgnoreCase) >= 0 || name.ToUpperInvariant().Contains(input.ToUpperInvariant()))
                    if (name.StartsWith(input, StringComparison.OrdinalIgnoreCase) || name.ToUpperInvariant().StartsWith(input.ToUpperInvariant()))
                    {
                        data.Add(name + "?1");
                        continue;
                    }
                }
                name = label.name;
                //if (name.IndexOf(input, StringComparison.OrdinalIgnoreCase) >= 0 || name.ToUpperInvariant().Contains(input.ToUpperInvariant()))
                if (name.StartsWith(input, StringComparison.OrdinalIgnoreCase) || name.ToUpperInvariant().StartsWith(input.ToUpperInvariant()))
                {
                    data.Add(name + "?1");
                }
            }
            data = data.Distinct().ToList();
            data.Sort();
            foreach (string value in data)
            {
                result += value + " ";
            }
            return result;
        }

        public string GetMembers(string obj, string input)
        {
            string result = "";
            input = input.ToUpperInvariant();
            List<string> data = new List<string>();
            foreach (SBObject label in objects)
            {
                string labelName = "";
                if (!FileFilter.EnableAliases || !FileFilter.Aliases.TryGetValue(label.name, out labelName))
                {
                    labelName = label.name;
                }
                if (obj.ToUpperInvariant() == labelName.ToUpperInvariant())
                {
                    foreach (Member member in label.members)
                    {
                        string memberName = "";
                        if (!FileFilter.EnableAliases || !FileFilter.Aliases.TryGetValue(member.name, out memberName))
                        {
                            memberName = member.name;
                        }
                        //if (memberName.IndexOf(input, StringComparison.OrdinalIgnoreCase) >= 0 || memberName.ToUpperInvariant().Contains(input.ToUpperInvariant()))
                        if (memberName.StartsWith(input, StringComparison.OrdinalIgnoreCase) || memberName.ToUpperInvariant().StartsWith(input.ToUpperInvariant()))
                        {
                            switch (member.type)
                            {
                                case MemberTypes.Method:
                                    data.Add(memberName + "?2");
                                    break;
                                case MemberTypes.Property:
                                    data.Add(memberName + "?3");
                                    break;
                                case MemberTypes.Event:
                                    data.Add(memberName + "?4");
                                    break;
                            }
                        }
                    }
                    break;
                }
            }
            data = data.Distinct().ToList();
            //data.Sort();
            foreach (string value in data)
            {
                result += value + " ";
            }
            return result;
        }

        public string GetVariables(string input)
        {
            string result = "";
            List<string> data = new List<string>();
            foreach (string label in variables)
            {
                //if (label.IndexOf(input, StringComparison.OrdinalIgnoreCase) >= 0 || label.ToUpperInvariant().Contains(input.ToUpperInvariant()))
                if (label.StartsWith(input, StringComparison.OrdinalIgnoreCase) || label.ToUpperInvariant().StartsWith(input.ToUpperInvariant()))
                {
                    data.Add(label + "?5");
                }
            }
            data = data.Distinct().ToList();
            data.Sort();
            foreach (string value in data)
            {
                result += value + " ";
            }
            return result;
        }

        public string GetSubroutines(string input)
        {
            string result = "";
            List<string> data = new List<string>();
            foreach (string label in subroutines)
            {
                //if (label.IndexOf(input, StringComparison.OrdinalIgnoreCase) >= 0 || label.ToUpperInvariant().Contains(input.ToUpperInvariant()))
                if (label.StartsWith(input, StringComparison.OrdinalIgnoreCase) || label.ToUpperInvariant().StartsWith(input.ToUpperInvariant()))
                {
                    data.Add(label + "?6");
                }
            }
            data = data.Distinct().ToList();
            data.Sort();
            foreach (string value in data)
            {
                result += value + " ";
            }
            return result;
        }

        public string GetLabels(string input)
        {
            string result = "";
            List<string> data = new List<string>();
            foreach (string label in labels)
            {
                //if (label.IndexOf(input, StringComparison.OrdinalIgnoreCase) >= 0 || label.ToUpperInvariant().Contains(input.ToUpperInvariant()))
                if (label.StartsWith(input, StringComparison.OrdinalIgnoreCase) || label.ToUpperInvariant().StartsWith(input.ToUpperInvariant()))
                {
                    data.Add(label + "?7");
                }
            }
            data = data.Distinct().ToList();
            data.Sort();
            foreach (string value in data)
            {
                result += value + " ";
            }
            return result;
        }
    }

    public class SBObject : IComparable
    {
        public string extension = "";
        public string name = "";
        public string summary = "";
        public List<Member> members = new List<Member>();

        public int CompareTo(object obj)
        {
            SBObject _obj = (SBObject)obj;
            if (extension == _obj.extension) return name.CompareTo(_obj.name);
            else return extension.CompareTo(_obj.extension);
        }
    }

    public class Member : IComparable
    {
        public string name = "";
        public MemberTypes type;
        public string summary = "";
        public Dictionary<string, string> arguments = new Dictionary<string, string>();
        public string returns = "";
        public Dictionary<string, string> other = new Dictionary<string, string>();

        public int CompareTo(object obj)
        {
            Member member = (Member)obj;
            if (type == member.type) return name.CompareTo(member.name);
            else return -type.CompareTo(member.type);
        }
    }
}
