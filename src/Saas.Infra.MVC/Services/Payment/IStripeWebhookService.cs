using System.Threading.Tasks;
using Stripe;

namespace Saas.Infra.MVC.Services.Payment
{
    /// <summary>
    /// Stripe Webhook事件处理服务接口。
    /// Stripe webhook event processing service interface.
    /// </summary>
    public interface IStripeWebhookService
    {
        /// <summary>
        /// 处理Stripe事件（路由到对应的Handler）。
        /// Processes a Stripe event by routing to the corresponding handler.
        /// </summary>
        /// <param name="stripeEvent">Stripe事件对象。 / Stripe event object.</param>
        /// <returns>异步任务。 / Async task.</returns>
        Task ProcessEventAsync(Event stripeEvent);
    }
}
