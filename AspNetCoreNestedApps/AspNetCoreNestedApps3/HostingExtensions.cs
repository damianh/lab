using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

// ReSharper disable once CheckNamespace
namespace Microsoft.AspNetCore.Hosting
{
    /// <summary>
    /// Provides extension methods for <see cref="IApplicationBuilder"/>.
    /// Reference:  https://github.com/aspnet/AspNetCore/blob/master/src/Hosting/Hosting/src/Internal/StartupLoader.cs
    /// </summary>
    public static class HostingExtensions
    {
        /// <summary>
        /// If the request path starts with the given <paramref name="path"/>, execute the app configured via
        /// the configuration method of the <typeparamref name="TStartup"/> class instead of continuing to the next component
        /// in the pipeline. The new app will get an own newly created <see cref="ServiceCollection"/> and will not share
        /// the <see cref="ServiceCollection"/> of the originating app.
        /// </summary>
        /// <typeparam name="TStartup">The startup class used to configure the new app and the service collection.</typeparam>
        /// <param name="app">The application builder to register the isolated map with.</param>
        /// <param name="path">The path to match. Must not end with a '/'.</param>
        /// <returns>The new pipeline with the isolated middleware configured.</returns>
        public static IApplicationBuilder IsolatedMap<TStartup>(this IApplicationBuilder app, PathString path)
            where TStartup : class
        {
            var environment = app.ApplicationServices.GetRequiredService<IWebHostEnvironment>();
            var methods = StartupLoader.LoadMethods(app.ApplicationServices, typeof(TStartup), environment.EnvironmentName);

            return app.IsolatedMap(path, methods.ConfigureDelegate, methods.ConfigureServicesDelegate);
        }

        /// <summary>
        /// If the request path starts with the given <paramref name="path"/>, execute the app configured via
        /// the configuration method of the <typeparamref name="TStartup"/> class instead of continuing to the next component
        /// in the pipeline. The new app will get an own newly created <see cref="ServiceCollection"/> and will not share
        /// the <see cref="ServiceCollection"/> of the originating app.
        /// </summary>
        /// <typeparam name="TStartup">The startup class used to configure the new app and the service collection.</typeparam>
        /// <param name="app">The application builder to register the isolated map with.</param>
        /// <param name="path">The path to match. Must not end with a '/'.</param>
        /// <returns>The new pipeline with the isolated middleware configured.</returns>
        public static IApplicationBuilder IsolatedMap<TStartup>(
            this IApplicationBuilder app,
            PathString path,
            Action<IServiceCollection> configureServices)
            where TStartup : class
        {
            var environment = app.ApplicationServices.GetRequiredService<IWebHostEnvironment>();
            var methods = StartupLoader.LoadMethods(
                app.ApplicationServices,
                typeof(TStartup),
                environment.EnvironmentName);

            return app.IsolatedMap(path, methods.ConfigureDelegate, methods.ConfigureServicesDelegate);
        }

        /// <summary>
        /// If the request path starts with the given <paramref name="path"/>, execute the app configured via
        /// <paramref name="configuration"/> parameter instead of continuing to the next component in the pipeline.
        /// The new app will get an own newly created <see cref="ServiceCollection"/> and will not share the
        /// <see cref="ServiceCollection"/> from the originating app.
        /// </summary>
        /// <param name="app">The application builder to register the isolated map with.</param>
        /// <param name="path">The path to match. Must not end with a '/'.</param>
        /// <param name="configuration">The branch to take for positive path matches.</param>
        /// <param name="configureServices">A method to configure the newly created service collection.</param>
        /// <returns>The new pipeline with the isolated middleware configured.</returns>
        public static IApplicationBuilder IsolatedMap(
            this IApplicationBuilder app, PathString path,
            Action<IApplicationBuilder> configuration,
            Action<IServiceCollection> configureServices)
        {
            return app.IsolatedMap(path, configuration, services =>
            {
                configureServices(services);

                return services.BuildServiceProvider();
            });
        }

