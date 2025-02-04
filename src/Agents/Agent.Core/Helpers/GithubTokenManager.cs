namespace Agents.Core.Helpers;
public static class GitHubTokenManager
{
    private static readonly string TokenFilePath = Path.GetTempPath();
    private static readonly SemaphoreSlim FileLock = new SemaphoreSlim(1, 1);

    public static async Task<bool> TokenExistsAsync()
    {
        await FileLock.WaitAsync();
        try
        {
            string userTokenPath = Path.Combine(TokenFilePath, $"new1_token.txt");
            return File.Exists(userTokenPath);
        }
        finally
        {
            FileLock.Release();
        }
    }

    public static async Task SaveTokenAsync(string token)
    {
        await FileLock.WaitAsync();
        try
        {
            Directory.CreateDirectory(TokenFilePath);
            string userTokenPath = Path.Combine(TokenFilePath, $"new1_token.txt");
            await File.WriteAllTextAsync(userTokenPath, token);
        }
        finally
        {
            FileLock.Release();
        }
    }

    public static async Task<string> GetTokenAsync()
    {
        await FileLock.WaitAsync();
        try
        {
            string userTokenPath = Path.Combine(TokenFilePath, $"new1_token.txt");
            if (!File.Exists(userTokenPath))
            {
                throw new FileNotFoundException($"No token found for new1");
            }
            return await File.ReadAllTextAsync(userTokenPath);
        }
        finally
        {
            FileLock.Release();
        }
    }

    public static async Task DeleteTokenAsync()
    {
        await FileLock.WaitAsync();
        try
        {
            string userTokenPath = Path.Combine(TokenFilePath, $"new1_token.txt");
            if (File.Exists(userTokenPath))
            {
                File.Delete(userTokenPath);
            }
        }
        finally
        {
            FileLock.Release();
        }
    }
}
