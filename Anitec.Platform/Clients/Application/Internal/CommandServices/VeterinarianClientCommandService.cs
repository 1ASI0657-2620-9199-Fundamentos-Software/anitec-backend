using Anitec.Platform.Clients.Application.CommandServices;
using Anitec.Platform.Clients.Domain.Model;
using Anitec.Platform.Clients.Domain.Model.Commands;
using Anitec.Platform.Clients.Domain.Model.Entities;
using Anitec.Platform.Clients.Domain.Repositories;
using Anitec.Platform.Shared.Application.Model;
using Anitec.Platform.Shared.Domain.Repositories;

namespace Anitec.Platform.Clients.Application.Internal.CommandServices;

public class VeterinarianClientCommandService(
    IVeterinarianClientRepository repository,
    IUnitOfWork unitOfWork) : IVeterinarianClientCommandService
{
    public async Task<Result<VeterinarianClient>> Handle(CreateVeterinarianClientCommand command, CancellationToken cancellationToken)
    {
        var existing = await repository.FindByVeterinarianIdAndRancherIdAsync(
            command.VeterinarianId,
            command.RancherId,
            cancellationToken);

        if (existing is not null)
        {
            // A veterinarian may ask again after the rancher rejected or revoked the access.
            if (!existing.IsRejected)
                return Result<VeterinarianClient>.Failure(
                    ClientsError.VeterinarianClientAlreadyExists,
                    "Veterinarian client relationship already exists.");

            existing.RequestAgain();
            repository.Update(existing);
            await unitOfWork.CompleteAsync(cancellationToken);
            return Result<VeterinarianClient>.Success(existing);
        }

        var entity = new VeterinarianClient(command);
        await repository.AddAsync(entity, cancellationToken);
        await unitOfWork.CompleteAsync(cancellationToken);
        return Result<VeterinarianClient>.Success(entity);
    }

    public async Task<Result<VeterinarianClient>> Handle(ApproveVeterinarianClientCommand command, CancellationToken cancellationToken)
    {
        var entity = await repository.FindByVeterinarianIdAndRancherIdAsync(
            command.VeterinarianId,
            command.RancherId,
            cancellationToken);

        if (entity is null)
            return Result<VeterinarianClient>.Failure(
                ClientsError.VeterinarianClientNotFound,
                "Veterinarian client relationship not found.");

        if (entity.IsAccepted) return Result<VeterinarianClient>.Success(entity);

        if (!entity.IsPending)
            return Result<VeterinarianClient>.Failure(
                ClientsError.VeterinarianClientNotPending,
                "Only a pending request can be approved.");

        entity.Approve();
        repository.Update(entity);
        await unitOfWork.CompleteAsync(cancellationToken);
        return Result<VeterinarianClient>.Success(entity);
    }

    public async Task<Result<VeterinarianClient>> Handle(RejectVeterinarianClientCommand command, CancellationToken cancellationToken)
    {
        var entity = await repository.FindByVeterinarianIdAndRancherIdAsync(
            command.VeterinarianId,
            command.RancherId,
            cancellationToken);

        if (entity is null)
            return Result<VeterinarianClient>.Failure(
                ClientsError.VeterinarianClientNotFound,
                "Veterinarian client relationship not found.");

        if (entity.IsRejected) return Result<VeterinarianClient>.Success(entity);

        entity.Reject();
        repository.Update(entity);
        await unitOfWork.CompleteAsync(cancellationToken);
        return Result<VeterinarianClient>.Success(entity);
    }

    public async Task<Result> Handle(DeleteVeterinarianClientCommand command, CancellationToken cancellationToken)
    {
        var entity = await repository.FindByVeterinarianIdAndRancherIdAsync(
            command.VeterinarianId,
            command.RancherId,
            cancellationToken);

        if (entity is null)
            return Result.Failure(ClientsError.VeterinarianClientNotFound, "Veterinarian client relationship not found.");

        repository.Remove(entity);
        await unitOfWork.CompleteAsync(cancellationToken);
        return Result.Success();
    }
}
