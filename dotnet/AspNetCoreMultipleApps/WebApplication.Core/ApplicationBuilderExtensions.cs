using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace WebApplication.Core
{
    public static class ApplicationBuilderExtensions
    {
        /// <summary>
        /// Check the request contains the correct pre-shared key. 
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static IApplicationBuilder CheckPreSharedKey(this IApplicationBuilder builder)
        {
            builder.Use((ctx, next) =>
            {
                var sharedSettings = ctx.RequestServices.GetRequiredService<SharedSettings>();

                //May wish to have some logging around this

                if (!ctx.Request.Headers.TryGetValue(SharedSettings.PreSharedKeyHeader, out var value))
                {
                    ctx.Abort(); 
                }

                if (value != sharedSettings.PreShardKey)
                {
                    ctx.Abort();
                }
                
                return next();
            });
            return builder;
        }

    }
}