using System;

namespace Saas.Infra.MVC.Models.Responses
{
    /// <summary>
    /// 产品响应DTO。
    /// Product response DTO.
    /// </summary>
    public class ProductDto
    {
        /// <summary>
        /// 产品ID。
        /// Product ID.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// 产品代码。
        /// Product code.
        /// </summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// 产品名称。
        /// Product name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 产品描述。
        /// Product description.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// 是否激活。
        /// Whether active.
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// JSON元数据。
        /// JSON metadata.
        /// </summary>
        public string? Metadata { get; set; }

        /// <summary>
        /// 创建时间。
        /// Created time.
        /// </summary>
        public DateTimeOffset CreatedTime { get; set; }
    }
}
