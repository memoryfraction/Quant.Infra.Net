using Quant.Infra.Net.Shared.Model;
using System.Threading.Tasks;


namespace Quant.Infra.Net.Exchange.Service
{
    /// <summary>
    /// IIBKRService， 定义IBKR的服务接口
    /// </summary>
    public interface IIBKRService
    {
        // 获取账户信息


        #region Orders
        /// <summary>
        /// 创建订单，并返回int类型的OrderId
        /// </summary>
        /// <param name="order"></param>
        /// <param name="exchange"></param>
        /// <param name="securityType"></param>
        /// <param name="currency"></param>
        /// <returns></returns>
        Task<int> PlaceOrderAsync(OrderAbstract order, string exchange = "SMART", ContractSecurityType securityType = ContractSecurityType.Stock, Currency currency = Currency.USD);

        // Task<Order> GetOrderAsync(int orderId);

        // Task<IEnumerable<Order>> GetOrdersAsync(IEnumerable<int> orderIds);
       
        #endregion

        // 获取持仓信息


        // 获取历史数据


        // 获取实时数据

    }
}
