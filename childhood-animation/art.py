"""Original, procedural 2-D artwork for the childhood story.

All coordinates are in the 960x540 composition. Assets are drawn at double
resolution before being reduced for clean outlines and exported into the film.
"""
from functools import lru_cache
import math
import random
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent
W, H = 960, 540
INK = '#29394b'
CREAM = '#fff7df'
YELLOW = '#ffd064'
CORAL = '#f47e6e'
MINT = '#71c8ba'
BLUE = '#5ca9c0'
DARK = '#28374d'


@lru_cache(maxsize=80)
def font(size, display=False):
    name = 'RussoOne-Regular.ttf' if display else 'Nunito.ttf'
    f = ImageFont.truetype(str(ROOT / 'assets' / 'fonts' / name), int(size))
    if not display:
        try:
            f.set_variation_by_name('ExtraBold')
        except OSError:
            pass
    return f


class Pen:
    """Tiny supersampled drawing API."""
    def __init__(self, w, h, bg=(0, 0, 0, 0), s=2):
        self.w, self.h, self.s = w, h, s
        self.im = Image.new('RGBA', (int(w*s), int(h*s)), bg)
        self.d = ImageDraw.Draw(self.im)

    def _box(self, box):
        return tuple(round(v * self.s) for v in box)

    def _points(self, pts):
        return [(round(x*self.s), round(y*self.s)) for x, y in pts]

    def rect(self, box, fill, outline=None, width=1):
        self.d.rectangle(self._box(box), fill=fill, outline=outline,
                         width=max(1, round(width*self.s)))

    def rr(self, box, radius, fill, outline=None, width=1):
        self.d.rounded_rectangle(self._box(box), round(radius*self.s), fill=fill,
                                 outline=outline, width=max(1, round(width*self.s)))

    def ellipse(self, box, fill, outline=None, width=1):
        self.d.ellipse(self._box(box), fill=fill, outline=outline,
                       width=max(1, round(width*self.s)))

    def poly(self, points, fill, outline=None, width=1):
        pts = self._points(points)
        self.d.polygon(pts, fill=fill)
        if outline:
            self.d.line(pts + [pts[0]], fill=outline, width=max(1, round(width*self.s)), joint='curve')

    def line(self, points, fill, width=1):
        self.d.line(self._points(points), fill=fill,
                    width=max(1, round(width*self.s)), joint='curve')

    def arc(self, box, start, end, fill, width=1):
        self.d.arc(self._box(box), start, end, fill=fill, width=max(1, round(width*self.s)))

    def text(self, xy, text, size, fill=INK, display=False, stroke_width=0, stroke_fill=None, anchor=None):
        self.d.text(self._box(xy), text, font=font(int(size*self.s), display), fill=fill,
                    stroke_width=round(stroke_width*self.s), stroke_fill=stroke_fill, anchor=anchor)

    def finish(self):
        return self.im.resize((int(self.w), int(self.h)), Image.Resampling.LANCZOS)


def _cloud(p, x, y, size=1.0, color='#fff7e8'):
    p.ellipse((x+4*size, y+20*size, x+99*size, y+50*size), color)
    p.ellipse((x+16*size, y+5*size, x+55*size, y+45*size), color)
    p.ellipse((x+39*size, y, x+83*size, y+48*size), color)
    p.ellipse((x+69*size, y+13*size, x+104*size, y+48*size), color)


def _building(p, x, y, w, h, wall, window='#c1e5dd', rows=3, cols=4):
    p.poly([(x,y+8),(x+w,y+8),(x+w,y+h),(x,y+h)], wall, INK, 2.3)
    p.poly([(x-7,y+8),(x+4,y-2),(x+w+5,y-2),(x+w+8,y+8)], '#d08d80', INK, 2)
    p.line([(x+3,y+31),(x+w-3,y+31)], '#dcaaa0', 2)
    for row in range(rows):
        for col in range(cols):
            xx=x+21+col*(w-42)/max(1,cols-1)
            yy=y+45+row*49
            if yy+30 > y+h-3:
                continue
            p.rr((xx-12,yy,xx+12,yy+32), 2, '#f6e6c7', INK, 1.7)
            p.rect((xx-8,yy+4,xx+8,yy+27), window, INK, 1.3)
            p.line([(xx,yy+4),(xx,yy+27)], '#f8f2d9', 1.5)
            p.line([(xx-8,yy+15),(xx+8,yy+15)], '#f8f2d9', 1.5)
            p.line([(xx-15,yy+33),(xx+15,yy+33)], '#a85f57', 2)


