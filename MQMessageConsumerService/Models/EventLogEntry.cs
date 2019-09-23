using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace MQMessageConsumerService.Models
{
    public class EventLogEntry
    {
        public string Description { get; set; }
        public string MessageId { get; set; }
        public string CorrelationId { get; set; }
        public Dictionary<string, string> NamedProperties { get; set; }
        public string MessagePayloadLocaton { get; set; }
        public Exception Exception { get; set; }
        public HttpResponseMessage HttpResponseMessage { get; set; }
    }
}
