using System.ComponentModel.DataAnnotations;

namespace Saas.Infra.MVC.Models.Requests
{
    /// <summary>
    /// 创建产品请求模型。
    /// Create product request model.
    /// </summary>
    public class CreateProductRequest
    {
        /// <summary>
        /// 产品代码（唯一）。
        /// Product code (unique).
        /// </summary>
        [Required(ErrorMessage = "Product code is required")]
        [StringLength(50, ErrorMessage = "Code cannot exceed 50 characters")]
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// 产品名称。
        /// Product name.
        /// </summary>
        [Required(ErrorMessage = "Product name is required")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 产品描述。
        /// Product description.
        /// </summary>
        [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
        public string? Description { get; set; }

        /// <summary>
        /// 是否激活。
        /// Whether active.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// JSON元数据。
        /// JSON metadata.
        /// </summary>
        public string? Metadata { get; set; }
    }

    /// <summary>
    /// 更新产品请求模型。
    /// Update product request model.
    /// </summary>
    public class UpdateProductRequest
    {
        /// <summary>
        /// 产品名称。
        /// Product name.
        /// </summary>
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
        public string? Name { get; set; }

        /// <summary>
        /// 产品描述。
        /// Product description.
        /// </summary>
        [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
        public string? Description { get; set; }

        /// <summary>
        /// 是否激活。
        /// Whether active.
        /// </summary>
        public bool? IsActive { get; set; }

        /// <summary>
        /// JSON元数据。
        /// JSON metadata.
        /// </summary>
        public string? Metadata { get; set; }
    }
}
