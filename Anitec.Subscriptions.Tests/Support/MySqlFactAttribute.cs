namespace Anitec.Subscriptions.Tests.Support;

/// <summary>
///     A test that needs the real MySQL database (unique indexes, concurrent writes). It is reported as skipped,
///     not as passed, when SUBSCRIPTIONS_TEST_MYSQL is not set.
/// </summary>
public sealed class MySqlFactAttribute : FactAttribute
{
    public MySqlFactAttribute()
    {
        if (!SubscriptionsApiFactory.UsesMySql)
            Skip = "Set SUBSCRIPTIONS_TEST_MYSQL to a throwaway MySQL connection string to run this test.";
    }
}
