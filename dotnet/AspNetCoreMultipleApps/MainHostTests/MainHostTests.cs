using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using MainHost;
using Microsoft.AspNetCore.Hosting;
using Shouldly;
using Xunit;
using static Microsoft.AspNetCore.WebHost;

namespace MainHostTests
{
    public class MainHostTests
    {
        [Fact]
        public async Task SanityTest()
        {
            var webHost = CreateDefaultBuilder<MainHostStartup>(Array.Empty<string>())
                .UseNCrunchContentRoot("AspNetCoreMultipleApps")
                .UseUrls("http://127.0.0.1:0")
                .Build();

            await webHost.StartAsync();
            var port = webHost.GetServerPort();
            var baseUri = new Uri($"http://127.0.0.1:{port}");

            var client = new HttpClient();

            var response = await client.GetAsync(new Uri(baseUri, "/app1"));

            response.StatusCode.ShouldBe(HttpStatusCode.OK);

            await webHost.StopAsync();

            webHost.Dispose();
        }
    }

    public static class WebHostBuilderExtensions
    {
        public static IWebHostBuilder UseNCrunchContentRoot(this IWebHostBuilder hostBuilder, string projectName)
        {
            // Not happy this exists. NCrunch copies things to it's own workspace fold structure that does not
            // resemble that of the project repository. An alternative is tell NCrunch to copy wwwroot files.
            // However, that is not what is done typically by the build. Am looking for improvement / alternative on this...

            var originalProjectPath = Environment.GetEnvironmentVariable("NCrunch.OriginalProjectPath");

            if (!string.IsNullOrWhiteSpace(originalProjectPath))
            {
                var contentRootDirectory = Directory.GetParent(originalProjectPath).Parent?
                    .GetDirectories(projectName)
                    .SingleOrDefault();

                if (contentRootDirectory != null)
                {
                    hostBuilder.UseContentRoot(contentRootDirectory.FullName);
                }
            }

            return hostBuilder;
        }
    }
}
