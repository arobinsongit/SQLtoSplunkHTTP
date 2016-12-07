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
using System.Globalization;
using SplunkHTTPUtility;
using Microsoft.Extensions.CommandLineUtils;

[assembly: log4net.Config.XmlConfigurator(ConfigFile = "log.config", Watch = true)]
namespace aaSQLToSplunk
{
    class Program
    {
        #region Globals

        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // HTTP Client for data transmission to Splunk
        private static SplunkHTTP splunkHTTPClient;
        internal static SplunkHTTP SplunkHTTPClient
        {
            get
            {
                return splunkHTTPClient;
            }

            set
            {
                splunkHTTPClient = value;
            }
        }

        //Setup Timer for reading logs
        private static Timer readTimer;
        
        //Runtime Options Object
        private static OptionsStruct runtimeOptions;
        internal static OptionsStruct RuntimeOptions
        {
            get
            {
                if (runtimeOptions == null)
                {
                    runtimeOptions = JsonConvert.DeserializeObject<OptionsStruct>(System.IO.File.ReadAllText("options.json"));
                }

                return runtimeOptions;
            }

            set
            {
                runtimeOptions = value;
            }
        }

        // Global SQL Connection
        private static SqlConnection sqlConnection;
        internal static SqlConnection SQLConnection
        {
            get
            {
                if (sqlConnection == null)
                {
                    log.DebugFormat("Connection String : {0}", RuntimeOptions.SQLConnectionString);
                    sqlConnection = new SqlConnection(RuntimeOptions.SQLConnectionString);
                }

                if (sqlConnection.State != ConnectionState.Open)
                {
                    log.Info("Opening SQL connection");
                    sqlConnection.Open();
                }

                return sqlConnection;
            }

            set
            {
                sqlConnection = value;
            }
        }

        internal static string CacheFileName
        {
            get
            {
                if (RuntimeOptions.CacheFilename != null)
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

                //https://github.com/aspnet/Scaffolding/blob/ff39da926a1aa605599c295633bfcc74381af19d/src/Microsoft.VisualStudio.Web.CodeGeneration.Tools/Program.cs

                #region Argument Options

                var app = new CommandLineApplication(false)
                {
                    Name = "aaSQLToSplunk",
                    Description = "SQL Server to Splunk Forwarder"
                };
                
                // Define app Options; 
                var helpOption = app.HelpOption("-?|-h|--help");
                var optionsFilePath = app.Option("-o|--optionsfile <PATH>", "Path to options file", CommandOptionType.SingleValue);

                app.OnExecute(() =>
                {
                    if(helpOption.HasValue())
                    {
                        Environment.Exit(0);
                        return 0;
                    }
                    
                    try
                    {
                        string optionsPath = optionsFilePath.Value();
                        if (string.IsNullOrEmpty(optionsPath))
                        {
                            optionsPath = "options.json";
                        }

                        if (System.IO.File.Exists(optionsPath))
                        {
                            RuntimeOptions = JsonConvert.DeserializeObject<OptionsStruct>(System.IO.File.ReadAllText(optionsPath));
                        }
                        else
                        {
                            log.WarnFormat("Specified options file {0} does not exist. Loading default values.", optionsPath);
                            RuntimeOptions = new OptionsStruct();
                        }
                    }
                    catch(Exception ex)
                    {
                        RuntimeOptions = new OptionsStruct();
                        log.Error(ex);
                    }
                    
                    return 0;
                }
                );
                
                app.Execute(args);
                
                #endregion


                //// Parse Arguments
                //CommandLineApplication commandLineApplication = new CommandLineApplication(throwOnUnexpectedArg: false);
                ////CommandArgument names = null;

                //CommandOption optionsfilename = commandLineApplication.Option("-o |--optionsfilename <greeting>", "Options file name.  If full path is not specified then file is assumed to be in same folder with EXE",CommandOptionType.SingleValue);                
                //commandLineApplication.HelpOption("-? | -h | --help");

                //commandLineApplication.OnExecute(() =>
                //{
                //    RuntimeOptions = JsonConvert.DeserializeObject<OptionsStruct>(System.IO.File.ReadAllText(optionsfilename));
                //});
                //commandLineApplication.Execute(args);

                // Setup the SplunkHTTPClient
                SplunkHTTPClient = new SplunkHTTP(log, RuntimeOptions.SplunkAuthorizationToken, RuntimeOptions.SplunkBaseAddress, RuntimeOptions.SplunkClientID);
                
                //Eat any SSL errors
                // TODO : Test this feature
                ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) =>
                {
                    return RuntimeOptions.SplunkIgnoreSSLErrors;
                };

                // Configure Timers
                readTimer = new Timer(RuntimeOptions.ReadInterval);
                readTimer.Elapsed += ReadTimer_Elapsed;

                //Start Timers
                readTimer.Start();

            }
            catch (Exception ex)
            {
                log.Error(ex);
            }

