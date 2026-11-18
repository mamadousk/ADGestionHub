"""Vérifie les vues Razor, l'authentification, les formulaires et l'isolation via HTTP.

Lancer après `dotnet build`. Une base SQLite et un serveur jetables sont créés pour le test.
"""
from base64 import b64decode
from html import unescape
from html.parser import HTMLParser
from http.cookiejar import CookieJar
import os
import re
from pathlib import Path
import socket
import sqlite3
import struct
import subprocess
import tempfile
import time
import traceback
from urllib.error import HTTPError, URLError
from urllib.parse import urlencode, urlparse
from urllib.request import HTTPCookieProcessor, Request, build_opener
from uuid import uuid4
import zlib


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

    def request(self, path, form=None, expected=200, file=None):
        headers = {}
        body = None if form is None else urlencode(form).encode()
        if file:
            boundary = uuid4().hex
            chunks = []
            for name, value in form.items():
                chunks.append(f'--{boundary}\r\nContent-Disposition: form-data; name="{name}"\r\n\r\n{value}\r\n'.encode())
            filename, kind, data = file
            chunks.append(f'--{boundary}\r\nContent-Disposition: form-data; name="Logo"; filename="{filename}"\r\nContent-Type: {kind}\r\n\r\n'.encode() + data + b'\r\n')
            chunks.append(f'--{boundary}--\r\n'.encode())
            body = b''.join(chunks)
            headers['Content-Type'] = 'multipart/form-data; boundary=' + boundary
        request = Request(self.base + path, body, headers)
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

    def post(self, path, form, source=None, expected=200, file=None):
        _, payload, _ = self.request(source or path)
        parser = Token()
        parser.feed(payload.decode())
        assert parser.value, f'Jeton anti-CSRF absent : {source or path}'
        return self.request(path, {**form, '__RequestVerificationToken': parser.value}, expected=expected, file=file)


def id_from(path, controller):
    assert path.startswith(f'/{controller}/Details/'), path
    return int(path.rsplit('/', 1)[-1])


def small_text(html):
    return unescape(html.decode()).replace('\u202f', ' ').replace('\u00a0', ' ')


PNG_HEADER = b'\x89PNG\r\n\x1a\n'
TERMS = '/Onboarding/Terms'
SETUP = '/Settings/Setup'
SETTINGS = {'StoreName': 'Pilote Marchand A', 'PrimaryCurrencyCode': 'CDF', 'CdfPerUsd': '2800,125678',
            'CustomPaymentMethods': 'Bon test', 'Address': '12 avenue des tests', 'Phone': '+243800000000',
            'ContactEmail': 'boutique-a@example.test', 'Description': 'Épicerie et denrées', 'FooterMessage': 'Merci pour votre passage.'}


def png_logo(width=96, height=32):
    def chunk(kind, data):
        return struct.pack('>I', len(data)) + kind + data + struct.pack('>I', zlib.crc32(kind + data))

    header = struct.pack('>IIBBBBB', width, height, 8, 2, 0, 0, 0)
    pixels = (b'\x00' + width * bytes((208, 64, 22))) * height
    return (PNG_HEADER + chunk(b'IHDR', header) + chunk(b'tEXt', b'Comment\x00non-conserve')
            + chunk(b'IDAT', zlib.compress(pixels)) + chunk(b'IEND', b''))


def receipt_logo(html):
    match = re.search(r'class="receipt-logo" src="data:image/png;base64,([^"\s]+)"', unescape(html.decode()))
    return b64decode(match[1]) if match else None


