internal sealed record FileRevision(string WorkspaceId, string GitRevision, string FileHash, long BufferRevision);
internal enum PatchApplyDecision { Allow, RejectWorkspace, RejectGitRevision, RejectFileHash, RejectBufferRevision }
internal static class RevisionSpike
{
    internal static PatchApplyDecision Check(FileRevision expected, FileRevision actual)
    {
        if (expected.WorkspaceId != actual.WorkspaceId) return PatchApplyDecision.RejectWorkspace;
        if (expected.GitRevision != actual.GitRevision) return PatchApplyDecision.RejectGitRevision;
        if (expected.FileHash != actual.FileHash) return PatchApplyDecision.RejectFileHash;
        if (expected.BufferRevision != actual.BufferRevision) return PatchApplyDecision.RejectBufferRevision;
        return PatchApplyDecision.Allow;
    }
    internal static bool Run()
    {
        var expected = new FileRevision("w1","abc","hash1",7);
        return Check(expected,expected)==PatchApplyDecision.Allow
            && Check(expected,expected with {WorkspaceId="w2"})==PatchApplyDecision.RejectWorkspace
            && Check(expected,expected with {GitRevision="def"})==PatchApplyDecision.RejectGitRevision
            && Check(expected,expected with {FileHash="hash2"})==PatchApplyDecision.RejectFileHash
            && Check(expected,expected with {BufferRevision=8})==PatchApplyDecision.RejectBufferRevision;
    }
}