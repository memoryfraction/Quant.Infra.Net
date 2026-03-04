using Mapster;
using Saas.Infra.Data;
using Saas.Infra.Core;

namespace Saas.Infra.Data
{
    /// <summary>
    /// Configure Mapster mappings between data entities and domain DTOs.
    /// </summary>
    public static class MapsterSetup
    {
        /// <summary>
        /// Register mappings. Call this once at application startup.
        /// </summary>
        public static void RegisterMappings()
        {
            var config = TypeAdapterConfig.GlobalSettings;

            config.NewConfig<UserEntity, Saas.Infra.Core.User>()
                .Map(dest => dest.Id, src => src.Id)
                .Map(dest => dest.UserId, src => src.UserId)
                .Map(dest => dest.Username, src => src.UserName)
                .Map(dest => dest.PasswordHash, src => src.PasswordHash)
                .Map(dest => dest.Email, src => src.Email)
                .Map(dest => dest.PhoneNumber, src => src.PhoneNumber)
                .Map(dest => dest.Status, src => src.Status)
                .Map(dest => dest.CreatedAt, src => src.CreatedTime)
                .Map(dest => dest.UpdatedTime, src => src.UpdatedTime)
                .Map(dest => dest.CreatedBy, src => src.CreatedBy)
                .Map(dest => dest.UpdatedBy, src => src.UpdatedBy)
                .Map(dest => dest.IsDeleted, src => src.IsDeleted)
                .IgnoreNullValues(true);

            config.NewConfig<Saas.Infra.Core.User, UserEntity>()
                .Map(dest => dest.Id, src => src.Id)
                .Map(dest => dest.UserId, src => src.UserId == Guid.Empty ? Guid.NewGuid() : src.UserId)
                .Map(dest => dest.UserName, src => src.Username)
                .Map(dest => dest.PasswordHash, src => src.PasswordHash)
                .Map(dest => dest.Email, src => src.Email ?? string.Empty)
                .Map(dest => dest.PhoneNumber, src => src.PhoneNumber)
                .Map(dest => dest.Status, src => src.Status)
                .Map(dest => dest.CreatedTime, src => src.CreatedAt == default ? DateTime.UtcNow : src.CreatedAt)
                .Map(dest => dest.UpdatedTime, src => src.UpdatedTime)
                .Map(dest => dest.CreatedBy, src => src.CreatedBy)
                .Map(dest => dest.UpdatedBy, src => src.UpdatedBy)
                .Map(dest => dest.IsDeleted, src => src.IsDeleted)
                .IgnoreNullValues(true);
        }
    }
}
