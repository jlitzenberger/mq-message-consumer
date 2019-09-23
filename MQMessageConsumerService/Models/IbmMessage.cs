using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MQMessageConsumerService.Models
{
    public class IbmMessage
    {
        public string MessageId { get; set; }
        public string CorrelationId { get; set; }
        public dynamic MessageBody { get; set; }
    }
}
