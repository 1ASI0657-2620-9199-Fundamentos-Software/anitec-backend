using Anitec.Subscriptions.Shared.Application.Model;
using Anitec.Subscriptions.Domain.Model.Commands;
using Anitec.Subscriptions.Domain.Model.Entities;

namespace Anitec.Subscriptions.Application.CommandServices;

public interface IPaymentCommandService
{
    Task<Result<Payment>> Handle(CreatePaymentCommand command, CancellationToken cancellationToken);
}
