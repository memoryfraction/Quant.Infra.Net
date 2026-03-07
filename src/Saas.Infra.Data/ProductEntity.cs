using System;
using System.Text.Json;

namespace Saas.Infra.Data
{
    /// <summary>
    /// Product entity stored in database following product pricing schema.
    /// </summary>
    public class ProductEntity
    {
        /// <summary>
        /// Primary key (UUID)
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Business code (unique), used as public product identifier
        /// </summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// Display name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Description (text)
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Allowed payment gateways (text[])
        /// </summary>
        public string[]? AllowedPaymentGateways { get; set; }

        /// <summary>
        /// Whether product is active
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// JSON metadata (jsonb)
        /// </summary>
        public JsonDocument? Metadata { get; set; }

        /// <summary>
        /// Created timestamp (with timezone)
        /// </summary>
        public DateTimeOffset CreatedTime { get; set; }

        /// <summary>
        /// Updated timestamp (with timezone)
        /// </summary>
        public DateTimeOffset? UpdatedTime { get; set; }

        /// <summary>
        /// Audit: created by user id
        /// </summary>
        public Guid? CreatedBy { get; set; }

        /// <summary>
        /// Audit: updated by user id
        /// </summary>
        public Guid? UpdatedBy { get; set; }
    }
}
