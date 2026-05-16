using Aspire.Hosting.Azure;

namespace PicoBusX.AppHost
{
    internal static class ServiceBusResourceBuilderExtensions
    {
        public static IResourceBuilder<AzureServiceBusResource> WithAdminConnectionStringEnvironment(
            this IResourceBuilder<AzureServiceBusResource> serviceBus,
            IResourceBuilder<ProjectResource> targetProject,
            string environmentVariableName = "SERVICEBUS_ADMINCONNECTIONSTRING")
        {
            // Capture the management endpoint reference at definition time.
            // emulatorhealth maps container port 5300 (HTTP REST management API) to a host port.
            var managementEndpoint = serviceBus.GetEndpoint("emulatorhealth");

            serviceBus.OnConnectionStringAvailable(async (resource, _, ct) =>
            {
                var connectionString = await resource.GetConnectionProperty("connectionString").GetValueAsync(ct)
                                       ?? throw new InvalidOperationException(
                                           "Service Bus connection string is not available.");
                var adminConnectionString = BuildAdminConnectionString(connectionString, managementEndpoint);
                targetProject.WithEnvironment(environmentVariableName, adminConnectionString);
            });

            return serviceBus;
        }

        private static string BuildAdminConnectionString(
            string amqpConnectionString,
            EndpointReference managementEndpoint)
        {
            var parsedValues = ParseConnectionString(amqpConnectionString);

            // The ServiceBusAdministrationClient uses the REST management API (HTTP) on port 5300.
            // Docker maps container port 5300 to managementEndpoint.Port on the host.
            // Always use "localhost" — Docker containers are reachable on localhost from the host machine.
            var adminConnectionString = $"Endpoint=sb://localhost:{managementEndpoint.Port}";

            if (parsedValues.TryGetValue("SharedAccessKeyName", out var keyName))
            {
                adminConnectionString += $";SharedAccessKeyName={keyName}";
            }

            if (parsedValues.TryGetValue("SharedAccessKey", out var key))
            {
                adminConnectionString += $";SharedAccessKey={key}";
            }

            // Always required for the Azure SDK to use HTTP instead of AMQP for management calls
            // when connecting to the local emulator.
            adminConnectionString += ";UseDevelopmentEmulator=true";

            return adminConnectionString;
        }

        private static Dictionary<string, string> ParseConnectionString(string connectionString)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var keyValue = part.Split('=', 2);
                if (keyValue.Length == 2)
                {
                    result[keyValue[0].Trim()] = keyValue[1].Trim();
                }
            }

            return result;
        }
    }
}