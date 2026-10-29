internal enum ExternalOperationState { Prepared, Executing, OutcomeUnknown, Succeeded, Failed, Reconciled }
internal enum RetryDecision { Deny, ManualReconciliationRequired, SafeToRetry }
internal sealed record OperationRecord(string OperationId,ExternalOperationState State,bool IsIdempotent,bool EvidenceEffectAbsent);
internal static class RecoverySpike
{
    internal static RetryDecision DecideRetry(OperationRecord operation)
    {
        if (operation.State == ExternalOperationState.OutcomeUnknown)
            return RetryDecision.ManualReconciliationRequired;
        if (operation.State != ExternalOperationState.Reconciled) return RetryDecision.Deny;
        if (!operation.IsIdempotent || !operation.EvidenceEffectAbsent)
            return RetryDecision.ManualReconciliationRequired;
        return RetryDecision.SafeToRetry;
    }
    internal static bool Run()
    {
        var unknown = new OperationRecord("op1",ExternalOperationState.OutcomeUnknown,true,false);
        var reconciled = unknown with {State=ExternalOperationState.Reconciled,EvidenceEffectAbsent=true};
        return DecideRetry(unknown)==RetryDecision.ManualReconciliationRequired
            && DecideRetry(reconciled)==RetryDecision.SafeToRetry
            && DecideRetry(reconciled with {IsIdempotent=false})==RetryDecision.ManualReconciliationRequired
            && DecideRetry(reconciled with {EvidenceEffectAbsent=false})==RetryDecision.ManualReconciliationRequired
            && DecideRetry(reconciled with {State=ExternalOperationState.Executing})==RetryDecision.Deny;
    }
}