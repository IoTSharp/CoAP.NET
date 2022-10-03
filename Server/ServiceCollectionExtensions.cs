namespace Server
{
    using System.Reflection;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using WorldDirect.CoAP;
    using WorldDirect.CoAP.Codes;
    using WorldDirect.CoAP.Codes.MethodCodes;
    using WorldDirect.CoAP.V1;
    using WorldDirect.CoAP.V1.Options;

    public static class ServiceCollectionExtensions
    {
        private static IServiceCollection AddCoapCodes(this IServiceCollection services)
        {
            return services.AddContentFormats(typeof(Get).Assembly);
        }

        private static IServiceCollection AddReaders(this IServiceCollection services)
        {
            return services.AddContentFormats(typeof(HeaderReader).Assembly);
        }

        public static IServiceCollection AddReaders(this IServiceCollection services, params Assembly[] assemblies)
        {
            return services.Scan(scan =>
            {
                scan.FromAssemblies(assemblies)
                    .AddClasses(c => c.AssignableTo(typeof(IReader<>)))
                    .AsImplementedInterfaces()
                    .WithTransientLifetime();
            });
        }

        public static IServiceCollection AddCoapCodes(this IServiceCollection services, params Assembly[] assemblies)
        {
            return services.Scan(scan =>
            {
                scan.FromAssemblies(assemblies)
                    .AddClasses(c => c.AssignableTo<CoapCode>())
                    .As<CoapCode>()
                    .WithTransientLifetime();
            });
        }

        private static IServiceCollection AddOptionFactories(this IServiceCollection services)
        {
            return services.AddContentFormats(typeof(Accept).Assembly);
        }

        public static IServiceCollection AddOptionFactories(this IServiceCollection services, params Assembly[] assemblies)
        {
            return services.Scan(scan =>
            {
                scan.FromAssemblies(assemblies)
                    .AddClasses(c => c.AssignableTo<IOptionFactory>())
                    .AsImplementedInterfaces()
                    .WithTransientLifetime();
            });
        }

        public static IServiceCollection AddContentFormats(this IServiceCollection services)
        {
            return services.AddContentFormats(typeof(JsonFormat).Assembly);
        }

        public static IServiceCollection AddContentFormats(this IServiceCollection services, params Assembly[] assemblies)
        {
            return services.Scan(scan =>
            {
                scan.FromAssemblies(assemblies)
                    .AddClasses(c => c.AssignableTo<ContentFormat>())
                    .As<ContentFormat>()
                    .WithTransientLifetime();
            });
        }

        public static IServiceCollection UseRFC7252Specification(this IServiceCollection services)
        {
            services.AddTransient<IMessageSerializer, CoapMessageSerializer>(s =>
            {
                var codeRegistry = s.GetRequiredService<CodeRegistry>();
                var logger = s.GetRequiredService<ILogger<CoapMessageSerializer>>();
                var optionFactories = s.GetServices<IOptionFactory>();
                return new CoapMessageSerializer(new HeaderReader(codeRegistry), new TokenReader(), new OptionsReader(optionFactories), new PayloadReader(), logger);
            });

            services.AddCoapCodes();
            services.AddOptionFactories();
            services.AddTransient<CodeRegistry>();
            services.AddContentFormats();
            services.AddTransient<ContentFormatRegistry>();
            return services;
        }
    }
}
