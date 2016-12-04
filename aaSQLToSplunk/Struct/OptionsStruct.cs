using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace aaSQLToSplunk
{
    public class OptionsStruct
    {
        public int ReadInterval = 5000;
        public int MaximumReadInterval = 50000;
        public ulong MaxRecords = 1000;
        public string SQLConnectionString;
        public string SQLQuery;
        public string SQLSequenceField;
        public string SQLSequenceFieldDefaultValue;
        public string SQLTimestampField;
        public bool SQLTimeStampIsUTC = false;
        public bool CacheWriteValueIsUTCTimestamp = false;
        public int CacheWriteValueTimestampMillisecondsAdd = 1;
        public string CacheWriteValueStringFormat = "";  // Any legal format that can be used with string.Format
        public string SQLOrderByClause = " ORDER by {{ SQLSequenceField}} DESC";
        public string SQLWhereClause = " WHERE {{SQLSequenceField}} > {{LastSQLSequenceFieldValue}}";
        public string SplunkBaseAddress = "http://localhost:8088";
        public string SplunkAuthorizationToken;
        public Guid SplunkClientID = Guid.NewGuid();
        public bool SplunkIgnoreSSLErrors = false; // TODO
        public string SplunkSourceHost = Environment.MachineName;
        public string SplunkSourceData;
        public string SplunkEventTimestampFormat = "yyyy-MM-dd HH:mm:ss.ffffff zz";
        public string CacheFilename;
    }
}