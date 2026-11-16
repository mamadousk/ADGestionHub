// Scripts globaux AdGestionHub
(function () {
    "use strict";

    document.addEventListener("DOMContentLoaded", function () {
        // 1. Accepter la virgule ET le point décimal dans jQuery Validation (culture fr-FR)
        if (window.jQuery && window.jQuery.validator) {
            window.jQuery.validator.methods.number = function (value, element) {
                if (this.optional(element)) return true;
                var normalized = String(value).trim().replace(/\s/g, "").replace(",", ".");
                return /^-?\d+(\.\d+)?$/.test(normalized);
            };

            window.jQuery.validator.methods.range = function (value, element, param) {
                if (this.optional(element)) return true;
                var normalized = parseFloat(String(value).trim().replace(/\s/g, "").replace(",", "."));
                return !isNaN(normalized) && normalized >= param[0] && normalized <= param[1];
            };
        }

        // 2. Soumission AJAX fluide du formulaire de Feedback global
        var feedbackForm = document.getElementById("globalFeedbackForm");
        if (feedbackForm) {
            feedbackForm.addEventListener("submit", function (e) {
                e.preventDefault();
                var submitBtn = feedbackForm.querySelector("button[type='submit']");
                var feedbackAlert = document.getElementById("feedbackFormAlert");
                var formData = new FormData(feedbackForm);

                if (submitBtn) submitBtn.disabled = true;

                fetch(feedbackForm.action, {
                    method: "POST",
                    headers: { "X-Requested-With": "XMLHttpRequest" },
                    body: new URLSearchParams(formData)
                })
                    .then(function (resp) {
                        if (!resp.ok) throw new Error("Validation error");
                        return resp.json();
                    })
                    .then(function (data) {
                        if (feedbackAlert) {
                            feedbackAlert.className = "alert alert-success small py-2 mb-3";
                            feedbackAlert.textContent = (data && data.message) || "Merci pour votre avis !";
                            feedbackAlert.classList.remove("d-none");
                        }
                        feedbackForm.reset();
                        setTimeout(function () {
                            var modalEl = document.getElementById("feedbackModal");
                            if (modalEl && window.bootstrap) {
                                var instance = window.bootstrap.Modal.getInstance(modalEl);
                                if (instance) instance.hide();
                            }
                            if (feedbackAlert) feedbackAlert.classList.add("d-none");
                        }, 1200);
                    })
                    .catch(function () {
                        if (feedbackAlert) {
                            feedbackAlert.className = "alert alert-danger small py-2 mb-3";
                            feedbackAlert.textContent = "Veuillez saisir un commentaire valide avant d'envoyer.";
                            feedbackAlert.classList.remove("d-none");
                        }
                    })
                    .finally(function () {
                        if (submitBtn) submitBtn.disabled = false;
                    });
            });
        }
    });
})();
