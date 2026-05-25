using System.Collections.Generic;

namespace TenantPortal.Application.Interfaces
{
    public interface IGeoScopeContext
    {
        bool IsGlobal { get; }
        HashSet<string> Countries { get; } // ISO codes (AE/IN/…)
    }
}