// Copyright (c) Andrew Robinson. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.


using System;
using System.Collections.Generic;
using System.Text;
using System.Data.SqlClient;
using System.Data;

namespace aaSQLToSplunk.Helpers
{
    /// <summary>
    /// Extension methods for reading different data types from streams.
    /// </summary>ring
    public static class DataTableExtensions
    {
        /// <summary>
        /// Render the datatable rows to multi-line Key-Value pair
        /// </summary>
        /// <param name="dataTable"></param>
        /// <returns></returns>
        public static string ToKVP(this DataTable dataTable, String additionalKVPValues = "", String timeStampField = "", string timestampFormat = "")
        {
            var returnValue = new StringBuilder();

            if(timeStampField.Length > 0 && timestampFormat.Length == 0)
            {
                throw new Exception("A timestamp format must be provided whenever a timestamp field is specified");
            }

            if (dataTable.Rows.Count > 0)
            {
                foreach (DataRow row in dataTable.Rows)
                {
                    foreach (DataColumn column in dataTable.Columns)
                    {
                        returnValue.AppendFormat("{0}=\"{1}\", ", column.ColumnName, row[column.Ordinal]);

                        // Add a timestamp field if this is the time column
                        if (column.ColumnName == timeStampField)
                        {
                            returnValue.AppendFormat("{0}=\"{1}\", ", "timestamp", DateTime.Parse(row[column.Ordinal].ToString()).ToString(timestampFormat));
                        }
                    }

                    // Append the additional value or trim the tailing ' ,'                    
                    if (additionalKVPValues.Length > 0)
                    {
                        returnValue.Append(additionalKVPValues);
                    }

                    returnValue.AppendLine();

                }
            }

            return returnValue.ToString();
        }
    }
}
