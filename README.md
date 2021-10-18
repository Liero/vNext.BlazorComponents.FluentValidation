# FluentValidation
A library for using FluentValidation with Blazor

![Build & Test Main](https://github.com/Liero/vNext.BlazorComponents.FluentValidation/workflows/Build%20&%20Test%20Main/badge.svg)

![Nuget](https://img.shields.io/nuget/v/vNext.BlazorComponents.FluentValidation.svg)

### Installing

You can install from Nuget using the following command:

`Install-Package vNext.BlazorComponents.FluentValidation`

Or via the Visual Studio package manger.

## Basic Usage
Start by add the following using statement to your root `_Imports.razor`.

```csharp
@using Blazored.FluentValidation
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

## Finding Validators
By default, the component will check for validators registered with DI first. If it can't find, any it will then try scanning the applications assemblies to find validators using reflection.

You can control this behaviour using the `DisableAssemblyScanning` parameter. If you only wish the component to get validators from DI, set the value to `true` and assembly scanning will be skipped.

```html
<FluentValidationValidator DisableAssemblyScanning="@true" />
```
**Note:** When scanning assemblies the component will swallow any exceptions thrown by that process. This is to stop exceptions thrown by scanning third party dependencies crashing your app.


For advanced scenarios, you can customize how the finding of validators

```html
<FluentValidationValidator ValidatorFactory="CreateValidator" />
```
```csharp
IValidator CreateValidator(ValidatorFactoryContext ctx)
{
    if (ctx.Model == Person.Address)
    {
        return new AddressValidator();
    }
    return (IValidator)ctx.ServiceProvider.GetService(ctx.ValidatorType);
}
```

