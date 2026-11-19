"""Vérification Chromium, appelée par http_smoke.py avec ADGH_SMOKE_BROWSER=1."""
from datetime import date, timedelta
from io import BytesIO
import os
import re
from urllib.parse import urlparse
from uuid import uuid4

from pypdf import PdfReader

from playwright.sync_api import expect, sync_playwright


def assert_receipt_pdf(data):
    reader = PdfReader(BytesIO(data))
    assert reader.pages
    text = ' '.join(' '.join(page.extract_text().split()) for page in reader.pages)
    assert 'Généré par AdGestionHub' in text and 'non normalisé DGI' in text, text[:500]
    assert any(len(page.images) for page in reader.pages), 'logo absent du PDF'


def assert_accent_contrast(element):
    ratio = element.evaluate(r'''node => {
        const computed = getComputedStyle(node);
        const rgb = color => color.match(/[\d.]+/g).slice(0, 3).map(Number);
        const luminance = color => rgb(color).map(n => n/255).map(n => n <= .04045 ? n / 12.92 : ((n+.055)/1.055) ** 2.4)
            .reduce((sum, n, i) => sum + n * [.2126, .7152, .0722][i], 0);
        const stops = computed.backgroundImage.match(/rgb\([^)]+\)/g);
        if (!stops?.length) { return 0; }
        const text = luminance(computed.color);
        return Math.min(...stops.map(stop => {
            const bg = luminance(stop);
            return (Math.max(bg, text) + .05) / (Math.min(bg, text) + .05);
        }));
    }''')
    assert ratio >= 4.5, ratio


def assert_centered_with_gutters(element):
    left, right = element.evaluate('''node => {
        const bounds = node.getBoundingClientRect();
        return [bounds.left, document.documentElement.clientWidth - bounds.right];
    }''')
    assert min(left, right) >= 11 and abs(left - right) <= 1, (left, right)


def assert_profile_navigation(page, active_id):
    nav = page.get_by_role('navigation', name='Réglages du profil', exact=True)
    links = nav.get_by_role('link')
    labels = ('Informations du profil', 'Adresse e-mail', 'Mot de passe',
              'Double authentification (2FA)', 'Données personnelles', 'Paramètres boutique')
    expect(links).to_have_count(len(labels))
    for label in labels:
        expect(nav.get_by_role('link', name=label, exact=True)).to_be_visible()
    expect(nav.locator('[aria-current="page"]')).to_have_count(1)
    expect(nav.locator(f'#{active_id}')).to_have_attribute('aria-current', 'page')

    ratios = links.evaluate_all(r'''nodes => {
        const channels = color => color.match(/[\d.]+/g).map(Number);
        const luminance = rgb => rgb.slice(0, 3).map(n => n / 255)
            .map(n => n <= .04045 ? n / 12.92 : ((n + .055) / 1.055) ** 2.4)
            .reduce((sum, n, i) => sum + n * [.2126, .7152, .0722][i], 0);
        return nodes.map(link => {
            const text = channels(getComputedStyle(link.querySelector('span')).color);
            let bg;
            for (let node = link; node; node = node.parentElement) {
                const current = channels(getComputedStyle(node).backgroundColor);
                if (current.length === 3 || current[3] === 1) { bg = current; break; }
            }
            if (!bg || (text.length === 4 && text[3] !== 1)) return 0;
            const a = luminance(text), b = luminance(bg);
            return (Math.max(a, b) + .05) / (Math.min(a, b) + .05);
        });
    }''')
    assert min(ratios) >= 4.5, ratios


