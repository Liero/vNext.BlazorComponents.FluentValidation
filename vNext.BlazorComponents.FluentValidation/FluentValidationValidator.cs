/*
    this code is inspired by https://github.com/Blazored/FluentValidation
 */
#nullable enable
using FluentValidation;
using FluentValidation.Internal;
using FluentValidation.Results;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static FluentValidation.AssemblyScanner;

namespace vNext.BlazorComponents.FluentValidation
{
    public class FluentValidationValidator : ComponentBase
    {
        private static readonly char[] Separators = { '.', '[' };
        private static readonly List<string> ScannedAssembly = new List<string>();
        private static readonly List<AssemblyScanResult> AssemblyScanResults = new List<AssemblyScanResult>();

        [Inject] private IServiceProvider ServiceProvider { get; set; } = default!;

        [CascadingParameter] private EditContext? CurrentEditContext { get; set; }

        [Parameter] public IValidator? Validator { get; set; }

        [Parameter] public Severity Severity { get; set; } = Severity.Info;
        [Parameter] public Func<ValidatorFactoryContext, IValidator?>? ValidatorFactory { get; set; }
        [Parameter] public Func<ValidatorFactoryContext, IValidator?>? ValidationMessageFactory { get; set; }
        [Parameter] public bool DisableAssemblyScanning { get; set; }

        public EditContext EditContext => CurrentEditContext ?? throw new InvalidOperationException($"{nameof(FluentValidationValidator)} requires a cascading " +
                    $"parameter of type {nameof(EditContext)}. For example, you can use {nameof(FluentValidationValidator)} " +
                    $"inside an {nameof(EditForm)}.");

        protected Action<ValidationStrategy<object>>? Options { get; set; }

        public virtual async Task<bool> Validate(Action<ValidationStrategy<object>>? options = null)
        {
            Options = options;           
            try
            {
                return await EditContext.ValidateAsync();
            }
            finally
            {
                if (Options == options)
                {
                    Options = null;
                }
            }
        }

        protected override void OnInitialized()
        {
            var messages = new ValidationMessageStore(EditContext);

            EditContext.OnValidationRequested +=
                async (sender, eventArgs) => await ValidateModel(messages);

            EditContext.OnFieldChanged +=
                async (sender, eventArgs) => await ValidateField(messages, eventArgs.FieldIdentifier);
        }

        protected virtual string MapValidationFailureToMessage(ValidationFailure failure, ValidationResult result, ValidationContext<object> validationContext)
        {
            if (failure.Severity == Severity.Error)
            {
                return failure.ErrorMessage;
            }
            return $"[{failure.Severity}] {failure.ErrorMessage}";
        }

        protected virtual IValidator? GetValidator(FieldIdentifier fieldIdentifier = default)
        {
            if (EditContext == null) throw new InvalidOperationException("EditContext is null");
            object model = fieldIdentifier.Model ?? EditContext.Model;
            Type interfaceValidatorType = typeof(IValidator<>).MakeGenericType(model.GetType());
            var ctx = new ValidatorFactoryContext(interfaceValidatorType, ServiceProvider, EditContext, model, fieldIdentifier);

            if (ValidatorFactory != null)
            {
                return ValidatorFactory(ctx);
            }

            if (ServiceProvider.GetService(interfaceValidatorType) is IValidator validator)
            {
                return validator;
            }


            if (!DisableAssemblyScanning)
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().Where(i => i.FullName is not null && !ScannedAssembly.Contains(i.FullName)))
                {
                    try
                    {
                        AssemblyScanResults.AddRange(FindValidatorsInAssembly(assembly));
                    }
                    catch (Exception)
                    {
                    }

                    ScannedAssembly.Add(assembly.FullName!);
                }


                Type? modelValidatorType = AssemblyScanResults.FirstOrDefault(i => interfaceValidatorType.IsAssignableFrom(i.InterfaceType))?.ValidatorType;

                if (modelValidatorType == null)
                {
                    return null;
                }

                return (IValidator)ActivatorUtilities.CreateInstance(ServiceProvider, modelValidatorType);
            }

