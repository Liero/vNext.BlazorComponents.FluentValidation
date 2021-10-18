/*
    this code is inspired by https://github.com/Blazored/FluentValidation
 */
#nullable enable
using Microsoft.AspNetCore.Components.Forms;
using System;

namespace vNext.BlazorComponents.FluentValidation
{
    public class ValidatorFactoryContext
    {
        public ValidatorFactoryContext(Type validatorType, IServiceProvider serviceProvider, EditContext editContext, object model, FieldIdentifier fieldIdentifier = default)
        {
            ValidatorType = validatorType;
            ServiceProvider = serviceProvider;
            EditContext = editContext;
            Model = model;
            FieldIdentifier = fieldIdentifier;
        }
        /// <summary>
        /// Generic Validator interface <see cref="global::FluentValidation.IValidator{}"/>
        /// </summary>
        public Type ValidatorType { get; }
        public IServiceProvider ServiceProvider { get; }
        public EditContext EditContext { get; }
        public object Model { get; }
        public FieldIdentifier FieldIdentifier { get; }
    }
}