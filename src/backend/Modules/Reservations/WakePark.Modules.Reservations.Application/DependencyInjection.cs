using Microsoft.Extensions.DependencyInjection;

using WakePark.BuildingBlocks.Application.Messaging;
using WakePark.Modules.Reservations.Application.Availability;
using WakePark.Modules.Reservations.Application.Reservations.CancelReservation;
using WakePark.Modules.Reservations.Application.Reservations.CreateReservation;
using WakePark.Modules.Reservations.Contracts;

namespace WakePark.Modules.Reservations.Application;

/// <summary>Registrace aplikační vrstvy modulu Reservations.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddReservationsApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<
            ICommandHandler<CreateReservationCommand, ReservationDto>,
            CreateReservationCommandHandler>();

        services.AddScoped<
            ICommandHandler<CancelReservationCommand, ReservationDto>,
            CancelReservationCommandHandler>();

        services.AddScoped<
            IQueryHandler<GetAvailabilityQuery, IReadOnlyList<SlotAvailabilityDto>>,
            GetAvailabilityQueryHandler>();

        return services;
    }
}
