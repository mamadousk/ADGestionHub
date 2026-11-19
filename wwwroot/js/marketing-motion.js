// The public landing page stays fully readable until these enhancements are ready.
(function () {
    "use strict";

    const page = document.querySelector(".marketing-page");
    if (!page) return;

    const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)");
    const canObserve = "IntersectionObserver" in window;

    function onMotionPreferenceChange(callback) {
        if (reducedMotion.addEventListener) reducedMotion.addEventListener("change", callback);
        else reducedMotion.addListener(callback);
    }

    function initDevices() {
        const scene = page.querySelector(".marketing-device-scene");
        const toggle = scene && scene.querySelector("[data-scene-motion-toggle]");
        if (!toggle) return;

        let userPaused = false;
        let inView = !canObserve;
        function update() {
            scene.classList.toggle("has-motion", !reducedMotion.matches);
            scene.classList.toggle("is-motion-paused", userPaused || !inView || document.hidden);
            toggle.hidden = reducedMotion.matches;
            toggle.setAttribute("aria-pressed", String(userPaused));
        }

        toggle.addEventListener("click", function () {
            userPaused = !userPaused;
            update();
        });
        document.addEventListener("visibilitychange", update);
        onMotionPreferenceChange(update);
        if (canObserve) {
            const observer = new IntersectionObserver(function (entries) {
                inView = entries[entries.length - 1].isIntersecting;
                update();
            });
            observer.observe(scene);
        }
        update();
    }

    function initEntrances() {
        if (!canObserve) return;
        const targets = document.querySelectorAll(
            ".public-topbar .public-nav, .marketing-page [data-landing-reveal], " +
            ".marketing-page [data-landing-stagger] > *, .marketing-page [data-landing-draw], .public-footer .row > div"
        );
        // Start just before the viewport edge. Only the items entering together share a stagger.
        const observer = new IntersectionObserver(function (entries) {
            let delay = 0;
            for (const entry of entries) {
                if (!entry.isIntersecting) continue;
                const target = entry.target;
                observer.unobserve(target);
                if (reducedMotion.matches || target.contains(document.activeElement)) continue;
                target.style.setProperty("--landing-delay", delay + "ms");
                target.classList.add("is-landing-entering");
                if (!target.hasAttribute("data-landing-draw")) delay = Math.min(delay + 65, 260);
            }
        }, { rootMargin: "0px 0px 28px 0px" });
        targets.forEach(function (target) { observer.observe(target); });

        function finish(event) {
            if (event.animationName !== "marketing-enter" && event.animationName !== "marketing-trace-draw") return;
            event.target.classList.remove("is-landing-entering");
            event.target.style.removeProperty("--landing-delay");
        }
        document.addEventListener("animationend", finish);
        document.addEventListener("animationcancel", finish);
    }

    function initReadingProgress() {
        const progress = document.querySelector(".marketing-reading-progress");
        const scrolling = document.scrollingElement;
        if (!progress || !scrolling) return;
        let pending = 0;

        function update() {
            if (pending || reducedMotion.matches) return;
            pending = requestAnimationFrame(function () {
                pending = 0;
                const range = scrolling.scrollHeight - scrolling.clientHeight;
                const ratio = range > 0 ? Math.min(1, Math.max(0, scrolling.scrollTop / range)) : 0;
                progress.style.setProperty("--landing-progress", String(ratio));
            });
        }
        function sync() {
            progress.hidden = reducedMotion.matches;
            update();
        }
        window.addEventListener("scroll", update, { passive: true });
        window.addEventListener("resize", update);
        if ("ResizeObserver" in window) new ResizeObserver(update).observe(document.body);
        onMotionPreferenceChange(sync);
        sync();
    }

    initDevices();
    initEntrances();
    initReadingProgress();
})();
