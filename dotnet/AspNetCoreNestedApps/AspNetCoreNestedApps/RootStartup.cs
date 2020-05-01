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


    public class NestedAppSettings { }

    public class NestedStartup
    {
        private readonly NestedAppSettings _settings;

        public NestedStartup(NestedAppSettings settings)
        {
            _settings = settings;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(_settings);
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.Run(async context => await context.Response.WriteAsync("Hello from Nested App!"));
        }
    }
}
