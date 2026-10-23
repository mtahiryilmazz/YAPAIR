using System.Text;
using Yapair.Cli;

Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

try
{
    return await CliApplication.RunAsync(args, Console.Out, Console.Error, Console.In, cancellation.Token);
}
catch (OperationCanceledException)
{
    return 130;
}
