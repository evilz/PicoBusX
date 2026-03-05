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
            serviceBus.OnConnectionStringAvailable(async (resource, _, ct) =>
            {
                var connectionString = await resource.GetConnectionProperty("connectionString").GetValueAsync(ct)
                                       ?? throw new InvalidOperationException(
                                           "Service Bus connection string is not available.");
                var adminConnectionString = BuildAdminConnectionString(serviceBus, connectionString);
                targetProject.WithEnvironment(environmentVariableName, adminConnectionString);
            });

            return serviceBus;
        }

        private static string BuildAdminConnectionString(
            IResourceBuilder<AzureServiceBusResource> serviceBus,
            string connectionString)
        {
            var parsedValues = ParseConnectionString(connectionString);
            var healthEndpoint = serviceBus.GetEndpoint("emulatorhealth");

            var adminConnectionString = $"Endpoint=sb://{healthEndpoint.Host}:{healthEndpoint.Port}";

            if (parsedValues.TryGetValue("SharedAccessKeyName", out var keyName))
            {
                adminConnectionString += $";SharedAccessKeyName={keyName}";
            }

            if (parsedValues.TryGetValue("SharedAccessKey", out var key))
            {
                adminConnectionString += $";SharedAccessKey={key}";
            }

            if (parsedValues.ContainsKey("UseDevelopmentEmulator"))
            {
                adminConnectionString += ";UseDevelopmentEmulator=true";
            }

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