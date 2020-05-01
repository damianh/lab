# Logging Configuration Values

**Motivation**: Using `Microsoft.Extensions.Configuration` I wanted to...

- ... log all of the configuration data at service startup.
- ... to see which configuration setting was in multiple provider(s), and from
  which provider the final value was chosen from.
- ... to redact sensitive values thus not writing secrets to the logs.

`ConfigurationRootExtensions.GetDebugView()` wasn't suitable:

- It doesn't show if a key is defined in multiple sources.
- It doesn't support redaction.

This repo contains some sample code, `GetDiagnosticView()`.

## Using

1. Clone the repo
2. `dotnet run -p .\ConsoleApp1\ConsoleApp1.csproj`
3. Explore `program.cs` to see how it works.

Example output:

```
*** Diagnostic View
ARCHITECTURE                   [ EnvironmentVariables (Prefix='PROCESSOR_') ]
IDENTIFIER                     [ EnvironmentVariables (Prefix='PROCESSOR_') ]
IsEnabled                      [ Json (Path=appsettings.json), Memory ]
LEVEL                          [ EnvironmentVariables (Prefix='PROCESSOR_') ]
Nested:Bar                     [ Json (Path=appsettings.json), Memory ]
REVISION                       [ EnvironmentVariables (Prefix='PROCESSOR_') ]
SomeSecret                     [ Memory ]

*** Settings as JSON with sensitive fields redacted.
{
  "IsEnabled": true,
  "SomeSecret": "**Redacted**",
  "Nested": {
    "Bar": "bob"
  }
}
```

## FAQ

**Q: Will this be a NuGet package?**

A: No.

## Feedback or improvements

Submit an issue or send on a PR :)
