using System.Security.Cryptography;
using System.Text;

internal sealed record ToolIntent(string Actor, string Tool, string Resource, string ArgsDigest, string Risk);
internal sealed record ScopedGrant(string Actor, string Tool, string Resource, string ArgsDigest, string Risk, DateTimeOffset ExpiresAt, bool Approved);
internal static class PolicySpike
{
    internal static bool Authorize(ScopedGrant? grant, ToolIntent request, DateTimeOffset now)
    {
        if(grant is null || !grant.Approved || now >= grant.ExpiresAt) return false;
        if(!Equal(grant.Actor,request.Actor) || !Equal(grant.Tool,request.Tool)
            || !Equal(grant.Resource,request.Resource) || !Equal(grant.ArgsDigest,request.ArgsDigest)
            || !Equal(grant.Risk,request.Risk)) return false;
        return true;
    }
    private static bool Equal(string a,string b)
    {
        if (a is null || b is null) return false;
        var x=Encoding.UTF8.GetBytes(a);
        var y=Encoding.UTF8.GetBytes(b);
        return x.Length==y.Length && CryptographicOperations.FixedTimeEquals(x,y);
    }
    internal static bool Run()
    {
        var now=DateTimeOffset.UtcNow;
        var intent=new ToolIntent("agent-1","file.write","workspace/src/main.cs","sha256:123","High");
        var grant=new ScopedGrant(intent.Actor,intent.Tool,intent.Resource,intent.ArgsDigest,intent.Risk,now.AddMinutes(2),true);
        return Authorize(grant,intent,now)
            && !Authorize(grant,intent with {ArgsDigest="sha256:malicious"},now)
            && !Authorize(grant,intent with {Resource="workspace/secrets.txt"},now)
            && !Authorize(grant,intent with {Actor="agent-2"},now)
            && !Authorize(grant,intent with {Risk="Critical"},now)
            && !Authorize(grant with {ExpiresAt=now.AddSeconds(-1)},intent,now)
            && !Authorize(grant with {Approved=false},intent,now)
            && !Authorize(null,intent,now);
    }
}