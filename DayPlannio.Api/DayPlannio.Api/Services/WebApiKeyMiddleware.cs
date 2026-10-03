using System.Security.Claims;

namespace DayPlannio.Api.Services
{
    public class WebApiKeyMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly string _apiKey;

        public WebApiKeyMiddleware(RequestDelegate next, IConfiguration configuration)
        {
            _next = next;
            _apiKey = configuration.GetValue<string>("WebApiKey") ?? string.Empty;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!string.IsNullOrEmpty(_apiKey)
                && (context.User?.Identity?.IsAuthenticated ?? false) == false)
            {
                if (context.Request.Headers.TryGetValue("X-Api-Key", out var headerKey)
                    && string.Equals(headerKey, _apiKey, StringComparison.Ordinal))
                {
                    var claims = new[]
                    {
                        new Claim(ClaimTypes.Name, "web_server"),
                        new Claim(ClaimTypes.Role, "Admin")
                    };
                    var identity = new ClaimsIdentity(claims, "WebApiKey");
                    context.User = new ClaimsPrincipal(identity);
                }
            }

            await _next(context);
        }
    }
}
