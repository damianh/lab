using Microsoft.AspNetCore.Hosting;
using static Microsoft.AspNetCore.WebHost;

namespace MainHost
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            CreateDefaultBuilder<MainHostStartup>(args)
                .Build()
                .Run();
        }
    }
}
