namespace Acmebot.Cli;

internal sealed class CommandLine
{
    private readonly Dictionary<string, List<string?>> _options;

    private CommandLine(IReadOnlyList<string> arguments, Dictionary<string, List<string?>> options)
    {
        Arguments = arguments;
        _options = options;
    }

    public IReadOnlyList<string> Arguments { get; }

    public IEnumerable<string> OptionNames => _options.Keys;

    public static CommandLine Parse(string[] args)
    {
        var arguments = new List<string>();
        var options = new Dictionary<string, List<string?>>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];

            if (arg == "--")
            {
                arguments.AddRange(args.Skip(index + 1));
                break;
            }

            if (arg == "-h")
            {
                AddOption(options, "help", null);
                continue;
            }

            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                arguments.Add(arg);
                continue;
            }

            var optionText = arg[2..];
            var separatorIndex = optionText.IndexOf('=');
            string name;
            string? value;

            if (separatorIndex >= 0)
            {
                name = optionText[..separatorIndex];
                value = optionText[(separatorIndex + 1)..];
            }
            else
            {
                name = optionText;
                value = null;

                if (CliOptionNames.OptionsWithValues.Contains(name))
                {
                    if (index + 1 >= args.Length || args[index + 1].StartsWith("-", StringComparison.Ordinal))
                    {
                        throw new CliException($"Option '--{name}' requires a value.");
                    }

                    value = args[++index];
                }
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new CliException("Option name cannot be empty.");
            }

            AddOption(options, name, value);
        }

        return new CommandLine(arguments, options);
    }

    public bool HasOption(string name) => _options.ContainsKey(name);

    public string? GetOption(string name)
    {
        return _options.TryGetValue(name, out var values) ? values.LastOrDefault() : null;
    }

    public IReadOnlyList<string> GetOptions(string name)
    {
        return _options.TryGetValue(name, out var values)
            ? values.Where(value => value is not null).Cast<string>().ToArray()
            : [];
    }

    public bool GetFlag(string name)
    {
        if (!_options.TryGetValue(name, out var values))
        {
            return false;
        }

        var value = values.LastOrDefault();

        if (value is null)
        {
            return true;
        }

        if (bool.TryParse(value, out var result))
        {
            return result;
        }

        throw new CliException($"Option '--{name}' expects true or false when a value is provided.");
    }

    private static void AddOption(Dictionary<string, List<string?>> options, string name, string? value)
    {
        if (!options.TryGetValue(name, out var values))
        {
            values = [];
            options.Add(name, values);
        }

        values.Add(value);
    }
}
