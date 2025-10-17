// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;

namespace Agent.Cli.Services;

public class ThreadManagerService
{
    private readonly string _threadsFile = "threads.json";
    private readonly string _currentThreadFile = ".current-thread";

    public class ThreadInfo
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public DateTime LastUsedAt { get; set; }
    }

    public class ThreadStorage
    {
        public List<ThreadInfo> Threads { get; set; } = [];
    }

    public async Task<string?> GetCurrentThreadIdAsync()
    {
        try
        {
            if (File.Exists(_currentThreadFile))
            {
                return await File.ReadAllTextAsync(_currentThreadFile);
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task SetCurrentThreadIdAsync(string threadId)
    {
        try
        {
            await File.WriteAllTextAsync(_currentThreadFile, threadId);
        }
        catch
        {
            // Ignore errors
        }
    }

    public async Task<List<ThreadInfo>> GetThreadsAsync()
    {
        try
        {
            if (File.Exists(_threadsFile))
            {
                var json = await File.ReadAllTextAsync(_threadsFile);
                var storage = JsonSerializer.Deserialize<ThreadStorage>(json);
                return storage?.Threads ?? [];
            }
            return [];
        }
        catch
        {
            return [];
        }
    }

    public async Task AddThreadAsync(string threadId, string title)
    {
        try
        {
            var threads = await GetThreadsAsync();
            var threadInfo = new ThreadInfo
            {
                Id = threadId,
                Title = title,
                CreatedAt = DateTime.Now,
                LastUsedAt = DateTime.Now
            };

            // Remove existing thread with same ID if it exists
            threads.RemoveAll(t => t.Id == threadId);
            threads.Add(threadInfo);

            // Keep only the last 100 threads
            if (threads.Count > 100)
            {
                threads = [.. threads.OrderByDescending(t => t.LastUsedAt).Take(100)];
            }

            var storage = new ThreadStorage { Threads = threads };
            var json = JsonSerializer.Serialize(storage, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_threadsFile, json);

            // Set as current thread
            await SetCurrentThreadIdAsync(threadId);
        }
        catch
        {
            // Ignore errors
        }
    }

    public async Task UpdateThreadLastUsedAsync(string threadId)
    {
        try
        {
            var threads = await GetThreadsAsync();
            var thread = threads.FirstOrDefault(t => t.Id == threadId);
            if (thread != null)
            {
                thread.LastUsedAt = DateTime.Now;
                var storage = new ThreadStorage { Threads = threads };
                var json = JsonSerializer.Serialize(storage, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_threadsFile, json);
            }

            // Set as current thread
            await SetCurrentThreadIdAsync(threadId);
        }
        catch
        {
            // Ignore errors
        }
    }

    public async Task<bool> DeleteThreadAsync(string threadId)
    {
        try
        {
            var threads = await GetThreadsAsync();
            var removed = threads.RemoveAll(t => t.Id == threadId) > 0;

            if (removed)
            {
                var storage = new ThreadStorage { Threads = threads };
                var json = JsonSerializer.Serialize(storage, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_threadsFile, json);

                // Clear current thread if it was the deleted one
                var currentThreadId = await GetCurrentThreadIdAsync();
                if (currentThreadId == threadId)
                {
                    if (File.Exists(_currentThreadFile))
                    {
                        File.Delete(_currentThreadFile);
                    }
                }
            }

            return removed;
        }
        catch
        {
            return false;
        }
    }
}