        /// <summary>
        /// If the request path starts with the given <paramref name="path"/>, execute the app configured via
        /// <paramref name="configuration"/> parameter instead of continuing to the next component in the pipeline.
        /// The new app will get an own newly created <see cref="ServiceCollection"/> and will not share the
        /// <see cref="ServiceCollection"/> from the originating app.
        /// </summary>
        /// <param name="app">The application builder to register the isolated map with.</param>
        /// <param name="path">The path to match. Must not end with a '/'.</param>
        /// <param name="configuration">The branch to take for positive path matches.</param>
        /// <param name="configureServices">A method to configure the newly created service collection.</param>
        /// <returns>The new pipeline with the isolated middleware configured.</returns>
        public static IApplicationBuilder IsolatedMap(
            this IApplicationBuilder app, PathString path,
            Action<IApplicationBuilder> configuration,
            Func<IServiceCollection, IServiceProvider> configureServices)
        {
            if (path.HasValue && path.Value.EndsWith("/", StringComparison.Ordinal))
            {
                throw new ArgumentException("The path must not end with a '/'", nameof(path));
            }

            return app.Isolate(builder => builder.Map(path, configuration), configureServices);
        }

        /// <summary>
        /// Creates a new isolated application builder which gets its own <see cref="ServiceCollection"/>, which only
        /// has the default services registered. It will not share the <see cref="ServiceCollection"/> from the
        /// originating app. The isolated map will be configured using the configuration methods of the
        /// <typeparamref name="TStartup"/> class.
        /// </summary>
        /// <typeparam name="TStartup">The startup class used to configure the new app and the service collection.</typeparam>
        /// <param name="app">The application builder to create the isolated app from.</param>
        /// <returns>The new pipeline with the isolated application integrated.</returns>
        public static IApplicationBuilder Isolate<TStartup>(this IApplicationBuilder app) where TStartup : class
        {
            var environment = app.ApplicationServices.GetRequiredService<IWebHostEnvironment>();
            var methods = StartupLoader.LoadMethods(app.ApplicationServices, typeof(TStartup), environment.EnvironmentName);

            return app.Isolate(methods.ConfigureDelegate, methods.ConfigureServicesDelegate);
        }

        /// <summary>
        /// Creates a new isolated application builder which gets its own <see cref="ServiceCollection"/>, which only
        /// has the default services registered. It will not share the <see cref="ServiceCollection"/> from the
        /// originating app.
        /// </summary>
        /// <param name="app">The application builder to create the isolated app from.</param>
        /// <param name="configuration">The branch of the isolated app.</param>
        /// <returns>The new pipeline with the isolated application integrated.</returns>
        public static IApplicationBuilder Isolate(
            this IApplicationBuilder app,
            Action<IApplicationBuilder> configuration)
        {
            return app.Isolate(configuration, services => services.BuildServiceProvider());
        }

        /// <summary>
        /// Creates a new isolated application builder which gets its own <see cref="ServiceCollection"/>, which only
        /// has the default services registered. It will not share the <see cref="ServiceCollection"/> from the
        /// originating app.
        /// </summary>
        /// <param name="app">The application builder to create the isolated app from.</param>
        /// <param name="configuration">The branch of the isolated app.</param>
        /// <param name="configureServices">A method to configure the newly created service collection.</param>
        /// <returns>The new pipeline with the isolated application integrated.</returns>
        public static IApplicationBuilder Isolate(
            this IApplicationBuilder app,
            Action<IApplicationBuilder> configuration,
            Action<IServiceCollection> configureServices)
        {
            return app.Isolate(configuration, services =>
            {
                configureServices(services);

                return services.BuildServiceProvider();
            });
        }

