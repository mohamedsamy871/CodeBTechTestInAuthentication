using Core.DTO.General;

namespace AuthenticationApi.Helpers
{
    public static class ResponseHelper<T> where T : class
    {
        public static ResponseModel<T> CreateResponseModel(T result, bool isError, string messageEn, int code, string messageAr = "", string exMessage = "")
        {
            if (isError)
            {
                ResponseModel<T> res = new ResponseModel<T>()
                {
                    Timestamp = DateTime.Now,
                    IsError = true,
                    Data = null,
                    StatusCode = code,
                    MessageEn = messageEn,
                    MessageAr = messageAr,
                    ExMessage = exMessage
                };
                return res;
            }
            else
            {
                ResponseModel<T> res = new ResponseModel<T>()
                {
                    Timestamp = DateTime.Now,
                    IsError = false,
                    Data = result,
                    MessageEn = messageEn,
                    MessageAr = messageAr,
                    StatusCode = code,
                };
                return res;
            }
        }

    }
}