            //Prevent the console from closing
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

                        additionalKVPValues.AppendFormat("{0}=\"{1}\", ", "SourceHost", RuntimeOptions.SplunkSourceHost);
                        additionalKVPValues.AppendFormat("{0}=\"{1}\", ", "SourceData", RuntimeOptions.SplunkSourceData);

                        //Get the KVP string for the records
                        kvpValue = dataTable.ToKVP(additionalKVPValues.ToString(), RuntimeOptions.SQLTimestampField, RuntimeOptions.SplunkEventTimestampFormat);

                        //Transmit the records
                        var result = SplunkHTTPClient.TransmitValues(kvpValue).Result;
                            
                            //TransmitValues(SplunkHTTPClient, RuntimeOptions.SplunkClientID, kvpValue).Result;

                        //If successful then write the last sequence value to disk
                        if (result.StatusCode == HttpStatusCode.OK)
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

        /// <summary>
        /// Write an entry for the maximum sequence field value into the cache file
        /// </summary>
        /// <param name="dataTable">Transmitted records</param>
        private static void WriteCacheFile(DataTable dataTable)
        {
            string cacheWriteValue;

            try
            {
                cacheWriteValue = string.Format("{0:" + RuntimeOptions.CacheWriteValueStringFormat + "}", dataTable.AsEnumerable().OrderByDescending(r => r[RuntimeOptions.SQLSequenceField]).First()[RuntimeOptions.SQLSequenceField]);
                log.DebugFormat("cacheWriteValue : {0}", cacheWriteValue);
                File.WriteAllText(CacheFileName, cacheWriteValue);
            }
            catch(Exception ex)
            {
                log.Error(ex);
            }
        }

        /// <summary>
        /// Calculate SQL query from options and cache values
        /// </summary>
        /// <returns></returns>
        private static string GetSqlQuery()
        {
            string returnValue;
            string cachedSqlSequenceFieldValue;
            DateTime cachedSqlSequenceFieldValueDateTime;
            DateTimeStyles cacheDateTimeStyle;

            //Get the base query and limit by TOP XX
            returnValue = RuntimeOptions.SQLQuery.Replace("{{MaxRecords}}", RuntimeOptions.MaxRecords.ToString());

            // Add the where clause if we can get the cached Sequence Field Value
            try
            {
                if (File.Exists(CacheFileName))
                {
                    cachedSqlSequenceFieldValue = File.ReadAllText(CacheFileName) ?? string.Empty;
                }
                else
                {
                    cachedSqlSequenceFieldValue = RuntimeOptions.SQLSequenceFieldDefaultValue;
                }

                if (RuntimeOptions.CacheWriteValueIsUTCTimestamp)
                {
                    cacheDateTimeStyle = DateTimeStyles.AssumeUniversal;
                }
                else
                {
                    cacheDateTimeStyle = DateTimeStyles.AssumeLocal;
                }

                if (DateTime.TryParseExact(cachedSqlSequenceFieldValue, RuntimeOptions.CacheWriteValueStringFormat, CultureInfo.InvariantCulture, cacheDateTimeStyle, out cachedSqlSequenceFieldValueDateTime))
                {
                    cachedSqlSequenceFieldValue = cachedSqlSequenceFieldValueDateTime.AddMilliseconds(RuntimeOptions.CacheWriteValueTimestampMillisecondsAdd).ToString("yyyy-MM-dd HH:mm:ss.ffffff");
                }

                if (cachedSqlSequenceFieldValue != string.Empty)
                {
                    returnValue += RuntimeOptions.SQLWhereClause.Replace("{{SQLSequenceField}}", RuntimeOptions.SQLSequenceField).Replace("{{LastSQLSequenceFieldValue}}", cachedSqlSequenceFieldValue);
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
        /// Slow down the timer by doubling the interval up to MaximumReadInterval
        /// </summary>
        private static void IncrementTimerBackoff()
        {
            try
            {
                var currentInterval = readTimer.Interval;

                if (currentInterval < RuntimeOptions.MaximumReadInterval)
                {
                    readTimer.Interval = System.Math.Min(currentInterval * 2, RuntimeOptions.MaximumReadInterval);
                    log.WarnFormat("Read Timer interval set to {0} milliseconds", readTimer.Interval);
                }
            }
            catch (Exception ex)
            {
                log.Error(ex);
                // Set to a default read interval of 60000
                readTimer.Interval = 60000;
            }
        }
    }
}