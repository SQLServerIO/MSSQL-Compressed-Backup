/*************************************************************************************\
File Name  :  ParameterInfo.cs
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
using System.Text;

namespace MSBackupPipe.StdPlugins
{
    public class ParameterInfo
    {
        private bool mAllowMultipleValues;
        private bool mIsRequired;

        public ParameterInfo()
        {
        }

        public ParameterInfo(bool allowMultipleValues, bool isRequired)
        {
            mAllowMultipleValues = allowMultipleValues;
            mIsRequired = IsRequired;
        }

        public bool AllowMultipleValues { 
            get { return mAllowMultipleValues; }
            internal set { mAllowMultipleValues = value; } 
        }
        public bool IsRequired {
            get { return mIsRequired; }
            internal set { mIsRequired = value; }
        }

        internal static void ValidateParams(Dictionary<string, ParameterInfo> paramSchema, Dictionary<string, List<string>> config)
        {
            if (config.Comparer != StringComparer.InvariantCultureIgnoreCase)
            {
                throw new ArgumentException(string.Format("Programming error: The config dictionary must be initialized with StringComparer.InvariantCultureIgnoreCase."));
            }

            foreach (string optionName in config.Keys)
            {
                ParameterInfo paramInfo;
                if (!paramSchema.TryGetValue(optionName, out paramInfo))
                {
                    throw new ArgumentException(string.Format("The parameter, {0}, is not a valid option.", optionName));
                }
                else
                {
                    List<string> optionValues = config[optionName];

                    if (optionValues == null || optionValues.Count == 0)
                    {
                        throw new ArgumentException(string.Format("Programming error: The parameter, {0}, cannot be null or empty.", optionName));
                    }

                    if (!paramInfo.AllowMultipleValues)
                    {
                        if (optionValues.Count > 1)
                        {
                            throw new ArgumentException(string.Format("The parameter, {0}, must be specified only once.", optionName));
                        }
                    }
                }
            }

            foreach (string schemaParam in paramSchema.Keys)
            {
                if (paramSchema[schemaParam].IsRequired)
                {
                    if (!config.ContainsKey(schemaParam))
                    {
                        throw new ArgumentException(string.Format("The parameter, {0}, is required.", schemaParam));
                    }
                }
            }
        }
    }
}
