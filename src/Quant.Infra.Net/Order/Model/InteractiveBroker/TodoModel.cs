using System.Collections.Generic;

namespace Quant.Infra.Net.Exchange.Model.InteractiveBroker
{
    /// <summary>
    /// CentralProcessService生成的TodoModel， 包括Order和需要发送的Notification
    /// </summary>
    public class TodoModel
    {
        public TodoModel() {
            Orders = new List<OrderAbstract>();
        }
        public List<OrderAbstract> Orders { get; set; }

        public string Notification { get; set; }
    }
}
