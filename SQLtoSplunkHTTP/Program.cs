// Copyright (c) Andrew Robinson. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Text;
using System.Timers;
using Newtonsoft.Json;
using SQLtoSplunkHTTP.Helpers;
using System.IO;
using System.Net;
using System.Globalization;
using SplunkHTTPUtility;
using Microsoft.Extensions.CommandLineUtils;
using System.Reflection;

[assembly: log4net.Config.XmlConfigurator(ConfigFile = "log.config", Watch = true)]
namespace SQLtoSplunkHTTP
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
        private static Options runtimeOptions;
        internal static Options RuntimeOptions
        {
            get
            {
                if (runtimeOptions == null)
                {
                    runtimeOptions = JsonConvert.DeserializeObject<Options>(System.IO.File.ReadAllText("options.json"));
                }

                return runtimeOptions;
            }

            set
            {
                runtimeOptions = value;
            }
        }

        // Global SQL Connection
        private static SqlConnection sqlConnectionObject;
        internal static SqlConnection SQLConnectionObject
        {
            get
            {
                if (sqlConnectionObject == null)
                {
                    log.DebugFormat("Connection String : {0}", RuntimeOptions.SQLConnectionString);
                    sqlConnectionObject = new SqlConnection(RuntimeOptions.SQLConnectionString);
                }

                if (sqlConnectionObject.State != ConnectionState.Open)
                {
                    log.Info("Opening SQL connection");
                    sqlConnectionObject.Open();
                }

                return sqlConnectionObject;
            }

            set
            {
                sqlConnectionObject = value;
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

        static int Main(string[] args)
        {
            // Setup logging
            log4net.Config.BasicConfigurator.Configure();

            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                log.InfoFormat("Starting {0} Version {1}", assembly.Location, assembly.GetName().Version.ToString());
                
                #region Argument Options
                
                var app = new CommandLineApplication(throwOnUnexpectedArg: false)
                {
                    Name = "SQLToSplunkHTTP",
                    Description = "Command line application meant to forward records from a SQL Server Database to a Splunk HTTP collector",
                    FullName = "SQL Server to Splunk HTTP Collector"      
                };
                
                // Define app Options; 
                app.HelpOption("-?| -h| --help");
                app.VersionOption("-v| --version", assembly.GetName().Version.MajorRevision.ToString(), assembly.GetName().Version.ToString());

                var optionsFilePathOption = app.Option("-o| --optionsfile <PATH>", "Path to options file (Optional)", CommandOptionType.SingleValue);
                
        app.OnExecute(() =>
                {
                    //Load runtime options
                    RuntimeOptions = ReadOptionsFile(optionsFilePathOption);

                    // Setup the SplunkHTTPClient
                    SplunkHTTPClient = new SplunkHTTP(log, RuntimeOptions.SplunkAuthorizationToken, RuntimeOptions.SplunkBaseAddress, RuntimeOptions.SplunkClientID);

                    //Eat any SSL errors
                    // TODO : Test this feature
                    ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) =>
                    {
                        return RuntimeOptions.SplunkIgnoreSSLErrors;
                    };

                    // Configure Timer
                    readTimer = new Timer(RuntimeOptions.ReadInterval);
                    
                    // Create delegate to handle elapsed time event
                    readTimer.Elapsed += ReadTimer_Elapsed;

                    //Start Timer
                    readTimer.Start();

                    //Prevent console from exiting
                    Console.Read();
                    return 0;
                });

                app.Command("clearcache", c =>
                 {

                     c.Description = "Deletes the current cache file";
                     c.HelpOption("-?| -h| --help");

                     c.OnExecute(() =>
                     {
                         //Load runtime options
                         RuntimeOptions = ReadOptionsFile(optionsFilePathOption);

                         log.InfoFormat("Deleting cache file {0}", CacheFileName);
                         System.IO.File.Delete(CacheFileName);

                         return 0;
                     });
                 });

                app.Command("createdefaultoptionsfile", c =>
                {

                    c.Description = "Create a default options.json file";
                    c.HelpOption("-?| -h| --help");

                    var overWriteOption = c.Option("-o| --overwrite", "Overwrite existing options.json file",CommandOptionType.NoValue);
                    var fileNameOption = c.Option("-f| --filename <PATH>", "Name of options file (Optional)", CommandOptionType.SingleValue);
                    
                    c.OnExecute(() =>
                    {
                        var fileName = fileNameOption.Value() ?? "options.json";
                        
                        if (System.IO.File.Exists(fileName))
                        {
                            log.InfoFormat("{0} exists", fileName);

                            if (!overWriteOption.HasValue())
                            {
                                log.InfoFormat("Applications options not set to overwrite {0}.  Specify options to overwrite or use different filename.", fileName);
                                return 0;
                            }
                            else
                            {
                                log.InfoFormat("Overwriting {0}", fileName);
                            }                
                        }
                        
                        System.IO.File.WriteAllText(fileName, JsonConvert.SerializeObject(new Options(),Formatting.Indented));

                        log.InfoFormat("Wrote default options to {0}", fileName);

                        return 0;
                    });
                });

                //Debug the startup arguments
                log.DebugFormat("Startup Arguments");
                log.Debug(JsonConvert.SerializeObject(args));

                // Run the application with arguments
                return app.Execute(args);
                
                #endregion
            }
            catch (Exception ex)
            {
                log.Error(ex);
                return -1;
            }
        }

        private static Options ReadOptionsFile(CommandOption optionsFilePathOption)
        {
            try
            {
                var optionsPath = optionsFilePathOption.Value() ?? "options.json";

                log.DebugFormat("Using options file {0}", optionsPath);

                if (System.IO.File.Exists(optionsPath))
                {
                    return JsonConvert.DeserializeObject<Options>(System.IO.File.ReadAllText(optionsPath));
                }
                else
                {
                    log.WarnFormat("Specified options file {0} does not exist. Loading default values.", optionsPath);
                    return new Options();
                }
            }
            catch (Exception ex)
            {
                log.Error(ex);
                return new Options();
            }
        }

        private static void ReadTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            ReadAndTransmitData(GetSqlQuery(RuntimeOptions),SQLConnectionObject);
        }

        private static void ReadAndTransmitData(string query, SqlConnection sqlConnectionObject)
        {
            DataTable dataTable = new DataTable();
            string kvpValue = "";

            try
            {
                if (sqlConnectionObject.State == ConnectionState.Open)
                {
                    SqlCommand command = new SqlCommand(query, sqlConnectionObject);

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

                        //If successful then write the last sequence value to disk
                        if (result.StatusCode == HttpStatusCode.OK)
                        {
                            // Write the last sequence value to the cache value named for the SQLSequence Field.  Order the result set by the sequence field then select the first record
                            WriteCacheFile(dataTable, CacheFileName, RuntimeOptions);

                            //Reset timer interval
                            ClearTimerBackoff(ref readTimer, RuntimeOptions);
                        }
                        else
                        {
                            // Implement a timer backoff so we don't flood the endpoint
                            IncrementTimerBackoff(ref readTimer, RuntimeOptions);
                            log.WarnFormat("HTTP Transmission not OK {0}",result);
                        }
                    }
                }
                else
                {
                    log.Warn("SQL Connection not Open");
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
        /// <param name="cacheFileName">Filename to write cache data to</param>
        /// <param name="runtimeOptions">Runtime Options Object</param>
        private static void WriteCacheFile(DataTable dataTable, string cacheFileName, Options runtimeOptions)
        {
            string cacheWriteValue;

            try
            {
                cacheWriteValue = string.Format("{0:" + runtimeOptions.CacheWriteValueStringFormat + "}", dataTable.AsEnumerable().OrderByDescending(r => r[runtimeOptions.SQLSequenceField]).First()[runtimeOptions.SQLSequenceField]);
                log.DebugFormat("cacheWriteValue : {0}", cacheWriteValue);
                File.WriteAllText(cacheFileName, cacheWriteValue);
            }
            catch(Exception ex)
            {
                log.Error(ex);
            }
        }

        /// <summary>
        /// Calculate SQL query from options and cache values
        /// </summary>
        /// <param name="runtimeOptions">Runtime Options object</param>
        /// <returns>String representing SQL Query based on provided runtime options</returns>
        private static string GetSqlQuery(Options runtimeOptions)
        {
            string returnValue;
            string cachedSqlSequenceFieldValue;
            DateTime cachedSqlSequenceFieldValueDateTime;
            DateTimeStyles cacheDateTimeStyle;

            //Get the base query and limit by TOP XX
            returnValue = runtimeOptions.SQLQuery.Replace("{{MaxRecords}}", runtimeOptions.MaxRecords.ToString());

            // Add the where clause if we can get the cached Sequence Field Value
            try
            {
                if (File.Exists(CacheFileName))
                {
                    cachedSqlSequenceFieldValue = File.ReadAllText(CacheFileName) ?? string.Empty;
                }
                else
                {
                    cachedSqlSequenceFieldValue = runtimeOptions.SQLSequenceFieldDefaultValue;
                }

                if (runtimeOptions.CacheWriteValueIsUTCTimestamp)
                {
                    cacheDateTimeStyle = DateTimeStyles.AssumeUniversal;
                }
                else
                {
                    cacheDateTimeStyle = DateTimeStyles.AssumeLocal;
                }

                if (DateTime.TryParseExact(cachedSqlSequenceFieldValue, runtimeOptions.CacheWriteValueStringFormat, CultureInfo.InvariantCulture, cacheDateTimeStyle, out cachedSqlSequenceFieldValueDateTime))
                {
                    cachedSqlSequenceFieldValue = cachedSqlSequenceFieldValueDateTime.AddMilliseconds(runtimeOptions.CacheWriteValueTimestampMillisecondsAdd).ToString("yyyy-MM-dd HH:mm:ss.ffffff");
                }

                if (cachedSqlSequenceFieldValue != string.Empty)
                {
                    returnValue += runtimeOptions.SQLWhereClause.Replace("{{SQLSequenceField}}", runtimeOptions.SQLSequenceField).Replace("{{LastSQLSequenceFieldValue}}", cachedSqlSequenceFieldValue);
                }
            }
            catch
            {
                // Do nothing
            }

            //Finally add the Order By Clause
            returnValue += runtimeOptions.SQLOrderByClause.Replace("{{SQLSequenceField}}", runtimeOptions.SQLSequenceField);

            log.DebugFormat("SQL Query : {0}", returnValue);

            return returnValue;
        }

        /// <summary>
        /// Slow down the timer by doubling the interval up to MaximumReadInterval
        /// </summary>
        private static void IncrementTimerBackoff(ref Timer readTimer, Options runtimeOptions)
        {
            try
            {
                lock (readTimer)
                {
                    var currentInterval = readTimer.Interval;

                    if (currentInterval < runtimeOptions.MaximumReadInterval)
                    {
                        readTimer.Interval = System.Math.Min(currentInterval * 2, runtimeOptions.MaximumReadInterval);
                        log.WarnFormat("Read Timer interval set to {0} milliseconds", readTimer.Interval);
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error(ex);
                // Set to a default read interval of 60000
                readTimer.Interval = 60000;
            }
        }

        /// <summary>
        /// Slow down the timer by doubling the interval up to MaximumReadInterval
        /// </summary>
        private static void ClearTimerBackoff(ref Timer readTimer, Options runtimeOptions)
        {
            try
            {                
                log.InfoFormat("Restoring transmission timer interval to {0}", runtimeOptions.ReadInterval);
                lock (readTimer)
                {
                    readTimer.Interval = RuntimeOptions.ReadInterval;
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