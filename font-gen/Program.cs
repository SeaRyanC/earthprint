using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

class Program {
    static void Main(string[] args) {
        string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz1234567890!@#$%^&*(),./<>?[]\\{}|~`";

        Dictionary<string, Font> fonts = new Dictionary<string, Font>();
        fonts["Small (7 tall)"] = new Font("Terminal", 7);
        fonts["Medium (12 tall)"] = new Font("Terminal", 12);
        fonts["Large (18 tall)"] = new Font("Terminal", 18);
        fonts["Huge (24 tall)"] = new Font("Terminal", 24);

        Dictionary<string, Dictionary<char, List<Point>>> points = new Dictionary<string, Dictionary<char, List<Point>>>();

        Bitmap bmp = new Bitmap(1024, 64);
        Graphics gfx = Graphics.FromImage(bmp);
        gfx.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
        gfx.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;

        foreach (var kvp in fonts) {
            var renderings = points[kvp.Key] = new Dictionary<char, List<Point>>();
            Font font = kvp.Value;

            FontFamily family = font.FontFamily;
            var height = font.Size * family.GetCellAscent(FontStyle.Regular) / family.GetEmHeight(FontStyle.Regular);

            // Draw every character onto the canvas so we can determine the maximum height
            gfx.Clear(Color.White);
            foreach (char c in alphabet) {
                gfx.DrawString(c.ToString(), font, Brushes.Black, 0, 0);
            }

            int totalLeft, totalRight, totalTop, totalBottom;
            GetRenderedBounds(bmp, out totalLeft, out totalRight, out totalTop, out totalBottom);

            // Render each character and produce a test image
            foreach (char c in alphabet) {
                var pixelList = renderings[c] = new List<Point>();

                gfx.Clear(Color.White);
                gfx.DrawString(c.ToString(), font, Brushes.Black, 0, 0);

                int left, right, top, bottom;
                GetRenderedBounds(bmp, out left, out right, out top, out bottom);

                for (int y = top; y <= bottom; y++) {
                    for (int x = totalLeft; x <= totalRight; x++) {
                        if (bmp.GetPixel(x, y).R == 0) {
                            pixelList.Add(new Point(x - left, y - totalTop));
                        }
                    }
                }
            }
        }

        // Produce a test rendering
        string testPhrase = "Ore outpost #4";
        foreach (var kvp in fonts) {
            int cx = 0;
            gfx.Clear(Color.White);
            int spaceWidth = points[kvp.Key]['.'].Max(p => p.X);
            foreach (char c in testPhrase) {
                int charWidth = 0;
                if (c == ' ') {
                    charWidth = spaceWidth;
                } else {
                    foreach (Point pt in points[kvp.Key][c]) {
                        bmp.SetPixel(cx + pt.X, pt.Y, Color.Black);
                        charWidth = Math.Max(pt.X, charWidth);
                    }
                }
                cx += charWidth + 2;
            }
            bmp.Save(kvp.Key + ".png");
        }

        // Generate Lua object
        LuaTable resultTable = new LuaTable();

        foreach (var kvp in fonts) {
            LuaTable fontLookup = new LuaTable();
            resultTable.Add(new LuaString(kvp.Key), fontLookup);

            foreach (char c in alphabet) {
                LuaArray pts = new LuaArray();
                foreach (var pt in points[kvp.Key][c]) {
                    LuaArray p = new LuaArray();
                    p.values.Add(new LuaInt(pt.X));
                    p.values.Add(new LuaInt(pt.Y));
                    pts.values.Add(p);
                }
                fontLookup.Add(new LuaString(c.ToString()), pts);
            }
        }

        File.WriteAllText("font.lua", "fonts = " + resultTable.ToString());
        Console.WriteLine("Done!");
    }

    private static void GetRenderedBounds(Bitmap bmp, out int left, out int right, out int top, out int bottom) {
        left = bmp.Width;
        right = 0;
        top = bmp.Height;
        bottom = 0;

        for (int y = 0; y < bmp.Height; y++) {
            for (int x = 0; x < bmp.Width; x++) {
                if (bmp.GetPixel(x, y).R == 0) {
                    left = Math.Min(left, x);
                    top = Math.Min(top, y);
                    right = Math.Max(right, x);
                    bottom = Math.Max(bottom, y);
                }
            }
        }
    }
}

class LuaObject {
}
class LuaString : LuaObject {
    public string value;
    public LuaString(string value) {
        this.value = value;
    }

    public override string ToString() {
        Dictionary<char, string> escapes = new Dictionary<char, string>();
        escapes['\\'] = @"\\";
        escapes['"'] = "\\\"";
        escapes['\''] = @"\'";
        escapes['['] = @"\['";
        escapes[']'] = @"\]'";
        StringBuilder sb = new StringBuilder();
        foreach (char c in this.value) {
            string s;
            if (escapes.TryGetValue(c, out s)) {
                sb.Append(s);
            } else {
                sb.Append(c.ToString());
            }
        }
        return "\"" + sb.ToString() + "\"";
    }
}
class LuaInt : LuaObject {
    public int value;
    public LuaInt(int value) {
        this.value = value;
    }
    public override string ToString() {
        return this.value.ToString();
    }
}
class LuaTable : LuaObject {
    private List<LuaObject> keys = new List<LuaObject>();
    private List<LuaObject> values = new List<LuaObject>();

    public void Add(LuaObject key, LuaObject value) {
        this.keys.Add(key);
        this.values.Add(value);
    }

    public override string ToString() {
        List<string> entries = new List<string>();
        for (var i = 0; i < this.keys.Count; i++) {
            entries.Add("[" + this.keys[i].ToString() + "] = " + this.values[i].ToString());
        }
        return "{ " + String.Join(", ", entries.ToArray()) + " }";
    }
}

class LuaArray : LuaObject {
    public List<LuaObject> values = new List<LuaObject>();

    public override string ToString() {
        return "{ " + String.Join(", ", this.values.Select(v => v.ToString()).ToArray()) + " }";
    }
}
