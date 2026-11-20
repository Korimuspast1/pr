# Встраивает шрифт www/fonts/pixel.woff в www/font.css (data: URI).
import base64, os
root = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'www')
b = open(os.path.join(root, 'fonts', 'pixel.woff'), 'rb').read()
css = "/* Шрифт игры встроен в файл стилей: так он загружается одинаково в любом окружении.\n   Создано tools/gen_font_css.py из www/fonts/pixel.woff — не править вручную. */\n@font-face { font-family: PixelRU; src: url(data:font/woff;base64,%s) format('woff'); font-display: block; }\n" % base64.b64encode(b).decode()
open(os.path.join(root, 'font.css'), 'w', encoding='utf8').write(css)
print('font.css:', len(css), 'байт')
