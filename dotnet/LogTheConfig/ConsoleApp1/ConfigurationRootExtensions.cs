using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;

namespace ConsoleApp1
{
    public static class ConfigurationRootExtensions
    {
        public static string GetDiagnosticView(this IConfigurationRoot root)
        {
            var diagnosticView = new StringBuilder();

            var providersByKey = GetProvidersByKey(root);

            foreach (var keyValuePair in providersByKey)
            {
                diagnosticView.Append($"{keyValuePair.Key.PadRight(30, ' ')} [ ");

                var infos = keyValuePair.Value
                    .Select(provider =>
                    {
                        var info = provider.GetType().Name.Replace("ConfigurationProvider", "");

                        switch (provider)
                        {
                            case FileConfigurationProvider p:
                                info += " (Path=" + p.Source.Path + ")";
                                break;
                            case EnvironmentVariablesConfigurationProvider p:
                                // Nasty, would be better if prefix was a property
                                var field = typeof(EnvironmentVariablesConfigurationProvider).GetField("_prefix",
                                    BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.Instance);
                                var prefix = field.GetValue(p);
                                info += " (Prefix='" + prefix + "')";
                                break;
                        }

                        return info;
                    });

                diagnosticView.Append(string.Join(", ", infos));
                diagnosticView.AppendLine(" ]");
            }

            return diagnosticView.ToString();
        }

        public static IReadOnlyCollection<KeyValuePair<string, IConfigurationProvider[]>> GetProvidersByKey(this IConfigurationRoot root)
        {
            var keys = new List<KeyValuePair<string, IConfigurationProvider[]>>();
            var configurationProviders = root.Providers.Reverse();

            void RecurseChildren(IEnumerable<IConfigurationSection> children)
            {
                foreach (var child in children)
                {
                    var providers = configurationProviders
                        .Where(provider => provider.TryGet(child.Path, out var _))
                        .ToArray();

                    if (providers.Length > 0)
                    {
                        keys.Add(new KeyValuePair<string, IConfigurationProvider[]>(child.Path, providers));
                    }

                    RecurseChildren(child.GetChildren());
                }
            }

            var configurationSections = root.GetChildren();

            RecurseChildren(configurationSections);

            return keys;
        }
    }
}