using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Timers;
using Newtonsoft.Json;
using aaSQLToSplunk.Helpers;
using System.IO;
using System.Net;

[assembly: log4net.Config.XmlConfigurator(ConfigFile = "log.config", Watch = true)]
namespace aaSQLToSplunk
{
    class Program
    {
        #region Globals

        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // HTTP Client for data transmission to Splunk
        private static HttpClient _splunkHTTPClient;

        //Setup Timer for reading logs
        private static Timer _readTimer;

        //Runtime Options Object
        private static OptionsStruct _runtimeOptions;

        // Global SQL Connection
        private static SqlConnection _sqlConnection;

        public static OptionsStruct RuntimeOptions
        {
            get
            {
                if (_runtimeOptions == null)
                {
                    _runtimeOptions = JsonConvert.DeserializeObject<OptionsStruct>(System.IO.File.ReadAllText("options.json"));
                }

                return _runtimeOptions;
            }

            set
            {
                _runtimeOptions = value;
            }
        }

        public static SqlConnection SQLConnection
        {
            get
            {
                if (_sqlConnection == null)
                {
                    log.DebugFormat("Connection String : {0}", RuntimeOptions.SQLConnectionString);
                    _sqlConnection = new SqlConnection(RuntimeOptions.SQLConnectionString);
                }

                if (_sqlConnection.State != ConnectionState.Open)
                {
                    log.Info("Opening SQL connection");
                    _sqlConnection.Open();
                }

                return _sqlConnection;
            }

            set
            {
                _sqlConnection = value;
            }
        }

        public static HttpClient SplunkHTTPClient
        {
            get
            {
                if (_splunkHTTPClient == null)
                {
                    _splunkHTTPClient = new HttpClient();
                    _splunkHTTPClient.BaseAddress = new Uri(RuntimeOptions.SplunkBaseAddress);
                    _splunkHTTPClient.DefaultRequestHeaders.Add("Authorization", "Splunk " + RuntimeOptions.SplunkAuthorizationToken);
                }

                return _splunkHTTPClient;
            }

            set
            {
                _splunkHTTPClient = value;
            }
        }

        private static string CacheFileName
        {
            get
            {
                if(RuntimeOptions.CacheFilename != null)
                {
                    return RuntimeOptions.CacheFilename;
                }
                else
                { 
                    return RuntimeOptions.SplunkSourceData + "-" + RuntimeOptions.SQLSequenceField + ".txt";
                }
            }
        }

        #endregion

        static void Main(string[] args)
        {

            // Setup logging
            log4net.Config.BasicConfigurator.Configure();

            try
            {
                //Eat any SSL errors
                // TODO : Test this feature
                ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) =>
                {
                    return RuntimeOptions.SplunkIgnoreSSLErrors;
                };

                // Configure Timers
                _readTimer = new Timer(RuntimeOptions.ReadInterval);
                _readTimer.Elapsed += ReadTimer_Elapsed;

                //Start Timers
                _readTimer.Start();

            }
            catch(Exception ex)
            {
                log.Error(ex);
            }

            //Prevent the console from closing when debugging
            Console.Read();            
        }

