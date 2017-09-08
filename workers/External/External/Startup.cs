using System;
using System.Reflection;
using Improbable.Worker;

namespace External
{
    internal class Startup
    {
        private const string WorkerType = "External";

        private const string LoggerName = "Startup.cs";

        private const int ErrorExitStatus = 1;

        private const uint GetOpListTimeoutInMilliseconds = 100;

        private static int Main(string[] args)
        {
            #region Check arguments

            if (args.Length < 1) {
                PrintUsage();
                return ErrorExitStatus;
            }

            if (args[0] != "receptionist" && args[0] != "locator") {
                PrintUsage();
                return ErrorExitStatus;
            }

            bool useLocator = (args[0] == "locator");

            if (useLocator && args.Length != 5 || !useLocator && args.Length != 4) {
                PrintUsage();
                return ErrorExitStatus;
            }

            #endregion

            // Avoid missing component errors because no components are directly used in this project
            // and the GeneratedCode assembly is not loaded but it should be
            Assembly.Load("GeneratedCode");

            var connectionParameters = new ConnectionParameters
            {
                WorkerType = WorkerType,
                Network =
                {
                    ConnectionType = NetworkConnectionType.Tcp,

                    // Local clients connecting to a local deployment shouldn't use external IP
                    // Clients connecting to a cloud deployment using the locator should
                    // Consider exposing this as a command-line option if you have an advanced configuration
                    // See table: https://docs.improbable.io/reference/11.0/workers/csharp/using#connecting-to-spatialos
                    UseExternalIp = useLocator
                }
            };

            using (var connection = useLocator
                ? ConnectClientWithLocator(args[1], args[2], args[3], args[4], connectionParameters)
                : ConnectClientWithReceptionist(args[1], Convert.ToUInt16(args[2]), args[3], connectionParameters))
            using (var dispatcher = new Dispatcher())
            {
                var isConnected = true;

                dispatcher.OnDisconnect(op =>
                {
                    Console.Error.WriteLine("[disconnect] " + op.Reason);
                    isConnected = false;
                });

                dispatcher.OnLogMessage(op =>
                {
                    connection.SendLogMessage(op.Level, LoggerName, op.Message);
                    if (op.Level == LogLevel.Fatal)
                    {
                        Console.Error.WriteLine("Fatal error: " + op.Message);
                        Environment.Exit(ErrorExitStatus);
                    }
                });

                while (isConnected)
                {
                    using (var opList = connection.GetOpList(GetOpListTimeoutInMilliseconds))
                    {
                        dispatcher.Process(opList);
                    }
                }
            }

            // This means we forcefully disconnected
            return ErrorExitStatus;
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage: mono External.exe receptionist <hostname> <port> <worker_id>");
            Console.WriteLine("       mono External.exe locator <hostname> <project_name> <deployment_id> <login_token>");
            Console.WriteLine("Connects to SpatialOS");
            Console.WriteLine("    <hostname>      - hostname of the receptionist or locator to connect to.");
            Console.WriteLine("    <port>          - port to use if connecting through the receptionist.");
            Console.WriteLine("    <worker_id>     - name of the worker assigned by SpatialOS.");
            Console.WriteLine("    <project_name>  - name of the project to run.");
            Console.WriteLine("    <deployment_id> - name of the cloud deployment to run.");
            Console.WriteLine("    <login_token>   - token to use when connecting through the locator.");
        }

        private static Connection ConnectClientWithLocator(string hostname, string projectName, string deploymentId,
            string loginToken, ConnectionParameters connectionParameters)
        {
            Connection connection;
            connectionParameters.Network.UseExternalIp = true;

            var locatorParameters = new LocatorParameters
            {
                ProjectName = projectName,
                CredentialsType = LocatorCredentialsType.LoginToken,
                LoginToken = {Token = loginToken}
            };

            var locator = new Locator(hostname, locatorParameters);

            using (var future = locator.ConnectAsync(deploymentId, connectionParameters, QueueCallback))
            {
                connection = future.Get();
            }

            connection.SendLogMessage(LogLevel.Info, LoggerName, "Successfully connected using the Locator");

            return connection;
        }

        private static bool QueueCallback(QueueStatus queueStatus)
        {
            if (!string.IsNullOrEmpty(queueStatus.Error))
            {
                Console.Error.WriteLine("Error while queueing: " + queueStatus.Error);
                Environment.Exit(ErrorExitStatus);
            }
            Console.WriteLine("Worker of type '" + WorkerType + "' connecting through locator: queueing.");
            return true;
        }

        private static Connection ConnectClientWithReceptionist(string hostname, ushort port,
            string workerId, ConnectionParameters connectionParameters)
        {
            Connection connection;

            using (var future = Connection.ConnectAsync(hostname, port, workerId, connectionParameters))
            {
                connection = future.Get();
            }

            connection.SendLogMessage(LogLevel.Info, LoggerName, "Successfully connected using the Receptionist");

            return connection;
        }
    }
}