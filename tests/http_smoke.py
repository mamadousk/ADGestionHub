"""Vérifie les vues Razor, l'authentification, les formulaires et l'isolation via HTTP.

Lancer après `dotnet build`. Une base SQLite et un serveur jetables sont créés pour le test.
"""
from base64 import b32decode, b64decode
import hmac
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
from urllib.parse import parse_qs, quote, urlencode, urlparse
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
            self.last_url = urlparse(response.geturl())
            self.last_disposition = response.headers.get('Content-Disposition')
            final = self.last_url.path
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
LOGIN = '/Identity/Account/Login'
LOGOUT = '/Identity/Account/Logout'
LOGIN_2FA = '/Identity/Account/LoginWith2fa'
RECOVERY = '/Identity/Account/LoginWithRecoveryCode'
HOME = ('/Home', '/Home/Index')
REPORTS = ('/Reports', '/Reports/Index')
PRIVACY = '/Infos/Confidentialite'
PRIVACY_ACCENTED = quote('/Infos/Confidentialité')
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


def assert_privacy(browser, url=PRIVACY):
    path, html, kind = browser.request(url)
    assert path == PRIVACY and kind == 'text/html', (url, path, kind)
    assert '<h1>Politique de confidentialité</h1>' in small_text(html)


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
    assert_privacy(browser, PRIVACY_ACCENTED)
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



