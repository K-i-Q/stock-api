using StockApi.Tests.Infra;
using Xunit;

namespace StockApi.Tests.Integration;

public abstract class TestBase : IClassFixture<CustomWebAppFactory>
{
    protected readonly CustomWebAppFactory Factory;

    protected TestBase(CustomWebAppFactory factory)
    {
        Factory = factory;
    }
}
