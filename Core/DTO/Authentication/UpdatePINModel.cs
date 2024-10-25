using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.DTO.Authentication
{
    public class UpdatePINModel
    {
        public string OldPIN { get; set; }
        public string NewPIN { get; set; }
        public string UserId { get; set; }
    }
}
