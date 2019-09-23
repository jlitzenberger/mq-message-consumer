using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MQMessageConsumerService.Models
{
    public class MqError
    {
        public dynamic OriginalMqMessage { get; set; }
        public dynamic MessageRequestHeaders { get; set; }
        public dynamic MessageRequestPayload { get; set; }
        public string Method { get; set; }
        public string QueueName { get; set; }
        public string UrlPath { get; set; }
        public string HttpResponseStatusCode { get; set; }
        public List<ErrorResponseHeaders> ErrorResponseHeaders { get; set; }
        public string ErrorResponsePayload { get; set; }
    }
}
