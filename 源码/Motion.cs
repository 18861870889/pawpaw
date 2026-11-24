using System;
using System.Collections.Generic;
using System.Drawing;

namespace KeyboardCat
{
    internal sealed class Cap
    {
        public readonly float X, Width;
        public readonly int Row, Tone;
        public readonly string Legend;
        public Cap(float x, int row, float width, string legend, int tone)
        { X = x; Row = row; Width = width; Legend = legend; Tone = tone; }
        public bool OnLeft { get { return Center.X<176; } }
        public PointF Center { get { return Keyboard.Project(X + Width * .5f, Row + .45f); } }
    }

    internal sealed class KeyHit
    {
        public readonly Cap Cap;
        public readonly string Label;
        public KeyHit(Cap cap, string label) { Cap = cap; Label = label; }
    }

    internal static class Keyboard
    {
        public static readonly List<Cap> Caps = new List<Cap>();
        static readonly Dictionary<int, KeyHit> map = new Dictionary<int, KeyHit>();
        static int row;
        static float x;

        static Keyboard()
        {
            Add(0x1b, "Esc", 1, 1);
            foreach (char c in "1234567890") Add(c, c.ToString(), 1, 0);
            Add(0xbd, "-", 1, 0); Add(0xbb, "=", 1, 0); Add(0x08, "Bksp", 2, 2);
            Next(); Add(0x09, "Tab", 1.5f, 2); Letters("QWERTYUIOP");
            Add(0xdb, "[", 1, 0); Add(0xdd, "]", 1, 0); Add(0xdc, "\\", 1.5f, 2);
            Next(); Add(0x14, "Caps", 1.75f, 2); Letters("ASDFGHJKL");
            Add(0xba, ";", 1, 0); Add(0xde, "'", 1, 0); Add(0x0d, "Enter", 2.25f, 3);
            Next(); Add(0xa0, "Shift", 2.25f, 2); Letters("ZXCVBNM");
            Add(0xbc, ",", 1, 0); Add(0xbe, ".", 1, 0); Add(0xbf, "/", 1, 0);
            Add(0x26, "↑", 1, 0); Add(0xa1, "Shift", 1.75f, 2);
            Next(); Add(0xa2, "Ctrl", 1.5f, 2); Add(0x5b, "Win", 1, 2);
            Add(0xa4, "Alt", 1.25f, 2); Add(0x20, "Space", 6, 3); Add(0xa5, "Alt", 1.25f, 2);
            Add(0x25, "←", 1, 0); Add(0x28, "↓", 1, 0); Add(0x27, "→", 1, 1);
            Add(0xa3, "Ctrl", 1, 2);

            Alias(0x5c, 0x5b, "Win"); Alias(0xc0, 0x1b, "\u0060");
            Alias(0x2e, 0x08, "Del"); Alias(0x2d, 0x08, "Ins");
            Alias(0x24, 0x25, "Home"); Alias(0x23, 0x27, "End");
            Alias(0x21, 0x26, "PgUp"); Alias(0x22, 0x28, "PgDn");
            for (int n = 0; n < 12; n++)
                Alias(0x70 + n, Caps[n + 1], "F" + (n + 1));
            for (int n = 0; n <= 9; n++)
                Alias(0x60 + n, '0' + n, n.ToString());
            Alias(0x6a, '8', "×"); Alias(0x6b, 0xbb, "+"); Alias(0x6d, 0xbd, "−");
            Alias(0x6e, 0xbe, "."); Alias(0x6f, 0xbf, "÷");
        }

        static void Next() { row++; x = 0; }
        static void Letters(string letters) { foreach (char c in letters) Add(c, c.ToString(), 1, 0); }
        static void Add(int vk, string label, float width, int tone)
        {
            Cap cap = new Cap(x, row, width, label, tone);
            Caps.Add(cap); map.Add(vk, new KeyHit(cap, label)); x += width;
        }
        static void Alias(int vk, int key, string label) { Alias(vk, map[key].Cap, label); }
        static void Alias(int vk, Cap cap, string label) { map[vk] = new KeyHit(cap, label); }

        public static KeyHit Find(int vk)
        {
            KeyHit key;
            return map.TryGetValue(vk, out key) ? key : null;
        }

        // A five-row keyboard seen at a slight angle. Paw targets use the same projection as the caps.
        public static PointF Project(float units, float rows)
        {
            // The cat faces the viewer. Turn the layout before perspective projection.
            units=15-units; rows=5-rows;
            return new PointF(69f - 2.05f * rows + (14.65f + .22f * rows) * units,
                              203f + 7.55f * rows + .29f * units);
        }
    }

