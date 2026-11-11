using DayPlannio.Api.IntegrationTests.Infra;
using Xunit;

namespace DayPlannio.Web.IntegrationTests.Infra;

public sealed class PortalFixture : IDisposable
{
    public ApiFactory Api { get; } = new();
    public WebFactory Web { get; }

    public PortalFixture() => Web = new WebFactory(Api);

    public void Dispose()
    {
        Web.Dispose();
        Api.Dispose();
    }
}

[CollectionDefinition(Nome)]
public sealed class PortalCollection : ICollectionFixture<PortalFixture>
{
    public const string Nome = "Portal Web + API";
}
