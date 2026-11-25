"""PROTOTYPE (#254), throwaway: the five looks' tokens, checked against WCAG AA, written as CSS to stdout."""
import sys

STATUS = {
    "light": dict(success="#1d6a40", success_muted="#dcf2e5", warning="#835000", warning_muted="#fbefd3", danger="#b42318", danger_hover="#931d14", danger_muted="#fde7e4", info="#1d4f91", info_muted="#e1ebf8"),
    "dark": dict(success="#7fd8a2", success_muted="#153122", warning="#f2bf62", warning_muted="#36280c", danger="#ff9385", danger_hover="#ffb0a6", danger_muted="#3d1713", info="#91bcff", info_muted="#172a45"),
}
K = "bg surface surface_muted fg fg_muted border border_strong accent accent_hover accent_fg accent_muted".split()
def p(values, **extra):
    d = dict(zip(K, values.split())); d.update(extra); return d

LOOKS = {
  "court": {
    "light": p("#e6f0fa #ffffff #d9e7f5 #0b1f3a #43587a #b9cfe6 #5b7aa1 #0b57a8 #094a8f #ffffff #d8e8fb", btn="#dff04a", btn_fg="#0b1f3a", head="#0b57a8", head2="#0b57a8", head_fg="#ffffff", head_muted="#dbe9f9", brand="#dff04a"),
    "dark": p("#071426 #0f2139 #18314f #eaf2fc #a9bfdb #274569 #6f8fb7 #8cc2ff #add4ff #071426 #123459", btn="#dff04a", btn_fg="#0b1f3a", head="#0a3a73", head2="#0a3a73", head_fg="#ffffff", head_muted="#c5daf3", brand="#dff04a"),
  },
  "sticker": {
    "light": p("#ffdc5c #ffffff #fff0b8 #1d1a2b #4b4660 #1d1a2b #1d1a2b #b01e3c #8f1730 #ffffff #ffe1e0", btn="#ff7666", btn_fg="#1d1a2b", head="#ff7666", head2="#ff7666", head_fg="#1d1a2b", head_muted="#1d1a2b", brand="#1d1a2b"),
    "dark": p("#1b1729 #2a2540 #3a3456 #fff6da #cfc6e3 #fff6da #fff6da #ff9d90 #ffb9b0 #1d1a2b #4a2238", btn="#ff7666", btn_fg="#1d1a2b", head="#ff7666", head2="#ff7666", head_fg="#1d1a2b", head_muted="#1d1a2b", brand="#1d1a2b"),
  },
  "berry": {
    "light": p("#eef8e6 #ffffff #dff0d2 #1f2b1a #4a5d43 #cbe2ba #64835a #b81840 #981234 #ffffff #ffe1e8", btn="#c81e45", btn_fg="#ffffff", head="#1c7432", head2="#207c37", head_fg="#ffffff", head_muted="#eaf7e6", brand="#ffffff"),
    "dark": p("#0f1a10 #17251a #213526 #eef8e8 #abc4a2 #2e4a33 #6f9172 #ff93a9 #ffb0c0 #2d0711 #451a27", btn="#ff93a9", btn_fg="#2d0711", head="#174d25", head2="#1c5a2c", head_fg="#ffffff", head_muted="#d7ecd2", brand="#ffffff"),
  },
  "seventies": {
    "light": p("#dff2ee #ffffff #cfe9e4 #2b1a3a #55456a #b3d9d2 #557f78 #0a6360 #084f4c #ffffff #c9ece8", btn="#f58a2a", btn_fg="#2b1a3a", head="#3b2150", head2="#3b2150", head_fg="#ffffff", head_muted="#e6d8f0", brand="#f7bd2b"),
    "dark": p("#1a1023 #281a35 #39274a #fdf3e0 #cdbbd8 #4b355f #8f77a5 #6fd6cd #93e3dc #0d2b29 #17443f", btn="#f58a2a", btn_fg="#2b1a3a", head="#3b2150", head2="#3b2150", head_fg="#ffffff", head_muted="#e6d8f0", brand="#f7bd2b"),
  },
  "board": {
    "light": p("#dbe8ef #ffffff #cfdfe8 #10241b #42554b #b5cad4 #587080 #0c5a3a #094a2f #ffffff #cfe9da", btn="#ffd23f", btn_fg="#10241b", head="#0c3323", head2="#0c3323", head_fg="#ffffff", head_muted="#cfe3d8", brand="#ffd23f"),
    "dark": p("#0a1410 #12261c #1b3628 #eef7f1 #a8c2b4 #2a4a3a #6d917f #8fe0b4 #b0ebcb #0a1410 #173a2a", btn="#ffd23f", btn_fg="#10241b", head="#0c3323", head2="#0c3323", head_fg="#ffffff", head_muted="#cfe3d8", brand="#ffd23f"),
    # Its cards are the painted board, in both themes: their own tokens, with the dark theme's status colours.
    "card": {
      "light": p("#124a33 #124a33 #0c3a27 #ffffff #c6dccf #2f6b50 #8fb8a3 #ffd23f #ffe07a #10241b #37501a", btn="#ffd23f", btn_fg="#10241b"),
      "dark": p("#10392a #10392a #0b2a1e #ffffff #c0d6ca #2a5f48 #86ae9a #ffd23f #ffe07a #10241b #33481a", btn="#ffd23f", btn_fg="#10241b"),
    },
  },
}

