using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebAssemblyDemo.Models
{
    public class Address
    {
        public string Line1 { get; set; }
        public string Line2 { get; set; }
        public string Town { get; set; }
        public string County { get; set; }
        public string Postcode { get; set; }
    }

    public class AddressValidator : AbstractValidator<Address>
    {
        public AddressValidator()
        {
            RuleFor(p => p.Line1).NotEmpty().WithMessage("You must enter Line 1");
            RuleFor(p => p.Town).NotEmpty().WithMessage("You must enter a town");
            RuleFor(p => p.Postcode).NotEmpty().WithMessage("You must enter a postcode");

            RuleFor(p => p.County).Configure(c => c.CascadeMode = CascadeMode.Stop)
                .NotEmpty().WithMessage("You must enter a county")
                .MinimumLength(3).WithSeverity(Severity.Warning).WithMessage("Enter full country name");

        }
    }
}
