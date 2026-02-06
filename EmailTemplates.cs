using System;
using System.Collections.Generic;
using System.Linq;

namespace Quant.Infra.Net.EmailTemplates
{
    /// <summary>
    /// 邮件模板管理类
    /// </summary>
    public static class EmailTemplates
    {
        /// <summary>
        /// 基础 HTML 模板
        /// </summary>
        private static string GetBaseTemplate(string title, string content, string? footerText = null)
        {
            return $@"
                <html>
                <head>
                    <meta charset='utf-8'>
                    <style>
                        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; line-height: 1.6; color: #333; margin: 0; padding: 0; }}
                        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                        .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 30px; text-align: center; border-radius: 10px 10px 0 0; }}
                        .content {{ background: #f8f9fa; padding: 30px; border-radius: 0 0 10px 10px; }}
                        .info-box {{ background: white; padding: 20px; border-radius: 8px; margin: 20px 0; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }}
                        .success {{ color: #28a745; font-weight: bold; }}
                        .warning {{ color: #ffc107; font-weight: bold; }}
                        .error {{ color: #dc3545; font-weight: bold; }}
                        .footer {{ text-align: center; color: #6c757d; font-size: 12px; margin-top: 30px; }}
                        table {{ width: 100%; border-collapse: collapse; }}
                        td {{ padding: 8px; border-bottom: 1px solid #dee2e6; }}
                        .btn {{ display: inline-block; padding: 12px 24px; background: #007bff; color: white; text-decoration: none; border-radius: 5px; margin: 10px 0; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h1>📈 Quant.Infra.Net</h1>
                            <h2>{title}</h2>
                        </div>
                        <div class='content'>
                            {content}
                        </div>
                        <div class='footer'>
                            <p>{footerText ?? "此邮件由 Quant.Infra.Net 系统自动发送"}</p>
                            <p>发送时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>
                        </div>
                    </div>
                </body>
                </html>";
        }

        /// <summary>
        /// 测试邮件模板
        /// </summary>
        public static string GetTestEmailTemplate(List<string> recipients)
        {
            var content = $@"
                <div class='info-box'>
                    <h3>🎉 邮件服务测试成功！</h3>
                    <p>恭喜！您的 Brevo 商业邮件服务已成功配置并正常工作。</p>
                    <p class='success'>✅ SMTP 连接正常</p>
                    <p class='success'>✅ 身份验证通过</p>
                    <p class='success'>✅ 邮件发送成功</p>
                </div>
                
                <div class='info-box'>
                    <h3>📊 本次测试详情</h3>
                    <table>
                        <tr><td><strong>发送时间:</strong></td><td>{DateTime.Now:yyyy-MM-dd HH:mm:ss}</td></tr>
                        <tr><td><strong>邮件服务:</strong></td><td>Brevo (SendinBlue)</td></tr>
                        <tr><td><strong>项目名称:</strong></td><td>Quant.Infra.Net</td></tr>
                        <tr><td><strong>收件人:</strong></td><td>{string.Join(", ", recipients)}</td></tr>
                        <tr><td><strong>服务类型:</strong></td><td>CommercialService (批量邮件)</td></tr>
                    </table>
                </div>
                
                <div class='info-box'>
                    <h3>🚀 下一步</h3>
                    <p>现在您可以在生产环境中使用这个邮件服务了：</p>
                    <ul>
                        <li>📧 发送交易通知</li>
                        <li>📊 发送报告邮件</li>
                        <li>⚠️ 发送系统警报</li>
                        <li>👥 发送批量通知</li>
                    </ul>
                </div>";

            return GetBaseTemplate("量化交易基础设施邮件服务", content, "此邮件由 MVP_SendCommercial 测试方法发送");
        }

        /// <summary>
        /// 交易通知邮件模板
        /// </summary>
        public static string GetTradeNotificationTemplate(string symbol, string action, decimal quantity, decimal price, DateTime tradeTime)
        {
            var actionColor = action.ToUpper() == "BUY" ? "success" : "error";
            var actionIcon = action.ToUpper() == "BUY" ? "📈" : "📉";
            
            var content = $@"
                <div class='info-box'>
                    <h3>{actionIcon} 交易执行通知</h3>
                    <p>您的交易订单已成功执行：</p>
                </div>
                
                <div class='info-box'>
                    <h3>📋 交易详情</h3>
                    <table>
                        <tr><td><strong>交易品种:</strong></td><td>{symbol}</td></tr>
                        <tr><td><strong>交易方向:</strong></td><td><span class='{actionColor}'>{action.ToUpper()}</span></td></tr>
                        <tr><td><strong>交易数量:</strong></td><td>{quantity:N2}</td></tr>
                        <tr><td><strong>成交价格:</strong></td><td>¥{price:N4}</td></tr>
                        <tr><td><strong>成交金额:</strong></td><td>¥{(quantity * price):N2}</td></tr>
                        <tr><td><strong>成交时间:</strong></td><td>{tradeTime:yyyy-MM-dd HH:mm:ss}</td></tr>
                    </table>
                </div>
                
                <div class='info-box'>
                    <h3>💡 温馨提示</h3>
                    <p>请及时关注市场变化，合理控制风险。如有疑问，请联系客服。</p>
                </div>";

            return GetBaseTemplate("交易执行通知", content);
        }

        /// <summary>
        /// 系统警报邮件模板
        /// </summary>
        public static string GetSystemAlertTemplate(string alertType, string message, Dictionary<string, string>? details = null)
        {
            var alertIcon = alertType.ToUpper() switch
            {
                "ERROR" => "🚨",
                "WARNING" => "⚠️",
                "INFO" => "ℹ️",
                _ => "📢"
            };

            var alertClass = alertType.ToUpper() switch
            {
                "ERROR" => "error",
                "WARNING" => "warning",
                _ => "success"
            };

            var content = $@"
                <div class='info-box'>
                    <h3>{alertIcon} 系统警报</h3>
                    <p class='{alertClass}'>警报类型: {alertType.ToUpper()}</p>
                    <p>{message}</p>
                </div>";

            if (details != null && details.Any())
            {
                content += @"
                    <div class='info-box'>
                        <h3>📋 详细信息</h3>
                        <table>";

                foreach (var detail in details)
                {
                    content += $"<tr><td><strong>{detail.Key}:</strong></td><td>{detail.Value}</td></tr>";
                }

                content += @"
                        </table>
                    </div>";
            }

            content += @"
                <div class='info-box'>
                    <h3>🔧 建议操作</h3>
                    <p>请及时检查系统状态，必要时联系技术支持团队。</p>
                </div>";

            return GetBaseTemplate("系统警报通知", content);
        }

        /// <summary>
        /// 日报邮件模板
        /// </summary>
        public static string GetDailyReportTemplate(DateTime reportDate, Dictionary<string, object> metrics)
        {
            var content = $@"
                <div class='info-box'>
                    <h3>📊 每日交易报告</h3>
                    <p>报告日期: <strong>{reportDate:yyyy年MM月dd日}</strong></p>
                </div>
                
                <div class='info-box'>
                    <h3>📈 关键指标</h3>
                    <table>";

            foreach (var metric in metrics)
            {
                var value = metric.Value switch
                {
                    decimal d => d.ToString("N2"),
                    double d => d.ToString("N2"),
                    float f => f.ToString("N2"),
                    int i => i.ToString("N0"),
                    _ => metric.Value?.ToString() ?? "N/A"
                };

                content += $"<tr><td><strong>{metric.Key}:</strong></td><td>{value}</td></tr>";
            }

            content += @"
                    </table>
                </div>
                
                <div class='info-box'>
                    <h3>💡 市场洞察</h3>
                    <p>基于今日数据分析，建议关注市场趋势变化，适时调整投资策略。</p>
                </div>";

            return GetBaseTemplate("每日交易报告", content);
        }

        /// <summary>
        /// 欢迎邮件模板
        /// </summary>
        public static string GetWelcomeTemplate(string userName, string? activationLink = null)
        {
            var content = $@"
                <div class='info-box'>
                    <h3>🎉 欢迎加入 Quant.Infra.Net！</h3>
                    <p>亲爱的 <strong>{userName}</strong>，</p>
                    <p>感谢您注册我们的量化交易平台！我们很高兴为您提供专业的量化交易基础设施服务。</p>
                </div>
                
                <div class='info-box'>
                    <h3>🚀 平台特色</h3>
                    <ul>
                        <li>📊 实时市场数据</li>
                        <li>🤖 智能交易策略</li>
                        <li>📈 风险管理工具</li>
                        <li>📧 及时通知服务</li>
                    </ul>
                </div>";

            if (!string.IsNullOrEmpty(activationLink))
            {
                content += $@"
                    <div class='info-box'>
                        <h3>✅ 激活账户</h3>
                        <p>请点击下方按钮激活您的账户：</p>
                        <a href='{activationLink}' class='btn'>激活账户</a>
                    </div>";
            }

            content += @"
                <div class='info-box'>
                    <h3>📞 联系我们</h3>
                    <p>如有任何问题，请随时联系我们的客服团队。</p>
                </div>";

            return GetBaseTemplate("欢迎加入量化交易平台", content);
        }

        /// <summary>
        /// 自定义邮件模板
        /// </summary>
        public static string GetCustomTemplate(string title, string content, string? footerText = null)
        {
            var wrappedContent = $@"
                <div class='info-box'>
                    {content}
                </div>";

            return GetBaseTemplate(title, wrappedContent, footerText);
        }
    }
}

/// <summary>
/// 邮件模板使用示例
/// </summary>
public static class EmailTemplateExamples
{
    /// <summary>
    /// 使用测试邮件模板
    /// </summary>
    public static string GetTestEmail(List<string> recipients)
    {
        return EmailTemplates.GetTestEmailTemplate(recipients);
    }

    /// <summary>
    /// 使用交易通知模板
    /// </summary>
    public static string GetTradeNotification()
    {
        return EmailTemplates.GetTradeNotificationTemplate(
            "AAPL", 
            "BUY", 
            100, 
            150.25m, 
            DateTime.Now
        );
    }

    /// <summary>
    /// 使用系统警报模板
    /// </summary>
    public static string GetSystemAlert()
    {
        var details = new Dictionary<string, string>
        {
            { "服务器", "Web-01" },
            { "错误代码", "500" },
            { "CPU 使用率", "95%" },
            { "内存使用率", "87%" }
        };

        return EmailTemplates.GetSystemAlertTemplate(
            "ERROR", 
            "服务器响应异常，请立即检查", 
            details
        );
    }

    /// <summary>
    /// 使用日报模板
    /// </summary>
    public static string GetDailyReport()
    {
        var metrics = new Dictionary<string, object>
        {
            { "总交易量", 1250000m },
            { "成功交易数", 1847 },
            { "失败交易数", 23 },
            { "平均收益率", 2.35 },
            { "最大回撤", -1.2 },
            { "夏普比率", 1.85 }
        };

        return EmailTemplates.GetDailyReportTemplate(DateTime.Today, metrics);
    }

    /// <summary>
    /// 使用欢迎邮件模板
    /// </summary>
    public static string GetWelcomeEmail(string userName)
    {
        return EmailTemplates.GetWelcomeTemplate(
            userName, 
            "https://your-platform.com/activate?token=abc123"
        );
    }
}