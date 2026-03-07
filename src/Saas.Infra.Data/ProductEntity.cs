using System;

namespace Saas.Infra.Data
{
    /// <summary>
    /// Product entity stored in database.
    /// </summary>
    public class ProductEntity
    {
        /// <summary>
        /// Product identifier (primary key), e.g., "cryptocycleai"
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Product display name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Url to enter the product (can be absolute or relative)
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Optional icon url
        /// </summary>
        public string? IconUrl { get; set; }

        /// <summary>
        /// Optional description
        /// </summary>
        public string? Description { get; set; }
    }
}
