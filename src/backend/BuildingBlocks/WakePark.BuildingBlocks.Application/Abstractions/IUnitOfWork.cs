namespace WakePark.BuildingBlocks.Application.Abstractions;

/// <summary>Potvrzení změn jednoho modulu jako celku.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
