using Mapster;
using Saas.Infra.Data;
using Saas.Infra.Core;

namespace Saas.Infra.Data
{
    /// <summary>
    /// 注册 Mapster 映射配置，负责 Data Entity 与 Domain DTO 之间的转换。
    /// Registers Mapster mapping configurations which handle conversions between Data Entities and Domain DTOs.
    /// </summary>
    public static class MapsterSetup
    {
        /// <summary>
        /// 在应用启动时调用以注册所有映射。
        /// Call at application startup to register all mappings.
        /// </summary>
        public static void RegisterMappings()
        {
            var config = TypeAdapterConfig.GlobalSettings;

            // UserEntity -> User mapping
            config.NewConfig<UserEntity, Saas.Infra.Core.User>()
                .Map(dest => dest.Id, src => src.Id)
                .Map(dest => dest.Username, src => src.UserName)
                .Map(dest => dest.PasswordHash, src => src.PasswordHash)
                .Map(dest => dest.Email, src => src.Email)
                .Map(dest => dest.PhoneNumber, src => src.PhoneNumber)
                .Map(dest => dest.Status, src => src.Status)
                .Map(dest => dest.LastLoginTime, src => src.LastLoginTime)
                .Map(dest => dest.CreatedTime, src => src.CreatedTime)
                .Map(dest => dest.UpdatedTime, src => src.UpdatedTime)
                .Map(dest => dest.IsDeleted, src => src.IsDeleted)
                .IgnoreNullValues(true);

            // User -> UserEntity mapping
            config.NewConfig<Saas.Infra.Core.User, UserEntity>()
                .Map(dest => dest.Id, src => src.Id)
                .Map(dest => dest.UserName, src => src.Username)
                .Map(dest => dest.PasswordHash, src => src.PasswordHash)
                .Map(dest => dest.Email, src => src.Email ?? string.Empty)
                .Map(dest => dest.PhoneNumber, src => src.PhoneNumber)
                .Map(dest => dest.Status, src => src.Status)
                .Map(dest => dest.LastLoginTime, src => src.LastLoginTime)
                .Map(dest => dest.CreatedTime, src => src.CreatedTime == default ? DateTimeOffset.UtcNow : src.CreatedTime)
                .Map(dest => dest.UpdatedTime, src => src.UpdatedTime)
                .Map(dest => dest.IsDeleted, src => src.IsDeleted)
                .IgnoreNullValues(true);
        }
    }
}