        /// <summary>
        /// Creates a new isolated application builder which gets its own <see cref="ServiceCollection"/>, which only
        /// has the default services registered. It will not share the <see cref="ServiceCollection"/> from the
        /// originating app.
        /// </summary>
        /// <param name="app">The application builder to create the isolated app from.</param>
        /// <param name="configuration">The branch of the isolated app.</param>
        /// <param name="configureServices">A method to configure the newly created service collection.</param>
        /// <returns>The new pipeline with the isolated application integrated.</returns>
        public static IApplicationBuilder Isolate(
            this IApplicationBuilder app,
            Action<IApplicationBuilder> configuration,
            Func<IServiceCollection, IServiceProvider> configureServices)
        {
            var services = CreateDefaultServiceCollection(app.ApplicationServices);
            var branchedServiceProvider = configureServices(services);

            var branchedApplicationBuilder = new ApplicationBuilder(branchedServiceProvider);
            branchedApplicationBuilder.EnableDependencyInjection(branchedServiceProvider);

            configuration(branchedApplicationBuilder);
            branchedApplicationBuilder.RunHostedServices().GetAwaiter().GetResult();

            return app.Use(next =>
            {
                // Run the rest of the pipeline in the original context,
                // with the services defined by the parent application builder.
                branchedApplicationBuilder.Run(async context =>
                {
                    var factory = app.ApplicationServices.GetRequiredService<IServiceScopeFactory>();

                    try
                    {
                        using var scope = factory.CreateScope();
                        context.RequestServices = scope.ServiceProvider;
                        await next(context);
                    }

                    finally
                    {
                        context.RequestServices = null;
                    }
                });

                var branch = branchedApplicationBuilder.Build();

                return context => branch(context);
            });
        }

        /// <summary>
        /// This creates a new <see cref="ServiceCollection"/> with the same services registered as the
        /// <see cref="WebHostBuilder"/> does when creating a new <see cref="ServiceCollection"/>.
        /// </summary>
        /// <param name="provider">The service provider used to retrieve the default services.</param>
        /// <returns>A new <see cref="ServiceCollection"/> with the default services registered.</returns>
        private static ServiceCollection CreateDefaultServiceCollection(IServiceProvider provider)
        {
            var services = new ServiceCollection();

            // Copy the services added by the hosting layer (WebHostBuilder.BuildHostingServices).
            // See https://github.com/aspnet/Hosting/blob/dev/src/Microsoft.AspNetCore.Hosting/WebHostBuilder.cs.

            services.AddLogging();

            if (provider.GetService<IHttpContextAccessor>() != null)
            {
                services.AddSingleton(provider.GetService<IHttpContextAccessor>());
            }

            services.AddSingleton(provider.GetRequiredService<IWebHostEnvironment>());
            services.AddSingleton(provider.GetRequiredService<ILoggerFactory>());
            services.AddSingleton(provider.GetRequiredService<IHostApplicationLifetime>());
            services.AddSingleton(_ => provider.GetRequiredService<IHttpContextFactory>());

            services.AddSingleton(provider.GetRequiredService<DiagnosticSource>());
            services.AddSingleton(provider.GetRequiredService<DiagnosticListener>());
            services.AddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>();
            services.AddSingleton<HostedServiceExecutor>();

            return services;
        }

        internal static IApplicationBuilder EnableDependencyInjection(this IApplicationBuilder app, IServiceProvider serviceProvider)
        {
            app.Use(async (branchContext, next) =>
            {
                var factory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

                // Store the original request services in the current ASP.NET context.
                branchContext.Items[typeof(IServiceProvider)] = branchContext.RequestServices;

                try
                {
                    using var scope = factory.CreateScope();
                    branchContext.RequestServices = scope.ServiceProvider;
                    await next();
                }

                finally
                {
                    branchContext.RequestServices = null;
                }
            });

            return app;
        }
    }

    internal static class HostedServiceExtensions
    {
        public static Task RunHostedServices(this IApplicationBuilder app)
        {
            var hostedServices = app.ApplicationServices.GetRequiredService<HostedServiceExecutor>();
            var lifeTime = app.ApplicationServices.GetRequiredService<IHostApplicationLifetime>();
            lifeTime.ApplicationStopping.Register(() => { hostedServices.StopAsync(CancellationToken.None).GetAwaiter().GetResult(); });
            return hostedServices.StartAsync(lifeTime.ApplicationStopping);
        }
    }

    internal class ConfigureServicesBuilder
    {
        public ConfigureServicesBuilder(MethodInfo configureServices)
        {
            MethodInfo = configureServices;
        }

        public MethodInfo MethodInfo { get; }

        public Func<Func<IServiceCollection, IServiceProvider>, Func<IServiceCollection, IServiceProvider>> StartupServiceFilters { get; set; } = f => f;

