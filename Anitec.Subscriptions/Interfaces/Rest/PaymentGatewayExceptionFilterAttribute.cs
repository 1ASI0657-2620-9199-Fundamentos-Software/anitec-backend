using Anitec.Subscriptions.Application.Internal.OutboundServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Anitec.Subscriptions.Interfaces.Rest;

/// <summary>
///     Turns the failures of the payment provider into clear answers instead of a generic 500: 503 when the
///     environment has no Stripe keys and 502 when Stripe fails. The messages never include keys or provider details.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class PaymentGatewayExceptionFilterAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        switch (context.Exception)
        {
            case PaymentGatewayNotConfiguredException:
                context.Result = Problem(StatusCodes.Status503ServiceUnavailable, "Payments unavailable",
                    "The payment provider is not configured in this environment.");
                context.ExceptionHandled = true;
                break;
            case PaymentGatewayException:
                context.Result = Problem(StatusCodes.Status502BadGateway, "Payment provider error",
                    "The payment provider did not answer correctly. Try again later.");
                context.ExceptionHandled = true;
                break;
        }
    }

    private static ObjectResult Problem(int status, string title, string detail)
    {
        return new ObjectResult(new ProblemDetails { Status = status, Title = title, Detail = detail })
        {
            StatusCode = status
        };
    }
}
