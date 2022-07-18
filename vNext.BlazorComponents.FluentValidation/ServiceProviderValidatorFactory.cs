#nullable enable

using FluentValidation;

namespace vNext.BlazorComponents.FluentValidation
{
    public class ServiceProviderValidatorFactory: IValidatorFactory
    {
        public IValidator? CreateValidator(ValidatorFactoryContext context)
        {
            if (context.ServiceProvider.GetService(context.ValidatorType) is IValidator validator)
            {
                return validator;
            }        

            return null;
        }
    }
}