    internal sealed class Paw
    {
        public KeyHit Key;
        public int KeyCode;
        public double Started = -100, Released = -100;
        public float Reach(double now)
        {
            if(Key==null || now<Started) return 0;
            double age=Math.Min(now,Released)-Started;
            // Give even a short tap a visible contact before withdrawing.
            float contact=Motion.Smooth((float)((now-Started)/.055));
            double beat=Math.Max(0,age-.42);
            float lift=(float)(.5-.5*Math.Cos(beat*Math.PI*12));
            contact*=1-.46f*Motion.Smooth((float)(beat/.22))*lift;
            return contact*(1-Motion.Smooth((float)((now-Math.Max(Started+.102,Released))/.188)));
        }

        public float Temper(double now)
        {
            if(Key==null) return 0;
            return Motion.Smooth((float)((Math.Min(now,Released)-Started-.44)/.45))*
                (1-Motion.Smooth((float)((now-Released)/.30)));
        }
    }

    internal struct KeyBubble
    {
        public string Label;
        public PointF Origin;
        public double Start;
        public int Lane;
        public bool IsWord;
    }

    internal sealed class Motion
    {
        public readonly Paw Left = new Paw(), Right = new Paw();
        public readonly TrickPlayer Tricks = new TrickPlayer();
        public readonly List<KeyBubble> Bubbles = new List<KeyBubble>(4);
        public KeyBubble? MatchedWord {get; private set;}
        public const double BubbleLifetime = .72, WordBubbleLifetime = 1.55;
        public float LookX, LookY, MouseReach;
        public double LastClick = -100, LastMouse = -100;
        public int MouseSide=2;
        public bool MouseHeld;
        readonly bool[] down = new bool[256];
        int lane;
        int heldButtons;
        public bool Private {get; private set;}
        double privacyChanged=-100;
        double typingStart,typingUntil=double.NegativeInfinity;
        float privacyStart;

        public float Cover(double now) {return Mix(privacyStart,Private?1:0,Smooth((float)((now-privacyChanged)/.18)));}
        public void SetPrivacy(bool value,double now)
        {
            if(value==Private) return;
            privacyStart=Cover(now);privacyChanged=now;Private=value;Reset();
        }

        public static float Limit(float x, float lo, float hi) { return Math.Max(lo, Math.Min(hi, x)); }
        public static float Smooth(float x) { x = Limit(x, 0, 1); return x * x * (3 - 2 * x); }
        public static float Mix(float a, float b, float t) { return a + (b - a) * t; }
        public static PointF Mix(PointF a, PointF b, float t)
        { return new PointF(Mix(a.X, b.X, t), Mix(a.Y, b.Y, t)); }

        public bool KeyHeld(int key) {return key>=0 && key<down.Length && down[key];}

        public void ChineseMatch(string text,double now,bool showLabels)
        {
            ChineseWord word=ChineseWord.Named(text);
            if(Private || !Tricks.Enabled || word==null) return;
            Tricks.Play(word.Trick,now);
            if(showLabels) {
                Bubbles.Clear();
                MatchedWord=new KeyBubble {Label=text,Start=now,IsWord=true};
            }
        }

        public void KeyEvent(int vk, bool release, double now, bool showLabels, bool systemShortcut=false,bool deferWord=false)
        {
            if (Private || vk < 0 || vk >= down.Length) return;
            Tricks.InterruptIdle(now);
            if (release) {
                down[vk] = false;
                if (Left.KeyCode == vk && double.IsPositiveInfinity(Left.Released)) Left.Released=now;
                if (Right.KeyCode == vk && double.IsPositiveInfinity(Right.Released)) Right.Released=now;
                return;
            }
            if (down[vk]) return;
            down[vk]=true;
            string matched=null;
            if (Tricks.Enabled)
            {
                bool shortcut=systemShortcut || down[0x11] || down[0x12] || down[0x5b] || down[0x5c] ||
                    down[0xa2] || down[0xa3] || down[0xa4] || down[0xa5];
                if(deferWord) Tricks.BreakSequence();
                else matched=Tricks.Observe(vk,now,shortcut);
            }
            KeyHit hit = Keyboard.Find(vk);
            if (hit == null) return;
            Paw paw = hit.Cap.OnLeft ? Left : Right;
            if (vk == 0x20) paw = Left.Started < Right.Started ? Left : Right;
            paw.Key = hit; paw.KeyCode=vk; paw.Started=now; paw.Released=double.PositiveInfinity;
            if (!showLabels) return;
            if (matched!=null) {
                Bubbles.Clear();
                MatchedWord=new KeyBubble {Label=matched,Start=now,IsWord=true};
                return;
            }
            if (Bubbles.Count == 4) Bubbles.RemoveAt(0);
            Bubbles.Add(new KeyBubble { Label = hit.Label, Origin = hit.Cap.Center, Start = now, Lane = lane });
            lane = (lane + 1) % 3;
        }

