using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WebApplication1;
using static Microsoft.AspNetCore.WebHost;

namespace MainHost
{
    internal class WebApplication1HostedService : BackgroundService
    {
        private readonly HostedServiceContext _hostedServiceContext;
        private readonly Settings _settings;
        private IWebHost _webHost;

        public WebApplication1HostedService(
            HostedServiceContext hostedServiceContext,
            Settings settings)
        {
            _hostedServiceContext = hostedServiceContext;
            _settings = settings;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _webHost = CreateDefaultBuilder<Startup>(Array.Empty<string>())
                .ConfigureServices(services =>
                {
                    services.AddSingleton(_settings);
                })
                .UseUrls("http://127.0.0.1:0")
                .Build();

            _webHost.RunAsync(stoppingToken);

            _hostedServiceContext.WebApplication1Port = _webHost.GetServerPort();

            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            base.Dispose();

            _webHost?.Dispose();
        }
    }
}