using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SB_Prime
{
    public static class FileFilter
    {
        private static Dictionary<string, string> aliases = new Dictionary<string, string>();
        private static Dictionary<string, string> allAliases = new Dictionary<string, string>();
        public static bool EnableAliases = true;

        public static Dictionary<string, string> Aliases
        {
            get
            {
                return aliases;
            }
        }

        public static Dictionary<string, string> AllAliases
        {
            get
            {
                return allAliases;
            }
        }

        public static void WriteAllText(string path, string contents)
        {
            File.WriteAllText(path, Write(contents));
        }

        public static void WriteAllLines(string path, string[] contents)
        {
            File.WriteAllLines(path, Write(contents));
        }

        public static void WriteAllLines(string path, List<string> contents)
        {
            File.WriteAllLines(path, Write(contents));
        }

        public static string ReadAllText(string path)
        {
            return Read(File.ReadAllText(path));
        }

        public static string[] ReadAllLines(string path)
        {
            return Read(File.ReadAllLines(path));
        }

        public static IEnumerable<string> ReadLines(string path)
        {
            return Read(File.ReadLines(path));
        }

        public static string Write(string text, bool appendDot = true)
        {
            string result = text;
            if (EnableAliases)
            {
                if (appendDot)
                {
                    foreach (KeyValuePair<string, string> kvp in Aliases)
                    {
                        result = Regex.Replace(result, @"\b" + kvp.Value + @"\.", kvp.Key + @".", RegexOptions.IgnoreCase);
                        result = Regex.Replace(result, @"\." + kvp.Value + @"\b", @"." + kvp.Key, RegexOptions.IgnoreCase);
                    }
                }
                else
                {
                    foreach (KeyValuePair<string, string> kvp in Aliases)
                    {
                        result = Regex.Replace(result, @"\b" + kvp.Value + @"\b", kvp.Key, RegexOptions.IgnoreCase);
                    }
                }
            }
            return result;
        }

        private static string[] Write(string[] text)
        {
            string[] result = text;
            foreach (KeyValuePair<string, string> kvp in Aliases)
            {
                for (int i = 0; i < result.Length; i++)
                {
                    result[i] = Write(result[i]);
                }
            }
            return result;
        }

        private static List<string> Write(List<string> text)
        {
            List<string> result = text;
            foreach (KeyValuePair<string, string> kvp in Aliases)
            {
                for (int i = 0; i < result.Count; i++)
                {
                    result[i] = Write(result[i]);
                }
            }
            return result;
        }

        public static string Read(string text, bool appendDot = true)
        {
            string result = text;
            if (EnableAliases)
            {
                if (appendDot)
                {
                    foreach (KeyValuePair<string, string> kvp in Aliases)
                    {
                        result = Regex.Replace(result, @"\b" + kvp.Key + @"\.", kvp.Value + @".", RegexOptions.IgnoreCase);
                        result = Regex.Replace(result, @"\." + kvp.Key + @"\b", @"." + kvp.Value, RegexOptions.IgnoreCase);
                    }
                }
                else
                {
                    foreach (KeyValuePair<string, string> kvp in Aliases)
                    {
                        result = Regex.Replace(result, @"\b" + kvp.Key + @"\b", kvp.Value, RegexOptions.IgnoreCase);
                    }
                }
            }
            return result;
        }

        private static string[] Read(string[] text)
        {
            string[] result = text;
            foreach (KeyValuePair<string, string> kvp in Aliases)
            {
                for (int i = 0; i < result.Length; i++)
                {
                    result[i] = Read(result[i]);
                }
            }
            return result;
        }

        private static IEnumerable<string> Read(IEnumerable<string> text)
        {
            List<string> result = text.ToList();
            foreach (KeyValuePair<string, string> kvp in Aliases)
            {
                for (int i = 0; i < result.Count; i++)
                {
                    result[i] = Read(result[i]);
                }
            }
            return result;
        }
    }
}
