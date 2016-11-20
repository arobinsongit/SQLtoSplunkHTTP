using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Timers;

[assembly: log4net.Config.XmlConfigurator(ConfigFile = "log.config", Watch = true)]
namespace aaSQLToSplunk
{
    class Program
    {
        #region Globals

        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static HttpClient _client = new HttpClient();

        //ClientID Guid for Splunk
        private static Guid _splunkClientID;

        //Setup Timer for reading logs
        private static Timer _readTimer;

        // Cache off the last read record
        private static object _lastRecordTransmitted;
        
        //Runtime Options Object
        private static OptionsStruct _runtimeOptions;

        // Global SQL Connection
        private static SqlConnection _connection;

        #endregion
        
        static void Main(string[] args)
        {

            // Setup logging
            log4net.Config.BasicConfigurator.Configure();

            try
            {
                _runtimeOptions = JsonConvert.DeserializeObject<OptionsStruct>(System.IO.File.ReadAllText("options.json"));


            }
            catch(Exception ex)
            {
                log.Error(ex);
            }

            
            
        }

        private static bool SetupSqlConnection(string connectionString)
        {
            _connection = new SqlConnection

            using (SqlConnection connection = new SqlConnection("Data Source=(local);Initial Catalog=SplunkTest;Integrated Security=SSPI"))
            {
                connection.Open();
            }
        }
    }
}
