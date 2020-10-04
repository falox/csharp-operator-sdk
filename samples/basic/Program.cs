using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Rest; 
using k8s.Operators.Logging;

namespace k8s.Operators.Samples.Basic
{
    class Program
    {
        static async Task<int> Main(string[] args) 
        {
            IOperator @operator = null;

            // Setup logging
            using var loggerFactory = SetupLogging(args);
            var logger = loggerFactory.CreateLogger<Program>();

            // Setup termination handlers
            SetupSignalHandlers();

            try
            {
                // Setup the Kubernetes client
                using var client = SetupClient();

                // Setup the operator
                @operator = new Operator(client, loggerFactory)
                    .AddController(new MyResourceController(client, loggerFactory));

                // Start the operator
                await @operator.StartAsync();
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Operator error");
                return 1;
            }

            return 0;

            IKubernetes SetupClient()
            {
                // Load the Kubernetes configuration
                KubernetesClientConfiguration config = KubernetesClientConfiguration.IsInCluster() 
                    ? KubernetesClientConfiguration.InClusterConfig()
                    : KubernetesClientConfiguration.BuildConfigFromConfigFile();


                if (logger.IsEnabled(LogLevel.Debug))
                {
                    var configString = config
                        .GetType()
                        .GetProperties()
                        .ToDictionary(x => x.Name, x => x.GetValue(config)?.ToString() ?? "NULL")
                        .AsFormattedString();
                    
                    logger.LogDebug($"Client configuration: {configString}");
                }
                
                return new Kubernetes(config);
            }

            ILoggerFactory SetupLogging(string[] args)
            {
                LogLevel logLevel = LogLevel.Information;

                if (args.Contains("--debug"))
                {
                    logLevel = LogLevel.Debug;
                }

                var loggerFactory = LoggerFactory.Create(builder => builder
                    .AddConsole(options => options.Format=ConsoleLoggerFormat.Systemd)
                    .SetMinimumLevel(logLevel)
                );

                // Enable Kubernetes client logging if level = DEBUG
                ServiceClientTracing.IsEnabled = logLevel <= LogLevel.Debug;
                ServiceClientTracing.AddTracingInterceptor(new ConsoleTracingInterceptor());

                return loggerFactory;
            }

            void SetupSignalHandlers()
            {
                // SIGTERM: signal the operator to shut down gracefully
                AppDomain.CurrentDomain.ProcessExit += (s, e) => 
                {
                    logger.LogDebug("Received SIGTERM");
                    @operator?.Stop();
                };

                // SIGINT: try to shut down gracefully on the first attempt
                Console.CancelKeyPress += (s, e) => 
                {
                    bool isFirstSignal = !@operator.IsDisposing;
                    logger.LogDebug($"Received SIGINT, first signal: {isFirstSignal}");
                    if (isFirstSignal)
                    {
                        e.Cancel = true;
                        Environment.Exit(0);
                    }
                };
            }
        }
    }
}
