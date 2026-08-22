using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using FormFlow.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FormFlow.Web.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly UserService _users;

        public LoginModel(UserService users)
        {
            _users = users;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new InputModel();

        public async Task<IActionResult> OnPostAsync(string returnUrl)
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var user = await _users.AuthenticateAsync(Input.Username, Input.Password);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "اسم المستخدم أو كلمة المرور غير صحيحة");
                return Page();
            }

            await UserService.SignInAsync(HttpContext, user, Input.RememberMe);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToPage("/Inbox/Index");
        }

        public class InputModel
        {
            [Required(ErrorMessage = "اسم المستخدم مطلوب")]
            [Display(Name = "اسم المستخدم")]
            public string Username { get; set; }

            [Required(ErrorMessage = "كلمة المرور مطلوبة")]
            [Display(Name = "كلمة المرور")]
            public string Password { get; set; }

            [Display(Name = "تذكرني")]
            public bool RememberMe { get; set; }
        }
    }
}
