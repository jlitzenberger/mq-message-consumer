using IBM.WMQ;
using MQMessageConsumer.Data.Configuration;
using MQMessageConsumerService.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Transactions;
using System.Web;
using System.Web.Http;

namespace MQMessageConsumer
{
    public class MessageWrapper
    {
        public string Time { get; set; }
        public string Level { get; set; }
        public string MachineName { get; set; }
        public dynamic Message { get; set; }
    }

    public class Library
    {
        private MQQueueManager queueManager = null;
        private MQQueue queue = null;

        private string QueueManagerName = string.Empty;
        private string QueueName = string.Empty;

        /// <summary>
        /// app.config elements
        /// </summary>
        private List<MessageLoggingElement> MessageLoggingElements = new List<MessageLoggingElement>();
        private List<MessagingPropertiesFilterElement> MessagingPropertiesFilterElements = new List<MessagingPropertiesFilterElement>();

        private string DestinationApiPath = string.Empty;

        private string DestinationApiPostResourceParams = null;
        private string DestinationApiPutResourceParams = null;
        private string DestinationApiPatchResourceParams = null;
        private string DestinationApiDeleteResourceParams = null;

        private string DestinationApiOverride = null;

        private string ApiDestinationScheme = string.Empty;
        private string ApiDestinationHost = string.Empty;
        private int ApiDestinationPort;
        private string ApiDestinationCredentials;

        private string ApiTopicCredentials;
        private string ApiTopicUri;
        private string TopicErrorName;

        private string TransformPostTrigger = string.Empty;
        private string TransformPostUrlParamsApiUri = string.Empty;
        private string TransformPutTrigger = string.Empty;
        private string TransformPutUrlParamsApiUri = string.Empty;
        private string TransformPatchTrigger = string.Empty;
        private string TransformPatchUrlParamsApiUri = string.Empty;
        private string TransformDeleteTrigger = string.Empty;
        private string TransformDeleteUrlParamsApiUri = string.Empty;

        Dictionary<string, string> ExpectedMqNamedProperties = null;
        Dictionary<string, string> MessagingProperties = null;

        List<string> ListExpectedMqNamedProperties = new List<string>();
        List<string> ListRequiredMqNamedProperties = new List<string>();

        string MessageHeaderRequestTimestamp = string.Empty;
        string MessageHeaderResponseTimestamp = string.Empty;

        dynamic OriginalMessage;