def _tree(p, x, y, scale=1.0):
    # y is the ground point
    s=scale
    p.poly([(x-15*s,y),(x-10*s,y-133*s),(x-22*s,y-169*s),
            (x-10*s,y-171*s),(x+2*s,y-145*s),(x+13*s,y-177*s),
            (x+24*s,y-173*s),(x+11*s,y-130*s),(x+17*s,y)], '#ac8465', INK, 2.4)
    p.line([(x-3*s,y-6*s),(x-3*s,y-110*s)], '#e0b191', 3*s)
    for dx,dy,r,col in [(-58,-163,42,'#6daa80'),(-29,-190,49,'#79b98b'),
                         (18,-200,51,'#8dc894'),(58,-170,43,'#71af80'),
                         (0,-158,50,'#86be88')]:
        p.ellipse((x+(dx-r)*s,y+(dy-r)*s,x+(dx+r)*s,y+(dy+r)*s),col,INK,2)
    for dx,dy in [(-64,-168),(-25,-211),(27,-218),(58,-169),(-5,-165)]:
        p.arc((x+(dx-11)*s,y+(dy-8)*s,x+(dx+12)*s,y+(dy+8)*s),185,345,'#b9dca4',2)


def _bench(p, x, y, s=1.0):
    p.line([(x+18*s,y),(x+10*s,y+43*s)], INK, 5*s)
    p.line([(x+124*s,y),(x+133*s,y+43*s)], INK, 5*s)
    p.rr((x,y-24*s,x+144*s,y-5*s),4*s,'#79b6bb',INK,2.5)
    p.rr((x+3*s,y-2*s,x+141*s,y+8*s),3*s,'#639ca8',INK,2.5)
    for dx in (9,46,84,122):
        p.line([(x+dx*s,y-20*s),(x+dx*s,y-8*s)], '#c8e4d6', 1.5*s)


def _fence(p, y=318):
    p.line([(0,y),(960,y)], '#d7b799', 4)
    for x in range(0, 980, 31):
        p.line([(x,y-14),(x,y+23)], '#e4c9a6', 3)


def _yard_base(p, variant='yard'):
    for y in range(0, 330):
        q=y/330
        col=(int(141+74*q),int(210+21*q),int(215+8*q),255)
        p.line([(0,y),(960,y)], col, 1)
    p.ellipse((803,34,880,111),'#ffe29a')
    for a in range(0,360,45):
        r=math.radians(a)
        p.line([(841+46*math.cos(r),73+46*math.sin(r)),
                (841+57*math.cos(r),73+57*math.sin(r))], '#ffe7a8', 3)
    _cloud(p,343,35,.83, '#f7f6e8')
    _cloud(p,604,88,.58, '#f7f7e9')
    _building(p,-22,113,263,216,'#e9b9a1',rows=3,cols=4)
    _building(p,706,82,285,247,'#f1c4a7',rows=4,cols=4)
    _building(p,342,177,231,152,'#f3d5b1',rows=2,cols=4)
    _fence(p,317)
    p.rect((0,326,960,540),'#d9d69e')
    p.poly([(0,412),(374,364),(626,364),(960,435),(960,540),(0,540)],'#ecd7a9')
    p.line([(0,411),(376,365),(625,365),(960,434)], '#c9bb91', 2)
    p.ellipse((735,341,908,369),'#bad09e')
    p.ellipse((30,341,187,371),'#bad09e')
    _tree(p,142,366,.83)
    _tree(p,830,369,.76)
    # path stones and tufts, fixed so the result is deterministic
    rng=random.Random(43)
    for _ in range(35):
        xx=rng.randint(5,955); yy=rng.randint(344,530)
        if rng.random()<.5:
            p.arc((xx,yy,xx+15,yy+5),185,355,'#d2bf99',1)
        else:
            p.line([(xx,yy),(xx+3,yy-5),(xx+5,yy)], '#a8bd84',1.4)


