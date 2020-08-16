using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using WebApplication.Core;

namespace WebApplication1
{
    public class Startup
    {
        private readonly Settings _settings;

        public Startup(Settings settings)
        {
            _settings = settings;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<SharedSettings>(_settings);
            services.AddControllersWithViews();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.CheckPreSharedKey();

            app.UseForwardedHeadersWithPathBase(new ForwardedHeadersWithPathBaseOptions
            {
                ForwardedHeaders = ForwardedHeadersWithPathBase.All,
                KnownProxies =
                {
                    IPAddress.Loopback,
                    IPAddress.IPv6Loopback,
                    IPAddress.Parse("::ffff:127.0.0.1")
                }
            });

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            if (_settings.WebRootPathOverride != null)
            {
                app.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = new PhysicalFileProvider(_settings.WebRootPathOverride)
                });
            }
            else
            {
                app.UseStaticFiles();
            }

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
