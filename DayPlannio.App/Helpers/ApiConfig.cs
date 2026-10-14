namespace DayPlannio.App.Helpers
{
    public static class ApiConfig
    {
        // URL pública da API hospedada na VM (nginx -> API).
        public const string NuvemUrl = "http://4.204.238.10/";

        // Para testar contra a API local sem recompilar, defina a variável de
        // ambiente DAYPLANNIO_API_URL (ex.: http://localhost:5143/).
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