def checkout(browser, password, name, configure):
    email = f'{uuid4().hex}@example.test'
    path, terms, _ = browser.post('/Identity/Account/Register', {
        'Input.StoreName': name, 'Input.Email': email, 'Input.PrimaryCurrencyCode': 'CDF',
        'Input.Password': password, 'Input.ConfirmPassword': password,
    })
    assert path == TERMS and 'name="acceptTerms"' in terms.decode() and 'checked' not in terms.decode(), path
    version = re.search(r'name="version" value="([^"]+)"', terms.decode())[1]
    for url in ('/Home/Index', '/Settings/Setup', '/Reports/Export?format=csv'):
        assert browser.request(url)[0] == TERMS
    browser.request('/Infos/CGU')  # The legal texts remain accessible before acceptance.
    assert browser.post('/Settings/SkipSetup', {}, source=TERMS)[0] == TERMS
    browser.request(TERMS, {'version': version, 'acceptTerms': 'true'}, expected=400)
    assert browser.post(TERMS, {'version': version})[0] == TERMS
    assert browser.post(TERMS, {'version': 'obsolete', 'acceptTerms': 'true'})[0] == TERMS
    if not configure:
        assert browser.post('/Identity/Account/Logout', {}, source=TERMS)[0].endswith('/Login')
        assert browser.post('/Identity/Account/Login?returnUrl=%2FSales%2FCreate', {
            'Input.Email': email, 'Input.Password': password,
        })[0] == TERMS
    path, html, _ = browser.post(TERMS, {'version': version, 'acceptTerms': 'true'})
    assert path == SETUP and 'Conditions d’utilisation acceptées' in small_text(html)
    text = small_text(html)
    assert name in text and email in text and 'Configurer plus tard' in text
    for field in (*SETTINGS.keys(), 'Logo'):
        assert f'name="{field}"' in text, field
    assert browser.request('/Sales/Create')[0] == SETUP
    if configure:
        browser.request('/Settings/SkipSetup', {}, expected=400)
        assert browser.post(TERMS, {'version': version, 'acceptTerms': 'true'}, source=SETUP)[0] == SETUP
        data = png_logo()
        path, _, _ = browser.post(SETUP, SETTINGS, file=('enseigne.png', 'image/png', data))
        assert path in ('/Home', '/Home/Index'), path
        _, html, _ = browser.request('/Settings')
        assert '2800,125678' in small_text(html) or '2800.125678' in small_text(html)
        assert 'Épicerie et denrées' in small_text(html) and '12 avenue des tests' in small_text(html)
        saved = receipt_logo(html)
        assert saved and saved.startswith(PNG_HEADER) and b'non-conserve' not in saved
    else:
        path, _, _ = browser.post('/Settings/SkipSetup', {}, source=SETUP)
        assert path in ('/Home', '/Home/Index'), path
        assert browser.request(SETUP)[0] in ('/Settings', '/Settings/Index')
        _, html, _ = browser.request('/Settings')
        assert receipt_logo(html) is None and email in small_text(html)
    return email


def seed_archives(db_file, email, product):
    today = time.strftime('%Y-%m-%d')
    with sqlite3.connect(db_file) as db:
        db.execute('PRAGMA foreign_keys = ON')
        shop, = db.execute('SELECT Id FROM Boutiques WHERE Email = ?', (email,)).fetchone()
        supplier = db.execute('INSERT INTO Suppliers (BoutiqueId, Name) VALUES (?, ?)', (shop, 'Fournisseur archivé A')).lastrowid
        purchase = db.execute('INSERT INTO Purchases (BoutiqueId, SupplierId, SupplierName, CurrencyCode, Total, RecordedAt) VALUES (?, ?, ?, ?, ?, ?)',
                              (shop, supplier, 'Fournisseur archivé A', 'USD', 12.50, today + ' 10:00:00')).lastrowid
        db.execute('INSERT INTO PurchaseItems (PurchaseId, ProductId, ProductName, Quantity, UnitPrice) VALUES (?, ?, ?, ?, ?)',
                   (purchase, product, 'Riz Pilote', 2, 6.25))
        db.execute('INSERT INTO Payments (BoutiqueId, PurchaseId, Method, CurrencyCode, AppliedCurrencyCode, Amount, AppliedAmount, RecordedAt) VALUES (?, ?, ?, ?, ?, ?, ?, ?)',
                   (shop, purchase, 'Espèces', 'USD', 'USD', 5, 5, today + ' 11:00:00'))
        return purchase


