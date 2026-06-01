using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using QuoteTrack.Domain.Entities;

namespace QuoteTrack.Web.Controllers
{
    // This tells the server to listen at the URL: /api/logout
    [Route("api/[controller]")]
    [ApiController]
    public class LogoutController : ControllerBase
    {
        private readonly SignInManager<ApplicationUser> _signInManager;

        public LogoutController(SignInManager<ApplicationUser> signInManager)
        {
            _signInManager = signInManager;
        }

        [HttpPost]
        public async Task<IActionResult> Post()
        {
            // Wipes the user's secure authentication cookie
            await _signInManager.SignOutAsync();

            // Sends them back to the login page (or home page)
            return Redirect("~/");
        }
    }
}