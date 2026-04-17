using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Saas.Infra.Core.Schwab
{
    /// <summary>
    /// 嘉信理财账户服务接口。
    /// Charles Schwab account service interface.
    /// </summary>
    public interface ISchwabAccountService
    {
        /// <summary>
        /// 获取用户的所有账户号码。
        /// Gets all account numbers for the user.
        /// </summary>
        /// <param name="userId">用户 ID。 / User ID.</param>
        /// <returns>账户号码列表。 / List of account numbers.</returns>
        Task<IReadOnlyList<SchwabAccountNumber>> GetAccountNumbersAsync(Guid userId);

        /// <summary>
        /// 获取账户详细信息。
        /// Gets account details.
        /// </summary>
        /// <param name="userId">用户 ID。 / User ID.</param>
        /// <param name="accountHashValue">账户哈希值。 / Account hash value.</param>
        /// <returns>账户详情。 / Account details.</returns>
        Task<SchwabAccount> GetAccountAsync(Guid userId, string accountHashValue);

        /// <summary>
        /// 获取账户持仓。
        /// Gets account positions.
        /// </summary>
        /// <param name="userId">用户 ID。 / User ID.</param>
        /// <param name="accountHashValue">账户哈希值。 / Account hash value.</param>
        /// <returns>持仓列表。 / List of positions.</returns>
        Task<IReadOnlyList<SchwabPosition>> GetPositionsAsync(Guid userId, string accountHashValue);

        /// <summary>
        /// 获取账户订单。
        /// Gets account orders.
        /// </summary>
        /// <param name="userId">用户 ID。 / User ID.</param>
        /// <param name="accountHashValue">账户哈希值。 / Account hash value.</param>
        /// <param name="fromDate">起始日期（可选）。 / From date (optional).</param>
        /// <param name="toDate">结束日期（可选）。 / To date (optional).</param>
        /// <returns>订单列表。 / List of orders.</returns>
        Task<IReadOnlyList<SchwabOrder>> GetOrdersAsync(
            Guid userId, 
            string accountHashValue, 
            DateTimeOffset? fromDate = null, 
            DateTimeOffset? toDate = null);
    }
}
