/*
    this code is inspired by https://github.com/Blazored/FluentValidation
 */
#nullable enable
using FluentValidation.Results;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Components.Forms
{
    public static class EditContextExtensions
    {
        public const string PROPERTY_VALIDATEASYNCTASK = "ValidateAsyncTask";
        public static async Task<bool> ValidateAsync(this EditContext editContext)
        {
            editContext.Validate();
            await GetValidationResultAsync(editContext);
            return !editContext.GetValidationMessages().Any();
        }

        public static Task<ValidationResult> GetValidationResultAsync(this EditContext editContext)
        {
            if (!editContext.Properties.TryGetValue(PROPERTY_VALIDATEASYNCTASK, out var validateAsyncTask))
                throw new InvalidOperationException("ValidationResult not found. Either EditContext has not been validater or EditForm does not contain FluentValidationValidator");
            {
                return (Task<ValidationResult>)validateAsyncTask;
            }

        }
    }
}