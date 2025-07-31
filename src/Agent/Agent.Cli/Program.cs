using Agent.Cli.Commands;

// Build and execute the CLI commands
var root = CommandBuilder.BuildCommands();
return root.Parse(args).Invoke();
