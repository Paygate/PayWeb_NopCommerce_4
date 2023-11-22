using Nop.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nop.Plugin.Payments.PayGate.Models
{
    public class OrderPayMethodRecord : BaseEntity
    {
        public string OrderGuid { get; set; }
        public string PayMethod { get; set; }
    }
}
