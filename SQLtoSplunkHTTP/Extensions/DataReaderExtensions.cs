// Copyright (c) Andrew Robinson. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Data.SqlClient;
using System.Data;

namespace SQLtoSplunkHTTP.Helpers
{
    /// <summary>
    /// Extension methods for reading different data types from streams.
    /// </summary>ring
    public static class DataReaderExtensions
    {
		/// <summary>
        /// Get the list of columns as a dictionary with ordinal and name
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
		public static Dictionary<int,string> GetColumns(this SqlDataReader reader)
        {
            var schemaTable = reader.GetSchemaTable();
            var schemaDict = new Dictionary<int, string>();

            //For each field in the table...
            foreach (DataRow field in schemaTable.Rows)
            {
                schemaDict.Add((int)field["ColumnOrdinal"], (string)field["ColumnName"]);
            }

            return schemaDict;
        }

		/// <summary>
        /// Render the records to multi-line Key-Value pair
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        public static string ToKVP(this SqlDataReader reader)
        {
            var returnValue = new StringBuilder();
            var columnsDictionary = reader.GetColumns();
				
           //var dict = reader.GetSchemaTable().Select(r => r["ColumnName"].ToString()).ToDictionary(cn => cn, 

            if (reader.HasRows)
            {
				while(reader.Read())
                {
					foreach(int ordinal in columnsDictionary.Keys)
                    {
                        returnValue.AppendFormat("{0}=\"{1}\", ",columnsDictionary[ordinal],reader.GetValue(ordinal));
                    }

                    // Trim the trailing ,
                    returnValue.Remove(returnValue.Length - 2,2);

                    returnValue.AppendLine();
                }
            }

            return returnValue.ToString();
        }
    }
}
