// Scripts globaux AdGestionHub — micro-interactions SaaS Pro
(function () {
    "use strict";

    function initSidebar() {
        var sidebar = document.getElementById("appSidebar");
        var toggle = document.querySelector("[data-sidebar-toggle]");
        var backdrop = document.querySelector("[data-sidebar-backdrop]");
        var closeButton = sidebar && sidebar.querySelector("[data-sidebar-close]");
        if (!sidebar || !toggle || !closeButton || !backdrop) return;

        var mobile = window.matchMedia("(max-width: 991.98px)");
        var activeLink = sidebar.querySelector(".sidebar-link.active");
        var open = false;
        function revealActiveLink() {
            if (activeLink) activeLink.scrollIntoView({ block: "nearest", inline: "nearest" });
        }
        function close(restoreFocus) {
            if (!open) return;
            open = false;
            document.body.classList.remove("sidebar-open");
            toggle.setAttribute("aria-expanded", "false");
            if (restoreFocus && mobile.matches) toggle.focus({ preventScroll: true });
        }
        toggle.addEventListener("click", function () {
            if (!mobile.matches) return;
            if (open) { close(true); return; }
            open = true;
            document.body.classList.add("sidebar-open");
            toggle.setAttribute("aria-expanded", "true");
            revealActiveLink();
            closeButton.focus({ preventScroll: true });
        });
        closeButton.addEventListener("click", function () { close(true); });
        backdrop.addEventListener("click", function () { close(true); });
        document.addEventListener("keydown", function (event) {
            if (open && event.key === "Escape" && !event.defaultPrevented) {
                event.preventDefault();
                close(true);
            }
        });
        // If keyboard focus leaves the disclosure, don't leave the backdrop over the page.
        document.addEventListener("focusin", function (event) {
            if (open && !sidebar.contains(event.target) && event.target !== toggle) close(false);
        });
        function syncViewport() {
            if (mobile.matches && sidebar.contains(document.activeElement)) toggle.focus({ preventScroll: true });
            close(false);
            if (!mobile.matches) revealActiveLink();
        }
        if (mobile.addEventListener) mobile.addEventListener("change", syncViewport);
        else mobile.addListener(syncViewport);
        window.addEventListener("pageshow", function (event) { if (event.persisted) close(true); });
        if (!mobile.matches) revealActiveLink();
    }

    function initPublicPopup() {
        var popup = document.getElementById("publicPopup");
        if (!popup || !window.bootstrap || document.querySelector(".modal.show")) return;

        var version = popup.dataset.publicPopupVersion;
        var storageKey = "adgh.public-popup.dismissed";
        try {
            if (window.localStorage.getItem(storageKey) === version) return;
        } catch { /* Storage can be disabled by the browser. */ }
        try {
            if (window.sessionStorage.getItem(storageKey) === version) return;
        } catch { /* Keeping the page usable takes precedence over persistence. */ }

        var previousFocus = document.activeElement;
        var title = document.getElementById("publicPopupTitle");
        var first = popup.querySelector(".public-popup-close");
        var last = popup.querySelector(".public-popup-footer button");
        var instance = window.bootstrap.Modal.getOrCreateInstance(popup);
        popup.addEventListener("shown.bs.modal", function () {
            title.focus({ preventScroll: true });
        });
        // Wrap explicitly at the two ends; the last control can be the last tab stop on the page.
        popup.addEventListener("keydown", function (event) {
            if (event.key !== "Tab" || event.ctrlKey || event.altKey || event.metaKey) return;
            var active = document.activeElement;
            if (event.shiftKey && (active === first || active === title || active === popup)) {
                event.preventDefault();
                last.focus();
            } else if (!event.shiftKey && active === last) {
                event.preventDefault();
                first.focus();
            }
        });
        popup.addEventListener("hide.bs.modal", function () {
            try {
                window.localStorage.setItem(storageKey, version);
            } catch {
                try { window.sessionStorage.setItem(storageKey, version); } catch { }
            }
            if (popup.contains(document.activeElement)) document.activeElement.blur();
        });
        popup.addEventListener("hidden.bs.modal", function () {
            var target = previousFocus && previousFocus !== document.body && previousFocus.getClientRects().length
                ? previousFocus : document.getElementById("contenu");
            if (target) target.focus({ preventScroll: true });
        });
        // Following a link in the announcement should not reopen it on the destination page.
        popup.addEventListener("click", function (event) {
            if (!event.defaultPrevented && event.target.closest("a[href]")) instance.hide();
        });
        instance.show();
    }

    function initMarketingMotion() {
        var preview = document.querySelector("[data-marketing-preview]");
        var control = preview && preview.querySelector("[data-marketing-motion]");
        if (!control) return;

        var motion = window.matchMedia("(prefers-reduced-motion: no-preference)");
        var pauseLabel = control.querySelector("[data-motion-pause]");
        var playLabel = control.querySelector("[data-motion-play]");
        var paused = false;
        function update() {
            control.hidden = !motion.matches;
            preview.dataset.motion = motion.matches ? (paused ? "paused" : "running") : "static";
            pauseLabel.hidden = paused;
            playLabel.hidden = !paused;
        }
        control.addEventListener("click", function () {
            paused = !paused;
            update();
        });
        if (motion.addEventListener) motion.addEventListener("change", update);
        else motion.addListener(update);
        update();
    }

    document.addEventListener("DOMContentLoaded", function () {
        initSidebar();
        initPublicPopup();
        initMarketingMotion();
        // 1. Ombre de la topbar au défilement (effet glass premium)
        var topbar = document.querySelector(".app-topbar");
        if (topbar) {
            var onScroll = function () {
                topbar.classList.toggle("scrolled", window.scrollY > 8);
            };
            window.addEventListener("scroll", onScroll, { passive: true });
            onScroll();
        }

        // 2. Apparition en cascade des cartes KPI / panneaux (stagger léger)
        var cards = document.querySelectorAll(".card-dashboard, .kpi-card");
        if (cards.length && "IntersectionObserver" in window) {
            cards.forEach(function (card, i) {
                card.style.opacity = "0";
                card.style.transform = "translateY(8px)";
                card.style.transition = "opacity 0.4s cubic-bezier(0.4,0,0.2,1), transform 0.4s cubic-bezier(0.4,0,0.2,1), box-shadow 0.22s cubic-bezier(0.4,0,0.2,1), border-color 0.22s cubic-bezier(0.4,0,0.2,1)";
                card.style.transitionDelay = (i * 60) + "ms";
            });
            var observer = new IntersectionObserver(function (entries) {
                entries.forEach(function (entry) {
                    if (entry.isIntersecting) {
                        entry.target.style.opacity = "1";
                        entry.target.style.transform = "translateY(0)";
                        observer.unobserve(entry.target);
                    }
                });
            }, { threshold: 0.08 });
            cards.forEach(function (card) { observer.observe(card); });
        }

        // 3. Soumission AJAX fluide du formulaire de Feedback global
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
