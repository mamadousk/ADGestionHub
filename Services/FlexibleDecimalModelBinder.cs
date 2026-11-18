using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AdGestionHub.Services
{
    /// <summary>
    /// Permet aux formulaires HTML d'envoyer des nombres décimaux aussi bien avec un point ("12.50",
    /// standard des inputs HTML5 type="number") qu'avec une virgule ("12,50", culture fr-FR).
    /// </summary>
    public sealed class FlexibleDecimalModelBinder : IModelBinder
    {
        private static readonly Regex DecimalText = new(@"\A[+-]?(?:[0-9]+|[0-9]{1,3}(?:[ \u00a0\u202f][0-9]{3})+)(?:[.,][0-9]+)?\z",
            RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            ArgumentNullException.ThrowIfNull(bindingContext);

            var modelName = bindingContext.ModelName;
            var valueProviderResult = bindingContext.ValueProvider.GetValue(modelName);
            if (valueProviderResult == ValueProviderResult.None)
                return Task.CompletedTask;

            bindingContext.ModelState.SetModelValue(modelName, valueProviderResult);
            var rawValue = valueProviderResult.FirstValue;

            if (string.IsNullOrWhiteSpace(rawValue))
            {
                if (Nullable.GetUnderlyingType(bindingContext.ModelType) != null)
                {
                    bindingContext.Result = ModelBindingResult.Success(null);
                }
                else
                {
                    bindingContext.ModelState.TryAddModelError(modelName, "Ce montant est obligatoire.");
                }
                return Task.CompletedTask;
            }

            var text = rawValue.Trim();
            var normalized = text.Replace(" ", "").Replace("\u00A0", "").Replace("\u202F", "").Replace(',', '.');
            var separator = normalized.IndexOf('.');
            if (separator >= 0) normalized = normalized.TrimEnd('0').TrimEnd('.');
            // decimal.TryParse can round when the text exceeds the precision of decimal.
            // Validate the syntax and capacity first so an entered amount is never silently changed.
            if (text.Length <= 64 && DecimalText.IsMatch(text)
                && normalized.TrimStart('+', '-', '0').Replace(".", "").Length <= 28
                && (separator < 0 || normalized.Length - separator - 1 <= 28)
                && decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsed))
            {
                bindingContext.Result = ModelBindingResult.Success(parsed);
            }
            else
            {
                bindingContext.ModelState.TryAddModelError(modelName, "Veuillez saisir un montant numérique valide.");
            }

            return Task.CompletedTask;
        }
    }

    public sealed class FlexibleDecimalModelBinderProvider : IModelBinderProvider
    {
        public IModelBinder? GetBinder(ModelBinderProviderContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            var modelType = context.Metadata.ModelType;
            if (modelType == typeof(decimal) || modelType == typeof(decimal?))
            {
                return new FlexibleDecimalModelBinder();
            }

            return null;
        }
    }
}