@lru_cache(maxsize=10)
def background(kind):
    p=Pen(W,H,bg='#f8e9ca')
    if kind in ('yard','playground','fort','laundry','sunset'):
        _yard_base(p,kind)
        if kind == 'playground':
            # Deliberately simple, so foreground character animation reads clearly.
            p.line([(530,346),(582,187),(635,346)], '#dc786b', 5)
            p.line([(721,346),(670,187),(617,346)], '#dc786b', 5)
            p.line([(579,187),(671,187)], '#dc786b', 6)
            p.line([(609,192),(609,269)], '#415869', 2)
            p.line([(641,192),(641,269)], '#415869', 2)
            p.rr((605,267,646,276),3,'#edbe62',INK,2)
            _bench(p,722,340,.8)
        elif kind == 'fort':
            # Patchwork roof: three solid pieces, dark entrance, box-door.
            p.ellipse((194,386,784,428),'#c9c48d')
            p.poly([(212,381),(321,194),(488,173),(669,201),(774,381)], '#f7d780',INK,4)
            p.poly([(212,381),(321,194),(408,184),(352,381)], '#ef8e7e',INK,3)
            p.poly([(408,184),(488,173),(556,183),(609,381),(352,381)], '#f6d889',INK,3)
            p.poly([(556,183),(669,201),(774,381),(609,381)], '#85c8ba',INK,3)
            p.line([(270,302),(736,302)], '#f6f0c8',5)
            p.poly([(394,381),(434,258),(531,258),(583,381)], '#526d68',INK,4)
            p.poly([(408,381),(440,273),(483,273),(483,381)], '#9fb69b',INK,2)
            p.line([(329,199),(260,379)], '#f5c7a2',2)
            p.line([(668,205),(740,379)], '#c3e1c6',2)
            p.rr((640,318,739,395),4,'#d4a06e',INK,3)
            p.poly([(640,318),(658,301),(754,302),(739,318)],'#ebc291',INK,3)
            p.ellipse((664,342,677,355),'#f5d985',INK,2)
            p.line([(649,370),(729,370)],'#af795a',2)
            p.ellipse((452,211,482,239),'#f6e6a6',INK,2)
            p.text((459,211),'★',19,INK)
        elif kind == 'laundry':
            p.line([(77,184),(895,169)],'#475667',3)
            p.line([(94,176),(94,363)],'#8b7169',4)
            p.line([(882,162),(882,364)],'#8b7169',4)
            for x,col in [(151,'#e7b9ad'),(609,'#a8d6c7'),(742,'#f1c877')]:
                p.poly([(x,183),(x+92,181),(x+98,254),(x+47,248),(x-4,255)],col,INK,2)
                p.rr((x+11,177,x+17,188),2,'#e1a277',INK,1)
                p.rr((x+74,175,x+80,187),2,'#e1a277',INK,1)
            p.ellipse((53,354,177,395),'#c6a178',INK,2)
            p.rr((61,333,170,383),9,'#d8b086',INK,3)
            for xx in range(70,170,18):
                p.line([(xx,338),(xx+6,379)], '#ae855f',2)
        elif kind == 'sunset':
            # Warm framing confetti, with the common courtyard underneath.
            p.ellipse((320,60,640,360), '#fbe1a1')
            p.ellipse((338,78,622,342), '#f5d295')
        else:
            _bench(p,633,349,.87)
            p.line([(330,335),(359,223),(391,335)],'#d68170',4)
            p.line([(360,223),(431,223)],'#d68170',5)
            p.line([(406,224),(406,281)],INK,2)
            p.rr((390,280,423,288),3,'#f2c974',INK,2)
    elif kind == 'kitchen':
        p.rect((0,0,960,540),'#f6e3b8')
        for x in range(0,960,72):
            p.line([(x,0),(x,341)],'#f2d8aa',3)
        for x in range(20,960,88):
            for y in range(27,315,78):
                p.ellipse((x,y,x+6,y+6),'#e7bb9e')
                p.ellipse((x+8,y+4,x+12,y+8),'#f0c995')
        p.rect((0,337,960,540),'#d7b595')
        p.line([(0,337),(960,337)],INK,4)
        for x in range(-200,1000,110):
            p.line([(480,338),(x,540)],'#c79f81',2)
        p.rr((61,78,359,295),5,'#f4d5aa',INK,4)
        p.rr((76,91,344,280),3,'#a9dadd',INK,3)
        p.rect((210,91,218,280),'#f9f0d8')
        p.rect((76,177,344,184),'#f9f0d8')
        _cloud(p,100,109,.48,'#f6f6e7')
        p.poly([(76,91),(138,91),(133,238),(105,224),(76,246)],'#f4a48e',INK,2)
        p.poly([(284,91),(344,91),(344,244),(319,225),(291,237)],'#f4a48e',INK,2)
        for x in (88,105,122,300,317,334):
            p.line([(x,99),(x-4,218)],'#f8d1b6',2)
        p.rr((63,277,357,291),4,'#f8f1d8',INK,2)
        p.rr((384,72,586,245),4,'#99cfc1',INK,3)
        p.line([(485,74),(485,243)],INK,2)
        p.ellipse((469,147,476,154),INK)
        p.ellipse((494,147,501,154),INK)
        p.rr((610,248,891,294),9,'#f0c18c',INK,3)
        p.line([(646,289),(626,416)],INK,7)
        p.line([(853,289),(875,416)],INK,7)
        p.ellipse((717,224,836,253),'#fff5db',INK,2)
        p.rr((778,170,814,227),4,'#c96568',INK,2)
        p.rr((775,166,817,178),3,'#f3d993',INK,2)
        p.rr((640,180,698,235),7,'#e9ad80',INK,2)
        p.arc((648,191,690,225),0,180,'#c68061',2)
    elif kind == 'fantasy':
        p.rect((0,0,960,540),'#667ba7')
        p.ellipse((639,-80,1069,350),'#8494ba')
        p.ellipse((665,-51,1038,319),'#a3b0ca')
        p.poly([(0,366),(196,320),(354,375),(547,330),(745,376),(960,310),(960,540),(0,540)],'#79979e')
        p.poly([(0,423),(245,380),(421,425),(646,390),(960,433),(960,540),(0,540)],'#e8c79c')
        for x,y,r in [(79,85,5),(170,150,3),(274,71,4),(422,128,6),(558,55,4),(780,83,4),(852,197,5),(531,224,3)]:
            p.line([(x-r*2,y),(x+r*2,y)],'#ffe6a0',2)
            p.line([(x,y-r*2),(x,y+r*2)],'#ffe6a0',2)
            p.ellipse((x-r,y-r,x+r,y+r),'#fff3be')
        _cloud(p,284,204,.8,'#b6bfd0')
    return p.finish().convert('RGB')


