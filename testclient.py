import tkinter as tk
from tkinter import ttk, scrolledtext, messagebox
import websocket, requests, json, threading, time, math

# ── Config ─────────────────────────────────────────────────────────────────
BASE_URL = "localhost:9000"
HTTP_URL = f"http://{BASE_URL}"
WS_URL   = f"ws://{BASE_URL}"
RECORD_SEPARATOR = chr(30)
USE_NGROK = False
NGROK_HEADERS = {"ngrok-skip-browser-warning": "true"}

# ── Palette ────────────────────────────────────────────────────────────────
BG        = "#080c09"
BG2       = "#0f130f"
BG3       = "#161c16"
BG4       = "#1d261d"
BORDER    = "#1e2a1e"
BORDER2   = "#2a3d2a"
ACCENT    = "#3d7a50"
ACCENT2   = "#52a86a"
ACCENT3   = "#6dd68a"
TEXT      = "#a8bfa8"
TEXT_DIM  = "#4a5e4a"
TEXT_BRT  = "#d4ead4"
DANGER    = "#7a2a2a"
DANGER2   = "#c04040"
GOLD      = "#8a6a28"
GOLD2     = "#c09840"
HEX_EMPTY = "#111811"
HEX_WALL  = "#080c09"
HEX_EXIT  = "#0e1a26"
HEX_OCC   = "#200e0e"
HEX_HOVER = "#1a2e1a"
HEX_PLAYER= "#2a5c3a"
FONT_MONO = ("Courier New", 10)
FONT_SM   = ("Courier New", 9)
FONT_LG   = ("Courier New", 11, "bold")
FONT_DISP = ("Courier New", 26, "bold")


# ══════════════════════════════════════════════════════════════════════════════
# ChatTab
# ══════════════════════════════════════════════════════════════════════════════

class ChatTab:
    def __init__(self, notebook, group_name, get_ws):
        self.group_name = group_name
        self.get_ws = get_ws          # callable -> ws or None

        self.frame = tk.Frame(notebook, bg=BG2)
        notebook.add(self.frame, text=self._short_name())

        self.log = scrolledtext.ScrolledText(
            self.frame, bg=BG2, fg=TEXT, font=FONT_SM,
            insertbackground=ACCENT2, wrap=tk.WORD,
            state=tk.DISABLED, relief=tk.FLAT, bd=0,
            selectbackground=ACCENT, selectforeground=TEXT_BRT,
        )
        self.log.pack(fill=tk.BOTH, expand=True, padx=0, pady=0)
        self.log.tag_config("sys",    foreground=TEXT_DIM, font=("Courier New", 9, "italic"))
        self.log.tag_config("normal", foreground=TEXT)
        self.log.tag_config("server", foreground=GOLD2)
        self.log.tag_config("me",     foreground=ACCENT3)
        self.log.tag_config("ts",     foreground=TEXT_DIM, font=("Courier New", 8))

        sep = tk.Frame(self.frame, bg=BORDER2, height=1)
        sep.pack(fill=tk.X)

        entry_row = tk.Frame(self.frame, bg=BG3)
        entry_row.pack(fill=tk.X)
        tk.Label(entry_row, text=">", fg=ACCENT2, bg=BG3,
                 font=("Courier New", 13)).pack(side=tk.LEFT, padx=(8, 4), pady=6)
        self.entry = tk.Entry(
            entry_row, bg=BG3, fg=TEXT_BRT, font=FONT_MONO,
            insertbackground=ACCENT2, relief=tk.FLAT, bd=0,
        )
        self.entry.pack(side=tk.LEFT, fill=tk.X, expand=True, pady=6)
        self.entry.bind("<Return>", self._send)

    def _short_name(self):
        if self.group_name == "global":
            return " * global "
        n = self.group_name
        return f" o {n[:10]}{'...' if len(n)>10 else ''} "

    def _send(self, _=None):
        msg = self.entry.get().strip()
        ws = self.get_ws()
        if not msg or not ws:
            return
        if self.group_name == "global":
            payload = {"type": 1, "target": "SendMessageGlobal", "arguments": [msg]}
        else:
            payload = {"type": 1, "target": "SendMessageToGroup",
                       "arguments": [self.group_name, msg]}
        ws.send(json.dumps(payload) + RECORD_SEPARATOR)
        self.entry.delete(0, tk.END)

    def append(self, sender, message, tag="normal"):
        self.log.config(state=tk.NORMAL)
        ts = time.strftime("%H:%M")
        self.log.insert(tk.END, f"{ts} ", "ts")
        self.log.insert(tk.END, f"{sender}: ", tag)
        self.log.insert(tk.END, f"{message}\n", "normal")
        self.log.config(state=tk.DISABLED)
        self.log.see(tk.END)


