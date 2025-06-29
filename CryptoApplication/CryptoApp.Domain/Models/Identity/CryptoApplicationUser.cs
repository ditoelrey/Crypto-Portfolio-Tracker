using Microsoft.AspNetCore.Identity;

namespace CryptoApp.Domain.Models.Identity
{
    public class CryptoApplicationUser : IdentityUser
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        
         public string Address { get; set; } = string.Empty;
        public virtual Portfolio Portfolio { get; set; } = null!;
    }
}
