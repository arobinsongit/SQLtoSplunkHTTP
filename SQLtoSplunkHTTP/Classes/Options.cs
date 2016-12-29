// Copyright (c) Andrew Robinson. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using System.ComponentModel;

namespace SQLtoSplunkHTTP
{
    /// <summary>
    /// The otpions to be
    /// </summary>
    public class Options
    {
        [Description("Time interval in milliseconds to read data from server")]
        public int ReadInterval = 5000;

        [Description("Maximum interval used when backoff timer slows down due to bad connections")]
        public int MaximumReadInterval = 50000;

        [Description("Maximum number of records to retrieve in a single read of the database when use the {{MaxRecords}} element in the base query.  If the available records based on cache data exceeds this value, then only the first XX records as sorted in descending order according to the sequence field will be retrieved.")]    
        public ulong MaxRecords = 1000;

        
        [Description("Standard Microsoft SQL Server connection string")]
        public string SQLConnectionString;

        
        [Description("First portion of query containing SELECT and FROM statements")]
        public string SQLQuery;

        
        [Description("Name of field used to indicate relative sequence for rows in the table")]
        public string SQLSequenceField;

        
        [Description("Default value to specify in where clause in the event no sequence field value is available")]
        public string SQLSequenceFieldDefaultValue;

        [Description("Specific field used when creating KVP for splunk that will indicate the timestamp of the event.  If this field is not specified then a distinct timestamp field will not be included in the event")]
        public string SQLTimestampField = "";

        [Description("Indication that the timestamp field is in UTC as opposed to local time")]
        public bool SQLTimeStampIsUTC = false;

        [Description("Indication that cache value is in UTC as opposed to local time ")]
        public bool CacheWriteValueIsUTCTimestamp = false;

        [Description("Depending on the precision of the sequence field it might be necessary to add a small increment to the cache value to avoid rereading the same data record on subsequent scans")]
        public int CacheWriteValueTimestampMillisecondsAdd = 1;

        [Description("Any legal format that can be used with the string.format - this will typically be used for timestamps")]
        public string CacheWriteValueStringFormat = "";  // Any legal format that can be used with string.Format

        [Description("Order By Clause that will substitute the SQLSequenceField as required")]
        public string SQLOrderByClause = " ORDER by {{SQLSequenceField}} DESC";

        [Description("Where Clause that will substitute the SQLSequenceField and LastSQLSequenceFieldValue as required")]
        public string SQLWhereClause = " WHERE {{SQLSequenceField}} > {{LastSQLSequenceFieldValue}}";

        [Description("Base HTTP address including port number")]
        public string SplunkBaseAddress = "http://localhost:8088";

        [Description("HTTP event collector authorization token")]
        public string SplunkAuthorizationToken;
        
        [Description("Unique client ID - will be generated automatically in the event one is not specified")]
        public Guid SplunkClientID = Guid.NewGuid();

        [Description("Ignore SSL errors")]
        public bool SplunkIgnoreSSLErrors = false; // TODO

        [Description("Name of the host that is the source of data - defaults to current machine name")]
        public string SplunkSourceHost = Environment.MachineName;

        [Description("Unique name of the data source that can be used for searching within Splunk")]
        public string SplunkSourceData;

        [Description("Timestamp format for writing event timestamp to Splunk - can be any legal format that can be used with string.format")]
        public string SplunkEventTimestampFormat = "yyyy-MM-dd HH:mm:ss.ffffff zz";
        public string CacheFilename;
    }
}