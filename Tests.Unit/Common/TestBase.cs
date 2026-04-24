

using Moq;

namespace Tests.Unit.Common;


public abstract class TestBase
{
    protected readonly MockRepository MockRepository;

    protected TestBase()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
    }

    protected static Guid CreateRetailerId() => Guid.NewGuid();

    protected static Guid CreateGuid() => Guid.NewGuid();

    protected static DateTime UtcNow => DateTime.UtcNow;
}