def _limb(p, pts, skin, width=17):
    p.line(pts, INK, width+6)
    p.line(pts, skin, width)
    x,y=pts[-1]
    p.ellipse((x-width*.54,y-width*.54,x+width*.54,y+width*.54),skin,INK,2.2)


@lru_cache(maxsize=256)
def boy(kind='hero', pose='relax', emotion='happy', mouth=0, blink=False):
    """Two recurring original characters, with switchable arms and expressions."""
    p=Pen(240,340)
    ishero=kind=='hero'
    skin='#ffd0a8' if ishero else '#ffcca1'
    hair='#384356' if ishero else '#c7774e'
    shirt='#f5ba54' if ishero else '#75c4b7'
    shorts='#547ca1' if ishero else '#666e9a'
    shoe='#e77868' if ishero else '#eaa15c'
    # Soft contact shadow.
    p.ellipse((48,313,196,330),(50,61,65,35))
    step=8 if pose=='walk' else (-8 if pose=='walk2' else 0)
    _limb(p,[(92,245),(90-step,286),(86-step,298)],skin,17)
    _limb(p,[(144,245),(146+step,286),(151+step,298)],skin,17)
    p.rr((57-step,290,105-step,311),9,shoe,INK,3)
    p.rr((134+step,290,182+step,311),9,shoe,INK,3)
    p.line([(62-step,304),(100-step,304)],'#fff2d8',2)
    p.line([(139+step,304),(177+step,304)],'#fff2d8',2)
    armleft=[(76,174),(55,192),(52,218)]
    armright=[(159,174),(180,192),(185,216)]
    if pose=='point':
        armright=[(160,173),(184,169),(213,157)]
    elif pose=='wave':
        armright=[(159,172),(187,150),(185,121)]
    elif pose=='cheer':
        armleft=[(76,173),(50,150),(46,123)]
        armright=[(159,173),(185,148),(190,120)]
    elif pose=='scared':
        armleft=[(75,174),(49,172),(55,145)]
        armright=[(160,174),(187,172),(180,145)]
    elif pose in ('walk','walk2'):
        armleft=[(76,174),(57,193),(65,213)] if pose=='walk' else [(76,174),(58,194),(48,213)]
        armright=[(159,174),(181,194),(174,214)] if pose=='walk' else [(159,174),(179,194),(191,213)]
    elif pose=='hide':
        armleft=[(76,174),(54,191),(72,205)]
        armright=[(159,174),(180,191),(160,205)]
    _limb(p,armleft,skin,17)
    _limb(p,armright,skin,17)
    p.poly([(72,212),(163,212),(160,261),(121,260),(118,246),(115,260),(75,260)],shorts,INK,3)
    p.line([(119,224),(119,249)],'#355b80',2)
    p.poly([(83,157),(151,157),(171,174),(162,226),(74,226),(65,174)],shirt,INK,3.5)
    p.line([(88,161),(97,175),(139,175),(148,161)], '#ffdf9c' if ishero else '#bcf0d9',3)
    p.ellipse((106,187,126,207),'#f9e7ac' if ishero else '#d3f1dd',INK,1.5)
    p.line([(111,197),(121,197)],INK,1.5)
    # Ears and large rounded head.
    p.ellipse((40,91,64,119),skin,INK,3)
    p.ellipse((177,91,200,119),skin,INK,3)
    p.ellipse((48,43,191,166),skin,INK,3.5)
    p.poly([(50,85),(53,67),(63,51),(78,43),(96,41),(108,35),(127,42),
            (143,38),(163,44),(179,57),(189,79),(180,86),(171,76),
            (159,83),(151,70),(139,81),(126,68),(112,80),(97,70),
            (84,81),(72,72),(61,88)],hair,INK,2.7)
    if not ishero:
        p.poly([(53,68),(64,48),(85,38),(126,36),(158,43),(176,58),(177,68)],'#71a87b',INK,3)
        p.poly([(92,65),(184,62),(198,70),(190,76),(128,75),(92,72)],'#568d6d',INK,2.5)
        p.ellipse((125,45,139,58),'#f3d08b',INK,1.5)
    # Small cheek marks.
    p.ellipse((62,119,75,126),'#f4a794')
    p.ellipse((161,119,174,126),'#f4a794')
    brow_y=89 if emotion!='worried' else 86
    if emotion=='worried':
        p.line([(78,brow_y+3),(94,brow_y)],INK,3)
        p.line([(136,brow_y),(152,brow_y+3)],INK,3)
    else:
        p.line([(78,brow_y),(94,brow_y-2)],INK,3)
        p.line([(136,brow_y-2),(151,brow_y)],INK,3)
    if blink:
        p.line([(78,108),(95,109)],INK,3.2)
        p.line([(135,109),(152,108)],INK,3.2)
    else:
        p.ellipse((76,96,96,117),'#fffdf2',INK,2)
        p.ellipse((134,96,154,117),'#fffdf2',INK,2)
        eyeoff=2 if emotion=='sly' else 0
        p.ellipse((86+eyeoff,102,92+eyeoff,112),INK)
        p.ellipse((144+eyeoff,102,150+eyeoff,112),INK)
    p.arc((110,109,123,122),5,165,'#b87d68',1.5)
    if emotion=='shocked':
        p.ellipse((109,127,125,146),'#553b40',INK,2)
        p.ellipse((114,139,122,144),'#ef9b91')
    elif mouth==2:
        p.ellipse((105,127,130,145),'#5b3e45',INK,2)
        p.ellipse((110,137,125,143),'#ee9f94')
    elif mouth==1:
        p.ellipse((108,128,127,140),'#69434a',INK,2)
    elif emotion=='worried':
        p.arc((107,133,130,148),190,345,INK,2.5)
    else:
        p.arc((106,122,132,141),10,165,INK,2.5)
    return p.finish()


