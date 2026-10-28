namespace DayPlannio.App.Helpers
{
    public static class ApiConfig
    {
        public const string NuvemUrl = "http://localhost:5143/";

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
