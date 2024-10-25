using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.DTO.General
{
    public class ResponseModel<T> where T : class
    {
        public DateTime Timestamp { get; set; } =DateTime.Now;
        public int StatusCode { get; set; }
        public bool IsError { get; set; }
        public string? MessageEn { get; set; }
        public string? MessageAr { get; set; }
        public string ExMessage { get; set; }
        public T? Data { get; set; }
    }
}