def verify_session_navigation(base, email, password, sale):
    visitor = Browser(base)
    credentials = {'Input.Email': email, 'Input.Password': password, 'Input.RememberMe': 'true'}

    def check_html(response, paths, text=None):
        path, data, kind = response
        assert path in paths and kind == 'text/html' and not visitor.last_disposition, (path, kind, visitor.last_disposition)
        if text:
            assert text in small_text(data), (path, text)
        return data

    def target(page, return_url):
        return f'{page}?{urlencode({"returnUrl": return_url})}'

    _, landing, _ = visitor.request('/')
    link = re.search(r'<a[^>]+href="([^"?#]+)"[^>]*>\s*Se connecter\s*</a>', small_text(landing))
    assert link and link.group(1) == LOGIN
    check_html(visitor.request(link.group(1)), (LOGIN,), 'id="account"')
    for step in (LOGIN_2FA, RECOVERY):
        # Without a temporary 2FA session, the browser should return to the login form instead of a 500.
        check_html(visitor.request(target(step, '/Sales/Create')), (LOGIN,), 'La vérification de connexion a expiré.')

    date = time.strftime('%Y-%m-%d')
    export = f'/Reports/Export?format=pdf&startDate={date}&endDate={date}&currency=USD'
    check_html(visitor.request(export), (LOGIN,), 'id="account"')
    check_html(visitor.post(target(LOGIN, export), credentials), REPORTS, 'Rapports d’activité')
    assert parse_qs(visitor.last_url.query) == {'startDate': [date], 'endDate': [date], 'currency': ['USD']}, visitor.last_url
    # A signed-in user who opens the login URL never sees the sign-in form or a file response.
    check_html(visitor.request(LOGIN), HOME, 'Tableau de bord')
    check_html(visitor.request(target(LOGIN, '/reports/EXPORT/?format=csv')), REPORTS, 'Rapports d’activité')
    check_html(visitor.request(target(LOGIN, 'https://outside.example.test/')), HOME, 'Tableau de bord')
    check_html(visitor.request(target(LOGIN, LOGIN)), HOME, 'Tableau de bord')
    # GET and POST without a valid anti-forgery token cannot sign the user out.
    check_html(visitor.request(LOGOUT), HOME, 'Tableau de bord')
    visitor.request(LOGOUT, {}, expected=400)
    check_html(visitor.request('/Home/Index'), HOME, 'Tableau de bord')
    check_html(visitor.post(target(LOGOUT, export), {}, source='/Home/Index'), (LOGIN,), 'id="account"')
    check_html(visitor.request('/Home/Index'), (LOGIN,), 'id="account"')

    # After expiry on a PDF URL, resume at the corresponding HTML receipt; explicit PDF is still available.
    printable = f'/Sales/Receipt/{sale}'
    pdf = printable + '?format=PDF'
    check_html(visitor.request(pdf), (LOGIN,), 'id="account"')
    check_html(visitor.post(target(LOGIN, pdf), credentials), (printable,), 'Télécharger le PDF')
    _, data, mime = visitor.request(pdf)
    assert data.startswith(b'%PDF-') and mime == 'application/pdf' and visitor.last_disposition.startswith('attachment'), mime
    check_html(visitor.request(target(LOGIN, '/Sales/Create')), ('/Sales/Create',), 'Nouvelle vente')

    # Enable an authenticator on the disposable account and run both completion paths.
    authenticator = '/Identity/Account/Manage/EnableAuthenticator'
    _, html, _ = visitor.request(authenticator)
    key = re.search(r'<code>([a-z2-7 ]+)</code>', small_text(html)).group(1).replace(' ', '')
    secret = b32decode(key + '=' * (-len(key) % 8), casefold=True)

    def temporary_code():
        digest = hmac.digest(secret, struct.pack('>Q', int(time.time()) // 30), 'sha1')
        offset = digest[-1] & 15
        return f'{(int.from_bytes(digest[offset:offset + 4], "big") & 0x7fffffff) % 1000000:06d}'

    _, html, _ = visitor.post(authenticator, {'Input.Code': temporary_code()})
    recovery_code = re.search(r'<code class="recovery-code">([^<]+)</code>', small_text(html)).group(1)
    check_html(visitor.post(LOGOUT, {}, source='/Home/Index'), (LOGIN,))
    check_html(visitor.post(target(LOGIN, export), credentials), (LOGIN_2FA,), 'Double authentification')
    mfa = target(LOGIN_2FA, export)
    rejected = check_html(visitor.post(mfa, {'Input.TwoFactorCode': 'x', 'rememberMe': 'true'}), (LOGIN_2FA,), 'Double authentification')
    # Submit the returned form action, not the original URL: the retry must preserve the page destination.
    retry = re.search(r'<form[^>]*action="([^"]+)"', small_text(rejected)).group(1)
    assert '/Reports' in parse_qs(urlparse(retry).query)['returnUrl'][0]
    assert re.search(r'(?i)<input(?=[^>]*name="RememberMe")(?=[^>]*value="true")[^>]*>', small_text(rejected))
    check_html(visitor.post(retry, {'Input.TwoFactorCode': temporary_code(), 'rememberMe': 'true'}), REPORTS)
    check_html(visitor.post(LOGOUT, {}, source='/Home/Index'), (LOGIN,))
    check_html(visitor.post(target(LOGIN, pdf), credentials), (LOGIN_2FA,))
    check_html(visitor.post(target(RECOVERY, pdf), {'Input.RecoveryCode': recovery_code}), (printable,))


def verify_broadcasts(base, db_file, customer, other_company, staff, public, superadmin):
    manage = '/SuperAdmin/ManagePopups'
    save = '/SuperAdmin/SavePopup'
    withdraw = '/SuperAdmin/WithdrawPopup'
    manager = Browser(base)
    login, _, _ = manager.post(LOGIN, {'Input.Email': superadmin[0], 'Input.Password': superadmin[1]})
    assert login in ('/SuperAdmin', '/SuperAdmin/Index'), login
    assert public.request(manage)[0] == LOGIN
    assert public.post(save, {'command': 'publish'}, source='/')[0] == LOGIN
    assert public.post(f'{withdraw}/1', {}, source='/')[0] == LOGIN
    for member in (customer, other_company, staff):
        assert member.request(manage)[0].endswith('/AccessDenied')
        assert member.post(save, {'command': 'preview'}, source='/Home/Index')[0].endswith('/AccessDenied')
        assert member.post(save, {'command': 'publish'}, source='/Home/Index')[0].endswith('/AccessDenied')
        assert member.post(f'{withdraw}/1', {}, source='/Home/Index')[0].endswith('/AccessDenied')
    manager.request('/SuperAdmin/SendNotification', {'message': 'Publication interdite'}, expected=400)
    manager.request(save, {'command': 'preview'}, expected=400)
    manager.request(save, {'command': 'publish'}, expected=400)
    manager.request(f'{withdraw}/1', {}, expected=400)

    # The original notification stays plain text and never enters an anonymous response.
    system_notice = 'Note interne de recette <b>non publiée</b>'
    manager.post('/SuperAdmin/SendNotification', {'message': system_notice, 'hoursActive': '2'}, source='/SuperAdmin/Index')
    public_paths = ('/', LOGIN, '/Infos/CGU')
    for url in public_paths:
        _, body, _ = public.request(url)
        assert b'systemNotice' not in body and 'Note interne de recette' not in small_text(body), url
    for member, url in ((customer, '/Home/Index'), (staff, '/Home/Index'), (other_company, '/Infos/CGU')):
        _, body, _ = member.request(url)
        assert b'id="systemNotice"' in body and system_notice in small_text(body), url
        assert b'<b>non publi' not in body and b'id="publicPopup"' not in body, url

    title = 'Maintenance prévue <dimanche>'
    html = ('<p style="color:red" onclick="return false">Bienvenue sur <strong>AD Gestion Hub</strong>.</p>'
            '<ul><li>Consulter les informations</li></ul><p><a href="/Infos/CGU">Lire les conditions</a></p>'
            '<a href="javascript:void(0)">Lien retiré</a><script>contenu non publiable</script>')
    html += '<p>Le service reste disponible pour les opérations habituelles pendant la maintenance.</p>' * 18
    fields = {'Input.Title': title, 'Input.MessageHtml': html, 'Input.HoursActive': '2'}

    def safe_message(body):
        match = re.search(rb'<div class="(?:modal-body )?public-popup-message"[^>]*>(.*?)</div>', body, re.S)
        assert match, 'Contenu du pop-up absent'
        fragment = match[1].decode()
        assert '<strong>AD Gestion Hub</strong>' in fragment and '<ul>' in fragment
        assert 'href="/Infos/CGU"' in fragment and 'javascript:' not in fragment
        assert not re.search(r'<script|onclick=|style=|contenu non publiable', fragment, re.I), fragment
        return fragment

    path, preview, _ = manager.post(save, {**fields, 'command': 'preview'}, source=manage)
    assert path == save and b'id="popupPreview"' in preview
    assert title in small_text(preview) and b'<dimanche>' not in preview
    message = safe_message(preview)
    for invalid, error in (({'Input.HoursActive': '0'}, 'durée de 1 à 720 heures'),
                           ({'Input.MessageHtml': '<script>contenu non publiable</script>'}, 'Ajoutez du texte')):
        path, body, _ = manager.post(save, {**fields, **invalid, 'command': 'publish'}, source=manage)
        assert path == save and error in small_text(body)
    assert b'id="publicPopup"' not in public.request('/')[1]
    with sqlite3.connect(db_file) as db:
        assert db.execute('SELECT COUNT(*) FROM PublicPopups').fetchone() == (0,)

    def publish(values):
        path, _, _ = manager.post(save, {**values, 'command': 'publish'}, source=manage)
        assert path == manage, path
        with sqlite3.connect(db_file) as db:
            return db.execute('SELECT Id, Title, MessageHtml FROM PublicPopups ORDER BY Id DESC LIMIT 1').fetchone()

    initial_id, stored_title, stored_html = publish(fields)
    assert stored_title == title and stored_html == message
    for browser, url in [(public, url) for url in public_paths] + [(customer, '/Home/Index'), (staff, '/Home/Index')]:
        _, body, _ = browser.request(url)
        assert b'id="publicPopup"' in body and f'data-public-popup-version="{initial_id}:'.encode() in body
        assert title in small_text(body) and b'<dimanche>' not in body
        assert (b'id="systemNotice"' in body) == (browser is not public)
        assert safe_message(body) == message
    path, body, _ = other_company.post(LOGOUT, {}, source='/Home/Index')
    assert path == LOGIN and b'id="systemNotice"' not in body and b'id="publicPopup"' in body

    # Opening the next publication must work even after this browser dismissed the first.
    second_title = 'Fin de la maintenance — nouvelle publication'

    def replace():
        return publish({**fields, 'Input.Title': second_title, 'Input.MessageHtml': '<p>Le service est disponible.</p>'})[0]

    if os.environ.get('ADGH_SMOKE_BROWSER') == '1':
        from browser_smoke import run_popup_browser
        second_id = run_popup_browser(base, customer.cookies, title, second_title, replace)
    else:
        second_id = replace()
    body = public.request('/')[1]
    assert second_title in small_text(body) and title not in small_text(body)
    path, history, _ = manager.post(f'{withdraw}/{second_id}', {}, source=manage)
    assert path == manage and 'Retiré' in small_text(history) and 'Remplacé' in small_text(history)
    assert b'id="publicPopup"' not in public.request('/')[1]
    assert b'id="systemNotice"' in customer.request('/Home/Index')[1]

    expired_id, _, _ = publish({**fields, 'Input.Title': 'Pop-up à expirer'})
    assert b'id="publicPopup"' in public.request('/')[1]
    with sqlite3.connect(db_file) as db:
        db.execute("UPDATE PublicPopups SET ExpiresAtUtc = '2000-01-01 00:00:00+00:00' WHERE Id = ?", (expired_id,))
    assert b'id="publicPopup"' not in public.request('/')[1]
    assert b'id="publicPopup"' not in customer.request('/Home/Index')[1]
    assert 'Expiré' in small_text(manager.request(manage)[1])
    print('Diffusion : rôles et anti-CSRF, annonce privée, aperçu filtré, publication publique, remplacement, retrait et expiration validés.')


def run(base, db_file, superadmin):
    password = 'Pilote-' + uuid4().hex + '!9'
    a, b, staff, public = (Browser(base) for _ in range(4))
    path, html, _ = public.request('/')
    text = small_text(html)
    assert path == '/' and 'Accès gratuit' in text and 'Créer mon compte gratuit' in text
    assert 'Version 1 gratuite' not in text and 'V1 gratuite' not in text
    assert 'https://fonts.googleapis' not in html.decode() and 'cookieConsent' not in html.decode()
    assert '<h1 id="hero-title">Pilotez votre entreprise,' in text and 'Une vente, quatre écrans.' in text
    assert 'Bénéfices pour votre PME' in text
    assert 'non normalisés DGI' in text and 'En préparation' in text and 'données fictives' in text
    assert 'iPhone 18 Pro Max' in text and 'marketing-vector-flow' in text
    assert 'Pause de l’animation' not in text and 'marketing-device-labels' not in text
    assert 'href="/favicon.ico' in text and 'href="/images/brand/mark.png' in text
    for section in ('benefices', 'demo', 'fonctionnalites', 'tarifs'):
        assert f'id="{section}"' in text and f'href="#{section}"' in text, section
    screenshots = ('vente', 'stock', 'creances', 'tableau-de-bord', 'tableau-de-bord-mobile')
    assets = [f'/images/demo/{name}.webp' for name in screenshots]
    for asset in assets:
        assert f'href="{asset}"' in text, asset
    for theme in ('logo', 'logo-inverse'):
        asset = f'/images/brand/{theme}.webp'
        assert f'src="{asset}' in text, asset
        assets.append(asset)
    for asset in assets:
        final, image, mime = public.request(asset)
        assert final == asset and mime == 'image/webp' and not public.last_disposition, asset
        assert image[:4] == b'RIFF' and image[8:12] == b'WEBP' and len(image) < 150 * 1024, asset
    for doc in ('CGU', 'MentionsLegales', 'Confidentialite', 'Cookies'):
        path, html, _ = public.request(f'/Infos/{doc}')
        text = small_text(html)
        assert doc in path and 'Adhil Digital Solutions' in text and 'contact@adhildigital.com' in text
        assert 'RCCM' not in text and 'contact@adhil-digital.com' not in text
    assert_privacy(public)
    assert_privacy(public, PRIVACY_ACCENTED)
    assert_privacy(public, '/Home/Privacy')
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
    verify_session_navigation(base, email_a, password, sale)
    verify_broadcasts(base, db_file, a, b, staff, public, superadmin)
    print('Parcours HTTP : connexion, déconnexion, double authentification, CGU, configuration, logo, ventes, rapports et isolation réussis.')


def main():
    with socket.socket() as sock:
        sock.bind(('127.0.0.1', 0))
        port = sock.getsockname()[1]
    root = Path(__file__).resolve().parents[1]
    with tempfile.TemporaryDirectory() as tmp, tempfile.TemporaryFile(mode='w+') as log:
        host = f'http://127.0.0.1:{port}'
        superadmin = (f'super-{uuid4().hex}@example.test', f'Recette-{uuid4().hex}!9')
        env = {**os.environ, 'ASPNETCORE_ENVIRONMENT': 'Development', 'DOTNET_ENVIRONMENT': 'Development',
               'DatabaseProvider': 'Sqlite', 'ConnectionStrings__DefaultConnection': f'Data Source={tmp}/http.db',
               'SuperAdmin__Email': superadmin[0], 'SuperAdmin__Password': superadmin[1]}
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
                run(host, Path(tmp) / 'http.db', superadmin)
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