        public Func<IServiceCollection, IServiceProvider> Build(object instance) => services => Invoke(instance, services);

        private IServiceProvider Invoke(object instance, IServiceCollection services)
        {
            return StartupServiceFilters(Startup)(services);

            IServiceProvider Startup(IServiceCollection serviceCollection) => InvokeCore(instance, serviceCollection);
        }

        private IServiceProvider InvokeCore(object instance, IServiceCollection services)
        {
            if (MethodInfo == null)
            {
                return null;
            }

            // Only support IServiceCollection parameters
            var parameters = MethodInfo.GetParameters();
            if (parameters.Length > 1 ||
                parameters.Any(p => p.ParameterType != typeof(IServiceCollection)))
            {
                throw new InvalidOperationException("The ConfigureServices method must either be parameterless or take only one parameter of type IServiceCollection.");
            }

            var arguments = new object[MethodInfo.GetParameters().Length];

            if (parameters.Length > 0)
            {
                arguments[0] = services;
            }

            return MethodInfo.InvokeWithoutWrappingExceptions(instance, arguments) as IServiceProvider;
        }
    }

    internal class ConfigureContainerBuilder
    {
        public ConfigureContainerBuilder(MethodInfo configureContainerMethod)
        {
            MethodInfo = configureContainerMethod;
        }

        public MethodInfo MethodInfo { get; }

        public Func<Action<object>, Action<object>> ConfigureContainerFilters { get; set; } = f => f;

        public Action<object> Build(object instance) => container => Invoke(instance, container);

        public Type GetContainerType()
        {
            var parameters = MethodInfo.GetParameters();
            if (parameters.Length != 1)
            {
                // REVIEW: This might be a breaking change
                throw new InvalidOperationException($"The {MethodInfo.Name} method must take only one parameter.");
            }
            return parameters[0].ParameterType;
        }

        private void Invoke(object instance, object container)
        {
            ConfigureContainerFilters(StartupConfigureContainer)(container);

            void StartupConfigureContainer(object containerBuilder) => InvokeCore(instance, containerBuilder);
        }

        private void InvokeCore(object instance, object container)
        {
            if (MethodInfo == null)
            {
                return;
            }

            var arguments = new object[1] { container };

            MethodInfo.InvokeWithoutWrappingExceptions(instance, arguments);
        }
    }

    internal class ConfigureBuilder
    {
        public ConfigureBuilder(MethodInfo configure)
        {
            MethodInfo = configure;
        }

        public MethodInfo MethodInfo { get; }

        public Action<IApplicationBuilder> Build(object instance) => builder => Invoke(instance, builder);

        private void Invoke(object instance, IApplicationBuilder builder)
        {
            // Create a scope for Configure, this allows creating scoped dependencies
            // without the hassle of manually creating a scope.
            using (var scope = builder.ApplicationServices.CreateScope())
            {
                var serviceProvider = scope.ServiceProvider;
                var parameterInfos = MethodInfo.GetParameters();
                var parameters = new object[parameterInfos.Length];
                for (var index = 0; index < parameterInfos.Length; index++)
                {
                    var parameterInfo = parameterInfos[index];
                    if (parameterInfo.ParameterType == typeof(IApplicationBuilder))
                    {
                        parameters[index] = builder;
                    }
                    else
                    {
                        try
                        {
                            parameters[index] = serviceProvider.GetRequiredService(parameterInfo.ParameterType);
                        }
                        catch (Exception ex)
                        {
                            throw new Exception(
                                $"Could not resolve a service of type '{parameterInfo.ParameterType.FullName}' for the parameter '{parameterInfo.Name}' of method '{MethodInfo.Name}' on type '{MethodInfo.DeclaringType.FullName}'.", ex);
                        }
                    }
                }

                MethodInfo.InvokeWithoutWrappingExceptions(instance, parameters);
            }
        }
    }