            return null;
        }


        protected virtual async Task ValidateModel(ValidationMessageStore messages)
        {
            if (EditContext == null) throw new InvalidOperationException("EditContext is null");

            IValidator? validator = GetValidator();

            if (validator is not null)
            {
                ValidationContext<object> context = CreateValidationContext(validator);

                Task<ValidationResult> validateAsyncTask = validator.ValidateAsync(context);
                EditContext.Properties[EditContextExtensions.PROPERTY_VALIDATEASYNCTASK] = validateAsyncTask;
                ValidationResult validationResults = await validateAsyncTask;

                messages.Clear();
                foreach (var failure in validationResults.Errors.Where(f => f.Severity <= Severity))
                {
                    var fieldIdentifier = ToFieldIdentifier(EditContext, failure.PropertyName);
                    string errorMessage = MapValidationFailureToMessage(failure, validationResults, context);
                    messages.Add(fieldIdentifier, errorMessage);
                }

                EditContext.NotifyValidationStateChanged();
            }
        }

        protected virtual async Task ValidateField(ValidationMessageStore messages, FieldIdentifier fieldIdentifier)
        {
            var properties = new[] { fieldIdentifier.FieldName };

            IValidator? validator = GetValidator(fieldIdentifier);

            if (validator is not null)
            {
                var context = CreateValidationContext(validator, fieldIdentifier);
                var validationResults = await validator.ValidateAsync(context);

                messages.Clear(fieldIdentifier);
                var fieldMessages = validationResults.Errors
                    .Where(failure => failure.Severity <= Severity)
                    .Select(failure => MapValidationFailureToMessage(failure, validationResults, context));

                messages.Add(fieldIdentifier, fieldMessages);

                EditContext.NotifyValidationStateChanged();
            }
        }

        protected virtual ValidationContext<object> CreateValidationContext(IValidator validator, FieldIdentifier fieldIdentifier = default)
        {
            var model = fieldIdentifier.Model ?? EditContext.Model;
            var context = ValidationContext<object>.CreateWithOptions(model, opt => ConfigureValidationStrategy(opt, validator, fieldIdentifier));
            context.RootContextData.Add(nameof(FluentValidationValidator), this);
            context.RootContextData.Add(nameof(EditContext), EditContext);
            return context;
        }

        protected virtual void ConfigureValidationStrategy(ValidationStrategy<object> options, IValidator validator, FieldIdentifier fieldIdentifier = default)
        {
            if (Options is not null)
            {
                Options(options);
            }
      
            if (fieldIdentifier.FieldName is not null)
            {
                options.IncludeProperties(fieldIdentifier.FieldName);
            }
            
        }

        protected static FieldIdentifier ToFieldIdentifier(EditContext editContext, string propertyPath)
        {
            // This code is taken from an article by Steve Sanderson (https://blog.stevensanderson.com/2019/09/04/blazor-fluentvalidation/)
            // all credit goes to him for this code.

            // This method parses property paths like 'SomeProp.MyCollection[123].ChildProp'
            // and returns a FieldIdentifier which is an (instance, propName) pair. For example,
            // it would return the pair (SomeProp.MyCollection[123], "ChildProp"). It traverses
            // as far into the propertyPath as it can go until it finds any null instance.

            var obj = editContext.Model;

            while (true)
            {
                var nextTokenEnd = propertyPath.IndexOfAny(Separators);
                if (nextTokenEnd < 0)
                {
                    return new FieldIdentifier(obj, propertyPath);
                }

                var nextToken = propertyPath.Substring(0, nextTokenEnd);
                propertyPath = propertyPath.Substring(nextTokenEnd + 1);

                object? newObj;
                if (nextToken.EndsWith("]"))
                {
                    // It's an indexer
                    // This code assumes C# conventions (one indexer named Item with one param)
                    nextToken = nextToken.Substring(0, nextToken.Length - 1);
                    var prop = obj.GetType().GetProperty("Item");

                    if (prop is not null)
                    {
                        // we've got an Item property
                        var indexerType = prop.GetIndexParameters()[0].ParameterType;
                        var indexerValue = Convert.ChangeType(nextToken, indexerType);
                        newObj = prop.GetValue(obj, new object[] { indexerValue });
                    }
                    else
                    {
                        // If there is no Item property
                        // Try to cast the object to array
                        if (obj is object[] array)
                        {
                            var indexerValue = Convert.ToInt32(nextToken);
                            newObj = array[indexerValue];
                        }
                        else
                        {
                            throw new InvalidOperationException($"Could not find indexer on object of type {obj.GetType().FullName}.");
                        }
                    }
                }
                else
                {
                    // It's a regular property
                    var prop = obj.GetType().GetProperty(nextToken);
                    if (prop == null)
                    {
                        throw new InvalidOperationException($"Could not find property named {nextToken} on object of type {obj.GetType().FullName}.");
                    }
                    newObj = prop.GetValue(obj);
                }

                if (newObj == null)
                {
                    // This is as far as we can go
                    return new FieldIdentifier(obj, nextToken);
                }

                obj = newObj;
            }
        }
    }
}