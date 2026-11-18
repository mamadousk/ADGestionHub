"""Vérification Chromium, appelée par http_smoke.py avec ADGH_SMOKE_BROWSER=1."""
from io import BytesIO
import os
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

            public = browser.new_context(locale='fr-CD')
            public.route('**/*', lambda route: route.continue_() if route.request.url.startswith(base + '/') else route.abort())
            visitor = public.new_page()
            visitor.on('pageerror', lambda error: failures.append(str(error)))
            for width, height in ((320, 760), (390, 844), (768, 1024), (1440, 900)):
                visitor.set_viewport_size({'width': width, 'height': height})
                assert visitor.goto(base + '/').ok
                assert visitor.evaluate('document.documentElement.scrollWidth <= innerWidth + 1'), width
                cta = visitor.locator('.marketing-hero').get_by_role('link', name='Créer mon compte gratuit')
                expect(cta).to_be_visible()
                assert_accent_contrast(cta)  # no hover
            visitor.set_viewport_size({'width': 390, 'height': 844})
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
            visitor.get_by_role('button', name='Accepter et continuer').click()
            expect(visitor.locator('#acceptTerms')).to_be_visible()
            visitor.locator('#acceptTerms').check()
            visitor.get_by_role('button', name='Accepter et continuer').click()
            expect(visitor.get_by_role('heading', name='Configurer ma boutique')).to_be_visible()
            assert visitor.evaluate('document.documentElement.scrollWidth <= innerWidth + 1')
            # Skipping is independent of this form, even when a required field is blank.
            visitor.locator('#StoreName').fill('')
            visitor.get_by_role('button', name='Configurer plus tard').click()
            expect(visitor.get_by_role('heading', name='Tableau de bord')).to_be_visible()
            public.close()

            page.set_viewport_size({'width': 390, 'height': 844})
            page.goto(base + '/Sales/Create')
            page.get_by_role('button', name='Menu', exact=True).click()
            assert page.locator('body').evaluate('node => node.classList.contains("sidebar-open")')
            page.locator('.sidebar-backdrop').click(position={'x': 340, 'y': 10})
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
            print('Chromium : vitrine, contraste clavier, onboarding, saisie multiligne et logo/attribution dans les deux PDF validés.')
        finally:
            browser.close()
