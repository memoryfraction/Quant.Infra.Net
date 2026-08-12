using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quant.Infra.Net.Notification.Model;
using System;

namespace Quant.Infra.Net.Notification.Service
{
    /// <summary>
    /// 邮件服务工厂，根据配置策略返回 Commercial 或 Personal 邮件服务。
    /// Email service factory that returns either a commercial or personal email service based on configuration.
    /// </summary>
    public class EmailServiceFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;

        /// <summary>
        /// 初始化邮件服务工厂。
        /// Initialize the email service factory with dependency injection provider and configuration.
        /// </summary>
        /// <param name="serviceProvider">依赖注入服务提供者 / Dependency injection service provider.</param>
        /// <param name="configuration">应用配置 / Application configuration.</param>
        public EmailServiceFactory(IServiceProvider serviceProvider, IConfiguration configuration)
        {
            _configuration = configuration;
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// 根据配置的邮件策略获取对应的邮件服务实例。
        /// Get the appropriate email service based on the configured strategy (Commercial or Personal).
        /// </summary>
        /// <param name="recipientCount">收件人数量（用于未来的自动选择策略）/ Number of recipients (reserved for auto-strategy).</param>
        /// <returns>对应的邮件服务实例 / The corresponding email service instance.</returns>
        /// <exception cref="InvalidOperationException">当配置的策略无效时抛出 / Thrown when the configured strategy is invalid.</exception>
        public IEmailService GetService(int recipientCount)
        {
            // 1. Get strategy from config (Commercial, Personal, or Auto)
            string strategy = _configuration["Email:Type"];

            if (strategy.ToLower() == "Commercial".ToLower())
            {
                return _serviceProvider.GetRequiredService<CommercialEmailService>();
            }

            if (strategy.ToLower() == "Personal".ToLower())
            {
                return _serviceProvider.GetRequiredService<PersonalEmailService>();
            }

            throw new InvalidOperationException("Invalid email service type configured.");
        }
    }
}