    internal class StartupLoader
    {
        // Creates an <see cref="StartupMethods"/> instance with the actions to run for configuring the application services and the
        // request pipeline of the application.
        // When using convention based startup, the process for initializing the services is as follows:
        // The host looks for a method with the signature <see cref="IServiceProvider"/> ConfigureServices(<see cref="IServiceCollection"/> services).
        // If it can't find one, it looks for a method with the signature <see cref="void"/> ConfigureServices(<see cref="IServiceCollection"/> services).
        // When the configure services method is void returning, the host builds a services configuration function that runs all the <see cref="IStartupConfigureServicesFilter"/>
        // instances registered on the host, along with the ConfigureServices method following a decorator pattern.
        // Additionally to the ConfigureServices method, the Startup class can define a <see cref="void"/> ConfigureContainer&lt;TContainerBuilder&gt;(TContainerBuilder builder)
        // method that further configures services into the container. If the ConfigureContainer method is defined, the services configuration function
        // creates a TContainerBuilder <see cref="IServiceProviderFactory{TContainerBuilder}"/> and runs all the <see cref="IStartupConfigureContainerFilter{TContainerBuilder}"/>
        // instances registered on the host, along with the ConfigureContainer method following a decorator pattern.
        // For example:
        // StartupFilter1
        //   StartupFilter2
        //     ConfigureServices
        //   StartupFilter2
        // StartupFilter1
        // ConfigureContainerFilter1
        //   ConfigureContainerFilter2
        //     ConfigureContainer
        //   ConfigureContainerFilter2
        // ConfigureContainerFilter1
        // 
        // If the Startup class ConfigureServices returns an <see cref="IServiceProvider"/> and there is at least an <see cref="IStartupConfigureServicesFilter"/> registered we
        // throw as the filters can't be applied.
        public static StartupMethods LoadMethods(IServiceProvider hostingServiceProvider, Type startupType, string environmentName)
        {
            var configureMethod = FindConfigureDelegate(startupType, environmentName);

            var servicesMethod = FindConfigureServicesDelegate(startupType, environmentName);
            var configureContainerMethod = FindConfigureContainerDelegate(startupType, environmentName);

            object instance = null;
            if (!configureMethod.MethodInfo.IsStatic || (servicesMethod != null && !servicesMethod.MethodInfo.IsStatic))
            {
                instance = ActivatorUtilities.GetServiceOrCreateInstance(hostingServiceProvider, startupType);
            }

            // The type of the TContainerBuilder. If there is no ConfigureContainer method we can just use object as it's not
            // going to be used for anything.
            var type = configureContainerMethod.MethodInfo != null ? configureContainerMethod.GetContainerType() : typeof(object);

            var builder = (ConfigureServicesDelegateBuilder)Activator.CreateInstance(
                typeof(ConfigureServicesDelegateBuilder<>).MakeGenericType(type),
                hostingServiceProvider,
                servicesMethod,
                configureContainerMethod,
                instance);

            return new StartupMethods(instance, configureMethod.Build(instance), builder.Build());
        }

        private abstract class ConfigureServicesDelegateBuilder
        {
            public abstract Func<IServiceCollection, IServiceProvider> Build();
        }

        private class ConfigureServicesDelegateBuilder<TContainerBuilder> : ConfigureServicesDelegateBuilder
        {
            public ConfigureServicesDelegateBuilder(
                IServiceProvider hostingServiceProvider,
                ConfigureServicesBuilder configureServicesBuilder,
                ConfigureContainerBuilder configureContainerBuilder,
                object instance)
            {
                HostingServiceProvider = hostingServiceProvider;
                ConfigureServicesBuilder = configureServicesBuilder;
                ConfigureContainerBuilder = configureContainerBuilder;
                Instance = instance;
            }

            public IServiceProvider HostingServiceProvider { get; }
            public ConfigureServicesBuilder ConfigureServicesBuilder { get; }
            public ConfigureContainerBuilder ConfigureContainerBuilder { get; }
            public object Instance { get; }

