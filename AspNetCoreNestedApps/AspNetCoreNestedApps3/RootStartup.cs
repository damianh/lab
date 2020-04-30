using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace AspNetCoreNestedApps
{
    public class RootStartup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddSingleton(new NestedAppSettings());
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
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
            services.AddHostedService<DummyIsolatedHostedService>();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IHostApplicationLifetime lifetime)
        {
            lifetime.ApplicationStopped.Register(() =>
            {
                Console.WriteLine(
                    $"Isolated HostedService StopAsync called {DummyIsolatedHostedService.StopCalled} times");
            });

            app.Run(async context =>
            {
                await context.Response.WriteAsync($"Hello from Nested App! And Isolated HostedService StartAsync called {DummyIsolatedHostedService.StartCalled} times");
            });
        }
    }

    public class DummyIsolatedHostedService : IHostedService
    {
        public static int StartCalled { get; set; }
        public static int StopCalled { get; set; }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            StartCalled++;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            StopCalled++;
            return Task.CompletedTask;
        }
    }
}