def run_browser(base, cookies, product, server_pdf):
    with sync_playwright() as playwright:
        browser = playwright.chromium.launch(channel=os.environ.get('ADGH_SMOKE_BROWSER_CHANNEL'))
        try:
            context = browser.new_context(locale='fr-CD')
            context.route('**/*', lambda route: route.continue_() if route.request.url.startswith(base + '/') else route.abort())
            context.add_cookies([{'name': cookie.name, 'value': cookie.value, 'url': base} for cookie in cookies])
            page = context.new_page()
            failures = []
            page.on('pageerror', lambda error: failures.append(str(error)))

            for width, height in ((390, 844), (768, 1024), (1440, 900)):
                page.set_viewport_size({'width': width, 'height': height})
                for path in ('/Home/Index', '/Products/Index', '/Sales/Create', '/Reports/Index'):
                    response = page.goto(base + path)
                    assert response and response.ok and urlparse(page.url).path == path, page.url
                    assert page.evaluate('document.documentElement.scrollWidth <= innerWidth + 1'), (width, path)

            sidebar_logo = page.locator('.sidebar-heading .brand-logo')
            expect(sidebar_logo).to_have_js_property('naturalWidth', 865)
            expect(sidebar_logo).to_have_attribute('src', re.compile(r'/images/brand/logo-inverse\.webp'))
            for width in (1440, 390, 320):
                page.set_viewport_size({'width': width, 'height': 900})
                assert page.goto(base + '/Identity/Account/Manage').ok
                assert_profile_navigation(page, 'profile')
                assert page.evaluate('document.documentElement.scrollWidth <= innerWidth + 1'), width
            page.locator('#profile').focus()
            page.keyboard.press('Tab')
            email = page.locator('#email')
            expect(email).to_be_focused()
            assert email.evaluate('el => el.matches(":focus-visible") && getComputedStyle(el).outlineStyle !== "none"')
            page.keyboard.press('Enter')
            expect(page.locator('.profile-content h2')).to_have_text('Email')
            assert_profile_navigation(page, 'email')
            page.locator('#two-factor').click()
            expect(page.locator('.profile-content h2')).to_have_text('Double authentification (2FA)')
            assert_profile_navigation(page, 'two-factor')

            public = browser.new_context(locale='fr-CD', reduced_motion='no-preference')
            public.route('**/*', lambda route: route.continue_() if route.request.url.startswith(base + '/') else route.abort())
            visitor = public.new_page()
            visitor.on('pageerror', lambda error: failures.append(str(error)))
            public_sizes = ((320, 760), (390, 844), (768, 1024), (1366, 768), (1440, 900))
            for width, height in public_sizes:
                visitor.set_viewport_size({'width': width, 'height': height})
                assert visitor.goto(base + '/').ok
                assert visitor.evaluate('document.documentElement.scrollWidth <= innerWidth + 1'), width
                assert_centered_with_gutters(visitor.locator('.marketing-hero'))
                assert_centered_with_gutters(visitor.locator('#demo'))
                cta = visitor.locator('.marketing-hero').get_by_role('link', name='Créer mon compte gratuit')
                expect(cta).to_be_visible()
                expect(visitor.locator('.public-brand .brand-logo')).to_have_js_property('naturalWidth', 865)
                expect(visitor.locator('.marketing-desktop-device img')).to_have_js_property('naturalWidth', 1160)
                expect(visitor.locator('.marketing-phone-device img')).to_have_js_property('naturalWidth', 824)
                assert_accent_contrast(cta)  # no hover

            expect(visitor.locator('#hero-title')).to_have_text('Pilotez votre entreprise, de la vente aux chiffres.')
            preview = visitor.locator('.marketing-preview')
            expect(preview.locator('figcaption')).to_have_text('Aperçus illustrés · Données fictives')
            expect(preview.get_by_role('button')).to_have_count(0)

            # No motion off screen; the short introduction plays once when discovered by scrolling.
            visitor.set_viewport_size({'width': 320, 'height': 480})
            assert visitor.goto(base + '/').ok
            scene = preview.locator('.marketing-device-scene')
            flow = scene.locator('.marketing-vector-flow')
            expect(scene).not_to_be_in_viewport()
            expect(flow).to_have_css('animation-name', 'none')
            scene.scroll_into_view_if_needed()
            expect(flow).to_have_css('animation-name', 'marketing-vector-travel')
            timing = scene.evaluate('el => el.getAnimations({ subtree: true }).map(a => a.effect.getComputedTiming())')
            assert timing and all(t['iterations'] == 1 and t['endTime'] < 5000 for t in timing), timing
            visitor.emulate_media(reduced_motion='reduce')
            expect(flow).to_have_css('animation-name', 'none')
            assert scene.evaluate('el => el.getAnimations({ subtree: true }).length === 0')

            visitor.set_viewport_size({'width': 320, 'height': 760})
            mobile = visitor.get_by_role('link', name='Agrandir la vue iPhone 18 Pro Max (nouvel onglet)', exact=True)
            with visitor.expect_popup() as mobile_tab:
                mobile.press('Enter')
            mobile_tab.value.wait_for_url(base + '/images/demo/tableau-de-bord-mobile.webp')
            mobile_tab.value.close()
            visitor.get_by_role('link', name='Voir la démo', exact=True).click()
            assert urlparse(visitor.url).fragment == 'demo'
            expect(visitor.get_by_role('heading', name='Une vente, quatre écrans.')).to_be_in_viewport()
            gallery = visitor.locator('.marketing-demo-grid')
            expect(gallery.locator('figure img')).to_have_count(4)
            for image in gallery.locator('figure img').all():
                image.scroll_into_view_if_needed()
                expect(image).to_have_js_property('naturalWidth', 1160)
            zoom = gallery.get_by_role('link', name='Agrandir l’aperçu : Enregistrer la vente (nouvel onglet)')
            with visitor.expect_popup() as screenshot_tab:
                zoom.press('Enter')
            picture = screenshot_tab.value
            picture.wait_for_url(base + '/images/demo/vente.webp')
            picture.close()
            assert visitor.evaluate('document.documentElement.scrollWidth <= innerWidth + 1')
            visitor.set_viewport_size({'width': 390, 'height': 844})
            visitor.locator('.public-footer').get_by_role('link', name='Politique de confidentialité').click()
            expect(visitor.get_by_role('heading', name='Politique de confidentialité')).to_be_visible()
            visitor.goto(base + '/')
            for _ in range(14):
                visitor.keyboard.press('Tab')
                if cta.evaluate('node => node.matches(":focus-visible")'):
                    break
            assert cta.evaluate('node => node.matches(":focus-visible") && getComputedStyle(node).outlineStyle !== "none"')
            assert_accent_contrast(cta)
            visitor.keyboard.press('Enter')
            expect(visitor.get_by_role('heading', name='Créer mon entreprise')).to_be_visible()
            password = 'Navigateur-' + uuid4().hex + '!9'
            visitor.locator('#Input_StoreName').fill('Boutique navigateur')
            visitor.locator('#Input_Email').fill(uuid4().hex + '@example.test')
            visitor.locator('#Input_Password').fill(password)
            visitor.locator('#Input_ConfirmPassword').fill(password)
            visitor.get_by_role('button', name='Créer mon entreprise').click()
            expect(visitor.locator('#acceptTerms')).not_to_be_checked()
            with visitor.expect_popup() as privacy_tab:
                visitor.locator('main').get_by_role('link', name='politique de confidentialité').click()
            privacy = privacy_tab.value
            expect(privacy.get_by_role('heading', name='Politique de confidentialité')).to_be_visible()
            privacy.close()
            visitor.get_by_role('button', name='Accepter et continuer').click()
            expect(visitor.locator('#acceptTerms')).to_be_visible()
            visitor.locator('#acceptTerms').check()
            visitor.get_by_role('button', name='Accepter et continuer').click()
            expect(visitor.get_by_role('heading', name='Configurer ma boutique')).to_be_visible()
            for width, height in public_sizes:
                visitor.set_viewport_size({'width': width, 'height': height})
                assert visitor.evaluate('document.documentElement.scrollWidth <= innerWidth + 1'), width
                assert_centered_with_gutters(visitor.locator('main .page-heading'))
                assert_centered_with_gutters(visitor.locator('.settings-form'))
            visitor.set_viewport_size({'width': 390, 'height': 844})
            # Skipping is independent of this form, even when a required field is blank.
            visitor.locator('#StoreName').fill('')
            visitor.get_by_role('button', name='Configurer plus tard').click()
            expect(visitor.get_by_role('heading', name='Tableau de bord')).to_be_visible()
            expect(visitor.locator('.app-topbar .dropdown-menu a[href^="/Infos/"]')).to_have_count(0)
            expect(visitor.locator('.app-footer').get_by_role('link', name='Mentions légales')).to_be_visible()
            visitor.locator('.app-footer').get_by_role('link', name='Confidentialité').click()
            expect(visitor.get_by_role('heading', name='Politique de confidentialité')).to_be_visible()
            public.close()

            end = date.today()
            start = end - timedelta(days=13)
            path = f'/Home/Index?startDate={start}&endDate={end}&currency=CDF'
            for width in (320, 390):
                page.set_viewport_size({'width': width, 'height': 844})
                assert page.goto(base + path).ok
                chart = page.get_by_role('region', name='Graphique de l’évolution des ventes')
                expect(chart.locator('.trend-slot')).to_have_count(14)
                assert chart.evaluate('node => node.scrollWidth > node.clientWidth'), width
                assert page.evaluate('document.documentElement.scrollWidth <= innerWidth + 1'), (width, path)
                page.get_by_text('Voir les montants du graphique', exact=True).click()
                expect(page.locator('details tbody tr')).to_have_count(14)
                expect(page.locator('details tbody tr').last).to_contain_text('4 000,00 CDF')
                assert page.evaluate('document.documentElement.scrollWidth <= innerWidth + 1'), (width, path, 'valeurs')

            page.set_viewport_size({'width': 390, 'height': 844})
            page.goto(base + '/Sales/Create')
            menu = page.locator('[data-sidebar-toggle]')
            expect(menu).to_have_accessible_name('Menu')
            sidebar = page.locator('#appSidebar')
            close = sidebar.get_by_role('button', name='Fermer le menu')
            expect(sidebar).to_be_hidden()
            page.keyboard.press('Tab')  # no off-screen links ahead of the menu toggle
            expect(menu).to_be_focused()
            page.keyboard.press('Enter')
            expect(sidebar).to_be_visible()
            expect(close).to_be_focused()
            expect(menu).to_have_attribute('aria-expanded', 'true')
            page.keyboard.press('Escape')
            expect(sidebar).to_be_hidden()
            expect(menu).to_be_focused()
            expect(menu).to_have_attribute('aria-expanded', 'false')
            menu.click()
            close.click()
            expect(sidebar).to_be_hidden()
            expect(menu).to_be_focused()
            menu.click()
            page.locator('.sidebar-backdrop').click(position={'x': 340, 'y': 10})
            expect(sidebar).to_be_hidden()
            expect(menu).to_be_focused()
            menu.click()
            page.set_viewport_size({'width': 1440, 'height': 900})
            expect(sidebar).to_be_visible()
            expect(menu).to_have_attribute('aria-expanded', 'false')
            page.set_viewport_size({'width': 390, 'height': 844})
            expect(sidebar).to_be_hidden()
            expect(page.locator('#line-0-product')).to_be_visible()
            page.locator('#line-0-product').select_option(str(product))
            page.locator('#line-0-quantity').fill('2')
            page.locator('[data-add-line]').click()
            page.locator('#line-1-product').select_option(str(product))
            page.locator('[data-trade-currency]').select_option('USD')
            expect(page.locator('[data-trade-total]')).to_have_text('2,13 USD')
            page.locator('[data-trade-currency]').select_option('CDF')
            page.locator('[data-trade-discount]').fill('1,25')
            expect(page.locator('[data-expected-total]')).to_have_value('6000.25')
            page.locator('[data-payment-mode]').select_option('Partial')
            expect(page.locator('[data-partial-payment]')).to_be_visible()
            page.locator('[data-payment-mode]').select_option('Full')
            expect(page.locator('[data-partial-payment]')).to_be_hidden()
            page.get_by_role('button', name='Enregistrer la vente', exact=True).click()
            expect(page.locator('main')).to_contain_text('Reste à régler')
            invoice = int(urlparse(page.url).path.rsplit('/', 1)[-1])
            expect(page.locator('main')).to_contain_text('non normalisé DGI')
            page.goto(base + f'/Sales/Receipt/{invoice}')
            expect(page.get_by_role('button', name='Imprimer', exact=True)).to_be_visible()
            logo = page.locator('.receipt-logo')
            expect(logo).to_be_visible()
            expect(logo).to_have_js_property('naturalWidth', 96)
            page.emulate_media(media='print')
            expect(logo).to_be_visible()
            expect(page.get_by_role('button', name='Imprimer', exact=True)).to_be_hidden()
            expect(page.locator('.print-document')).to_contain_text('non normalisé DGI')
            assert_receipt_pdf(page.pdf(format='A4'))
            assert_receipt_pdf(server_pdf)
            assert not failures, failures
            print('Chromium : marges de la vitrine et de la configuration, liens légaux, contraste clavier, '
                  'onboarding, menu mobile et graphique, vente multiligne et logo/attribution dans les deux PDF validés.')
        finally:
            browser.close()


