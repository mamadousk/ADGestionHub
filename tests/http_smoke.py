"""Vérifie les vues Razor, l'authentification, les formulaires et l'isolation via HTTP.

Lancer après `dotnet build`. Une base SQLite et un serveur jetables sont créés pour le test.
"""
from html import unescape
from html.parser import HTMLParser
from http.cookiejar import CookieJar
import os
from pathlib import Path
import socket
import subprocess
import tempfile
import time
import traceback
from urllib.error import HTTPError, URLError
from urllib.parse import urlencode, urlparse
from urllib.request import HTTPCookieProcessor, Request, build_opener
from uuid import uuid4


class Token(HTMLParser):
    value = None

    def handle_starttag(self, tag, attrs):
        fields = dict(attrs)
        if tag == 'input' and fields.get('name') == '__RequestVerificationToken':
            self.value = fields.get('value')


class Browser:
    def __init__(self, base):
        self.base = base
        self.cookies = CookieJar()
        self.client = build_opener(HTTPCookieProcessor(self.cookies))

    def request(self, path, form=None, expected=200):
        request = Request(self.base + path, None if form is None else urlencode(form).encode())
        try:
            response = self.client.open(request, timeout=20)
        except HTTPError as error:
            response = error
        with response:
            payload = response.read()
            final = urlparse(response.geturl()).path
            if response.code != expected:
                raise AssertionError(f'{path}: HTTP {response.code}, attendu {expected}; arrivée {final}\n{payload[:500]!r}')
            return final, payload, response.headers.get_content_type()

    def post(self, path, form, source=None, expected=200):
        _, payload, _ = self.request(source or path)
        parser = Token()
        parser.feed(payload.decode())
        assert parser.value, f'Jeton anti-CSRF absent : {source or path}'
        return self.request(path, {**form, '__RequestVerificationToken': parser.value}, expected=expected)


def id_from(path, controller):
    assert path.startswith(f'/{controller}/Details/'), path
    return int(path.rsplit('/', 1)[-1])


def small_text(html):
    return unescape(html.decode()).replace('\u202f', ' ').replace('\u00a0', ' ')


def checkout(browser, password, name):
    path, _, _ = browser.post('/Identity/Account/Register', {
        'Input.StoreName': name, 'Input.Email': f'{uuid4().hex}@example.test',
        'Input.PrimaryCurrencyCode': 'CDF', 'Input.Password': password, 'Input.ConfirmPassword': password,
    })
    assert path == '/Home/Index', path
    path, _, _ = browser.post('/Settings/Edit', {
        'StoreName': name, 'PrimaryCurrencyCode': 'CDF', 'CdfPerUsd': '2800,125678',
        'CustomPaymentMethods': 'Bon test',
    }, source='/Settings')
    assert path in ('/Settings', '/Settings/Index'), path
    _, html, _ = browser.request('/Settings')
    assert '2800,125678' in small_text(html) or '2800.125678' in small_text(html)


