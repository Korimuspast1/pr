# Склеивает снимки экрана в листы по N штук, чтобы просматривать их пачкой.
import sys, glob
from PIL import Image
pat, out, per = sys.argv[1], sys.argv[2], int(sys.argv[3]) if len(sys.argv) > 3 else 4
files = sorted(glob.glob(pat))
for i in range(0, len(files), per):
    ims = [Image.open(f).convert('RGB') for f in files[i:i + per]]
    ims = [im.resize((im.width // 2, im.height // 2), Image.LANCZOS) for im in ims]
    w = sum(im.width for im in ims) + 6 * (len(ims) - 1); h = max(im.height for im in ims)
    sh = Image.new('RGB', (w, h), (255, 0, 255)); x = 0
    for im in ims: sh.paste(im, (x, 0)); x += im.width + 6
    sh.save('%s_%02d.png' % (out, i // per + 1))
    print('%s_%02d.png' % (out, i // per + 1), [f.split('/')[-1] for f in files[i:i + per]])
