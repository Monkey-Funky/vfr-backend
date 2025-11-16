namespace Shared.Constants;

public static class CacheKeys
{
    public const string UserPrefix = "User_";
    public const string AllUsers = "All_Users";

    public static string GetUserKey(Guid userId) => $"{UserPrefix}{userId}";
}
