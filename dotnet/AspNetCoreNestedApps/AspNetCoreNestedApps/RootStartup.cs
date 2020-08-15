using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AspNetCoreNestedApps
{
    public class RootStartup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddSingleton(new NestedAppSettings());
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.IsolatedMap<NestedStartup>("/nested");
            app.Run(async context => await context.Response.WriteAsync("Hello World!"));
        }
    }
}