        private readonly EventLog _log;
        Message Message;
        public Library(EventLog log)
        {
            _log = log;

            SetParams();
        }
        [STAThread]
        public void ProcessQueue()
        {
            try
            {
                Process();
            }
            catch (Exception ex)
            {
                Log(EventLogEntryType.Error, ex.Message, ToJTokenOrString(SerializeMqMessage(Message)), ex);
            }
        }
        private void SetParams()
        {
            var consumerConfigurationManagerSection = ConfigurationManager.GetSection(ConsumerConfigurationManagerSection.SectionName) as ConsumerConfigurationManagerSection;
            if (consumerConfigurationManagerSection != null)
            {
                foreach (MessageLoggingElement messageLoggingElement in consumerConfigurationManagerSection.MessageLoggings)
                {
                    MessageLoggingElements.Add(messageLoggingElement);
                }
                foreach (MessagingPropertiesFilterElement messagingPropertiesFilterElement in consumerConfigurationManagerSection.MessagePropertiesFilters)
                {
                    MessagingPropertiesFilterElements.Add(messagingPropertiesFilterElement);
                }
            }

            //MessagingPropertiesFilter = ConfigurationManager.GetSection("messagingPropertiesFilter") as NameValueCollection;
            //MessageLogging = ConfigurationManager.GetSection("messageLogging") as NameValueCollection;
            QueueManagerName = ConfigurationManager.AppSettings.Get("QueueManagerName");
            QueueName = ConfigurationManager.AppSettings.Get("QueueName");

            DestinationApiPath = ConfigurationManager.AppSettings.Get("DestinationApiPath");

            DestinationApiPostResourceParams = ConfigurationManager.AppSettings["DestinationApiPostResourceParams"];
            DestinationApiPutResourceParams = ConfigurationManager.AppSettings["DestinationApiPutResourceParams"];
            DestinationApiPatchResourceParams = ConfigurationManager.AppSettings["DestinationApiPatchResourceParams"];
            DestinationApiDeleteResourceParams = ConfigurationManager.AppSettings["DestinationApiDeleteResourceParams"];

            DestinationApiOverride = ConfigurationManager.AppSettings["DestinationApiOverride"];

            ListExpectedMqNamedProperties.Add("registrationVersion");
            ListExpectedMqNamedProperties.Add("httpMethod");
            ListExpectedMqNamedProperties.Add("originSystem");
            ListExpectedMqNamedProperties.Add("urlParams");

            ListRequiredMqNamedProperties.Add("registrationVersion");
            //ListRequiredMqNamedProperties.Add("httpMethod");
            ListRequiredMqNamedProperties.Add("originSystem");

            SetEnvionmentParams();
        }
        private void SetEnvionmentParams()
        {
            if (ConfigurationManager.AppSettings.Get("Env") == "LOCAL")
            {
                ApiDestinationScheme = ConfigurationManager.AppSettings.Get("LOCAL_ApiDestinationScheme");
                ApiDestinationHost = ConfigurationManager.AppSettings.Get("LOCAL_ApiDestinationHost");
                ApiDestinationPort = int.Parse(ConfigurationManager.AppSettings.Get("LOCAL_ApiDestinationPort"));
                DestinationApiPath = ConfigurationManager.AppSettings.Get("LOCAL_ApiDestinationPath");
                ApiDestinationCredentials = ConfigurationManager.AppSettings.Get("LOCAL_ApiDestinationCredentials");

                ApiTopicCredentials = ConfigurationManager.AppSettings.Get("LOCAL_ApiTopicCredentials");
                ApiTopicUri = ConfigurationManager.AppSettings.Get("LOCAL_ApiTopicUri");
                TopicErrorName = ConfigurationManager.AppSettings.Get("LOCAL_TopicErrorName");

                TransformPostTrigger = ConfigurationManager.AppSettings.Get("LOCAL_TransformPostTrigger");
                TransformPostUrlParamsApiUri = ConfigurationManager.AppSettings.Get("LOCAL_TransformPostUrlParamsApiUri");
                TransformPutTrigger = ConfigurationManager.AppSettings.Get("LOCAL_TransformPutTrigger");
                TransformPutUrlParamsApiUri = ConfigurationManager.AppSettings.Get("LOCAL_TransformPutUrlParamsApiUri");
                TransformPatchTrigger = ConfigurationManager.AppSettings.Get("LOCAL_TransformPatchTrigger");
                TransformPatchUrlParamsApiUri = ConfigurationManager.AppSettings.Get("LOCAL_TransformPatchUrlParamsApiUri");
                TransformDeleteTrigger = ConfigurationManager.AppSettings.Get("LOCAL_TransformDeleteTrigger");
                TransformDeleteUrlParamsApiUri = ConfigurationManager.AppSettings.Get("LOCAL_TransformDeleteUrlParamsApiUri");
            }

            if (ConfigurationManager.AppSettings.Get("Env") == "DEV")
            {
                ApiDestinationScheme = ConfigurationManager.AppSettings.Get("DEV_ApiDestinationScheme");
                ApiDestinationHost = ConfigurationManager.AppSettings.Get("DEV_ApiDestinationHost");
                ApiDestinationPort = int.Parse(ConfigurationManager.AppSettings.Get("DEV_ApiDestinationPort"));
                DestinationApiPath = ConfigurationManager.AppSettings.Get("DEV_ApiDestinationPath");
                ApiDestinationCredentials = ConfigurationManager.AppSettings.Get("DEV_ApiDestinationCredentials");

                ApiTopicCredentials = ConfigurationManager.AppSettings.Get("DEV_ApiTopicCredentials");
                ApiTopicUri = ConfigurationManager.AppSettings.Get("DEV_ApiTopicUri");
                TopicErrorName = ConfigurationManager.AppSettings.Get("DEV_TopicErrorName");

                TransformPostTrigger = ConfigurationManager.AppSettings.Get("DEV_TransformPostTrigger");
                TransformPostUrlParamsApiUri = ConfigurationManager.AppSettings.Get("DEV_TransformPostUrlParamsApiUri");
                TransformPutTrigger = ConfigurationManager.AppSettings.Get("DEV_TransformPutTrigger");
                TransformPutUrlParamsApiUri = ConfigurationManager.AppSettings.Get("DEV_TransformPutUrlParamsApiUri");
                TransformPatchTrigger = ConfigurationManager.AppSettings.Get("DEV_TransformPatchTrigger");
                TransformPatchUrlParamsApiUri = ConfigurationManager.AppSettings.Get("DEV_TransformPatchUrlParamsApiUri");
                TransformDeleteTrigger = ConfigurationManager.AppSettings.Get("DEV_TransformDeleteTrigger");
                TransformDeleteUrlParamsApiUri = ConfigurationManager.AppSettings.Get("DEV_TransformDeletetUrlParamsApiUri");
            }

            if (ConfigurationManager.AppSettings.Get("Env") == "STG")
            {
                ApiDestinationScheme = ConfigurationManager.AppSettings.Get("STG_ApiDestinationScheme");
                ApiDestinationHost = ConfigurationManager.AppSettings.Get("STG_ApiDestinationHost");
                ApiDestinationPort = int.Parse(ConfigurationManager.AppSettings.Get("STG_ApiDestinationPort"));
                DestinationApiPath = ConfigurationManager.AppSettings.Get("STG_ApiDestinationPath");
                ApiDestinationCredentials = ConfigurationManager.AppSettings.Get("STG_ApiDestinationCredentials");

                ApiTopicCredentials = ConfigurationManager.AppSettings.Get("STG_ApiTopicCredentials");
                ApiTopicUri = ConfigurationManager.AppSettings.Get("STG_ApiTopicUri");
                TopicErrorName = ConfigurationManager.AppSettings.Get("STG_TopicErrorName");

                TransformPostTrigger = ConfigurationManager.AppSettings.Get("STG_TransformPostTrigger");
                TransformPostUrlParamsApiUri = ConfigurationManager.AppSettings.Get("STG_TransformPostUrlParamsApiUri");
                TransformPutTrigger = ConfigurationManager.AppSettings.Get("STG_TransformPutTrigger");
                TransformPutUrlParamsApiUri = ConfigurationManager.AppSettings.Get("STG_TransformPutUrlParamsApiUri");
                TransformPatchTrigger = ConfigurationManager.AppSettings.Get("STG_TransformPatchTrigger");
                TransformPatchUrlParamsApiUri = ConfigurationManager.AppSettings.Get("STG_TransformPatchUrlParamsApiUri");
                TransformDeleteTrigger = ConfigurationManager.AppSettings.Get("STG_TransformDeleteTrigger");
                TransformDeleteUrlParamsApiUri = ConfigurationManager.AppSettings.Get("STG_TransformDeletetUrlParamsApiUri");
            }

            if (ConfigurationManager.AppSettings.Get("Env") == "PRD")
            {
                ApiDestinationScheme = ConfigurationManager.AppSettings.Get("PRD_ApiDestinationScheme");
                ApiDestinationHost = ConfigurationManager.AppSettings.Get("PRD_ApiDestinationHost");
                ApiDestinationPort = int.Parse(ConfigurationManager.AppSettings.Get("PRD_ApiDestinationPort"));
                DestinationApiPath = ConfigurationManager.AppSettings.Get("PRD_ApiDestinationPath");
                ApiDestinationCredentials = ConfigurationManager.AppSettings.Get("PRD_ApiDestinationCredentials");

                ApiTopicCredentials = ConfigurationManager.AppSettings.Get("PRD_ApiTopicCredentials");
                ApiTopicUri = ConfigurationManager.AppSettings.Get("PRD_ApiTopicUri");
                TopicErrorName = ConfigurationManager.AppSettings.Get("PRD_TopicErrorName");

                TransformPostTrigger = ConfigurationManager.AppSettings.Get("PRD_TransformPostTrigger");
                TransformPostUrlParamsApiUri = ConfigurationManager.AppSettings.Get("PRD_TransformPostUrlParamsApiUri");
                TransformPutTrigger = ConfigurationManager.AppSettings.Get("PRD_TransformPutTrigger");
                TransformPutUrlParamsApiUri = ConfigurationManager.AppSettings.Get("PRD_TransformPutUrlParamsApiUri");
                TransformPatchTrigger = ConfigurationManager.AppSettings.Get("PRD_TransformPatchTrigger");
                TransformPatchUrlParamsApiUri = ConfigurationManager.AppSettings.Get("PRD_TransformPatchUrlParamsApiUri");
                TransformDeleteTrigger = ConfigurationManager.AppSettings.Get("PRD_TransformDeleteTrigger");
                TransformDeleteUrlParamsApiUri = ConfigurationManager.AppSettings.Get("PRD_TransformDeletetUrlParamsApiUri");
            }
        }
        public void ConnectMq()
        {
            Hashtable connectionProperties = new Hashtable();
            connectionProperties.Add(MQC.TRANSPORT_PROPERTY, MQC.TRANSPORT_MQSERIES_XACLIENT);
            queueManager = new MQQueueManager(QueueManagerName, connectionProperties);
        }
        public void DisconnectMq()
        {
            queueManager.Disconnect();
        }
        public void Process()
        {
            TransactionOptions transactionOptions = new TransactionOptions { Timeout = TimeSpan.MaxValue };
            using (TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, EnterpriseServicesInteropOption.Full))
            {
                try
                {
                    // connect to IBM MQ
                    ConnectMq();
                    // get an IBM MQ message from the queue                   
                    MQMessage mqMessage = GetMqMessage();
                    // disconnect from IBM MQ
                    DisconnectMq();

                    // read mqMessage into IbmMessage
                    if (mqMessage.MessageLength > 0)
                    {
                        // assemble the Message from the MQMessage
                        AssembleMessage(mqMessage);
                        // get only the expected "Named Properties" from all of the "Named Properties" on the MQ message for error and normal processing
                        ExpectedMqNamedProperties = GetExpectedMqNamedProperties(mqMessage);
                        // check if the required MQ "Named Properties" are present on the MQ Message
                        CheckMissingRequiredMqNamedProperites(mqMessage);
                        // clear message and disconnect
                        mqMessage.ClearMessage();

                        // get the Messaging-Properties "headers"
                        MessagingProperties = MessagingPropertiesToDictionary(Message.HttpHeaders.MessagingProperties);
                        // override the httpMethod if necessary
                        OverrideHttpMethod();
                        // filter only the messages you want, remove the rest
                        if (!IsFilteredMessage(MessagingProperties))
                        {
                            // process the message from the queue
                            HttpRequestMessage httpRequestMessage = CreateHttpRequestMessage(Message);
                            HttpResponseMessage httpResponseMessage = ProcessMessage(httpRequestMessage);

                            ProcessResponseMessage(transactionScope, httpRequestMessage, httpResponseMessage);
                        }
                        else
                        {
                            // commit the transaction
                            transactionScope.Complete();
                        }
                    }
                    else
                    {
                        // the message is empty - commit the transaction
                        transactionScope.Complete();
                    }
                }
                catch (MQException ex) // there was an error with IBM MQ
                {
                    ProcessMqException(ex);
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    ProcessWin32Exception(ex);
                }
                catch (System.AggregateException ex) // there was a timeout error returned from the web proxy or the API
                {
                    ProcessAggregateException(ex);
                    transactionScope.Complete(); // commit the transaction                 
                }
                catch (MqNamedProperitesNotFoundException ex)  // NamedProperites are missing on the MQ message
                {
                    ProcessMqNamedProperitesNotFoundException(ex);
                    transactionScope.Complete(); // commit the transaction
                }
                catch (JsonReaderException ex) // jToken error - invalid json
                {
                    ProcessJsonReaderException(ex);
                    transactionScope.Complete(); // commit the transaction
                }
                catch (JsonSerializationException ex) // deseriaization error - the json message structure is invalid
                {
                    ProcessJsonSerializationException(ex);
                    transactionScope.Complete(); // commit the transaction  
                }
                catch (System.Web.Http.HttpResponseException ex) // the was an error returned from the API or thrown from TransformMessagingPropertiesUrlParams()
                {
                    ProcessHttpResponseException(ex, transactionScope);  //determine commit or rollback in ProcessHttpResponseException() function
                }
                catch (Exception ex)
                {
                    ProcessException(ex, transactionScope);  //determine commit or rollback in ProcessException() function
                }
            }
        }

