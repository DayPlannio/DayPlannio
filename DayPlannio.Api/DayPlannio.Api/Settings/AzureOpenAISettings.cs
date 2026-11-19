namespace DayPlannio.Api.Settings
{
    public class AzureOpenAISettings
    {
        public string Endpoint { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string Deployment { get; set; } = string.Empty;
        public string ApiVersion { get; set; } = "2024-10-21";

        public bool Configurado =>
            !string.IsNullOrWhiteSpace(Endpoint) &&
            !string.IsNullOrWhiteSpace(ApiKey) &&
            !string.IsNullOrWhiteSpace(Deployment);
    }
}