def run_popup_browser(base, cookies, title, replacement_title, publish_replacement):
    with sync_playwright() as playwright:
        browser = playwright.chromium.launch(channel=os.environ.get('ADGH_SMOKE_BROWSER_CHANNEL'))
        try:
            failures = []

            def new_page(width, height):
                page = browser.new_page(viewport={'width': width, 'height': height}, locale='fr-CD')
                page.route('**/*', lambda route: route.continue_() if route.request.url.startswith(base + '/') else route.abort())
                page.on('pageerror', lambda error: failures.append(str(error)))
                return page

            def opened(page, name):
                dialog = page.get_by_role('dialog', name=name, exact=True)
                expect(dialog).to_have_attribute('aria-modal', 'true')
                expect(dialog.locator('#publicPopupTitle')).to_be_focused()
                assert page.evaluate('document.documentElement.scrollWidth <= innerWidth + 1')
                return dialog

            def closed(page):
                expect(page.locator('#publicPopup')).to_be_hidden()
                expect(page.locator('.modal-backdrop')).to_have_count(0)

            visitor = new_page(320, 568)
            assert visitor.goto(base + '/').ok
            dialog = opened(visitor, title)
            message = dialog.get_by_role('region', name='Contenu du pop-up')
            cross = dialog.get_by_role('button', name='Fermer le pop-up')
            understood = dialog.get_by_role('button', name='J’ai compris')
            expect(cross).to_be_in_viewport()
            expect(understood).to_be_in_viewport()
            assert message.evaluate('node => node.scrollHeight > node.clientHeight')
            visitor.keyboard.press('Shift+Tab')
            expect(understood).to_be_focused()  # Focus wraps without reaching the page underneath.
            visitor.keyboard.press('Tab')
            expect(cross).to_be_focused()
            visitor.keyboard.press('Tab')
            expect(message).to_be_focused()
            visitor.keyboard.press('End')
            # Unlike wait_for_function, this does not require eval inside the page's CSP.
            assert message.evaluate('''node => node.scrollTop > 0 || new Promise(resolve => {
                node.addEventListener('scroll', () => resolve(node.scrollTop > 0), { once: true });
                setTimeout(() => resolve(node.scrollTop > 0), 2000);
            })'''), 'Le corps du pop-up ne défile pas au clavier.'
            cross.click()
            closed(visitor)
            expect(visitor.locator('#contenu')).to_be_focused()
            assert visitor.goto(base + '/Identity/Account/Login').ok
            closed(visitor)

            member = new_page(1440, 900)
            member.context.add_cookies([{'name': cookie.name, 'value': cookie.value, 'url': base} for cookie in cookies])
            assert member.goto(base + '/Home/Index').ok
            dialog = opened(member, title)
            dialog.get_by_role('button', name='J’ai compris').press('Enter')
            closed(member)
            expect(member.locator('#contenu')).to_be_focused()
            assert member.reload().ok
            closed(member)

            replacement_id = publish_replacement()
            visitor.set_viewport_size({'width': 568, 'height': 320})
            assert visitor.goto(base + '/Infos/CGU').ok
            opened(visitor, replacement_title)
            visitor.keyboard.press('Escape')
            closed(visitor)
            expect(visitor.locator('#contenu')).to_be_focused()
            assert visitor.reload().ok
            closed(visitor)
            assert not failures, failures
            print('Chromium : pop-up anonyme et connecté, défilement, focus clavier, croix, confirmation, Échap et mémorisation validés.')
            return replacement_id
        finally:
            browser.close()