        ////////////////////////////////////
        // Exception processing functions
        private void ProcessMqException(MQException ex)
        {
            EventLogEntryType logLevel;

            if (ex.Message == "MQRC_NO_MSG_AVAILABLE")
            {
                logLevel = EventLogEntryType.Warning;
            }
            else
            {
                logLevel = EventLogEntryType.Error;
            }

            Log(logLevel, ex.Message, ToJTokenOrString(SerializeMqMessage(Message)), ex);
            // rollback the transaction
        }
        private void ProcessWin32Exception(Win32Exception ex)
        {
            // it is still unknown why this error occurs, but it is harmless and no messages are lost
            if (ex.Message == "The operation completed successfully")
            {
                Log(EventLogEntryType.Warning, "Warning: IBM.WMQ.Nmqi.XAUnmanagedNmqiMQ.MQCONNX()", null, ex);
                // rollback the transaction 
            }
            else
            {
                throw ex;
            }
        }
        private void ProcessAggregateException(AggregateException ex)
        {
            if (ex.InnerException != null && ex.InnerException.InnerException != null)
            {
                // Timeout from web proxy (Zues). 
                if (ex.InnerException.InnerException is WebException &&
                    ex.InnerException.InnerException.Message == "The underlying connection was closed: The connection was closed unexpectedly.")
                {
                    Log(EventLogEntryType.Warning, ex.InnerException.InnerException.Message, ToJTokenOrString(SerializeMqMessage(Message)), ex);
                }
                else
                {
                    Log(EventLogEntryType.Error, "Unexpected Error", ToJTokenOrString(SerializeMqMessage(Message)), ex);
                }
            }
            else if (ex.InnerException is System.Threading.Tasks.TaskCanceledException &&
                     ex.InnerException.Message == "A task was canceled.")
            {
                // Timeout from the web service.                        
                Log(EventLogEntryType.Warning, ex.InnerException.Message, ToJTokenOrString(SerializeMqMessage(Message)), ex);
            }
            else
            {
                Log(EventLogEntryType.Error, "Unexpected Error", ToJTokenOrString(SerializeMqMessage(Message)), ex);
            }
        }
        private void ProcessMqNamedProperitesNotFoundException(MqNamedProperitesNotFoundException ex)
        {
            Log(EventLogEntryType.Warning, "MqNamedProperitesNotFoundException", ToJTokenOrString(SerializeMqMessage(Message)), ex);

            if (!string.IsNullOrEmpty(TopicErrorName))
            {
                CreateTopicError(ex);
            }
        }
        private void ProcessJsonReaderException(JsonReaderException ex)
        {
            Log(EventLogEntryType.Warning, "The json payload written to the queue is invalid", ToJTokenOrString(Message?.Payload?.ToString()), ex);

            if (!string.IsNullOrEmpty(TopicErrorName))
            {
                CreateSerializationError(ex.Message);
            }
        }
        private void ProcessJsonSerializationException(JsonSerializationException ex)
        {
            Log(EventLogEntryType.Warning, "Deseriaization error - the json message structure is invalid", ToJTokenOrString(Message?.Payload?.ToString()), ex);

            if (!string.IsNullOrEmpty(TopicErrorName))
            {
                CreateSerializationError(ex.Message);
            }
        }
        private void ProcessHttpResponseException(HttpResponseException ex, TransactionScope transactionScope)
        {
            if (ex.Response.StatusCode == HttpStatusCode.ServiceUnavailable &&
                        ex.Response.Headers.Contains("X-Status-Code") &&
                        ex.Response.Headers.GetValues("X-Status-Code").Any(m => m == "9000"))
            {
                // Database is unavailable
                Log(EventLogEntryType.Warning, "The database is unavailable", null, null, ex.Response);
                // rollback the transaction
            }
            else if (ex.Response.StatusCode == HttpStatusCode.ServiceUnavailable &&
                        ex.Response.Headers.Contains("X-Status-Code") &&
                        ex.Response.Headers.GetValues("X-Status-Code").Any(m => m == "9999"))
            {
                // Api's internal services are unavailable
                Log(EventLogEntryType.Warning, "Unable to connect to the remote server", null, null, ex.Response);
                // rollback the transaction
            }
            else if (ex.Response.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                // Api is unavailable
                Log(EventLogEntryType.Warning, "Unable to connect to the remote server", null, null, ex.Response);
                // rollback the transaction
            }
            else
            {
                EventLogEntryType logLevel = TranslateLogLevel(ex.Response.StatusCode);

                Log(logLevel, ex.Response?.Content?.ReadAsStringAsync().Result, ToJTokenOrString(SerializeMqMessage(Message)), ex, ex.Response);

                if (!string.IsNullOrEmpty(TopicErrorName))
                {
                    CreateTopicError(ex);
                }

                transactionScope.Complete(); // commit the transaction
            }
        }
        private void ProcessException(Exception ex, TransactionScope transactionScope)
        {
            //if unable to connect to API - leave message on the queue
            if (ex.InnerException != null
                && ex.InnerException.InnerException != null
                && ex.InnerException.InnerException.Message.Equals("Unable to connect to the remote server"))
            {
                Log(EventLogEntryType.Warning, "Unable to connect to endpoint", ToJTokenOrString(SerializeMqMessage(Message)), ex);
                // rollback the transaction
            }
            else //there was an unexpected error - remove message from the queue
            {
                Log(EventLogEntryType.Error, ex.Message, ToJTokenOrString(SerializeMqMessage(Message)), ex);

                if (!string.IsNullOrEmpty(TopicErrorName))
                {
                    CreateTopicError(ex);
                }

                transactionScope.Complete(); // commit the transaction                
            }
        }

        private void AssembleMessage(MQMessage mqMessage)
        {
            //copy the original message into an invalidated/unserialized variable for potential downstream error processing
            OriginalMessage = mqMessage.ReadString(mqMessage.MessageLength).Trim();

            Message = new Message();
            Message.Payload = OriginalMessage;
            // set messages ID's in case DeserializeAndValidateJsonSchema throws an error
            Message.HttpHeaders = new MQMessageConsumerService.Models.HttpHeaders();
            Message.HttpHeaders.MessageId = ByteArrayToString(mqMessage.MessageId);
            Message.HttpHeaders.MessageCorrelationId = ByteArrayToString(mqMessage.CorrelationId);

            // read Message.Payload into a JToken to VALIDATE
            JToken jToken = JToken.Parse(Message.Payload);
            //copy the serialized message for potential downstream error processing
            OriginalMessage = jToken;

            // deserialize jToken to message object
            Message = DeserializeAndValidateJsonSchema<Message>(jToken.ToString());

            if (string.IsNullOrEmpty(Message?.HttpHeaders?.MessagingQueue))
            {
                Message.HttpHeaders.MessagingQueue = ConfigurationManager.AppSettings["QueueName"];
            }
            //if (string.IsNullOrEmpty(Message?.HttpHeaders?.MessagingTopic))
            //{

            //}
            // set messages ID's again after DeserializeAndValidateJsonSchema
            Message.HttpHeaders.MessageId = ByteArrayToString(mqMessage.MessageId);
            Message.HttpHeaders.MessageCorrelationId = ByteArrayToString(mqMessage.CorrelationId);
        }
        private EventLogEntryType TranslateLogLevel(HttpStatusCode statusCode)
        {
            return (int)statusCode >= 500 ? EventLogEntryType.Error : EventLogEntryType.Warning;
        }

