/*************************************************************************************\
File Name  :  ConfigUtil.cs
Project    :  MSSQL Compressed Backup

Copyright 2009 Clay Lenhart <clay@lenharts.net>

This file is part of MSSQL Compressed Backup.

MSSQL Compressed Backup is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

MSSQL Compressed Backup is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with MSSQL Compressed Backup.  If not, see <http://www.gnu.org/licenses/>.

THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND, 
EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE IMPLIED 
WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR PURPOSE.
\*************************************************************************************/

using System;
using System.Collections.Generic;
using MSBackupPipe.Common;

namespace MSBackupPipe.Cmd
{
    internal static class ConfigUtil
    {
        public static ConfigPair ParseComponentConfig(Dictionary<string, Type> pipelineComponents, string componentString)
        {
            var config = new ConfigPair();

            string componentName;
            string configString;

            var pPos = componentString.IndexOf('(');
            if (pPos < 0)
            {
                componentName = componentString;
                configString = "";
            }
            else
            {
                componentName = componentString.Substring(0, pPos).Trim();

                if (componentString.Substring(componentString.Length - 1, 1) != ")")
                {
                    throw new ArgumentException(string.Format("Invalid pipeline.  The closing parenthesis not found: {0}", componentString));
                }

                configString = componentString.Substring(pPos + 1, componentString.Length - pPos - 2);
            }

            Type foundType;
            if (pipelineComponents.TryGetValue(componentName.ToLowerInvariant(), out foundType))
            {
                config.Parameters = ParseArrayConfig(configString);
                config.TransformationType = foundType;
            }
            else
            {
                throw new ArgumentException(string.Format("Plugin not found: {0}", componentName));
            }
            return config;
        }

        private static Dictionary<string, List<string>> ParseArrayConfig(string s)
        {
            var pairs = SplitNameValues(s);

            var result = new Dictionary<string, List<string>>(StringComparer.InvariantCultureIgnoreCase);

            foreach (var pair in pairs)
            {
                if (string.IsNullOrEmpty(pair)) continue;
                var nameValue = pair.Split(new[] { '=' }, 2);
                var name = nameValue[0].Trim();
                var val = nameValue.Length > 1 ? nameValue[1].Trim() : null;

                name = name.Replace(";;", ";");

                if (val != null)
                {
                    val = val.Replace(";;", ";");
                }

                if (result.ContainsKey(name))
                {
                    result[name].Add(val);
                }
                else
                {
                    result.Add(name, new List<string>(new[] { val }));
                }
            }

            return result;
        }

        private static IEnumerable<string> SplitNameValues(string s)
        {
            // separated by semicolons ";", however ignores ";;"

            var pos = 0;

            while (true)
            {
                var nextSemiPos = FindNextSemiPos(s, pos);
                if (nextSemiPos < 0)
                {
                    if (s.Length > pos)
                    {
                        yield return s.Substring(pos);
                    }

                    yield break;
                }

                yield return s.Substring(pos, nextSemiPos - pos);

                pos = nextSemiPos + 1;
             
            }
        }

        /// <summary>
        ///  Finds the next semicolon but ignores ";;"
        /// </summary>
        /// <param name="s"></param>
        /// <param name="startIndex">should be the position after a semicolon</param>
        /// <returns>-1 if there are no more semicolons</returns>
        private static int FindNextSemiPos(string s, int startIndex)
        {
            if (startIndex >= s.Length)
            {
                return -1;
            }

            var nextSemi = s.IndexOf(';', startIndex);
            if (nextSemi < 0)
            {
                return -1;
            }

            if (s.Length >= nextSemi + 2 && s[nextSemi + 1] == ';')
            {
                return FindNextSemiPos(s, nextSemi + 2);
            }

            return nextSemi;
        }
    }
}
