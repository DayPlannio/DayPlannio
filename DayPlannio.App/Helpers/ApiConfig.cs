namespace DayPlannio.App.Helpers
{
    public static class ApiConfig
    {
        public const string NuvemUrl = "http://dayplannio.canadacentral.cloudapp.azure.com/";

        public static string BaseUrl
        {
            get
            {
                var url = Environment.GetEnvironmentVariable("DAYPLANNIO_API_URL");
                return string.IsNullOrWhiteSpace(url) ? NuvemUrl : url;
            }
        }
    }
}