        private void OverrideHttpMethod()
        {
            if (!string.IsNullOrEmpty(DestinationApiOverride))
            {
                if (MessagingProperties.ContainsKey("httpMethod"))
                {
                    MessagingProperties["httpMethod"] = DestinationApiOverride;
                }
            }
        }
        private void CheckMissingRequiredMqNamedProperites(MQMessage mqMessage)
        {
            List<string> missingRequiredMqNamedProperites = MissingRequiredMqNamedProperites(mqMessage);

            if (missingRequiredMqNamedProperites != null && missingRequiredMqNamedProperites.Count > 0)
            {
                throw new MqNamedProperitesNotFoundException("Missing MqNamedProperites: " + string.Join(", ", missingRequiredMqNamedProperites.ToArray()));
            }
        }
        private void ProcessResponseMessage(TransactionScope transactionScope, HttpRequestMessage httpRequestMessage, HttpResponseMessage httpResponseMessage)
        {
            if (!httpResponseMessage.IsSuccessStatusCode)
            {
                if (httpResponseMessage.StatusCode == HttpStatusCode.ServiceUnavailable &&
                         httpResponseMessage.Headers.Contains("X-Status-Code") &&
                         httpResponseMessage.Headers.GetValues("X-Status-Code").Any(m => m == "9000"))
                {
                    // Database is unavailable
                    Log(EventLogEntryType.Warning, "The database is unavailable", null, null, httpResponseMessage);
                    // rollback the transaction
                }
                else if (httpResponseMessage.StatusCode == HttpStatusCode.ServiceUnavailable &&
                         httpResponseMessage.Headers.Contains("X-Status-Code") &&
                         httpResponseMessage.Headers.GetValues("X-Status-Code").Any(m => m == "9999"))
                {
                    // Api's internal services are unavailable
                    Log(EventLogEntryType.Warning, "Unable to connect to the remote server", null, null, httpResponseMessage);
                    // rollback the transaction
                }
                else if (httpResponseMessage.StatusCode == HttpStatusCode.ServiceUnavailable)
                {
                    // Api is unavailable
                    Log(EventLogEntryType.Warning, "Unable to connect to the remote server", null, null, httpResponseMessage);
                    // rollback the transaction
                }
                else
                {
                    EventLogEntryType logLevel = TranslateLogLevel(httpResponseMessage.StatusCode);

                    Log(logLevel, "Unsuccessful API response", ToJTokenOrString(SerializeMqMessage(Message)), null, httpResponseMessage);

                    if (!string.IsNullOrEmpty(TopicErrorName))
                    {
                        CreateTopicError(httpRequestMessage, httpResponseMessage);
                    }

                    transactionScope.Complete(); // commit the transaction
                }
            }
            else
            {
                Log(EventLogEntryType.Information, "The message was successfully processed", ToJTokenOrString(SerializeMqMessage(Message)), null, httpResponseMessage);
                transactionScope.Complete(); // commit the transaction
            }
        }

        private void Log(EventLogEntryType logLevel, string description, dynamic message = null, Exception exception = null, HttpResponseMessage httpResponseMessage = null)
        {
            this.MessageHeaderResponseTimestamp = DateTime.Now.ToString();

            if (message != null)
            {
                LogMessageToDisk(logLevel, message);
            }

            WriteEventLogEntry(logLevel, description, exception, httpResponseMessage, message);
        }
        private void LogMessageToDisk(EventLogEntryType logLevel, dynamic message)
        {
            if (message != null)
            {
                //create wrapper message
                MessageWrapper wrapper = new MessageWrapper();
                wrapper.Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.ffff");
                wrapper.Level = logLevel.ToString().ToUpper();
                wrapper.MachineName = Environment.MachineName;
                wrapper.Message = message;

                //create the log directory if it doesn't exist
                System.IO.DirectoryInfo di = new System.IO.DirectoryInfo(System.IO.Directory.GetCurrentDirectory() + @"\Logs" + @"\" + logLevel);
                if (!System.IO.Directory.Exists(di.FullName))
                {
                    System.IO.Directory.CreateDirectory(di.FullName);
                }

                //serialize and write message to disk
                System.IO.File.WriteAllText(di.FullName + @"\" + Message.HttpHeaders.MessageId + ".json",
                            JsonConvert.SerializeObject(wrapper, Formatting.Indented, new JsonSerializerSettings
                            {
                                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                                Converters = new List<JsonConverter> { new StringEnumConverter() }
                            })
                );
            }
        }
        private void WriteEventLogEntry(EventLogEntryType entryType, string description, Exception exception, HttpResponseMessage httpResponseMessage, dynamic message = null)
        {
            MQMessageConsumerService.Models.EventLogEntry eventLogEntry = new MQMessageConsumerService.Models.EventLogEntry();
            eventLogEntry.Description = description;
            eventLogEntry.MessageId = Message?.HttpHeaders?.MessageId;
            eventLogEntry.CorrelationId = Message?.HttpHeaders?.MessageCorrelationId;
            if (ExpectedMqNamedProperties != null)
            {
                eventLogEntry.NamedProperties = new Dictionary<string, string>();
                foreach (var item in ExpectedMqNamedProperties)
                {
                    eventLogEntry.NamedProperties.Add(item.Key, item.Value);
                }
            }
            if (message != null && Message != null && Message.Payload != null)
            {
                eventLogEntry.MessagePayloadLocaton = @"\\" + System.Environment.MachineName + MessageLoggingElements[0].UncFileLocation + @"\" + entryType + @"\" + Message.HttpHeaders.MessageId + ".json";
            }

            eventLogEntry.Exception = exception;
            eventLogEntry.HttpResponseMessage = httpResponseMessage;

            WriteEventLogEntryHeader(eventLogEntry, entryType);
        }
        private void WriteEventLogEntryHeader(MQMessageConsumerService.Models.EventLogEntry log, EventLogEntryType logType)
        {
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                Converters = new List<JsonConverter> { new StringEnumConverter() }
            };

            string NamedProperties = string.Empty;
            if (ExpectedMqNamedProperties != null)
            {
                NamedProperties = "MQ Named Properties: " + "\r\n";
                foreach (var item in ExpectedMqNamedProperties)
                {
                    NamedProperties += "  " + item.Key + ": " + item.Value + "\r\n";
                }
            }

            _log.WriteEntry(
                  "Event Description: " + log.Description + "\r\n"
                + "MQ Queue Name: " + QueueName + "\r\n"
                + "MQ Queue Manager: " + QueueManagerName + "\r\n"
                + "MQ Message Id: " + log.MessageId + "\r\n"
                + "MQ Correlation Id: " + log.CorrelationId + "\r\n"
                + NamedProperties
                + "MQ Message File Location : " + log.MessagePayloadLocaton + "\r\n"
                + "API Request Timestamp: " + MessageHeaderRequestTimestamp + "\r\n"
                + "API Response Timestamp: " + MessageHeaderResponseTimestamp + "\r\n\r\n"
                + "EventLogJson: " + JsonConvert.SerializeObject(log, Formatting.Indented, settings), logType);
        }
        public string ByteArrayToString(byte[] ba)
        {
            StringBuilder hex = new StringBuilder(ba.Length * 2);

            foreach (byte b in ba)
            {
                hex.AppendFormat("{0:x2}", b);
            }

            return hex.ToString();
        }

