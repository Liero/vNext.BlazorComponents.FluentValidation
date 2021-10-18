using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebAssemblyDemo.Models
{
    public class EmailModel
    {
        public string EmailAddress { get; set; }

    }

    public class EmailModelValidator : AbstractValidator<EmailModel>
    {
        public EmailModelValidator()
        {
            RuleFor(p => p.EmailAddress)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .MustAsync(async (email, cancellationToken) => await IsHotmailAsync(email))
                .WithMessage("Email address must end with @hotmail.com");
        }

        private async Task<bool> IsHotmailAsync(string email)
        {
            await Task.Delay(500);
            return email.ToLower().EndsWith("@hotmail.com");
        }
    }
}