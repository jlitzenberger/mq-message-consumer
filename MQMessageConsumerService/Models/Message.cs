using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace MQMessageConsumerService.Models
{
    public class Message
    {
        [JsonProperty(PropertyName = "HttpHeaders", Required = Required.Always)]
        public HttpHeaders HttpHeaders { get; set; }
        [JsonProperty(PropertyName = "Payload", Required = Required.Always)]
        public dynamic Payload { get; set; }
    }

    public class HttpHeaders
    {
        [JsonProperty(PropertyName = "Content-Application-Name", Required = Required.Always)]
        public string ContentApplicationName { get; set; }
        [JsonProperty(PropertyName = "Content-Mock")]
        public string ContentMock { get; set; }
        [JsonProperty(PropertyName = "Content-Roles")]
        public string ContentRoles { get; set; }
        [JsonProperty(PropertyName = "Content-Tracking-Id")]
        public string ContentTrackingId { get; set; }
        [JsonProperty(PropertyName = "Content-Type", Required = Required.Always)]
        public string ContentType { get; set; }
        [JsonProperty(PropertyName = "Content-User-Id", Required = Required.Always)]
        public string ContentUserId { get; set; }
        [JsonProperty(PropertyName = "Messaging-Priority")]
        public string MessagingPriority { get; set; }
        [JsonProperty(PropertyName = "Messaging-Properties", Required = Required.Always)]
        public string MessagingProperties { get; set; }
        [JsonProperty(PropertyName = "Content-Mq-X-Correlation-Id")]
        public string ContentMqXCorrelationId { get; set; }


        [JsonProperty(PropertyName = "Messaging-Queue")]
        public string MessagingQueue { get; set; }
        [JsonProperty(PropertyName = "Messaging-Topic")]
        public string MessagingTopic { get; set; }
        [JsonProperty(PropertyName = "Message-Id")]
        public string MessageId { get; set; }
        [JsonProperty(PropertyName = "Message-Correlation-Id")]
        public string MessageCorrelationId { get; set; }       
    }
}
