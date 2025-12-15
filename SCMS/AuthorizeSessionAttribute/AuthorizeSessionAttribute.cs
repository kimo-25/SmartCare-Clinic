using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SCMS.Filters
{
    public class AuthorizeSessionAttribute : ActionFilterAttribute
    {
        private readonly string? _role; // اختياري، لو عايز تتحقق من Role معين

        public AuthorizeSessionAttribute(string? role = null)
        {
            _role = role;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var session = context.HttpContext.Session;
            var userId = session.GetString("UserId");
            var userType = session.GetString("UserType");

            // لو مش مسجل دخول
            if (string.IsNullOrEmpty(userId))
            {
                context.Result = new RedirectToActionResult("Login", "Account", null);
                return;
            }

            // لو حددت Role وتحقق منها
            if (!string.IsNullOrEmpty(_role) && _role != userType)
            {
                context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
                return;
            }

            base.OnActionExecuting(context);
        }
    }
}
