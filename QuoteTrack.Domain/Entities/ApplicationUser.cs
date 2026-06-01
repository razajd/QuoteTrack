using Microsoft.AspNetCore.Identity;

namespace QuoteTrack.Domain.Entities
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;

        // This is now officially part of your model
        public bool IsActive { get; set; } = true;
    }
}