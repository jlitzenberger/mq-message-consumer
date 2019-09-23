using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MQMessageConsumerService.Models
{
    public class MqNamedProperitesNotFoundException : Exception
    {
        public MqNamedProperitesNotFoundException()
        {
        }

        public MqNamedProperitesNotFoundException(string message)
            : base(message)
        {
        }

        public MqNamedProperitesNotFoundException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