@lru_cache(maxsize=64)
def grownup(kind='grandma', pose='relax', emotion='happy', mouth=0, blink=False):
    p=Pen(240,360)
    grandma=kind=='grandma'
    skin='#ffd0ad'
    dress='#b49bcb' if grandma else '#e89775'
    hair='#a9a5a4' if grandma else '#6f5c59'
    p.ellipse((42,332,200,347),(50,61,65,35))
    p.rr((73,308,111,330),8,'#785b67',INK,3)
    p.rr((135,308,174,330),8,'#785b67',INK,3)
    left=[(76,182),(53,204),(50,231)]
    right=[(163,182),(185,203),(187,230)]
    if pose in ('wave','point','offer'):
        right=[(163,182),(186,166),(207,160)]
    if pose=='cheer':
        left=[(76,182),(50,161),(45,139)]
        right=[(163,182),(190,158),(196,138)]
    _limb(p,left,skin,18); _limb(p,right,skin,18)
    p.poly([(85,163),(151,163),(169,183),(184,310),(58,310),(72,183)],dress,INK,3.5)
    p.poly([(95,181),(143,181),(153,271),(146,290),(92,290),(84,271)],'#f8ecd5',INK,2)
    p.rr((101,240,138,265),4,'#eed5c6',INK,1.5)
    p.line([(84,182),(69,168)],'#f8ecd5',5)
    p.line([(151,182),(167,168)],'#f8ecd5',5)
    if grandma:
        p.ellipse((86,23,151,81),hair,INK,3)
    else:
        p.ellipse((48,47,193,170),'#c98a85',INK,3)
    p.ellipse((47,96,69,122),skin,INK,3)
    p.ellipse((173,96,195,122),skin,INK,3)
    p.ellipse((52,52,190,171),skin,INK,3.5)
    if grandma:
        p.poly([(54,86),(57,65),(74,52),(93,49),(111,54),(130,46),(149,50),(168,56),(186,76),(186,88),(174,79),(157,85),(143,73),(122,83),(101,72),(82,82),(68,77)],hair,INK,2.5)
    else:
        p.poly([(54,84),(55,66),(68,52),(94,42),(129,43),(160,49),(180,67),(188,86),(172,79),(161,70),(145,79),(132,68),(114,78),(98,68),(81,79),(69,75)],'#bb7b7d',INK,3)
        p.poly([(174,81),(198,121),(189,149),(175,126)],'#bb7b7d',INK,2)
    p.ellipse((68,119,81,126),'#eda895'); p.ellipse((160,119,173,126),'#eda895')
    if blink:
        p.line([(78,106),(93,106)],INK,3); p.line([(142,106),(157,106)],INK,3)
    else:
        p.ellipse((78,100,94,116),'#fffdf0',INK,1.5); p.ellipse((140,100,156,116),'#fffdf0',INK,1.5)
        p.ellipse((86,103,91,112),INK); p.ellipse((148,103,153,112),INK)
    if grandma:
        p.ellipse((71,95,102,122),None,INK,2.3); p.ellipse((133,95,164,122),None,INK,2.3)
        p.line([(102,105),(133,105)],INK,2)
    else:
        p.line([(78,91),(94,89)],INK,3); p.line([(140,89),(155,91)],INK,3)
    if mouth:
        p.ellipse((109,132,128,144),'#744c50',INK,1.5)
    else:
        p.arc((106,126,133,143),10,170,INK,2.5)
    p.arc((109,110,124,123),5,165,'#c28c76',1.5)
    return p.finish()


