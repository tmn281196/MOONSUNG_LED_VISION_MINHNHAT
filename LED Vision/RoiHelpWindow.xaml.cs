using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace LEDVision
{
    /// <summary>
    /// Cửa sổ hướng dẫn dùng chung, chọn theo chủ đề:
    ///   "roi" : thao tác ROI (vẽ / di chuyển / chọn nhóm / copy / paste / xóa / zoom)
    ///   "hsv" : lý thuyết màu HSV và cách app tách màu (segmentation) theo dải H/S/V
    /// Hỗ trợ 2 ngôn ngữ: English và ไทย, chuyển qua lại bằng nút ở góc phải trên (nhớ trong suốt phiên chạy).
    /// Nội dung được dựng từ bảng chuỗi bên dưới → thêm chủ đề / ngôn ngữ mới chỉ cần thêm một mục vào bảng.
    /// </summary>
    public partial class RoiHelpWindow : Window
    {
        // Ngôn ngữ đang chọn, giữ trong suốt phiên chạy (mở lại cửa sổ vẫn nhớ)
        private static string currentLang = "EN";

        private readonly string topic;

        private class Section
        {
            public string Title;
            public string[] Lines;
            public Section(string title, params string[] lines) { Title = title; Lines = lines; }
        }

        private class HelpText
        {
            public string WindowTitle;
            public string Heading;
            public string CloseLabel;
            public Section[] Sections;
        }

        // texts[topic][lang]
        private static readonly Dictionary<string, Dictionary<string, HelpText>> texts = new Dictionary<string, Dictionary<string, HelpText>>
        {
            // ======================= ROI =======================
            {
                "roi", new Dictionary<string, HelpText>
                {
                    {
                        "EN", new HelpText
                        {
                            WindowTitle = "ROI Help",
                            Heading = "ROI editing guide",
                            CloseLabel = "Close",
                            Sections = new[]
                            {
                                new Section("1. Choose a group",
                                    "In Inspection Settings → Group Name, tick 4-LED, Decimal Point or 7-Segment.",
                                    "Every ROI you draw goes into the ticked group.",
                                    "Tick None to lock the image (no drawing / moving)."),
                                new Section("2. Add an ROI",
                                    "RIGHT-click on the camera image.",
                                    "A circular ROI is created there with the current Radius (ROI Size slider).",
                                    "Area Threshold sets the minimum lit area for that group."),
                                new Section("3. Move one ROI",
                                    "Drag the ROI with the LEFT mouse button.",
                                    "Or click the ROI once, then press the arrow keys ← ↑ → ↓ to nudge it 1 px at a time."),
                                new Section("4. Select and move many ROIs (Group Select)",
                                    "Click the Group Select button (the group icon) to enter group mode.",
                                    "LEFT-drag on an empty area to draw a rectangle around the ROIs you want.",
                                    "LEFT-drag inside that rectangle to move the whole selection.",
                                    "Arrow keys also move the whole selection. In this mode you cannot add or drag single ROIs.",
                                    "Click Group Select again to return to single-ROI mode."),
                                new Section("5. Copy / Paste / Duplicate",
                                    "Ctrl+C : copy the selected ROI (or the group selection).",
                                    "Ctrl+V : paste into the current group at a new position (each paste shifts 20 px further).",
                                    "After pasting, the pasted copies become the current selection, not the originals.",
                                    "Ctrl+D : duplicate the selected ROI right next to the original."),
                                new Section("6. Delete",
                                    "Delete : remove the selected ROI (or the group selection).",
                                    "Ctrl+Delete : remove every ROI of the current group.",
                                    "Ctrl+Shift+Delete : remove all ROIs of all groups."),
                                new Section("7. Zoom the view",
                                    "Roll the mouse wheel over the image to zoom in / out around the cursor.",
                                    "Reset Zoom button (magnifier icon) restores 1:1."),
                                new Section("8. Tips",
                                    "Keyboard shortcuts work anywhere on the Vision page, except while typing in a text box.",
                                    "After editing, press the Save button at the top-right to write the model file, otherwise changes are lost.",
                                    "Camera settings (exposure, focus, shift...) are saved into the same model file."),
                            }
                        }
                    },
                    {
                        "TH", new HelpText
                        {
                            WindowTitle = "คู่มือ ROI",
                            Heading = "คู่มือการแก้ไข ROI",
                            CloseLabel = "ปิด",
                            Sections = new[]
                            {
                                new Section("1. เลือกกลุ่ม",
                                    "ที่แท็บ Inspection Settings → Group Name ให้ติ๊ก 4-LED, Decimal Point หรือ 7-Segment",
                                    "ROI ทุกอันที่วาดจะถูกเพิ่มเข้ากลุ่มที่ติ๊กไว้",
                                    "ติ๊ก None เพื่อล็อกภาพ (วาด / ย้ายไม่ได้)"),
                                new Section("2. เพิ่ม ROI",
                                    "คลิกขวาที่ภาพจากกล้อง",
                                    "จะสร้าง ROI วงกลมที่จุดนั้นด้วยขนาดรัศมีปัจจุบัน (แถบเลื่อน Radius ในส่วน ROI Size)",
                                    "Area Threshold คือพื้นที่สว่างขั้นต่ำที่ถือว่าติดของกลุ่มนั้น"),
                                new Section("3. ย้าย ROI ทีละอัน",
                                    "ลาก ROI ด้วยเมาส์ซ้าย",
                                    "หรือคลิก ROI หนึ่งครั้ง แล้วกดปุ่มลูกศร ← ↑ → ↓ เพื่อขยับทีละ 1 พิกเซล"),
                                new Section("4. เลือกและย้ายหลาย ROI (Group Select)",
                                    "กดปุ่ม Group Select (ไอคอนรูปกลุ่ม) เพื่อเข้าโหมดเลือกหลายอัน",
                                    "ลากเมาส์ซ้ายบนพื้นที่ว่างเพื่อวาดกรอบสี่เหลี่ยมล้อม ROI ที่ต้องการ",
                                    "ลากเมาส์ซ้ายภายในกรอบนั้นเพื่อย้าย ROI ทั้งหมดที่เลือกไว้พร้อมกัน",
                                    "ปุ่มลูกศรก็ใช้ย้ายทั้งกลุ่มได้ ในโหมดนี้จะเพิ่มหรือลาก ROI ทีละอันไม่ได้",
                                    "กด Group Select อีกครั้งเพื่อกลับสู่โหมดเลือกทีละอัน"),
                                new Section("5. คัดลอก / วาง / ทำซ้ำ",
                                    "Ctrl+C : คัดลอก ROI ที่เลือก (หรือกลุ่มที่เลือกไว้)",
                                    "Ctrl+V : วางลงในกลุ่มปัจจุบันที่ตำแหน่งใหม่ (วางแต่ละครั้งเลื่อนออกไปอีก 20 พิกเซล)",
                                    "หลังวาง ROI ที่วางใหม่จะกลายเป็นตัวที่ถูกเลือก ไม่ใช่ต้นฉบับ",
                                    "Ctrl+D : ทำซ้ำ ROI ที่เลือกไว้ข้าง ๆ ต้นฉบับทันที"),
                                new Section("6. ลบ",
                                    "Delete : ลบ ROI ที่เลือก (หรือกลุ่มที่เลือกไว้)",
                                    "Ctrl+Delete : ลบ ROI ทั้งหมดของกลุ่มปัจจุบัน",
                                    "Ctrl+Shift+Delete : ลบ ROI ทั้งหมดของทุกกลุ่ม"),
                                new Section("7. ซูมภาพ",
                                    "หมุนล้อเมาส์บนภาพเพื่อซูมเข้า / ออก รอบตำแหน่งเมาส์",
                                    "ปุ่ม Reset Zoom (ไอคอนแว่นขยาย) กลับสู่ขนาด 1:1"),
                                new Section("8. เคล็ดลับ",
                                    "คีย์ลัดใช้ได้ทุกที่ในหน้า Vision ยกเว้นตอนกำลังพิมพ์ในช่องข้อความ",
                                    "หลังแก้ไขให้กดปุ่ม Save ที่มุมขวาบนเพื่อบันทึกไฟล์โมเดล มิฉะนั้นการแก้ไขจะหายไป",
                                    "ค่าตั้งกล้อง (exposure, focus, shift...) ถูกบันทึกลงไฟล์โมเดลเดียวกัน"),
                            }
                        }
                    },
                }
            },

            // ======================= HSV =======================
            {
                "hsv", new Dictionary<string, HelpText>
                {
                    {
                        "EN", new HelpText
                        {
                            WindowTitle = "HSV Help",
                            Heading = "HSV colour segmentation",
                            CloseLabel = "Close",
                            Sections = new[]
                            {
                                new Section("1. What is HSV?",
                                    "The camera delivers pixels as Red / Green / Blue. The app converts every frame to HSV before inspecting.",
                                    "H (Hue) = the colour tone: red, yellow, green, cyan, blue, magenta... independent of how bright it is.",
                                    "S (Saturation) = how pure the colour is. 0 = grey / white, 255 = fully vivid.",
                                    "V (Value) = how bright the pixel is. 0 = black, 255 = brightest.",
                                    "HSV is used because an LED's Hue stays almost the same when exposure or ambient light changes, while RGB values all move together."),
                                new Section("2. Ranges used by this app (OpenCV scale)",
                                    "H : 0 ~ 179 (half of the 0 ~ 359° colour wheel).  S : 0 ~ 255.  V : 0 ~ 255.",
                                    "Approximate Hue of common LED colours: Red 0 (and 175 ~ 179), Orange 10 ~ 20, Yellow 25 ~ 35, Green 55 ~ 75, Cyan 85 ~ 95, Blue 110 ~ 130, Magenta / Pink 145 ~ 165.",
                                    "Red sits at BOTH ends of the scale. The app does not wrap, so pick one side: either 0 ~ 10 or 170 ~ 179, whichever the histogram shows."),
                                new Section("3. How the inspection works",
                                    "For each ROI the app keeps only pixels whose H, S and V all fall inside the Set ranges of that ROI's group (inRange → mask).",
                                    "It then counts the kept pixels (the lit area). If the count is greater than Area Threshold, the LED is judged ON with the right colour → PASS; otherwise → NG.",
                                    "Persist (Inspection Settings) requires the result to hold for N consecutive frames before PASS / NG changes, to stop flicker."),
                                new Section("4. Reading the panel",
                                    "Set : the min ~ max range for the group currently ticked. Click a number box to pick the value with the colour picker.",
                                    "Dotted circle row : the measured min ~ max of the SELECTED ROI (dashed outline on the image, orange line in the histogram).",
                                    "Solid circle row : the measured min ~ max over the UNSELECTED ROIs of the group (blue line in the histogram).",
                                    "All row : the measured min ~ max over ALL ROIs of the group (numbers only, not drawn in the histogram).",
                                    "H / S / V histogram tabs : how many pixels of the selected ROIs have each value. The tall peak is the LED colour; set min and max a little either side of that peak."),
                                new Section("5. How to set the ranges",
                                    "1) Turn the LED on, draw the ROI, then open the H tab: put Hue min / max just around the main peak (about ±8).",
                                    "2) S tab: an LED that is on is vivid, so start with S min ≈ 80 ~ 120 and S max = 255. White LEDs are the exception: S is low, use 0 ~ 60.",
                                    "3) V tab: an LED that is on is bright, so V min ≈ 100 ~ 150 and V max = 255. V min is what rejects an LED that is OFF.",
                                    "4) Turn the LED off and check that the lit area drops below Area Threshold. Raise V min or Area Threshold if it does not.",
                                    "5) Check the other colours in the same group do not fall inside this Hue range. Narrow the range if they do."),
                                new Section("6. Common problems",
                                    "Colours look white / washed out and S is near 0 → the camera is over-exposed. Lower Exposure or Gain in Camera Settings.",
                                    "Everything is dark and V is low → raise Exposure, or lower V min.",
                                    "Two colours overlap in Hue (e.g. orange vs yellow) → narrow both ranges and give each its own group.",
                                    "Reflections from the housing count as lit pixels → shrink the ROI radius or raise Area Threshold.",
                                    "Result flickers between PASS and NG → enable Persist with 3 ~ 5 frames."),
                            }
                        }
                    },
                    {
                        "TH", new HelpText
                        {
                            WindowTitle = "คู่มือ HSV",
                            Heading = "การแยกสีด้วย HSV",
                            CloseLabel = "ปิด",
                            Sections = new[]
                            {
                                new Section("1. HSV คืออะไร",
                                    "กล้องส่งพิกเซลมาเป็นค่า แดง / เขียว / น้ำเงิน (RGB) แอปจะแปลงทุกเฟรมเป็น HSV ก่อนตรวจ",
                                    "H (Hue) = โทนสี เช่น แดง เหลือง เขียว ฟ้า น้ำเงิน ม่วง โดยไม่ขึ้นกับความสว่าง",
                                    "S (Saturation) = ความสดของสี 0 = เทา / ขาว, 255 = สีสดเต็มที่",
                                    "V (Value) = ความสว่างของพิกเซล 0 = ดำ, 255 = สว่างที่สุด",
                                    "ที่ใช้ HSV เพราะค่า Hue ของ LED แทบไม่เปลี่ยนเมื่อแสงหรือ exposure เปลี่ยน ในขณะที่ค่า RGB จะเปลี่ยนไปพร้อมกันทั้งหมด"),
                                new Section("2. ช่วงค่าที่แอปใช้ (สเกลของ OpenCV)",
                                    "H : 0 ~ 179 (ครึ่งหนึ่งของวงล้อสี 0 ~ 359°)  S : 0 ~ 255  V : 0 ~ 255",
                                    "ค่า Hue โดยประมาณของสี LED ที่พบบ่อย: แดง 0 (และ 175 ~ 179), ส้ม 10 ~ 20, เหลือง 25 ~ 35, เขียว 55 ~ 75, ฟ้า 85 ~ 95, น้ำเงิน 110 ~ 130, ม่วง / ชมพู 145 ~ 165",
                                    "สีแดงอยู่ทั้งสองปลายของสเกล แอปไม่วนค่า ให้เลือกด้านเดียว: 0 ~ 10 หรือ 170 ~ 179 ตามที่ฮิสโทแกรมแสดง"),
                                new Section("3. การตรวจทำงานอย่างไร",
                                    "ในแต่ละ ROI แอปจะเก็บเฉพาะพิกเซลที่ค่า H, S และ V ทั้งสามอยู่ในช่วง Set ของกลุ่มนั้น (inRange → mask)",
                                    "จากนั้นนับจำนวนพิกเซลที่เหลือ (พื้นที่สว่าง) ถ้ามากกว่า Area Threshold ถือว่า LED ติดและสีถูกต้อง → PASS ไม่เช่นนั้น → NG",
                                    "Persist (ใน Inspection Settings) ต้องให้ผลคงที่ N เฟรมติดกันก่อนจึงเปลี่ยน PASS / NG เพื่อกันผลกะพริบ"),
                                new Section("4. อ่านค่าบนแผง",
                                    "Set : ช่วง min ~ max ของกลุ่มที่ติ๊กอยู่ คลิกช่องตัวเลขเพื่อเลือกค่าจากตัวเลือกสี",
                                    "แถววงกลมเส้นประ : ค่า min ~ max ที่วัดได้จาก ROI ที่เลือกอยู่ (ROI ที่มีเส้นขอบประบนภาพ เส้นสีส้มในฮิสโทแกรม)",
                                    "แถววงกลมเส้นทึบ : ค่า min ~ max ที่วัดได้จาก ROI ที่ยังไม่ได้เลือกของกลุ่ม (เส้นสีน้ำเงินในฮิสโทแกรม)",
                                    "แถว All : ค่า min ~ max ที่วัดได้จาก ROI ทั้งหมดของกลุ่ม (แสดงเฉพาะตัวเลข ไม่วาดในฮิสโทแกรม)",
                                    "แท็บฮิสโทแกรม H / S / V : จำนวนพิกเซลของ ROI ที่เลือกในแต่ละค่า ยอดสูงคือสีของ LED ให้ตั้ง min และ max เผื่อไว้เล็กน้อยสองข้างของยอดนั้น"),
                                new Section("5. วิธีตั้งช่วงค่า",
                                    "1) เปิด LED วาด ROI แล้วเปิดแท็บ H ตั้ง Hue min / max ล้อมรอบยอดหลัก (ประมาณ ±8)",
                                    "2) แท็บ S: LED ที่ติดจะมีสีสด เริ่มที่ S min ≈ 80 ~ 120 และ S max = 255 ยกเว้น LED สีขาวที่ S ต่ำ ให้ใช้ 0 ~ 60",
                                    "3) แท็บ V: LED ที่ติดจะสว่าง ตั้ง V min ≈ 100 ~ 150 และ V max = 255 ค่า V min คือตัวคัด LED ที่ดับออก",
                                    "4) ปิด LED แล้วตรวจว่าพื้นที่สว่างลดต่ำกว่า Area Threshold ถ้าไม่ ให้เพิ่ม V min หรือ Area Threshold",
                                    "5) ตรวจว่าสีอื่นในกลุ่มเดียวกันไม่ตกอยู่ในช่วง Hue นี้ ถ้าตกให้บีบช่วงให้แคบลง"),
                                new Section("6. ปัญหาที่พบบ่อย",
                                    "สีดูขาว / ซีด และ S ใกล้ 0 → กล้องรับแสงมากเกินไป ให้ลด Exposure หรือ Gain ใน Camera Settings",
                                    "ภาพมืดทั้งหมดและ V ต่ำ → เพิ่ม Exposure หรือลด V min",
                                    "สองสีมี Hue ทับกัน (เช่น ส้มกับเหลือง) → บีบช่วงของทั้งคู่ให้แคบและแยกคนละกลุ่ม",
                                    "แสงสะท้อนจากตัวเครื่องถูกนับเป็นพิกเซลสว่าง → ลดรัศมี ROI หรือเพิ่ม Area Threshold",
                                    "ผลกะพริบสลับ PASS / NG → เปิด Persist ที่ 3 ~ 5 เฟรม"),
                            }
                        }
                    },
                }
            },
        };

        public RoiHelpWindow() : this("roi")
        {
        }

        public RoiHelpWindow(string topic)
        {
            InitializeComponent();
            this.topic = texts.ContainsKey(topic) ? topic : "roi";
            Render();
        }

        private void LangEn_Click(object sender, RoutedEventArgs e)
        {
            currentLang = "EN";
            Render();
        }

        private void LangTh_Click(object sender, RoutedEventArgs e)
        {
            currentLang = "TH";
            Render();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // Dựng lại toàn bộ nội dung theo chủ đề + ngôn ngữ đang chọn
        private void Render()
        {
            var byLang = texts[topic];
            HelpText t;
            if (!byLang.TryGetValue(currentLang, out t)) t = byLang["EN"];

            Title = t.WindowTitle;
            titleText.Text = t.Heading;
            btnClose.Content = t.CloseLabel;
            btnEn.Style = (Style)FindResource(currentLang == "EN" ? "LangBtnActive" : "LangBtn");
            btnTh.Style = (Style)FindResource(currentLang == "TH" ? "LangBtnActive" : "LangBtn");

            contentPanel.Children.Clear();
            var titleBrush = new SolidColorBrush(Color.FromRgb(0x32, 0x3F, 0x4E));
            var bodyBrush = new SolidColorBrush(Color.FromRgb(0x2B, 0x2B, 0x2B));

            if (topic == "hsv")
            {
                contentPanel.Children.Add(BuildHueBar());
            }

            foreach (var sec in t.Sections)
            {
                contentPanel.Children.Add(new TextBlock
                {
                    Text = sec.Title,
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Foreground = titleBrush,
                    Margin = new Thickness(0, 10, 0, 4),
                });

                foreach (var line in sec.Lines)
                {
                    var tb = new TextBlock
                    {
                        FontSize = 13,
                        Foreground = bodyBrush,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(12, 1, 0, 1),
                    };
                    tb.Inlines.Add(new Run("•  "));
                    tb.Inlines.Add(new Run(line));
                    contentPanel.Children.Add(tb);
                }
            }
        }

        // Thanh màu Hue 0 → 179 (đỏ → vàng → lục → cyan → xanh → magenta → đỏ) kèm vạch số theo thang OpenCV
        private static UIElement BuildHueBar()
        {
            var panel = new StackPanel { Margin = new Thickness(0, 6, 0, 4) };

            var gradient = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
            gradient.GradientStops.Add(new GradientStop(Color.FromRgb(255, 0, 0), 0.0));       // H 0    red
            gradient.GradientStops.Add(new GradientStop(Color.FromRgb(255, 255, 0), 1.0 / 6)); // H 30   yellow
            gradient.GradientStops.Add(new GradientStop(Color.FromRgb(0, 255, 0), 2.0 / 6));   // H 60   green
            gradient.GradientStops.Add(new GradientStop(Color.FromRgb(0, 255, 255), 3.0 / 6)); // H 90   cyan
            gradient.GradientStops.Add(new GradientStop(Color.FromRgb(0, 0, 255), 4.0 / 6));   // H 120  blue
            gradient.GradientStops.Add(new GradientStop(Color.FromRgb(255, 0, 255), 5.0 / 6)); // H 150  magenta
            gradient.GradientStops.Add(new GradientStop(Color.FromRgb(255, 0, 0), 1.0));       // H 179  red

            panel.Children.Add(new Border
            {
                Height = 22,
                Background = gradient,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xC9, 0xD1, 0xDB)),
                BorderThickness = new Thickness(1),
            });

            var ticks = new Grid { Margin = new Thickness(0, 2, 0, 0) };
            string[] labels = { "0", "30", "60", "90", "120", "150", "179" };
            for (int i = 0; i < labels.Length; i++)
            {
                ticks.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var tb = new TextBlock
                {
                    Text = labels[i],
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x6B, 0x7A, 0x8C)),
                    HorizontalAlignment = i == 0 ? HorizontalAlignment.Left : (i == labels.Length - 1 ? HorizontalAlignment.Right : HorizontalAlignment.Center),
                };
                Grid.SetColumn(tb, i);
                ticks.Children.Add(tb);
            }
            panel.Children.Add(ticks);
            panel.Children.Add(new TextBlock
            {
                Text = "H (Hue) 0 ~ 179  ·  S (Saturation) 0 ~ 255  ·  V (Value) 0 ~ 255",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0x6B, 0x7A, 0x8C)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0),
            });
            return panel;
        }
    }
}
