#!/usr/bin/env python3
# Печатает тело функции(й) верхнего уровня: python3 tools/fn.py www/js/game.js name1 name2 ...
import re, sys, textwrap
src = open(sys.argv[1], encoding='utf8').read().split('\n')
starts = [i for i, l in enumerate(src) if re.match(r'^(async )?(function|const|let|var|class) |^/\* =====|^// ', l)]
for name in sys.argv[2:]:
    found = False
    for k, i in enumerate(starts):
        if re.match(r'^(async )?(function|const|let|var) ' + re.escape(name) + r'\b', src[i]):
            j = starts[k + 1] if k + 1 < len(starts) else len(src)
            body = '\n'.join(src[i:j]).rstrip()
            print('\n'.join(textwrap.wrap(body, 380, replace_whitespace=False, drop_whitespace=False, break_long_words=False)) if False else '\n'.join(l if len(l) < 380 else '\n    '.join(textwrap.wrap(l, 380, break_long_words=False, replace_whitespace=False)) for l in body.split('\n')))
            found = True
    if not found: print('?? not found:', name)