            public override Func<IServiceCollection, IServiceProvider> Build()
            {
                ConfigureServicesBuilder.StartupServiceFilters = BuildStartupServicesFilterPipeline;
                var configureServicesCallback = ConfigureServicesBuilder.Build(Instance);

                ConfigureContainerBuilder.ConfigureContainerFilters = ConfigureContainerPipeline;
                var configureContainerCallback = ConfigureContainerBuilder.Build(Instance);

                return ConfigureServices(configureServicesCallback, configureContainerCallback);

                Action<object> ConfigureContainerPipeline(Action<object> action)
                {
                    return Target;

                    // The ConfigureContainer pipeline needs an Action<TContainerBuilder> as source, so we just adapt the
                    // signature with this function.
                    void Source(TContainerBuilder containerBuilder) =>
                        action(containerBuilder);

                    // The ConfigureContainerBuilder.ConfigureContainerFilters expects an Action<object> as value, but our pipeline
                    // produces an Action<TContainerBuilder> given a source, so we wrap it on an Action<object> that internally casts
                    // the object containerBuilder to TContainerBuilder to match the expected signature of our ConfigureContainer pipeline.
                    void Target(object containerBuilder) =>
                        BuildStartupConfigureContainerFiltersPipeline(Source)((TContainerBuilder)containerBuilder);
                }
            }

            Func<IServiceCollection, IServiceProvider> ConfigureServices(
                Func<IServiceCollection, IServiceProvider> configureServicesCallback,
                Action<object> configureContainerCallback)
            {
                return ConfigureServicesWithContainerConfiguration;

                IServiceProvider ConfigureServicesWithContainerConfiguration(IServiceCollection services)
                {
                    // Call ConfigureServices, if that returned an IServiceProvider, we're done
                    IServiceProvider applicationServiceProvider = configureServicesCallback.Invoke(services);

                    if (applicationServiceProvider != null)
                    {
                        return applicationServiceProvider;
                    }

                    // If there's a ConfigureContainer method
                    if (ConfigureContainerBuilder.MethodInfo != null)
                    {
                        var serviceProviderFactory = HostingServiceProvider.GetRequiredService<IServiceProviderFactory<TContainerBuilder>>();
                        var builder = serviceProviderFactory.CreateBuilder(services);
                        configureContainerCallback(builder);
                        applicationServiceProvider = serviceProviderFactory.CreateServiceProvider(builder);
                    }
                    else
                    {
                        // Get the default factory
                        var serviceProviderFactory = HostingServiceProvider.GetRequiredService<IServiceProviderFactory<IServiceCollection>>();
                        var builder = serviceProviderFactory.CreateBuilder(services);
                        applicationServiceProvider = serviceProviderFactory.CreateServiceProvider(builder);
                    }

                    return applicationServiceProvider ?? services.BuildServiceProvider();
                }
            }

            private Func<IServiceCollection, IServiceProvider> BuildStartupServicesFilterPipeline(Func<IServiceCollection, IServiceProvider> startup)
            {
                return RunPipeline;

                IServiceProvider RunPipeline(IServiceCollection services)
                {
#pragma warning disable CS0612 // Type or member is obsolete
                    var filters = HostingServiceProvider.GetRequiredService<IEnumerable<IStartupConfigureServicesFilter>>().Reverse().ToArray();
#pragma warning restore CS0612 // Type or member is obsolete

                    // If there are no filters just run startup (makes IServiceProvider ConfigureServices(IServiceCollection services) work.
                    if (filters.Length == 0)
                    {
                        return startup(services);
                    }

                    Action<IServiceCollection> pipeline = InvokeStartup;
                    for (int i = 0; i < filters.Length; i++)
                    {
                        pipeline = filters[i].ConfigureServices(pipeline);
                    }

                    pipeline(services);

                    // We return null so that the host here builds the container (same result as void ConfigureServices(IServiceCollection services);
                    return null;

                    void InvokeStartup(IServiceCollection serviceCollection)
                    {
                        var result = startup(serviceCollection);
                        if (filters.Length > 0 && result != null)
                        {
                            // public IServiceProvider ConfigureServices(IServiceCollection serviceCollection) is not compatible with IStartupServicesFilter;
#pragma warning disable CS0612 // Type or member is obsolete
                            var message = $"A ConfigureServices method that returns an {nameof(IServiceProvider)} is " +
                                $"not compatible with the use of one or more {nameof(IStartupConfigureServicesFilter)}. " +
                                $"Use a void returning ConfigureServices method instead or a ConfigureContainer method.";
#pragma warning restore CS0612 // Type or member is obsolete
                            throw new InvalidOperationException(message);
                        };
                    }
                }
            }

