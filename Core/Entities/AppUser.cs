using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Entities
{
    public class AppUser:IdentityUser
    {
        public string? OTPCode { get; set; }
        public string ICNumber { get; set; }
        public string? EmailOTP { get; set; }
        public string? PhoneOTP { get; set; }

        public DateTime? EmailOTPExpiration { get; set; }
        public DateTime? PhoneOTPExpiration { get; set; }
        public string? SixDigitPin { get; set; }
    }
}
