# FluentValidation
A library for using FluentValidation with Blazor that supports **async validation, severity levels** and more.

For introduction, see this [blog post](https://blog.vyvojari.dev/advanced-validation-in-blazor-using-fluentvalidation/)

Live demo can be found here: https://blazorrepl.telerik.com/GQYpbvbP37ENDltY58

![Build & Test Main](https://github.com/Liero/vNext.BlazorComponents.FluentValidation/workflows/Build%20&%20Test%20Main/badge.svg)

![Nuget](https://img.shields.io/nuget/v/vNext.BlazorComponents.FluentValidation.svg)



### Installing

You can install from Nuget using the following command:

`Install-Package vNext.BlazorComponents.FluentValidation`

Or via the Visual Studio package manger.

## Basic Usage
Start by add the following using statement to your root `_Imports.razor`.

```csharp
@using vNext.BlazorComponents.FluentValidation
```

You can then use it as follows within a `EditForm` component.

```html
<EditForm Model="@Person" OnValidSubmit="@SubmitValidForm">
    <FluentValidationValidator />
    <ValidationSummary />

    <p>
        <label>Name: </label>
        <InputText @bind-Value="@Person.Name" />
    </p>

    <p>
        <label>Age: </label>
        <InputNumber @bind-Value="@Person.Age" />
    </p>

    <p>
        <label>Email Address: </label>
        <InputText @bind-Value="@Person.EmailAddress" />
    </p>

    <button type="submit">Save</button>

</EditForm>

@code {
    Person Person { get; set; } = new Person();

    async Task SubmitValidForm(EditContext editContext)
    {
        var validationResult = await editContext.GetValidationResultAsync(); //make sure async valiation completes
        if (validationResult.IsValid)
        {
            await JS.InvokeVoidAsync("alert", "Form Submitted Successfully!");
        }
    }
}
```

## Discovering Validators
The component locates validators using `IValidatorFactory` optional service.
The `DefaultValidatorFactory` implementation check for validators registered with DI first.

If it finds multiple validators, validators in the same assembly and namespace are takes precedence. 
If it can't find any, it will then try scanning the applications assemblies

You can override default behaviour on by registering `IValidatorFactory` service:

```csharp
   services.AddSingleton<IValidatorFactory>(new DefaultValidatorFactory { DisableAssemblyScanning = false })
```

or per FluentValidationValidator component
```razor
  <FluentValidationValidator ValidatorFactory="customValidatorFactory" />

  @code {
      IValidatorFactory customValidatorFactory = new MyCustomValidatorFactory();
  }
```

 See [DefaultValidatorFactory.cs](vNext.BlazorComponents.FluentValidation/DefaultValidatorFactory.cs) for more info.

## Validating Complex Models

```csharp
class PersonValidator : AbstractValidator<Person>
{
        public PersonValidator() {
            RuleFor(x => x.Name).NotEmpty();
            RuleFor(x => x.Address).SetValidator(new AddressValidator()); //must be set explicitelly
        }
}
class AddressValidator: AbstractValidator<Address> //should be separate class
{
        public AddressValidator() {
            RuleFor(x => x.Street).NotEmpty();
        }  
}
```

Blazor performs two kinds of validation:

1. Model validation triggered by `EditContext.Validate()` which is called usually on form submit
2. FieldIdentifier validation triggered by `EditContext.NotifyValidationStateChanged()` which is called automatically, when user edits inputs.

When Field validation is triggered, FluentValidator will create validator based on `FieldIdentifier.Model`, which might be different from `EditContext.Model` in case of complex models.

Consider following example:

```razor
<EditContext Model="Person" OnValidSubmit="ValidSubmitted">
    <InputText @bind-Value="Person.Name" />
    <InputText @bind-Value="Person.Address.Street" />
    <button type="submit" />
</EditContext>
```

1. When user edits `Person.Name`, FluentValidator validates the property using `IValidator<Person>`
2. When user edits `Person.Address.Street`,  FluentValidator validates the property using `IValidator<Address>`
3. When user clicks submit button,  FluentValidator validates the Person class using `IValidator<Person>`.

   However, `IValidator<Address>` will not be automatically used, unless it is explicitelly defined for Address property in `IValidator<Person>`.

### Common mistakes:

Address street is validated only when user edits the input, but not on submit:
```csharp
class PersonValidator : AbstractValidator<Person>
{
        public PersonValidator() {
            RuleFor(x => x.Name).NotEmpty();
        }
}
class AddressValidator: AbstractValidator<Person>
{
        public AddressValidator() {
            RuleFor(x => x.Street).NotEmpty();
        }  
}
```

Street is validated only on submit, but not when user edits the input:
```csharp
class PersonValidator : AbstractValidator<Person>
{
        public PersonValidator() {
            RuleFor(x => x.Name).NotEmpty();
            RuleFor(x => x.Address.Street).NotEmpty();
        }
}
```