def run(base):
    password = 'Pilote-' + uuid4().hex + '!9'
    a, b, staff = (Browser(base) for _ in range(3))
    checkout(a, password, 'Pilote Marchand A')
    path, _, _ = a.post('/Products/Create', {
        'Name': 'Riz Pilote', 'PurchasePrice': '900,35', 'SalePrice': '2000,50', 'StockQuantity': '8',
        'Sku': 'RIZ-P', 'Category': 'Épicerie', 'StockAlertThreshold': '3', 'CatalogCurrencyCode': 'CDF',
    })
    product = id_from(path, 'Products')
    path, _, _ = a.post('/Clients/Create', {'Name': 'Client confidentiel A', 'Email': 'client@example.test'})
    client = id_from(path, 'Clients')
    _, sale_page, _ = a.request('/Sales/Create')
    assert 'name="Items[0].ProductId"' in small_text(sale_page)
    assert any(f'data-price-cdf="{price}"' in small_text(sale_page) for price in ('2000.5', '2000.50'))
    path, payload, _ = a.post('/Sales/Create', {
        'ClientId': client, 'CurrencyCode': 'CDF', 'Items[0].ProductId': product, 'Items[0].Quantity': '2',
        'Discount': '1,00', 'PaymentMode': 'Partial', 'InitialPayment': '1000', 'PaymentMethod': 'Bon test',
        'ExpectedTotal': '4000.00',
    })
    sale = id_from(path, 'Sales')
    assert '3 000,00 CDF' in small_text(payload)
    a.request(f'/Payments/Settle/{sale}?target=Sale', {'Amount': '3000'}, expected=400)
    _, payload, _ = a.post(f'/Payments/Settle/{sale}?target=Sale', {
        'CurrencyCode': 'CDF', 'Amount': '1000', 'Method': 'Mobile Money',
    }, source=f'/Sales/Details/{sale}')
    assert '2 000,00 CDF' in small_text(payload)
    _, payload, _ = a.request(f'/Products/Details/{product}')
    assert 'Stock initial' in small_text(payload) and 'Stock après' in small_text(payload)
    _, payload, _ = a.request('/Home/Index')
    assert '4 000,00 CDF' in small_text(payload) and '2 000,00 CDF' in small_text(payload)
    for path, signature, mime in (
        (f'/Sales/Receipt/{sale}?format=pdf', b'%PDF-', 'application/pdf'),
        ('/Reports/Export?format=pdf', b'%PDF-', 'application/pdf'),
        ('/Reports/Export?format=xlsx', b'PK', 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'),
        ('/Reports/Export?format=csv', b'\xef\xbb\xbfRubrique;', 'text/csv'),
    ):
        _, data, kind = a.request(path)
        assert data.startswith(signature) and kind == mime, (path, kind)
    _, payload, _ = a.request(f'/Sales/Receipt/{sale}')
    assert 'non normalisé DGI' in small_text(payload)
    a.post('/Expenses/Create', {'Description': 'Transport test', 'Amount': '2,35', 'CurrencyCode': 'USD',
                              'Category': 'Transport', 'PaymentMethod': 'Espèces', 'Date': time.strftime('%Y-%m-%d')})
    _, report, _ = a.request('/Reports/Index?currency=USD')
    assert 'Transport test' in small_text(report)

    path, _, _ = a.post('/Suppliers/Create', {'Name': 'Fournisseur confidentiel A'})
    supplier = id_from(path, 'Suppliers')
    path, payload, _ = a.post('/Purchases/Create', {
        'SupplierId': supplier, 'CurrencyCode': 'USD', 'Items[0].ProductId': product,
        'Items[0].Quantity': '2', 'Items[0].UnitPrice': '1,25', 'PaymentMode': 'Unpaid', 'ReceiveNow': 'false',
    })
    purchase = id_from(path, 'Purchases')
    assert 'En attente de réception' in small_text(payload)
    _, payload, _ = a.post(f'/Purchases/Receive/{purchase}', {}, source=f'/Purchases/Details/{purchase}')
    assert 'Réception confirmée' in small_text(payload)
    quote_form = {'ClientId': client, 'CurrencyCode': 'CDF', 'Items[0].ProductId': product, 'Items[0].Quantity': '1'}
    path, payload, _ = a.post('/Quotes/Create', {**quote_form, 'Publish': 'false'})
    quote = id_from(path, 'Quotes')
    assert 'Brouillon' in small_text(payload)
    a.post(f'/Quotes/Edit/{quote}', {**quote_form, 'Publish': 'true'})
    a.post(f'/Quotes/Accept/{quote}', {}, source=f'/Quotes/Details/{quote}')
    path, payload, _ = a.post(f'/Quotes/ConvertToSale/{quote}', {}, source=f'/Quotes/Details/{quote}')
    assert id_from(path, 'Sales') != sale and 'Reste à régler' in small_text(payload)

    checkout(b, password, 'Pilote Marchand B')
    for path in (f'/Sales/Details/{sale}', f'/Sales/Receipt/{sale}?format=pdf', f'/Products/Details/{product}', f'/Clients/Details/{client}',
                 f'/Purchases/Details/{purchase}', f'/Quotes/DownloadPdf/{quote}'):
        b.request(path, expected=404)
    _, data, _ = b.request('/Reports/Export?format=csv')
    assert b'Client confidentiel A' not in data and b'Riz Pilote' not in data

    username = 'vendeur' + uuid4().hex[:12]
    path, _, _ = a.post('/Users/Create', {'UserName': username, 'Email': f'{username}@example.test',
                                        'Password': password, 'ConfirmPassword': password, 'Role': 'Employé'})
    assert path in ('/Users', '/Users/Index'), path
    _, payload, _ = staff.post('/Identity/Account/Login', {'Input.Email': username, 'Input.Password': password})
    assert 'Pilote Marchand A' in small_text(payload)
    staff.request(f'/Sales/Details/{sale}')
    # MVC uses a cookie challenge and redirects a forbidden user to the AccessDenied Razor page.
    try:
        path, _, _ = staff.request('/Reports/Index')
    except AssertionError:
        path, _, _ = staff.request('/Reports/Index', expected=403)
    assert path.endswith('/AccessDenied'), path
    print('Parcours HTTP : inscription, stock, ventes, règlements, achats, devis, exports et isolation réussis.')
    if os.environ.get('ADGH_SMOKE_BROWSER') == '1':
        from browser_smoke import run_browser
        run_browser(base, a.cookies, product)


def main():
    with socket.socket() as sock:
        sock.bind(('127.0.0.1', 0))
        port = sock.getsockname()[1]
    root = Path(__file__).resolve().parents[1]
    with tempfile.TemporaryDirectory() as tmp, tempfile.TemporaryFile(mode='w+') as log:
        host = f'http://127.0.0.1:{port}'
        env = {**os.environ, 'ASPNETCORE_ENVIRONMENT': 'Development', 'DOTNET_ENVIRONMENT': 'Development',
               'DatabaseProvider': 'Sqlite', 'ConnectionStrings__DefaultConnection': f'Data Source={tmp}/http.db'}
        command = ['dotnet', 'run', '--project', str(root / 'AdGestionHub.csproj'), '--no-build', '--no-launch-profile', '--urls', host]
        with subprocess.Popen(command, stdout=log, stderr=subprocess.STDOUT, env=env, cwd=root) as server:
            try:
                for _ in range(90):
                    try:
                        Browser(host).request('/health')
                        break
                    except URLError:
                        if server.poll() is not None:
                            raise RuntimeError('Le serveur a quitté avant de démarrer.')
                        time.sleep(1)
                else:
                    raise RuntimeError('Le démarrage a dépassé le délai du test.')
                run(host)
            except Exception as error:
                log.seek(0)
                print(log.read()[-15000:])
                if os.environ.get('GITHUB_ACTIONS') == 'true':
                    detail = ''.join(traceback.format_exception(error))[-3300:]
                    encoded = detail.replace('%', '%25').replace('\r', '%0D').replace('\n', '%0A')
                    print(f'::error title=Recette HTTP ou navigateur::{encoded}', flush=True)
                raise
            finally:
                server.terminate()
                try:
                    server.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    server.kill()


if __name__ == '__main__':
    main()
