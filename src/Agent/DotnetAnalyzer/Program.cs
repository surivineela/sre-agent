namespace DotnetAnalyzer;

internal class Program
{
    static async Task Main(string[] args)
    {
        // args[0] = Command.
        // args[1] = Artifact path.
        if (args.Length < 2)
        {
            throw new ArgumentException("Invalid arguments. Please provide the command and artifact path.");
        }

        if (string.IsNullOrWhiteSpace(args[0]) || string.IsNullOrWhiteSpace(args[1]))
        {
            throw new ArgumentException("Command and artifact path cannot be null or empty.");
        }

        if (!File.Exists(args[1]))
        {
            throw new FileNotFoundException($"Artifact file not found: {args[1]}");
        }

        string command = args[0];
        string artifactPath = args[1];

        switch (command.ToLower())
        {
            case "analyze-memory":
                Console.WriteLine(AnalyzeMemoryCommand.AnalyzeMemory(artifactPath));
                break;
            case "analyze-latency":
                Console.WriteLine(await AnalyzeLatencyCommand.AnalyzeLatencyAsync(artifactPath));
                break;
            default:
                throw new ArgumentException($"Unknown command: {command}");
        }
    }
}