        private static void ReadTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            ReadAndTransmitData();
        }
        
        private static void ReadAndTransmitData()
        {
            DataTable dataTable = new DataTable();
            string kvpValue = "";

            try
            {
                if (SQLConnection.State == ConnectionState.Open)
                {
                    SqlCommand command = new SqlCommand(GetSqlQuery(), SQLConnection);

                    dataTable.Load(command.ExecuteReader());
                    
                    log.DebugFormat("{0} rows retrieved", dataTable.Rows.Count);

                    if (dataTable.Rows.Count > 0)
                    {
                        //Build the additional KVP values to Append
                        var additionalKVPValues = new StringBuilder();

                        additionalKVPValues.AppendFormat("{0}=\"{1}\", ", "SourceHost" , RuntimeOptions.SplunkSourceHost);
                        additionalKVPValues.AppendFormat("{0}=\"{1}\", ", "SourceData", RuntimeOptions.SplunkSourceData);
                        
                        //Get the KVP string for the records
                        kvpValue = dataTable.ToKVP(additionalKVPValues.ToString(),RuntimeOptions.SQLTimestampField, RuntimeOptions.SplunkEventTimestampFormat);

                        //log.Debug(kvpValue);

                        //Transmit the records
                        var result = TransmitValues(SplunkHTTPClient, RuntimeOptions.SplunkClientID, kvpValue).Result;

                        //If successful then write the last sequence value to disk
                        if(result.StatusCode == HttpStatusCode.OK)
                        {
                            // Write the last sequence value to the cache value named for the SQLSequence Field.  Order the result set by the sequence field then select the first record
                            WriteCacheFile(dataTable);     
                        }
                        else
                        {
                            log.Warn(result);
                        }      
                    }
                }
                else
                {
                    //
                    log.Warn("SQL Connection not Open");
                    //SetupSqlConnection(RuntimeOptions.SQLConnectionString);
                }
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
            finally
            {
                dataTable.Dispose();
            }
        }

        private static void WriteCacheFile(DataTable dataTable)
        {
            string cacheWriteValue;
            DateTime dateTimeValue;

            cacheWriteValue = dataTable.AsEnumerable().OrderByDescending(r => r[RuntimeOptions.SQLSequenceField]).First()[RuntimeOptions.SQLSequenceField].ToString();
            
            if(DateTime.TryParse(cacheWriteValue,out dateTimeValue))
            {
               x
            }
            else
            {
                // Do nothing
            }

            // Write the last sequence value to the cache value named for the SQLSequence Field.  Order the result set by the sequence field then select the first record
            File.WriteAllText(CacheFileName, dataTable.AsEnumerable().OrderByDescending(r => r[RuntimeOptions.SQLSequenceField]).First()[RuntimeOptions.SQLSequenceField].ToString(RuntimeOptions.SplunkEventTimestampFormat));
        }

        private static string GetSqlQuery()
        {
            string returnValue;
            string sqlSequenceFieldValue;

            //Get the base query and limit by TOP XX
            returnValue = RuntimeOptions.SQLQuery.Replace("{{MaxRecords}}", RuntimeOptions.MaxRecords.ToString());
            
            // Add the where clause if we can get the cached Sequence Field Value
            try
            {
                if (File.Exists(CacheFileName))
                {
                    sqlSequenceFieldValue = File.ReadAllText(CacheFileName) ?? string.Empty;
                }
                else
                {
                    sqlSequenceFieldValue = RuntimeOptions.SQLSequenceFieldDefaultValue;
                }
                

                if (sqlSequenceFieldValue != string.Empty)
                {
                    returnValue += RuntimeOptions.SQLWhereClause.Replace("{{SQLSequenceField}}", RuntimeOptions.SQLSequenceField).Replace("{{LastSQLSequenceFieldValue}}", sqlSequenceFieldValue);

                   // returnValue += " WHERE " + RuntimeOptions.SQLSequenceField + " > " + sqlSequenceFieldValue;'//'
                }
            }
            catch
            {
                                
                // Do nothing
            }

            //Finally add the Order By Clause
            returnValue += RuntimeOptions.SQLOrderByClause.Replace("{{SQLSequenceField}}", RuntimeOptions.SQLSequenceField);

            log.DebugFormat("SQL Query : {0}", returnValue);

            return returnValue;
        }
        
      
        /// <summary>
        /// Transmit the KVP values via HTTP to the Splunk HTTP Raw Collector
        /// </summary>
        /// <param name="client"></param>
        /// <param name="clientID"></param>
        /// <param name="kvpValues"></param>
        static async Task<HttpResponseMessage> TransmitValues(HttpClient client, Guid clientID, string kvpValues)
        {
            HttpResponseMessage responseMessage = new HttpResponseMessage();

            try
            {
                log.DebugFormat("Transmitting {0} bytes", System.Text.ASCIIEncoding.Unicode.GetByteCount(kvpValues));
                responseMessage = await client.PostAsync("/services/collector/raw?channel=" + clientID, new StringContent(kvpValues));
            }
            catch (Exception ex)
            {
                if (ex is HttpRequestException || ex is AggregateException)
                {
                    IncrementTimerBackoff();
                    responseMessage.StatusCode = HttpStatusCode.ServiceUnavailable;
                    responseMessage.ReasonPhrase = string.Format("Transmit failed : {0}", ex.Message);
                }
                else
                {
                    log.Error(ex);
                }
            }
            return responseMessage;
        }

        private static void IncrementTimerBackoff()
        {
            try
            {
                var currentInterval = _readTimer.Interval;

                if (currentInterval < RuntimeOptions.MaximumReadInterval)
                {
                    _readTimer.Interval = System.Math.Min(currentInterval * 2, RuntimeOptions.MaximumReadInterval);
                    log.WarnFormat("Read Timer interval set to {0} milliseconds", _readTimer.Interval);
                }
            }
            catch (Exception ex)
            {
                log.Error(ex);
                // Set to a default read interval of 5000
                _readTimer.Interval = 60000;
            }
        }
    }
}