# ══════════════════════════════════════════════════════════════════════════════
# HexBoard
# ══════════════════════════════════════════════════════════════════════════════

class HexBoard(tk.Canvas):
    HEX_SIZE   = 16
    ZOOM_MIN   = 0.4
    ZOOM_MAX   = 4.0
    ZOOM_STEP  = 0.12

    def __init__(self, parent, on_click, on_hover_change, **kw):
        super().__init__(parent, bg=BG, highlightthickness=0, **kw)
        self.on_click        = on_click
        self.on_hover_change = on_hover_change
        self.tiles      = {}
        self.hex_items  = {}
        self.text_items = {}
        self.offset_x   = 0
        self.offset_y   = 0
        self.zoom       = 1.0
        self.player_pos = None
        self._hovered   = None
        self._pan_last  = None
        self._pulse_phase = 0
        self._pulse_job   = None

        self.bind("<Configure>",      self._on_resize)
        self.bind("<Motion>",         self._on_motion)
        self.bind("<Button-1>",       self._on_click_ev)
        self.bind("<ButtonPress-2>",  self._pan_start)
        self.bind("<B2-Motion>",      self._pan_move)
        self.bind("<ButtonPress-3>",  self._pan_start)
        self.bind("<B3-Motion>",      self._pan_move)
        self.bind("<MouseWheel>",     self._on_scroll)
        self.bind("<Button-4>",       self._on_scroll)
        self.bind("<Button-5>",       self._on_scroll)

    def load_board(self, board_data, player_pos=None):
        self.tiles = {}
        for t in board_data:
            p = t["position"]
            self.tiles[(p["q"], p["r"], p["s"])] = t
        self.player_pos = player_pos
        self._redraw()
        self._start_pulse()

    def set_player(self, q, r, s):
        self.player_pos = (q, r, s)
        self._redraw()

    def _size(self):
        return self.HEX_SIZE * self.zoom

    def _hex_to_pixel(self, q, r):
        s = self._size()
        x = self.offset_x + s * (3/2 * q)
        y = self.offset_y + s * (math.sqrt(3)/2 * q + math.sqrt(3) * r)
        return x, y

    def _corners(self, cx, cy):
        s = self._size()
        return [(cx + s * math.cos(math.radians(a)),
                 cy + s * math.sin(math.radians(a))) for a in range(0, 360, 60)]

    def _key_at(self, x, y):
        best, bd = None, float("inf")
        for key in self.tiles:
            cx, cy = self._hex_to_pixel(*key[:2])
            d = math.hypot(x - cx, y - cy)
            if d < bd:
                bd, best = d, key
        return best if best and bd < self._size() * 1.2 else None

    def _base_color(self, tile):
        if not tile.get("pathable", True):  return HEX_WALL
        if tile.get("isWorldExit"):         return HEX_EXIT
        if tile.get("occupied"):            return HEX_OCC
        return HEX_EMPTY

    def _lerp_color(self, c1, c2, t):
        r1,g1,b1 = int(c1[1:3],16), int(c1[3:5],16), int(c1[5:7],16)
        r2,g2,b2 = int(c2[1:3],16), int(c2[3:5],16), int(c2[5:7],16)
        return "#{:02x}{:02x}{:02x}".format(
            int(r1+(r2-r1)*t), int(g1+(g2-g1)*t), int(b1+(b2-b1)*t))

    def _redraw(self):
        self.delete("all")
        self.hex_items  = {}
        self.text_items = {}
        for key, tile in self.tiles.items():
            q, r, s = key
            cx, cy = self._hex_to_pixel(q, r)
            pts  = self._corners(cx, cy)
            flat = [c for pt in pts for c in pt]
            is_player = self.player_pos == key
            color   = HEX_PLAYER if is_player else self._base_color(tile)
            outline = ACCENT2    if is_player else BORDER
            ow      = 1.5        if is_player else 0.5
            self.hex_items[key] = self.create_polygon(
                flat, fill=color, outline=outline, width=ow)
            fsz = max(6, int(self._size() * 0.5))
            if tile.get("isWorldExit"):
                self.text_items[key] = self.create_text(
                    cx, cy, text="*", fill=ACCENT2,
                    font=("Courier New", fsz, "bold"))
            elif is_player:
                self.text_items[key] = self.create_text(
                    cx, cy, text="@", fill=ACCENT3,
                    font=("Courier New", fsz, "bold"))

    def _start_pulse(self):
        if self._pulse_job:
            self.after_cancel(self._pulse_job)
        self._pulse_loop()

    def _pulse_loop(self):
        self._pulse_phase = (self._pulse_phase + 0.06) % (2 * math.pi)
        t = (math.sin(self._pulse_phase) + 1) / 2
        if self.player_pos and self.player_pos in self.hex_items:
            self.itemconfig(self.hex_items[self.player_pos],
                            fill=self._lerp_color(HEX_PLAYER, ACCENT, t))
        self._pulse_job = self.after(50, self._pulse_loop)

    def _on_resize(self, e):
        self.offset_x = e.width  // 2
        self.offset_y = e.height // 2
        self._redraw()

    def _on_scroll(self, e):
        if e.num == 4 or (hasattr(e, 'delta') and e.delta > 0):
            self.zoom = min(self.ZOOM_MAX, self.zoom + self.ZOOM_STEP)
        else:
            self.zoom = max(self.ZOOM_MIN, self.zoom - self.ZOOM_STEP)
        self._redraw()

    def _pan_start(self, e):
        self._pan_last = (e.x, e.y)

    def _pan_move(self, e):
        if self._pan_last:
            self.offset_x += e.x - self._pan_last[0]
            self.offset_y += e.y - self._pan_last[1]
            self._pan_last = (e.x, e.y)
            self._redraw()

    def _on_motion(self, e):
        key = self._key_at(e.x, e.y)
        if key == self._hovered:
            return
        if self._hovered and self._hovered in self.hex_items:
            old = self.tiles[self._hovered]
            is_p = self.player_pos == self._hovered
            self.itemconfig(self.hex_items[self._hovered],
                            fill=HEX_PLAYER if is_p else self._base_color(old),
                            outline=ACCENT2 if is_p else BORDER)
        self._hovered = key
        if key and key in self.hex_items:
            if self.player_pos != key:
                self.itemconfig(self.hex_items[key], fill=HEX_HOVER, outline=BORDER2)
        self.on_hover_change(key, self.tiles.get(key) if key else None)

    def _on_click_ev(self, e):
        key = self._key_at(e.x, e.y)
        if key:
            self.on_click(*key)


