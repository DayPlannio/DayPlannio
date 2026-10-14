namespace DayPlannio.App.Helpers
{
    public static class ApiConfig
    {
        // URL pública da API hospedada na nuvem (Azure App Service).
        // Ajuste caso o nome do seu App Service seja diferente.
        public const string NuvemUrl = "https://dayplannio-api.azurewebsites.net/";

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
