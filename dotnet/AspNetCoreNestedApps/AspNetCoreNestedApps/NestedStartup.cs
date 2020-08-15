using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AspNetCoreNestedApps
{
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