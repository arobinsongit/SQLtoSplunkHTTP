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
        public ulong MaxUnreadRecords = 1000;
        public string SQLConnectionString = "";
        public string SQLQuery = "";
        public string SplunkBaseAddress = "http://localhost:8088";
        public string AuthorizationToken = "0000-0000-0000-0000-0000";
        public Guid ClientID = Guid.NewGuid();
    }
}