# ══════════════════════════════════════════════════════════════════════════════
# StatusDot
# ══════════════════════════════════════════════════════════════════════════════

class StatusDot(tk.Canvas):
    def __init__(self, parent, **kw):
        super().__init__(parent, width=10, height=10,
                         bg=kw.pop("bg", BG3), highlightthickness=0, **kw)
        self._dot = self.create_oval(2, 2, 8, 8, fill=TEXT_DIM, outline="")

    def set(self, color):
        self.itemconfig(self._dot, fill=color)


# ══════════════════════════════════════════════════════════════════════════════
# App
# ══════════════════════════════════════════════════════════════════════════════

class App(tk.Tk):
    def __init__(self):
        super().__init__()
        self.title("CHASMWATCH")
        self.geometry("1280x820")
        self.minsize(900, 600)
        self.configure(bg=BG)

        self.chat_ws          = None
        self.game_ws          = None
        self.username         = ""
        self.current_tile_pos = None
        self.chat_tabs        = {}
        self._exit_prompt_open = False

        self._build_login()

    def _get_chat_ws(self):
        return self.chat_ws

    def _tile_key(self, pos):
        if pos is None: return "global"
        return f"{pos[0]},{pos[1]},{pos[2]}"

    # ── Login ─────────────────────────────────────────────────────────────

    def _build_login(self):
        # decorative bg
        bg_cv = tk.Canvas(self, bg=BG, highlightthickness=0)
        bg_cv.place(x=0, y=0, relwidth=1, relheight=1)
        self._draw_bg_hexes(bg_cv)

        self.login_frame = tk.Frame(self, bg=BG)
        self.login_frame.place(relx=0.5, rely=0.5, anchor="center")

        card = tk.Frame(self.login_frame, bg=BG3, padx=44, pady=38)
        card.pack()

        tk.Frame(card, bg=ACCENT, height=2).pack(fill=tk.X, pady=(0, 20))
        tk.Label(card, text="CHASMWATCH", font=FONT_DISP,
                 fg=ACCENT3, bg=BG3).pack()
        tk.Label(card, text="a hexcrawl into the deep",
                 font=("Courier New", 9), fg=TEXT_DIM, bg=BG3).pack(pady=(2, 26))

        tk.Label(card, text="CALLSIGN", font=("Courier New", 8),
                 fg=TEXT_DIM, bg=BG3).pack(anchor="w")
        erow = tk.Frame(card, bg=BG4)
        erow.pack(fill=tk.X, pady=(4, 18))
        tk.Label(erow, text=">", fg=ACCENT2, bg=BG4,
                 font=("Courier New", 13)).pack(side=tk.LEFT, padx=(8,4), pady=8)
        self.username_entry = tk.Entry(
            erow, font=FONT_MONO, bg=BG4, fg=TEXT_BRT,
            insertbackground=ACCENT2, relief=tk.FLAT, bd=0, width=22,
        )
        self.username_entry.pack(side=tk.LEFT, fill=tk.X, expand=True, pady=8, padx=(0,8))
        self.username_entry.bind("<Return>", self._do_login)
        self.username_entry.focus_set()

        self.login_btn = tk.Button(
            card, text="DESCEND", font=("Courier New", 10, "bold"),
            bg=ACCENT, fg=TEXT_BRT, relief=tk.FLAT, bd=0,
            activebackground=ACCENT2, activeforeground=BG,
            cursor="hand2", command=self._do_login, pady=10,
        )
        self.login_btn.pack(fill=tk.X)
        self.login_status = tk.Label(card, text="", font=FONT_SM,
                                     fg=DANGER2, bg=BG3)
        self.login_status.pack(pady=(10, 0))
        tk.Frame(card, bg=ACCENT, height=2).pack(fill=tk.X, pady=(20, 0))

    def _draw_bg_hexes(self, canvas):
        size = 30
        for q in range(-2, 22):
            for r in range(-2, 16):
                cx = 50 + size * (3/2 * q)
                cy = 30 + size * (math.sqrt(3)/2 * q + math.sqrt(3) * r)
                pts = [(cx + size*math.cos(math.radians(a)),
                        cy + size*math.sin(math.radians(a))) for a in range(0,360,60)]
                flat = [c for pt in pts for c in pt]
                canvas.create_polygon(flat, fill=BG, outline=BORDER, width=0.5)

    def _do_login(self, _=None):
        u = self.username_entry.get().strip()
        if not u:
            self.login_status.config(text="callsign required")
            return
        self.login_btn.config(state=tk.DISABLED, text="descending...")
        threading.Thread(target=self._auth, args=(u,), daemon=True).start()

    def _auth(self, username):
        try:
            s = requests.Session()
            if USE_NGROK:
                s.headers.update(NGROK_HEADERS)
            s.post(f"{HTTP_URL}/auth/login/{username}")
            r  = s.post(f"{HTTP_URL}/chat/negotiate?negotiateVersion=1")
            ct = r.json()["connectionToken"]
            r2 = s.post(f"{HTTP_URL}/game/negotiate?negotiateVersion=1")
            gt = r2.json()["connectionToken"]
            cookies = "; ".join(f"{c.name}={c.value}" for c in s.cookies)
            self.username = username
            self.after(0, lambda: self._launch(ct, gt, cookies))
        except Exception as e:
            msg = str(e)
            self.after(0, lambda: self._login_err(msg))

    def _login_err(self, msg):
        self.login_status.config(text=f"x {msg[:56]}")
        self.login_btn.config(state=tk.NORMAL, text="DESCEND")

    # ── Main UI ───────────────────────────────────────────────────────────

    def _launch(self, ct, gt, cookies):
        for w in self.winfo_children():
            w.destroy()
        self._build_ui()
        self._connect_chat(ct, cookies)
        self._connect_game(gt, cookies)

    def _build_ui(self):
        # topbar
        top = tk.Frame(self, bg=BG2, height=38)
        top.pack(fill=tk.X)
        top.pack_propagate(False)
        tk.Frame(top, bg=ACCENT, width=4).pack(side=tk.LEFT, fill=tk.Y)
        tk.Label(top, text="CHASMWATCH", font=FONT_LG,
                 fg=ACCENT3, bg=BG2).pack(side=tk.LEFT, padx=(12, 20))
        self.pos_label = tk.Label(top, text="no tile", font=FONT_SM,
                                  fg=TEXT_DIM, bg=BG2)
        self.pos_label.pack(side=tk.LEFT)
        tk.Label(top, text=f"[ {self.username} ]", font=FONT_SM,
                 fg=GOLD2, bg=BG2).pack(side=tk.RIGHT, padx=14)

        dot_f = tk.Frame(top, bg=BG2)
        dot_f.pack(side=tk.RIGHT, padx=4)
        for label, attr in [("chat","chat_dot"),("game","game_dot")]:
            tk.Label(dot_f, text=label, font=("Courier New",8),
                     fg=TEXT_DIM, bg=BG2).pack(side=tk.LEFT, padx=(0,2))
            dot = StatusDot(dot_f, bg=BG2)
            dot.pack(side=tk.LEFT, padx=(0,8))
            setattr(self, attr, dot)

        tk.Frame(self, bg=BORDER, height=1).pack(fill=tk.X)

        body = tk.Frame(self, bg=BG)
        body.pack(fill=tk.BOTH, expand=True)

        # board side
        bp = tk.Frame(body, bg=BG)
        bp.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)

        jbar = tk.Frame(bp, bg=BG2)
        jbar.pack(fill=tk.X, padx=10, pady=8)
        tk.Label(jbar, text="WORLD TILE", font=("Courier New",8,"bold"),
                 fg=TEXT_DIM, bg=BG2).pack(side=tk.LEFT, padx=(8,8))
        self.q_e = self._coord_entry(jbar, "q", "0")
        self.r_e = self._coord_entry(jbar, "r", "0")
        self.s_e = self._coord_entry(jbar, "s", "0")
        tk.Button(jbar, text="JOIN", font=("Courier New",9,"bold"),
                  bg=ACCENT, fg=TEXT_BRT, relief=tk.FLAT, bd=0,
                  activebackground=ACCENT2, cursor="hand2",
                  command=self._join_tile).pack(side=tk.LEFT, padx=10, ipady=4, ipadx=10)
        self.status_lbl = tk.Label(jbar, text="", font=FONT_SM, fg=TEXT_DIM, bg=BG2)
        self.status_lbl.pack(side=tk.LEFT)

        tk.Frame(bp, bg=BORDER, height=1).pack(fill=tk.X, padx=10)

        self.board = HexBoard(bp, on_click=self._on_hex_click,
                              on_hover_change=self._on_hover_change)
        self.board.pack(fill=tk.BOTH, expand=True, padx=10, pady=6)

        foot = tk.Frame(bp, bg=BG2)
        foot.pack(fill=tk.X, padx=10, pady=(0,8))
        self.hover_lbl = tk.Label(foot, text="hover a tile",
                                  font=("Courier New",8), fg=TEXT_DIM, bg=BG2)
        self.hover_lbl.pack(side=tk.LEFT, padx=8)
        for col, lbl in [(HEX_PLAYER,"you"),(HEX_EXIT,"exit"),
                         (HEX_OCC,"occupied"),(HEX_EMPTY,"open"),(HEX_HOVER,"hover")]:
            tk.Label(foot, text="##", fg=col, bg=BG2,
                     font=("Courier New",9)).pack(side=tk.RIGHT, padx=(0,1))
            tk.Label(foot, text=lbl, fg=TEXT_DIM, bg=BG2,
                     font=("Courier New",8)).pack(side=tk.RIGHT, padx=(0,4))

        tk.Frame(body, bg=BORDER, width=1).pack(side=tk.LEFT, fill=tk.Y)

        # sidebar
        side = tk.Frame(body, bg=BG2, width=308)
        side.pack(side=tk.RIGHT, fill=tk.Y)
        side.pack_propagate(False)
        tk.Label(side, text="COMMS", font=("Courier New",8,"bold"),
                 fg=TEXT_DIM, bg=BG2).pack(anchor="w", padx=10, pady=(8,2))
        tk.Frame(side, bg=BORDER, height=1).pack(fill=tk.X, padx=4)

        self.notebook = ttk.Notebook(side)
        st = ttk.Style()
        st.theme_use("default")
        st.configure("TNotebook",     background=BG2, borderwidth=0, tabmargins=0)
        st.configure("TNotebook.Tab", background=BG3, foreground=TEXT_DIM,
                     font=("Courier New",8), padding=[5,3], borderwidth=0)
        st.map("TNotebook.Tab",
               background=[("selected", ACCENT)],
               foreground=[("selected", TEXT_BRT)])
        self.notebook.pack(fill=tk.BOTH, expand=True, padx=2, pady=(2,4))
        self._add_tab("global")

    def _coord_entry(self, parent, label, default):
        tk.Label(parent, text=label, font=("Courier New",8),
                 fg=TEXT_DIM, bg=BG2).pack(side=tk.LEFT, padx=(0,1))
        e = tk.Entry(parent, width=4, bg=BG3, fg=TEXT_BRT, font=FONT_MONO,
                     insertbackground=ACCENT2, relief=tk.FLAT, bd=0)
        e.insert(0, default)
        e.pack(side=tk.LEFT, padx=(0,6), ipady=4)
        return e

    # ── Tabs ──────────────────────────────────────────────────────────────

    def _add_tab(self, name):
        if name in self.chat_tabs: return
        self.chat_tabs[name] = ChatTab(self.notebook, name, self._get_chat_ws)

    def _get_tab(self, name):
        if name not in self.chat_tabs: self._add_tab(name)
        return self.chat_tabs[name]

    def _switch_tab(self, name):
        if name not in self.chat_tabs: return
        frame = self.chat_tabs[name].frame
        for i in range(self.notebook.index("end")):
            if self.notebook.nametowidget(self.notebook.tabs()[i]) == frame:
                self.notebook.select(i)
                return

    def _sys_log(self, msg, tab="global"):
        self._get_tab(tab).append("system", msg, "sys")

    # ── WebSocket — Chat ──────────────────────────────────────────────────

    def _connect_chat(self, token, cookies):
        hdrs = [f"Cookie: {cookies}"]
        if USE_NGROK: hdrs.append("ngrok-skip-browser-warning: true")

        def on_open(ws):
            ws.send(json.dumps({"protocol":"json","version":1}) + RECORD_SEPARATOR)

        def on_message(ws, message):
            for raw in message.split(RECORD_SEPARATOR):
                if not raw: continue
                data = json.loads(raw)
                if data == {}:
                    self.chat_ws = ws
                    self.after(0, lambda: self.chat_dot.set(ACCENT2))
                    return
                if data.get("type") == 6: continue
                if data.get("type") == 1:
                    t, args = data.get("target"), data.get("arguments",[])
                    if t == "ReceiveMessage":
                        s, m = args[0], args[1]
                        self.after(0, lambda s=s, m=m:
                            self._get_tab("global").append(
                                s, m, "sys" if s=="System" else "normal"))

        ws = websocket.WebSocketApp(
            f"{WS_URL}/chat?id={token}", header=hdrs,
            on_open=on_open, on_message=on_message,
            on_error=lambda ws,e: self.after(0, lambda: self._sys_log(f"[chat err] {e}")),
            on_close=lambda ws,*_: self.after(0, lambda: self.chat_dot.set(DANGER2)),
        )
        threading.Thread(target=ws.run_forever, daemon=True).start()

    # ── WebSocket — Game ──────────────────────────────────────────────────

    def _connect_game(self, token, cookies):
        hdrs = [f"Cookie: {cookies}"]
        if USE_NGROK: hdrs.append("ngrok-skip-browser-warning: true")

        def on_open(ws):
            ws.send(json.dumps({"protocol":"json","version":1}) + RECORD_SEPARATOR)

        def on_message(ws, message):
            for raw in message.split(RECORD_SEPARATOR):
                if not raw: continue
                data = json.loads(raw)
                if data == {}:
                    self.game_ws = ws
                    self.after(0, lambda: self.game_dot.set(ACCENT2))
                    return
                if data.get("type") == 6: continue
                if data.get("type") == 1:
                    self.after(0, lambda d=data: self._on_game_msg(d))

        ws = websocket.WebSocketApp(
            f"{WS_URL}/game?id={token}", header=hdrs,
            on_open=on_open, on_message=on_message,
            on_error=lambda ws,e: self.after(0, lambda: self._sys_log(f"[game err] {e}")),
            on_close=lambda ws,*_: self.after(0, lambda: self.game_dot.set(DANGER2)),
        )
        threading.Thread(target=ws.run_forever, daemon=True).start()

    def _on_game_msg(self, data):
        target = data.get("target")
        args   = data.get("arguments", [])
        if target == "ReceiveTileInfo":
            board = args[0] if args else []
            unit  = args[1] if len(args) > 1 else None
            self._on_tile_info(board, unit)
        elif target == "ExitPrompt":
            if args:
                self._on_exit_prompt(args[0])
        elif target == "ReceiveMessage":
            s, m = args[0], args[1]
            tab = self._tile_key(self.current_tile_pos)
            self._get_tab(tab).append(s, m, "sys" if s=="System" else "normal")
        elif target == "SystemMessage":
            self._sys_log(args[0])

    # ── Board / unit state ────────────────────────────────────────────────

    @staticmethod
    def _pos_tuple(d):
        if not isinstance(d, dict):
            return None
        q = d.get("q", d.get("Q"))
        r = d.get("r", d.get("R"))
        s = d.get("s", d.get("S"))
        if q is None or r is None or s is None:
            return None
        return (int(q), int(r), int(s))

    def _unit_is_mine(self, unit):
        if not unit or not isinstance(unit, dict):
            return False
        name = unit.get("name", unit.get("Name"))
        return bool(name) and name == self.username

    def _on_tile_info(self, board, unit):
        if not hasattr(self, "board"):
            return
        mine = self._unit_is_mine(unit)
        if mine and isinstance(unit, dict):
            tile_pos = self._pos_tuple(unit.get("tilePos", unit.get("TilePos")))
            board_pos = self._pos_tuple(unit.get("boardPos", unit.get("BoardPos")))
            if tile_pos is not None and tile_pos != self.current_tile_pos:
                self._enter_tile_context(*tile_pos, status="traveling...")
            self.board.load_board(board, player_pos=board_pos)
            n = len(board) if isinstance(board, list) else 0
            self.status_lbl.config(text=f"+ {n} tiles", fg=ACCENT2)
        else:
            # Another player's update (or legacy payload): keep our own marker.
            keep = getattr(getattr(self, "board", None), "player_pos", None)
            self.board.load_board(board, player_pos=keep)
            n = len(board) if isinstance(board, list) else 0
            self.status_lbl.config(text=f"+ {n} tiles", fg=ACCENT2)

    def _enter_tile_context(self, q, r, s, status="joining..."):
        self.current_tile_pos = (q, r, s)
        group = self._tile_key((q, r, s))
        if self.chat_ws:
            self.chat_ws.send(json.dumps({
                "type": 1, "target": "JoinRoom", "arguments": [group]
            }) + RECORD_SEPARATOR)
        self._add_tab(group)
        self._switch_tab(group)
        self.pos_label.config(text=f"tile {q},{r},{s}", fg=TEXT)
        self.status_lbl.config(text=status, fg=TEXT_DIM)

    # ── World-exit prompt ─────────────────────────────────────────────────

    def _on_exit_prompt(self, prompt):
        if getattr(self, "_exit_prompt_open", False):
            return
        world = self._pos_tuple(prompt.get("worldTilePos", prompt.get("WorldTilePos")))
        neighbor = self._pos_tuple(prompt.get("neighborTilePos", prompt.get("NeighborTilePos")))
        exit_dir = prompt.get("exitDirection", prompt.get("ExitDirection", "?"))
        if world is None or neighbor is None:
            return
        if world != self.current_tile_pos:
            return  # stale prompt for a tile we already left
        nq, nr, ns = neighbor
        self._exit_prompt_open = True
        try:
            go = messagebox.askyesno(
                "World exit",
                f"You stand on the {exit_dir} exit.\n"
                f"Travel to tile {nq},{nr},{ns}?",
            )
        finally:
            self._exit_prompt_open = False
        if go:
            self._confirm_traverse(world)
        else:
            self.status_lbl.config(text="stayed on this tile", fg=TEXT_DIM)

    def _confirm_traverse(self, world_pos):
        if not self.game_ws:
            return
        wq, wr, ws = world_pos
        self.game_ws.send(json.dumps({
            "type": 1, "target": "TraverseWorldExit",
            "arguments": [{"q": wq, "r": wr, "s": ws}]
        }) + RECORD_SEPARATOR)
        self.status_lbl.config(text="traveling...", fg=TEXT_DIM)

    # ── Actions ───────────────────────────────────────────────────────────

    def _join_tile(self):
        if not self.game_ws:
            self.status_lbl.config(text="x game hub not connected", fg=DANGER2)
            return
        try:
            q, r, s = int(self.q_e.get()), int(self.r_e.get()), int(self.s_e.get())
        except ValueError:
            self.status_lbl.config(text="x q r s must be integers", fg=DANGER2)
            return
        if q + r + s != 0:
            self.status_lbl.config(text="x q+r+s must equal 0", fg=DANGER2)
            return

        self._enter_tile_context(q, r, s, status="joining...")

        self.game_ws.send(json.dumps({
            "type":1, "target":"JoinTile",
            "arguments":[{"q":q,"r":r,"s":s}, None]
        }) + RECORD_SEPARATOR)

    def _on_hex_click(self, q, r, s):
        if not self.game_ws or not self.current_tile_pos:
            return
        wq, wr, ws = self.current_tile_pos
        self.game_ws.send(json.dumps({
            "type":1, "target":"MoveUnit",
            "arguments":[{"q":wq,"r":wr,"s":ws}, {"q":q,"r":r,"s":s}]
        }) + RECORD_SEPARATOR)

    def _on_hover_change(self, key, tile):
        if not key or not tile:
            self.hover_lbl.config(text="hover a tile")
            return
        q, r, s = key
        flags = []
        if tile.get("isWorldExit"):   flags.append(f"exit>{tile.get('worldExitDirection','?')} (click to travel)")
        if tile.get("occupied"):      flags.append("occupied")
        if not tile.get("pathable", True): flags.append("impassable")
        flag_str = "  " + "  ".join(f"[{f}]" for f in flags) if flags else ""
        self.hover_lbl.config(text=f"q={q} r={r} s={s}{flag_str}")


if __name__ == "__main__":
    App().mainloop()