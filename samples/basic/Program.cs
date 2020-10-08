using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Rest; 
using k8s.Operators.Logging;
using Newtonsoft.Json;

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

            try
            {
                logger.LogDebug($"Environment variables: {JsonConvert.SerializeObject(Environment.GetEnvironmentVariables())}");

                // Setup termination handlers
                SetupSignalHandlers();

                // Setup the Kubernetes client
                using var client = SetupClient(args);

                var watchNamespace = Environment.GetEnvironmentVariable("WATCH_NAMESPACE") ?? "";

                // Setup the operator
                @operator = new Operator(client, loggerFactory)
                    .AddController(new MyResourceController(client, loggerFactory), watchNamespace);

                // Start the operator
                await @operator.StartAsync();
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Operator error");
                return 1;
            }

            return 0;

            IKubernetes SetupClient(string[] args)
            {
                // Load the Kubernetes configuration
                KubernetesClientConfiguration config = null;
                
                if (KubernetesClientConfiguration.IsInCluster())
                {
                    logger.LogDebug("Loading cluster configuration");
                    config = KubernetesClientConfiguration.InClusterConfig();
                }
                else
                {
                    logger.LogDebug("Loading local configuration");
                    config = KubernetesClientConfiguration.BuildConfigFromConfigFile();
                }

                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug($"Client configuration: {JsonConvert.SerializeObject(config)}");
                }
                
                return new Kubernetes(config);
            }

            ILoggerFactory SetupLogging(string[] args)
            {
                if (!System.Enum.TryParse<LogLevel>(Environment.GetEnvironmentVariable("LOG_LEVEL"), true, out LogLevel logLevel))
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