def lum(h):
    c = [int(h[i:i+2], 16)/255 for i in (1, 3, 5)]
    c = [x/12.92 if x <= .03928 else ((x+.055)/1.055)**2.4 for x in c]
    return .2126*c[0]+.7152*c[1]+.0722*c[2]
def cr(a, b):
    x, y = sorted((lum(a), lum(b)), reverse=True); return (x+.05)/(y+.05)

bad = []
def check(name, d, st):
    low = {}
    def need(fg, bg, n, label):
        r = cr(fg, bg); low[label] = min(low.get(label, 99), r)
        if r < n: bad.append(f"{name}: {label} {fg} on {bg} = {r:.2f} < {n}")
    for b in ("bg", "surface", "surface_muted", "accent_muted"):
        need(d["fg"], d[b], 4.5, "fg"); need(d["fg_muted"], d[b], 4.5, "fg-muted")
    for b in ("bg", "surface", "accent_muted"): need(d["accent"], d[b], 4.5, "accent")
    need(d["btn_fg"], d["btn"], 4.5, "button"); need(d["accent_fg"], d["accent"], 4.5, "accent-fg")
    for b in ("bg", "surface"): need(d["border_strong"], d[b], 3, "border-strong")
    for s in ("success", "warning", "danger", "info"):
        need(st[s], st[s+"_muted"], 4.5, s); need(st[s], d["surface"], 4.5, s+" on surface")
    need(st["danger"], d["bg"], 4.5, "danger on bg")
    if "head" in d:
        for h in ("head", "head2"):
            need(d["head_fg"], d[h], 4.5, "header"); need(d["head_muted"], d[h], 4.5, "header muted"); need(d["brand"], d[h], 4.5, "brand")
    print(f"{name:18}", " ".join(f"{k} {v:.1f}" for k, v in low.items()), file=sys.stderr)

css = lambda d, keys: "".join(f"--st-{k.replace('_','-')}:{d[k]};" for k in keys)
out = []
for look, modes in LOOKS.items():
    for mode in ("light", "dark"):
        d, st = modes[mode], STATUS[mode]
        check(f"{look} {mode}", d, st)
        rules = [f'html[data-look="{look}"], html[data-look="{look}"] dialog {{ {css(d, K)}{css(st, st)}--btn:{d["btn"]};--btn-fg:{d["btn_fg"]};--head:{d["head"]};--head2:{d["head2"]};--brand:{d["brand"]}; }}',
                 f'html[data-look="{look}"] header.sticky {{ --st-surface:{d["head"]};--st-fg:{d["head_fg"]};--st-fg-muted:{d["head_muted"]};--st-accent:{d["brand"]};--st-accent-muted:{"rgb(0 0 0 / .25)" if d["head_fg"] == "#ffffff" else "rgb(255 255 255 / .3)"};--st-surface-muted:{"rgb(0 0 0 / .15)" if d["head_fg"] == "#ffffff" else "rgb(255 255 255 / .2)"}; }}',
                 f'@media (max-width: 767.98px) {{ html[data-look="{look}"] nav[aria-label="Main"] {{ {css(d, K)} }} }}']
        if "card" in modes:
            c = modes["card"][mode]; check(f"{look} card {mode}", c, STATUS["dark"])
            rules.append(f'html[data-look="{look}"] :is(main section.rounded-3xl, main form > div.rounded-2xl.border) {{ {css(c, K)}{css(STATUS["dark"], STATUS["dark"])} color:var(--st-fg); }}')
        block = "\n".join(rules)
        out.append(block if mode == "light" else "@media (prefers-color-scheme: dark) {\n" + block + "\n}")
print("\n".join(bad) or "all pairs pass", file=sys.stderr)
print("\n".join(out))
sys.exit(bool(bad))