def run(base, db_file):
    password = 'Pilote-' + uuid4().hex + '!9'
    a, b, staff, public = (Browser(base) for _ in range(4))
    path, html, _ = public.request('/')
    assert path == '/' and 'Version 1 gratuite' in small_text(html) and 'Créer mon compte gratuit' in small_text(html)
    assert 'https://fonts.googleapis' not in html.decode() and 'cookieConsent' not in html.decode()
    for doc in ('CGU', 'MentionsLegales', 'Confidentialite', 'Cookies'):
        path, html, _ = public.request(f'/Infos/{doc}')
        text = small_text(html)
        assert doc in path and 'Adhil Digital Solutions' in text and 'contact@adhildigital.com' in text
        assert 'RCCM' not in text and 'contact@adhil-digital.com' not in text
    email_a = checkout(a, password, 'Pilote Marchand A', configure=True)
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
    assert 'non normalisé DGI' in small_text(payload) and 'Généré par AdGestionHub' in small_text(payload)
    logo_a = receipt_logo(payload)
    assert logo_a and 'Merci pour votre passage.' in small_text(payload)
    a.post('/Expenses/Create', {'Description': 'Transport test', 'Amount': '2,35', 'CurrencyCode': 'USD',
                              'Category': 'Transport', 'PaymentMethod': 'Espèces', 'Date': time.strftime('%Y-%m-%d')})
    _, report, _ = a.request('/Reports/Index?currency=USD')
    assert 'Transport test' in small_text(report) and 'Historique fournisseurs' not in small_text(report)

    purchase = seed_archives(db_file, email_a, product)
    for removed in ('/Quotes', '/Quotes/Create', '/Quotes/DownloadPdf/1', '/Purchases', f'/Purchases/Details/{purchase}', '/Suppliers', '/Suppliers/Create'):
        a.request(removed, expected=404)
    for write in ('/Quotes/Create', '/Quotes/Accept/1', '/Quotes/ConvertToSale/1', '/Purchases/Create',
                  f'/Purchases/Receive/{purchase}', '/Suppliers/Create', '/Marketing/JoinWaitingList', f'/Payments/Settle/{purchase}?target=Purchase'):
        a.post(write, {'Amount': '5', 'Method': 'Espèces', 'CurrencyCode': 'USD'}, source='/Settings', expected=404)
    _, report, _ = a.request('/Reports/Index?currency=USD')
    assert 'Historique fournisseurs' in small_text(report) and 'Fournisseur archivé A' in small_text(report) and '7,50 USD' in small_text(report)
    _, csv, _ = a.request('/Reports/Export?format=csv&currency=USD')
    assert 'Fournisseur archivé A' in small_text(csv) and 'Achats archivés' in small_text(csv)
    _, debts, _ = a.request('/Debts')
    assert 'Fournisseur archivé A' not in small_text(debts) and 'Créances clients' in small_text(debts)

    for filename, kind, invalid, error in (
        ('intrus.png', 'image/png', b'<svg xmlns="http://www.w3.org/2000/svg"></svg>', 'image PNG ou JPEG valide'),
        ('trop-large.png', 'image/png', png_logo(2049, 1), '2 048 pixels par côté'),
        ('trop-lourd.png', 'image/png', b'\x00' * (2 * 1024 * 1024 + 1), '2 Mo maximum'),
    ):
        path, html, _ = a.post('/Settings/Edit', {**SETTINGS, 'StoreName': 'Modification refusée'}, source='/Settings', file=(filename, kind, invalid))
        assert path == '/Settings/Edit' and error in small_text(html) and receipt_logo(html) == logo_a
        _, unchanged, _ = a.request(f'/Sales/Receipt/{sale}')
        assert 'Pilote Marchand A' in small_text(unchanged) and receipt_logo(unchanged) == logo_a

    email_b = checkout(b, password, 'Pilote Marchand B', configure=False)
    for path in (f'/Sales/Details/{sale}', f'/Sales/Receipt/{sale}', f'/Sales/Receipt/{sale}?format=pdf', f'/Products/Details/{product}', f'/Clients/Details/{client}'):
        b.request(path, expected=404)
    _, updated_b, _ = b.post('/Settings/Edit', {'StoreName': 'Pilote Marchand B', 'PrimaryCurrencyCode': 'CDF'},
                             source='/Settings', file=('autre.png', 'image/png', png_logo(48, 16)))
    logo_b = receipt_logo(updated_b)
    assert logo_b and logo_b != logo_a
    assert receipt_logo(a.request(f'/Sales/Receipt/{sale}')[1]) == logo_a
    _, data, _ = b.request('/Reports/Export?format=csv')
    assert b'Client confidentiel A' not in data and b'Riz Pilote' not in data

    username = 'vendeur' + uuid4().hex[:12]
    path, _, _ = a.post('/Users/Create', {'UserName': username, 'Email': f'{username}@example.test',
                                        'Password': password, 'ConfirmPassword': password, 'Role': 'Employé'})
    assert path in ('/Users', '/Users/Index'), path
    path, payload, _ = staff.post('/Identity/Account/Login', {'Input.Email': username, 'Input.Password': password})
    assert path == TERMS and staff.request(f'/Sales/Receipt/{sale}')[0] == TERMS
    version = re.search(r'name="version" value="([^"]+)"', payload.decode())[1]
    path, payload, _ = staff.post(TERMS, {'version': version, 'acceptTerms': 'true'})
    assert path in ('/Home', '/Home/Index') and 'Pilote Marchand A' in small_text(payload)
    _, html, _ = staff.request(f'/Sales/Receipt/{sale}')
    assert receipt_logo(html) == logo_a
    # MVC uses a cookie challenge and redirects a forbidden user to the AccessDenied Razor page.
    for restricted in ('/Reports/Index', '/Settings/Setup', '/Settings'):
        try:
            path, _, _ = staff.request(restricted)
        except AssertionError:
            path, _, _ = staff.request(restricted, expected=403)
        assert path.endswith('/AccessDenied'), path
    with sqlite3.connect(db_file) as db:
        done = db.execute('SELECT Email, SetupPending FROM Boutiques').fetchall()
        assert dict(done) == {email_a: 0, email_b: 0}
        assert db.execute('SELECT COUNT(*), COUNT(DISTINCT UserId), COUNT(AcceptedAtUtc) FROM TermsAcceptances WHERE Version = ?', (version,)).fetchone() == (3, 3, 3)
        assert db.execute('SELECT COUNT(*) FROM StoreLogos').fetchone() == (2,)
        assert db.execute('SELECT COUNT(*) FROM Payments WHERE PurchaseId = ?', (purchase,)).fetchone() == (1,)
        assert db.execute('SELECT StockQuantity FROM Products WHERE Id = ?', (product,)).fetchone() == (6,)
    _, audit, _ = a.request('/Settings/Audit')
    assert f'Compte employé créé : {username}' in small_text(audit)
    _, audit, _ = b.request('/Settings/Audit')
    assert username not in small_text(audit)
    if os.environ.get('ADGH_SMOKE_BROWSER') == '1':
        from browser_smoke import run_browser
        _, server_pdf, _ = a.request(f'/Sales/Receipt/{sale}?format=pdf')
        run_browser(base, a.cookies, product, server_pdf)

    a.post('/Settings/Edit', {**SETTINGS, 'RemoveLogo': 'true'}, source='/Settings')
    _, html, _ = a.request(f'/Sales/Receipt/{sale}')
    assert receipt_logo(html) is None and 'Généré par AdGestionHub' in small_text(html)
    _, _, mime = a.request(f'/Sales/Receipt/{sale}?format=pdf')
    assert mime == 'application/pdf'
    print('Parcours HTTP : CGU, configuration et report, logo, ventes, anciens modules désactivés, rapports et isolation réussis.')


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
                run(host, Path(tmp) / 'http.db')
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