@lru_cache(maxsize=8)
def cat(blink=False, carrying=False):
    p=Pen(178,126)
    p.ellipse((10,108,168,119),(50,61,65,35))
    p.line([(132,91),(158,77),(160,51),(151,41)],INK,15)
    p.line([(132,91),(158,77),(160,51),(151,41)],'#eaa568',10)
    p.ellipse((37,50,144,105),'#eaa568',INK,3)
    p.ellipse((52,78,82,111),'#eaa568',INK,3)
    p.ellipse((105,78,135,111),'#eaa568',INK,3)
    p.poly([(41,58),(45,17),(66,30),(79,22),(97,32),(117,19),(121,61)],'#eaa568',INK,3)
    p.poly([(49,28),(51,45),(63,36)],'#f3c0aa')
    p.poly([(111,29),(107,45),(99,36)],'#f3c0aa')
    p.ellipse((39,32,120,95),'#efb377',INK,3)
    p.poly([(62,34),(68,48),(72,34)],'#c77a55')
    p.poly([(87,34),(92,48),(96,34)],'#c77a55')
    if blink:
        p.line([(58,60),(71,61)],INK,2.5); p.line([(91,61),(104,60)],INK,2.5)
    else:
        p.ellipse((56,52,73,67),'#fff5dd',INK,1.5); p.ellipse((90,52,107,67),'#fff5dd',INK,1.5)
        p.ellipse((64,56,68,65),INK); p.ellipse((98,56,102,65),INK)
    p.poly([(76,69),(84,69),(80,74)],'#cf8275',INK,1.3)
    p.arc((72,72,80,81),0,155,INK,1.5); p.arc((80,72,88,81),10,170,INK,1.5)
    for yy in (72,78):
        p.line([(39,yy),(61,yy+2)],INK,1.3); p.line([(100,yy+2),(122,yy)],INK,1.3)
    if carrying:
        p.poly([(72,81),(112,73),(121,92),(78,97)],'#f5d497',INK,2)
        p.line([(82,86),(111,81)],'#c15d61',4)
    return p.finish()


