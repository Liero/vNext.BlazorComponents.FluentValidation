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
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace vNext.BlazorComponents.FluentValidation
{
    public class FluentValidationValidator : ComponentBase
    {
        private static readonly char[] Separators = { '.', '[' };
        private ValidationMessageStore? _validationMessageStore;
        private ValidationResult? validationResults;

        [Inject] private IServiceProvider ServiceProvider { get; set; } = default!;

        [CascadingParameter] private EditContext? CurrentEditContext { get; set; }

        [Parameter] public IValidator? Validator { get; set; }

        /// <summary>
        /// Minimum severity to be treated as an error.
        /// For example, if Severity == Error, then any validation messages with Severity warning will be ignored
        /// </summary>
        [Parameter] public Severity Severity { get; set; } = Severity.Info;

        /// <summary>
        /// Determines how validator are resolved for <see cref="EditContext.Model"/>, or <see cref="FieldIdentifier.Model"/> in case of complex models
        /// </summary>
        /// <seealso cref="DefaultValidatorFactory"/>
        [Parameter] public IValidatorFactory ValidatorFactory { get; set; } = default!;
        [Parameter] public Action<ValidationStrategy<object>>? ValidationStrategyOptions { get; set; }

        public EditContext EditContext => CurrentEditContext ?? throw new InvalidOperationException($"{nameof(FluentValidationValidator)} requires a cascading " +
                    $"parameter of type {nameof(EditContext)}. For example, you can use {nameof(FluentValidationValidator)} " +
                    $"inside an {nameof(EditForm)}.");

        public ValidationMessageStore ValidationMessageStore => _validationMessageStore ?? throw new InvalidOperationException("FluentValidationValidator not initialized.");

        public virtual async Task<bool> Validate()
        {
            return await EditContext.ValidateAsync();
        }

        public virtual Task<ValidationResult> ValidateModelAsync(bool updateValidationState = true)
            => ValidateModel(ValidationMessageStore, updateValidationState);

        public virtual Task<ValidationResult> ValidateFieldAsync(Expression<Func<object>> accessor, bool updateValidationState = true)
            => ValidateFieldAsync(FieldIdentifier.Create(accessor), updateValidationState);

        public virtual async Task<ValidationResult> ValidateFieldAsync(FieldIdentifier fieldIdentifier, bool updateValidationState = true)
            => await ValidateField(ValidationMessageStore, fieldIdentifier, updateValidationState);


        public virtual void ClearMessages()
        {
            _validationMessageStore?.Clear();
            validationResults?.Errors?.Clear();
            EditContext.NotifyValidationStateChanged();
        }

        /// <summary>
        /// get validator for <see cref="FieldIdentifier.Model"/> of <paramref name="fieldIdentifier"/>. 
        /// If <paramref name="fieldIdentifier"/> is default, return <see cref="EditContext.Model"/>
        /// </summary>       
        /// <seealso cref="ValidatorFactory"/>
        public virtual IValidator? ResolveValidator(FieldIdentifier fieldIdentifier = default)
        {
            if (EditContext == null) throw new InvalidOperationException("EditContext is null");
            object model = fieldIdentifier.Model ?? EditContext.Model;
            Type interfaceValidatorType = typeof(IValidator<>).MakeGenericType(model.GetType());
            var ctx = new ValidatorFactoryContext(interfaceValidatorType, ServiceProvider, EditContext, model, fieldIdentifier);
            return ValidatorFactory.CreateValidator(ctx);
        }

        protected override void OnInitialized()
        {
            ValidatorFactory ??= ServiceProvider.GetService<IValidatorFactory>() ?? new DefaultValidatorFactory();

            _validationMessageStore = new ValidationMessageStore(EditContext);
            EditContext.Properties["ValidationMessageStore"] = _validationMessageStore;

            EditContext.OnValidationRequested +=
                async (sender, eventArgs) => await ValidateModel(ValidationMessageStore, true);

            EditContext.OnFieldChanged +=
                async (sender, eventArgs) => await ValidateField(ValidationMessageStore, eventArgs.FieldIdentifier, true);
        }

        protected virtual string MapValidationFailureToMessage(ValidationFailure failure, ValidationResult result, ValidationContext<object> validationContext)
        {
            if (failure.Severity == Severity.Error)
            {
                return failure.ErrorMessage;
            }
            return $"[{failure.Severity}] {failure.ErrorMessage}";
        }

        protected virtual async Task<ValidationResult> ValidateModel(ValidationMessageStore messages, bool updateValidationState)
        {
            IValidator? validator = ResolveValidator();

            if (validator is not null)
            {
                ValidationContext<object> context = CreateValidationContext(validator);


                Task<ValidationResult> validateAsyncTask = validator.ValidateAsync(context);
                if (updateValidationState)
                {
                    EditContext.Properties[EditContextExtensions.PROPERTY_VALIDATEASYNCTASK] = validateAsyncTask;
                }
                var validationResults = await validateAsyncTask;
                if (updateValidationState)
                {
                    this.validationResults = validationResults;
                    messages.Clear();
                    foreach (var failure in validationResults.Errors.Where(f => f.Severity <= Severity))
                    {
                        try
                        {
                            var fieldIdentifier = ToFieldIdentifier(EditContext, failure.PropertyName);
                            string errorMessage = MapValidationFailureToMessage(failure, validationResults, context);
                            messages.Add(fieldIdentifier, errorMessage);
                        }
                        catch (InvalidOperationException ex)
                        {
                            ServiceProvider.GetService<ILogger<FluentValidationValidator>>()?.LogError(ex, $"An error occured while parsing ValidationFailure(PropertyName={failure.PropertyName})");
                        }
                    }

                    EditContext.NotifyValidationStateChanged();
                }
                return validationResults;
            }
            else
            {
                var emptyValidationResult = new ValidationResult();
                if (updateValidationState)
                {
                    EditContext.Properties[EditContextExtensions.PROPERTY_VALIDATEASYNCTASK] = Task.FromResult(emptyValidationResult);
                }
                return emptyValidationResult;
            }
        }

        protected virtual async Task<ValidationResult> ValidateField(ValidationMessageStore messages, FieldIdentifier fieldIdentifier, bool updateValidationState)
        {
            var properties = new[] { fieldIdentifier.FieldName };

            IValidator? validator = ResolveValidator(fieldIdentifier);

            if (validator is not null)
            {
                var context = CreateValidationContext(validator, fieldIdentifier);
                var validationResults = await validator.ValidateAsync(context);

                if (updateValidationState)
                {
                    messages.Clear(fieldIdentifier);
                    var fieldMessages = validationResults.Errors
                        .Where(failure => failure.Severity <= Severity)
                        .Select(failure => MapValidationFailureToMessage(failure, validationResults, context));

                    messages.Add(fieldIdentifier, fieldMessages);
                    EditContext.NotifyValidationStateChanged();
                }

                return validationResults;
            }
            return new ValidationResult();
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
            if (ValidationStrategyOptions is not null)
            {
                ValidationStrategyOptions(options);
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

                    var prop = obj.GetType().GetProperties().Where(e => e.Name == "Item" && e.GetIndexParameters().Length == 1).FirstOrDefault()
                        ?? obj.GetType().GetInterfaces().FirstOrDefault(e => e.IsGenericType && e.GetGenericTypeDefinition() == typeof(IReadOnlyList<>) || e.GetGenericTypeDefinition() == typeof(IList<>))?.GetProperty("Item"); //e.g. arrays

                    if (prop is not null)
                    {
                        // we've got an Item property
                        var indexerType = prop.GetIndexParameters()[0].ParameterType;
                        var indexerValue = Convert.ChangeType(nextToken, indexerType);
                        newObj = prop.GetValue(obj, new object[] { indexerValue });
                    }
                    else if (obj is IEnumerable<object> objEnumerable && int.TryParse(nextToken, out int indexerValue)) //e.g. hashset
                    {
                        newObj = objEnumerable.ElementAt(indexerValue);
                    }
                    else
                    {
                        throw new InvalidOperationException($"Could not find indexer on object of type {obj.GetType().FullName}.");
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