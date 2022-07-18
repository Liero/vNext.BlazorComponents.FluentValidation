#nullable enable

using FluentValidation;

namespace vNext.BlazorComponents.FluentValidation
{

    /// <summary>
    /// Returns validator from ServiceProvider or by scanning assemlies
    /// </summary>
    public class DefaultValidatorFactory : IValidatorFactory
    {
        AssemblyScannerValidatorFactory? _assemblyScannerValidatorFactory;
        ServiceProviderValidatorFactory? _serviceProviderValidatorFactory;

        public bool DisableAssemblyScanning { get; set; }
        public bool DisableServiceProvider { get; set; }

        public IValidator? CreateValidator(ValidatorFactoryContext context)
        {
            IValidator? result = null;
            if (!DisableServiceProvider)
            {
                _serviceProviderValidatorFactory ??= new();
                result = _serviceProviderValidatorFactory.CreateValidator(context);
            }
            if (!DisableAssemblyScanning)
            {
                _assemblyScannerValidatorFactory ??= new();
                result ??= _assemblyScannerValidatorFactory.CreateValidator(context);
            }
            return result;
        }
    }
}