@lru_cache(maxsize=30)
def prop(kind):
    if kind=='box':
        p=Pen(205,155)
        p.ellipse((12,134,197,151),(50,61,65,35))
        p.poly([(26,43),(173,43),(178,136),(21,136)],'#d6a36f',INK,3)
        p.poly([(26,43),(16,20),(80,13),(103,43)],'#e2b985',INK,3)
        p.poly([(103,43),(123,13),(185,20),(173,43)],'#edc796',INK,3)
        p.line([(103,44),(103,133)],'#b87955',2)
        p.rr((51,73,146,111),4,'#fae7bd',INK,2)
        p.text((61,78),'ШТАБ',22,INK,True)
    elif kind=='sandwich':
        p=Pen(95,74)
        p.poly([(11,54),(45,9),(84,54),(77,63),(17,63)],'#f4d298',INK,3)
        p.poly([(21,50),(46,19),(75,50)],'#fff0c8')
        p.line([(29,42),(38,38),(46,42),(55,36),(65,42)],'#bc5961',5)
        p.ellipse((40,30,46,36),'#e78479')
    elif kind=='cup':
        p=Pen(88,92)
        p.ellipse((47,20,79,59),None,INK,6)
        p.ellipse((47,20,79,59),None,'#b5cdd0',3)
        p.rr((10,18,58,70),7,'#d3e2dd',INK,3)
        p.ellipse((10,13,58,26),'#eef3e5',INK,3)
        p.line([(18,31),(18,59)],'#f7fff5',3)
    elif kind=='blanket':
        p=Pen(224,115)
        p.poly([(10,18),(63,6),(111,15),(166,6),(213,16),(206,93),(155,104),(106,94),(57,103),(13,93)],'#f4cc7d',INK,3)
        p.poly([(13,18),(63,6),(64,102),(13,93)],'#ed9688',INK,2)
        p.poly([(111,15),(166,6),(155,104),(106,94)],'#81c8b8',INK,2)
        p.line([(12,56),(207,56)],'#fae9bf',5)
    elif kind=='rocket':
        p=Pen(200,270)
        p.poly([(66,201),(32,220),(39,168),(68,152)],'#ed8d7b',INK,3)
        p.poly([(132,201),(168,220),(161,168),(132,152)],'#ed8d7b',INK,3)
        p.poly([(77,213),(100,254),(120,213)],'#ffc361',INK,3)
        p.poly([(66,208),(57,136),(62,81),(100,18),(138,81),(143,136),(134,208)],'#f6e4c0',INK,3)
        p.poly([(65,84),(100,18),(135,84)],'#ec897b',INK,3)
        p.ellipse((77,93,124,140),'#86c9d1',INK,3)
        p.arc((84,100,117,131),190,270,'#def6eb',3)
        p.rr((75,190,125,212),4,'#9dbdb5',INK,2)
    elif kind=='icecream':
        p=Pen(235,240)
        p.rr((28,76,207,216),5,'#f1be8c',INK,3)
        p.poly([(18,77),(43,22),(193,22),(217,77)],'#e88978',INK,3)
        p.poly([(54,22),(84,22),(73,77),(38,77)],'#f9e6b9',INK,2)
        p.poly([(116,22),(147,22),(158,77),(124,77)],'#f9e6b9',INK,2)
        p.rr((46,98,188,143),4,'#fff1d4',INK,2)
        p.text((55,106),'МОРОЖЕНОЕ',13,INK,True)
        p.poly([(92,189),(119,224),(147,189)],'#d9a674',INK,2)
        p.ellipse((87,162,150,198),'#f7e8cc',INK,2)
        p.ellipse((97,148,137,186),'#eeada4',INK,2)
    elif kind=='castle':
        p=Pen(246,235)
        p.poly([(28,214),(28,83),(50,83),(50,60),(76,60),(76,83),(97,83),(97,43),(121,16),(145,43),(145,83),(166,83),(166,60),(192,60),(192,83),(218,83),(218,214)],'#f7d5aa',INK,3)
        p.poly([(91,43),(121,10),(151,43)],'#e79282',INK,3)
        p.rr((100,152,143,214),19,'#98b2b0',INK,3)
        for x in (53,170): p.rr((x,111,x+22,145),9,'#aad5d3',INK,2)
        p.poly([(124,11),(124,0),(151,6),(124,13)],'#ffd977',INK,2)
    elif kind=='clock':
        p=Pen(150,150)
        p.ellipse((6,6,144,144),'#f7e7c1',INK,4)
        p.ellipse((20,20,130,130),'#fffaf0',INK,2)
        for a in range(0,360,30):
            r=math.radians(a)
            p.line([(75+44*math.cos(r),75+44*math.sin(r)),(75+50*math.cos(r),75+50*math.sin(r))],INK,2)
        p.line([(75,75),(75,39)],INK,4); p.line([(75,75),(99,87)],INK,4)
        p.ellipse((71,71,79,79),INK)
    elif kind=='board':
        p=Pen(265,250)
        p.rr((10,10,255,240),12,'#e7bc84',INK,4)
        p.rr((24,25,241,226),5,'#fff4d9',INK,2)
        p.rr((89,4,176,32),6,'#8baab0',INK,2)
        p.text((48,57),'ПРАВИЛА',25,INK,True)
        for i,tx in enumerate(['1. БЕЗ ВЗРОСЛЫХ','2. ПАРОЛЬ!','3. ПОЧТАЛЬОН']):
            p.text((43,105+i*35),tx,16,INK,True)
    else:
        raise KeyError(kind)
    return p.finish()


