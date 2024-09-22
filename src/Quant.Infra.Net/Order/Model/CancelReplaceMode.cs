namespace Quant.Infra.Net
{
    public enum CancelReplaceMode
    {
        /// <summary>
        /// If the cancel request fails, the new order placement will not be attempted.
        /// </summary>
        StopOnFailure,

        /// <summary>
        /// New order placement will be attempted even if cancel request fails.
        /// </summary>
        AllowFailure
    }
}