        // Unclassified fields report activity only. Both paws tap to a fixed beat that has no key identity.
        public void TypingActivity(double now)
        {
            if(Private) return;
            Tricks.InterruptIdle(now);
            if(now>=typingUntil) typingStart=now;
            typingUntil=now+.52;
        }
        public bool IsTypingActivity(double now) {return now>=typingStart && now<typingUntil;}
        public float TypingReach(double now,bool left)
        {
            if(!IsTypingActivity(now)) return 0;
            float envelope=Smooth((float)Math.Min((now-typingStart)/.07,(typingUntil-now)/.16));
            double beat=Math.Cos((now-typingStart)*Math.PI*5.6)*(left?1:-1);
            return envelope*(.62f+.38f*(float)beat);
        }

        public void MouseButtons(ushort flags, double now)
        {
            if(Private) return;
            Tricks.InterruptIdle(now);
            const int pressMask=0x155, wheelMask=0xc00;
            heldButtons=(heldButtons | (flags & pressMask)) & ~((flags >> 1) & pressMask);
            MouseHeld=heldButtons!=0;
            if ((flags & (pressMask | wheelMask)) == 0) return;
            Tricks.BreakSequence();
            MouseSide=(flags & 1)!=0 ? 0 : (flags & 4)!=0 ? 1 : 2;
            LastClick = LastMouse = now;
        }

        public void Step(double now, double elapsed, float dx, float dy, bool gameSurface=false)
        {
            float follow = (float)(1 - Math.Exp(-15 * Math.Max(0, Math.Min(elapsed, .2))));
            LookX = Mix(LookX, dx / (Math.Abs(dx) + 118f), follow);
            LookY = Mix(LookY, dy / (Math.Abs(dy) + 100f), follow);
            float mouseTarget;
            if(IsTypingActivity(now)) mouseTarget=0;
            else if(gameSurface) {
                // Aiming moves the eyes. Give a skill's keypress time to land before reaching for a click.
                bool onKeyboard=Left.Key!=null || Right.Key!=null;
                mouseTarget=onKeyboard ? 0 : MouseHeld ? 1 : 1-Smooth((float)((now-LastClick-.10)/.18));
            } else mouseTarget=MouseHeld ? 1 : 1-Smooth((float)((now-LastMouse-.65)/.55));
            MouseReach = Mix(MouseReach, mouseTarget, follow);
            while (Bubbles.Count > 0 && now - Bubbles[0].Start >= BubbleLifetime) Bubbles.RemoveAt(0);
            if (MatchedWord.HasValue && now-MatchedWord.Value.Start>=WordBubbleLifetime) MatchedWord=null;
            if (Left.Key!=null && now>=Math.Max(Left.Started+.102,Left.Released)+.30) Left.Key=null;
            if (Right.Key!=null && now>=Math.Max(Right.Started+.102,Right.Released)+.30) Right.Key=null;
        }

        public void ClearBubbles()
        {
            Bubbles.Clear();
            MatchedWord=null;
        }

        public void ClearInput()
        {
            Left.Released=Right.Released=-100;
            Left.Key = Right.Key = null;
            Left.Started = Right.Started = LastClick = LastMouse = -100;
            MouseHeld=false; heldButtons=0; MouseSide=2;
            MouseReach = LookX = LookY = 0;
            typingUntil=double.NegativeInfinity;
            Array.Clear(down, 0, down.Length);
            ClearBubbles();
            Tricks.BreakSequence();
        }

        public void Reset()
        {
            ClearInput();
            Tricks.Reset();
        }

        public static float Blink(double t)
        {
            double phase = t % 8.15;
            double d = Math.Min(Math.Abs(phase - 2.83), Math.Abs(phase - 6.94));
            if (phase > 7.0) d = Math.Min(d, Math.Abs(phase - 7.23));
            return Smooth((float)(d / .105));
        }
    }
}