def paste_scaled(dst, asset, cx, bottom, height, angle=0):
    w=max(1,round(asset.width*height/asset.height))
    img=asset.resize((w,round(height)),Image.Resampling.LANCZOS)
    if angle:
        img=img.rotate(angle, resample=Image.Resampling.BICUBIC, expand=True)
    dst.paste(img,(round(cx-img.width/2),round(bottom-img.height)),img)


def speech_tag(dst, text, x, y, color='#fff8e7', size=23):
    """Sticker-like callout with a small offset shadow."""
    d=ImageDraw.Draw(dst)
    f=font(size,True)
    lines=text.split('\n')
    widths=[d.textlength(line,font=f) for line in lines]
    w=int(max(widths,default=0)+28); h=int(len(lines)*(size+5)+18)
    d.rounded_rectangle((x+4,y+5,x+w+4,y+h+5),radius=11,fill='#b88972')
    d.rounded_rectangle((x,y,x+w,y+h),radius=11,fill=color,outline=INK,width=3)
    for i,line in enumerate(lines):
        d.text((x+14,y+7+i*(size+5)),line,font=f,fill=INK)


def sparkle(dst, x, y, radius, color='#fff5bf'):
    d=ImageDraw.Draw(dst)
    d.polygon([(x,y-radius),(x+radius*.23,y-radius*.23),(x+radius,y),
               (x+radius*.23,y+radius*.23),(x,y+radius),
               (x-radius*.23,y+radius*.23),(x-radius,y),
               (x-radius*.23,y-radius*.23)],fill=color,outline=INK)
