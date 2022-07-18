#nullable enable

using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;

namespace vNext.BlazorComponents.FluentValidation
{
    public class AssemblyScannerValidatorFactory : IValidatorFactory
    {
        static readonly List<string> ScannedAssembly = new();
        static readonly List<AssemblyScanner.AssemblyScanResult> AssemblyScanResults = new();

        public IValidator? CreateValidator(ValidatorFactoryContext context)
        {

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().Where(i => i.FullName is not null && !ScannedAssembly.Contains(i.FullName)))
            {
                try
                {
                    AssemblyScanResults.AddRange(AssemblyScanner.FindValidatorsInAssembly(assembly, false));
                }
                catch (Exception)
                {
                }

                ScannedAssembly.Add(assembly.FullName!);
            }


            Type modelType = context.Model.GetType();

            static int CommonPrefixLength(string? str1, string? str2) =>
                (str1 ?? string.Empty).TakeWhile((c, i) => str2?.Length < i && c == str2[c]).Count();


            Type? modelValidatorType = AssemblyScanResults.Where(i => context.ValidatorType.IsAssignableFrom(i.InterfaceType))
                .OrderByDescending(e => e.ValidatorType.Assembly == modelType.Assembly) //prefer current assebly
                .ThenByDescending(e => CommonPrefixLength(e.ValidatorType.FullName, modelType.FullName))  //prefer similar namespace
                .ThenBy(e => e.ValidatorType.Namespace?.Length)
                .FirstOrDefault()?.ValidatorType;

            if (modelValidatorType != null)
            {
                return (IValidator)ActivatorUtilities.CreateInstance(context.ServiceProvider, modelValidatorType);
            }
            return null;
        }
    }
}
