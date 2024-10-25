using Twilio.Types;
using Twilio;
using Twilio.Rest.Api.V2010.Account;

namespace AuthenticationApi.Services
{
    public interface ISms
    {
        Task SendSmsAsync(string toPhoneNumber, string messageBody);
    }
    public class SmsService: ISms
    {

        private readonly string _accountSid = "your_account_sid";
        private readonly string _authToken = "your_auth_token";
        private readonly string _fromPhoneNumber = "your_twilio_phone_number";

        public SmsService()
        {
            TwilioClient.Init(_accountSid, _authToken);
        }

        public async Task SendSmsAsync(string toPhoneNumber, string messageBody)
        {
            try
            {
                var message = await MessageResource.CreateAsync(
                    to: new PhoneNumber(toPhoneNumber),
                    from: new PhoneNumber(_fromPhoneNumber),
                    body: messageBody
                );
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(ex.Message);
            }
        }
    }
}