            private Action<TContainerBuilder> BuildStartupConfigureContainerFiltersPipeline(Action<TContainerBuilder> configureContainer)
            {
                return RunPipeline;

                void RunPipeline(TContainerBuilder containerBuilder)
                {
                    var filters = HostingServiceProvider
#pragma warning disable CS0612 // Type or member is obsolete
                        .GetRequiredService<IEnumerable<IStartupConfigureContainerFilter<TContainerBuilder>>>()
#pragma warning restore CS0612 // Type or member is obsolete
                        .Reverse()
                        .ToArray();

                    Action<TContainerBuilder> pipeline = InvokeConfigureContainer;
                    for (int i = 0; i < filters.Length; i++)
                    {
                        pipeline = filters[i].ConfigureContainer(pipeline);
                    }

                    pipeline(containerBuilder);

                    void InvokeConfigureContainer(TContainerBuilder builder) => configureContainer(builder);
                }
            }
        }

        public static Type FindStartupType(string startupAssemblyName, string environmentName)
        {
            if (string.IsNullOrEmpty(startupAssemblyName))
            {
                throw new ArgumentException(
                    $"A startup method, startup type or startup assembly is required. If specifying an assembly, '{nameof(startupAssemblyName)}' cannot be null or empty.",
                    nameof(startupAssemblyName));
            }

            var assembly = Assembly.Load(new AssemblyName(startupAssemblyName));
            if (assembly == null)
            {
                throw new InvalidOperationException($"The assembly '{startupAssemblyName}' failed to load.");
            }

            var startupNameWithEnv = "Startup" + environmentName;
            var startupNameWithoutEnv = "Startup";

            // Check the most likely places first
            var type =
                assembly.GetType(startupNameWithEnv) ??
                assembly.GetType(startupAssemblyName + "." + startupNameWithEnv) ??
                assembly.GetType(startupNameWithoutEnv) ??
                assembly.GetType(startupAssemblyName + "." + startupNameWithoutEnv);

            if (type == null)
            {
                // Full scan
                var definedTypes = assembly.DefinedTypes.ToList();

                var startupType1 = definedTypes.Where(info => info.Name.Equals(startupNameWithEnv, StringComparison.OrdinalIgnoreCase));
                var startupType2 = definedTypes.Where(info => info.Name.Equals(startupNameWithoutEnv, StringComparison.OrdinalIgnoreCase));

                var typeInfo = startupType1.Concat(startupType2).FirstOrDefault();
                if (typeInfo != null)
                {
                    type = typeInfo.AsType();
                }
            }

            if (type == null)
            {
                throw new InvalidOperationException(
                    $"A type named '{startupNameWithEnv}' or '{startupNameWithoutEnv}' could not be found in assembly '{startupAssemblyName}'.");
            }

            return type;
        }

        internal static ConfigureBuilder FindConfigureDelegate(Type startupType, string environmentName)
        {
            var configureMethod = FindMethod(startupType, "Configure{0}", environmentName, typeof(void), required: true);
            return new ConfigureBuilder(configureMethod);
        }

        internal static ConfigureContainerBuilder FindConfigureContainerDelegate(Type startupType, string environmentName)
        {
            var configureMethod = FindMethod(startupType, "Configure{0}Container", environmentName, typeof(void), required: false);
            return new ConfigureContainerBuilder(configureMethod);
        }

        internal static bool HasConfigureServicesIServiceProviderDelegate(Type startupType, string environmentName)
        {
            return null != FindMethod(startupType, "Configure{0}Services", environmentName, typeof(IServiceProvider), required: false);
        }

        internal static ConfigureServicesBuilder FindConfigureServicesDelegate(Type startupType, string environmentName)
        {
            var servicesMethod = FindMethod(startupType, "Configure{0}Services", environmentName, typeof(IServiceProvider), required: false)
                ?? FindMethod(startupType, "Configure{0}Services", environmentName, typeof(void), required: false);
            return new ConfigureServicesBuilder(servicesMethod);
        }

