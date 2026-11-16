using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;

namespace ArtuMod;

// Fixtures build a RunState without starting a live run. The v0.113 selector
// stack is run-scoped, so bind it to the fixture and restore the previous one.
sealed class VerificationSelectorScope : IDisposable
{
    static readonly System.Reflection.PropertyInfo StackProperty =
        AccessTools.Property(typeof(RunManager), nameof(RunManager.CardSelectorStack));
    readonly RunManager manager = RunManager.Instance;
    readonly object? previous;

    public VerificationSelectorScope(RunState run)
    {
        if (!TestMode.IsOn) throw new InvalidOperationException("Only verification can replace the selector stack.");
        previous = StackProperty.GetValue(manager);
        StackProperty.SetValue(manager, new CardSelectorStack(run));
    }

    public void Dispose() => StackProperty.SetValue(manager, previous);
}
