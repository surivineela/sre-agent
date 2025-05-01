namespace DotnetAnalyzer;

internal class Program
{
    static void Main(string[] args)
    {
        // args[0] = Command.
        // args[1] = Artifact path.
        if (args.Length < 2)
        {
            throw new ArgumentException("Invalid arguments. Please provide the command and artifact path.");
        }

        string command = args[0];
        string artifactPath = args[1];

        switch (command)
        {
            case "analyze-memory":
                Console.WriteLine(AnalyzeMemoryCommand.AnalyzeMemory(artifactPath));
                break;
            default:
                throw new ArgumentException($"Unknown command: {command}");
        }
    }
}
