using Yapair.Core.Contracts;

namespace Yapair.Core.Verification;

/// <summary>
/// Collect -> compare -> ask -> optionally re-diagnose. The UI supplies the
/// prompt implementation (approval buttons); the CLI supplies a console prompt.
/// </summary>
public sealed class VerificationLoop
{
    public async Task<VerificationResult> RunAsync(
        SystemSnapshot baseline,
        Func<CancellationToken, Task<SystemSnapshot>> recollect,
        IVerificationPrompt prompt,
        VerificationOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(recollect);
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(options);

        var maxAttempts = Math.Clamp(options.MaxAttempts, 1, 5);
        var current = await recollect(cancellationToken).ConfigureAwait(false);
        var result = SnapshotComparer.Compare(baseline, current, options.PrinterName);
        var attempts = 1;

        while (true)
        {
            var user = await prompt.AskOutcomeAsync(result, cancellationToken).ConfigureAwait(false);
            result = SnapshotComparer.ApplyUserConfirmation(result, user) with { Attempts = attempts };

            if (result.Outcome is not (VerificationOutcome.NotResolved or VerificationOutcome.Contradicted))
            {
                return result;
            }

            if (attempts >= maxAttempts)
            {
                return result;
            }

            if (!await prompt.AskReDiagnoseAsync(result, cancellationToken).ConfigureAwait(false))
            {
                return result;
            }

            baseline = current;
            current = await recollect(cancellationToken).ConfigureAwait(false);
            result = SnapshotComparer.Compare(baseline, current, options.PrinterName);
            attempts++;
        }
    }
}
