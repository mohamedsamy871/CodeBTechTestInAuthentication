using AuthenticationApi.Helpers;
using Core.DTO.Authentication;
using Core.DTO.General;
using Core.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace AuthenticationApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthenticationController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AuthenticationController(UserManager<AppUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }
        public async Task<ResponseModel<BooleanResultDTO>> Register([FromBody] UserRegistrationModel model)
        {
            try
            {
                if (model == null)
                    return ResponseHelper<BooleanResultDTO>.CreateResponseModel(null, true, "Invalid request data", 400, "بيانات الطلب غير صالحة");
                bool isPhoneAlreadyRegistered = _userManager.Users.Any(item => item.PhoneNumber == model.PhoneNumber);

                if (isPhoneAlreadyRegistered)
                    return ResponseHelper<BooleanResultDTO>.CreateResponseModel(null, true, "Phone Number is already Registered in our system!", 400, "رقم الجوال مسجل من قبل");

                var userEmailExist = await _userManager.FindByEmailAsync(model.Email);
                if (userEmailExist != null)
                    return ResponseHelper<BooleanResultDTO>.CreateResponseModel(null, true, "Email is already Registered in our system!", 400, "البريد الالكتروني مسجل من قبل");

                var userICNumberExist = await _userManager.Users.Where(m=>m.ICNumber==model.ICNumber).FirstOrDefaultAsync();
                if (userICNumberExist != null)
                    return ResponseHelper<BooleanResultDTO>.CreateResponseModel(null, true, "There is account registered with the IC number.", 400, "الرقم التعريفي مسجل من قبل");

                // Validate email format
                if (!Regex.IsMatch(model.Email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                    return ResponseHelper<BooleanResultDTO>.CreateResponseModel(null, true, "Invalid email format. Please provide a valid email address.", 400, "صيغة البريد الإلكتروني غير صالحة. يرجى تقديم عنوان بريد إلكتروني صحيح");
                // Validate Malaysian mobile number format
                if (!Regex.IsMatch(model.PhoneNumber, @"^(?:\+60|0)\d{9,10}$"))
                    return ResponseHelper<BooleanResultDTO>.CreateResponseModel(null, true, "Invalid phone number format. Please provide a valid Malaysian phone number.", 400, "صيغة رقم الهاتف غير صالحة. يرجى تقديم رقم هاتف ماليزي صحيح.");
                AppUser user = new()
                {
                    Email = model.Email,
                    SecurityStamp = Guid.NewGuid().ToString(),
                    UserName = model.Username,
                    PhoneNumber = model.PhoneNumber,
                    ICNumber = model.ICNumber,
                    EmailConfirmed =false
                };
                var result = await _userManager.CreateAsync(user);
                if (!result.Succeeded)
                {
                    var errors = result.Errors.Select(error => error.Description);
                    var errorMessage = string.Join("; ", errors);
                    return ResponseHelper<BooleanResultDTO>.CreateResponseModel(null, true, "An error occurred while processing your request", 400, "فشلت عملية التسجيل . برجاء التحقق من البيانات المدخلة وحاول مرة ثانية.", errorMessage);
                }
                await EnsureRolesExistAsync();
                await _userManager.AddToRoleAsync(user, UserRoles.User);
                return ResponseHelper<BooleanResultDTO>.CreateResponseModel(new BooleanResultDTO { Success =true}, false, "User created successfully!", 200, "تم إنشاء المستخدم بنجاح.");
            }
            catch (DbUpdateException)
            {
                return ResponseHelper<BooleanResultDTO>.CreateResponseModel(null, true, "Database error occurred while processing your request", 400, "حدث خطأ في قاعدة البيانات أثناء معالجة طلبك.");
            }
            catch (Exception ex)
            {
                return ResponseHelper<BooleanResultDTO>.CreateResponseModel(null, true, "We apologize for this technical error, please try again", 400, "نأسف لهذا الخطأ التقنى برجاء المحاولة مرة أخرى", ex.Message);
            }
        }
        #region Methods
        private  async Task EnsureRolesExistAsync()
        {
            if (!await _roleManager.RoleExistsAsync(UserRoles.User))
            {
                await _roleManager.CreateAsync(new IdentityRole(UserRoles.User));
            }
        }
        #endregion
    }
}
