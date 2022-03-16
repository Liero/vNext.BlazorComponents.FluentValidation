#nullable enable

using FluentValidation;

namespace vNext.BlazorComponents.FluentValidation
{
    public interface IValidatorFactory
    {
        IValidator? CreateValidator(ValidatorFactoryContext validator);
    }
}
