using System;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProxyKit;
using WebApplication.Core;

namespace MainHost
{
    public class MainHostStartup
    {
        private readonly IConfiguration _configuration;
        private readonly IHostEnvironment _environment;
        private readonly HostedServiceContext _hostedServiceContext = new HostedServiceContext();
        private static readonly string PreSharedKey = Guid.NewGuid().ToString();

        public MainHostStartup(IConfiguration configuration, IHostEnvironment environment)
        {
            _configuration = configuration;
            _environment = environment;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(_hostedServiceContext);

            services.AddSingleton(CreateWebApplication1Settings());
            services.AddSingleton(CreateWebApplication2Settings());
            services.AddHostedService<WebApplication1HostedService>();
            services.AddHostedService<WebApplication2HostedService>();

            services.AddProxy();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseForwardedHeadersWithPathBase();

            app.Map("/app1", app1 =>
            {
                app1.RunProxy(ctx => ctx
                    .ForwardTo($"http://127.0.0.1:{_hostedServiceContext.WebApplication1Port}")
                    .AddXForwardedHeaders()
                    .AddPreSharedKeyHeader(PreSharedKey)
                    .Send());
            });

            app.Map("/app2", app2 =>
            {
                app2.RunProxy(ctx => ctx
                    .ForwardTo($"http://127.0.0.1:{_hostedServiceContext.WebApplication2Port}")
                    .AddXForwardedHeaders()
                    .AddPreSharedKeyHeader(PreSharedKey)
                    .Send());
            });

            app.Run(async ctx =>
            {
                ctx.Response.ContentType = "text/html";
                await ctx.Response.WriteAsync(@"
<!DOCTYPE html>
<html>
<body>
<h1>Main Host</h1>

<p>
	<ul>
    	<li>
        	<a href=""app1"">WebApplication1</a>
        </li>
        <li>
        	<a href=""app2"">WebApplication2</a>
        </li>
    </ul>
</p>
</body>
</html>
");
            });
        }

        private WebApplication1.Settings CreateWebApplication1Settings()
        {
            var settings = new WebApplication1.Settings();
            DetectWebRoot(settings, "WebApplication1");
            settings.PreShardKey = PreSharedKey;
            return settings;
        }

        private WebApplication2.Settings CreateWebApplication2Settings()
        {
            var settings = new WebApplication2.Settings();
            DetectWebRoot(settings, "WebApplication2");
            settings.PreShardKey = PreSharedKey;
            return settings;
        }

        private void DetectWebRoot(SharedSettings settings, string applicationName)
        {
            /*
            There are multiple hosting scenarios where the WebRoot is not in the
            standard location ({contentRoot}/wwwroot) from the WebApplication libraries'
            perspectives. The host needs to indicate to the libraries where they can find their
            static content.
            
            Example hosts scenarios:
            1. Running MainHost (via dotnet run, or F5).
            2. Running tests under NCrunch which has the ContentRoot in ncrunch workspace directory.
            3. dotnet publish which publish library static content into a wwwroot/_content/{lib} folder.
            */

            // Applies to dotnet run / F5
            var path = Path.Combine(_environment.ContentRootPath, "..", applicationName);
            if (Directory.Exists(path))
            {
                settings.WebRootPathOverride = Path.Combine(path, "wwwroot");
                return;
            }

            // Applies to dotnet publish
            path = Path.Combine(_environment.ContentRootPath, "wwwroot", "_content", applicationName);
            if (Directory.Exists(path))
            {
                settings.WebRootPathOverride = path;
            }
        }
    }
}