using Anitec.Subscriptions.Shared.Application.Model;
using Anitec.Subscriptions.Shared.Domain.Repositories;
using Anitec.Subscriptions.Application.CommandServices;
using Anitec.Subscriptions.Domain.Model.Commands;
using Anitec.Subscriptions.Domain.Model.Entities;
using Anitec.Subscriptions.Domain.Repositories;

namespace Anitec.Subscriptions.Application.Internal.CommandServices;

public class PaymentCommandService(IPaymentRepository repository, IUnitOfWork unitOfWork) : IPaymentCommandService
{
    public async Task<Result<Payment>> Handle(CreatePaymentCommand command, CancellationToken cancellationToken)
    {
        var payment = new Payment(command);
        await repository.AddAsync(payment, cancellationToken);
        await unitOfWork.CompleteAsync(cancellationToken);
        return Result<Payment>.Success(payment);
    }
}
