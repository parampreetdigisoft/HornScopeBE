using HornScope.Common.Models;
using HornScope.Dtos.PaymentDto;

namespace HornScope.IServices
{
    public interface IPaymentService
    {
        Task<ResultResponseDto<CheckoutSessionResponse>> CreateCheckoutSession(CreateCheckoutSessionDto request);
        Task<ResultResponseDto<VerifySessionResponse>> VerifySession(VerifySessionDto request);
        Task<ResultResponseDto<string>> StripeWebhook();
    }
}
