using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MQMessageConsumer.Data.Configuration
{
    public class ConsumerConfigurationManagerSection : ConfigurationSection
    {
        /// <summary>
        /// The name of this section in the app.config.
        /// </summary>
        public const string SectionName = "messageConfigurationSection";

        private const string MessageLoggingCollectionName = "messageLogging";
        [ConfigurationProperty(MessageLoggingCollectionName)]
        [ConfigurationCollection(typeof(MessageLoggingConfigurationCollection), AddItemName = "add")]
        public MessageLoggingConfigurationCollection MessageLoggings
        {
            get
            {
                return (MessageLoggingConfigurationCollection)base[MessageLoggingCollectionName];
            }
        }
        private const string MessagingPropertiesFilterCollectioName = "messagingPropertiesFilter";
        [ConfigurationProperty(MessagingPropertiesFilterCollectioName)]
        [ConfigurationCollection(typeof(MessagePropertiesFilterConfigurationCollection), AddItemName = "add")]
        public MessagePropertiesFilterConfigurationCollection MessagePropertiesFilters
        {
            get
            {
                return (MessagePropertiesFilterConfigurationCollection)base[MessagingPropertiesFilterCollectioName];
            }
        }
    }
    public class MessageLoggingConfigurationCollection : ConfigurationElementCollection
    {
        protected override ConfigurationElement CreateNewElement()
        {
            return new MessageLoggingElement();
        }
        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((MessageLoggingElement)element).UncFileLocation;
        }
    }
    public class MessageLoggingElement : ConfigurationElement
    {
        [ConfigurationProperty("uncFileLocation", IsRequired = true)]
        public string UncFileLocation
        {
            get { return (string)this["uncFileLocation"]; }
            set { this["uncFileLocation"] = value; }
        }

        [ConfigurationProperty("active", IsRequired = true, DefaultValue = true)]
        public bool Active
        {
            get { return (bool)this["active"]; }
            set { this["active"] = value; }
        }
    }
    public class MessagePropertiesFilterConfigurationCollection : ConfigurationElementCollection
    {
        protected override ConfigurationElement CreateNewElement()
        {
            return new MessagingPropertiesFilterElement();
        }
        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((MessagingPropertiesFilterElement)element).UrlParam;
        }
    }
    public class MessagingPropertiesFilterElement : ConfigurationElement
    {
        [ConfigurationProperty("urlParam", IsRequired = true)]
        public string UrlParam
        {
            get { return (string)this["urlParam"]; }
            set { this["urlParam"] = value; }
        }

        [ConfigurationProperty("value", IsRequired = true)]
        public string Value
        {
            get { return (string)this["value"]; }
            set { this["value"] = value; }
        }
    }
}