        /// <summary>
        /// This function will process invalid json to the configured error topic
        /// </summary>
        /// <param name="error"></param>
        private void CreateSerializationError(string error)
        {
            MqError mqe = new MqError();
            mqe.OriginalMqMessage = OriginalMessage != null ? OriginalMessage : string.Empty;
            mqe.MessageRequestHeaders = null; //will never be filled because the message can't be deserialized
            mqe.MessageRequestPayload = null; //will never be filled because the message can't be deserialized
            mqe.HttpResponseStatusCode = "400";
            mqe.Method = ExpectedMqNamedProperties != null && ExpectedMqNamedProperties.ContainsKey("httpMethod") ? ExpectedMqNamedProperties["httpMethod"] : string.Empty;
            mqe.UrlPath = ExpectedMqNamedProperties != null && ExpectedMqNamedProperties.ContainsKey("httpMethod") ? BuildDestinationUriFromConfig(ExpectedMqNamedProperties["httpMethod"]) : string.Empty;
            mqe.QueueName = QueueName;

            mqe.ErrorResponseHeaders = new List<ErrorResponseHeaders>();
            mqe.ErrorResponseHeaders.Add(new ErrorResponseHeaders { Key = "X-Status-Description", Value = error });
            mqe.ErrorResponseHeaders.Add(new ErrorResponseHeaders { Key = "X-Status-Reason", Value = "The json payload is invalid" });
            mqe.ErrorResponsePayload = null;

            // Instantiate the Message class because it was never created because of bad json 
            Message message = new Message();
            message.HttpHeaders = new MQMessageConsumerService.Models.HttpHeaders();
            message.HttpHeaders.MessagingProperties = (ExpectedMqNamedProperties != null) ? MessagingPropertiesToString(ExpectedMqNamedProperties) : "registrationVersion=rv1, httpMethod=POST, originSystem=ERROR";

            GenerateTopicError(message.HttpHeaders, mqe);
        }
        /// <summary>
        /// This function will process errors when the MQ message doesn't have the required Named Properties to the configured error topic
        /// </summary>
        /// <param name="ex"></param>
        private void CreateTopicError(MqNamedProperitesNotFoundException ex)
        {
            MqError mqe = new MqError();
            mqe.OriginalMqMessage = OriginalMessage != null ? OriginalMessage : string.Empty;
            mqe.MessageRequestHeaders = null; //will never be filled because the message can't be deserialized
            mqe.MessageRequestPayload = null; //will never be filled because the message can't be deserialized
            mqe.HttpResponseStatusCode = "400";
            mqe.Method = ExpectedMqNamedProperties != null && ExpectedMqNamedProperties.ContainsKey("httpMethod") ? ExpectedMqNamedProperties["httpMethod"] : string.Empty;
            mqe.UrlPath = ExpectedMqNamedProperties != null && ExpectedMqNamedProperties.ContainsKey("httpMethod") && ExpectedMqNamedProperties.ContainsKey("registrationVersion") ? BuildDestinationUriFromConfig(ExpectedMqNamedProperties["httpMethod"]) : string.Empty;
            mqe.QueueName = QueueName;

            mqe.ErrorResponseHeaders = new List<ErrorResponseHeaders>();
            mqe.ErrorResponseHeaders.Add(new ErrorResponseHeaders { Key = "X-Status-Description", Value = "A MqNamedProperitesNotFoundException exception occurred. One or more required MQ Named Properties are missing from the message on the queue" });
            mqe.ErrorResponseHeaders.Add(new ErrorResponseHeaders { Key = "X-Status-Reason", Value = ex.Message });
            mqe.ErrorResponsePayload = null;

            // Instantiate the Message class because it was never created because of a bad message
            Message message = new Message();
            message.HttpHeaders = new MQMessageConsumerService.Models.HttpHeaders();
            message.HttpHeaders.MessagingProperties = "registrationVersion=rv1, httpMethod=POST, originSystem=ERROR";

            GenerateTopicError(message.HttpHeaders, mqe);
        }
        /// <summary>
        /// This function will process the unhandled exceptions caused by HttpResponseException's to the configured error topic
        /// </summary>
        /// <param name="ex"></param>
        private void CreateTopicError(HttpResponseException ex)
        {
            MqError mqe = new MqError();
            mqe.OriginalMqMessage = OriginalMessage != null ? OriginalMessage : string.Empty;
            mqe.MessageRequestHeaders = Message?.HttpHeaders;
            mqe.MessageRequestPayload = Message?.Payload;
            mqe.HttpResponseStatusCode = ((int)ex.Response.StatusCode).ToString();
            mqe.Method = ExpectedMqNamedProperties != null && ExpectedMqNamedProperties.ContainsKey("httpMethod") ? ExpectedMqNamedProperties["httpMethod"] : string.Empty;
            mqe.UrlPath = ExpectedMqNamedProperties != null && ExpectedMqNamedProperties.ContainsKey("httpMethod") ? BuildDestinationUriFromConfig(ExpectedMqNamedProperties["httpMethod"]) : string.Empty;
            mqe.QueueName = QueueName;

            mqe.ErrorResponseHeaders = new List<ErrorResponseHeaders>();
            mqe.ErrorResponseHeaders.Add(new ErrorResponseHeaders { Key = "X-Status-Description", Value = !string.IsNullOrEmpty(ex.Response?.Content?.ReadAsStringAsync().Result) ? ex.Response.Content.ReadAsStringAsync().Result : ex.Message });
            mqe.ErrorResponseHeaders.Add(new ErrorResponseHeaders { Key = "X-Status-Reason", Value = "An unhandled exception occurred" });
            mqe.ErrorResponsePayload = null;

            GenerateTopicError(Message.HttpHeaders, mqe);
        }
        /// <summary>
        /// This function will process unsuccessful returns from the api's to the configured error topic
        /// </summary>
        /// <param name="httpRequestMessage"></param>
        /// <param name="httpResponseMessage"></param>
        private void CreateTopicError(HttpRequestMessage httpRequestMessage, HttpResponseMessage httpResponseMessage)
        {
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                Converters = new List<JsonConverter> { new StringEnumConverter() }
            };

            int statusCode = (int)httpResponseMessage.StatusCode;

            MqError mqe = new MqError();
            mqe.OriginalMqMessage = OriginalMessage != null ? OriginalMessage : string.Empty;
            mqe.MessageRequestHeaders = null;
            if (Message?.HttpHeaders != null)
            {
                Dictionary<string, object> dic = ToDictionary(Message.HttpHeaders);
                // remove null values from the dictionary
                dic = dic.Where(item => item.Value != null).ToDictionary(i => i.Key, i => i.Value);

                mqe.MessageRequestHeaders = JToken.Parse(JsonConvert.SerializeObject(dic.ToList(), settings));
            }
            mqe.MessageRequestPayload = null;
            if (Message?.Payload != null)
            {
                mqe.MessageRequestPayload = Message.Payload;
            }
            mqe.HttpResponseStatusCode = statusCode.ToString();
            mqe.Method = string.Empty;
            if (ExpectedMqNamedProperties != null && ExpectedMqNamedProperties.ContainsKey("httpMethod"))
            {
                mqe.Method = ExpectedMqNamedProperties["httpMethod"];
            }
            mqe.UrlPath = httpRequestMessage.RequestUri.ToString();
            mqe.QueueName = QueueName;

            mqe.ErrorResponseHeaders = CreateErrorResponseHeaders(httpResponseMessage);
            mqe.ErrorResponsePayload = null;

