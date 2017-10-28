using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SB_IDE
{
    public class SBObjects
    {
        public static List<SBObject> objects = new List<SBObject>();
        public static List<string> keywords = new List<string>() { "Sub", "EndSub", "For", "To", "Step", "EndFor", "If", "Then", "Else", "ElseIf", "EndIf", "While", "EndWhile", "Goto" };
        public List<string> variables = new List<string>();
        public List<string> subroutines = new List<string>();
        public List<string> labels = new List<string>();

        public string GetKeywords(string input)
        {
            string result = "";
            List<string> data = new List<string>();
            foreach (string label in keywords)
            {
                if (label.ToUpper().StartsWith(input.ToUpper()))
                {
                    data.Add(label + "?0");
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
                if (label.name.ToUpper().StartsWith(input.ToUpper()))
                {
                    data.Add(label.name + "?1");
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
            input = input.ToUpper();
            List<string> data = new List<string>();
            foreach (SBObject label in objects)
            {
                if (obj.ToUpper() == label.name.ToUpper())
                {
                    foreach (Member member in label.members)
                    {
                        if (member.name.ToUpper().StartsWith(input.ToUpper()))
                        {
                            switch (member.type)
                            {
                                case MemberTypes.Method:
                                    data.Add(member.name + "?2");
                                    break;
                                case MemberTypes.Property:
                                    data.Add(member.name + "?3");
                                    break;
                                case MemberTypes.Event:
                                    data.Add(member.name + "?4");
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
                if (label.ToUpper().StartsWith(input.ToUpper()))
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
                if (label.ToUpper().StartsWith(input.ToUpper()))
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
                if (label.ToUpper().StartsWith(input.ToUpper()))
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

    public class SBObject
    {
        public string name;
        public string summary;
        public List<Member> members = new List<Member>();
    }

    public class Member : IComparable
    {
        public string name;
        public MemberTypes type;
        public string summary;
        public Dictionary<string, string> arguments = new Dictionary<string, string>();
        public string returns;
        public Dictionary<string, string> other = new Dictionary<string, string>();

        public int CompareTo(object obj)
        {
            Member member = (Member)obj;
            if (type == member.type) return name.CompareTo(member.name);
            else return -type.CompareTo(member.type);
        }
    }
}
