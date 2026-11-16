using System;
using AdGestionHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AdGestionHub.Controllers
{
    [AllowAnonymous]
    public class LocalizationController : Controller
    {
        [HttpGet]
        public IActionResult Set(string lang = "fr", string? returnUrl = null)
        {
            var normalized = string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "fr";

            Response.Cookies.Append(
                AppLocalizer.LanguageCookieName,
                normalized,
                new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    HttpOnly = false,
                    IsEssential = true,
                    SameSite = SameSiteMode.Lax,
                    Secure = Request.IsHttps
                });

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);

            return RedirectToAction("Welcome", "Home");
        }

        [HttpGet]
        public IActionResult CountryDefaults(string? code)
        {
            var country = CountryProfileCatalog.GetCountry(code);
            var primary = CountryProfileCatalog.GetCurrency(country.PrimaryCurrencyCode);
            var secondary = CountryProfileCatalog.GetCurrency(country.SuggestedSecondaryCurrencyCode);

            return Json(new
            {
                code = country.Code,
                nameFr = country.NameFr,
                nameEn = country.NameEn,
                defaultLanguage = country.DefaultLanguage,
                primaryCurrencyCode = primary.Code,
                primaryCurrencySymbol = primary.Symbol,
                primaryCurrencyDecimals = primary.Decimals,
                suggestedSecondaryCurrencyCode = secondary.Code,
                suggestedSecondarySymbol = secondary.Symbol,
                phoneDialCode = country.PhoneDialCode,
                dateFormat = country.DateFormat,
                taxIdLabelFr = country.TaxIdLabelFr,
                taxIdLabelEn = country.TaxIdLabelEn,
                tradeRegisterLabelFr = country.TradeRegisterLabelFr,
                tradeRegisterLabelEn = country.TradeRegisterLabelEn,
                standardVatRate = country.StandardVatRate,
                suggestedVatRates = country.SuggestedVatRates
            });
        }
    }
}
