using AuthenticationApi.Helpers;
using AuthenticationApi.Services;
using Core.DTO.Authentication;
using Core.DTO.General;
using Core.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Metadata.Ecma335;
using System.Text.RegularExpressions;
using Twilio.Rest.Chat.V2.Service.User;
using static System.Net.WebRequestMethods;

namespace AuthenticationApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthenticationController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IMailer _mailer;
        private readonly ISms _sms;

        public AuthenticationController(UserManager<AppUser> userManager,
            RoleManager<IdentityRole> roleManager, IMailer mailer, ISms sms)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _mailer = mailer;
            _sms = sms;
        }
        [HttpPost("UserRegistration")]
        public async Task<ResponseModel<string>> Register([FromBody] UserRegistrationModel model)
        {
            if (model == null)
                return ResponseHelper<string>.CreateResponseModel(null, true, "Invalid request data", 400, "بيانات الطلب غير صالحة");

            try
            {
                // Validate unique fields
                if (await IsPhoneAlreadyRegistered(model.PhoneNumber))
                    return ResponseHelper<string>.CreateResponseModel(null, true, "Phone Number is already Registered in our system!", 400, "رقم الجوال مسجل من قبل");

                if (await IsEmailAlreadyRegistered(model.Email))
                    return ResponseHelper<string>.CreateResponseModel(null, true, "Email is already Registered in our system!", 400, "البريد الالكتروني مسجل من قبل");

                if (await IsICNumberAlreadyRegistered(model.ICNumber))
                    return ResponseHelper<string>.CreateResponseModel(null, true, "There is an account registered with the IC number.", 400, "الرقم التعريفي مسجل من قبل");

                // Validate formats
                if (!IsValidEmailFormat(model.Email))
                    return ResponseHelper<string>.CreateResponseModel(null, true, "Invalid email format. Please provide a valid email address.", 400, "صيغة البريد الإلكتروني غير صالحة. يرجى تقديم عنوان بريد إلكتروني صحيح");

                if (!IsValidMalaysianPhoneNumber(model.PhoneNumber))
                    return ResponseHelper<string>.CreateResponseModel(null, true, "Invalid phone number format. Please provide a valid Malaysian phone number.", 400, "صيغة رقم الهاتف غير صالحة. يرجى تقديم رقم هاتف ماليزي صحيح.");

                // Generate OTPs
                var emailOtp = GenerateRandomNo();
                var phoneOtp = GenerateRandomNo();

                // Create new user
                var user = new AppUser
                {
                    Email = model.Email,
                    SecurityStamp = Guid.NewGuid().ToString(),
                    UserName = model.Username,
                    PhoneNumber = model.PhoneNumber,
                    PhoneNumberConfirmed = false,
                    ICNumber = model.ICNumber,
                    EmailConfirmed = false,
                    EmailOTP = emailOtp,
                    PhoneOTP = phoneOtp,
                    PhoneOTPExpiration = DateTime.UtcNow.AddHours(1),
                    EmailOTPExpiration = DateTime.UtcNow.AddHours(1)
                };

                var result = await _userManager.CreateAsync(user);
                if (!result.Succeeded)
                {
                    var errorMessage = string.Join("; ", result.Errors.Select(error => error.Description));
                    return ResponseHelper<string>.CreateResponseModel(null, true, "An error occurred while processing your request", 400, "فشلت عملية التسجيل. برجاء التحقق من البيانات المدخلة وحاول مرة ثانية.", errorMessage);
                }

                // Assign default user role
                await EnsureRolesExistAsync();
                await _userManager.AddToRoleAsync(user, UserRoles.User);

                // Send OTP via email and SMS
                await _mailer.SendEmailAsync(user.Email, "Email Validation - Koperasi Tentera", $"Here is your OTP: {emailOtp}");
                await _sms.SendSmsAsync(user.PhoneNumber, $"Koperasi Tentera App - Here is your OTP: {phoneOtp}");

                return ResponseHelper<string>.CreateResponseModel(user.Id, false, "User created successfully!", 200, "تم إنشاء المستخدم بنجاح.");
            }
            catch (DbUpdateException)
            {
                return ResponseHelper<string>.CreateResponseModel(null, true, "Database error occurred while processing your request", 400, "حدث خطأ في قاعدة البيانات أثناء معالجة طلبك.");
            }
            catch (Exception ex)
            {
                return ResponseHelper<string>.CreateResponseModel(null, true, "We apologize for this technical error, please try again", 400, "نأسف لهذا الخطأ التقني، برجاء المحاولة مرة أخرى", ex.Message);
            }
        }
        [HttpPost("VerifyEmail")]
        public async Task<ResponseModel<BooleanResultDTO>> VerifyEmail(string otp, string userId)
        {
            if (string.IsNullOrEmpty(otp) || string.IsNullOrEmpty(userId))
                return ResponseHelper<BooleanResultDTO>.CreateResponseModel(null, true, "Invalid request data", 400, "بيانات الطلب غير صالحة");

            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                    return ResponseHelper<BooleanResultDTO>.CreateResponseModel(null, true, "User not found", 404, "المستخدم غير موجود");

                // Validate OTP and Expiration
                var validationResponse = ValidateOtp(user, otp);
                if (validationResponse != null)
                    return validationResponse;

                // Confirm email
                var emailConfirmationResponse = await ConfirmEmailAsync(user);
                if (emailConfirmationResponse != null)
                    return emailConfirmationResponse;

                return ResponseHelper<BooleanResultDTO>.CreateResponseModel(new BooleanResultDTO { Success = true }, false, "Email successfully verified!", 200, "تم التحقق من الايميل.");
            }
            catch (DbUpdateException)
            {
                return ResponseHelper<BooleanResultDTO>.CreateResponseModel(null, true, "Database error occurred while processing your request", 400, "حدث خطأ في قاعدة البيانات أثناء معالجة طلبك.");
            }
            catch (Exception ex)
            {
                return ResponseHelper<BooleanResultDTO>.CreateResponseModel(null, true, "We apologize for this technical error, please try again", 500, "نأسف لهذا الخطأ التقني، برجاء المحاولة مرة أخرى", ex.Message);
            }
        }

        [HttpPost("VerifyPhone")]
        public async Task<ResponseModel<BooleanResultDTO>> VerifyPhone(string otp, string userId)
        {
            if (string.IsNullOrEmpty(otp) || string.IsNullOrEmpty(userId))
                return ResponseHelper<BooleanResultDTO>.CreateResponseModel(null, true, "Invalid request data", 400, "بيانات الطلب غير صالحة");

            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                    return ResponseHelper<BooleanResultDTO>.CreateResponseModel(null, true, "User not found", 404, "المستخدم غير موجود");

                // Validate OTP and Expiration for phone verification
                var validationResponse = ValidatePhoneOtp(user, otp);
                if (validationResponse != null)
                    return validationResponse;

                // Confirm phone number
                user.PhoneNumberConfirmed = true;
                var updateResult = await _userManager.UpdateAsync(user);

                if (!updateResult.Succeeded)
                {
                    var errorMessage = string.Join("; ", updateResult.Errors.Select(error => error.Description));
                    return ResponseHelper<BooleanResultDTO>.CreateResponseModel(null, true, "An error occurred while processing your request", 400, "حدث خطأ أثناء معالجة الطلب. برجاء التحقق من البيانات المدخلة وحاول مرة ثانية.", errorMessage);
                }

                return ResponseHelper<BooleanResultDTO>.CreateResponseModel(new BooleanResultDTO { Success = true }, false, "Phone number successfully verified!", 200, "تم التحقق من رقم الهاتف بنجاح.");
            }
            catch (ArgumentNullException ex)
            {
                return ResponseHelper<BooleanResultDTO>.CreateResponseModel(null, true, "Invalid argument detected", 400, "تم الكشف عن قيمة غير صالحة", ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return ResponseHelper<BooleanResultDTO>.CreateResponseModel(null, true, "Invalid operation attempted", 400, "تمت محاولة عملية غير صالحة", ex.Message);
            }
            catch (DbUpdateException ex)
            {
                return ResponseHelper<BooleanResultDTO>.CreateResponseModel(null, true, "Database error occurred while processing your request", 500, "حدث خطأ في قاعدة البيانات أثناء معالجة طلبك.", ex.Message);
            }
            catch (Exception ex)
            {
                return ResponseHelper<BooleanResultDTO>.CreateResponseModel(null, true, "An unexpected error occurred. Please try again later.", 500, "حدث خطأ غير متوقع. يرجى المحاولة مرة أخرى لاحقاً", ex.Message);
            }
        }
        [HttpPost("CreatePIN")]
        public async Task<ResponseModel<BooleanResultDTO>> CreatePIN(string PIN, string userId)
        {
            if (string.IsNullOrEmpty(PIN) || string.IsNullOrEmpty(userId))
                return ResponseHelper<BooleanResultDTO>.CreateResponseModel(null, true, "Invalid request data", 400, "بيانات الطلب غير صالحة");

            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                    return ResponseHelper<BooleanResultDTO>.CreateResponseModel(null, true, "User not found", 404, "المستخدم غير موجود");

                var result = await _userManager.AddPasswordAsync(user, PIN);
                if (!result.Succeeded)
                {
                    var errorMessage = string.Join("; ", result.Errors.Select(error => error.Description));
                    return ResponseHelper<BooleanResultDTO>.CreateResponseModel(null, true, "An error occurred while processing your request", 400, "حدث خطأ أثناء معالجة الطلب. برجاء التحقق من البيانات المدخلة وحاول مرة ثانية.", errorMessage);
                }
                return ResponseHelper<BooleanResultDTO>.CreateResponseModel(new BooleanResultDTO { Success = true }, false, "PIN was successfully created!", 200, "تم إنشاء الرمز بنجاح");
            }
            catch (ArgumentNullException ex)
            {
                return ResponseHelper<BooleanResultDTO>.CreateResponseModel(null, true, "Invalid argument detected", 400, "تم الكشف عن قيمة غير صالحة", ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return ResponseHelper<BooleanResultDTO>.CreateResponseModel(null, true, "Invalid operation attempted", 400, "تمت محاولة عملية غير صالحة", ex.Message);
            }
            catch (DbUpdateException ex)
            {
                return ResponseHelper<BooleanResultDTO>.CreateResponseModel(null, true, "Database error occurred while processing your request", 500, "حدث خطأ في قاعدة البيانات أثناء معالجة طلبك.", ex.Message);
            }
            catch (Exception ex)
            {
                return ResponseHelper<BooleanResultDTO>.CreateResponseModel(null, true, "An unexpected error occurred. Please try again later.", 500, "حدث خطأ غير متوقع. يرجى المحاولة مرة أخرى لاحقاً", ex.Message);
            }
        }


        [HttpPost("UserLogin")]
        public async Task<ResponseModel<string>> Login([FromBody] UserLoginModel model)
        {
            if (model == null)
                return ResponseHelper<string>.CreateResponseModel(null, true, "Invalid request data", 400, "بيانات الطلب غير صالحة");

            try
            {
                var user = await _userManager.Users.Where(m => m.ICNumber == model.ICNumber).FirstOrDefaultAsync();
                if(user == null)
                    return ResponseHelper<string>.CreateResponseModel(null, true, "Invalid Ic Number", 400, "خطأ ببيانات الطلب");

                var emailOtp = GenerateRandomNo();
                var phoneOtp = GenerateRandomNo();
                user.EmailOTP = emailOtp;
                user.PhoneOTP = phoneOtp;
                user.PhoneOTPExpiration = DateTime.UtcNow.AddHours(1);
                user.EmailOTPExpiration = DateTime.UtcNow.AddHours(1);
                var updateResult = await _userManager.UpdateAsync(user);

                if (!updateResult.Succeeded)
                {
                    var errorMessage = string.Join("; ", updateResult.Errors.Select(error => error.Description));
                    return ResponseHelper<string>.CreateResponseModel(null, true, "An error occurred while processing your request", 400, "حدث خطأ أثناء معالجة الطلب. برجاء التحقق من البيانات المدخلة وحاول مرة ثانية.", errorMessage);
                }
                // Send OTP via email and SMS
                await _mailer.SendEmailAsync(user.Email, "Account Verification - Koperasi Tentera", $"Here is your OTP: {emailOtp}");
                await _sms.SendSmsAsync(user.PhoneNumber, $"Koperasi Tentera App - Here is your OTP: {phoneOtp}");

                return ResponseHelper<string>.CreateResponseModel(user.Id, false, "User is valid!", 200, "بيانات المستخدم موجودة بالنظام . ");
            }
            catch (DbUpdateException)
            {
                return ResponseHelper<string>.CreateResponseModel(null, true, "Database error occurred while processing your request", 400, "حدث خطأ في قاعدة البيانات أثناء معالجة طلبك.");
            }
            catch (Exception ex)
            {
                return ResponseHelper<string>.CreateResponseModel(null, true, "We apologize for this technical error, please try again", 400, "نأسف لهذا الخطأ التقني، برجاء المحاولة مرة أخرى", ex.Message);
            }
        }

        [HttpPost("UpdatePIN")]
        public async Task<ResponseModel<string>> UpdatePIN([FromBody] UpdatePINModel model)
        {
            if (model == null)
                return ResponseHelper<string>.CreateResponseModel(null, true, "Invalid request data", 400, "بيانات الطلب غير صالحة");

            try
            {
                var user = await _userManager.FindByIdAsync(model.UserId);
                if (user == null)
                    return ResponseHelper<string>.CreateResponseModel(null, true, "Invalid Ic Number", 400, "خطأ ببيانات الطلب");

                var _isValidPassword = await _userManager.CheckPasswordAsync(user, model.OldPIN);
                if (!_isValidPassword)
                    return ResponseHelper<string>.CreateResponseModel(null, true, "Invalid Old PIN Number", 400, "الرمز غير صحيح");

                var _changingPasswordProcess = await _userManager.ChangePasswordAsync(user, model.OldPIN, model.NewPIN);
                if (!_changingPasswordProcess.Succeeded)
                {
                    var errorMessage = string.Join("; ", _changingPasswordProcess.Errors.Select(error => error.Description));
                    return ResponseHelper<string>.CreateResponseModel(null, true, "An error occurred while processing your request", 400, "حدث خطأ أثناء معالجة الطلب. برجاء التحقق من البيانات المدخلة وحاول مرة ثانية.", errorMessage);
                }
                return ResponseHelper<string>.CreateResponseModel(user.Id, false, "Process Successfully done!", 200, "تم معالجة العملية بنجاح");
            }
            catch (DbUpdateException)
            {
                return ResponseHelper<string>.CreateResponseModel(null, true, "Database error occurred while processing your request", 400, "حدث خطأ في قاعدة البيانات أثناء معالجة طلبك.");
            }
            catch (Exception ex)
            {
                return ResponseHelper<string>.CreateResponseModel(null, true, "We apologize for this technical error, please try again", 400, "نأسف لهذا الخطأ التقني، برجاء المحاولة مرة أخرى", ex.Message);
            }
        }

        #region Helper Methods
        #region Register Action Helper Methods
        private async Task<bool> IsPhoneAlreadyRegistered(string phoneNumber) =>
            _userManager.Users.Any(user => user.PhoneNumber == phoneNumber);

        private async Task<bool> IsEmailAlreadyRegistered(string email) =>
            await _userManager.FindByEmailAsync(email) != null;

        private async Task<bool> IsICNumberAlreadyRegistered(string icNumber) =>
            await _userManager.Users.AnyAsync(user => user.ICNumber == icNumber);

        private bool IsValidEmailFormat(string email) =>
            Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");

        private bool IsValidMalaysianPhoneNumber(string phoneNumber) =>
            Regex.IsMatch(phoneNumber, @"^(?:\+60|060|0060)\d{8,11}$");

        private string GenerateRandomNo() =>
             new Random().Next(1000,9999).ToString();
        private async Task EnsureRolesExistAsync()
        {
            if (!await _roleManager.RoleExistsAsync(UserRoles.User))
                await _roleManager.CreateAsync(new IdentityRole(UserRoles.User));
        }
        #endregion
        private ResponseModel<BooleanResultDTO> ValidateOtp(AppUser user, string otp)
        {
            if (user.EmailOTP != otp)
                return ResponseHelper<BooleanResultDTO>.CreateResponseModel(null, true, "Invalid OTP", 400, "خطأ بالكود");

            if (user.EmailOTPExpiration < DateTime.UtcNow)
                return ResponseHelper<BooleanResultDTO>.CreateResponseModel(null, true, "OTP Expired", 400, "انتهت صلاحية الكود");

            return null;
        }

        private async Task<ResponseModel<BooleanResultDTO>> ConfirmEmailAsync(AppUser user)
        {
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var result = await _userManager.ConfirmEmailAsync(user, token);

            if (!result.Succeeded)
            {
                var errorMessage = string.Join("; ", result.Errors.Select(error => error.Description));
                return ResponseHelper<BooleanResultDTO>.CreateResponseModel(null, true, "An error occurred while processing your request", 400, "فشلت عملية التسجيل. برجاء التحقق من البيانات المدخلة وحاول مرة ثانية.", errorMessage);
            }

            return null;
        }

        private ResponseModel<BooleanResultDTO> ValidatePhoneOtp(AppUser user, string otp)
        {
            if (user.PhoneOTP != otp)
                return ResponseHelper<BooleanResultDTO>.CreateResponseModel(null, true, "Incorrect OTP", 400, "خطأ بالكود");

            if (user.PhoneOTPExpiration < DateTime.UtcNow)
                return ResponseHelper<BooleanResultDTO>.CreateResponseModel(null, true, "OTP Expired", 400, "انتهت صلاحية الكود");

            return null;
        }

        #endregion
    }
}
