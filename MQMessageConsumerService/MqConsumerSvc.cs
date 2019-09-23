using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using IBM.WMQ;

namespace MQMessageConsumerService
{
    public partial class MqConsumerSvc : ServiceBase
    {
        private System.Timers.Timer _myTimer = null;
        private readonly object _timerLock = new object();

        public MqConsumerSvc()
        {
            InitializeComponent();

            if (!EventLog.SourceExists(ConfigurationManager.AppSettings.Get("SourceName")))
            {
                EventLog.CreateEventSource(ConfigurationManager.AppSettings.Get("SourceName"), ConfigurationManager.AppSettings.Get("EventViewerLogName"));
            }
            eventLog1.Source = ConfigurationManager.AppSettings.Get("SourceName");
            eventLog1.Log = ConfigurationManager.AppSettings.Get("EventViewerLogName");
        }

        protected override void OnStart(string[] args)
        {
            System.IO.Directory.SetCurrentDirectory(System.AppDomain.CurrentDomain.BaseDirectory);

            if (ConfigurationManager.AppSettings.Get("IsDebug") == "true")
            {
                while (!Debugger.IsAttached)
                {
                    System.Threading.Thread.Sleep(1000);
                }
                
                new MQMessageConsumer.Library(eventLog1).ProcessQueue();
            }
            else
            {
                eventLog1.WriteEntry("Service Started", EventLogEntryType.Information);

                _myTimer = new System.Timers.Timer();
                _myTimer.Interval = 1000; // 1 second
                _myTimer.AutoReset = false;
                _myTimer.Elapsed += new ElapsedEventHandler(this.OnTimer);
                _myTimer.Enabled = true;
            }
        }

        protected override void OnStop()
        {
            // wait for timer process to stop
            Monitor.Enter(_timerLock);
            // do shutdown tasks here
            eventLog1.WriteEntry("Service Stopped", EventLogEntryType.Warning);
        }

        protected void OnTimer(object sender, ElapsedEventArgs e)
        {
            //If you want the main program to go ahead and shut down after some period of time, regardless of whether it's obtained the lock, use Monitor.TryEnter. For example, this will wait 15 seconds.
            //bool gotLock = Monitor.TryEnter(_timerLock, TimeSpan.FromSeconds(15));

            if (!Monitor.TryEnter(_timerLock))
            {
                // something has the lock. Probably shutting down.
                return;
            }

            try
            {
                System.Collections.Hashtable connectionProperties = new System.Collections.Hashtable();
                connectionProperties.Add(IBM.WMQ.MQC.TRANSPORT_PROPERTY, IBM.WMQ.MQC.TRANSPORT_MQSERIES_XACLIENT);
                IBM.WMQ.MQQueueManager queueManager = new IBM.WMQ.MQQueueManager(ConfigurationManager.AppSettings.Get("QueueManagerName"), connectionProperties);
                // get message count
                int countOfMessages = queueManager.AccessQueue(ConfigurationManager.AppSettings.Get("QueueName"), IBM.WMQ.MQC.MQOO_INPUT_AS_Q_DEF + IBM.WMQ.MQC.MQOO_FAIL_IF_QUIESCING + IBM.WMQ.MQC.MQOO_INQUIRE).CurrentDepth;
                queueManager.Disconnect();

                if (ConfigurationManager.AppSettings.Get("IsDebug") != "true")
                {
                    int numOfThreads = Convert.ToInt32(ConfigurationManager.AppSettings.Get("ThreadCountMax"));

                    if (countOfMessages > numOfThreads * 3)
                    {
                        countOfMessages = numOfThreads * 3;
                    }

                    System.Threading.Tasks.Parallel.For(0, countOfMessages, new ParallelOptions { MaxDegreeOfParallelism = numOfThreads }, index =>
                    {
                        new MQMessageConsumer.Library(eventLog1).ProcessQueue();
                    });
                }
                else
                {
                    new MQMessageConsumer.Library(eventLog1).ProcessQueue();
                }
            }
            catch (MQException ex) // there is an error with IBM MQ
            {
                eventLog1.WriteEntry(ex.ToString(), EventLogEntryType.Warning);
            }
            catch (Exception ex)
            {
                eventLog1.WriteEntry(ex.ToString(), EventLogEntryType.Error);
            }
            finally
            {
                _myTimer.Start(); // re-enables the timer
                Monitor.Exit(_timerLock);
            }

        }
    }
}
