using Yapair.Core.Contracts;
using Yapair.Core.Verification;
using Yapair.TestSupport;

namespace Yapair.Core.Tests;

public sealed class VerificationTests
{
    [Fact]
    public void MissingBaselineProblemBecomesResolved()
    {
        var baseline = SnapshotFixture.WithPrinterProblem();
        var current = SnapshotFixture.CreateHealthy("snap-bbbbbbbbbbbb");

        var result = SnapshotComparer.Compare(baseline, current, "Test Printer");

        Assert.Equal(VerificationOutcome.Resolved, result.Outcome);
        Assert.Single(result.BaselineSignals);
        Assert.Single(result.ResolvedSignals);
        Assert.Empty(result.PersistingSignals);
        Assert.True(result.Outcome == VerificationOutcome.Resolved);
    }

    [Fact]
    public void VerificationResultMatchesV1SchemaAndRoundTrips()
    {
        var result = SnapshotComparer.Compare(
            SnapshotFixture.WithPrinterProblem(),
            SnapshotFixture.CreateHealthy("snap-bbbbbbbbbbbb"),
            "Test Printer");

        var validation = Yapair.Core.Serialization.VerificationSchema.Validate(result);
        Assert.True(validation.IsValid, validation.DescribeErrors());

        var json = Yapair.Core.Serialization.VerificationJson.Serialize(result, indented: true);
        var roundTripped = Yapair.Core.Serialization.VerificationJson.Deserialize(json);
        Assert.Equal(result.Outcome, roundTripped.Outcome);
        Assert.Equal(result.BaselineSignals.Count, roundTripped.BaselineSignals.Count);
        Assert.DoesNotContain("\"key\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidVerificationIdFailsSchema()
    {
        var result = SnapshotComparer.Compare(
            SnapshotFixture.WithPrinterProblem(),
            SnapshotFixture.CreateHealthy("snap-bbbbbbbbbbbb"),
            "Test Printer") with
        {
            VerificationId = "not-a-valid-id",
        };

        var validation = Yapair.Core.Serialization.VerificationSchema.Validate(result);
        Assert.False(validation.IsValid);
    }

    [Fact]
    public void PersistingProblemIsNotResolved()
    {
        var baseline = SnapshotFixture.WithPrinterProblem();
        var current = SnapshotFixture.WithPrinterProblem("snap-bbbbbbbbbbbb");

        var result = SnapshotComparer.Compare(baseline, current, "Test Printer");

        Assert.Equal(VerificationOutcome.NotResolved, result.Outcome);
        Assert.Single(result.PersistingSignals);
        Assert.Empty(result.ResolvedSignals);
    }

    [Fact]
    public void MissingCurrentEvidenceIsUnknownNotResolved()
    {
        var baseline = SnapshotFixture.WithPrinterProblem();
        var current = SnapshotFixture.WithAllSourcesFailed("snap-bbbbbbbbbbbb");

        var result = SnapshotComparer.Compare(baseline, current, "Test Printer");

        Assert.Equal(VerificationOutcome.Unknown, result.Outcome);
    }

    [Fact]
    public void UserResolvedWithPersistingProblemIsContradicted()
    {
        var baseline = SnapshotFixture.WithPrinterProblem();
        var current = SnapshotFixture.WithPrinterProblem("snap-bbbbbbbbbbbb");

        var result = SnapshotComparer.Compare(baseline, current, "Test Printer", UserConfirmation.Resolved);

        Assert.Equal(VerificationOutcome.Contradicted, result.Outcome);
        Assert.Contains(result.Details, detail => detail.Contains("user_confirmation=resolved", StringComparison.Ordinal));
    }

    [Fact]
    public void UserNotResolvedOverridesAutomatedSuccess()
    {
        var baseline = SnapshotFixture.WithPrinterProblem();
        var current = SnapshotFixture.CreateHealthy("snap-bbbbbbbbbbbb");

        var result = SnapshotComparer.Compare(baseline, current, "Test Printer", UserConfirmation.NotResolved);

        Assert.Equal(VerificationOutcome.NotResolved, result.Outcome);
    }

    [Fact]
    public void PrinterScopeIgnoresFindingsForOtherPrinters()
    {
        var baseline = SnapshotFixture.WithPrinterProblem();
        var current = SnapshotFixture.WithPrinterProblem("snap-bbbbbbbbbbbb") with
        {
            Findings =
            [
                new Finding
                {
                    Source = SourceNames.Printers,
                    Code = "printer_offline",
                    Domain = HealthDomain.Printers,
                    Severity = FindingSeverity.Error,
                    Subject = "Another Printer",
                    Summary = "Another printer is offline.",
                },
            ],
        };

        var result = SnapshotComparer.Compare(baseline, current, "Test Printer");

        Assert.Equal(VerificationOutcome.Resolved, result.Outcome);
    }

    [Fact]
    public async Task VerificationLoopAsksForConfirmationAndStopsWhenResolved()
    {
        var baseline = SnapshotFixture.WithPrinterProblem();
        var prompt = new FakePrompt(UserConfirmation.Resolved, reDiagnose: false);
        var collected = 0;

        var result = await new VerificationLoop().RunAsync(
            baseline,
            _ =>
            {
                collected++;
                return Task.FromResult(SnapshotFixture.CreateHealthy("snap-bbbbbbbbbbbb"));
            },
            prompt,
            new VerificationOptions { PrinterName = "Test Printer" });

        Assert.Equal(VerificationOutcome.Resolved, result.Outcome);
        Assert.Equal(1, collected);
        Assert.Equal(UserConfirmation.Resolved, result.UserConfirmation);
    }

    [Fact]
    public async Task VerificationLoopReDiagnosesWhenUserSaysNotResolved()
    {
        var baseline = SnapshotFixture.WithPrinterProblem();
        var prompt = new FakePrompt(UserConfirmation.NotResolved, reDiagnose: true)
        {
            NextConfirmation = UserConfirmation.Resolved,
        };
        var collected = 0;

        var result = await new VerificationLoop().RunAsync(
            baseline,
            _ =>
            {
                collected++;
                return Task.FromResult(SnapshotFixture.WithPrinterProblem("snap-bbbbbbbbbbbb"));
            },
            prompt,
            new VerificationOptions { PrinterName = "Test Printer", MaxAttempts = 2 });

        Assert.Equal(2, collected);
        Assert.Equal(VerificationOutcome.Contradicted, result.Outcome);
    }

    [Fact]
    public void DetectorUsesFindingsNotRawPrinterState()
    {
        var snapshot = SnapshotFixture.CreateHealthy() with
        {
            Printers =
            [
                SnapshotFixture.CreateHealthy().Printers[0] with
                {
                    WorkOffline = true,
                    ErrorState = "paper_jam",
                },
            ],
            Findings = [],
        };

        var signals = SnapshotProblemDetector.Detect(snapshot, "Test Printer");

        Assert.Empty(signals);
    }

    private sealed class FakePrompt : IVerificationPrompt
    {
        private readonly UserConfirmation _first;
        private readonly bool _reDiagnose;
        private int _outcomeCalls;

        public FakePrompt(UserConfirmation first, bool reDiagnose)
        {
            _first = first;
            _reDiagnose = reDiagnose;
        }

        public UserConfirmation NextConfirmation { get; init; } = UserConfirmation.NotAsked;

        public Task<UserConfirmation> AskOutcomeAsync(VerificationResult result, CancellationToken cancellationToken)
        {
            _outcomeCalls++;
            return Task.FromResult(_outcomeCalls == 1 ? _first : NextConfirmation);
        }

        public Task<bool> AskReDiagnoseAsync(VerificationResult result, CancellationToken cancellationToken) =>
            Task.FromResult(_reDiagnose);
    }
}
