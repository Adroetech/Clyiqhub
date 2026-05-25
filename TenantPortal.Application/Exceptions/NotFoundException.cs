using System;

namespace TenantPortal.Application.Exceptions
{
    public class NotFoundException : Exception
    {
        public NotFoundException(string entityName, object key)
            : base($"{entityName} not found: {key}") { }
    }
}