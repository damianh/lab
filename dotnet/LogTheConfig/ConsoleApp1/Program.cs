using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace ConsoleApp1
{
    class Program
    {
        static void Main(string[] args)
        {
            var key = "IsEnabled";
            var secretKey = "SomeSecret";
            var configurationBuilder = new ConfigurationBuilder()
                .AddEnvironmentVariables("PROCESSOR_")
                .AddInMemoryCollection(new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>(key, "false"),
                    new KeyValuePair<string, string>(secretKey, "foo"),
                    new KeyValuePair<string, string>("Nested:Bar", "baz")
                })
                .AddJsonFile("appsettings.json");

            var configurationRoot = configurationBuilder.Build();

            var diagnosticView = configurationRoot.GetDiagnosticView(); // Custom extension method.
            Console.WriteLine("*** Diagnostic View");
            Console.WriteLine(diagnosticView);

            var settings = new Settings();
            configurationRoot.Bind(settings);
            var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            Console.WriteLine("*** Settings as JSON with sensitive fields redacted.");
            Console.WriteLine(json);
        }

        public class Settings
        {
            public bool IsEnabled { get; set; }

            [JsonConverter(typeof(SensitiveConverter))] // Custom json formatter.
            public string SomeSecret { get; set; }

            public NestedSettings Nested { get; set; }
            public class NestedSettings
            {
                public string Bar { get; set; }
            }
        }
    }
}
