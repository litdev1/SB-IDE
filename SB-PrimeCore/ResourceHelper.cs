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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Windows;
using System.Xml.Linq;
using System.Xml.XPath;

namespace SB_Prime
{
	public static class ResourceHelper
	{
		private static Dictionary<string, string> stringResources;

		private static ResourceManager resourceManager;

		static ResourceHelper()
		{
			Assembly executingAssembly = Assembly.GetExecutingAssembly();
			ResourceHelper.resourceManager = new ResourceManager("SB_Prime.Strings", executingAssembly);
			string directoryName = Path.GetDirectoryName(executingAssembly.Location);
			CultureInfo currentUICulture = CultureInfo.CurrentUICulture;
			string text = Path.Combine(directoryName, string.Format(CultureInfo.InvariantCulture, "strings.{0}.resx", new object[]
			{
				currentUICulture.IetfLanguageTag
			}));
			if (!File.Exists(text))
			{
				text = Path.Combine(directoryName, string.Format(CultureInfo.InvariantCulture, "strings.{0}.resx", new object[]
				{
					currentUICulture.TwoLetterISOLanguageName
				}));
				if (!File.Exists(text))
				{
					text = Path.Combine(directoryName, string.Format(CultureInfo.InvariantCulture, "strings.{0}.resx", new object[]
					{
						(currentUICulture.Parent == null) ? currentUICulture.IetfLanguageTag : currentUICulture.Parent.IetfLanguageTag
					}));
					if (!File.Exists(text))
					{
						text = Path.Combine(directoryName, "strings.resx");
						if (!File.Exists(text))
						{
							return;
						}
					}
				}
			}
			IEnumerable<KeyValuePair<string, string>> source = ResourceHelper.FromResX(text);
			ResourceHelper.stringResources = source.ToDictionary((KeyValuePair<string, string> c) => c.Key, (KeyValuePair<string, string> c) => c.Value);
		}

		public static string GetString(string key)
		{
			string result = null;
			if (ResourceHelper.stringResources != null && ResourceHelper.stringResources.TryGetValue(key, out result))
			{
				return result;
			}
			string result2;
			try
			{
				result2 = ResourceHelper.resourceManager.GetString(key);
			}
			catch
			{
				MessageBox.Show(Properties.Strings.String56, "Small Basic", MessageBoxButton.OK, MessageBoxImage.Hand);
				Environment.Exit(-1);
				result2 = null;
			}
			return result2;
		}

		private static IEnumerable<KeyValuePair<string, string>> FromResX(string resxFileName)
		{
			return ResourceHelper.FromResX(new StreamReader(resxFileName));
		}

		private static IEnumerable<KeyValuePair<string, string>> FromResX(TextReader reader)
		{
			XDocument node = XDocument.Load(reader);
			return from xe in node.XPathSelectElements("/root/data")
				   let attributes = xe.Attributes()
				   let name =
					   from attribute in attributes
					   where attribute.Name.LocalName == "name"
					   select attribute.Value
				   let elements = xe.Elements()
				   let value =
					   from element in elements
					   where element.Name.LocalName == "value"
					   select element.Value
				   select new KeyValuePair<string, string>(name.First<string>(), value.First<string>());
		}
	}
}
