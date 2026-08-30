namespace WakePark.BuildingBlocks.Application.Abstractions;

/// <summary>Tenant aktuálního požadavku. Určuje, jaká data jsou vůbec viditelná.</summary>
public interface ITenantContext
{
    Guid? EnterpriseId { get; }
}
