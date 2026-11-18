"""Vérification Chromium, appelée par http_smoke.py avec ADGH_SMOKE_BROWSER=1."""
import os
from urllib.parse import urlparse

from playwright.sync_api import expect, sync_playwright


def run_browser(base, cookies, product):
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
            page.emulate_media(media='print')
            expect(page.get_by_role('button', name='Imprimer', exact=True)).to_be_hidden()
            expect(page.locator('.print-document')).to_contain_text('non normalisé DGI')
            assert page.pdf(format='A4').startswith(b'%PDF-')
            assert not failures, failures
            print('Chromium : trois largeurs, saisie multiligne en CDF/USD et impression validées.')
        finally:
            browser.close()