            GenerateTopicError(Message.HttpHeaders, mqe);
        }
        /// <summary>
        /// This function is the catch all for creating error topic unhandled exceptions
        /// </summary>
        /// <param name="ex"></param>
        private void CreateTopicError(Exception ex)
        {
            MqError mqe = new MqError();
            mqe.OriginalMqMessage = OriginalMessage != null ? OriginalMessage : string.Empty;
            mqe.MessageRequestHeaders = Message?.HttpHeaders;
            mqe.MessageRequestPayload = Message?.Payload;
            mqe.HttpResponseStatusCode = "500";
            mqe.Method = (ExpectedMqNamedProperties != null && ExpectedMqNamedProperties.ContainsKey("httpMethod")) ? ExpectedMqNamedProperties["httpMethod"] : string.Empty;
            mqe.UrlPath = (ExpectedMqNamedProperties != null && ExpectedMqNamedProperties.ContainsKey("httpMethod")) ? BuildDestinationUriFromConfig(ExpectedMqNamedProperties["httpMethod"]) : string.Empty;
            mqe.QueueName = QueueName;

            mqe.ErrorResponseHeaders = new List<ErrorResponseHeaders>();
            mqe.ErrorResponseHeaders.Add(new ErrorResponseHeaders { Key = "X-Status-Description", Value = ex.Message });
            mqe.ErrorResponseHeaders.Add(new ErrorResponseHeaders { Key = "X-Status-Reason", Value = "An unhandled exception occurred" });
            mqe.ErrorResponsePayload = null;

            GenerateTopicError(Message.HttpHeaders, mqe);
        }
        /// <summary>
        /// This function will generate all topic errors
        /// </summary>
        /// <param name="headers"></param>
        /// <param name="fje"></param>
        private void GenerateTopicError(MQMessageConsumerService.Models.HttpHeaders headers, MqError fje)
        {
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                Converters = new List<JsonConverter> { new StringEnumConverter() }
            };

            // serialize the FieldJobError error into json
            JToken jToken = JToken.Parse(JsonConvert.SerializeObject(fje, settings));

            Dictionary<string, string> requestHeaders = new Dictionary<string, string>();
            // Required header
            requestHeaders.Add("Messaging-Properties", headers.MessagingProperties);
            requestHeaders.Add("Messaging-Topic", TopicErrorName);

            Dictionary<string, string> contentHeaders = null;
            if (headers != null)
            {
                contentHeaders = new Dictionary<string, string>();
                // Required header
                contentHeaders.Add("Content-Application-Name", !string.IsNullOrEmpty(headers.ContentApplicationName) ? headers.ContentApplicationName : ConfigurationManager.AppSettings.Get("ServiceName"));
                // Required header
                contentHeaders.Add("Content-User-Id", !string.IsNullOrEmpty(headers.ContentUserId) ? headers.ContentUserId : ConfigurationManager.AppSettings.Get("ServiceName"));
                if (headers.ContentTrackingId != null)
                {
                    contentHeaders.Add("Content-Tracking-Id", headers.ContentTrackingId);
                }
                if (headers.ContentMock != null)
                {
                    contentHeaders.Add("Content-Mock", headers.ContentMock);
                }
                if (headers.ContentRoles != null)
                {
                    contentHeaders.Add("Content-Roles", headers.ContentRoles);
                }
                if (!string.IsNullOrEmpty(headers.ContentMqXCorrelationId))
                {
                    contentHeaders.Add("Content-Mq-X-Correlation-Id", (headers.ContentMqXCorrelationId != null) ? headers.ContentMqXCorrelationId : string.Empty);
                }
            }

            HttpResponseMessage resp = PostServiceCall(ApiTopicUri, jToken, ApiTopicCredentials, requestHeaders, contentHeaders);

            if (resp.IsSuccessStatusCode)
            {
                // write to the machine event log
                Log(EventLogEntryType.Information, "The message was successfully sent to the error topic: " + TopicErrorName, fje, null, resp);
            }
            else
            {
                // write to the machine event log
                Log(EventLogEntryType.Error, "POST to the topic MQApi failed.", ToJTokenOrString(SerializeMqMessage(Message)), null, resp);
            }
        }
        /// <summary>
        /// Construct HttpContent from the Message
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        private HttpContent CreateHttpContent(Message message)
        {
            // load HttpContent with the json message payload
            string payload = JsonConvert.SerializeObject(message.Payload, Formatting.None);
            HttpContent content = new StringContent(payload);

            // load the ContentType into the HttpContent headers
            content.Headers.ContentType = MediaTypeHeaderValue.Parse(message.HttpHeaders.ContentType);

            return content;
        }
        /// <summary>
        /// Create HttpRequestMessage from the Message loaded from the json payload
        /// </summary>
        /// <param name="message"></param>
        /// <returns>HttpRequestMessage</returns>
        private HttpRequestMessage CreateHttpRequestMessage(Message message)
        {
            string transformUrlParamsApiUri = string.Empty;
            string destinationApiResourceParams = string.Empty;

            switch (MessagingProperties["httpMethod"].ToString())
            {
                case "POST":
                    transformUrlParamsApiUri = TransformPostUrlParamsApiUri;
                    destinationApiResourceParams = DestinationApiPostResourceParams;

                    break;
                case "PUT":
                    transformUrlParamsApiUri = TransformPutUrlParamsApiUri;
                    destinationApiResourceParams = DestinationApiPutResourceParams;

                    break;
                case "PATCH":
                    transformUrlParamsApiUri = TransformPatchUrlParamsApiUri;
                    destinationApiResourceParams = DestinationApiPatchResourceParams;

                    break;
                case "DELETE":
                    transformUrlParamsApiUri = TransformDeleteUrlParamsApiUri;
                    destinationApiResourceParams = DestinationApiDeleteResourceParams;

                    break;
                default:

                    break;
            }

            List<string> urlParams = new List<string>();
            if (MessagingProperties.ContainsKey("urlParams") && !string.IsNullOrEmpty(MessagingProperties["urlParams"]))
            {
                if (IsValidTransformationCriteria(MessagingProperties["httpMethod"].ToString()))
                {
                    //transform urlParams in message-properties
                    string transformedUrlParams = TransformMessagingPropertiesUrlParams(transformUrlParamsApiUri, MessagingProperties["urlParams"], message);
                    if (!string.IsNullOrEmpty(transformedUrlParams))
                    {
                        urlParams = transformedUrlParams?.Split('|')?.ToList();
                    }
                }
                else
                {
                    urlParams = MessagingProperties["urlParams"]?.Split('|')?.ToList();
                }
            }

            HttpRequestMessage request = CreateHttpRequestMessage(message, destinationApiResourceParams, urlParams);

            return request;
        }
        /// <summary>
        /// Construct the complete HttpRequestMessage
        /// </summary>
        /// <param name="message"></param>
        /// <param name="destinationApiResourceParams"></param>
        /// <param name="urlParams"></param>
        /// <returns></returns>
        private HttpRequestMessage CreateHttpRequestMessage(Message message, string destinationApiResourceParams, List<string> urlParams)
        {
            HttpRequestMessage request = new HttpRequestMessage(new HttpMethod(MessagingProperties["httpMethod"].ToString()), UriBuilder(destinationApiResourceParams, urlParams))
            {
                Content = CreateHttpContent(message)
            };

            // add the custom json httpHeaders to the request
            AddHttpHeadersToRequest(message.HttpHeaders, request);

            return request;
        }
        /// <summary>
        /// Send the json created HttpRequestMessage message to the destination api
        /// </summary>
        /// <param name="httpRequestMessage"></param>
        /// <returns>HttpResponseMessage</returns>
        private HttpResponseMessage ProcessMessage(HttpRequestMessage httpRequestMessage)
        {
            HttpResponseMessage httpResponseMessage = ApiCall(httpRequestMessage);

            return httpResponseMessage;
        }
        /// <summary>
        /// Create the destination uri based on "urlParams" and configured values
        /// </summary>
        /// <param name="destinationApiResourceParam"></param>
        /// <returns>Uri</returns>
        private Uri UriBuilder(string destinationApiResourceParam, List<string> urlParams)
        {
            // build the base uri
            UriBuilder uri = new UriBuilder(ApiDestinationScheme, ApiDestinationHost, ApiDestinationPort, DestinationApiPath + "/" + MessagingProperties["registrationVersion"].Remove(0, 1));

            // get the count of the api resource params to be replaced
            List<string> listUriPath = destinationApiResourceParam.Split('/')?.ToList();
            int countResourceParams = CountDestinationApiResourceParams(listUriPath);

            // build the rest of the uri - map the destinationApiResourceParam with it's corresponding MessagingProperties urlParams values.
            string resource = destinationApiResourceParam;
            for (int i = 0; i < countResourceParams; i++)
            {
                string urlParam = string.Empty;
                if (i <= urlParams.Count && urlParams.Count > 0)
                {
                    urlParam = urlParams[i].Trim();
                }

                resource = resource.Replace("{" + i + "}", urlParam);
            }

            uri.Path += "/" + resource;

            return uri.Uri;
        }
        /// <summary>
        /// Transform the UrlParams in the MessagingProperties header the transformed UrlParams
        /// </summary>
        /// <param name="transformUrlParamsApiUri"></param>
        /// <returns></returns>
        private string TransformMessagingPropertiesUrlParams(string transformUrlParamsApiUri, string originalUrlParams, Message message)
        {
            HttpRequestMessage request = new HttpRequestMessage(new HttpMethod("POST"), UriBuilder(TransformPostUrlParamsApiUri, new List<string>() { originalUrlParams }))
            {
                Content = CreateHttpContent(message)
            };

            Dictionary<string, string> headers = new Dictionary<string, string>();
            headers["Content-User-Id"] = message?.HttpHeaders?.ContentUserId;
            headers["Content-Application-Name"] = message?.HttpHeaders?.ContentApplicationName;
            headers["Message-Id"] = message?.HttpHeaders?.MessageId;
            headers["Message-Correlation-Id"] = message?.HttpHeaders?.MessageCorrelationId;
            headers["Messaging-Queue"] = message?.HttpHeaders?.MessagingQueue;
            headers["Messaging-Topic"] = message?.HttpHeaders?.MessagingTopic;
            headers["Messaging-Properties"] = message?.HttpHeaders?.MessagingProperties;

            if (headers != null && headers.Count > 0)
            {
                foreach (var header in headers)
                {
                    request.Headers.Add(header.Key, header.Value);
                }
            }

            Dictionary<string, string> dic = new Dictionary<string, string>();
            dic["urlParams"] = MessagingProperties["urlParams"];
            dic["transformUrlParams"] = MessagingProperties["transformUrlParams"];

            if (dic != null && headers.Count > 0)
            {
                request.RequestUri = AddQueryStrings(request, dic);
            }

            HttpResponseMessage transformedUrlParamsResponse = ApiCall(request);

            string urlParams = string.Empty;
            if (!transformedUrlParamsResponse.IsSuccessStatusCode)
            {
                throw new HttpResponseException(transformedUrlParamsResponse);
            }

            urlParams = JsonConvert.DeserializeObject<string>(transformedUrlParamsResponse.Content.ReadAsStringAsync().Result);

            return urlParams;
        }
        /// <summary>
        /// Determine if the transformation is valid for the given httpMethod
        /// </summary>
        /// <param name="httpMethod"></param>
        /// <returns></returns>
        private bool IsValidTransformationCriteria(string httpMethod)
        {
            switch (httpMethod)
            {
                case "POST":
                    if (!string.IsNullOrEmpty(TransformPostUrlParamsApiUri))
                    {
                        return IsMessagingPropertiesContainTransformTrigger(TransformPostTrigger);
                    }
                    break;
                case "PUT":
                    if (!string.IsNullOrEmpty(TransformPutUrlParamsApiUri))
                    {
                        return IsMessagingPropertiesContainTransformTrigger(TransformPutTrigger);
                    }
                    break;
                case "PATCH":
                    if (!string.IsNullOrEmpty(TransformPatchUrlParamsApiUri))
                    {
                        return IsMessagingPropertiesContainTransformTrigger(TransformPatchTrigger);
                    }
                    break;
                case "DELETE":
                    if (!string.IsNullOrEmpty(TransformDeleteUrlParamsApiUri))
                    {
                        return IsMessagingPropertiesContainTransformTrigger(TransformDeleteTrigger);
                    }
                    break;

                default:
                    break;
            }

            return false;
        }
        /// <summary>
        /// Determine if the TransformTrigger configured values are in the MessagingProperties of the header
        /// </summary>
        /// <param name="configuredTransformTrigger"></param>
        /// <returns></returns>
        private bool IsMessagingPropertiesContainTransformTrigger(string configuredTransformTrigger)
        {
            if (!string.IsNullOrEmpty(configuredTransformTrigger) && configuredTransformTrigger.IndexOf("=", StringComparison.Ordinal) > 0)
            {
                //split from left of the "="
                string configurationName = configuredTransformTrigger.Substring(0, configuredTransformTrigger.IndexOf("=", StringComparison.Ordinal));
                //split from right of the "="
                string configurationValue = configuredTransformTrigger.Substring(configuredTransformTrigger.LastIndexOf('=') + 1);

                //put into an array split by ","
                string[] configurationValues = configurationValue
                    .Split(new char[] {','}, StringSplitOptions.RemoveEmptyEntries).ToArray();

                //find the configured
                foreach (var item in configurationValues)
                {
                    if (MessagingProperties.Contains(new KeyValuePair<string, string>(configurationName, item)))
                    {
                        return true;
                    }
                }
            }

            //Dictionary<string, string> dic = ToDictionary(transformTrigger);

            //foreach (var item in dic)
            //{
            //    if (MessagingProperties.Contains(new KeyValuePair<string, string>(item.Key, item.Value)))
            //    {
            //        return true;
            //    }
            //}

            return false;
        }
        /// <summary>
        /// Determine if the MQ message should be processed based on messagingPropertiesFilter configuration value
        /// </summary>
        /// <param name="messagingProperties"></param>
        /// <returns></returns>
        private bool IsFilteredMessage(Dictionary<string, string> messagingProperties)
        {
            List<string> list = new List<string>();
            foreach (var item in MessagingPropertiesFilterElements)
            {
                if (item.UrlParam.StartsWith("originSystem"))
                {
                    list.Add(item.Value);
                }
            }

            if (list.Any(x => x == "*") || list.Any(x => x == MessagingProperties["originSystem"]))
            {
                return false;
            }

            return true;
        }
        /// <summary>
        /// Return missing required MQ "Named Properites" from the message. NULL if all required are present
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        private List<string> MissingRequiredMqNamedProperites(MQMessage obj)
        {
            List<string> list = GetMqNamedProperties(obj);

            var results = ListRequiredMqNamedProperties.Except(list != null ? list : new List<string>());

            if (results != null && results.ToList().Count() > 0)
            {
                return results.ToList();
            }

            return null;
        }

        /// <summary>
        /// Build the destination uri from the config
        /// </summary>
        /// <param name="httpMethod"></param>
        /// <returns></returns>
        private string BuildDestinationUriFromConfig(string httpMethod)
        {
            string uriPath = string.Empty;

            switch (httpMethod)
            {
                case "POST":
                    uriPath = new UriBuilder(ApiDestinationScheme, ApiDestinationHost, ApiDestinationPort, DestinationApiPath + "/" + ExpectedMqNamedProperties["registrationVersion"].Remove(0, 1)).ToString() + DestinationApiPostResourceParams;
                    break;

                case "PUT":
                    uriPath = new UriBuilder(ApiDestinationScheme, ApiDestinationHost, ApiDestinationPort, DestinationApiPath + "/" + ExpectedMqNamedProperties["registrationVersion"].Remove(0, 1)).ToString() + DestinationApiPutResourceParams;
                    break;

                case "PATCH":
                    uriPath = new UriBuilder(ApiDestinationScheme, ApiDestinationHost, ApiDestinationPort, DestinationApiPath + "/" + ExpectedMqNamedProperties["registrationVersion"].Remove(0, 1)).ToString() + DestinationApiPatchResourceParams;
                    break;

                case "DELETE":
                    uriPath = new UriBuilder(ApiDestinationScheme, ApiDestinationHost, ApiDestinationPort, DestinationApiPath + "/" + ExpectedMqNamedProperties["registrationVersion"].Remove(0, 1)).ToString() + DestinationApiDeleteResourceParams;
                    break;
            }

            return uriPath;
        }
        /// <summary>
        /// If the json is valid return json otherwise return the original escaped string
        /// </summary>
        /// <param name="original"></param>
        /// <returns></returns>
        private dynamic ToJTokenOrString(string original)
        {
            dynamic obj;

            try
            {
                obj = JToken.Parse(original);
            }
            catch
            {
                obj = original;
            }

            return obj;
        }

        private dynamic SerializeMqMessage(Message message)
        {
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                Converters = new List<JsonConverter> { new StringEnumConverter() }
            };

            if (message != null)
            {
                //serialize the message httpHeaders
                dynamic httpHeaders = JToken.Parse(JsonConvert.SerializeObject(message.HttpHeaders, Formatting.Indented, settings));
                dynamic payload = message.Payload;

                //combine the httpHeaders and payload into a dynamic message object
                dynamic msg = new
                {
                    httpHeaders = httpHeaders,
                    payload = payload
                };

                //serialize the dynamic message object
                return JsonConvert.SerializeObject(msg, Formatting.Indented, settings);
            }

            return null;
        }

        /// <summary>
        /// Count the number of resource paramters
        /// </summary>
        /// <param name="resourceParams"></param>
        /// <returns></returns>
        private int CountDestinationApiResourceParams(List<string> resourceParams)
        {
            int i = 0;
            foreach (var item in resourceParams)
            {
                if (item.Split('{', '}').Length > 1)
                {
                    i++;
                }
            }

            return i;
        }
        private static Dictionary<string, string> ToDictionary(string formattedString)
        {
            string[] array = formattedString.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToArray();
            Dictionary<string, string> dic = array.Select(x => x.Split('=')).ToDictionary(y => y[0], y => y[1]);

            return dic;
        }
        private Dictionary<string, object> ToDictionary(object obj)
        {
            return Message.HttpHeaders.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public).ToDictionary(prop => prop.Name, prop => prop.GetValue(obj, null));
        }
        private List<ErrorResponseHeaders> CreateErrorResponseHeaders(HttpResponseMessage httpResponseMessage)
        {
            List<ErrorResponseHeaders> ErrorResponseHeaders = new List<ErrorResponseHeaders>();

            foreach (var item in httpResponseMessage.Headers)
            {
                List<string> list = item.Value.ToList();

                foreach (var value in list)
                {
                    ErrorResponseHeaders head = new ErrorResponseHeaders();
                    head.Key = item.Key;
                    head.Value = value;
                    ErrorResponseHeaders.Add(head);
                }
            }

            return ErrorResponseHeaders;
        }
        private static void AddHttpHeadersToRequest(MQMessageConsumerService.Models.HttpHeaders httpHeaders, HttpRequestMessage request)
        {
            // add deseriaized message "httpHeaders" to the new request
            PropertyInfo[] properties = typeof(MQMessageConsumerService.Models.HttpHeaders).GetProperties();
            foreach (PropertyInfo property in properties)
            {
                var prop = ((JsonPropertyAttribute[])property.GetCustomAttributes(typeof(JsonPropertyAttribute))).FirstOrDefault().PropertyName;
                if (prop != "Content-Type")
                {
                    if (property.GetValue(httpHeaders) != null)
                    {
                        var value = property.GetValue(httpHeaders).ToString();

                        request.Headers.Add(prop, value);
                    }
                }
            }
        }
        /// <summary>
        /// Convert the Messaging-Propeties custom header into a dictionary
        /// </summary>
        /// <param name="messagingProperties"></param>
        /// <returns></returns>
        private Dictionary<string, string> MessagingPropertiesToDictionary(string messagingProperties)
        {
            Dictionary<string, string> dic = ToDictionary(messagingProperties);

            //if httpMethod is not there it's a POST
            if (!dic.ContainsKey("httpMethod"))
            {
                dic.Add("httpMethod", "POST");
            }

            return dic;
        }
        /// <summary>
        /// Return a List of MQ "Named Properties" that are on the MQ message
        /// </summary>
        /// <param name="mqMessage"></param>
        /// <returns></returns>
        private List<string> GetMqNamedProperties(MQMessage mqMessage)
        {
            IEnumerator mqMessageNamedProperties = mqMessage.GetPropertyNames("%");

            if (mqMessageNamedProperties.MoveNext() == true)
            {
                List<string> list = new List<string>();
                mqMessageNamedProperties.Reset();

                while (mqMessageNamedProperties.MoveNext() == true)
                {
                    list.Add(mqMessageNamedProperties.Current.ToString());
                }

                return list;
            }

            return null;
        }
        /// <summary>
        /// Used to get the expected "Named Properties" from the MQ message - will filter out all non-essential "Named Properties"
        /// </summary>
        /// <param name="mqMessage"></param>
        /// <returns></returns>
        private Dictionary<string, string> GetExpectedMqNamedProperties(MQMessage mqMessage)
        {
            return AddHttpMethodPost(FilterExpectedMqNamedProperties(MqNamedPropertiesToDictionary(mqMessage)));
        }
        /// <summary>
        /// Retrieves all of the MQ message "Named Properties" 
        /// </summary>
        /// <param name="mqMessage"></param>
        /// <returns></returns>
        private Dictionary<string, string> MqNamedPropertiesToDictionary(MQMessage mqMessage)
        {
            Dictionary<string, string> dic = new Dictionary<string, string>();

            IEnumerator mqMessageNamedProperties = mqMessage.GetPropertyNames("%");
            if (mqMessageNamedProperties.MoveNext() == true)
            {
                dic = new Dictionary<string, string>();
                mqMessageNamedProperties.Reset();

                while (mqMessageNamedProperties.MoveNext() == true)
                {
                    try
                    {
                        string mqMessagePropertyName = string.Empty;
                        string mqMessagePropertyValue = string.Empty;

                        mqMessagePropertyName = mqMessageNamedProperties.Current.ToString();
                        mqMessagePropertyValue = mqMessage.GetObjectProperty(mqMessagePropertyName)?.ToString();

                        dic.Add(mqMessagePropertyName, mqMessagePropertyValue);
                    }
                    catch (MQException ex)
                    {
                        // the property is null, so keep processing
                    }
                }

                return dic;
            }

            return null;
        }
        /// <summary>
        /// Filters the message "Named Properties" to only the ListExpectedMqNamedProperties ones
        /// </summary>
        /// <param name="originalDictionary"></param>
        /// <returns></returns>
        private Dictionary<string, string> FilterExpectedMqNamedProperties(Dictionary<string, string> originalDictionary)
        {
            if (originalDictionary != null)
            {
                Dictionary<string, string> expectedDictionary = new Dictionary<string, string>();
                foreach (var item in originalDictionary)
                {
                    if (ListExpectedMqNamedProperties.Any(x => x == item.Key))
                    {
                        expectedDictionary.Add(item.Key, item.Value);
                    }
                }

                return expectedDictionary;
            }

            return null;
        }
        /// <summary>
        /// Add the default POST httpMethod to the "Named Properties" dictionary
        /// </summary>
        /// <param name="namedProperties"></param>
        /// <returns></returns>
        private Dictionary<string, string> AddHttpMethodPost(Dictionary<string, string> namedProperties)
        {
            Dictionary<string, string> dic = new Dictionary<string, string>();

            if (namedProperties != null)
            {
                dic = namedProperties;

                //if httpMethod is not there it's a POST
                if (!dic.ContainsKey("httpMethod"))
                {
                    dic.Add("httpMethod", "POST");
                }
            }
            else
            {
                dic.Add("httpMethod", "POST");
            }

            return dic;
        }
        /// <summary>
        /// This function converts the Messaging-Propeties custom header dictionary into a string
        /// </summary>
        /// <param name="messagingProperties"></param>
        /// <returns></returns>
        private string MessagingPropertiesToString(Dictionary<string, string> messagingProperties)
        {
            // remove commas add equals sign
            List<string> list = messagingProperties.Select(x => x.Key + "=" + x.Value).ToList();

            // move registrationVersion to the front of the list per WE standard
            int registrationVersionIndex = list.FindIndex(x => x.StartsWith("registrationVersion"));
            string registrationVersionValue = list[registrationVersionIndex];

            list.RemoveAt(registrationVersionIndex);
            list.Insert(0, registrationVersionValue);

            // create comma separated string from list
            string joinedList = string.Join(",", list);

            return joinedList;
        }

        private HttpResponseMessage ApiCall(HttpRequestMessage request)
        {
            HttpResponseMessage response = null;

            using (var client = new HttpClient())
            {
                if (!string.IsNullOrEmpty(ConfigurationManager.AppSettings.Get("HttpClientTimeOut")))
                {
                    client.Timeout = TimeSpan.FromMilliseconds(Convert.ToDouble(ConfigurationManager.AppSettings.Get("HttpClientTimeOut")));
                }
                client.BaseAddress = request.RequestUri;
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", ApiDestinationCredentials);

                if (ApiDestinationScheme == "http")
                {
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls11;
                }
                if (ApiDestinationScheme == "https")
                {
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                }

                MessageHeaderRequestTimestamp = DateTime.Now.ToString();
                response = client.SendAsync(request).Result;
            }

            return response;
        }
        public HttpResponseMessage PostServiceCall(string requestUri, JToken payload, string credentials = null, Dictionary<string, string> requestHeaders = null, Dictionary<string, string> contentHeaders = null)
        {
            HttpResponseMessage response = null;

            using (var client = new HttpClient())
            {
                if (!string.IsNullOrEmpty(ConfigurationManager.AppSettings.Get("HttpClientTimeOut")))
                {
                    client.Timeout = TimeSpan.FromMilliseconds(Convert.ToDouble(ConfigurationManager.AppSettings.Get("HttpClientTimeOut")));
                }
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                if (!string.IsNullOrEmpty(credentials))
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
                }

                if (requestHeaders != null)
                {
                    foreach (var header in requestHeaders)
                    {
                        client.DefaultRequestHeaders.Add(header.Key, header.Value);
                    }
                }

                System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestUri)
                {
                    Content = new StringContent(payload.ToString(), System.Text.Encoding.UTF8)
                };

                if (contentHeaders != null)
                {
                    foreach (var header in contentHeaders)
                    {
                        request.Content.Headers.Add(header.Key, header.Value);
                    }
                }

                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                MessageHeaderRequestTimestamp = DateTime.Now.ToString();
                response = client.SendAsync(request).Result;
            }

            return response;
        }
        private Uri AddQueryStrings(HttpRequestMessage request, Dictionary<string, string> queryStrings)
        {
            UriBuilder builder = new UriBuilder(request.RequestUri);
            var query = HttpUtility.ParseQueryString(builder.Query);

            foreach (var item in queryStrings)
            {
                query[item.Key] = item.Value;
            }

            builder.Query = query.ToString();

            Uri uri = new Uri(builder.ToString());

            return uri;
        }

        /// <summary>
        /// Will deserialize into T and validate json against the schema of T
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="jsonMessage"></param>
        /// <returns></returns>
        private T DeserializeAndValidateJsonSchema<T>(string jsonMessage) where T : new()
        {
            IList<string> errorMessages = new List<string>();

            string jsonSchema = GenerateJsonSchema<T>();

            JsonValidatingReader validatingReader = new JsonValidatingReader(new JsonTextReader(new System.IO.StringReader(jsonMessage)));
            validatingReader.Schema = JsonSchema.Parse(jsonSchema);
            validatingReader.ValidationEventHandler += (o, a) => errorMessages.Add(a.Message);

            JsonSerializer serializer = new JsonSerializer();
            T obj = serializer.Deserialize<T>(validatingReader);  // will raise JsonSerializationException if fail

            return obj;
        }
        /// <summary>
        /// Generate a json schema of T to be validated against
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        private string GenerateJsonSchema<T>()
        {
            var jsonSchemaGenerator = new JsonSchemaGenerator();
            var myType = typeof(T);
            var schema = jsonSchemaGenerator.Generate(myType);
            schema.Title = myType.Name;

            return schema.ToString();
        }

        /// <summary>
        /// Gets the messages off of the queue
        /// </summary>
        /// <returns></returns>
        private MQMessage GetMqMessage()
        {
            queue = queueManager.AccessQueue(QueueName, MQC.MQOO_INPUT_AS_Q_DEF + MQC.MQOO_FAIL_IF_QUIESCING);
            MQGetMessageOptions getMessageOptions = new MQGetMessageOptions();
            getMessageOptions.Options += MQC.MQGMO_WAIT + MQC.MQGMO_SYNCPOINT;
            getMessageOptions.WaitInterval = 20000; //20 Seconds 

            MQMessage message = new MQMessage();
            queue.Get(message, getMessageOptions);
            queue.Close();

            return message;
        }
    }
}