        private static MethodInfo FindMethod(Type startupType, string methodName, string environmentName, Type returnType = null, bool required = true)
        {
            var methodNameWithEnv = string.Format(CultureInfo.InvariantCulture, methodName, environmentName);
            var methodNameWithNoEnv = string.Format(CultureInfo.InvariantCulture, methodName, "");

            var methods = startupType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            var selectedMethods = methods.Where(method => method.Name.Equals(methodNameWithEnv, StringComparison.OrdinalIgnoreCase)).ToList();
            if (selectedMethods.Count > 1)
            {
                throw new InvalidOperationException(
                    $"Having multiple overloads of method '{methodNameWithEnv}' is not supported.");
            }
            if (selectedMethods.Count == 0)
            {
                selectedMethods = methods.Where(method => method.Name.Equals(methodNameWithNoEnv, StringComparison.OrdinalIgnoreCase)).ToList();
                if (selectedMethods.Count > 1)
                {
                    throw new InvalidOperationException(
                        $"Having multiple overloads of method '{methodNameWithNoEnv}' is not supported.");
                }
            }

            var methodInfo = selectedMethods.FirstOrDefault();
            if (methodInfo == null)
            {
                if (required)
                {
                    throw new InvalidOperationException(
                        $"A public method named '{methodNameWithEnv}' or '{methodNameWithNoEnv}' could not be found in the '{startupType.FullName}' type.");

                }
                return null;
            }
            if (returnType != null && methodInfo.ReturnType != returnType)
            {
                if (required)
                {
                    throw new InvalidOperationException(
                        $"The '{methodInfo.Name}' method in the type '{startupType.FullName}' must have a return type of '{returnType.Name}'.");
                }
                return null;
            }
            return methodInfo;
        }
    }

    internal class StartupMethods
    {
        public StartupMethods(object instance, Action<IApplicationBuilder> configure, Func<IServiceCollection, IServiceProvider> configureServices)
        {
            Debug.Assert(configure != null);
            Debug.Assert(configureServices != null);

            StartupInstance = instance;
            ConfigureDelegate = configure;
            ConfigureServicesDelegate = configureServices;
        }

        public object StartupInstance { get; }
        public Func<IServiceCollection, IServiceProvider> ConfigureServicesDelegate { get; }
        public Action<IApplicationBuilder> ConfigureDelegate { get; }

    }

    internal class HostedServiceExecutor
    {
        private readonly IEnumerable<IHostedService> _services;
        private readonly ILogger<HostedServiceExecutor> _logger;

        public HostedServiceExecutor(ILogger<HostedServiceExecutor> logger, IEnumerable<IHostedService> services)
        {
            _logger = logger;
            _services = services;
        }

        public Task StartAsync(CancellationToken token)
        {
            return ExecuteAsync(service => service.StartAsync(token));
        }

        public Task StopAsync(CancellationToken token)
        {
            return ExecuteAsync(service => service.StopAsync(token), throwOnFirstFailure: false);
        }

        private async Task ExecuteAsync(Func<IHostedService, Task> callback, bool throwOnFirstFailure = true)
        {
            List<Exception> exceptions = null;

            foreach (var service in _services)
            {
                try
                {
                    await callback(service);
                }
                catch (Exception ex)
                {
                    if (throwOnFirstFailure)
                    {
                        throw;
                    }

                    if (exceptions == null)
                    {
                        exceptions = new List<Exception>();
                    }

                    exceptions.Add(ex);
                }
            }

            // Throw an aggregate exception if there were any exceptions
            if (exceptions != null)
            {
                throw new AggregateException(exceptions);
            }
        }
    }

    internal static class MethodInfoExtensions
    {
        // This version of MethodInfo.Invoke removes TargetInvocationExceptions
        public static object InvokeWithoutWrappingExceptions(this MethodInfo methodInfo, object obj, object[] parameters)
        {
            // These are the default arguments passed when methodInfo.Invoke(obj, parameters) are called. We do the same
            // here but specify BindingFlags.DoNotWrapExceptions to avoid getting TAE (TargetInvocationException)
            // methodInfo.Invoke(obj, BindingFlags.Default, binder: null, parameters: parameters, culture: null)

            return methodInfo.Invoke(obj, BindingFlags.DoNotWrapExceptions, binder: null, parameters: parameters, culture: null);
        }
    }
}

