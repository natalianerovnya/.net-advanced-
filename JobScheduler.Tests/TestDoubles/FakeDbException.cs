using System.Data.Common;

namespace JobScheduler.Tests.TestDoubles;

internal sealed class FakeDbException(string message) : DbException(message)
{
}
