using Yapair.Core.Verification;

namespace Yapair.Cli;

/// <summary>
/// CLI implementation of the approval buttons. The desktop UI will implement the
/// same interface with real buttons; the core loop does not depend on the shell.
/// </summary>
internal sealed class ConsoleVerificationPrompt : IVerificationPrompt
{
    private readonly TextReader _input;
    private readonly TextWriter _output;

    public ConsoleVerificationPrompt(TextReader input, TextWriter output)
    {
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _output = output ?? throw new ArgumentNullException(nameof(output));
    }

    public Task<UserConfirmation> AskOutcomeAsync(VerificationResult result, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _output.WriteLine();
        _output.WriteLine($"Otomatik doğrulama: {CliSummary.OutcomeText(result.Outcome)}");
        _output.WriteLine("Sorun çözüldü mü?");
        _output.WriteLine("  [e] Evet, çözüldü");
        _output.WriteLine("  [h] Hayır, devam ediyor");
        _output.WriteLine("  [b] Bilmiyorum");

        for (var attempt = 0; attempt < 3; attempt++)
        {
            _output.Write("Seçim: ");
            var line = _input.ReadLine();
            if (line is null)
            {
                return Task.FromResult(UserConfirmation.NotAsked);
            }

            switch (line.Trim().ToLowerInvariant())
            {
                case "e":
                case "evet":
                case "y":
                case "yes":
                    return Task.FromResult(UserConfirmation.Resolved);
                case "h":
                case "hayır":
                case "hayir":
                case "n":
                case "no":
                    return Task.FromResult(UserConfirmation.NotResolved);
                case "b":
                case "bilmiyorum":
                case "?":
                case "":
                    return Task.FromResult(UserConfirmation.Unsure);
                default:
                    _output.WriteLine("Geçersiz seçim. e, h veya b girin.");
                    break;
            }
        }

        return Task.FromResult(UserConfirmation.Unsure);
    }

    public Task<bool> AskReDiagnoseAsync(VerificationResult result, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _output.WriteLine();
        _output.WriteLine("Sorun sürüyor. Kök neden farklı olabilir; yeniden tanılama yapılsın mı? [e]/[h]");

        for (var attempt = 0; attempt < 3; attempt++)
        {
            _output.Write("Seçim: ");
            var line = _input.ReadLine();
            if (line is null)
            {
                return Task.FromResult(false);
            }

            switch (line.Trim().ToLowerInvariant())
            {
                case "e":
                case "evet":
                case "y":
                case "yes":
                    return Task.FromResult(true);
                case "h":
                case "hayır":
                case "hayir":
                case "n":
                case "no":
                case "":
                    return Task.FromResult(false);
                default:
                    _output.WriteLine("Geçersiz seçim. e veya h girin.");
                    break;
            }
        }

        return Task.FromResult(false);
    }
}
