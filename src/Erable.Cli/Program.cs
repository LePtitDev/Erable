using System.Text;
using Erable.Cli;

// JSON is UTF-8, whatever the code page of the console (Windows defaults to an OEM one).
Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
return await CliApp.RunAsync(args, Console.Out, Console.Error).ConfigureAwait(false);
